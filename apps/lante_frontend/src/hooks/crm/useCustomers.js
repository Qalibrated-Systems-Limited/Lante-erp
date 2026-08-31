import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export const TIERS = ['Standard', 'Silver', 'Gold', 'Platinum']
export const CUSTOMER_TYPES = ['Company', 'Individual', 'Government', 'Ngo', 'Other']
export const CUSTOMER_STATUSES = [
  'PendingLineManager', 'PendingHeadBd', 'PendingCfo', 'PendingMd', 'Active', 'Rejected', 'Inactive',
]

// Badge variant + human label per status.
export const STATUS_VARIANT = {
  PendingLineManager: 'amber', PendingHeadBd: 'amber', PendingCfo: 'amber', PendingMd: 'amber',
  Active: 'green', Rejected: 'red', Inactive: 'default',
}
export const statusLabel = (s) => (s || '').replace(/([A-Z])/g, ' $1').replace('Bd', 'BD').trim()

export function useCustomers() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')

  const [customers, setCustomers] = useState([])
  const [total, setTotal] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [search, setSearch] = useState('')
  const [statusF, setStatusF] = useState('')
  const [activityF, setActivityF] = useState('')   // '' | 'active' | 'dormant'
  const [tierF, setTierF] = useState('')
  const [page, setPage] = useState(1)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const res = await crm.listCustomers({
        search: search || undefined,
        status: statusF || undefined,
        activity: activityF || undefined,
        accountTier: tierF || undefined,
        page, pageSize: 20,
      })
      setCustomers(res.data ?? [])
      setTotal(res.total ?? 0)
      setTotalPages(res.pages ?? 1)
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to load customers.')
    } finally {
      setLoading(false)
    }
  }, [search, statusF, activityF, tierF, page])

  useEffect(() => { load() }, [load])

  return {
    customers, total, totalPages, loading, error, canWrite,
    search, setSearch, statusF, setStatusF, activityF, setActivityF, tierF, setTierF, page, setPage,
    reload: load,
    createCustomer: crm.createCustomer,
    checkDuplicate: crm.checkCustomerDuplicate,
  }
}
