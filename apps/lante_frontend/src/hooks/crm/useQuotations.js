import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export const QUOTE_STATUSES = ['Draft', 'PendingDeptHead', 'PendingMd', 'Approved', 'Sent', 'Accepted', 'Rejected', 'Expired', 'Superseded']
export const QUOTE_STATUS_VARIANT = {
  Draft: 'default', PendingDeptHead: 'amber', PendingMd: 'amber', Approved: 'blue',
  Sent: 'blue', Accepted: 'green', Rejected: 'red', Expired: 'default', Superseded: 'default',
}
export const qLabel = (s) => (s || '').replace(/([A-Z])/g, ' $1').replace('Md', 'MD').replace('Dept', 'Dept ').trim()

export function useQuotations() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')
  const canApproveBd = hasPermission?.('crm.approve.bd')
  const canApproveMd = hasPermission?.('crm.approve.md')

  const [quotes, setQuotes] = useState([])
  const [total, setTotal] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [statusF, setStatusF] = useState('')
  const [page, setPage] = useState(1)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const res = await crm.listQuotations({ status: statusF || undefined, currentOnly: true, page, pageSize: 20 })
      setQuotes(res.data ?? [])
      setTotal(res.total ?? 0)
      setTotalPages(res.pages ?? 1)
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to load quotations.')
    } finally {
      setLoading(false)
    }
  }, [statusF, page])

  useEffect(() => { load() }, [load])

  return { quotes, total, totalPages, loading, error, canWrite, canApproveBd, canApproveMd, statusF, setStatusF, page, setPage, reload: load }
}
