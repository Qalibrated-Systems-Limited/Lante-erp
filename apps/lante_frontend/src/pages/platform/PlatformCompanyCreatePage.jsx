import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import PlatformLayout from './PlatformLayout.jsx'
import { BASE_DOMAIN, isValidSlug, isReservedSlug } from '../../utils/subdomain.js'

const STEPS = ['Company Details', 'HQ Branch', 'Admin Account', 'Plan & Review']

// Business services that can be provisioned per company (keys match backend PlatformServices.Business).
// "Identity / Users" is always provisioned and isn't shown here.
const SERVICES = [
  { key: 'ticketing',  label: 'Ticketing',  desc: 'Support tickets & service requests' },
  { key: 'operations', label: 'Operations', desc: 'Projects, assignments & lab data' },
  { key: 'fleet',      label: 'Fleet',      desc: 'Vehicles, trips & drivers' },
  { key: 'licensing',  label: 'Licensing',  desc: 'Device licenses' },
  { key: 'stores',     label: 'Stores',     desc: 'Suppliers, inventory & stock management' },
]

function parseFeatures(featuresJson) {
  try {
    const arr = JSON.parse(featuresJson || '[]')
    return Array.isArray(arr) ? arr.filter(s => SERVICES.some(x => x.key === s)) : []
  } catch { return [] }
}

