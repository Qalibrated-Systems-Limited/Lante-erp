import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import Chart from 'react-apexcharts'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Kpi, KPI_GRID } from '../../components/ui.jsx'
import { getDashboardSummary, getSlaCompliance, getMySummary, listTickets } from '../../services/ticketing.js'
import { useAuth } from '../../context/AuthContext.jsx'

// Helpdesk dashboard — mirrors the layout of pages/DashboardPage.jsx (Tabs → KPI grid → Cards),
// using the shared ui.jsx kit + theme tokens, but wired to real ticketing data.

const PAD = 'clamp(16px, 2.4vw, 26px)'
const CHART_PALETTE = [T.navy, T.gold, T.green, T.blue, T.amber, T.purple, T.red, T.navyL]
const STATUS_ORDER = ['New', 'Assigned', 'InProgress', 'Pending', 'Escalated', 'Resolved', 'Closed', 'Reopened']
const PRIORITY_ORDER = ['Critical', 'High', 'Medium', 'Low']

// Map a ticket status to a Badge variant for the recent-tickets table.
const STATUS_VARIANT = {
  New: 'blue', Assigned: 'blue', InProgress: 'amber', Pending: 'amber',
  Escalated: 'red', Resolved: 'green', Closed: 'default', Reopened: 'amber',
}
const PRIORITY_VARIANT = { Critical: 'red', High: 'amber', Medium: 'default', Low: 'default' }

