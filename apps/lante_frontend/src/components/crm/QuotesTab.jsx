import { useState, useEffect } from 'react'
import { DataTable, Badge, Btn, Modal } from '../ui.jsx'
import { T } from '../../theme/tokens.js'
import * as crm from '../../services/crm.js'
import { useQuotations, QUOTE_STATUSES, QUOTE_STATUS_VARIANT, qLabel } from '../../hooks/crm/useQuotations.js'

const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'
const emptyLine = () => ({ description: '', quantity: 1, unitPrice: '', discountPercent: 0, priceExceptionReason: '' })

export default function QuotesTab() {
  const Q = useQuotations()
  const [toast, setToast] = useState('')
  const [modal, setModal] = useState(null)   // 'new' | 'detail'
  const [detail, setDetail] = useState(null)
  const [opps, setOpps] = useState([])
  const [form, setForm] = useState({ opportunityId: '', title: '' })
  const [lines, setLines] = useState([emptyLine()])
  const [lineErr, setLineErr] = useState('')
  const [busy, setBusy] = useState(false)

  const flash = (m) => { setToast(m); setTimeout(() => setToast(''), 4000) }
  const isDraft = detail?.status === 'Draft'

  const openDetail = async (id) => {
    try {
      const q = await crm.getQuotation(id); setDetail(q)
      setLines((q.lines?.length ? q.lines.map(l => ({ ...l, priceExceptionReason: '' })) : [emptyLine()]))
      setLineErr(''); setModal('detail')
    } catch { flash('Failed to load quotation.') }
  }
  const refresh = async () => { if (detail) { const q = await crm.getQuotation(detail.id); setDetail(q) } Q.reload() }
  const run = async (fn, ok) => {
    setBusy(true)
    try { await fn(); await refresh(); flash(ok) }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  const openNew = async () => {
    setForm({ opportunityId: '', title: '' })
    try { const r = await crm.listOpportunities({ status: 'Open', pageSize: 100 }); setOpps(r.data ?? []) } catch { setOpps([]) }
    setModal('new')
  }
  const createQuote = async () => {
    if (!form.opportunityId) return flash('Select an opportunity.')
    setBusy(true)
    try { const q = await crm.createQuotation(form); setModal(null); await openDetail(q.id); Q.reload() }
    catch (e) { flash(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }

  const saveLines = async () => {
    setBusy(true); setLineErr('')
    try {
      const payload = { lines: lines.filter(l => l.description.trim()).map(l => ({
        productId: l.productId || undefined, description: l.description,
        quantity: Number(l.quantity) || 0, unitPrice: Number(l.unitPrice) || 0,
        discountPercent: Number(l.discountPercent) || 0,
        priceExceptionReason: l.priceExceptionReason || undefined,
      })) }
      const q = await crm.saveQuotationLines(detail.id, payload)
      setDetail(q); Q.reload(); flash('Lines saved.')
    } catch (e) {
      setLineErr(e.response?.data?.message ?? 'Failed to save lines.')
    } finally { setBusy(false) }
  }

  const setLine = (i, patch) => setLines(ls => ls.map((l, idx) => idx === i ? { ...l, ...patch } : l))

  return (
    <div>
      {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg max-w-md">{toast}</div>}

      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <h2 className="text-lg font-bold text-navy">Quotations</h2>
          <select value={Q.statusF} onChange={e => { Q.setStatusF(e.target.value); Q.setPage(1) }} className="input" style={{ width: 'auto', marginBottom: 0 }}>
            <option value="">All statuses</option>{QUOTE_STATUSES.map(s => <option key={s} value={s}>{qLabel(s)}</option>)}
          </select>
        </div>
        {Q.canWrite && <Btn onClick={openNew}>+ New Quotation</Btn>}
      </div>

      {Q.error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{Q.error}</div>}

      {Q.loading ? (
        <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-14 bg-white rounded-xl border border-gray-100 animate-pulse" />)}</div>
      ) : (
        <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, overflow: 'hidden' }}>
          <DataTable
            headers={['Quote #', 'Title', 'Customer', 'Total', 'Status', 'Valid Until']}
            empty="No quotations yet."
            onRowClick={(_, i) => Q.quotes[i] && openDetail(Q.quotes[i].id)}
            rows={Q.quotes.map(q => [
              <span style={{ fontWeight: 600, color: T.navy }}>{q.quoteNumber}{q.version > 1 ? ` v${q.version}` : ''}</span>,
              q.title,
              q.customerName ?? '—',
              <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(q.totalAmount)}</span>,
              <Badge variant={QUOTE_STATUS_VARIANT[q.status] ?? 'default'}>{qLabel(q.status)}</Badge>,
              fmtDate(q.validUntil),
            ])}
          />
        </div>
      )}

      {Q.totalPages > 1 && (
        <div className="flex items-center justify-between mt-5">
          <p className="text-sm text-gray-500">Page {Q.page} of {Q.totalPages}</p>
          <div className="flex gap-2">
            <button disabled={Q.page === 1} onClick={() => Q.setPage(p => p - 1)} className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50">Previous</button>
            <button disabled={Q.page === Q.totalPages} onClick={() => Q.setPage(p => p + 1)} className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50">Next</button>
          </div>
        </div>
      )}

      {/* New quotation */}
      {modal === 'new' && (
        <Modal title="New Quotation" onClose={() => setModal(null)} width={480}>
          <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">Opportunity *</span>
            <select value={form.opportunityId} onChange={e => setForm(f => ({ ...f, opportunityId: e.target.value }))} className="input">
              <option value="">Select an open opportunity…</option>
              {opps.map(o => <option key={o.id} value={o.id}>{o.opportunityNumber} · {o.name}{o.customerName ? ` (${o.customerName})` : ''}</option>)}
            </select>
          </label>
          <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">Title</span>
            <input value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))} className="input" placeholder="Defaults to the opportunity name" /></label>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn><Btn onClick={createQuote} disabled={busy}>Create Draft</Btn></div>
        </Modal>
      )}

      {/* Detail */}
      {modal === 'detail' && detail && (
        <Modal title={`${detail.quoteNumber}${detail.version > 1 ? ` v${detail.version}` : ''} · ${detail.title}`} onClose={() => { setModal(null); setDetail(null) }} width={760}>
          <div className="flex items-center gap-2 mb-4 flex-wrap">
            <Badge variant={QUOTE_STATUS_VARIANT[detail.status] ?? 'default'}>{qLabel(detail.status)}</Badge>
            <span className="text-xs text-gray-500">{detail.customerName ?? '—'} · valid to {fmtDate(detail.validUntil)}</span>
            {detail.requiresMdApproval && <span className="text-xs font-semibold text-amber-600">MD approval (&gt;500k)</span>}
          </div>

          {/* Line editor */}
          <div className="border border-gray-200 rounded-xl overflow-hidden mb-3">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-gray-500 text-xs">
                <tr><th className="text-left px-3 py-2">Description</th><th className="px-2 w-16">Qty</th><th className="px-2 w-28">Unit price</th><th className="px-2 w-16">Disc %</th><th className="px-3 text-right w-28">Total</th>{isDraft && <th className="w-8"></th>}</tr>
              </thead>
              <tbody>
                {lines.map((l, i) => {
                  const total = (Number(l.quantity) || 0) * (Number(l.unitPrice) || 0) * (1 - (Number(l.discountPercent) || 0) / 100)
                  return (
                    <tr key={i} className="border-t border-gray-100">
                      <td className="px-3 py-1.5">{isDraft ? <input value={l.description} onChange={e => setLine(i, { description: e.target.value })} className="input" style={{ marginBottom: 0 }} /> : l.description}</td>
                      <td className="px-2">{isDraft ? <input type="number" value={l.quantity} onChange={e => setLine(i, { quantity: e.target.value })} className="input" style={{ marginBottom: 0 }} /> : l.quantity}</td>
                      <td className="px-2">{isDraft ? <input type="number" value={l.unitPrice} onChange={e => setLine(i, { unitPrice: e.target.value })} className="input" style={{ marginBottom: 0 }} /> : fmtKes(l.unitPrice)}</td>
                      <td className="px-2">{isDraft ? <input type="number" value={l.discountPercent} onChange={e => setLine(i, { discountPercent: e.target.value })} className="input" style={{ marginBottom: 0 }} /> : `${l.discountPercent}%`}</td>
                      <td className="px-3 text-right whitespace-nowrap">{fmtKes(l.lineTotal ?? total)}</td>
                      {isDraft && <td className="px-1">{lines.length > 1 && <button onClick={() => setLines(ls => ls.filter((_, x) => x !== i))} className="text-red-500 text-xs">✕</button>}</td>}
                    </tr>
                  )
                })}
              </tbody>
            </table>
            {isDraft && (
              <div className="px-3 py-2 border-t border-gray-100"><button onClick={() => setLines(ls => [...ls, emptyLine()])} className="text-sm font-semibold text-navy hover:underline">+ Add line</button></div>
            )}
          </div>

          {/* Price-exception reasons (shown when a save was blocked) */}
          {lineErr && (
            <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 text-sm mb-3">
              <p className="text-amber-800">{lineErr}</p>
              {isDraft && (
                <div className="mt-2 space-y-2">
                  {lines.map((l, i) => (
                    <input key={i} value={l.priceExceptionReason} onChange={e => setLine(i, { priceExceptionReason: e.target.value })}
                      className="input" style={{ marginBottom: 0 }} placeholder={`Reason for "${l.description || `line ${i + 1}`}" price change (if flagged)`} />
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Totals */}
          <div className="flex justify-end mb-4">
            <div className="text-sm w-56 space-y-1">
              <div className="flex justify-between"><span className="text-gray-500">Subtotal</span><span>{fmtKes(detail.subtotal)}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">VAT ({Math.round(detail.vatRate * 100)}%)</span><span>{fmtKes(detail.vatAmount)}</span></div>
              <div className="flex justify-between font-bold text-navy border-t border-gray-100 pt-1"><span>Total</span><span>{fmtKes(detail.totalAmount)}</span></div>
            </div>
          </div>

          {/* Workflow actions */}
          {Q.canWrite && (
            <div className="flex gap-2 flex-wrap pt-3 border-t border-gray-100">
              {isDraft && <Btn size="sm" onClick={saveLines} disabled={busy}>Save Lines</Btn>}
              {isDraft && <Btn size="sm" variant="green" onClick={() => run(() => crm.submitQuotation(detail.id), 'Submitted.')} disabled={busy}>Submit for Approval</Btn>}
              {detail.status === 'PendingDeptHead' && Q.canApproveBd && <>
                <Btn size="sm" variant="green" onClick={() => run(() => crm.deptHeadReviewQuotation(detail.id, { approve: true }), 'Approved.')} disabled={busy}>Dept Head Approve</Btn>
                <Btn size="sm" variant="danger" onClick={() => { const r = prompt('Rejection reason:'); if (r) run(() => crm.deptHeadReviewQuotation(detail.id, { approve: false, reason: r }), 'Returned.') }} disabled={busy}>Reject</Btn>
              </>}
              {detail.status === 'PendingMd' && Q.canApproveMd && <Btn size="sm" variant="green" onClick={() => run(() => crm.mdApproveQuotation(detail.id), 'MD approved.')} disabled={busy}>MD Approve</Btn>}
              {detail.status === 'Approved' && <Btn size="sm" onClick={() => run(() => crm.sendQuotation(detail.id), 'Sent.')} disabled={busy}>Send to Client</Btn>}
              {detail.status === 'Sent' && <>
                <Btn size="sm" variant="green" onClick={() => run(() => crm.quotationOutcome(detail.id, { accepted: true }), 'Accepted.')} disabled={busy}>Mark Accepted</Btn>
                <Btn size="sm" variant="danger" onClick={() => { const r = prompt('Rejection reason (optional):'); run(() => crm.quotationOutcome(detail.id, { accepted: false, reason: r || undefined }), 'Marked rejected.') }} disabled={busy}>Mark Rejected</Btn>
              </>}
              {['Sent', 'Rejected', 'Expired', 'Approved', 'PendingDeptHead', 'PendingMd'].includes(detail.status) &&
                <Btn size="sm" variant="outline" onClick={() => run(() => crm.reviseQuotation(detail.id), 'New version created.').then(() => setModal(null))} disabled={busy}>Revise (new version)</Btn>}
            </div>
          )}

          {detail.rejectionReason && <p className="text-xs text-red-600 mt-3">Reason: {detail.rejectionReason}</p>}
        </Modal>
      )}
    </div>
  )
}
