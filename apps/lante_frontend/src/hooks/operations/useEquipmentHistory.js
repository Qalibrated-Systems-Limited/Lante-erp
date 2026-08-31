import { useState, useCallback } from 'react'
import * as opsApi from '../../services/operations.js'

// O11.6 — per-serial equipment service history (the point of FSR_EQUIPMENT). Search on demand.
export function useEquipmentHistory() {
  const [serial, setSerial]   = useState('')
  const [items, setItems]     = useState(null)   // null = not searched yet
  const [loading, setLoading] = useState(false)
  const [error, setError]     = useState('')

  const search = useCallback(async (term) => {
    const q = (term ?? serial).trim()
    if (!q) return
    setLoading(true); setError('')
    try {
      setItems(await opsApi.getEquipmentHistory(q) ?? [])
    } catch {
      setError('Failed to load equipment history.')
      setItems([])
    } finally {
      setLoading(false)
    }
  }, [serial])

  return { serial, setSerial, items, loading, error, search }
}
