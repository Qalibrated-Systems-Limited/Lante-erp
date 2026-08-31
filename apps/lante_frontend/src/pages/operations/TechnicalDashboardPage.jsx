import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import Chart from 'react-apexcharts'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Kpi, KPI_GRID } from '../../components/ui.jsx'
import { listServiceRequests, listReferenceStandards } from '../../services/operations.js'

// Technical Department dashboard — mirrors the Helpdesk dashboard layout. Covers the technical
// cluster: service-request pipeline + calibration reference-standard traceability. Metrics are
// derived client-side from the list endpoints (there is no aggregate API).

const PAD = 'clamp(16px, 2.4vw, 26px)'
const CHART_PALETTE = [T.navy, T.gold, T.green, T.blue, T.amber, T.purple, T.red, T.navyL]

const SR_STATUS_ORDER = ['Submitted', 'UnderReview', 'QuotationDraft', 'QuotationSent', 'QuotationApproved', 'InProgress', 'Completed', 'QuotationRejected', 'Rejected', 'Cancelled']
const SR_CLOSED = ['Completed', 'Rejected', 'Cancelled']
const SR_VARIANT = {
  Submitted: 'blue', UnderReview: 'amber', QuotationDraft: 'purple', QuotationSent: 'blue',
  QuotationApproved: 'green', InProgress: 'amber', Completed: 'green',
  QuotationRejected: 'red', Rejected: 'red', Cancelled: 'default',
}
const FORM_TYPES = ['SRF', 'CRF_NAWI', 'CRF_MASS']
const FORM_LABEL = { SRF: 'Service (SRF)', CRF_NAWI: 'Calibration — NAWI', CRF_MASS: 'Calibration — Mass' }

