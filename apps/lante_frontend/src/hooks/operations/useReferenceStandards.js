import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as opsApi from '../../services/operations.js'

// O11.5 — data + CRUD for the calibration reference-standard register. Returns everything the
// ReferenceStandardsPage shell needs (list, loading/error, permission flags, save/remove actions).
export function useReferenceStandards() {
  const { hasPermission } = useAuth()
  const canWrite  = hasPermission('operations.write')
  const canDelete = hasPermission('operations.delete') || canWrite

  const [items, setItems]         = useState([])
  const [loading, setLoading]     = useState(true)
  const [error, setError]         = useState('')
  const [activeOnly, setActiveOnly] = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const res = await opsApi.listReferenceStandards({ page: 1, pageSize: 200, activeOnly })
      setItems(res?.items ?? [])
    } catch {
      setError('Failed to load reference standards.')
    } finally {
      setLoading(false)
    }
  }, [activeOnly])

  useEffect(() => { load() }, [load])

  const save = async (dto, id) => {
    if (id) await opsApi.updateReferenceStandard(id, dto)
    else    await opsApi.createReferenceStandard(dto)
    await load()
  }

  const remove = async (id) => {
    await opsApi.deleteReferenceStandard(id)
    await load()
  }

  return { items, loading, error, activeOnly, setActiveOnly, canWrite, canDelete, load, save, remove }
}

// Expiry helper — maps a standard's NextDueDate to a { variant, label } badge descriptor.
export function expiryBadge(std) {
  if (!std?.nextDueDate) return { variant: 'default', label: 'No due date' }
  const due = new Date(std.nextDueDate)
  const days = Math.ceil((due - new Date()) / 86400000)
  if (std.isExpired || days < 0) return { variant: 'red', label: 'Expired' }
  if (days <= 30) return { variant: 'red', label: `Due in ${days}d` }
  if (days <= 60) return { variant: 'amber', label: `Due in ${days}d` }
  return { variant: 'green', label: due.toISOString().slice(0, 10) }
}
