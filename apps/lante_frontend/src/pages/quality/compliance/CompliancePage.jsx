import { useState, useEffect, useCallback, useMemo } from 'react'
import api from '../../../api/axios.js'
import { useAuth } from '../../../context/AuthContext.jsx'
import { Kpi, KPI_GRID, Alert, Tabs, Loading } from '../../../components/ui.jsx'

import GiftsTab from './tabs/GiftsTab.jsx'
import CoiTab from './tabs/CoiTab.jsx'
import WhistleblowerTab from './tabs/WhistleblowerTab.jsx'
import DsrTab from './tabs/DsrTab.jsx'
import BreachesTab from './tabs/BreachesTab.jsx'
import PoliciesTab from './tabs/PoliciesTab.jsx'
import ResolutionsTab from './tabs/ResolutionsTab.jsx'
import LicencesTab from './tabs/LicencesTab.jsx'
import TrainingTab from './tabs/TrainingTab.jsx'
import RelatedPartyTab from './tabs/RelatedPartyTab.jsx'
import IntercompanyTab from './tabs/IntercompanyTab.jsx'
import StatutoryCalendarTab from './tabs/StatutoryCalendarTab.jsx'
import AnnualReturnsTab from './tabs/AnnualReturnsTab.jsx'
import TccTab from './tabs/TccTab.jsx'
import CosecTasksTab from './tabs/CosecTasksTab.jsx'
import DashboardTab from './tabs/DashboardTab.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Compliance & Governance — backed by the compliance-service microservice
// (packages/microservices/compliance) via the gateway's /api/v1/compliance-*
// routes. Follows the same shell/tabs/KPI convention as every other module
// page — AppShell + ui.jsx primitives + T tokens, nothing new introduced.
//
// COMP-003 (whistleblower case tracker) is restricted access: the tab is only
// shown/fetched for users holding compliance.whistleblower.read, kept
// deliberately isolated from the general compliance.read permission both here
// and in the backend's PermissionAuthorizationHandler.
//
// This shell only fetches shared data eagerly on mount (employees, dashboard,
// and — when permitted — statutoryDashboard). Every other resource is owned
// and lazily fetched by its own tab component the first time that tab is
// activated, to avoid firing a burst of ~17 concurrent requests on every
// mount (see tabs/*.jsx). Once a tab has been visited it stays mounted
// (hidden via display:none) so switching back to it does not refetch.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'

