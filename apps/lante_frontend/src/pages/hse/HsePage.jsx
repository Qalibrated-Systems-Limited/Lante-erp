import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'
import { KPI_GRID, Kpi, Alert, Tabs, Loading } from '../../components/ui.jsx'

import IncidentsTab from './tabs/IncidentsTab.jsx'
import RamsTab from './tabs/RamsTab.jsx'
import PpeTab from './tabs/PpeTab.jsx'
import ToolboxTalksTab from './tabs/ToolboxTalksTab.jsx'
import TrainingTab from './tabs/TrainingTab.jsx'
import InspectionsTab from './tabs/InspectionsTab.jsx'
import SubcontractorsTab from './tabs/SubcontractorsTab.jsx'
import DashboardTab from './tabs/DashboardTab.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// HSE — Health, Safety & Environment. Backed by the hse-service microservice
// (packages/microservices/hse) via the gateway's /api/v1/hse-* routes.
// Follows the same shell/tabs/KPI convention as every other module page —
// AppShell + ui.jsx primitives + T tokens, nothing new introduced.
//
// Data loading: the old version fired 9 concurrent API calls on every mount
// via a single Promise.all, which caused real backend contention (~20s
// response times under load). This shell now only eagerly fetches the two
// things every tab/the KPI strip needs (employees, dashboard) — each tab
// fetches its own resource lazily, the first time it's activated, and stays
// mounted (hidden via CSS) afterwards so revisiting a tab doesn't refetch.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'

const TABS = [
  { id: 'dashboard', label: 'HSE Dashboard' },
  { id: 'incidents', label: 'Incident Register' },
  { id: 'rams', label: 'RAMS Library' },
  { id: 'ppe', label: 'PPE Tracker' },
  { id: 'toolbox', label: 'Toolbox Talks' },
  { id: 'training', label: 'Training Register' },
  { id: 'inspections', label: 'Statutory Inspections' },
  { id: 'subcontractors', label: 'Subcontractor Prequal' },
]

