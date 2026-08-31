import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Badge, Btn, Modal } from '../../components/ui.jsx'
import { useCustomerDetail } from '../../hooks/crm/useCustomerDetail.js'
import { STATUS_VARIANT, statusLabel } from '../../hooks/crm/useCustomers.js'
import CustomerActivityPanel from '../../components/crm/CustomerActivityPanel.jsx'
import CustomerTransferPanel from '../../components/crm/CustomerTransferPanel.jsx'

const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'

const STEPS = [
  { key: 'PendingLineManager', label: 'Line Manager' },
  { key: 'PendingHeadBd', label: 'Head of BD' },
  { key: 'PendingCfo', label: 'CFO Credit' },
  { key: 'PendingMd', label: 'MD Approval' },
  { key: 'Active', label: 'Active' },
]

export default function CustomerDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const d = useCustomerDetail(id)
  const [toast, setToast] = useState('')
  const [modal, setModal] = useState(null)   // 'cfo' | 'reject' | 'contact'
  const [cfo, setCfo] = useState({ creditLimit: '', creditTermsDays: '', notes: '' })
  const [reason, setReason] = useState('')
  const [contact, setContact] = useState({ firstName: '', lastName: '', jobTitle: '', email: '', phone: '', isPrimary: false })

  const flash = (m) => { setToast(m); setTimeout(() => setToast(''), 3500) }
  const after = (r) => { flash(r.message || (r.ok ? 'Done.' : 'Failed.')); if (r.ok) setModal(null) }

  if (d.loading) return <div className="p-10 text-center text-sm text-gray-400">Loading…</div>
  if (d.error || !d.customer) return <div className="p-10 text-center text-sm text-red-500">{d.error || 'Not found.'}</div>

  const c = d.customer
  const stepIdx = STEPS.findIndex(s => s.key === c.status)
  const isRejected = c.status === 'Rejected'
  const isActive = c.status === 'Active'

  const doApprove = () => {
    if (d.stage?.key === 'cfo') { setCfo({ creditLimit: '', creditTermsDays: '', notes: '' }); setModal('cfo') }
    else d.approveStage().then(after)
  }

  return (
    <>
      {toast && <div className="fixed bottom-6 right-6 z-50 bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}

      <main className="flex-1 max-w-5xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <button onClick={() => navigate('/modules/crm')} className="text-sm text-gray-500 hover:text-navy mb-4">← Back to Commercial</button>

        {/* Header */}
        <div className="flex items-start justify-between gap-4 flex-wrap mb-6">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-extrabold text-navy">{c.name}</h1>
              <Badge variant={STATUS_VARIANT[c.status] ?? 'default'}>{statusLabel(c.status)}</Badge>
              <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-amber-100 text-amber-700">{c.accountTier}</span>
              {c.dormantSince && <Badge variant="red">Dormant</Badge>}
            </div>
            <p className="text-sm text-gray-500 mt-1">{c.industry ?? '—'}{c.geography ? ` · ${c.geography}` : ''}{c.businessLine ? ` · ${c.businessLine}` : ''}</p>
          </div>
          <div className="flex gap-2 flex-wrap">
            {d.stage && d.canActOnStage && (
              <Btn onClick={doApprove} disabled={d.busy}>{d.stage.label}</Btn>
            )}
            {d.stage && d.canReject && (
              <Btn variant="danger" onClick={() => { setReason(''); setModal('reject') }} disabled={d.busy}>Reject</Btn>
            )}
            {isActive && d.canWrite && (
              <Btn variant="ghost" onClick={() => d.deactivate().then(after)} disabled={d.busy}>Deactivate</Btn>
            )}
          </div>
        </div>

        {isRejected && c.rejectionReason && (
          <div className="bg-red-50 border border-red-200 rounded-xl px-5 py-3 mb-6">
            <p className="text-sm font-semibold text-red-700">Rejected</p>
            <p className="text-sm text-red-600 mt-0.5">{c.rejectionReason}</p>
          </div>
        )}

        {/* Onboarding stepper */}
        {!isRejected && (
          <div className="bg-white border border-gray-200 rounded-xl p-5 mb-6">
            <div className="flex items-center justify-between">
              {STEPS.map((s, i) => {
                const done = stepIdx > i || (isActive && i <= stepIdx)
                const current = stepIdx === i
                return (
                  <div key={s.key} className="flex-1 flex flex-col items-center relative">
                    {i > 0 && <div className={`absolute top-3 right-1/2 w-full h-0.5 ${done || current ? 'bg-navy' : 'bg-gray-200'}`} />}
                    <div className={`relative z-10 w-6 h-6 rounded-full flex items-center justify-center text-[11px] font-bold ${done ? 'bg-navy text-white' : current ? 'bg-gold text-white' : 'bg-gray-200 text-gray-500'}`}>{done ? '✓' : i + 1}</div>
                    <span className={`text-[11px] mt-1.5 text-center ${current ? 'font-bold text-navy' : 'text-gray-500'}`}>{s.label}</span>
                  </div>
                )
              })}
            </div>
          </div>
        )}

        <div className="grid md:grid-cols-2 gap-6">
          {/* Details */}
          <section className="bg-white border border-gray-200 rounded-xl p-5">
            <h2 className="text-sm font-bold text-gray-700 mb-3">Details</h2>
            <dl className="text-sm space-y-2">
              <Row k="Type" v={c.customerType} />
              <Row k="Segment" v={c.segment ?? '—'} />
              <Row k="Email" v={c.email ?? '—'} />
              <Row k="Phone" v={c.phone ?? '—'} />
              <Row k="Reference" v={c.clientReference ?? '—'} />
              <Row k="Account owner" v={c.accountOwnerName ?? c.accountOwnerId} />
              <Row k="Introduced by" v={<>{c.introducedBy}{c.introducedByLocked && <span className="ml-1 text-[11px] text-gray-400">🔒 locked</span>}</>} />
              <Row k="Credit limit" v={fmtKes(c.creditLimit)} />
              <Row k="Credit terms" v={`${c.creditTermsDays} days`} />
            </dl>
          </section>

          {/* Approval trail */}
          <section className="bg-white border border-gray-200 rounded-xl p-5">
            <h2 className="text-sm font-bold text-gray-700 mb-3">Onboarding Trail</h2>
            <dl className="text-sm space-y-2">
              <Row k="Submitted" v={`${c.submittedBy} · ${fmtDate(c.submittedAt)}`} />
              <Row k="Line Manager" v={c.lineManagerApprovedBy ? `${c.lineManagerApprovedBy} · ${fmtDate(c.lineManagerApprovedAt)}` : '—'} />
              <Row k="Head of BD" v={c.headBdApprovedBy ? `${c.headBdApprovedBy} · ${fmtDate(c.headBdApprovedAt)}` : '—'} />
              <Row k="CFO" v={c.cfoApprovedBy ? `${c.cfoApprovedBy} · ${fmtDate(c.cfoApprovedAt)}` : '—'} />
              <Row k="MD" v={c.mdApprovedBy ? `${c.mdApprovedBy} · ${fmtDate(c.mdApprovedAt)}` : '—'} />
              {c.activatedAt && <Row k="Activated" v={fmtDate(c.activatedAt)} />}
            </dl>
          </section>
        </div>

        {/* Contacts */}
        <section className="bg-white border border-gray-200 rounded-xl p-5 mt-6">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-sm font-bold text-gray-700">Contacts</h2>
            {d.canWrite && <button onClick={() => { setContact({ firstName: '', lastName: '', jobTitle: '', email: '', phone: '', isPrimary: (c.contacts?.length ?? 0) === 0 }); setModal('contact') }} className="text-sm font-semibold text-navy hover:underline">+ Add contact</button>}
          </div>
          {(c.contacts?.length ?? 0) === 0 ? <p className="text-sm text-gray-400">No contacts yet.</p> : (
            <div className="divide-y divide-gray-100">
              {c.contacts.map(ct => (
                <div key={ct.id} className="py-2.5 flex items-center justify-between">
                  <div>
                    <p className="text-sm font-semibold text-navy">{ct.firstName} {ct.lastName}{ct.isPrimary && <span className="ml-2 text-[10px] font-bold uppercase text-gold">Primary</span>}</p>
                    <p className="text-xs text-gray-500">{ct.jobTitle ?? '—'}{ct.email ? ` · ${ct.email}` : ''}{ct.phone ? ` · ${ct.phone}` : ''}</p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* C8 — account ownership transfer */}
        <CustomerTransferPanel customer={c} onOwnerChanged={d.reload} />

        {/* C7 — interactions & follow-up tasks */}
        <CustomerActivityPanel customerId={c.id} />
      </main>

      {/* CFO credit modal */}
      {modal === 'cfo' && (
        <Modal title="CFO Credit Review" onClose={() => setModal(null)} width={440}>
          <div className="space-y-3">
            <label className="block"><span className="block text-xs font-medium text-gray-500 mb-1">Credit limit (KES) *</span>
              <input type="number" value={cfo.creditLimit} onChange={e => setCfo(s => ({ ...s, creditLimit: e.target.value }))} className="input" /></label>
            <label className="block"><span className="block text-xs font-medium text-gray-500 mb-1">Credit terms (days) *</span>
              <input type="number" value={cfo.creditTermsDays} onChange={e => setCfo(s => ({ ...s, creditTermsDays: e.target.value }))} className="input" /></label>
            <label className="block"><span className="block text-xs font-medium text-gray-500 mb-1">Notes</span>
              <textarea rows={2} value={cfo.notes} onChange={e => setCfo(s => ({ ...s, notes: e.target.value }))} className="input" /></label>
            <div className="flex justify-end gap-2 pt-1">
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={() => d.approveStage({ creditLimit: Number(cfo.creditLimit) || 0, creditTermsDays: Number(cfo.creditTermsDays) || 0, notes: cfo.notes || undefined }).then(after)} disabled={d.busy}>Set Credit &amp; Advance</Btn>
            </div>
          </div>
        </Modal>
      )}

      {/* Reject modal */}
      {modal === 'reject' && (
        <Modal title="Reject Client" onClose={() => setModal(null)} width={440}>
          <label className="block"><span className="block text-xs font-medium text-gray-500 mb-1">Reason *</span>
            <textarea rows={3} value={reason} onChange={e => setReason(e.target.value)} className="input" placeholder="Why is this client being rejected?" /></label>
          <div className="flex justify-end gap-2 pt-2">
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn variant="danger" onClick={() => reason.trim() && d.reject(reason).then(after)} disabled={d.busy || !reason.trim()}>Confirm Reject</Btn>
          </div>
        </Modal>
      )}

      {/* Add contact modal */}
      {modal === 'contact' && (
        <Modal title="Add Contact" onClose={() => setModal(null)} width={440}>
          <div className="grid grid-cols-2 gap-3">
            <label className="block"><span className="block text-xs font-medium text-gray-500 mb-1">First name *</span><input value={contact.firstName} onChange={e => setContact(s => ({ ...s, firstName: e.target.value }))} className="input" /></label>
            <label className="block"><span className="block text-xs font-medium text-gray-500 mb-1">Last name *</span><input value={contact.lastName} onChange={e => setContact(s => ({ ...s, lastName: e.target.value }))} className="input" /></label>
            <label className="block col-span-2"><span className="block text-xs font-medium text-gray-500 mb-1">Job title</span><input value={contact.jobTitle} onChange={e => setContact(s => ({ ...s, jobTitle: e.target.value }))} className="input" /></label>
            <label className="block"><span className="block text-xs font-medium text-gray-500 mb-1">Email</span><input type="email" value={contact.email} onChange={e => setContact(s => ({ ...s, email: e.target.value }))} className="input" /></label>
            <label className="block"><span className="block text-xs font-medium text-gray-500 mb-1">Phone</span><input value={contact.phone} onChange={e => setContact(s => ({ ...s, phone: e.target.value }))} className="input" /></label>
            <label className="col-span-2 flex items-center gap-2 text-sm text-gray-600"><input type="checkbox" checked={contact.isPrimary} onChange={e => setContact(s => ({ ...s, isPrimary: e.target.checked }))} /> Primary contact</label>
          </div>
          <div className="flex justify-end gap-2 pt-3">
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={() => contact.firstName.trim() && contact.lastName.trim() && d.addContact(contact).then(after)} disabled={d.busy || !contact.firstName.trim() || !contact.lastName.trim()}>Add</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}

function Row({ k, v }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-gray-500">{k}</dt>
      <dd className="text-navy font-medium text-right">{v}</dd>
    </div>
  )
}
