import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as opsApi from '../../services/operations.js'

// O11.6 — a single Field Service Report: load its 8 sections + equipment, and (for managers) review
// it. Approving triggers the backend FSR time auto-import onto the technician's timesheet.
export function useServiceReport(id) {
  const { hasPermission } = useAuth()
  const canReview = hasPermission('operations.approve')

  const [report, setReport] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]   = useState('')
  const [busy, setBusy]     = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      setReport(await opsApi.getServiceReport(id))
    } catch {
      setError('Failed to load service report.')
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => { load() }, [load])

  const review = async (approved, comments) => {
    setBusy(true); setError('')
    try { await opsApi.reviewServiceReport(id, { approved, comments }); await load() }
    catch (e) { setError(e?.response?.data?.message || 'Review failed.'); throw e }
    finally { setBusy(false) }
  }

  const reviewable = !!report && canReview && (report.status === 'Submitted' || report.status === 'UnderReview')

  return { report, loading, error, busy, canReview, reviewable, load, review }
}
