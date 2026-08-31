import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import Chart from 'react-apexcharts'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Tabs, SectionHeader, DataTable, Kpi, KPI_GRID } from '../../components/ui.jsx'
import ProjectStatusBadge from '../../components/projects/ProjectStatusBadge.jsx'
import { listProjects, listAssignments, listTimesheets, listNegligenceIncidents } from '../../services/operations.js'

// Projects & Tasks dashboard — mirrors the Helpdesk dashboard layout (header → Tabs → KPI grid →
// Cards → recent table). No aggregate API, so metrics are derived from the list endpoints.

const PAD = 'clamp(16px, 2.4vw, 26px)'
const CHART_PALETTE = [T.navy, T.gold, T.green, T.blue, T.amber, T.purple, T.red, T.navyL]

const PROJECT_STATUS_ORDER = ['Draft', 'Planning', 'PendingApproval', 'Active', 'OnHold', 'Completed', 'Closed', 'Cancelled']
const ASSIGN_STATUS_ORDER  = ['Pending', 'Accepted', 'InProgress', 'Completed', 'Declined', 'Cancelled', 'AwaitingProjectLink', 'Archived']
const ASSIGN_CLOSED        = ['Completed', 'Cancelled', 'Declined', 'Archived']

const label = (s) => s.replace(/([A-Z])/g, ' $1').trim()
const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const burnPct = (p) => {
  const planned = Number(p?.plannedBudget) || 0
  if (planned <= 0) return 0
  return ((Number(p?.actualCost) || 0) + (Number(p?.committed) || 0)) / planned * 100
}

