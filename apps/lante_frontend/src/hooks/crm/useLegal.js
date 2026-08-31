import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export const NDA_STATUS_VARIANT = { Active: 'green', Expired: 'red', Terminated: 'default' }
export const FRAMEWORK_STATUS_VARIANT = { Active: 'green', UnderReview: 'amber', Renewed: 'blue', Expired: 'red', Terminated: 'default' }
export const SUBCONTRACT_STATUS_VARIANT = { Active: 'green', Completed: 'blue', Expired: 'red', Terminated: 'default' }
export const CARRIER_STATUS_VARIANT = { Active: 'green', Suspended: 'amber', Expired: 'red' }
export const VETTING_VARIANT = { Pending: 'amber', Approved: 'green', Rejected: 'red' }

// Legal & contract register hub: summary + NDAs + frameworks + subcontracts + carriers.
export function useLegal() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')

  const [summary, setSummary] = useState(null)
  const [ndas, setNdas] = useState([])
  const [frameworks, setFrameworks] = useState([])
  const [subcontracts, setSubcontracts] = useState([])
  const [carriers, setCarriers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const [su, n, f, s, c] = await Promise.allSettled([
        crm.getLegalSummary(), crm.listNdas(), crm.listFrameworks(), crm.listSubcontracts(), crm.listCarriers(),
      ])
      if (su.status === 'fulfilled') setSummary(su.value)
      if (n.status === 'fulfilled') setNdas(n.value ?? [])
      if (f.status === 'fulfilled') setFrameworks(f.value ?? [])
      if (s.status === 'fulfilled') setSubcontracts(s.value ?? [])
      if (c.status === 'fulfilled') setCarriers(c.value ?? [])
      if ([su, n, f, s, c].every(x => x.status === 'rejected')) setError('Failed to load legal register.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  return { summary, ndas, frameworks, subcontracts, carriers, loading, error, canWrite, reload: load }
}
