import { useState } from 'react'
import { DataTable, Badge, Btn, Modal, Kpi, KPI_GRID } from '../../components/ui.jsx'
import { T } from '../../theme/tokens.js'
import * as crm from '../../services/crm.js'
import { useTenders, TENDER_STATUSES, TENDER_STATUS_VARIANT, tLabel } from '../../hooks/crm/useTenders.js'

const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'
const EMPTY = { title: '', clientName: '', source: '', estimatedValue: '', submissionDeadline: '', description: '' }

export default function TendersPage() {
  const Tn = useTenders()
  const [toast, setToast] = useState('')
  const [modal, setModal] = useState(null)   // 'new' | 'detail' | 'bond'
  const [form, setForm] = useState(EMPTY)
  const [detail, setDetail] = useState(null)
  const [bond, setBond] = useState({ guaranteeNumber: '', issuingBank: '', validityDate: '', amount: '' })
  const [busy, setBusy] = useState(false)

  const flash = (m) => { setToast(m); setTimeout(() => setToast(''), 3800) }
  const closed = detail && ['Won', 'Lost', 'Cancelled'].includes(detail.status)

  const openDetail = async (id) => { try { setDetail(await crm.getTender(id)); setModal('detail') } catch { flash('Failed to load.') } }
  const refresh = async () => { if (detail) setDetail(await crm.getTender(detail.id)); Tn.reload() }
  const run = async (fn, ok) => {
    setBusy(true)
    try { await fn(); await refresh(); flash(ok) }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }
  const createTender = async () => {
    if (!form.title.trim() || !form.clientName.trim() || !form.submissionDeadline) return flash('Title, client and deadline required.')
    setBusy(true)
    try { await crm.createTender({ ...form, estimatedValue: Number(form.estimatedValue) || 0, submissionDeadline: form.submissionDeadline }); setModal(null); setForm(EMPTY); Tn.reload(); flash('Tender registered.') }
    catch (e) { flash(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  const saveBond = async () => {
    if (!bond.guaranteeNumber.trim() || !bond.validityDate) return flash('Guarantee number and validity required.')
    await run(() => crm.saveBidBond(detail.id, { ...bond, amount: Number(bond.amount) || 0 }), 'Bid bond saved.')
    setModal('detail')
  }
  const deadlineColor = (days) => days <= 3 ? T.red : days <= 7 ? T.amber : T.dgrey

  return (
    <>
      <main className="flex-1 w-full px-4 sm:px-6 lg:px-8 py-8">
        {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}

        <div className="mb-5"><h1 className="text-2xl font-extrabold text-navy">Bids &amp; Pre-Sales</h1><p className="text-sm text-gray-500 mt-0.5">Tender register, deadline alerts &amp; bid bonds</p></div>

        <div style={{ ...KPI_GRID, marginBottom: 20 }}>
          <Kpi label="Open Tenders" value={Tn.loading ? '…' : Tn.kpi.openCount} icon="📋" />
          <Kpi label="Due ≤7 days" value={Tn.loading ? '…' : Tn.kpi.dueSoonCount} icon="⏰" variant={Tn.kpi.dueSoonCount > 0 ? 'amber' : 'green'} />
          <Kpi label="Open Value" value={Tn.loading ? '…' : fmtKes(Tn.kpi.openValue)} icon="💰" variant="blue" />
        </div>

        <div className="flex items-center justify-between gap-3 flex-wrap mb-4">
          <div className="flex items-center gap-3">
            <input value={Tn.search} onChange={e => Tn.setSearch(e.target.value)} placeholder="Search…" className="input" style={{ width: 220, marginBottom: 0 }} />
            <select value={Tn.statusF} onChange={e => { Tn.setStatusF(e.target.value); Tn.setPage(1) }} className="input" style={{ width: 'auto', marginBottom: 0 }}>
              <option value="">All statuses</option>{TENDER_STATUSES.map(s => <option key={s} value={s}>{tLabel(s)}</option>)}
            </select>
          </div>
          {Tn.canWrite && <Btn onClick={() => { setForm(EMPTY); setModal('new') }}>+ New Tender</Btn>}
        </div>

        {Tn.error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{Tn.error}</div>}

        {Tn.loading ? (
          <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-14 bg-white rounded-xl border border-gray-100 animate-pulse" />)}</div>
        ) : (
          <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, overflow: 'hidden' }}>
            <DataTable
              headers={['Tender #', 'Title', 'Client', 'Value', 'Deadline', 'Status']}
              empty="No tenders yet."
              onRowClick={(_, i) => Tn.tenders[i] && openDetail(Tn.tenders[i].id)}
              rows={Tn.tenders.map(t => [
                <span style={{ fontWeight: 600, color: T.navy }}>{t.tenderNumber}</span>,
                t.title,
                t.clientName,
                <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(t.estimatedValue)}</span>,
                <span style={{ whiteSpace: 'nowrap', color: ['Registered','Submitted'].includes(t.status) ? deadlineColor(t.daysToDeadline) : T.dgrey, fontWeight: ['Registered','Submitted'].includes(t.status) && t.daysToDeadline <= 7 ? 700 : 400 }}>
                  {fmtDate(t.submissionDeadline)}{['Registered','Submitted'].includes(t.status) ? ` (${t.daysToDeadline}d)` : ''}
                </span>,
                <Badge variant={TENDER_STATUS_VARIANT[t.status] ?? 'default'}>{tLabel(t.status)}</Badge>,
              ])}
            />
          </div>
        )}

        {Tn.totalPages > 1 && (
          <div className="flex items-center justify-between mt-5">
            <p className="text-sm text-gray-500">Page {Tn.page} of {Tn.totalPages}</p>
            <div className="flex gap-2">
              <button disabled={Tn.page === 1} onClick={() => Tn.setPage(p => p - 1)} className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50">Previous</button>
              <button disabled={Tn.page === Tn.totalPages} onClick={() => Tn.setPage(p => p + 1)} className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50">Next</button>
            </div>
          </div>
        )}

        {/* New tender */}
        {modal === 'new' && (
          <Modal title="New Tender" onClose={() => setModal(null)} width={520}>
            <div className="grid grid-cols-2 gap-3">
              <Fld label="Title *"><input value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))} className="input" /></Fld>
              <Fld label="Client *"><input value={form.clientName} onChange={e => setForm(f => ({ ...f, clientName: e.target.value }))} className="input" /></Fld>
              <Fld label="Source"><input value={form.source} onChange={e => setForm(f => ({ ...f, source: e.target.value }))} className="input" placeholder="e.g. Tender notice" /></Fld>
              <Fld label="Estimated value (KES)"><input type="number" value={form.estimatedValue} onChange={e => setForm(f => ({ ...f, estimatedValue: e.target.value }))} className="input" /></Fld>
              <Fld label="Submission deadline *"><input type="date" value={form.submissionDeadline} onChange={e => setForm(f => ({ ...f, submissionDeadline: e.target.value }))} className="input" /></Fld>
            </div>
            <Fld label="Description"><textarea rows={2} value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} className="input" /></Fld>
            <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn><Btn onClick={createTender} disabled={busy}>Register</Btn></div>
          </Modal>
        )}

        {/* Detail */}
        {modal === 'detail' && detail && (
          <Modal title={`${detail.tenderNumber} · ${detail.title}`} onClose={() => { setModal(null); setDetail(null) }} width={580}>
            <div className="flex items-center gap-2 mb-4 flex-wrap">
              <Badge variant={TENDER_STATUS_VARIANT[detail.status] ?? 'default'}>{tLabel(detail.status)}</Badge>
              <span className="text-xs text-gray-500">{detail.clientName} · {fmtKes(detail.estimatedValue)} · due {fmtDate(detail.submissionDeadline)}{['Registered','Submitted'].includes(detail.status) ? ` (${detail.daysToDeadline}d)` : ''}</span>
            </div>
            {detail.description && <p className="text-sm text-gray-600 mb-3">{detail.description}</p>}
            {detail.lostReason && <p className="text-sm text-red-600 mb-3">Lost: {detail.lostReason}</p>}
            {detail.linkedOpportunityId && <p className="text-sm text-green-700 mb-3">✓ Opportunity created from this tender.</p>}

            {/* Bid bond */}
            <div className="border border-gray-100 rounded-lg p-3 mb-4">
              <div className="flex items-center justify-between">
                <h4 className="text-sm font-bold text-gray-700">Bid Bond</h4>
                {Tn.canWrite && !closed && <button onClick={() => { setBond({ guaranteeNumber: detail.bidBond?.guaranteeNumber ?? '', issuingBank: detail.bidBond?.issuingBank ?? '', validityDate: '', amount: detail.bidBond?.amount ?? '' }); setModal('bond') }} className="text-xs font-semibold text-navy hover:underline">{detail.bidBond ? 'Update' : 'Register'}</button>}
              </div>
              {detail.bidBond ? <p className="text-xs text-gray-600 mt-1">{detail.bidBond.guaranteeNumber} · {detail.bidBond.issuingBank} · {fmtKes(detail.bidBond.amount)} · valid to {fmtDate(detail.bidBond.validityDate)}</p> : <p className="text-xs text-gray-400 mt-1">No bid bond.</p>}
            </div>

            {Tn.canWrite && !closed && (
              <div className="flex gap-2 flex-wrap pt-2 border-t border-gray-100">
                {detail.status === 'Registered' && <Btn size="sm" onClick={() => run(() => crm.submitTender(detail.id), 'Submitted.')} disabled={busy}>Mark Submitted</Btn>}
                <Btn size="sm" variant="green" onClick={() => run(() => crm.winTender(detail.id, { notes: 'Awarded' }), 'Won → opportunity created.')} disabled={busy}>Won</Btn>
                <Btn size="sm" variant="danger" onClick={() => { const r = prompt('Lost reason:'); if (r) run(() => crm.loseTender(detail.id, { reason: r }), 'Marked lost.') }} disabled={busy}>Lost</Btn>
                <Btn size="sm" variant="outline" onClick={() => run(() => crm.noBidTender(detail.id, {}), 'Marked no-bid.')} disabled={busy}>No Bid</Btn>
              </div>
            )}
          </Modal>
        )}

        {/* Bid bond modal */}
        {modal === 'bond' && detail && (
          <Modal title="Bid Bond" onClose={() => setModal('detail')} width={420}>
            <Fld label="Guarantee number *"><input value={bond.guaranteeNumber} onChange={e => setBond(b => ({ ...b, guaranteeNumber: e.target.value }))} className="input" /></Fld>
            <Fld label="Issuing bank"><input value={bond.issuingBank} onChange={e => setBond(b => ({ ...b, issuingBank: e.target.value }))} className="input" /></Fld>
            <div className="grid grid-cols-2 gap-3">
              <Fld label="Validity date *"><input type="date" value={bond.validityDate} onChange={e => setBond(b => ({ ...b, validityDate: e.target.value }))} className="input" /></Fld>
              <Fld label="Amount (KES)"><input type="number" value={bond.amount} onChange={e => setBond(b => ({ ...b, amount: e.target.value }))} className="input" /></Fld>
            </div>
            <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal('detail')}>Cancel</Btn><Btn onClick={saveBond} disabled={busy}>Save</Btn></div>
          </Modal>
        )}
      </main>
    </>
  )
}

function Fld({ label, children }) {
  return <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">{label}</span>{children}</label>
}
