import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export function usePipeline() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')

  const [columns, setColumns] = useState([])
  const [stages, setStages] = useState([])
  const [totals, setTotals] = useState({ totalValue: 0, totalWeighted: 0, openCount: 0 })
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const [board, stg] = await Promise.all([crm.getPipelineBoard(), crm.getPipelineStages()])
      setColumns(board.data ?? [])
      setTotals({ totalValue: board.totalValue ?? 0, totalWeighted: board.totalWeighted ?? 0, openCount: board.openCount ?? 0 })
      setStages(stg ?? [])
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to load pipeline.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  return { columns, stages, totals, loading, error, canWrite, reload: load }
}