function timeAgo(dt) {
  if (!dt) return '—'
  const m = Math.floor((Date.now() - new Date(dt).getTime()) / 60000)
  if (m < 2) return 'just now'
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ago`
  return `${Math.floor(h / 24)}d ago`
}

export default function HelpdeskDashboardPage() {
  const navigate = useNavigate()
  const { ticketScope, user } = useAuth()
  const [tab, setTab] = useState('overview')

  const [summary, setSummary] = useState(null)
  const [sla, setSla] = useState(null)
  const [mySummary, setMySummary] = useState(null)
  const [recent, setRecent] = useState([])
  const [loading, setLoading] = useState(true)
  const [refreshedAt, setRefreshedAt] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [sumRes, slaRes, myRes, recentRes] = await Promise.allSettled([
        getDashboardSummary(),
        getSlaCompliance(),
        getMySummary(),
        listTickets({ page: 1, pageSize: 8, sortDescending: true }),
      ])
      if (sumRes.status === 'fulfilled') setSummary(sumRes.value)
      if (slaRes.status === 'fulfilled') setSla(slaRes.value)
      if (myRes.status === 'fulfilled') setMySummary(myRes.value)
      if (recentRes.status === 'fulfilled') setRecent(recentRes.value?.items ?? [])
      setRefreshedAt(new Date())
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  const byStatus = summary?.byStatus ?? {}
  const byPriority = summary?.byPriority ?? {}
  const openCount = summary?.open ?? 0
  const escalated = byStatus['Escalated'] ?? 0
  const compliance = sla?.complianceRate ?? null
  const dash = (v) => (loading ? '…' : (v ?? 0))

  const statusEntries = STATUS_ORDER.filter(s => (byStatus[s] ?? 0) > 0)
  const priorityCounts = PRIORITY_ORDER.map(p => byPriority[p] ?? 0)

  const alerts = []
  if (escalated > 0) alerts.push(`⚠ ${escalated} escalated ticket${escalated > 1 ? 's' : ''} need attention`)
  if ((mySummary?.myOverdueTickets ?? 0) > 0) alerts.push(`⏰ ${mySummary.myOverdueTickets} of your tickets are overdue`)
  if ((sla?.resolutionBreached ?? 0) > 0) alerts.push(`⏱ ${sla.resolutionBreached} ticket${sla.resolutionBreached > 1 ? 's have' : ' has'} breached resolution SLA`)

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {/* Header row */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>Helpdesk Overview</h1>
            <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>
              {refreshedAt ? `Last refreshed ${refreshedAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}` : 'Loading…'}
              {ticketScope !== 'all' && ` · scoped to ${ticketScope === 'dept' ? (user?.departmentName ?? 'your department') : 'your tickets'}`}
            </p>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn size="sm" variant="ghost" onClick={load} disabled={loading}>{loading ? 'Refreshing…' : '↻ Refresh'}</Btn>
            <Btn size="sm" onClick={() => navigate('/modules/ticketing/new')}>+ New Ticket</Btn>
          </div>
        </div>

        <Tabs
          tabs={[{ id: 'overview', label: 'Overview' }, { id: 'analytics', label: 'Analytics' }]}
          active={tab} setActive={setTab}
        />

        {tab === 'overview' && (
          <div>
            {alerts.length > 0 && (
              <div style={{ background: T.redL, border: '1px solid #FCA5A5', borderRadius: 10, padding: '12px 16px', marginBottom: 18 }}>
                <div style={{ fontWeight: 700, color: T.red, fontSize: 13, marginBottom: alerts.length ? 8 : 0 }}>🚨 Attention Required</div>
                {alerts.map((a, i) => <div key={i} style={{ fontSize: 12, color: T.red, marginBottom: 3 }}>{a}</div>)}
              </div>
            )}

            <div style={{ ...KPI_GRID, marginBottom: 20 }}>
              <Kpi label="Total Tickets" value={dash(summary?.total)} sub="All time" icon="🎫" />
              <Kpi label="Open" value={dash(openCount)} sub="Not resolved / closed" icon="📂" variant="blue" />
              <Kpi label="Escalated" value={dash(escalated)} sub={escalated > 0 ? 'Needs attention' : 'All clear'} icon="⚠️" variant={escalated > 0 ? 'red' : 'green'} />
              <Kpi label="SLA Compliance" value={loading ? '…' : (compliance != null ? `${compliance}%` : '—')} sub={sla ? `${sla.onTrack} on track` : ''} icon="⏱️" variant={compliance == null ? undefined : compliance >= 80 ? 'green' : compliance >= 60 ? 'amber' : 'red'} />
              <Kpi label="My Open" value={dash(mySummary?.myOpenTickets)} sub="Assigned to me" icon="🙋" />
              <Kpi label="My Overdue" value={dash(mySummary?.myOverdueTickets)} icon="🔥" variant={(mySummary?.myOverdueTickets ?? 0) > 0 ? 'red' : 'green'} />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 20, marginBottom: 20 }}>
              <Card>
                <SectionHeader title="SLA Health" sub="Response & resolution breaches" />
                {sla ? (
                  <div style={{ display: 'grid', gap: 10 }}>
                    <SlaLine label="On track" value={sla.onTrack} color={T.green} />
                    <SlaLine label="Response breached" value={sla.responseBreached} color={sla.responseBreached > 0 ? T.amber : T.mgrey} />
                    <SlaLine label="Resolution breached" value={sla.resolutionBreached} color={sla.resolutionBreached > 0 ? T.red : T.mgrey} />
                    <div style={{ marginTop: 4, paddingTop: 10, borderTop: `1px solid ${T.lgrey}`, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontSize: 13, color: T.mgrey, fontWeight: 500 }}>Compliance rate</span>
                      <strong style={{ fontSize: 18, color: compliance >= 80 ? T.green : compliance >= 60 ? T.amber : T.red }}>{compliance}%</strong>
                    </div>
                  </div>
                ) : <p style={{ color: T.mgrey, fontSize: 13 }}>SLA data unavailable.</p>}
              </Card>

              <Card>
                <SectionHeader title="Quick Actions" sub="Common tasks" />
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 12 }}>
                  {[
                    ['🎫 New ticket', '/modules/ticketing/new'],
                    ['📋 All tickets', '/modules/ticketing'],
                    ['🏷️ Tags', '/modules/ticketing/tags'],
                    ['⚡ Workflow rules', '/modules/ticketing/workflows'],
                    ['📚 Knowledge base', '/modules/ticketing/kb'],
                    ['⚙️ Settings', '/modules/ticketing/settings'],
                  ].map(([label, path]) => (
                    <button key={label} onClick={() => navigate(path)}
                      style={{ padding: '14px 12px', background: T.offwt, border: `1px solid ${T.lgrey}`, borderRadius: 10, cursor: 'pointer', fontSize: 13, fontWeight: 600, color: T.navy, textAlign: 'left' }}
                      onMouseEnter={e => { e.currentTarget.style.borderColor = T.gold; e.currentTarget.style.background = '#fff' }}
                      onMouseLeave={e => { e.currentTarget.style.borderColor = T.lgrey; e.currentTarget.style.background = T.offwt }}>
                      {label}
                    </button>
                  ))}
                </div>
              </Card>
            </div>

            <Card>
              <SectionHeader title="Recent Tickets" sub="Latest activity" action={<Btn size="sm" variant="ghost" onClick={() => navigate('/modules/ticketing')}>View all</Btn>} />
              <DataTable
                headers={['Ticket', 'Category', 'Status', 'Priority', 'Age']}
                empty={loading ? 'Loading…' : 'No tickets yet.'}
                onRowClick={(_, i) => recent[i] && navigate(`/modules/ticketing/${recent[i].id}`)}
                rows={recent.map(t => [
                  <span style={{ fontWeight: 600, color: T.navy }}>{t.reference ? `${t.reference} · ` : ''}{t.title}{t.isEscalated ? ' ⚠' : ''}</span>,
                  t.categoryName ?? '—',
                  <Badge variant={STATUS_VARIANT[t.statusLabel] ?? 'default'}>{t.statusLabel === 'InProgress' ? 'In Progress' : t.statusLabel}</Badge>,
                  <Badge variant={PRIORITY_VARIANT[t.priorityLabel] ?? 'default'}>{t.priorityLabel}</Badge>,
                  timeAgo(t.createdAt),
                ])}
              />
            </Card>
          </div>
        )}

        {tab === 'analytics' && (
          <div>
            <div style={{ ...KPI_GRID, marginBottom: 18 }}>
              <Kpi label="On Track" value={dash(sla?.onTrack)} icon="✅" variant="green" />
              <Kpi label="Response Breached" value={dash(sla?.responseBreached)} icon="⚡" variant={(sla?.responseBreached ?? 0) > 0 ? 'amber' : 'green'} />
              <Kpi label="Resolution Breached" value={dash(sla?.resolutionBreached)} icon="⏱️" variant={(sla?.resolutionBreached ?? 0) > 0 ? 'red' : 'green'} />
              <Kpi label="Compliance" value={loading ? '…' : (compliance != null ? `${compliance}%` : '—')} icon="🎯" variant={compliance == null ? undefined : compliance >= 80 ? 'green' : 'amber'} />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 20 }}>
              <Card>
                <SectionHeader title="Tickets by Status" sub="Current distribution" />
                {statusEntries.length === 0 ? <p style={{ color: T.mgrey, fontSize: 13 }}>No ticket data yet.</p> : (
                  <Chart type="donut" height={280}
                    series={statusEntries.map(s => byStatus[s])}
                    options={{
                      labels: statusEntries.map(s => s === 'InProgress' ? 'In Progress' : s),
                      colors: CHART_PALETTE, legend: { position: 'bottom', fontSize: '11px' },
                      dataLabels: { enabled: false }, stroke: { width: 0 },
                    }} />
                )}
              </Card>

              <Card>
                <SectionHeader title="Tickets by Priority" sub="Open workload by severity" />
                {priorityCounts.every(c => c === 0) ? <p style={{ color: T.mgrey, fontSize: 13 }}>No ticket data yet.</p> : (
                  <Chart type="bar" height={280}
                    series={[{ name: 'Tickets', data: priorityCounts }]}
                    options={{
                      chart: { toolbar: { show: false } },
                      colors: [T.gold],
                      plotOptions: { bar: { borderRadius: 4, columnWidth: '45%', distributed: true } },
                      dataLabels: { enabled: true },
                      legend: { show: false },
                      xaxis: { categories: PRIORITY_ORDER, labels: { style: { fontSize: '11px' } } },
                      grid: { borderColor: T.lgrey },
                    }} />
                )}
              </Card>
            </div>
          </div>
        )}
      </div>
    </>
  )
}

function SlaLine({ label, value, color }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <span style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: T.mgrey }}>
        <span style={{ width: 8, height: 8, borderRadius: '50%', background: color }} /> {label}
      </span>
      <strong style={{ fontSize: 14, color }}>{value ?? 0}</strong>
    </div>
  )
}
