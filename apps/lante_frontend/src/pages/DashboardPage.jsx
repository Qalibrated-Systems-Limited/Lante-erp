import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import Chart from 'react-apexcharts'
import { T, fmt } from '../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Kpi, KPI_GRID } from '../components/ui.jsx'
import api from '../api/axios.js'
import { useAuth } from '../context/AuthContext.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Dashboard — matches the deployed QSL MD dashboard. Full-width, responsive.
// Summary tab is wired to real data (see loadSummary() below): management-accounts
// + invoices/imprest (finance-service, via reporting-service where available),
// active projects (operations-service), aged-debtors and compliance-dashboard
// (reporting-service), overdue assignments (operations-service). "Open Bids" was
// dropped — the Bids module has no backend yet, so there's nothing real to show.
// Analytics tab (inventory/pending-approvals/procurement/stock-movement) is still
// MOCK aside from Fleet Utilization, which was already wired — see
// loadFleetUtilization() below. No backend aggregate exists yet for those.
// ─────────────────────────────────────────────────────────────────────────────

const EMPTY_SUMMARY = {
  revenue_active: 0,
  gross_profit: 0,
  total_collected: 0,
  total_invoiced: 0,
  total_expenses: 0,
  active_projects: 0,
  overdue_imprest: { count: 0, amount: 0 },
  overdue_tasks: 0,
  expiring_docs: 0,
  top_debtors: [],
}

const MOCK_ANALYTICS = {
  inventory: {
    total_value: 8_450_000, item_count: 214, low_stock_count: 6,
    by_category: [
      { category: 'Reference Standards', value: 3_200_000 },
      { category: 'Spare Parts', value: 2_100_000 },
      { category: 'Consumables', value: 1_450_000 },
      { category: 'Tools', value: 1_000_000 },
      { category: 'Office', value: 700_000 },
    ],
  },
  pending_approvals: {
    total: 9,
    breakdown: [
      { name: 'Requisitions', value: 4 }, { name: 'LPOs', value: 2 },
      { name: 'Leave', value: 2 }, { name: 'Claims', value: 1 },
    ],
  },
  fleet: {
    utilization_rate: 62, active_vehicles: 5, total_vehicles: 8,
    top_utilized: [
      { reg_no: 'KDA 123A', trip_count: 14, total_distance: 3_820 },
      { reg_no: 'KDB 456B', trip_count: 9, total_distance: 2_140 },
      { reg_no: 'KDC 789C', trip_count: 0, total_distance: 0 },
    ],
  },
  procurement_trend: [
    { month: 'Feb', total_value: 1_200_000 }, { month: 'Mar', total_value: 1_850_000 },
    { month: 'Apr', total_value: 1_400_000 }, { month: 'May', total_value: 2_300_000 },
    { month: 'Jun', total_value: 1_950_000 }, { month: 'Jul', total_value: 2_600_000 },
  ],
  stock_movement_trend: [
    { month: 'Feb', received: 320, issued: 280 }, { month: 'Mar', received: 410, issued: 350 },
    { month: 'Apr', received: 280, issued: 300 }, { month: 'May', received: 500, issued: 420 },
    { month: 'Jun', received: 380, issued: 360 }, { month: 'Jul', received: 450, issued: 400 },
  ],
}

const CHART_PALETTE = [T.navy, T.gold, T.green, T.blue, T.amber, T.purple, T.red, T.navyL]
const PAD = 'clamp(16px, 2.4vw, 26px)'
const kpiGrid = KPI_GRID

