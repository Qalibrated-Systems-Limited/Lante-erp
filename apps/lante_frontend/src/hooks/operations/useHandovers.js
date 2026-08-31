import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as opsApi from '../../services/operations.js'

// O11.4 — project handovers: 8-step checklist, four mandatory signatures, permanent on completion.
export function useHandovers(projectId) {
  const { hasPermission } = useAuth()
  const canWrite    = hasPermission('projects.write')
  const canComplete = hasPermission('projects.approve')

  const [items, setItems]     = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')

  const load = useCallback(async () => {
    if (!projectId) return
    setLoading(true); setError('')
    try {
      setItems(await opsApi.listHandoversByProject(projectId) ?? [])
    } catch {
      setError('Failed to load handovers.')
    } finally {
      setLoading(false)
    }
  }, [projectId])

  useEffect(() => { load() }, [load])

  const create   = async (dto) => { const h = await opsApi.createHandover({ ...dto, projectId }); await load(); return h }
  const update   = async (id, dto) => { await opsApi.updateHandover(id, dto); await load() }
  const sign     = async (id, dto) => { await opsApi.addHandoverSignature(id, dto); await load() }
  const complete = async (id) => { await opsApi.completeHandover(id); await load() }
  const remove   = async (id) => { await opsApi.deleteHandover(id); await load() }

  return { items, loading, error, canWrite, canComplete, load, create, update, sign, complete, remove }
}

export const HANDOVER_STEPS = [
  'All deliverables handed over',
  'Outstanding works / defects list agreed',
  'As-built documents & manuals provided',
  'Warranties & guarantees transferred',
  'Client training completed',
  'Site cleared & made safe',
  'Final account / retention agreed',
  'Client acceptance obtained',
]

export const SIGNATURE_ROLES = [
  { value: 'ProjectManager', label: 'Project Manager' },
  { value: 'DepartmentHead', label: 'Department Head' },
  { value: 'ClientRepresentative', label: 'Client Representative' },
  { value: 'QualityAssurance', label: 'Quality Assurance' },
]