export default function HsePage() {
  const [tab, setTab] = useState('incidents')
  const [msg, setMsg] = useState(null)
  const [loading, setLoading] = useState(true)

  const [employees, setEmployees] = useState([])
  const [dashboard, setDashboard] = useState(null)

  // Lifted so the always-visible KPI strip can read live values as soon as the
  // Incidents tab (the default tab) has loaded, without adding incidents as a
  // 10th eager call on mount — IncidentsTab fetches into this via setIncidents.
  const [incidents, setIncidents] = useState([])
  const [incidentsPage, setIncidentsPage] = useState(1)
  const [incidentsTotalCount, setIncidentsTotalCount] = useState(0)

  // Lifted and fetched eagerly (not left to SubcontractorsTab's lazy load) —
  // RamsTab's Upload/Edit RAMS forms need this dropdown populated regardless of
  // whether the Subcontractor Prequal tab has been visited yet.
  const [subcontractors, setSubcontractors] = useState([])

  // Tabs render once activated, then stay mounted (display:none when inactive)
  // so switching back to an already-visited tab doesn't refetch its data.
  const [visited, setVisited] = useState({ incidents: true })
  useEffect(() => {
    setVisited(v => (v[tab] ? v : { ...v, [tab]: true }))
  }, [tab])

  // Eager, mount-only fetch: just the two things the KPI strip / every tab's
  // dropdowns need. Everything else loads lazily per-tab.
  useEffect(() => {
    let cancelled = false
    ;(async () => {
      setLoading(true)
      try {
        const [dashRes, usersRes, subRes] = await Promise.all([
          api.get('/api/v1/hse-dashboard').catch(() => ({ data: { data: null } })),
          api.get('/api/v1/users', { params: { pageSize: 200 } }).catch(() => ({ data: { data: { items: [] } } })),
          api.get('/api/v1/subcontractors').catch(() => ({ data: { data: { items: [] } } })),
        ])
        if (cancelled) return
        setDashboard(dashRes.data?.data ?? null)
        const items = usersRes.data?.data?.items ?? []
        setEmployees(items.map(u => ({ value: u.id, label: `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.email })))
        setSubcontractors(subRes.data?.data?.items ?? [])
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => { cancelled = true }
  }, [])

  // Re-fetches the dashboard with no params — matches the old plain load()'s
  // dashboard call, used by every tab after a mutation to refresh KPIs.
  // (Silently falls back to null on failure, same as the old Promise.all catch.)
  const refreshDashboard = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/hse-dashboard')
      setDashboard(res.data?.data ?? null)
    } catch {
      setDashboard(null)
    }
  }, [])

  const openCapas = dashboard?.openCorrectiveActions ?? 0
  const nearMisses = dashboard?.nearMissCount ?? incidents.filter(i => i.type === 0).length
  const incidentsYtd = dashboard?.totalIncidentsYtd ?? incidents.length
  const ltiCount = incidents.filter(i => i.type === 3).length

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}

        <Tabs tabs={TABS} active={tab} setActive={setTab} />

        {/* KPIs — shown on every tab */}
        <div style={{ ...KPI_GRID, marginBottom: 18 }}>
          <Kpi label="Incidents YTD" value={incidentsYtd} icon="🦺" />
          <Kpi label="Lost Time Injuries" value={ltiCount} sub={ltiCount === 0 ? 'Zero LTI' : undefined} icon={ltiCount === 0 ? '✅' : '⚠️'} variant={ltiCount === 0 ? 'green' : 'red'} />
          <Kpi label="Open CAPAs" value={openCapas} icon="🔧" variant={openCapas > 0 ? 'amber' : 'green'} />
          <Kpi label="Near Misses" value={nearMisses} icon="⚠️" variant="amber" />
        </div>

        {loading ? <Loading /> : (
          <>
            <div style={{ display: tab === 'incidents' ? 'block' : 'none' }}>
              {visited.incidents && (
                <IncidentsTab employees={employees} setMsg={setMsg} incidents={incidents} setIncidents={setIncidents}
                  page={incidentsPage} setPage={setIncidentsPage} totalCount={incidentsTotalCount} setTotalCount={setIncidentsTotalCount}
                  refreshDashboard={refreshDashboard} />
              )}
            </div>

            <div style={{ display: tab === 'rams' ? 'block' : 'none' }}>
              {visited.rams && (
                <RamsTab employees={employees} setMsg={setMsg} subcontractors={subcontractors} setSubcontractors={setSubcontractors} refreshDashboard={refreshDashboard} />
              )}
            </div>

            <div style={{ display: tab === 'ppe' ? 'block' : 'none' }}>
              {visited.ppe && (
                <PpeTab employees={employees} setMsg={setMsg} refreshDashboard={refreshDashboard} />
              )}
            </div>

            <div style={{ display: tab === 'toolbox' ? 'block' : 'none' }}>
              {visited.toolbox && (
                <ToolboxTalksTab employees={employees} setMsg={setMsg} refreshDashboard={refreshDashboard} />
              )}
            </div>

            <div style={{ display: tab === 'training' ? 'block' : 'none' }}>
              {visited.training && (
                <TrainingTab employees={employees} setMsg={setMsg} refreshDashboard={refreshDashboard} />
              )}
            </div>

            <div style={{ display: tab === 'inspections' ? 'block' : 'none' }}>
              {visited.inspections && (
                <InspectionsTab setMsg={setMsg} refreshDashboard={refreshDashboard} />
              )}
            </div>

            <div style={{ display: tab === 'subcontractors' ? 'block' : 'none' }}>
              {visited.subcontractors && (
                <SubcontractorsTab setMsg={setMsg} subcontractors={subcontractors} setSubcontractors={setSubcontractors} refreshDashboard={refreshDashboard} />
              )}
            </div>

            <div style={{ display: tab === 'dashboard' ? 'block' : 'none' }}>
              {visited.dashboard && (
                <DashboardTab dashboard={dashboard} setDashboard={setDashboard} setMsg={setMsg} setTab={setTab} />
              )}
            </div>
          </>
        )}
      </div>
    </>
  )
}
