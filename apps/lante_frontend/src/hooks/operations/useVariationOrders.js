import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as opsApi from '../../services/operations.js'

// O11.3 — variation orders for a project: list + the Draft → MD → client-approval workflow.
export function useVariationOrders(projectId) {
  const { hasPermission } = useAuth()
  const canWrite     = hasPermission('projects.write')
  const canApproveMd = hasPermission('projects.approve')

  const [items, setItems]     = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')

  const load = useCallback(async () => {
    if (!projectId) return
    setLoading(true); setError('')
    try {
      setItems(await opsApi.listVariationOrdersByProject(projectId) ?? [])
    } catch {
      setError('Failed to load variation orders.')
    } finally {
      setLoading(false)
    }
  }, [projectId])

  useEffect(() => { load() }, [load])

  const save = async (dto, id) => {
    if (id) await opsApi.updateVariationOrder(id, dto)
    else    await opsApi.createVariationOrder({ ...dto, projectId })
    await load()
  }
  const remove        = async (id) => { await opsApi.deleteVariationOrder(id); await load() }
  const submit        = async (id) => { await opsApi.submitVariationOrder(id); await load() }
  const approveMd     = async (id, approved, comments) => { await opsApi.approveVariationOrderMd(id, { approved, comments }); await load() }
  const approveClient = async (id, clientApprovedBy) => { await opsApi.approveVariationOrderClient(id, { clientApprovedBy }); await load() }

  return { items, loading, error, canWrite, canApproveMd, load, save, remove, submit, approveMd, approveClient }
}
