import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as opsApi from '../../services/operations.js'

// O11.2 — one weekly timesheet: entry CRUD, overtime pre-approval, submit, and line-manager review.
export function useTimesheetDetail(id) {
  const { user, hasPermission } = useAuth()
  const canApprove = hasPermission('operations.approve')

  const [ts, setTs]         = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]   = useState('')
  const [busy, setBusy]     = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      setTs(await opsApi.getTimesheet(id))
    } catch {
      setError('Failed to load timesheet.')
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => { load() }, [load])

  const isOwner  = !!ts && !!user?.id && ts.employeeId === user.id
  const editable = !!ts && isOwner && (ts.status === 'Draft' || ts.status === 'Rejected')
  const canReview = !!ts && canApprove && ts.status === 'Submitted'
  const hasUnapprovedOt = !!ts && (ts.entries ?? []).some(e => e.overtimeHours > 0 && !e.isOvertimeApproved)

  const run = async (fn) => {
    setBusy(true); setError('')
    try { await fn(); await load() }
    catch (e) { setError(e?.response?.data?.message || 'Action failed.'); throw e }
    finally { setBusy(false) }
  }

  return {
    ts, loading, error, busy, isOwner, editable, canReview, hasUnapprovedOt,
    load,
    addEntry:       (dto)          => run(() => opsApi.addTimesheetEntry(id, dto)),
    updateEntry:    (entryId, dto) => run(() => opsApi.updateTimesheetEntry(entryId, dto)),
    deleteEntry:    (entryId)      => run(() => opsApi.deleteTimesheetEntry(entryId)),
    requestOvertime:(entryId, dto) => run(() => opsApi.requestOvertime(entryId, dto)),
    submit:         ()            => run(() => opsApi.submitTimesheet(id)),
    review:         (approved, comments) => run(() => opsApi.reviewTimesheet(id, { approved, comments })),
  }
}