export default function CompliancePage() {
  const { hasPermission } = useAuth()
  const canSeeWhistleblower = hasPermission('compliance.whistleblower.read')
  const canSeeStatutory = hasPermission('statutory.read')

  const [tab, setTab] = useState('gifts')
  const [visitedTabs, setVisitedTabs] = useState(() => new Set(['gifts']))
  const [msg, setMsg] = useState(null)
  const [loading, setLoading] = useState(true)

  const [employees, setEmployees] = useState([])
  const [dashboard, setDashboard] = useState(null)
  const [statutoryDashboard, setStatutoryDashboard] = useState(null)

  const refreshDashboard = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/compliance-dashboard')
      setDashboard(res.data?.data ?? null)
    } catch {
      // KPI strip simply keeps its last known values if this fails.
    }
  }, [])

  useEffect(() => {
    let active = true
    async function load() {
      setLoading(true)
      try {
        const [dashRes, usersRes, statDashRes] = await Promise.all([
          api.get('/api/v1/compliance-dashboard').catch(() => ({ data: { data: null } })),
          api.get('/api/v1/users', { params: { pageSize: 200 } }).catch(() => ({ data: { data: { items: [] } } })),
          canSeeStatutory ? api.get('/api/v1/statutory-dashboard').catch(() => ({ data: { data: null } })) : Promise.resolve({ data: { data: null } }),
        ])
        if (!active) return
        setDashboard(dashRes.data?.data ?? null)
        const items = usersRes.data?.data?.items ?? []
        setEmployees(items.map(u => ({ value: u.id, label: `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.email })))
        setStatutoryDashboard(statDashRes.data?.data ?? null)
      } finally {
        if (active) setLoading(false)
      }
    }
    load()
    return () => { active = false }
  }, [canSeeStatutory])

  const employeeName = useCallback(id => employees.find(e => e.value === id)?.label ?? id, [employees])

  function selectTab(id) {
    setTab(id)
    setVisitedTabs(prev => (prev.has(id) ? prev : new Set(prev).add(id)))
  }

  const tabsList = useMemo(() => [
    { id: 'dashboard', label: 'Dashboard' },
    { id: 'gifts', label: 'Gifts & Hospitality' },
    { id: 'coi', label: 'Conflict of Interest' },
    ...(canSeeWhistleblower ? [{ id: 'whistleblower', label: 'Whistleblower' }] : []),
    { id: 'dsr', label: 'Data Subject Requests' },
    { id: 'breaches', label: 'Data Breaches' },
    { id: 'policies', label: 'Policies' },
    { id: 'resolutions', label: 'Board Resolutions' },
    { id: 'licences', label: 'Regulatory Licences' },
    { id: 'training', label: 'Anti-Bribery Training' },
    { id: 'relatedParty', label: 'Related Party' },
    { id: 'intercompany', label: 'Inter-Company' },
    ...(canSeeStatutory ? [
      { id: 'statutoryCalendar', label: 'Statutory Calendar' },
      { id: 'annualReturns', label: 'Annual Returns' },
      { id: 'tcc', label: 'Tax Compliance Cert' },
      { id: 'cosecTasks', label: 'Cosec Tasks' },
    ] : []),
  ], [canSeeWhistleblower, canSeeStatutory])

  function panel(id, node) {
    if (!visitedTabs.has(id)) return null
    return <div style={{ display: tab === id ? 'block' : 'none' }}>{node}</div>
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}

        <Tabs tabs={tabsList} active={tab} setActive={selectTab} />

        {/* KPIs — shown on every tab */}
        <div style={{ ...KPI_GRID, marginBottom: 18 }}>
          <Kpi label="Gifts Flagged" value={dashboard?.giftsFlaggedPendingReview ?? 0} icon="🎁" variant={(dashboard?.giftsFlaggedPendingReview ?? 0) > 0 ? 'amber' : 'green'} />
          <Kpi label="DSR Overdue" value={dashboard?.dsrOverdue ?? 0} icon="📨" variant={(dashboard?.dsrOverdue ?? 0) > 0 ? 'red' : 'green'} />
          <Kpi label="Breaches Pending ODPC" value={dashboard?.dataBreachesPendingOdpcNotification ?? 0} icon="🚨" variant={(dashboard?.dataBreachesPendingOdpcNotification ?? 0) > 0 ? 'red' : 'green'} />
          <Kpi label="Licences Expiring Soon" value={dashboard?.licencesExpiringSoon ?? 0} icon="📄" variant={(dashboard?.licencesExpiringSoon ?? 0) > 0 ? 'amber' : 'green'} />
        </div>

        {loading ? <Loading /> : (
          <>
            {panel('gifts', <GiftsTab employees={employees} employeeName={employeeName} setMsg={setMsg} refreshDashboard={refreshDashboard} />)}
            {panel('coi', <CoiTab employees={employees} employeeName={employeeName} setMsg={setMsg} refreshDashboard={refreshDashboard} />)}
            {canSeeWhistleblower && panel('whistleblower', <WhistleblowerTab setMsg={setMsg} />)}
            {panel('dsr', <DsrTab setMsg={setMsg} refreshDashboard={refreshDashboard} />)}
            {panel('breaches', <BreachesTab setMsg={setMsg} refreshDashboard={refreshDashboard} />)}
            {panel('policies', <PoliciesTab setMsg={setMsg} />)}
            {panel('resolutions', <ResolutionsTab setMsg={setMsg} />)}
            {panel('licences', <LicencesTab setMsg={setMsg} refreshDashboard={refreshDashboard} />)}
            {panel('training', <TrainingTab employees={employees} employeeName={employeeName} setMsg={setMsg} refreshDashboard={refreshDashboard} />)}
            {panel('relatedParty', <RelatedPartyTab setMsg={setMsg} refreshDashboard={refreshDashboard} />)}
            {panel('intercompany', <IntercompanyTab setMsg={setMsg} />)}
            {canSeeStatutory && panel('statutoryCalendar', <StatutoryCalendarTab employees={employees} employeeName={employeeName} setMsg={setMsg} statutoryDashboard={statutoryDashboard} />)}
            {canSeeStatutory && panel('annualReturns', <AnnualReturnsTab setMsg={setMsg} />)}
            {canSeeStatutory && panel('tcc', <TccTab setMsg={setMsg} />)}
            {canSeeStatutory && panel('cosecTasks', <CosecTasksTab employees={employees} employeeName={employeeName} setMsg={setMsg} />)}
            {panel('dashboard', <DashboardTab dashboard={dashboard} canSeeWhistleblower={canSeeWhistleblower} />)}
          </>
        )}
      </div>
    </>
  )
}
