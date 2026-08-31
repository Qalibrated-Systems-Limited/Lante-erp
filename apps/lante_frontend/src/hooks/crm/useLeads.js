import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export const LEAD_SOURCES = ['Web', 'Referral', 'Tender', 'ColdCall', 'Exhibition', 'Social', 'Other']
export const LEAD_STATUSES = ['New', 'Contacted', 'Qualified', 'Unqualified', 'Converted']
export const LEAD_RATINGS = ['Hot', 'Warm', 'Cold']

export const LEAD_STATUS_VARIANT = {
  New: 'blue', Contacted: 'amber', Qualified: 'green', Unqualified: 'default', Converted: 'navy',
}
export const RATING_VARIANT = { Hot: 'red', Warm: 'amber', Cold: 'blue' }
export const label = (s) => (s || '').replace(/([A-Z])/g, ' $1').trim()

export function useLeads() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')

  const [leads, setLeads] = useState([])
  const [total, setTotal] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [openCount, setOpenCount] = useState(0)
  const [openValue, setOpenValue] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [search, setSearch] = useState('')
  const [statusF, setStatusF] = useState('')
  const [sourceF, setSourceF] = useState('')
  const [staleOnly, setStaleOnly] = useState(false)
  const [page, setPage] = useState(1)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const res = await crm.listLeads({
        search: search || undefined, status: statusF || undefined, source: sourceF || undefined,
        stale: staleOnly || undefined, page, pageSize: 20,
      })
      setLeads(res.data ?? [])
      setTotal(res.total ?? 0)
      setTotalPages(res.pages ?? 1)
      setOpenCount(res.openCount ?? 0)
      setOpenValue(res.openValue ?? 0)
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to load leads.')
    } finally {
      setLoading(false)
    }
  }, [search, statusF, sourceF, staleOnly, page])

  useEffect(() => { load() }, [load])

  return {
    leads, total, totalPages, openCount, openValue, loading, error, canWrite,
    search, setSearch, statusF, setStatusF, sourceF, setSourceF, staleOnly, setStaleOnly, page, setPage,
    reload: load,
  }
}
