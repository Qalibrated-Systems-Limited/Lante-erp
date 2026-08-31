import { useState, useMemo } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import { Tabs } from '../../components/ui.jsx'
import ManagementAccountsTab from '../../components/reports/ManagementAccountsTab.jsx'
import BudgetVarianceTab from '../../components/reports/BudgetVarianceTab.jsx'
import AgedDebtorsTab from '../../components/reports/AgedDebtorsTab.jsx'
import CashFlowForecastTab from '../../components/reports/CashFlowForecastTab.jsx'
import ProjectProfitabilityTab from '../../components/reports/ProjectProfitabilityTab.jsx'
import FleetCostTab from '../../components/reports/FleetCostTab.jsx'
import ProcurementSpendTab from '../../components/reports/ProcurementSpendTab.jsx'
import HseIncidentTrirTab from '../../components/reports/HseIncidentTrirTab.jsx'
import ComplianceDashboardTab from '../../components/reports/ComplianceDashboardTab.jsx'
import ScheduleTab from '../../components/reports/ScheduleTab.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Reports — cross-module reports/analytics/exports. Thin shell: composes the
// 9 report tabs (each self-fetching from src/services/reports.js) and lazily
// mounts a tab's content only the first time it's visited (matches HsePage's
// pattern) so switching tabs doesn't refire every report's fetch on load.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'

const BASE_TABS = [
  { id: 'management-accounts', label: 'Management Accounts' },
  { id: 'budget-variance', label: 'Budget vs Actual' },
  { id: 'aged-debtors', label: 'Aged Debtors' },
  { id: 'cash-flow', label: 'Cash Flow Forecast' },
  { id: 'project-profitability', label: 'Project Profitability' },
  { id: 'fleet-cost', label: 'Fleet Cost & Utilisation' },
  { id: 'procurement-spend', label: 'Procurement Spend' },
  { id: 'hse-trir', label: 'HSE Incident & TRIR' },
  { id: 'compliance', label: 'Compliance Dashboard' },
]

const TAB_COMPONENTS = {
  'management-accounts': ManagementAccountsTab,
  'budget-variance': BudgetVarianceTab,
  'aged-debtors': AgedDebtorsTab,
  'cash-flow': CashFlowForecastTab,
  'project-profitability': ProjectProfitabilityTab,
  'fleet-cost': FleetCostTab,
  'procurement-spend': ProcurementSpendTab,
  'hse-trir': HseIncidentTrirTab,
  'compliance': ComplianceDashboardTab,
  'schedule': ScheduleTab,
}

export default function ReportsPage() {
  const { hasPermission } = useAuth()
  const canExport = hasPermission('reports.export')
  const canSchedule = hasPermission('reports.schedule')
  const TABS = useMemo(() => [
    ...BASE_TABS,
    ...(canSchedule ? [{ id: 'schedule', label: 'Schedule & Delivery' }] : []),
  ], [canSchedule])
  const [tab, setTab] = useState(BASE_TABS[0].id)
  const [visited, setVisited] = useState({ [BASE_TABS[0].id]: true })

  function go(id) {
    setTab(id)
    setVisited(v => (v[id] ? v : { ...v, [id]: true }))
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <Tabs tabs={TABS} active={tab} setActive={go} />
        {TABS.map(t => {
          const TabComponent = TAB_COMPONENTS[t.id]
          return (
            <div key={t.id} style={{ display: tab === t.id ? 'block' : 'none' }}>
              {visited[t.id] && <TabComponent canExport={canExport} />}
            </div>
          )
        })}
      </div>
    </>
  )
}
