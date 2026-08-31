import { useState, useEffect, useCallback } from 'react'
import * as ops from '../../../services/operations.js'

// PR1 — what we quoted the client against what the job is costing, plus the detailed budget that
// goes through approval and the rate card it is priced from.

const kes = (v) => `KES ${Number(v ?? 0).toLocaleString('en-KE', { maximumFractionDigits: 0 })}`
const CATEGORIES = ['Labour', 'Materials', 'Equipment', 'Fleet', 'Subcontractor', 'Other']
const UNITS = ['Hour', 'Day', 'Item', 'Visit', 'Kilometre', 'LumpSum']

const STATUS_STYLE = {
  Draft:           'bg-gray-100 text-gray-700',
  PendingApproval: 'bg-amber-100 text-amber-800',
  Approved:        'bg-emerald-100 text-emerald-800',
  Rejected:        'bg-red-100 text-red-700',
  Superseded:      'bg-gray-100 text-gray-500',
}

function Stat({ label, value, sub, tone }) {
  const toneCls = tone === 'good' ? 'text-emerald-700' : tone === 'bad' ? 'text-red-700' : 'text-gray-900'
  return (
    <div className="border border-gray-200 rounded-lg px-4 py-3">
      <p className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">{label}</p>
      <p className={`text-lg font-bold mt-0.5 tabular-nums ${toneCls}`}>{value}</p>
      {sub && <p className="text-[11px] text-gray-400 mt-0.5">{sub}</p>}
    </div>
  )
}

