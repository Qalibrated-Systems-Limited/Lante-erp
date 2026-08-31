import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export const CONTRACT_TYPES = ['Calibration', 'Maintenance', 'Support', 'Other']
export const CONTRACT_STATUSES = ['Active', 'Expired', 'Renewed', 'Cancelled']
export const CONTRACT_STATUS_VARIANT = { Active: 'green', Expired: 'red', Renewed: 'blue', Cancelled: 'default' }

export const COMPLAINT_SEVERITIES = ['Low', 'Medium', 'High', 'Critical']
export const COMPLAINT_STATUSES = ['Open', 'Assigned', 'InProgress', 'Resolved', 'Closed']
export const COMPLAINT_STATUS_VARIANT = { Open: 'red', Assigned: 'amber', InProgress: 'blue', Resolved: 'green', Closed: 'default' }
export const SEVERITY_VARIANT = { Low: 'default', Medium: 'blue', High: 'amber', Critical: 'red' }
export const complaintStatusLabel = (s) => (s || '').replace('InProgress', 'In Progress')

export const SURVEY_STATUS_VARIANT = { Pending: 'amber', Completed: 'green', Expired: 'default' }
export const NPS_CATEGORY_VARIANT = { Promoter: 'green', Passive: 'amber', Detractor: 'red', None: 'default' }

// After-sales & retention hub: summary + surveys + service contracts + complaints + NPS.
export function useAfterSales() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')

  const [summary, setSummary] = useState(null)
  const [surveys, setSurveys] = useState([])
  const [contracts, setContracts] = useState([])
  const [complaints, setComplaints] = useState([])
  const [nps, setNps] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const [su, sv, sc, cp, np] = await Promise.allSettled([
        crm.getAfterSalesSummary(), crm.listSurveys(), crm.listServiceContracts(),
        crm.listComplaints(), crm.listNps(),
      ])
      if (su.status === 'fulfilled') setSummary(su.value)
      if (sv.status === 'fulfilled') setSurveys(sv.value ?? [])
      if (sc.status === 'fulfilled') setContracts(sc.value ?? [])
      if (cp.status === 'fulfilled') setComplaints(cp.value ?? [])
      if (np.status === 'fulfilled') setNps(np.value ?? [])
      if ([su, sv, sc, cp, np].every(x => x.status === 'rejected')) setError('Failed to load after-sales data.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  return { summary, surveys, contracts, complaints, nps, loading, error, canWrite, reload: load }
}
