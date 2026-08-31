import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as opsApi from '../../services/operations.js'

// O11.4 — negligence register: report incidents, respond (HR/DeptHead/MD), payroll-deduction, and
// the 24h-late / 5-day-response / repeat-offense surfacing.
export function useNegligence() {
  const { hasPermission } = useAuth()
  const canReport  = hasPermission('operations.write')
  const canRespond = hasPermission('operations.approve')

  const [items, setItems]       = useState([])
  const [loading, setLoading]   = useState(true)
  const [error, setError]       = useState('')
  const [statusFilter, setStatus] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const params = { page: 1, pageSize: 100 }
      if (statusFilter) params.status = statusFilter
      const res = await opsApi.listNegligenceIncidents(params)
      setItems(res?.items ?? [])
    } catch {
      setError('Failed to load negligence incidents.')
    } finally {
      setLoading(false)
    }
  }, [statusFilter])

  useEffect(() => { load() }, [load])

  const report  = async (dto)      => { await opsApi.reportNegligence(dto); await load() }
  const respond = async (id, dto)  => { await opsApi.respondNegligence(id, dto); await load() }
  const getOne  = (id)             => opsApi.getNegligenceIncident(id)

  return { items, loading, error, statusFilter, setStatus, canReport, canRespond, load, report, respond, getOne }
}

export const SEVERITIES = ['Minor', 'Moderate', 'Major', 'Critical']
export const RESPONDER_ROLES = ['HR', 'DepartmentHead', 'MD']
