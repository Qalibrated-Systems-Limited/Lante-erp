import { useState } from 'react'
import { DataTable, Badge, Btn, Modal, Kpi, KPI_GRID } from '../ui.jsx'
import { T } from '../../theme/tokens.js'
import * as crm from '../../services/crm.js'
import { useLeads, LEAD_SOURCES, LEAD_STATUSES, LEAD_RATINGS, LEAD_STATUS_VARIANT, RATING_VARIANT, label } from '../../hooks/crm/useLeads.js'

const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'
// Mirrors CrmFieldRules.ProductRanges on the server, which rejects anything outside this list.
const PRODUCT_RANGES = [
  'Calibration Services',
  'Inspection Services',
  'Equipment Repair & Maintenance',
  'Fleet / Asset Management',
  'Training',
  'Other',
]

const EMPTY_LEAD = { source: 'Web', sourceName: '', firstName: '', lastName: '', companyName: '', email: '', phone: '', industry: '', estimatedValue: '', rating: 'Warm', productRange: [] }

export default function LeadsTab() {
  const L = useLeads()
  const [toast, setToast] = useState('')
  const [modal, setModal] = useState(null)          // 'new' | 'detail' | 'activity' | 'qualify' | 'unqualify'
  const [createError, setCreateError] = useState('')
  const [existingCustomer, setExistingCustomer] = useState(null)  // set when capture is blocked by an existing client
  const [form, setForm] = useState(EMPTY_LEAD)
  const [detail, setDetail] = useState(null)
  const [act, setAct] = useState({ activityType: 'Call', subject: '', description: '' })
  const [qual, setQual] = useState({ rating: 'Hot', notes: '' })
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)

  const flash = (m) => { setToast(m); setTimeout(() => setToast(''), 3500) }
  const converted = detail && (detail.status === 'Converted' || detail.isConverted)
  const closed = detail && (converted || detail.status === 'Unqualified')

  const openDetail = async (id) => {
    try { setDetail(await crm.getLead(id)); setModal('detail') }
    catch { flash('Failed to load lead.') }
  }
  const refreshDetail = async () => { if (detail) setDetail(await crm.getLead(detail.id)); L.reload() }

  const run = async (fn, ok) => {
    setBusy(true)
    try { await fn(); await refreshDetail(); flash(ok) }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  const createLead = async () => {
    if (!form.firstName.trim() && !form.companyName.trim()) return flash('Contact name or company required.')
    setBusy(true); setCreateError(''); setExistingCustomer(null)
    try {
      await crm.createLead({ ...form, estimatedValue: Number(form.estimatedValue) || 0 })
      setModal(null); setForm(EMPTY_LEAD); L.reload(); flash('Lead captured.')
    } catch (e) {
      // A block explains which lead or client already exists — too much to lose in a toast, so it
      // stays in the dialog until the user acts on it. A 409 also names the customer, which lets
      // us offer the correct action rather than just refusing.
      setCreateError(e.response?.data?.message ?? 'Failed to create lead.')
      setExistingCustomer(e.response?.data?.existingCustomer ?? null)
    }
    finally { setBusy(false) }
  }

  // The captured details are still good — carry them onto the opportunity rather than making the
  // user retype them against the client's account.
  const raiseOpportunity = async () => {
    setBusy(true)
    try {
      const who = form.companyName.trim() || `${form.firstName} ${form.lastName}`.trim()
      await crm.createOpportunity({
        name: who ? `${who} enquiry` : `${existingCustomer.name} enquiry`,
        customerId: existingCustomer.id,
        customerName: existingCustomer.name,
        estimatedValue: Number(form.estimatedValue) || 0,
        source: form.source || undefined,
      })
      setModal(null); setForm(EMPTY_LEAD); setCreateError(''); setExistingCustomer(null)
      flash(`Opportunity raised for ${existingCustomer.name}.`)
    } catch (e) { setCreateError(e.response?.data?.message ?? 'Failed to raise the opportunity.') }
    finally { setBusy(false) }
  }

  const toggleRange = (value) => setForm(f => ({
    ...f,
    productRange: f.productRange.includes(value)
      ? f.productRange.filter(v => v !== value)
      : [...f.productRange, value],
  }))

  return (
    <div>
      {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}

      {/* KPIs */}
      <div style={{ ...KPI_GRID, marginBottom: 20 }}>
        <Kpi label="Open Leads" value={L.loading ? '…' : L.openCount} sub="Not converted / unqualified" icon="🎯" />
        <Kpi label="Pipeline Value" value={L.loading ? '…' : fmtKes(L.openValue)} sub="Est. value of open leads" icon="💰" variant="blue" />
        <Kpi label="Total Leads" value={L.loading ? '…' : L.total} sub="All statuses" icon="📇" />
      </div>

      {/* Toolbar */}
      <div className="flex items-center justify-between gap-3 flex-wrap mb-4">
        <h2 className="text-lg font-bold text-navy">Lead Register</h2>
        {L.canWrite && <Btn onClick={() => { setForm(EMPTY_LEAD); setCreateError(''); setExistingCustomer(null); setModal('new') }}>+ New Lead</Btn>}
      </div>

      {/* Filters */}
      <div className="bg-white rounded-xl border border-gray-200 p-4 mb-4 flex flex-wrap gap-3 items-end">
        <div className="flex-1 min-w-[180px]">
          <label className="block text-xs font-medium text-gray-500 mb-1">Search</label>
          <input value={L.search} onChange={e => L.setSearch(e.target.value)} placeholder="Name, company or email…" className="input" />
        </div>
        <div className="w-40">
          <label className="block text-xs font-medium text-gray-500 mb-1">Status</label>
          <select value={L.statusF} onChange={e => { L.setStatusF(e.target.value); L.setPage(1) }} className="input">
            <option value="">All</option>{LEAD_STATUSES.map(s => <option key={s} value={s}>{label(s)}</option>)}
          </select>
        </div>
        <div className="w-40">
          <label className="block text-xs font-medium text-gray-500 mb-1">Source</label>
          <select value={L.sourceF} onChange={e => { L.setSourceF(e.target.value); L.setPage(1) }} className="input">
            <option value="">All</option>{LEAD_SOURCES.map(s => <option key={s} value={s}>{label(s)}</option>)}
          </select>
        </div>
        <label className="flex items-center gap-2 text-sm text-gray-600 pb-2"><input type="checkbox" checked={L.staleOnly} onChange={e => { L.setStaleOnly(e.target.checked); L.setPage(1) }} /> Stale only</label>
      </div>

      {L.error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{L.error}</div>}

      {L.loading ? (
        <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-14 bg-white rounded-xl border border-gray-100 animate-pulse" />)}</div>
      ) : (
        <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, overflow: 'hidden' }}>
          <DataTable
            headers={['Lead', 'Company', 'Source', 'Rating', 'Value', 'Status', 'Owner']}
            empty="No leads yet."
            onRowClick={(_, i) => L.leads[i] && openDetail(L.leads[i].id)}
            rows={L.leads.map(l => [
              <span style={{ fontWeight: 600, color: T.navy }}>{l.firstName} {l.lastName}{l.isStale ? <span title="No activity >2 days" style={{ color: T.red, marginLeft: 5 }}>●</span> : ''}</span>,
              l.companyName ?? '—',
              label(l.source),
              <Badge variant={RATING_VARIANT[l.rating] ?? 'default'}>{l.rating}</Badge>,
              <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(l.estimatedValue)}</span>,
              <Badge variant={LEAD_STATUS_VARIANT[l.status] ?? 'default'}>{label(l.status)}</Badge>,
              l.assignedToName ?? l.assignedTo ?? '—',
            ])}
          />
        </div>
      )}

      {L.totalPages > 1 && (
        <div className="flex items-center justify-between mt-5">
          <p className="text-sm text-gray-500">Page {L.page} of {L.totalPages}</p>
          <div className="flex gap-2">
            <button disabled={L.page === 1} onClick={() => L.setPage(p => p - 1)} className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50">Previous</button>
            <button disabled={L.page === L.totalPages} onClick={() => L.setPage(p => p + 1)} className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50">Next</button>
          </div>
        </div>
      )}

      {/* New Lead */}
      {modal === 'new' && (
        <Modal title="New Lead" onClose={() => setModal(null)} width={560}>
          <div className="grid grid-cols-2 gap-3">
            <Fld label="Source *"><select value={form.source} onChange={e => setForm(f => ({ ...f, source: e.target.value }))} className="input">{LEAD_SOURCES.map(s => <option key={s} value={s}>{label(s)}</option>)}</select></Fld>
            <Fld label="Source detail"><input value={form.sourceName} onChange={e => setForm(f => ({ ...f, sourceName: e.target.value }))} className="input" placeholder="e.g. which expo" /></Fld>
            <Fld label="First name"><input value={form.firstName} onChange={e => setForm(f => ({ ...f, firstName: e.target.value }))} className="input" /></Fld>
            <Fld label="Last name"><input value={form.lastName} onChange={e => setForm(f => ({ ...f, lastName: e.target.value }))} className="input" /></Fld>
            <Fld label="Company"><input value={form.companyName} onChange={e => setForm(f => ({ ...f, companyName: e.target.value }))} className="input" /></Fld>
            <Fld label="Industry"><input value={form.industry} onChange={e => setForm(f => ({ ...f, industry: e.target.value }))} className="input" /></Fld>
            <Fld label="Email"><input type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} className="input" /></Fld>
            <Fld label="Phone"><input value={form.phone} onChange={e => setForm(f => ({ ...f, phone: e.target.value }))} className="input" /></Fld>
            <Fld label="Estimated value (KES)"><input type="number" value={form.estimatedValue} onChange={e => setForm(f => ({ ...f, estimatedValue: e.target.value }))} className="input" /></Fld>
            <Fld label="Rating"><select value={form.rating} onChange={e => setForm(f => ({ ...f, rating: e.target.value }))} className="input">{LEAD_RATINGS.map(r => <option key={r} value={r}>{r}</option>)}</select></Fld>
          </div>

          {/* Which service lines the enquiry is for — drives routing to the right technical team
              and lets marketing report demand by line. */}
          <div className="pt-3">
            <p className="text-xs font-semibold text-gray-600 mb-1.5">Product range</p>
            <div className="flex flex-wrap gap-2">
              {PRODUCT_RANGES.map(r => {
                const on = form.productRange.includes(r)
                return (
                  <button key={r} type="button" onClick={() => toggleRange(r)}
                    className={`px-3 py-1.5 text-xs rounded-full border transition-colors ${
                      on ? 'bg-zinc-900 text-white border-zinc-900' : 'bg-white text-gray-600 border-gray-300 hover:bg-gray-50'}`}>
                    {r}
                  </button>
                )
              })}
            </div>
          </div>

          {createError && (
            <div className="mt-3 px-3 py-2 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">
              <p>{createError}</p>
              {existingCustomer && (
                <div className="mt-2">
                  <Btn size="sm" onClick={raiseOpportunity} disabled={busy}>
                    {busy ? 'Raising…' : `Raise opportunity for ${existingCustomer.name}`}
                  </Btn>
                </div>
              )}
            </div>
          )}

          <div className="flex justify-end gap-2 pt-3">
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={createLead} disabled={busy}>Capture Lead</Btn>
          </div>
        </Modal>
      )}

      {/* Detail */}
      {modal === 'detail' && detail && (
        <Modal title={`${detail.firstName} ${detail.lastName}${detail.companyName ? ' · ' + detail.companyName : ''}`} onClose={() => { setModal(null); setDetail(null) }} width={640}>
          <div className="flex items-center gap-2 mb-4 flex-wrap">
            <Badge variant={LEAD_STATUS_VARIANT[detail.status] ?? 'default'}>{label(detail.status)}</Badge>
            <Badge variant={RATING_VARIANT[detail.rating] ?? 'default'}>{detail.rating}</Badge>
            <span className="text-xs text-gray-500">{label(detail.source)}{detail.sourceName ? ` · ${detail.sourceName}` : ''} · {fmtKes(detail.estimatedValue)}</span>
            {detail.isStale && <span className="text-xs font-semibold text-red-600">● Stale</span>}
          </div>

          <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm mb-4">
            <Row k="Email" v={detail.email ?? '—'} /><Row k="Phone" v={detail.phone ?? '—'} />
            <Row k="Industry" v={detail.industry ?? '—'} /><Row k="Owner" v={detail.assignedToName ?? detail.assignedTo} />
            {detail.unqualifiedReason && <Row k="Unqualified" v={detail.unqualifiedReason} />}
            {detail.convertedAt && <Row k="Converted" v={fmtDate(detail.convertedAt)} />}
          </div>

          {/* Actions */}
          {L.canWrite && !closed && (
            <div className="flex gap-2 flex-wrap mb-4 pb-4 border-b border-gray-100">
              <Btn size="sm" variant="outline" onClick={() => { setAct({ activityType: 'Call', subject: '', description: '' }); setModal('activity') }}>Log Activity</Btn>
              {detail.status !== 'Qualified' && <Btn size="sm" onClick={() => { setQual({ rating: detail.rating, notes: '' }); setModal('qualify') }}>Qualify</Btn>}
              {detail.status === 'Qualified' && <Btn size="sm" variant="green" onClick={() => run(() => crm.convertLead(detail.id, {}), 'Lead converted.')} disabled={busy}>Convert</Btn>}
              <Btn size="sm" variant="danger" onClick={() => { setReason(''); setModal('unqualify') }}>Unqualify</Btn>
            </div>
          )}

          {/* Activity timeline */}
          <h4 className="text-sm font-bold text-gray-700 mb-2">Activity</h4>
          {(detail.activities?.length ?? 0) === 0 ? <p className="text-sm text-gray-400">No activity logged.</p> : (
            <div className="space-y-2">
              {detail.activities.map(a => (
                <div key={a.id} className="border border-gray-100 rounded-lg px-3 py-2">
                  <p className="text-sm font-semibold text-navy">{a.activityType} · {a.subject}</p>
                  {a.description && <p className="text-xs text-gray-500 mt-0.5">{a.description}</p>}
                  <p className="text-[11px] text-gray-400 mt-0.5">{fmtDate(a.activityDate)} · {a.performedBy}</p>
                </div>
              ))}
            </div>
          )}
        </Modal>
      )}

      {/* Log activity */}
      {modal === 'activity' && detail && (
        <Modal title="Log Activity" onClose={() => setModal('detail')} width={440}>
          <Fld label="Type"><select value={act.activityType} onChange={e => setAct(a => ({ ...a, activityType: e.target.value }))} className="input">{['Call','Email','Meeting','Note'].map(t => <option key={t}>{t}</option>)}</select></Fld>
          <Fld label="Subject *"><input value={act.subject} onChange={e => setAct(a => ({ ...a, subject: e.target.value }))} className="input" /></Fld>
          <Fld label="Notes"><textarea rows={2} value={act.description} onChange={e => setAct(a => ({ ...a, description: e.target.value }))} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1">
            <Btn variant="ghost" onClick={() => setModal('detail')}>Cancel</Btn>
            <Btn onClick={() => act.subject.trim() && run(() => crm.addLeadActivity(detail.id, act), 'Activity logged.').then(() => setModal('detail'))} disabled={busy || !act.subject.trim()}>Save</Btn>
          </div>
        </Modal>
      )}

      {/* Qualify */}
      {modal === 'qualify' && detail && (
        <Modal title="Qualify Lead" onClose={() => setModal('detail')} width={420}>
          <Fld label="Rating"><select value={qual.rating} onChange={e => setQual(q => ({ ...q, rating: e.target.value }))} className="input">{LEAD_RATINGS.map(r => <option key={r}>{r}</option>)}</select></Fld>
          <Fld label="Notes"><textarea rows={2} value={qual.notes} onChange={e => setQual(q => ({ ...q, notes: e.target.value }))} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1">
            <Btn variant="ghost" onClick={() => setModal('detail')}>Cancel</Btn>
            <Btn onClick={() => run(() => crm.qualifyLead(detail.id, qual), 'Lead qualified.').then(() => setModal('detail'))} disabled={busy}>Qualify</Btn>
          </div>
        </Modal>
      )}

      {/* Unqualify */}
      {modal === 'unqualify' && detail && (
        <Modal title="Unqualify Lead" onClose={() => setModal('detail')} width={420}>
          <Fld label="Reason *"><textarea rows={3} value={reason} onChange={e => setReason(e.target.value)} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1">
            <Btn variant="ghost" onClick={() => setModal('detail')}>Cancel</Btn>
            <Btn variant="danger" onClick={() => reason.trim() && run(() => crm.unqualifyLead(detail.id, { reason }), 'Lead unqualified.').then(() => setModal('detail'))} disabled={busy || !reason.trim()}>Unqualify</Btn>
          </div>
        </Modal>
      )}
    </div>
  )
}

function Fld({ label, children }) {
  return <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">{label}</span>{children}</label>
}
function Row({ k, v }) {
  return <><dt className="text-gray-500">{k}</dt><dd className="text-navy font-medium text-right">{v}</dd></>
}