const label = (s) => s.replace(/([A-Z])/g, ' $1').trim()
const dueSoonOrExpired = (s) => {
  if (s?.isExpired) return true
  if (!s?.nextDueDate) return false
  return Math.ceil((new Date(s.nextDueDate) - new Date()) / 86400000) <= 60
}
function timeAgo(dt) {
  if (!dt) return '—'
  const m = Math.floor((Date.now() - new Date(dt).getTime()) / 60000)
  if (m < 2) return 'just now'
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ago`
  return `${Math.floor(h / 24)}d ago`
}

export default function TechnicalDashboardPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState('overview')

  const [srs, setSrs]           = useState([])
  const [srTotal, setSrTotal]   = useState(0)
  const [standards, setStandards] = useState([])
  const [loading, setLoading]   = useState(true)
  const [refreshedAt, setRefreshedAt] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [srRes, rsRes] = await Promise.allSettled([
        listServiceRequests({ page: 1, pageSize: 200 }),
        listReferenceStandards({ page: 1, pageSize: 200 }),
      ])
      if (srRes.status === 'fulfilled') {
        setSrs(srRes.value?.data ?? srRes.value?.items ?? [])
        setSrTotal(srRes.value?.total ?? (srRes.value?.data ?? []).length)
      }
      if (rsRes.status === 'fulfilled') setStandards(rsRes.value?.items ?? rsRes.value ?? [])
      setRefreshedAt(new Date())
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  const dash = (v) => (loading ? '…' : (v ?? 0))

  const byStatus     = countBy(srs, s => s.status)
  const byForm       = countBy(srs, s => s.formType)
  const openSrs      = srs.filter(s => !SR_CLOSED.includes(s.status)).length
  const awaitingRev  = (byStatus['Submitted'] ?? 0) + (byStatus['UnderReview'] ?? 0)
  const quotesSent   = byStatus['QuotationSent'] ?? 0
  const activeStds   = standards.filter(s => s.status !== 'Retired').length
  const dueStds      = standards.filter(dueSoonOrExpired).length

  const statusEntries = SR_STATUS_ORDER.filter(s => (byStatus[s] ?? 0) > 0)
  const formCounts    = FORM_TYPES.map(f => byForm[f] ?? 0)

  const recent = [...srs]
    .sort((a, b) => new Date(b.createdAt ?? 0) - new Date(a.createdAt ?? 0))
    .slice(0, 8)

  const alerts = []
  if (dueStds > 0)     alerts.push(`⚖ ${dueStds} reference standard${dueStds > 1 ? 's' : ''} due within 60 days or expired`)
  if (awaitingRev > 0) alerts.push(`🛠 ${awaitingRev} service request${awaitingRev > 1 ? 's' : ''} awaiting technical review`)
  if (quotesSent > 0)  alerts.push(`📤 ${quotesSent} quotation${quotesSent > 1 ? 's' : ''} sent, awaiting client response`)

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {/* Header row */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>Technical Department Overview</h1>
            <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>
              {refreshedAt ? `Last refreshed ${refreshedAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}` : 'Loading…'}
            </p>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn size="sm" variant="ghost" onClick={load} disabled={loading}>{loading ? 'Refreshing…' : '↻ Refresh'}</Btn>
            <Btn size="sm" onClick={() => navigate('/modules/operations/service-requests')}>Service Requests</Btn>
          </div>
        </div>

        <Tabs tabs={[{ id: 'overview', label: 'Overview' }, { id: 'analytics', label: 'Analytics' }]} active={tab} setActive={setTab} />

        {tab === 'overview' && (
          <div>
            {alerts.length > 0 && (
              <div style={{ background: T.redL, border: '1px solid #FCA5A5', borderRadius: 10, padding: '12px 16px', marginBottom: 18 }}>
                <div style={{ fontWeight: 700, color: T.red, fontSize: 13, marginBottom: 8 }}>🚨 Attention Required</div>
                {alerts.map((a, i) => <div key={i} style={{ fontSize: 12, color: T.red, marginBottom: 3 }}>{a}</div>)}
              </div>
            )}

            <div style={{ ...KPI_GRID, marginBottom: 20 }}>
              <Kpi label="Total Requests" value={dash(srTotal)} sub="All time" icon="🛠️" />
              <Kpi label="Open Requests" value={dash(openSrs)} sub="In pipeline" icon="📂" variant="blue" />
              <Kpi label="Awaiting Review" value={dash(awaitingRev)} sub={awaitingRev > 0 ? 'Needs TM action' : 'All clear'} icon="🔎" variant={awaitingRev > 0 ? 'amber' : 'green'} />
              <Kpi label="Quotations Sent" value={dash(quotesSent)} sub="Awaiting client" icon="📤" />
              <Kpi label="Reference Standards" value={dash(activeStds)} sub="Active in register" icon="⚖️" />
              <Kpi label="Standards Due" value={dash(dueStds)} sub={dueStds > 0 ? '≤60d or expired' : 'All in date'} icon="⏳" variant={dueStds > 0 ? 'red' : 'green'} />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 20, marginBottom: 20 }}>
              <Card>
                <SectionHeader title="Service Request Pipeline" sub="Current distribution" />
                {statusEntries.length === 0 ? <p style={{ color: T.mgrey, fontSize: 13 }}>No service-request data yet.</p> : (
                  <div style={{ display: 'grid', gap: 10 }}>
                    {statusEntries.map((s, i) => (
                      <StatLine key={s} label={label(s)} value={byStatus[s]} color={CHART_PALETTE[i % CHART_PALETTE.length]} />
                    ))}
                    <div style={{ marginTop: 4, paddingTop: 10, borderTop: `1px solid ${T.lgrey}`, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontSize: 13, color: T.mgrey, fontWeight: 500 }}>Open in pipeline</span>
                      <strong style={{ fontSize: 18, color: T.navy }}>{openSrs}</strong>
                    </div>
                  </div>
                )}
              </Card>

              <Card>
                <SectionHeader title="Quick Actions" sub="Common tasks" />
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 12 }}>
                  {[
                    ['🛠️ Service requests', '/modules/operations/service-requests'],
                    ['⚖️ Reference standards', '/modules/operations/reference-standards'],
                    ['📇 Equipment history', '/modules/operations/equipment-history'],
                    ['🔬 Technical dept', '/modules/operations'],
                  ].map(([lbl, path]) => (
                    <button key={lbl} onClick={() => navigate(path)}
                      style={{ padding: '14px 12px', background: T.offwt, border: `1px solid ${T.lgrey}`, borderRadius: 10, cursor: 'pointer', fontSize: 13, fontWeight: 600, color: T.navy, textAlign: 'left' }}
                      onMouseEnter={e => { e.currentTarget.style.borderColor = T.gold; e.currentTarget.style.background = '#fff' }}
                      onMouseLeave={e => { e.currentTarget.style.borderColor = T.lgrey; e.currentTarget.style.background = T.offwt }}>
                      {lbl}
                    </button>
                  ))}
                </div>
              </Card>
            </div>

            <Card>
              <SectionHeader title="Recent Service Requests" sub="Latest activity" action={<Btn size="sm" variant="ghost" onClick={() => navigate('/modules/operations/service-requests')}>View all</Btn>} />
              <DataTable
                headers={['Reference', 'Client', 'Type', 'Status', 'Age']}
                empty={loading ? 'Loading…' : 'No service requests yet.'}
                onRowClick={(_, i) => recent[i] && navigate(`/modules/operations/service-requests/${recent[i].id}`)}
                rows={recent.map(s => [
                  <span style={{ fontWeight: 600, color: T.navy, fontFamily: 'monospace' }}>{s.referenceNumber ?? '—'}</span>,
                  s.clientOrganization || s.clientName || '—',
                  FORM_LABEL[s.formType] ?? s.formType ?? '—',
                  <Badge variant={SR_VARIANT[s.status] ?? 'default'}>{label(s.status)}</Badge>,
                  timeAgo(s.createdAt),
                ])}
              />
            </Card>
          </div>
        )}

        {tab === 'analytics' && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 20 }}>
            <Card>
              <SectionHeader title="Requests by Status" sub="Pipeline distribution" />
              {statusEntries.length === 0 ? <p style={{ color: T.mgrey, fontSize: 13 }}>No data yet.</p> : (
                <Chart type="donut" height={280}
                  series={statusEntries.map(s => byStatus[s])}
                  options={{
                    labels: statusEntries.map(label),
                    colors: CHART_PALETTE, legend: { position: 'bottom', fontSize: '11px' },
                    dataLabels: { enabled: false }, stroke: { width: 0 },
                  }} />
              )}
            </Card>

            <Card>
              <SectionHeader title="Requests by Form Type" sub="Service vs calibration" />
              {formCounts.every(c => c === 0) ? <p style={{ color: T.mgrey, fontSize: 13 }}>No data yet.</p> : (
                <Chart type="bar" height={280}
                  series={[{ name: 'Requests', data: formCounts }]}
                  options={{
                    chart: { toolbar: { show: false } },
                    colors: [T.gold],
                    plotOptions: { bar: { borderRadius: 4, columnWidth: '45%', distributed: true } },
                    dataLabels: { enabled: true },
                    legend: { show: false },
                    xaxis: { categories: FORM_TYPES.map(f => FORM_LABEL[f]), labels: { style: { fontSize: '11px' } } },
                    grid: { borderColor: T.lgrey },
                  }} />
              )}
            </Card>
          </div>
        )}
      </div>
    </>
  )
}

function countBy(arr, keyFn) {
  const out = {}
  for (const x of arr) { const k = keyFn(x); if (k) out[k] = (out[k] ?? 0) + 1 }
  return out
}

function StatLine({ label, value, color }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <span style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: T.mgrey }}>
        <span style={{ width: 8, height: 8, borderRadius: '50%', background: color }} /> {label}
      </span>
      <strong style={{ fontSize: 14, color: T.dgrey }}>{value ?? 0}</strong>
    </div>
  )
}
