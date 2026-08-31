import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as opsApi from '../../services/operations.js'

// O11.2 — timesheet list + line-manager approval queue. `view` toggles between the caller's own
// timesheets and the queue of submitted sheets awaiting review (needs operations.approve).
export function useTimesheets() {
  const { hasPermission } = useAuth()
  const canApprove = hasPermission('operations.approve')

  const [items, setItems]           = useState([])
  const [loading, setLoading]       = useState(true)
  const [error, setError]           = useState('')
  const [view, setView]             = useState('mine')       // 'mine' | 'queue'
  const [statusFilter, setStatus]   = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const params = { page: 1, pageSize: 100 }
      if (view === 'queue') params.status = 'Submitted'
      else if (statusFilter) params.status = statusFilter
      const res = await opsApi.listTimesheets(params)
      setItems(res?.items ?? [])
    } catch {
      setError('Failed to load timesheets.')
    } finally {
      setLoading(false)
    }
  }, [view, statusFilter])

  useEffect(() => { load() }, [load])

  const createForWeek = async (weekStartDate) => {
    const ts = await opsApi.createTimesheet({ weekStartDate })
    await load()
    return ts
  }

  return { items, loading, error, view, setView, statusFilter, setStatus, canApprove, load, createForWeek }
}
