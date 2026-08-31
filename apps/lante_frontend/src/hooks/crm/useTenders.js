import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export const TENDER_STATUSES = ['Registered', 'Submitted', 'Won', 'Lost', 'NoBid', 'Cancelled']
export const TENDER_STATUS_VARIANT = {
  Registered: 'blue', Submitted: 'amber', Won: 'green', Lost: 'red', NoBid: 'default', Cancelled: 'default',
}
export const tLabel = (s) => (s || '').replace('NoBid', 'No Bid')

export function useTenders() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')

  const [tenders, setTenders] = useState([])
  const [total, setTotal] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [kpi, setKpi] = useState({ openCount: 0, dueSoonCount: 0, openValue: 0 })
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [statusF, setStatusF] = useState('')
  const [page, setPage] = useState(1)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const res = await crm.listTenders({ search: search || undefined, status: statusF || undefined, page, pageSize: 20 })
      setTenders(res.data ?? [])
      setTotal(res.total ?? 0)
      setTotalPages(res.pages ?? 1)
      setKpi({ openCount: res.openCount ?? 0, dueSoonCount: res.dueSoonCount ?? 0, openValue: res.openValue ?? 0 })
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to load tenders.')
    } finally {
      setLoading(false)
    }
  }, [search, statusF, page])

  useEffect(() => { load() }, [load])

  return { tenders, total, totalPages, kpi, loading, error, canWrite, search, setSearch, statusF, setStatusF, page, setPage, reload: load }
}