export default function ProjectsDashboardPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState('overview')

  const [projects, setProjects]     = useState([])
  const [assignments, setAssignments] = useState([])
  const [tsQueue, setTsQueue]       = useState(0)
  const [openNeg, setOpenNeg]       = useState(0)
  const [loading, setLoading]       = useState(true)
  const [refreshedAt, setRefreshedAt] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [projRes, asgRes, tsRes, negRes] = await Promise.allSettled([
        listProjects({ page: 1, pageSize: 200 }),
        listAssignments({ page: 1, pageSize: 200 }),
        listTimesheets({ page: 1, pageSize: 100, status: 'Submitted' }),
        listNegligenceIncidents({ page: 1, pageSize: 100 }),
      ])
      const arr = (r) => (r.status === 'fulfilled' ? (r.value?.items ?? r.value?.data ?? r.value ?? []) : [])
      setProjects(arr(projRes))
      setAssignments(arr(asgRes))
      setTsQueue(arr(tsRes).length)
      setOpenNeg(arr(negRes).filter(i => i.status !== 'Closed').length)
      setRefreshedAt(new Date())
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  const dash = (v) => (loading ? '…' : (v ?? 0))

  const projByStatus = countBy(projects, p => p.status)
  const asgByStatus  = countBy(assignments, a => a.status)
  const activeCount  = projByStatus['Active'] ?? 0
  const critical     = projects.filter(p => p.budgetLocked || burnPct(p) >= 90).length
  const contractValue = projects.reduce((s, p) => s + (Number(p.contractValue) || 0), 0)
  const openTasks    = assignments.filter(a => !ASSIGN_CLOSED.includes(a.status)).length

  const projStatusEntries = PROJECT_STATUS_ORDER.filter(s => (projByStatus[s] ?? 0) > 0)
  const asgCounts = ASSIGN_STATUS_ORDER.map(s => asgByStatus[s] ?? 0)

  const recent = [...projects]
    .sort((a, b) => new Date(b.createdAt ?? 0) - new Date(a.createdAt ?? 0))
    .slice(0, 8)

  const alerts = []
  if (critical > 0) alerts.push(`🔴 ${critical} project${critical > 1 ? 's' : ''} at/over budget (locked or ≥90% burn)`)
  if (openNeg > 0)  alerts.push(`⚠ ${openNeg} open negligence incident${openNeg > 1 ? 's' : ''}`)
  if (tsQueue > 0)  alerts.push(`⏱ ${tsQueue} timesheet${tsQueue > 1 ? 's' : ''} awaiting approval`)

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {/* Header row */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>Projects &amp; Tasks Overview</h1>
            <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>
              {refreshedAt ? `Last refreshed ${refreshedAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}` : 'Loading…'}
            </p>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn size="sm" variant="ghost" onClick={load} disabled={loading}>{loading ? 'Refreshing…' : '↻ Refresh'}</Btn>
            <Btn size="sm" onClick={() => navigate('/modules/projects/new')}>+ New Project</Btn>
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
              <Kpi label="Total Projects" value={dash(projects.length)} sub="All statuses" icon="🏛️" />
              <Kpi label="Active" value={dash(activeCount)} sub="In execution" icon="🟢" variant="green" />
              <Kpi label="Budget Critical" value={dash(critical)} sub={critical > 0 ? 'Locked / ≥90% burn' : 'All healthy'} icon="🔴" variant={critical > 0 ? 'red' : 'green'} />
              <Kpi label="Contract Value" value={loading ? '…' : fmtKes(contractValue)} sub="Portfolio total" icon="💰" />
              <Kpi label="Open Tasks" value={dash(openTasks)} sub="Assignments in progress" icon="☑️" variant="blue" />
              <Kpi label="Timesheets" value={dash(tsQueue)} sub="Awaiting approval" icon="⏱️" variant={tsQueue > 0 ? 'amber' : 'green'} />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 20, marginBottom: 20 }}>
              <Card>
                <SectionHeader title="Project Health" sub="Portfolio by status" />
                {projStatusEntries.length === 0 ? <p style={{ color: T.mgrey, fontSize: 13 }}>No project data yet.</p> : (
                  <div style={{ display: 'grid', gap: 10 }}>
                    {projStatusEntries.map((s, i) => (
                      <StatLine key={s} label={label(s)} value={projByStatus[s]} color={CHART_PALETTE[i % CHART_PALETTE.length]} />
                    ))}
                    <div style={{ marginTop: 4, paddingTop: 10, borderTop: `1px solid ${T.lgrey}`, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontSize: 13, color: T.mgrey, fontWeight: 500 }}>Budget critical</span>
                      <strong style={{ fontSize: 18, color: critical > 0 ? T.red : T.green }}>{critical}</strong>
                    </div>
                  </div>
                )}
              </Card>

              <Card>
                <SectionHeader title="Quick Actions" sub="Common tasks" />
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 12 }}>
                  {[
                    ['🏛️ New project', '/modules/projects/new'],
                    ['📋 All projects', '/modules/projects'],
                    ['☑️ Tasks', '/modules/operations/assignments'],
                    ['⏱️ Timesheets', '/modules/operations/timesheets'],
                    ['⚠️ Negligence', '/modules/operations/negligence'],
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
              <SectionHeader title="Recent Projects" sub="Latest activity" action={<Btn size="sm" variant="ghost" onClick={() => navigate('/modules/projects')}>View all</Btn>} />
              <DataTable
                headers={['Project', 'Client', 'Status', 'Contract Value', 'Budget Used']}
                empty={loading ? 'Loading…' : 'No projects yet.'}
                onRowClick={(_, i) => recent[i] && navigate(`/modules/projects/${recent[i].id}`)}
                rows={recent.map(p => {
                  const ratio = burnPct(p)
                  const pctColor = ratio >= 100 ? T.red : ratio >= 80 ? T.amber : T.green
                  return [
                    <span style={{ fontWeight: 600, color: T.navy }}>{p.name}</span>,
                    p.clientName ?? '—',
                    <ProjectStatusBadge status={p.status} />,
                    <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(p.contractValue ?? p.plannedBudget ?? 0)}</span>,
                    <span style={{ color: pctColor, fontWeight: 700, whiteSpace: 'nowrap' }}>{ratio.toFixed(0)}%</span>,
                  ]
                })}
              />
            </Card>
          </div>
        )}

        {tab === 'analytics' && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 20 }}>
            <Card>
              <SectionHeader title="Projects by Status" sub="Current distribution" />
              {projStatusEntries.length === 0 ? <p style={{ color: T.mgrey, fontSize: 13 }}>No project data yet.</p> : (
                <Chart type="donut" height={280}
                  series={projStatusEntries.map(s => projByStatus[s])}
                  options={{
                    labels: projStatusEntries.map(label),
                    colors: CHART_PALETTE, legend: { position: 'bottom', fontSize: '11px' },
                    dataLabels: { enabled: false }, stroke: { width: 0 },
                  }} />
              )}
            </Card>

            <Card>
              <SectionHeader title="Tasks by Status" sub="Assignment workload" />
              {asgCounts.every(c => c === 0) ? <p style={{ color: T.mgrey, fontSize: 13 }}>No task data yet.</p> : (
                <Chart type="bar" height={280}
                  series={[{ name: 'Tasks', data: asgCounts }]}
                  options={{
                    chart: { toolbar: { show: false } },
                    colors: [T.gold],
                    plotOptions: { bar: { borderRadius: 4, columnWidth: '55%', distributed: true } },
                    dataLabels: { enabled: true },
                    legend: { show: false },
                    xaxis: { categories: ASSIGN_STATUS_ORDER.map(label), labels: { style: { fontSize: '10px' }, rotate: -35 } },
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
