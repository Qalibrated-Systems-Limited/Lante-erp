import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export const ALERT_TYPES = [
  'SupplierInvoiceDue', 'ClientInvoiceOverdue', 'SupplierUnauthorised',
  'DebtorOver45', 'DebtorOver60', 'WeeklyCfoSummary', 'MonthlyMdDashboard',
]
export const SEVERITY_VARIANT = { Critical: 'red', Warning: 'amber', Info: 'blue' }
export const ALERT_STATUS_VARIANT = { Open: 'amber', Acknowledged: 'green' }
export const alertTypeLabel = (t) => ({
  SupplierInvoiceDue: 'Supplier Invoice Due', ClientInvoiceOverdue: 'Client Invoice Overdue',
  SupplierUnauthorised: 'Voucher Unauthorised', DebtorOver45: 'Debtor > 45 days',
  DebtorOver60: 'Debtor > 60 days', WeeklyCfoSummary: 'Weekly CFO Summary',
  MonthlyMdDashboard: 'Monthly MD Dashboard',
}[t] ?? t)

// Payment/debtor alerts hub: live Finance-backed summary + the logged alert feed.
export function usePaymentAlerts() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')

  const [summary, setSummary] = useState(null)
  const [alerts, setAlerts] = useState([])
  const [statusF, setStatusF] = useState('')
  const [severityF, setSeverityF] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const [s, a] = await Promise.allSettled([
        crm.getPaymentAlertSummary(),
        crm.listPaymentAlerts({ status: statusF || undefined, severity: severityF || undefined }),
      ])
      if (s.status === 'fulfilled') setSummary(s.value)
      if (a.status === 'fulfilled') setAlerts(a.value ?? [])
      if (s.status === 'rejected' && a.status === 'rejected') setError('Failed to load payment alerts.')
    } finally {
      setLoading(false)
    }
  }, [statusF, severityF])

  useEffect(() => { load() }, [load])

  return { summary, alerts, loading, error, canWrite, statusF, setStatusF, severityF, setSeverityF, reload: load }
}
