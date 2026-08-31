import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

// The onboarding stage → the action available NOW, with the permission that gates it.
// Returns null once the client is Active/Rejected/Inactive (no pending action).
export function nextStage(status) {
  switch (status) {
    case 'PendingLineManager': return { key: 'line-manager', label: 'Approve (Line Manager)', perm: 'crm.approve.linemanager' }
    case 'PendingHeadBd':      return { key: 'head-bd',      label: 'Approve (Head of BD)',   perm: 'crm.approve.bd' }
    case 'PendingCfo':         return { key: 'cfo',          label: 'CFO Credit Review',      perm: 'crm.approve.cfo' }
    case 'PendingMd':          return { key: 'md',           label: 'Approve (MD)',           perm: 'crm.approve.md' }
    default: return null
  }
}

export function useCustomerDetail(id) {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')
  const canReject = hasPermission?.('crm.approve.bd')

  const [customer, setCustomer] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setCustomer(await crm.getCustomer(id)) }
    catch (e) { setError(e.response?.data?.message ?? 'Failed to load customer.') }
    finally { setLoading(false) }
  }, [id])

  useEffect(() => { load() }, [load])

  const stage = customer ? nextStage(customer.status) : null
  const canActOnStage = stage ? hasPermission?.(stage.perm) : false

  // Runs an action, refreshes, returns {ok, message}.
  const run = useCallback(async (fn) => {
    setBusy(true)
    try { const r = await fn(); await load(); return { ok: true, message: r?.message } }
    catch (e) { return { ok: false, message: e.response?.data?.message ?? 'Action failed.' } }
    finally { setBusy(false) }
  }, [load])

  const approveStage = (dto) => run(() => {
    switch (stage?.key) {
      case 'line-manager': return crm.approveCustomerLineManager(id)
      case 'head-bd':      return crm.approveCustomerHeadBd(id)
      case 'cfo':          return crm.cfoReviewCustomer(id, dto)
      case 'md':           return crm.approveCustomerMd(id)
      default: return Promise.reject(new Error('No pending stage.'))
    }
  })

  return {
    customer, loading, error, busy, canWrite, canReject,
    stage, canActOnStage,
    reload: load,
    approveStage,
    reject: (reason) => run(() => crm.rejectCustomer(id, { reason })),
    deactivate: () => run(() => crm.deactivateCustomer(id)),
    addContact: (dto) => run(() => crm.addCustomerContact(id, dto)),
    updateContact: (contactId, dto) => run(() => crm.updateCustomerContact(contactId, dto)),
  }
}
