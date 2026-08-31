import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Badge, Btn, Modal, Kpi, KPI_GRID } from '../ui.jsx'
import { T } from '../../theme/tokens.js'
import * as crm from '../../services/crm.js'
import { usePipeline } from '../../hooks/crm/usePipeline.js'

const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'

export default function PipelineTab() {
  const P = usePipeline()
  const navigate = useNavigate()
  const [toast, setToast] = useState('')
  const [modal, setModal] = useState(null)   // 'new' | 'detail' | 'activity' | 'lost'
  const [detail, setDetail] = useState(null)
  const [form, setForm] = useState({ name: '', customerName: '', estimatedValue: '', expectedCloseDate: '' })
  const [act, setAct] = useState({ activityType: 'Call', subject: '', description: '', outcome: '' })
  const [lost, setLost] = useState({ reason: '', competitorName: '', competitorNotes: '' })
  const [busy, setBusy] = useState(false)

  // C5 — deal close from won opportunities.
  const [wonOpps, setWonOpps] = useState([])
  const [dealOpp, setDealOpp] = useState(null)
  const [deal, setDeal] = useState(null)
  const [contract, setContract] = useState({ title: '', contractType: '', startDate: '', endDate: '', retentionPct: 0, fileUrl: '' })
  const [showContract, setShowContract] = useState(false)

  const loadWon = useCallback(async () => {
    try { const r = await crm.listOpportunities({ status: 'Won', pageSize: 50 }); setWonOpps(r.data ?? []) } catch { setWonOpps([]) }
  }, [])
  useEffect(() => { loadWon() }, [loadWon])

  const flash = (m) => { setToast(m); setTimeout(() => setToast(''), 3500) }
  const openStages = P.stages.filter(s => s.stageType === 'Open')
  const isOpen = detail && detail.status === 'Open'

  const openDetail = async (id) => { try { setDetail(await crm.getOpportunity(id)); setModal('detail') } catch { flash('Failed to load.') } }
  const refresh = async () => { if (detail) setDetail(await crm.getOpportunity(detail.id)); P.reload(); loadWon() }

  // Deal-close flow.
  const openDeal = async (opp) => {
    setDealOpp(opp); setShowContract(false)
    try { setDeal(opp.dealId ? await crm.getDeal(opp.dealId) : null) } catch { setDeal(null) }
    setModal('deal')
  }
  const refreshDeal = async () => { if (deal) setDeal(await crm.getDeal(deal.id)); loadWon() }
  const runDeal = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); if (r && r.id && r.dealNumber) setDeal(r); await refreshDeal(); flash(ok) }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }
  const createDealForOpp = () => runDeal(() => crm.createDeal({ opportunityId: dealOpp.id }), 'Deal created.')
  const saveContract = async () => {
    if (!contract.title.trim()) return flash('Contract title required.')
    await runDeal(() => crm.registerContract(deal.id, { ...contract, retentionPct: Number(contract.retentionPct) || 0, startDate: contract.startDate || undefined, endDate: contract.endDate || undefined }), 'Contract registered.')
    setShowContract(false)
  }
  const run = async (fn, ok) => {
    setBusy(true)
    try { await fn(); await refresh(); flash(ok) }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  const createOpp = async () => {
    if (!form.name.trim()) return flash('Name is required.')
    setBusy(true)
    try {
      await crm.createOpportunity({ name: form.name, customerName: form.customerName || undefined, estimatedValue: Number(form.estimatedValue) || 0, expectedCloseDate: form.expectedCloseDate || undefined })
      setModal(null); setForm({ name: '', customerName: '', estimatedValue: '', expectedCloseDate: '' }); P.reload(); flash('Opportunity created.')
    } catch (e) { flash(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }

  return (
    <div>
      {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}

      <div style={{ ...KPI_GRID, marginBottom: 20 }}>
        <Kpi label="Open Opportunities" value={P.loading ? '…' : P.totals.openCount} icon="📈" />
        <Kpi label="Pipeline Value" value={P.loading ? '…' : fmtKes(P.totals.totalValue)} sub="Sum of open est. value" icon="💰" variant="blue" />
        <Kpi label="Weighted Pipeline" value={P.loading ? '…' : fmtKes(P.totals.totalWeighted)} sub="Σ value × win probability" icon="🎯" variant="green" />
      </div>

      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-bold text-navy">Pipeline</h2>
        {P.canWrite && <Btn onClick={() => setModal('new')}>+ New Opportunity</Btn>}
      </div>

      {P.error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{P.error}</div>}

      {P.loading ? (
        <div className="h-64 bg-white rounded-xl border border-gray-100 animate-pulse" />
      ) : (
        <div className="flex gap-4 overflow-x-auto pb-3">
          {P.columns.map(col => (
            <div key={col.stage.id} className="flex-shrink-0 w-72">
              <div className="bg-white rounded-t-xl border border-gray-200 px-4 py-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-bold text-navy">{col.stage.name}</span>
                  <span className="text-xs text-gray-400">{col.count} · {Math.round(col.stage.winProbability * 100)}%</span>
                </div>
                <p className="text-xs text-gray-500 mt-0.5">{fmtKes(col.weightedValue)} weighted</p>
              </div>
              <div className="bg-offwhite border-x border-b border-gray-200 rounded-b-xl p-2 space-y-2 min-h-[120px]">
                {col.opportunities.length === 0 ? (
                  <p className="text-xs text-gray-400 text-center py-6">—</p>
                ) : col.opportunities.map(o => (
                  <button key={o.id} onClick={() => openDetail(o.id)}
                    className="block w-full text-left bg-white rounded-lg border border-gray-200 hover:border-gold hover:shadow-sm transition p-3">
                    <p className="text-sm font-semibold text-navy leading-snug truncate">{o.name}</p>
                    <p className="text-xs text-gray-500 truncate">{o.customerName ?? '—'}</p>
                    <div className="flex items-center justify-between mt-1.5">
                      <span className="text-xs font-bold text-navy">{fmtKes(o.estimatedValue)}</span>
                      {o.isStale && <span title="No movement >14 days" className="text-red-500 text-xs">● stale</span>}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Won — deal close */}
      {wonOpps.length > 0 && (
        <div className="mt-6">
          <h2 className="text-lg font-bold text-navy mb-3">Won — Deal Close</h2>
          <div className="bg-white border border-gray-200 rounded-xl divide-y divide-gray-100">
            {wonOpps.map(o => (
              <div key={o.id} className="flex items-center justify-between px-4 py-3">
                <div>
                  <p className="text-sm font-semibold text-navy">{o.opportunityNumber} · {o.name}</p>
                  <p className="text-xs text-gray-500">{o.customerName ?? '—'} · {fmtKes(o.estimatedValue)}</p>
                </div>
                <Btn size="sm" variant={o.dealId ? 'outline' : 'green'} onClick={() => openDeal(o)}>{o.dealId ? 'Manage Deal' : 'Close Deal'}</Btn>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Deal modal */}
      {modal === 'deal' && dealOpp && (
        <Modal title={`Deal — ${dealOpp.name}`} onClose={() => { setModal(null); setDeal(null); setDealOpp(null) }} width={600}>
          {!deal ? (
            <div className="text-center py-6">
              <p className="text-sm text-gray-500 mb-4">Create the deal from this won opportunity. It links the accepted quotation and copies its products.</p>
              {P.canWrite && <Btn variant="green" onClick={createDealForOpp} disabled={busy}>Create Deal</Btn>}
            </div>
          ) : (
            <div>
              <div className="flex items-center gap-2 mb-4 flex-wrap">
                <span className="text-sm font-bold text-navy">{deal.dealNumber}</span>
                <Badge variant={deal.status === 'Closed' ? 'green' : 'blue'}>{deal.status}</Badge>
                <span className="text-xs text-gray-500">{fmtKes(deal.contractValue)}</span>
                {deal.invoiceTriggered && <span className="text-xs text-green-600">● invoice fired</span>}
                {deal.projectId && <button onClick={() => navigate(`/modules/operations/projects/${deal.projectId}`)} className="text-xs text-navy hover:underline">● project {deal.projectId.slice(0, 8)} ↗</button>}
              </div>

              {/* Contract */}
              <div className="border border-gray-100 rounded-lg p-3 mb-3">
                <div className="flex items-center justify-between">
                  <h4 className="text-sm font-bold text-gray-700">Contract</h4>
                  {P.canWrite && deal.status !== 'Closed' && <button onClick={() => { setContract({ title: deal.contract?.title ?? '', contractType: deal.contract?.contractType ?? '', startDate: '', endDate: '', retentionPct: deal.contract?.retentionPct ?? 0, fileUrl: deal.contract?.fileUrl ?? '' }); setShowContract(v => !v) }} className="text-xs font-semibold text-navy hover:underline">{deal.contract ? 'Update' : 'Register'}</button>}
                </div>
                {deal.contract ? (
                  <p className="text-xs text-gray-600 mt-1">{deal.contract.contractNumber} · {deal.contract.title} · {deal.contract.status}{deal.contract.endDate ? ` · ends ${fmtDate(deal.contract.endDate)}` : ''}</p>
                ) : <p className="text-xs text-gray-400 mt-1">No contract registered.</p>}
                {showContract && (
                  <div className="grid grid-cols-2 gap-2 mt-2">
                    <input value={contract.title} onChange={e => setContract(c => ({ ...c, title: e.target.value }))} className="input" placeholder="Title *" style={{ marginBottom: 0 }} />
                    <input value={contract.contractType} onChange={e => setContract(c => ({ ...c, contractType: e.target.value }))} className="input" placeholder="Type" style={{ marginBottom: 0 }} />
                    <input type="date" value={contract.startDate} onChange={e => setContract(c => ({ ...c, startDate: e.target.value }))} className="input" style={{ marginBottom: 0 }} />
                    <input type="date" value={contract.endDate} onChange={e => setContract(c => ({ ...c, endDate: e.target.value }))} className="input" style={{ marginBottom: 0 }} />
                    <input type="number" value={contract.retentionPct} onChange={e => setContract(c => ({ ...c, retentionPct: e.target.value }))} className="input" placeholder="Retention %" style={{ marginBottom: 0 }} />
                    <input value={contract.fileUrl} onChange={e => setContract(c => ({ ...c, fileUrl: e.target.value }))} className="input" placeholder="Signed contract URL" style={{ marginBottom: 0 }} />
                    <div className="col-span-2 flex justify-end"><Btn size="sm" onClick={saveContract} disabled={busy}>Save Contract</Btn></div>
                  </div>
                )}
              </div>

              {P.canWrite && (
                <div className="flex gap-2 flex-wrap pt-2 border-t border-gray-100">
                  {!deal.projectId && <Btn size="sm" variant="outline" onClick={() => runDeal(() => crm.createDealProject(deal.id), 'Project requested.')} disabled={busy}>Create Project</Btn>}
                  {deal.status !== 'Closed' && <Btn size="sm" variant="green" onClick={() => runDeal(() => crm.closeDeal(deal.id), 'Deal closed.')} disabled={busy}>Close Deal &amp; Invoice</Btn>}
                </div>
              )}
              <p className="text-[11px] text-gray-400 mt-3">Project creation &amp; Finance invoice run through config-gated seams — enable Operations / Finance integration to complete them.</p>
            </div>
          )}
        </Modal>
      )}

      {/* New Opportunity */}
      {modal === 'new' && (
        <Modal title="New Opportunity" onClose={() => setModal(null)} width={480}>
          <Fld label="Name *"><input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} className="input" placeholder="e.g. Summit Foods — annual calibration" /></Fld>
          <Fld label="Customer"><input value={form.customerName} onChange={e => setForm(f => ({ ...f, customerName: e.target.value }))} className="input" /></Fld>
          <div className="grid grid-cols-2 gap-3">
            <Fld label="Estimated value (KES)"><input type="number" value={form.estimatedValue} onChange={e => setForm(f => ({ ...f, estimatedValue: e.target.value }))} className="input" /></Fld>
            <Fld label="Expected close"><input type="date" value={form.expectedCloseDate} onChange={e => setForm(f => ({ ...f, expectedCloseDate: e.target.value }))} className="input" /></Fld>
          </div>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn><Btn onClick={createOpp} disabled={busy}>Create</Btn></div>
        </Modal>
      )}

      {/* Detail */}
      {modal === 'detail' && detail && (
        <Modal title={`${detail.opportunityNumber} · ${detail.name}`} onClose={() => { setModal(null); setDetail(null) }} width={640}>
          <div className="flex items-center gap-2 mb-4 flex-wrap">
            <Badge variant={detail.status === 'Won' ? 'green' : detail.status === 'Lost' ? 'red' : 'blue'}>{detail.status}</Badge>
            <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-navy/10 text-navy">{detail.stageName} · {Math.round(detail.probability * 100)}%</span>
            <span className="text-xs text-gray-500">{fmtKes(detail.estimatedValue)} · weighted {fmtKes(detail.weightedValue)}</span>
            {detail.isStale && <span className="text-xs font-semibold text-red-600">● Stale</span>}
          </div>

          <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm mb-4">
            <Row k="Customer" v={detail.customerName ?? '—'} /><Row k="Owner" v={detail.assignedToName ?? detail.assignedTo} />
            <Row k="Expected close" v={fmtDate(detail.expectedCloseDate)} />
            {detail.actualCloseDate && <Row k="Closed" v={fmtDate(detail.actualCloseDate)} />}
            {detail.lostReason && <Row k="Lost reason" v={detail.lostReason} />}
          </div>

          {P.canWrite && isOpen && (
            <div className="flex gap-2 flex-wrap items-center mb-4 pb-4 border-b border-gray-100">
              <select value={detail.pipelineStageId} onChange={e => run(() => crm.advanceOpportunity(detail.id, { pipelineStageId: e.target.value }), 'Stage updated.')}
                className="input" style={{ width: 'auto', marginBottom: 0 }}>
                {openStages.map(s => <option key={s.id} value={s.id}>{s.name} ({Math.round(s.winProbability * 100)}%)</option>)}
              </select>
              <Btn size="sm" variant="outline" onClick={() => { setAct({ activityType: 'Call', subject: '', description: '', outcome: '' }); setModal('activity') }}>Log Activity</Btn>
              <Btn size="sm" variant="green" onClick={() => run(() => crm.winOpportunity(detail.id), 'Marked won.')} disabled={busy}>Won</Btn>
              <Btn size="sm" variant="danger" onClick={() => { setLost({ reason: '', competitorName: '', competitorNotes: '' }); setModal('lost') }}>Lost</Btn>
            </div>
          )}

          {(detail.competitors?.length ?? 0) > 0 && (
            <div className="mb-4">
              <h4 className="text-sm font-bold text-gray-700 mb-1">Competitors</h4>
              {detail.competitors.map(c => <p key={c.id} className="text-sm text-gray-600">{c.competitorName}{c.wasSelected ? ' (won)' : ''}{c.notes ? ` — ${c.notes}` : ''}</p>)}
            </div>
          )}

          <h4 className="text-sm font-bold text-gray-700 mb-2">Activity</h4>
          {(detail.activities?.length ?? 0) === 0 ? <p className="text-sm text-gray-400">No activity logged.</p> : (
            <div className="space-y-2">
              {detail.activities.map(a => (
                <div key={a.id} className="border border-gray-100 rounded-lg px-3 py-2">
                  <p className="text-sm font-semibold text-navy">{a.activityType} · {a.subject}</p>
                  {a.description && <p className="text-xs text-gray-500 mt-0.5">{a.description}</p>}
                  {a.outcome && <p className="text-xs text-gray-600 mt-0.5">Outcome: {a.outcome}</p>}
                  <p className="text-[11px] text-gray-400 mt-0.5">{fmtDate(a.activityDate)} · {a.performedBy}{a.nextFollowUp ? ` · follow-up ${fmtDate(a.nextFollowUp)}` : ''}</p>
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
          <Fld label="Outcome"><input value={act.outcome} onChange={e => setAct(a => ({ ...a, outcome: e.target.value }))} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal('detail')}>Cancel</Btn><Btn onClick={() => act.subject.trim() && run(() => crm.addOpportunityActivity(detail.id, act), 'Activity logged.').then(() => setModal('detail'))} disabled={busy || !act.subject.trim()}>Save</Btn></div>
        </Modal>
      )}

      {/* Lost */}
      {modal === 'lost' && detail && (
        <Modal title="Mark Opportunity Lost" onClose={() => setModal('detail')} width={440}>
          <Fld label="Reason *"><textarea rows={2} value={lost.reason} onChange={e => setLost(l => ({ ...l, reason: e.target.value }))} className="input" /></Fld>
          <Fld label="Winning competitor"><input value={lost.competitorName} onChange={e => setLost(l => ({ ...l, competitorName: e.target.value }))} className="input" /></Fld>
          <Fld label="Competitor notes"><input value={lost.competitorNotes} onChange={e => setLost(l => ({ ...l, competitorNotes: e.target.value }))} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal('detail')}>Cancel</Btn><Btn variant="danger" onClick={() => lost.reason.trim() && run(() => crm.loseOpportunity(detail.id, lost), 'Marked lost.').then(() => setModal('detail'))} disabled={busy || !lost.reason.trim()}>Confirm Lost</Btn></div>
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