export default function DashboardPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [tab, setTab] = useState('summary')
  const [fleet, setFleet] = useState(MOCK_ANALYTICS.fleet)
  const [summary, setSummary] = useState(EMPTY_SUMMARY)
  const go = (path) => navigate(path)

  useEffect(() => {
    // Each metric comes from a different service — fetch independently so one
    // unavailable service (e.g. finance-service down) doesn't blank the rest.
    async function loadSummary() {
      const today = new Date()

      // Status is matched server-side via Enum.TryParse<ProjectStatus>(...) against the string
      // name (case-sensitive) — NOT an ordinal index. The backend enum is Draft, Planning,
      // PendingMdApproval, PendingFinanceApproval, Active, OnHold, Completed, Closed, Cancelled;
      // sending a numeric index here would silently match the wrong status.
      const projectsP = api.get('/api/v1/projects', { params: { status: 'Active', pageSize: 200 } })
        .then(r => {
          const items = r.data?.data?.items ?? []
          return {
            active_projects: r.data?.data?.totalCount ?? items.length,
            revenue_active: items.reduce((s, p) => s + (p.contractValue || 0), 0),
          }
        })
        .catch(() => ({ active_projects: 0, revenue_active: 0 }))

      const managementAccountsP = api.get('/api/v1/reports/management-accounts')
        .then(r => {
          const pnl = r.data?.data?.profitAndLoss
          return { gross_profit: pnl?.netProfit || 0, total_expenses: pnl?.expenseTotal || 0 }
        })
        .catch(() => ({ gross_profit: 0, total_expenses: 0 }))

      const invoicesP = api.get('/api/v1/finance/invoices')
        .then(r => {
          const items = r.data?.data ?? []
          return {
            total_invoiced: items.reduce((s, i) => s + (i.total || 0), 0),
            total_collected: items.reduce((s, i) => s + (i.paidAmount || 0), 0),
          }
        })
        .catch(() => ({ total_invoiced: 0, total_collected: 0 }))

      const debtorsP = api.get('/api/v1/reports/aged-debtors')
        .then(r => (r.data?.data ?? [])
          .slice().sort((a, b) => (b.total || 0) - (a.total || 0)).slice(0, 3)
          .map(row => ({ name: row.customerName, outstanding: row.total })))
        .catch(() => [])

      const imprestP = api.get('/api/v1/finance/imprest')
        .then(r => {
          const overdue = (r.data?.data ?? []).filter(i =>
            ['Disbursed', 'PartlyRetired'].includes(i.status) && (i.daysToDue ?? 0) < 0)
          return { count: overdue.length, amount: overdue.reduce((s, i) => s + (i.unretiredBalance || 0), 0) }
        })
        .catch(() => ({ count: 0, amount: 0 }))

      const CLOSED_ASSIGNMENTS = ['Completed', 'Cancelled', 'Archived', 'Declined']
      const overdueTasksP = api.get('/api/v1/assignments', { params: { page: 1, pageSize: 100, sortDescending: true } })
        .then(r => (r.data?.data?.items ?? [])
          .filter(a => a.deadline && !CLOSED_ASSIGNMENTS.includes(a.status) && new Date(a.deadline) < today)
          .length)
        .catch(() => 0)

      const complianceP = api.get('/api/v1/reports/compliance-dashboard')
        .then(r => r.data?.data?.complianceDashboard?.licencesExpiringSoon || 0)
        .catch(() => 0)

      const [projects, mgmt, invoices, top_debtors, overdue_imprest, overdue_tasks, expiring_docs] =
        await Promise.all([projectsP, managementAccountsP, invoicesP, debtorsP, imprestP, overdueTasksP, complianceP])

      setSummary({
        ...projects, ...mgmt, ...invoices,
        top_debtors, overdue_imprest, overdue_tasks, expiring_docs,
      })
    }
    loadSummary()
  }, [])

  useEffect(() => {
    async function loadFleetUtilization() {
      try {
        const to = new Date()
        const from = new Date(to.getTime() - 30 * 24 * 60 * 60 * 1000)
        const [utilRes, trucksRes] = await Promise.all([
          api.get('/api/v1/reports/fleet-cost-utilisation', { params: { from: from.toISOString(), to: to.toISOString() } }),
          api.get('/api/v1/Trucks'),
        ])
        const trucks = utilRes.data?.data?.trucks ?? []
        const totalVehicles = trucksRes.data?.data?.length ?? 0
        const activeVehicles = trucks.length
        setFleet({
          utilization_rate: totalVehicles > 0 ? Math.round((activeVehicles / totalVehicles) * 100) : 0,
          active_vehicles: activeVehicles,
          total_vehicles: totalVehicles,
          top_utilized: [...trucks]
            .sort((a, b) => b.tripCount - a.tripCount)
            .slice(0, 5)
            .map(t => ({ reg_no: t.licensePlate || t.truckId, trip_count: t.tripCount, total_distance: t.totalMileage })),
        })
      } catch {
        // Leave the mock fleet data in place — this is still a reference/mock dashboard elsewhere.
      }
    }
    loadFleetUtilization()
  }, [])

  const d = summary
  const rev = d.revenue_active || 0
  const gp = d.gross_profit || 0
  const margin = rev ? gp / rev : 0
  const alertCount = (d.overdue_imprest?.count || 0) + (d.overdue_tasks || 0) + (d.expiring_docs || 0)

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <Tabs
          tabs={[{ id: 'summary', label: 'Executive Summary' }, { id: 'analytics', label: 'Analytics & KPIs' }]}
          active={tab} setActive={setTab}
        />

        {tab === 'summary' && (
          <div>
            {alertCount > 0 && (
              <div style={{ background: T.redL, border: '1px solid #FCA5A5', borderRadius: 10, padding: '12px 16px', marginBottom: 20 }}>
                <div style={{ fontWeight: 700, color: T.red, fontSize: 13, marginBottom: 8 }}>🚨 Active Alerts Requiring Attention</div>
                {(d.overdue_imprest?.count || 0) > 0 && <div style={{ fontSize: 12, color: T.red, marginBottom: 3 }}>⏰ {d.overdue_imprest.count} overdue imprest — {fmt.kes(d.overdue_imprest.amount)} at risk</div>}
                {(d.overdue_tasks || 0) > 0 && <div style={{ fontSize: 12, color: T.red, marginBottom: 3 }}>📋 {d.overdue_tasks} overdue tasks</div>}
                {(d.expiring_docs || 0) > 0 && <div style={{ fontSize: 12, color: T.amber }}>📄 {d.expiring_docs} compliance documents expiring within 60 days</div>}
              </div>
            )}

            <div style={{ ...kpiGrid, marginBottom: 14 }}>
              {hasPermission('finance.reports') && <Kpi label="Portfolio Value" value={`Kshs ${(rev / 1e6).toFixed(1)}M`} sub="Active projects" icon="💼" />}
              {hasPermission('finance.reports') && <Kpi label="Gross Profit" value={`Kshs ${(gp / 1e6).toFixed(1)}M`} sub={`Margin: ${fmt.pct(margin)}`} icon="📈" variant={margin >= .15 ? 'green' : margin >= .1 ? 'amber' : 'red'} />}
              {hasPermission('finance.read') && <Kpi label="Collections" value={`Kshs ${((d.total_collected || 0) / 1e6).toFixed(1)}M`} sub="Total received" icon="💳" />}
              {hasPermission('finance.read') && <Kpi label="Invoiced" value={`Kshs ${((d.total_invoiced || 0) / 1e6).toFixed(1)}M`} sub="Billed to date" icon="🧾" />}
              {hasPermission('projects.read.own') && <Kpi label="Active Projects" value={d.active_projects || 0} icon="🏛️" />}
            </div>

            <div style={{ ...kpiGrid, marginBottom: 22 }}>
              {hasPermission('finance.read') && <Kpi label="Overdue Imprest" value={d.overdue_imprest?.count || 0} icon="⏰" variant={(d.overdue_imprest?.count || 0) > 0 ? 'red' : 'green'} />}
              {hasPermission('operations.read.own') && <Kpi label="Overdue Tasks" value={d.overdue_tasks || 0} icon="📋" variant={(d.overdue_tasks || 0) > 0 ? 'amber' : 'green'} />}
              {hasPermission('compliance.read') && <Kpi label="Expiring Docs" value={d.expiring_docs || 0} icon="📄" variant={(d.expiring_docs || 0) > 0 ? 'amber' : 'green'} />}
              {hasPermission('finance.read') && <Kpi label="Total Expenses" value={`Kshs ${((d.total_expenses || 0) / 1e6).toFixed(1)}M`} icon="📉" />}
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 20 }}>
              {hasPermission('finance.read') && (
                <Card>
                  <SectionHeader title="Top Debtors" sub="Clients with outstanding balances" action={<Btn size="sm" onClick={() => go('/modules/reports')}>Full Report</Btn>} />
                  {(d.top_debtors || []).length === 0 ? <p style={{ color: T.mgrey, fontSize: 13 }}>No outstanding balances</p> : (
                    d.top_debtors.map((x, i) => (
                      <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 0', borderBottom: i < d.top_debtors.length - 1 ? `1px solid ${T.lgrey}` : 'none', fontSize: 13 }}>
                        <span style={{ fontWeight: 500 }}>{x.name}</span>
                        <strong style={{ color: T.amber, whiteSpace: 'nowrap' }}>{fmt.kes(x.outstanding)}</strong>
                      </div>
                    ))
                  )}
                </Card>
              )}

              <Card>
                <SectionHeader title="Quick Actions" sub="Common tasks" />
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 12 }}>
                  {[
                    ['Request Imprest', '/modules/requisitions', null],
                    ['Post Expense', '/modules/finance', 'finance.write'],
                    ['New Lead', '/modules/crm', null],
                    ['Raise PR', '/modules/procurement', null],
                    ['Check Compliance', '/modules/compliance', 'compliance.read'],
                    ['Run Tax Report', '/modules/tax', 'finance.read'],
                  ].filter(([, , permission]) => !permission || hasPermission(permission))
                   .map(([label, path]) => (
                    <button key={label} onClick={() => go(path)} style={{ padding: '15px 12px', background: T.offwt, border: `1px solid ${T.lgrey}`, borderRadius: 10, cursor: 'pointer', fontSize: 13, fontWeight: 600, color: T.navy }}
                      onMouseEnter={e => { e.currentTarget.style.borderColor = T.gold; e.currentTarget.style.background = '#fff' }}
                      onMouseLeave={e => { e.currentTarget.style.borderColor = T.lgrey; e.currentTarget.style.background = T.offwt }}>
                      {label}
                    </button>
                  ))}
                </div>
              </Card>
            </div>
          </div>
        )}

        {tab === 'analytics' && <AnalyticsTab a={{ ...MOCK_ANALYTICS, fleet }} hasPermission={hasPermission} />}
      </div>
    </>
  )
}

