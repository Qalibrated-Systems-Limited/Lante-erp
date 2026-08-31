import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import api from '../../api/axios.js'
import PlatformLayout from './PlatformLayout.jsx'

const STATUS_COLORS = {
  Active:    ['rgba(52,211,153,.1)', '#34d399'],
  Trial:     ['rgba(200,150,12,.1)', '#C8960C'],
  Suspended: ['rgba(239,68,68,.1)', '#f87171'],
  Cancelled: ['rgba(107,114,128,.1)', '#9ca3af'],
}

// Per-service schema provisioning states (Phase 2 engine)
const PROV_COLORS = {
  Provisioned:  ['rgba(52,211,153,.1)', '#34d399'],
  Provisioning: ['rgba(200,150,12,.1)', '#C8960C'],
  Pending:      ['rgba(107,114,128,.1)', '#9ca3af'],
  Failed:       ['rgba(239,68,68,.1)', '#f87171'],
}

const SERVICE_LABELS = {
  user: 'Identity / Users', ticketing: 'Ticketing', operations: 'Operations',
  fleet: 'Fleet', licensing: 'Licensing',
}

export default function PlatformCompanyDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [company, setCompany] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [editActive, setEditActive] = useState(null)
  const [provisioning, setProvisioning] = useState([])
  const [provRunning, setProvRunning] = useState(false)
  const [provError, setProvError] = useState('')
  const [retryRunning, setRetryRunning] = useState(false)

  useEffect(() => {
    api.get(`/api/v1/platform/companies/${id}`)
      .then(r => {
        const d = r.data?.data ?? r.data
        setCompany(d)
        setEditActive(d.isActive)
      })
      .catch(() => setError('Failed to load company.'))
      .finally(() => setLoading(false))
    fetchProvisioning()
  }, [id])

  function fetchProvisioning() {
    return api.get(`/api/v1/platform/companies/${id}/provisioning`)
      .then(r => setProvisioning(r.data?.data ?? r.data ?? []))
      .catch(() => { /* endpoint optional / no rows yet */ })
  }

  async function runProvision() {
    setProvError('')
    setProvRunning(true)
    // optimistic: mark everything in-flight
    setProvisioning(rows => rows.map(s => ({ ...s, status: 'Provisioning', lastError: null })))
    try {
      const r = await api.post(`/api/v1/platform/companies/${id}/provision`)
      const services = r.data?.data?.services
      if (services) setProvisioning(services)
      else await fetchProvisioning()
    } catch {
      setProvError('Provisioning request failed.')
      await fetchProvisioning()
    } finally {
      setProvRunning(false)
    }
  }

  async function retryFailed() {
    setProvError('')
    setRetryRunning(true)
    setProvisioning(rows => rows.map(s => s.status === 'Failed' ? { ...s, status: 'Provisioning', lastError: null } : s))
    try {
      const r = await api.post(`/api/v1/platform/companies/${id}/provision/retry-failed`)
      const services = r.data?.data?.services
      if (services) setProvisioning(services)
      else await fetchProvisioning()
    } catch {
      setProvError('Retry request failed.')
      await fetchProvisioning()
    } finally {
      setRetryRunning(false)
    }
  }

  async function toggleActive() {
    setSaving(true)
    try {
      await api.put(`/api/v1/platform/companies/${id}`, { isActive: !editActive })
      setEditActive(v => !v)
      setCompany(c => ({ ...c, isActive: !editActive }))
    } catch {
      setError('Failed to update company status.')
    } finally {
      setSaving(false)
    }
  }

  if (loading) return (
    <PlatformLayout>
      <div style={{ color: '#6b7280', fontSize: 14, padding: '40px 0' }}>Loading company…</div>
    </PlatformLayout>
  )

  if (error || !company) return (
    <PlatformLayout>
      <div style={{ color: '#f87171', fontSize: 14 }}>{error || 'Company not found.'}</div>
    </PlatformLayout>
  )

  const sub = company.subscription
  const [subBg, subColor] = STATUS_COLORS[sub?.status] ?? STATUS_COLORS.Cancelled

  return (
    <PlatformLayout>
      <div style={{ maxWidth: 900 }}>
        {/* Breadcrumb */}
        <button onClick={() => navigate('/platform/companies')} style={{
          fontSize: 13, fontWeight: 600, color: '#6b7280', background: 'none', border: 'none',
          cursor: 'pointer', fontFamily: "'Inter',sans-serif", marginBottom: 20,
          transition: 'color .15s',
        }}
        onMouseEnter={e => (e.currentTarget.style.color = '#1B3A5C')}
        onMouseLeave={e => (e.currentTarget.style.color = '#6b7280')}>
          ← Back to Companies
        </button>

        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 28, flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 6 }}>
              <h1 style={{ fontFamily: "'Inter',sans-serif", fontSize: 24, fontWeight: 700, color: '#1B3A5C' }}>{company.name}</h1>
              <span style={{
                fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100,
                background: company.isActive ? 'rgba(52,211,153,.1)' : 'rgba(239,68,68,.08)',
                color: company.isActive ? '#34d399' : '#f87171',
              }}>{company.isActive ? 'Active' : 'Inactive'}</span>
            </div>
            <p style={{ fontSize: 13, color: '#6b7280', fontFamily: 'monospace' }}>{company.slug}</p>
          </div>
          <button onClick={toggleActive} disabled={saving} style={{
            padding: '9px 18px', borderRadius: 10,
            background: company.isActive ? 'rgba(239,68,68,.08)' : 'rgba(52,211,153,.08)',
            color: company.isActive ? '#f87171' : '#34d399',
            border: `1px solid ${company.isActive ? 'rgba(239,68,68,.2)' : 'rgba(52,211,153,.2)'}`,
            fontSize: 13, fontWeight: 600, cursor: saving ? 'not-allowed' : 'pointer',
            fontFamily: "'Inter',sans-serif",
            opacity: saving ? .6 : 1,
          }}>
            {saving ? 'Saving…' : company.isActive ? 'Suspend Company' : 'Reactivate Company'}
          </button>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 24 }}>
          {/* Subscription card */}
          <SectionCard title="Subscription">
            {sub ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                <Row label="Plan" value={sub.name} />
                <Row label="Status" value={
                  <span style={{ fontSize: 12, fontWeight: 700, padding: '2px 8px', borderRadius: 100, background: subBg, color: subColor }}>{sub.status}</span>
                } />
                <Row label="Billing" value={sub.billingCycle} />
                <Row label="Started" value={sub.startDate ? new Date(sub.startDate).toLocaleDateString() : '—'} />
                {sub.trialEndsAt && <Row label="Trial ends" value={new Date(sub.trialEndsAt).toLocaleDateString()} />}
                {sub.endDate && <Row label="Ends" value={new Date(sub.endDate).toLocaleDateString()} />}
              </div>
            ) : (
              <p style={{ fontSize: 14, color: '#4b5563' }}>No subscription.</p>
            )}
          </SectionCard>

          {/* Stats card */}
          <SectionCard title="At a Glance">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              {[
                { label: 'Branches', val: company.branchCount ?? 0, color: '#60a5fa' },
                { label: 'Users',    val: company.userCount   ?? 0, color: '#34d399' },
              ].map(s => (
                <div key={s.label} style={{ background: '#FFFFFF', borderRadius: 10, padding: '14px 16px' }}>
                  <p style={{ fontSize: 11, fontWeight: 700, color: '#4b5563', textTransform: 'uppercase', letterSpacing: '1px', marginBottom: 6 }}>{s.label}</p>
                  <p style={{ fontFamily: "'Inter',sans-serif", fontSize: 28, fontWeight: 700, color: s.color, lineHeight: 1 }}>{s.val}</p>
                </div>
              ))}
            </div>
          </SectionCard>
        </div>

        {/* Schema Provisioning (Phase 2) */}
        <div style={{ background: '#FFFFFF', border: '1px solid #E8ECF0', borderRadius: 14, overflow: 'hidden', marginBottom: 16 }}>
          <div style={{ padding: '14px 20px', borderBottom: '1px solid #E8ECF0', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
            <div>
              <h2 style={{ fontSize: 13, fontWeight: 700, color: '#6b7280', textTransform: 'uppercase', letterSpacing: '1px' }}>Schema Provisioning</h2>
              {provisioning.length > 0 && (
                <p style={{ fontSize: 12, color: '#4b5563', marginTop: 4 }}>
                  {provisioning.filter(s => s.status === 'Provisioned').length}/{provisioning.length} services provisioned
                </p>
              )}
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              {provisioning.some(s => s.status === 'Failed') && (
                <button onClick={retryFailed} disabled={provRunning || retryRunning} style={{
                  padding: '8px 16px', borderRadius: 10,
                  background: retryRunning ? 'rgba(239,68,68,.06)' : 'rgba(239,68,68,.1)',
                  color: '#f87171', border: '1px solid rgba(239,68,68,.25)',
                  fontSize: 13, fontWeight: 600, cursor: (provRunning || retryRunning) ? 'not-allowed' : 'pointer',
                  fontFamily: "'Inter',sans-serif", opacity: (provRunning || retryRunning) ? .6 : 1,
                }}>
                  {retryRunning ? 'Retrying…' : 'Retry Failed'}
                </button>
              )}
              <button onClick={runProvision} disabled={provRunning || retryRunning} style={{
                padding: '8px 16px', borderRadius: 10,
                background: provRunning ? 'rgba(200,150,12,.08)' : 'rgba(200,150,12,.12)',
                color: '#C8960C', border: '1px solid rgba(200,150,12,.25)',
                fontSize: 13, fontWeight: 600, cursor: (provRunning || retryRunning) ? 'not-allowed' : 'pointer',
                fontFamily: "'Inter',sans-serif", opacity: (provRunning || retryRunning) ? .6 : 1,
              }}>
                {provRunning ? 'Provisioning…' : provisioning.some(s => s.status === 'Provisioned') ? 'Re-provision' : 'Provision Schemas'}
              </button>
            </div>
          </div>
          <div style={{ padding: '18px 20px' }}>
            {provError && <p style={{ fontSize: 13, color: '#f87171', marginBottom: 12 }}>{provError}</p>}
            {provisioning.length === 0 ? (
              <p style={{ fontSize: 14, color: '#4b5563' }}>
                Not provisioned yet. Click “Provision Schemas” to create this tenant’s per-service database schemas.
              </p>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {provisioning.map(s => {
                  const [bg, color] = PROV_COLORS[s.status] ?? PROV_COLORS.Pending
                  return (
                    <div key={s.serviceKey} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 12px', background: '#FFFFFF', borderRadius: 10 }}>
                      <div style={{ flex: '0 0 150px' }}>
                        <p style={{ fontSize: 13, fontWeight: 600, color: '#1B3A5C' }}>{SERVICE_LABELS[s.serviceKey] ?? s.serviceKey}</p>
                        <p style={{ fontSize: 11, color: '#4b5563', fontFamily: 'monospace' }}>{s.schemaName || '—'}</p>
                      </div>
                      <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100, background: bg, color, flexShrink: 0 }}>{s.status}</span>
                      {s.status === 'Failed' && s.retryCount >= 5 && (
                        <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100, background: 'rgba(239,68,68,.1)', color: '#f87171', flexShrink: 0 }} title="The automatic retry sweep has given up after 5 attempts — needs manual retry or investigation.">
                          Needs attention
                        </span>
                      )}
                      {s.status === 'Failed' && s.retryCount > 0 && (
                        <span style={{ fontSize: 11, color: '#4b5563', flexShrink: 0 }}>{s.retryCount} {s.retryCount === 1 ? 'retry' : 'retries'}</span>
                      )}
                      <span style={{ flex: 1, fontSize: 12, color: '#f87171', fontFamily: 'monospace', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={s.lastError || ''}>
                        {s.lastError || ''}
                      </span>
                    </div>
                  )
                })}
              </div>
            )}
          </div>
        </div>

        {/* Branches */}
        <SectionCard title="Branches" style={{ marginBottom: 16 }}>
          {(company.branches ?? []).length === 0 ? (
            <p style={{ fontSize: 14, color: '#4b5563' }}>No branches found.</p>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr>
                  {['Name', 'Code', 'Head Office', 'Status'].map(h => (
                    <th key={h} style={{ padding: '8px 16px', textAlign: 'left', fontSize: 11, fontWeight: 700, color: '#4b5563', textTransform: 'uppercase', letterSpacing: '1px' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {company.branches.map((b, i) => (
                  <tr key={b.id} style={{ borderTop: i > 0 ? '1px solid #FFFFFF' : 'none' }}>
                    <td style={{ padding: '10px 16px', fontSize: 14, fontWeight: 500, color: '#1B3A5C' }}>{b.name}</td>
                    <td style={{ padding: '10px 16px', fontSize: 13, color: '#6b7280', fontFamily: 'monospace' }}>{b.code}</td>
                    <td style={{ padding: '10px 16px' }}>
                      {b.isHeadOffice && <span style={{ fontSize: 11, fontWeight: 700, padding: '2px 8px', borderRadius: 100, background: 'rgba(200,150,12,.1)', color: '#C8960C' }}>HQ</span>}
                    </td>
                    <td style={{ padding: '10px 16px' }}>
                      <span style={{ fontSize: 11, fontWeight: 700, padding: '2px 8px', borderRadius: 100, background: b.isActive ? 'rgba(52,211,153,.1)' : 'rgba(239,68,68,.08)', color: b.isActive ? '#34d399' : '#f87171' }}>
                        {b.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </SectionCard>

        {/* Admins */}
        <SectionCard title="Company Admins">
          {(company.admins ?? []).length === 0 ? (
            <p style={{ fontSize: 14, color: '#4b5563' }}>No admins found.</p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {company.admins.map(admin => (
                <div key={admin.id} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 0', borderBottom: '1px solid #FFFFFF' }}>
                  <div style={{
                    width: 36, height: 36, borderRadius: '50%',
                    background: 'linear-gradient(135deg,#C8960C,#E8B84D)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 13, fontWeight: 700, color: '#000', flexShrink: 0,
                  }}>{(admin.firstName?.[0] ?? '') + (admin.lastName?.[0] ?? '')}</div>
                  <div>
                    <p style={{ fontSize: 14, fontWeight: 600, color: '#1B3A5C' }}>{admin.firstName} {admin.lastName}</p>
                    <p style={{ fontSize: 12, color: '#6b7280' }}>{admin.email}</p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </SectionCard>
      </div>
    </PlatformLayout>
  )
}

function SectionCard({ title, children, style }) {
  return (
    <div style={{ background: '#FFFFFF', border: '1px solid #E8ECF0', borderRadius: 14, overflow: 'hidden', marginBottom: 16, ...style }}>
      <div style={{ padding: '14px 20px', borderBottom: '1px solid #E8ECF0' }}>
        <h2 style={{ fontSize: 13, fontWeight: 700, color: '#6b7280', textTransform: 'uppercase', letterSpacing: '1px' }}>{title}</h2>
      </div>
      <div style={{ padding: '18px 20px' }}>
        {children}
      </div>
    </div>
  )
}

function Row({ label, value }) {
  return (
    <div style={{ display: 'flex', gap: 12, fontSize: 13 }}>
      <span style={{ color: '#6b7280', width: 80, flexShrink: 0 }}>{label}</span>
      <span style={{ color: '#1B3A5C', fontWeight: 500 }}>{value}</span>
    </div>
  )
}