export default function PlatformCompanyCreatePage() {
  const navigate = useNavigate()
  const [step, setStep] = useState(0)
  const [plans, setPlans] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const [form, setForm] = useState({
    companyName: '', slug: '',
    hqBranchName: '', hqBranchCode: '',
    adminFirstName: '', adminLastName: '', adminEmail: '', adminPhone: '',
    planId: '', services: [],
  })

  function toggleService(key) {
    setForm(f => ({
      ...f,
      services: f.services.includes(key) ? f.services.filter(s => s !== key) : [...f.services, key],
    }))
  }

  useEffect(() => {
    api.get('/api/v1/platform/plans')
      .then(r => setPlans(r.data?.data ?? r.data))
      .catch(console.error)
  }, [])

  function set(field) {
    return e => setForm(f => ({ ...f, [field]: e.target.value }))
  }

  function autoSlug(name) {
    return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '')
  }

  function next() { setError(''); setStep(s => Math.min(s + 1, STEPS.length - 1)) }
  function back() { setStep(s => Math.max(s - 1, 0)) }

  const slugError = !form.slug
    ? ''
    : !isValidSlug(form.slug)
    ? 'Use 2–50 lowercase letters, digits and single hyphens, starting with a letter.'
    : isReservedSlug(form.slug)
    ? `"${form.slug}" is reserved and can't be used as a subdomain.`
    : ''

  async function handleSubmit() {
    setError('')
    setLoading(true)
    try {
      await api.post('/api/v1/platform/companies', form)
      navigate('/platform/companies')
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to create company.')
    } finally {
      setLoading(false)
    }
  }

  const inputStyle = {
    width: '100%', height: 44, padding: '0 14px',
    fontSize: 14, fontFamily: "'Inter',sans-serif",
    border: '1.5px solid #E8ECF0', borderRadius: 10,
    background: '#FFFFFF', color: '#1B3A5C', outline: 'none',
    transition: 'border-color .2s',
    boxSizing: 'border-box',
  }
  const labelStyle = { display: 'block', fontSize: 13, fontWeight: 600, color: '#9ca3af', marginBottom: 7, letterSpacing: '.3px' }

  return (
    <PlatformLayout>
      <div style={{ maxWidth: 640 }}>
        <button onClick={() => navigate('/platform/companies')} style={{
          display: 'flex', alignItems: 'center', gap: 6,
          fontSize: 14, fontWeight: 600, color: '#6b7280',
          background: 'none', border: 'none', cursor: 'pointer',
          fontFamily: "'Inter',sans-serif", marginBottom: 24,
          transition: 'color .15s',
        }}
        onMouseEnter={e => (e.currentTarget.style.color = '#1B3A5C')}
        onMouseLeave={e => (e.currentTarget.style.color = '#6b7280')}>
          ← Back to Companies
        </button>

        <h1 style={{ fontFamily: "'Inter',sans-serif", fontSize: 24, fontWeight: 700, color: '#1B3A5C', marginBottom: 8 }}>
          Add New Company
        </h1>
        <p style={{ fontSize: 14, color: '#6b7280', marginBottom: 32 }}>
          Set up a new tenant company, HQ branch, and their admin account in one flow.
        </p>

        {/* Step bar */}
        <div style={{ display: 'flex', gap: 8, marginBottom: 36 }}>
          {STEPS.map((label, i) => (
            <div key={i} style={{ flex: 1 }}>
              <div style={{
                height: 4, borderRadius: 99,
                background: i <= step ? 'linear-gradient(135deg,#C8960C,#E8B84D)' : '#E8ECF0',
                transition: 'background .3s',
              }}/>
              <p style={{ fontSize: 11, fontWeight: 600, color: i === step ? '#C8960C' : '#4b5563', marginTop: 6, transition: 'color .3s' }}>
                {label}
              </p>
            </div>
          ))}
        </div>

        <div style={{
          background: '#FFFFFF', border: '1px solid #E8ECF0',
          borderRadius: 16, padding: '28px 28px',
        }}>
          {error && (
            <div style={{
              display: 'flex', alignItems: 'flex-start', gap: 10,
              background: 'rgba(239,68,68,.08)', border: '1px solid rgba(239,68,68,.2)',
              color: '#f87171', borderRadius: 10, padding: '12px 14px',
              fontSize: 14, marginBottom: 24,
            }}>
              {error}
            </div>
          )}

          {/* Step 0 — Company Details */}
          {step === 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
              <h2 style={{ fontSize: 16, fontWeight: 700, color: '#1B3A5C', marginBottom: 4 }}>Company Details</h2>
              <div>
                <label style={labelStyle}>COMPANY NAME</label>
                <input style={inputStyle} value={form.companyName}
                  onChange={e => { set('companyName')(e); setForm(f => ({ ...f, slug: autoSlug(e.target.value) })) }}
                  placeholder="Acme Corporation Ltd" />
              </div>
              <div>
                <label style={labelStyle}>SUBDOMAIN <span style={{ color: '#4b5563', fontWeight: 400, textTransform: 'none', letterSpacing: 0 }}>(the company's login address)</span></label>
                <input style={{
                    ...inputStyle, fontFamily: 'monospace',
                    borderColor: slugError ? 'rgba(239,68,68,.5)' : '#E8ECF0',
                  }}
                  value={form.slug}
                  onChange={e => setForm(f => ({ ...f, slug: e.target.value.toLowerCase() }))}
                  placeholder="acme-corporation" />
                <p style={{ fontSize: 12, marginTop: 7, fontFamily: 'monospace', color: slugError ? '#f87171' : '#6b7280' }}>
                  {slugError || <>https://<span style={{ color: '#C8960C' }}>{form.slug || 'your-company'}</span>.{BASE_DOMAIN}</>}
                </p>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', paddingTop: 8 }}>
                <NextBtn onClick={next} disabled={!form.companyName || !form.slug || !!slugError} />
              </div>
            </div>
          )}

          {/* Step 1 — HQ Branch */}
          {step === 1 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
              <h2 style={{ fontSize: 16, fontWeight: 700, color: '#1B3A5C', marginBottom: 4 }}>Head Office Branch</h2>
              <p style={{ fontSize: 13, color: '#6b7280', marginBottom: 4 }}>The primary branch — additional branches can be added later from Settings.</p>
              <div>
                <label style={labelStyle}>BRANCH NAME</label>
                <input style={inputStyle} value={form.hqBranchName} onChange={set('hqBranchName')} placeholder="Nairobi HQ" />
              </div>
              <div>
                <label style={labelStyle}>BRANCH CODE <span style={{ color: '#4b5563', fontWeight: 400, textTransform: 'none', letterSpacing: 0 }}>(short code, e.g. NBI)</span></label>
                <input style={{ ...inputStyle, fontFamily: 'monospace', textTransform: 'uppercase' }}
                  value={form.hqBranchCode} onChange={e => setForm(f => ({ ...f, hqBranchCode: e.target.value.toUpperCase() }))}
                  placeholder="NBI" maxLength={6} />
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', paddingTop: 8 }}>
                <BackBtn onClick={back} />
                <NextBtn onClick={next} disabled={!form.hqBranchName || !form.hqBranchCode} />
              </div>
            </div>
          )}

          {/* Step 2 — Admin Account */}
          {step === 2 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
              <h2 style={{ fontSize: 16, fontWeight: 700, color: '#1B3A5C', marginBottom: 4 }}>Company Administrator</h2>
              <p style={{ fontSize: 13, color: '#6b7280', marginBottom: 4 }}>A welcome email with a temporary password will be sent to this admin.</p>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                <div>
                  <label style={labelStyle}>FIRST NAME</label>
                  <input style={inputStyle} value={form.adminFirstName} onChange={set('adminFirstName')} placeholder="Jane" />
                </div>
                <div>
                  <label style={labelStyle}>LAST NAME</label>
                  <input style={inputStyle} value={form.adminLastName} onChange={set('adminLastName')} placeholder="Doe" />
                </div>
              </div>
              <div>
                <label style={labelStyle}>EMAIL</label>
                <input type="email" style={inputStyle} value={form.adminEmail} onChange={set('adminEmail')} placeholder="admin@company.com" />
              </div>
              <div>
                <label style={labelStyle}>PHONE</label>
                <input type="tel" style={inputStyle} value={form.adminPhone} onChange={set('adminPhone')} placeholder="+254 700 000 000" />
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', paddingTop: 8 }}>
                <BackBtn onClick={back} />
                <NextBtn onClick={next} disabled={!form.adminFirstName || !form.adminLastName || !form.adminEmail} />
              </div>
            </div>
          )}

          {/* Step 3 — Plan & Review */}
          {step === 3 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
              <h2 style={{ fontSize: 16, fontWeight: 700, color: '#1B3A5C', marginBottom: 4 }}>Plan & Review</h2>

              {/* Plan selection */}
              <div>
                <label style={labelStyle}>SUBSCRIPTION PLAN</label>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  {plans.filter(p => p.isActive).map(p => (
                    <label key={p.id} style={{
                      display: 'flex', alignItems: 'center', gap: 14,
                      padding: '14px 16px', borderRadius: 12, cursor: 'pointer',
                      border: `1.5px solid ${form.planId === p.id ? '#C8960C' : '#E8ECF0'}`,
                      background: form.planId === p.id ? 'rgba(200,150,12,.06)' : '#FFFFFF',
                      transition: 'all .15s',
                    }}>
                      <input type="radio" name="plan" value={p.id} checked={form.planId === p.id}
                        onChange={() => setForm(f => ({ ...f, planId: p.id, services: parseFeatures(p.featuresJson) }))}
                        style={{ accentColor: '#C8960C' }} />
                      <div>
                        <p style={{ fontSize: 14, fontWeight: 700, color: '#1B3A5C', marginBottom: 2 }}>{p.name}</p>
                        <p style={{ fontSize: 12, color: '#6b7280' }}>
                          {p.priceMonthly === 0 ? 'Free' : `KES ${p.priceMonthly.toLocaleString()}/mo`}
                          {' · '}{p.maxBranches === -1 ? 'Unlimited' : p.maxBranches} branches
                          {' · '}{p.maxUsers === -1 ? 'Unlimited' : p.maxUsers} users
                        </p>
                      </div>
                    </label>
                  ))}
                </div>
              </div>

              {/* Services selector */}
              <div>
                <label style={labelStyle}>SERVICES <span style={{ color: '#4b5563', fontWeight: 400, textTransform: 'none', letterSpacing: 0 }}>(schemas provisioned for this company · Users is always included)</span></label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                  {SERVICES.map(s => {
                    const on = form.services.includes(s.key)
                    return (
                      <label key={s.key} style={{
                        display: 'flex', alignItems: 'flex-start', gap: 10,
                        padding: '12px 14px', borderRadius: 12, cursor: 'pointer',
                        border: `1.5px solid ${on ? '#C8960C' : '#E8ECF0'}`,
                        background: on ? 'rgba(200,150,12,.06)' : '#FFFFFF',
                        transition: 'all .15s',
                      }}>
                        <input type="checkbox" checked={on} onChange={() => toggleService(s.key)}
                          style={{ accentColor: '#C8960C', marginTop: 2 }} />
                        <div>
                          <p style={{ fontSize: 13, fontWeight: 700, color: '#1B3A5C', marginBottom: 2 }}>{s.label}</p>
                          <p style={{ fontSize: 11, color: '#6b7280' }}>{s.desc}</p>
                        </div>
                      </label>
                    )
                  })}
                </div>
                <p style={{ fontSize: 12, color: '#6b7280', marginTop: 8 }}>
                  Defaults to the selected plan’s services — adjust as needed.
                </p>
              </div>

              {/* Summary */}
              <div style={{ background: '#FFFFFF', border: '1px solid #E8ECF0', borderRadius: 12, padding: '16px 18px' }}>
                <p style={{ fontSize: 12, fontWeight: 700, color: '#4b5563', textTransform: 'uppercase', letterSpacing: '1px', marginBottom: 14 }}>Summary</p>
                {[
                  ['Company',   form.companyName],
                  ['Subdomain', `${form.slug}.${BASE_DOMAIN}`],
                  ['HQ Branch', `${form.hqBranchName} (${form.hqBranchCode})`],
                  ['Admin',     `${form.adminFirstName} ${form.adminLastName} · ${form.adminEmail}`],
                  ['Plan',      plans.find(p => p.id === form.planId)?.name ?? 'None selected'],
                  ['Services',  ['Users', ...form.services.map(k => SERVICES.find(s => s.key === k)?.label ?? k)].join(', ')],
                ].map(([key, val]) => (
                  <div key={key} style={{ display: 'flex', gap: 12, marginBottom: 8, fontSize: 13 }}>
                    <span style={{ color: '#6b7280', width: 90, flexShrink: 0 }}>{key}</span>
                    <span style={{ color: '#1B3A5C', fontWeight: 500 }}>{val}</span>
                  </div>
                ))}
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', paddingTop: 4 }}>
                <BackBtn onClick={back} />
                <button onClick={handleSubmit} disabled={loading} style={{
                  padding: '10px 28px', borderRadius: 10,
                  background: 'linear-gradient(135deg,#C8960C,#E8B84D)',
                  color: '#000', fontSize: 14, fontWeight: 700,
                  border: 'none', cursor: loading ? 'not-allowed' : 'pointer',
                  fontFamily: "'Inter',sans-serif",
                  opacity: loading ? .6 : 1,
                  boxShadow: '0 4px 14px rgba(200,150,12,.25)',
                  display: 'flex', alignItems: 'center', gap: 8,
                }}>
                  {loading ? 'Creating…' : 'Create Company →'}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </PlatformLayout>
  )
}

function NextBtn({ onClick, disabled }) {
  return (
    <button onClick={onClick} disabled={disabled} style={{
      padding: '10px 24px', borderRadius: 10,
      background: disabled ? '#E8ECF0' : 'linear-gradient(135deg,#C8960C,#E8B84D)',
      color: disabled ? '#4b5563' : '#000', fontSize: 14, fontWeight: 700,
      border: 'none', cursor: disabled ? 'not-allowed' : 'pointer',
      fontFamily: "'Inter',sans-serif",
      transition: 'all .15s',
    }}>Continue →</button>
  )
}

function BackBtn({ onClick }) {
  return (
    <button onClick={onClick} style={{
      padding: '10px 20px', borderRadius: 10,
      background: '#F0F4F8', color: '#9ca3af', fontSize: 14, fontWeight: 600,
      border: '1px solid #E8ECF0', cursor: 'pointer',
      fontFamily: "'Inter',sans-serif",
      transition: 'all .15s',
    }}>← Back</button>
  )
}
