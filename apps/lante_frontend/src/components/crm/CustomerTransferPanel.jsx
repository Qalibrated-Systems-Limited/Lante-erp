import { useState, useEffect, useCallback } from 'react'
import { Btn, Modal, Badge } from '../ui.jsx'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

const fmtDate = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'
const STEPS = [
  { key: 'PendingHeadBd', label: 'Head of BD' },
  { key: 'PendingCfo', label: 'CFO' },
  { key: 'PendingMd', label: 'MD' },
  { key: 'PendingHandover', label: 'Handover' },
  { key: 'Completed', label: 'Done' },
]
const SIG_ROLES = [['Outgoing', 'Outgoing owner'], ['Incoming', 'Incoming owner'], ['DeptHead', 'Department Head'], ['Md', 'MD']]

// C8 — account-ownership transfer panel for a single customer (Customer detail page).
export default function CustomerTransferPanel({ customer, onOwnerChanged }) {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')
  const canBd = hasPermission?.('crm.approve.bd'), canCfo = hasPermission?.('crm.approve.cfo'), canMd = hasPermission?.('crm.approve.md')
  const [tr, setTr] = useState(null)
  const [modal, setModal] = useState(null)  // 'raise' | 'sign'
  const [raise, setRaise] = useState({ incomingOwnerId: '', incomingOwnerName: '', reason: '', effectiveDate: '' })
  const [sig, setSig] = useState({ role: 'Outgoing', signatoryName: '' })
  const [toast, setToast] = useState('')
  const [busy, setBusy] = useState(false)

  const flash = (m) => { setToast(m); setTimeout(() => setToast(''), 3500) }
  const load = useCallback(async () => {
    try {
      const res = await crm.listTransfers({ customerId: customer.id })
      const active = (res.data ?? []).find(t => !['Completed', 'Rejected'].includes(t.status))
      setTr(active ? await crm.getTransfer(active.id) : null)
    } catch { /* silent */ }
  }, [customer.id])
  useEffect(() => { load() }, [load])

  const run = async (fn, ok, ownerMaybeChanged) => {
    setBusy(true)
    try { await fn(); await load(); flash(ok); if (ownerMaybeChanged) onOwnerChanged?.() }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }
  const doRaise = async () => {
    if (!raise.incomingOwnerId.trim() || !raise.reason.trim()) return flash('Incoming owner and reason required.')
    setBusy(true)
    try { await crm.raiseTransfer({ ...raise, customerId: customer.id, effectiveDate: raise.effectiveDate || undefined }); setModal(null); setRaise({ incomingOwnerId: '', incomingOwnerName: '', reason: '', effectiveDate: '' }); await load(); flash('Transfer raised.') }
    catch (e) { flash(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  const stepIdx = tr ? STEPS.findIndex(s => s.key === tr.status) : -1

  return (
    <section className="bg-white border border-gray-200 rounded-xl p-5 mt-6">
      {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-sm font-bold text-gray-700">Account Ownership</h2>
        {!tr && canWrite && customer.status === 'Active' && <button onClick={() => setModal('raise')} className="text-sm font-semibold text-navy hover:underline">Transfer Ownership</button>}
      </div>
      <p className="text-sm text-gray-600 mb-3">Owner: <span className="font-semibold text-navy">{customer.accountOwnerName ?? customer.accountOwnerId}</span></p>

      {tr && (
        <div className="border border-gray-100 rounded-lg p-3">
          <div className="flex items-center gap-2 mb-3 flex-wrap">
            <Badge variant={tr.status === 'Rejected' ? 'red' : tr.status === 'Completed' ? 'green' : 'amber'}>{tr.status}</Badge>
            <span className="text-xs text-gray-500">→ {tr.incomingOwnerName ?? tr.incomingOwnerId} · {tr.reason}</span>
          </div>
          {/* Stepper */}
          <div className="flex items-center justify-between mb-3">
            {STEPS.map((s, i) => (
              <div key={s.key} className="flex-1 flex flex-col items-center relative">
                {i > 0 && <div className={`absolute top-3 right-1/2 w-full h-0.5 ${stepIdx >= i ? 'bg-navy' : 'bg-gray-200'}`} />}
                <div className={`relative z-10 w-6 h-6 rounded-full flex items-center justify-center text-[11px] font-bold ${stepIdx > i ? 'bg-navy text-white' : stepIdx === i ? 'bg-gold text-white' : 'bg-gray-200 text-gray-500'}`}>{stepIdx > i ? '✓' : i + 1}</div>
                <span className="text-[10px] mt-1 text-gray-500">{s.label}</span>
              </div>
            ))}
          </div>

          {/* Approvals */}
          {canWrite && (
            <div className="flex gap-2 flex-wrap">
              {tr.status === 'PendingHeadBd' && canBd && <Btn size="sm" onClick={() => run(() => crm.approveTransferHeadBd(tr.id), 'Endorsed.')} disabled={busy}>Head of BD Endorse</Btn>}
              {tr.status === 'PendingCfo' && canCfo && <Btn size="sm" onClick={() => run(() => crm.approveTransferCfo(tr.id), 'CFO reviewed.')} disabled={busy}>CFO Review</Btn>}
              {tr.status === 'PendingMd' && canMd && <Btn size="sm" onClick={() => run(() => crm.approveTransferMd(tr.id), 'MD approved.')} disabled={busy}>MD Approve</Btn>}
              {['PendingHeadBd','PendingCfo','PendingMd'].includes(tr.status) && canBd &&
                <Btn size="sm" variant="danger" onClick={() => { const r = prompt('Rejection reason:'); if (r) run(() => crm.rejectTransfer(tr.id, { reason: r }), 'Rejected.') }} disabled={busy}>Reject</Btn>}
            </div>
          )}

          {/* Handover signatures */}
          {tr.status === 'PendingHandover' && tr.handover && (
            <div className="mt-3 pt-3 border-t border-gray-100">
              <p className="text-xs font-bold text-gray-700 mb-2">Status Handover Document — signatures</p>
              <div className="grid grid-cols-2 gap-2 mb-3">
                {SIG_ROLES.map(([role, label]) => {
                  const name = tr.handover[`${role.charAt(0).toLowerCase() + role.slice(1)}SignedName`]
                  return (
                    <div key={role} className="flex items-center justify-between border border-gray-100 rounded px-2 py-1.5">
                      <span className="text-xs text-gray-600">{label}</span>
                      {name ? <span className="text-xs text-green-600">✓ {name}</span> : (canWrite && <button onClick={() => { setSig({ role, signatoryName: '' }); setModal('sign') }} className="text-xs text-navy hover:underline">Sign</button>)}
                    </div>
                  )
                })}
              </div>
              {canMd && <Btn size="sm" variant="green" disabled={busy || (tr.handover.missingSignatures?.length ?? 1) > 0}
                onClick={() => run(() => crm.completeTransfer(tr.id), 'Ownership transferred.', true)}>Complete Transfer</Btn>}
              {(tr.handover.missingSignatures?.length ?? 0) > 0 && <p className="text-[11px] text-gray-400 mt-1">Missing: {tr.handover.missingSignatures.join(', ')}</p>}
            </div>
          )}
        </div>
      )}

      {modal === 'raise' && (
        <Modal title="Transfer Account Ownership" onClose={() => setModal(null)} width={440}>
          <Fld label="Incoming owner (user id) *"><input value={raise.incomingOwnerId} onChange={e => setRaise(r => ({ ...r, incomingOwnerId: e.target.value }))} className="input" /></Fld>
          <Fld label="Incoming owner name"><input value={raise.incomingOwnerName} onChange={e => setRaise(r => ({ ...r, incomingOwnerName: e.target.value }))} className="input" /></Fld>
          <Fld label="Reason *"><textarea rows={2} value={raise.reason} onChange={e => setRaise(r => ({ ...r, reason: e.target.value }))} className="input" /></Fld>
          <Fld label="Effective date"><input type="date" value={raise.effectiveDate} onChange={e => setRaise(r => ({ ...r, effectiveDate: e.target.value }))} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn><Btn onClick={doRaise} disabled={busy}>Raise</Btn></div>
        </Modal>
      )}
      {modal === 'sign' && (
        <Modal title={`Sign — ${SIG_ROLES.find(r => r[0] === sig.role)?.[1]}`} onClose={() => setModal(null)} width={380}>
          <Fld label="Signatory name *"><input value={sig.signatoryName} onChange={e => setSig(s => ({ ...s, signatoryName: e.target.value }))} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn><Btn onClick={() => sig.signatoryName.trim() && run(() => crm.signHandover(tr.id, sig), 'Signed.').then(() => setModal(null))} disabled={busy}>Sign</Btn></div>
        </Modal>
      )}
    </section>
  )
}

function Fld({ label, children }) {
  return <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">{label}</span>{children}</label>
}
