import { useState, useEffect } from 'react'
import * as opsApi from '../../services/operations.js'

// O11.1 — loads the budget-burn alert log + HSE summary for a project (the new O2/O8 surfaces).
// Self-contained so it can drop into the existing ProjectDetailPage overview without touching its state.
export function useProjectCompliance(projectId) {
  const [alerts, setAlerts] = useState([])
  const [hse, setHse]       = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let live = true
    if (!projectId) return
    setLoading(true)
    Promise.allSettled([opsApi.getBudgetAlerts(projectId), opsApi.getProjectHseSummary(projectId)])
      .then(([a, h]) => {
        if (!live) return
        setAlerts(a.status === 'fulfilled' ? (a.value ?? []) : [])
        setHse(h.status === 'fulfilled' ? h.value : null)
      })
      .finally(() => { if (live) setLoading(false) })
    return () => { live = false }
  }, [projectId])

  return { alerts, hse, loading }
}

// Burn exposure = (spent + committed) / planned, matching the backend burn engine.
export function burnPercent(project) {
  const planned = Number(project?.plannedBudget) || 0
  if (planned <= 0) return 0
  const exposure = (Number(project?.actualCost) || 0) + (Number(project?.committed) || 0)
  return Math.round((exposure / planned) * 1000) / 10
}