function AnalyticsTab({ a, hasPermission }) {
  const canStores = hasPermission('stores.read')
  const canFleet = hasPermission('fleet.read')

  return (
    <div>
      <div style={{ ...kpiGrid, marginBottom: 18 }}>
        {canStores && <Kpi label="Inventory Value" value={fmt.kes(a.inventory.total_value)} sub={`${a.inventory.item_count} active items`} icon="📦" />}
        {canStores && <Kpi label="Low Stock Alerts" value={a.inventory.low_stock_count} icon="⚠️" variant={a.inventory.low_stock_count ? 'red' : 'green'} />}
        <Kpi label="Pending Approvals" value={a.pending_approvals.total} icon="⏳" variant={a.pending_approvals.total ? 'amber' : 'green'} />
        {canFleet && <Kpi label="Fleet Utilization" value={`${a.fleet.utilization_rate}%`} sub={`${a.fleet.active_vehicles} of ${a.fleet.total_vehicles} vehicles`} icon="🚗" variant={a.fleet.utilization_rate >= 60 ? 'green' : a.fleet.utilization_rate >= 30 ? 'amber' : 'red'} />}
      </div>

      {canStores && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 20, marginBottom: 20 }}>
          <Card>
            <SectionHeader title="Inventory Value by Category" sub="Current stock on hand" />
            <Chart type="donut" height={260}
              series={a.inventory.by_category.map(c => c.value)}
              options={{ labels: a.inventory.by_category.map(c => c.category), colors: CHART_PALETTE, legend: { position: 'bottom', fontSize: '11px' }, dataLabels: { enabled: false }, tooltip: { y: { formatter: v => fmt.kes(v) } }, stroke: { width: 0 } }} />
          </Card>
          <Card>
            <SectionHeader title="Pending Approvals by Type" sub="Items awaiting action right now" />
            <Chart type="bar" height={260}
              series={[{ name: 'Pending', data: a.pending_approvals.breakdown.map(b => b.value) }]}
              options={{ chart: { toolbar: { show: false } }, colors: [T.amber], plotOptions: { bar: { borderRadius: 4, columnWidth: '45%' } }, dataLabels: { enabled: false }, xaxis: { categories: a.pending_approvals.breakdown.map(b => b.name), labels: { style: { fontSize: '10px' } } }, grid: { borderColor: T.lgrey } }} />
          </Card>
        </div>
      )}

      {(canStores || canFleet) && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 20, marginBottom: 20 }}>
          <Card>
            <SectionHeader title="Procurement Trend" sub="LPO value by month, last 6 months" />
            <Chart type="line" height={240}
              series={[{ name: 'LPO Value', data: a.procurement_trend.map(p => p.total_value) }]}
              options={{ chart: { toolbar: { show: false } }, colors: [T.navy], stroke: { width: 2, curve: 'smooth' }, markers: { size: 4 }, xaxis: { categories: a.procurement_trend.map(p => p.month) }, yaxis: { labels: { formatter: v => `${(v / 1e6).toFixed(1)}M` } }, tooltip: { y: { formatter: v => fmt.kes(v) } }, grid: { borderColor: T.lgrey } }} />
          </Card>
          {canStores && (
            <Card>
              <SectionHeader title="Stock Movement Trend" sub="Units received vs issued, last 6 months" />
              <Chart type="line" height={240}
                series={[{ name: 'Received', data: a.stock_movement_trend.map(s => s.received) }, { name: 'Issued', data: a.stock_movement_trend.map(s => s.issued) }]}
                options={{ chart: { toolbar: { show: false } }, colors: [T.green, T.red], stroke: { width: 2, curve: 'smooth' }, markers: { size: 4 }, legend: { position: 'top', fontSize: '11px' }, xaxis: { categories: a.stock_movement_trend.map(s => s.month) }, grid: { borderColor: T.lgrey } }} />
            </Card>
          )}
        </div>
      )}

      {canFleet && (
        <Card>
          <SectionHeader title="Fleet Utilization Detail" sub="Trips and distance covered, last 30 days" />
          <DataTable headers={['Vehicle', 'Trips (30d)', 'Distance (km)', 'Status']} empty="No vehicles registered yet."
            rows={a.fleet.top_utilized.map(v => [
              <strong>{v.reg_no}</strong>, v.trip_count, fmt.num(v.total_distance),
              v.trip_count > 0 ? <Badge variant="green">In Use</Badge> : <Badge variant="default">Idle</Badge>,
            ])} />
        </Card>
      )}
    </div>
  )
}