export default function CommercialsTab({ projectId, canWrite, canApprove, onToast }) {
  const [comm, setComm]       = useState(null)
  const [versions, setVersions] = useState([])
  const [rates, setRates]     = useState([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy]       = useState(false)
  const [err, setErr]         = useState('')
  const [openVersion, setOpenVersion] = useState(null)
  const [line, setLine] = useState({ category: 0, description: '', quantity: '', contractRateId: '' })
  const [rate, setRate] = useState({ code: '', description: '', category: 0, unit: 0, clientRate: '', costRate: '' })
  const [showRates, setShowRates] = useState(false)

  const load = useCallback(async () => {
    setLoading(true); setErr('')
    try {
      const [c, v, r] = await Promise.all([
        ops.getProjectCommercials(projectId),
        ops.getBudgetVersions(projectId),
        ops.getContractRates(projectId),
      ])
      setComm(c); setVersions(v ?? []); setRates(r ?? [])
      const editable = (v ?? []).find(x => x.status === 'Draft' || x.status === 'PendingApproval')
      setOpenVersion(editable?.id ?? (v ?? [])[0]?.id ?? null)
    } catch { setErr('Failed to load commercials.') }
    finally { setLoading(false) }
  }, [projectId])

  useEffect(() => { load() }, [load])

  const run = async (fn, ok) => {
    setBusy(true); setErr('')
    try { await fn(); await load(); if (ok) onToast?.(ok) }
    catch (e) { setErr(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  const newVersion = async () => {
    const first = versions.length === 0
    // A revision must say why; the server enforces it, so ask here rather than bounce the user.
    const reason = first ? null : window.prompt('Why is the budget being revised?')
    if (!first && !reason) return
    await run(() => ops.createBudgetVersion(projectId, { copyFromApproved: !first, revisionReason: reason }),
              first ? 'Draft budget started.' : 'Revision started from the approved budget.')
  }

  const addLine = async (vId) => {
    if (!line.description.trim()) { setErr('Give the line a description.'); return }
    await run(() => ops.upsertBudgetLine(projectId, vId, {
      category: Number(line.category),
      description: line.description,
      quantity: line.quantity === '' ? null : Number(line.quantity),
      contractRateId: line.contractRateId || null,
    }), 'Line added.')
    setLine({ category: 0, description: '', quantity: '', contractRateId: '' })
  }

  const addRate = async () => {
    if (!rate.code.trim()) { setErr('A rate code is required.'); return }
    await run(() => ops.addContractRate(projectId, {
      ...rate, category: Number(rate.category), unit: Number(rate.unit),
      clientRate: Number(rate.clientRate) || 0, costRate: Number(rate.costRate) || 0,
    }), `Rate ${rate.code.toUpperCase()} added.`)
    setRate({ code: '', description: '', category: 0, unit: 0, clientRate: '', costRate: '' })
  }

  const reject = async (vId) => {
    const reason = window.prompt('Why is this budget being rejected?')
    if (!reason) return
    await run(() => ops.rejectBudgetVersion(projectId, vId, reason), 'Budget rejected.')
  }

  if (loading) return <p className="text-sm text-gray-500 py-8">Loading commercials…</p>

  const overspent = comm && comm.varianceToBaseline != null && comm.varianceToBaseline > 0
  const version = versions.find(v => v.id === openVersion)

  return (
    <div className="space-y-8">
      {err && <div className="px-4 py-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">{err}</div>}

      {/* Quote vs spend */}
      {comm && (
        <section>
          <h4 className="text-sm font-bold text-gray-800 mb-3">Quote vs spending</h4>
          <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-5 gap-3">
            <Stat label="Quoted to client" value={kes(comm.quotedTotal || comm.contractValue)}
                  sub={comm.quotedTotal ? 'sum of budget lines' : 'contract value — budget not priced yet'} />
            <Stat label="Approved baseline" value={comm.baselineBudget != null ? kes(comm.baselineBudget) : '—'}
                  sub={comm.baselineBudget == null ? 'no approved budget' : 'what variance measures against'} />
            <Stat label="Exposure" value={kes(comm.exposure)} sub="actual + committed" />
            <Stat label="vs baseline" value={comm.varianceToBaseline == null ? '—' : kes(comm.varianceToBaseline)}
                  tone={overspent ? 'bad' : 'good'} sub={overspent ? 'over what was approved' : 'within approval'} />
            <Stat label="Projected margin" value={kes(comm.projectedMargin)}
                  tone={comm.projectedMargin < 0 ? 'bad' : 'good'} sub={`${comm.projectedMarginPct}%`} />
          </div>

          {(comm.byCategory ?? []).length > 0 && (
            <div className="mt-4 overflow-x-auto border border-gray-200 rounded-lg">
              <table className="w-full text-sm min-w-[640px]">
                <thead className="bg-gray-50">
                  <tr>
                    {['Category', 'Quoted', 'Planned cost', 'Actual', 'Margin', 'Burn'].map((h, i) => (
                      <th key={h} className={`px-3 py-2 text-[11px] uppercase tracking-wide font-semibold text-gray-500 ${i ? 'text-right' : 'text-left'}`}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {comm.byCategory.map(c => (
                    <tr key={c.category}>
                      <td className="px-3 py-2 font-medium">{c.category}</td>
                      <td className="px-3 py-2 text-right tabular-nums">{kes(c.quoted)}</td>
                      <td className="px-3 py-2 text-right tabular-nums">{kes(c.planned)}</td>
                      <td className="px-3 py-2 text-right tabular-nums">{kes(c.actual)}</td>
                      <td className={`px-3 py-2 text-right tabular-nums font-medium ${c.margin < 0 ? 'text-red-700' : ''}`}>{kes(c.margin)}</td>
                      <td className="px-3 py-2 text-right">
                        {/* Over 100% means the category has spent more than it was budgeted. */}
                        <span className={`text-xs font-semibold ${c.burnPct > 100 ? 'text-red-700' : c.burnPct > 80 ? 'text-amber-700' : 'text-gray-600'}`}>
                          {c.burnPct}%
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {/* Budget versions */}
      <section>
        <div className="flex items-center justify-between mb-3 flex-wrap gap-2">
          <h4 className="text-sm font-bold text-gray-800">Detailed budget</h4>
          {canWrite && (
            <button onClick={newVersion} disabled={busy}
                    className="px-3 py-1.5 bg-navy text-white text-xs font-semibold rounded-lg disabled:opacity-60">
              {versions.length === 0 ? '+ Start budget' : '+ Revise budget'}
            </button>
          )}
        </div>

        {versions.length === 0 ? (
          <p className="text-sm text-gray-400">No budget yet. Start one, price the lines, then submit it for approval —
            approving it is what sets the project baseline.</p>
        ) : (
          <>
            <div className="flex flex-wrap gap-2 mb-4">
              {versions.map(v => (
                <button key={v.id} onClick={() => setOpenVersion(v.id)}
                        className={`px-3 py-1.5 text-xs font-semibold rounded-lg border transition-colors ${
                          openVersion === v.id ? 'border-navy bg-navy text-white' : 'border-gray-200 text-gray-600 hover:bg-gray-50'}`}>
                  v{v.versionNo}
                  <span className={`ml-2 px-1.5 py-0.5 rounded text-[10px] ${openVersion === v.id ? 'bg-white/20' : STATUS_STYLE[v.status]}`}>
                    {v.status}
                  </span>
                </button>
              ))}
            </div>

            {version && (
              <div className="border border-gray-200 rounded-lg overflow-hidden">
                <div className="px-4 py-3 bg-gray-50 border-b border-gray-200 flex flex-wrap items-center gap-4 justify-between">
                  <div className="text-sm">
                    <span className="text-gray-500">Planned </span><span className="font-semibold tabular-nums">{kes(version.totalPlanned)}</span>
                    <span className="text-gray-300 mx-2">·</span>
                    <span className="text-gray-500">Quoted </span><span className="font-semibold tabular-nums">{kes(version.totalQuoted)}</span>
                    <span className="text-gray-300 mx-2">·</span>
                    <span className="text-gray-500">Margin </span>
                    <span className={`font-semibold tabular-nums ${version.plannedMargin < 0 ? 'text-red-700' : 'text-emerald-700'}`}>
                      {kes(version.plannedMargin)} ({version.plannedMarginPct}%)
                    </span>
                  </div>
                  <div className="flex gap-2">
                    {canWrite && version.status === 'Draft' && (
                      <button onClick={() => run(() => ops.submitBudgetVersion(projectId, version.id), 'Submitted for approval.')}
                              disabled={busy} className="px-3 py-1.5 bg-navy text-white text-xs font-semibold rounded-lg disabled:opacity-60">
                        Submit for approval
                      </button>
                    )}
                    {canApprove && version.status === 'PendingApproval' && (
                      <>
                        <button onClick={() => run(() => ops.approveBudgetVersion(projectId, version.id), 'Budget approved — baseline set.')}
                                disabled={busy} className="px-3 py-1.5 bg-emerald-600 text-white text-xs font-semibold rounded-lg disabled:opacity-60">
                          Approve
                        </button>
                        <button onClick={() => reject(version.id)} disabled={busy}
                                className="px-3 py-1.5 border border-red-300 text-red-700 text-xs font-semibold rounded-lg disabled:opacity-60">
                          Reject
                        </button>
                      </>
                    )}
                  </div>
                </div>

                {version.revisionReason && (
                  <p className="px-4 py-2 text-xs text-gray-600 bg-amber-50 border-b border-amber-100">
                    <span className="font-semibold">Revision reason: </span>{version.revisionReason}
                  </p>
                )}
                {version.status === 'Rejected' && version.rejectionReason && (
                  <p className="px-4 py-2 text-xs text-red-700 bg-red-50 border-b border-red-100">
                    <span className="font-semibold">Rejected: </span>{version.rejectionReason}
                  </p>
                )}
                {version.status === 'PendingApproval' && (
                  <p className="px-4 py-2 text-xs text-amber-800 bg-amber-50 border-b border-amber-100">
                    Awaiting approval — the budget can't be edited now. Whoever submitted it can't approve it either.
                  </p>
                )}

                <div className="overflow-x-auto">
                  <table className="w-full text-sm min-w-[720px]">
                    <thead className="bg-white border-b border-gray-200">
                      <tr>
                        {['Category', 'Description', 'Qty', 'Rate', 'Planned cost', 'Quoted', 'Margin', ''].map((h, i) => (
                          <th key={i} className={`px-3 py-2 text-[11px] uppercase tracking-wide font-semibold text-gray-500 ${i > 1 && i < 7 ? 'text-right' : 'text-left'}`}>{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {(version.lines ?? []).length === 0 && (
                        <tr><td colSpan={8} className="px-3 py-6 text-center text-gray-400">No lines yet.</td></tr>
                      )}
                      {(version.lines ?? []).map(l => (
                        <tr key={l.id}>
                          <td className="px-3 py-2">{l.category}</td>
                          <td className="px-3 py-2">
                            {l.description}
                            {l.contractRateCode && <span className="ml-2 text-[10px] text-gray-400">{l.contractRateCode}</span>}
                          </td>
                          <td className="px-3 py-2 text-right tabular-nums">{l.quantity ?? '—'}</td>
                          <td className="px-3 py-2 text-right tabular-nums">{l.unitCostRate ?? '—'}</td>
                          <td className="px-3 py-2 text-right tabular-nums">{kes(l.plannedAmount)}</td>
                          <td className="px-3 py-2 text-right tabular-nums">{kes(l.quotedAmount)}</td>
                          <td className={`px-3 py-2 text-right tabular-nums ${l.plannedMargin < 0 ? 'text-red-700' : ''}`}>{kes(l.plannedMargin)}</td>
                          <td className="px-3 py-2 text-right">
                            {canWrite && version.status === 'Draft' && (
                              <button onClick={() => run(() => ops.deleteBudgetLine(projectId, version.id, l.id))}
                                      disabled={busy} className="text-xs text-red-600 hover:underline disabled:opacity-50">remove</button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {canWrite && version.status === 'Draft' && (
                  <div className="px-4 py-3 bg-gray-50 border-t border-gray-200 flex flex-wrap items-end gap-2">
                    <label className="text-xs text-gray-500"><span className="block mb-1">Category</span>
                      <select value={line.category} onChange={e => setLine(l => ({ ...l, category: e.target.value }))} className="input">
                        {CATEGORIES.map((c, i) => <option key={c} value={i}>{c}</option>)}
                      </select>
                    </label>
                    <label className="text-xs text-gray-500 flex-1 min-w-[180px]"><span className="block mb-1">Description</span>
                      <input value={line.description} onChange={e => setLine(l => ({ ...l, description: e.target.value }))} className="input w-full" />
                    </label>
                    <label className="text-xs text-gray-500"><span className="block mb-1">Qty</span>
                      <input type="number" value={line.quantity} onChange={e => setLine(l => ({ ...l, quantity: e.target.value }))} className="input w-24" />
                    </label>
                    <label className="text-xs text-gray-500"><span className="block mb-1">Priced from rate</span>
                      <select value={line.contractRateId} onChange={e => setLine(l => ({ ...l, contractRateId: e.target.value }))} className="input min-w-[170px]">
                        <option value="">— none —</option>
                        {rates.filter(r => r.isActive).map(r => <option key={r.id} value={r.id}>{r.code} · {r.clientRate}/{r.unit}</option>)}
                      </select>
                    </label>
                    <button onClick={() => addLine(version.id)} disabled={busy}
                            className="px-4 py-2 bg-navy text-white text-sm font-semibold rounded-lg disabled:opacity-60">Add line</button>
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </section>

      {/* Rate card */}
      <section>
        <button onClick={() => setShowRates(s => !s)} className="text-sm font-bold text-gray-800 mb-2 flex items-center gap-2">
          Contract rate card
          <span className="text-xs font-normal text-gray-400">({rates.length}) {showRates ? '▾' : '▸'}</span>
        </button>
        {showRates && (
          <>
            <p className="text-xs text-gray-500 mb-3">
              Each rate holds what the client pays and what it costs us, so budget lines priced from
              it carry margin automatically.
            </p>
            <div className="overflow-x-auto border border-gray-200 rounded-lg">
              <table className="w-full text-sm min-w-[620px]">
                <thead className="bg-gray-50">
                  <tr>
                    {['Code', 'Description', 'Unit', 'Client rate', 'Our cost', 'Margin/unit', ''].map((h, i) => (
                      <th key={i} className={`px-3 py-2 text-[11px] uppercase tracking-wide font-semibold text-gray-500 ${i > 2 && i < 6 ? 'text-right' : 'text-left'}`}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {rates.length === 0 && <tr><td colSpan={7} className="px-3 py-6 text-center text-gray-400">No rates yet.</td></tr>}
                  {rates.map(r => (
                    <tr key={r.id} className={r.isActive ? '' : 'opacity-50'}>
                      <td className="px-3 py-2 font-semibold">{r.code}</td>
                      <td className="px-3 py-2">{r.description}</td>
                      <td className="px-3 py-2">{r.unit}</td>
                      <td className="px-3 py-2 text-right tabular-nums">{r.clientRate}</td>
                      <td className="px-3 py-2 text-right tabular-nums">{r.costRate}</td>
                      <td className={`px-3 py-2 text-right tabular-nums font-medium ${r.marginPerUnit < 0 ? 'text-red-700' : 'text-emerald-700'}`}>{r.marginPerUnit}</td>
                      <td className="px-3 py-2 text-right">
                        {canWrite && r.isActive && (
                          <button onClick={() => run(() => ops.deactivateContractRate(projectId, r.id), 'Rate retired.')}
                                  disabled={busy} className="text-xs text-red-600 hover:underline disabled:opacity-50">retire</button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {canWrite && (
              <div className="mt-3 flex flex-wrap items-end gap-2">
                <label className="text-xs text-gray-500"><span className="block mb-1">Code</span>
                  <input value={rate.code} onChange={e => setRate(r => ({ ...r, code: e.target.value }))} placeholder="TECH-HR" className="input w-28" /></label>
                <label className="text-xs text-gray-500 flex-1 min-w-[160px]"><span className="block mb-1">Description</span>
                  <input value={rate.description} onChange={e => setRate(r => ({ ...r, description: e.target.value }))} className="input w-full" /></label>
                <label className="text-xs text-gray-500"><span className="block mb-1">Category</span>
                  <select value={rate.category} onChange={e => setRate(r => ({ ...r, category: e.target.value }))} className="input">
                    {CATEGORIES.map((c, i) => <option key={c} value={i}>{c}</option>)}
                  </select></label>
                <label className="text-xs text-gray-500"><span className="block mb-1">Unit</span>
                  <select value={rate.unit} onChange={e => setRate(r => ({ ...r, unit: e.target.value }))} className="input">
                    {UNITS.map((u, i) => <option key={u} value={i}>{u}</option>)}
                  </select></label>
                <label className="text-xs text-gray-500"><span className="block mb-1">Client rate</span>
                  <input type="number" value={rate.clientRate} onChange={e => setRate(r => ({ ...r, clientRate: e.target.value }))} className="input w-28" /></label>
                <label className="text-xs text-gray-500"><span className="block mb-1">Our cost</span>
                  <input type="number" value={rate.costRate} onChange={e => setRate(r => ({ ...r, costRate: e.target.value }))} className="input w-28" /></label>
                <button onClick={addRate} disabled={busy}
                        className="px-4 py-2 bg-navy text-white text-sm font-semibold rounded-lg disabled:opacity-60">Add rate</button>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  )
}
