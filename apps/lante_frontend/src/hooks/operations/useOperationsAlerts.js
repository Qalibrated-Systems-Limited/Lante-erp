import { useState, useEffect } from 'react'
import * as opsApi from '../../services/operations.js'

const dueSoonOrExpired = (s) => {
  if (s?.isExpired) return true
  if (!s?.nextDueDate) return false
  const days = Math.ceil((new Date(s.nextDueDate) - new Date()) / 86400000)
  return days <= 60
}
const burnPct = (p) => {
  const planned = Number(p?.plannedBudget) || 0
  if (planned <= 0) return 0
  return ((Number(p?.actualCost) || 0) + (Number(p?.committed) || 0)) / planned * 100
}

// O11.7 — derives the operations "needs attention" counts from existing list endpoints (there is no
// single aggregate-alerts API). Surfaces the worker-engine outcomes: calibration expiry, negligence,
// budget burn/lock, and the timesheet approval queue.
export function useOperationsAlerts() {
  const [alerts, setAlerts] = useState({ calibration: 0, negligence: 0, budget: 0, timesheets: 0 })
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let live = true
    setLoading(true)
    Promise.allSettled([
      opsApi.listReferenceStandards({ page: 1, pageSize: 200, activeOnly: true }),
      opsApi.listNegligenceIncidents({ page: 1, pageSize: 100 }),
      opsApi.listProjects({ page: 1, pageSize: 200 }),
      opsApi.listTimesheets({ page: 1, pageSize: 100, status: 'Submitted' }),
    ]).then(([rs, neg, proj, ts]) => {
      if (!live) return
      const val = (r) => (r.status === 'fulfilled' ? (r.value?.items ?? r.value ?? []) : [])
      const stds      = val(rs)
      const incidents = val(neg)
      const projects  = val(proj)
      const sheets    = val(ts)
      setAlerts({
        calibration: stds.filter(dueSoonOrExpired).length,
        negligence:  incidents.filter(i => i.status !== 'Closed').length,
        budget:      projects.filter(p => p.budgetLocked || burnPct(p) >= 90).length,
        timesheets:  sheets.length,
      })
    }).finally(() => { if (live) setLoading(false) })
    return () => { live = false }
  }, [])

  return { alerts, loading }
}
