import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Kpi, KPI_GRID, Modal } from '../../components/ui.jsx'
import * as crm from '../../services/crm.js'
import {
  useAfterSales, CONTRACT_TYPES, CONTRACT_STATUSES, CONTRACT_STATUS_VARIANT,
  COMPLAINT_SEVERITIES, COMPLAINT_STATUS_VARIANT, SEVERITY_VARIANT, complaintStatusLabel,
  SURVEY_STATUS_VARIANT, NPS_CATEGORY_VARIANT,
} from '../../hooks/crm/useAfterSales.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const todayISO = () => new Date().toISOString().slice(0, 10)

export default function AfterSalesPage() {
  const a = useAfterSales()
  const [tab, setTab] = useState('overview')
  const [modal, setModal] = useState(null) // { kind, ... }
  const [toast, setToast] = useState('')
  const flash = (x) => { setToast(x); setTimeout(() => setToast(''), 3000) }
  const s = a.summary

  const close = () => setModal(null)
  const afterSave = (msg) => { close(); flash(msg); a.reload() }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>After-Sales &amp; Retention</h1>
            <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>Satisfaction, service contracts, complaints &amp; NPS</p>
          </div>
          <Btn size="sm" variant="ghost" onClick={a.reload} disabled={a.loading}>{a.loading ? 'Refreshing…' : '↻ Refresh'}</Btn>
        </div>

        {a.error && <Card style={{ marginBottom: 16, borderColor: T.red }}><span style={{ color: T.red, fontSize: 13 }}>{a.error}</span></Card>}

        <Tabs tabs={[
          { id: 'overview', label: 'Overview' },
          { id: 'surveys', label: 'Satisfaction' },
          { id: 'contracts', label: 'Service Contracts' },
          { id: 'complaints', label: 'Complaints' },
          { id: 'nps', label: 'NPS' },
        ]} active={tab} setActive={setTab} />

        {/* ── Overview ── */}
        {tab === 'overview' && (
          <div>
            <div style={{ ...KPI_GRID, marginBottom: 20 }}>
              <Kpi label="Average CSAT" value={a.loading ? '…' : `${s?.averageCsat ?? 0} / 5`} sub={`${s?.csatResponses ?? 0} responses`} icon="⭐" />
              <Kpi label="Current NPS" value={a.loading ? '…' : (s?.currentNps ?? 0)} icon="📣" variant="blue" />
              <Kpi label="Open Complaints" value={a.loading ? '…' : (s?.openComplaints ?? 0)} sub={`${s?.resolvedThisMonth ?? 0} resolved this month`} icon="⚠️" variant={s?.openComplaints > 0 ? 'amber' : 'green'} />
              <Kpi label="Active Contracts" value={a.loading ? '…' : (s?.activeServiceContracts ?? 0)} sub={`${s?.contractsExpiringSoon ?? 0} expiring ≤60d`} icon="📄" variant="green" />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 20 }}>
              <Card>
                <SectionHeader title="NPS Trend" sub="Year over year · %promoters − %detractors" />
                <DataTable headers={['Year', 'Responses', 'Promoters', 'Passives', 'Detractors', 'NPS']}
                  empty={a.loading ? 'Loading…' : 'No NPS responses yet.'}
                  rows={(s?.npsTrend ?? []).map(y => [y.year, y.responses, y.promoters, y.passives, y.detractors,
                    <strong style={{ color: y.npsScore >= 0 ? T.green : T.red }}>{y.npsScore}</strong>])} />
              </Card>
              <Card>
                <SectionHeader title="Pending Follow-ups" sub="Needs attention" />
                <div style={{ display: 'grid', gap: 10 }}>
                  <RiskLine label="Surveys awaiting response" value={s?.surveysPending} />
                  <RiskLine label="Open complaints" value={s?.openComplaints} />
                  <RiskLine label="Contracts expiring (≤60d)" value={s?.contractsExpiringSoon} />
                </div>
              </Card>
            </div>
          </div>
        )}

        {/* ── Satisfaction surveys ── */}
        {tab === 'surveys' && (
          <div>
            <ActionBar canWrite={a.canWrite} label="+ Send Survey" onClick={() => setModal({ kind: 'send-survey' })} />
            <Card>
              <DataTable headers={['Customer', 'Project', 'Source', 'Status', 'Score', 'Sent', a.canWrite ? '' : null].filter(x => x !== null)}
                empty={a.loading ? 'Loading…' : 'No surveys yet.'}
                rows={a.surveys.map(sv => [
                  sv.customerName ?? sv.customerId,
                  sv.projectName ?? '—',
                  sv.source === 'ProjectClose' ? 'Project close' : 'Manual',
                  <Badge variant={SURVEY_STATUS_VARIANT[sv.status] ?? 'default'}>{sv.status}</Badge>,
                  sv.score != null ? `${sv.score} / 5` : '—',
                  fmtDate(sv.sentAt),
                  ...(a.canWrite ? [sv.status === 'Pending'
                    ? <Btn size="sm" onClick={() => setModal({ kind: 'respond-survey', item: sv })}>Record</Btn>
                    : <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>] : []),
                ])} />
            </Card>
          </div>
        )}

        {/* ── Service contracts ── */}
        {tab === 'contracts' && (
          <div>
            <ActionBar canWrite={a.canWrite} label="+ New Contract" onClick={() => setModal({ kind: 'new-contract' })} />
            <Card>
              <DataTable headers={['Contract', 'Customer', 'Type', 'Period', 'Value', 'Expiry', 'Status']}
                empty={a.loading ? 'Loading…' : 'No service contracts yet.'}
                rows={a.contracts.map(c => [
                  <button onClick={() => setModal({ kind: 'contract-detail', id: c.id })} style={linkBtn}>{c.contractNumber}</button>,
                  c.customerName ?? c.customerId,
                  c.contractType,
                  <span style={{ fontSize: 12, whiteSpace: 'nowrap' }}>{fmtDate(c.startDate)} – {fmtDate(c.endDate)}</span>,
                  <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(c.value)}</span>,
                  <ExpiryBadge days={c.daysToExpiry} status={c.status} />,
                  <Badge variant={CONTRACT_STATUS_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>,
                ])} />
            </Card>
          </div>
        )}

        {/* ── Complaints ── */}
        {tab === 'complaints' && (
          <div>
            <ActionBar canWrite={a.canWrite} label="+ Raise Complaint" onClick={() => setModal({ kind: 'raise-complaint' })} />
            <Card>
              <DataTable headers={['Ref', 'Customer', 'Subject', 'Severity', 'Status', 'Assigned', 'Raised']}
                empty={a.loading ? 'Loading…' : 'No complaints logged.'}
                rows={a.complaints.map(c => [
                  <button onClick={() => setModal({ kind: 'complaint-detail', id: c.id })} style={linkBtn}>{c.complaintNumber}</button>,
                  c.customerName ?? c.customerId,
                  c.subject,
                  <Badge variant={SEVERITY_VARIANT[c.severity] ?? 'default'}>{c.severity}</Badge>,
                  <Badge variant={COMPLAINT_STATUS_VARIANT[c.status] ?? 'default'}>{complaintStatusLabel(c.status)}</Badge>,
                  c.assignedToName ?? c.assignedTo ?? '—',
                  fmtDate(c.raisedAt),
                ])} />
            </Card>
          </div>
        )}

        {/* ── NPS ── */}
        {tab === 'nps' && (
          <div>
            <ActionBar canWrite={a.canWrite} label="+ Send NPS" onClick={() => setModal({ kind: 'send-nps' })} />
            <Card>
              <DataTable headers={['Customer', 'Year', 'Status', 'Score', 'Category', 'Sent', a.canWrite ? '' : null].filter(x => x !== null)}
                empty={a.loading ? 'Loading…' : 'No NPS surveys yet.'}
                rows={a.nps.map(n => [
                  n.customerName ?? n.customerId,
                  n.year,
                  <Badge variant={SURVEY_STATUS_VARIANT[n.status] ?? 'default'}>{n.status}</Badge>,
                  n.score != null ? `${n.score} / 10` : '—',
                  n.category && n.category !== 'None' ? <Badge variant={NPS_CATEGORY_VARIANT[n.category] ?? 'default'}>{n.category}</Badge> : '—',
                  fmtDate(n.sentAt),
                  ...(a.canWrite ? [n.status === 'Pending'
                    ? <Btn size="sm" onClick={() => setModal({ kind: 'respond-nps', item: n })}>Record</Btn>
                    : <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>] : []),
                ])} />
            </Card>
          </div>
        )}
      </div>

      {modal?.kind === 'send-survey' && <SendSurveyModal onClose={close} onSaved={() => afterSave('Survey sent.')} onErr={flash} />}
      {modal?.kind === 'respond-survey' && <RespondSurveyModal item={modal.item} onClose={close} onSaved={() => afterSave('Response recorded.')} onErr={flash} />}
      {modal?.kind === 'new-contract' && <ContractModal onClose={close} onSaved={() => afterSave('Contract created.')} onErr={flash} />}
      {modal?.kind === 'contract-detail' && <ContractDetailModal id={modal.id} canWrite={a.canWrite} onClose={close} onChanged={a.reload} onErr={flash} />}
      {modal?.kind === 'raise-complaint' && <RaiseComplaintModal onClose={close} onSaved={() => afterSave('Complaint logged.')} onErr={flash} />}
      {modal?.kind === 'complaint-detail' && <ComplaintDetailModal id={modal.id} canWrite={a.canWrite} onClose={close} onChanged={a.reload} onErr={flash} />}
      {modal?.kind === 'send-nps' && <SendNpsModal onClose={close} onSaved={() => afterSave('NPS survey sent.')} onErr={flash} />}
      {modal?.kind === 'respond-nps' && <RespondNpsModal item={modal.item} onClose={close} onSaved={() => afterSave('Response recorded.')} onErr={flash} />}
    </>
  )
}

/* ── shared bits ── */
const linkBtn = { fontWeight: 600, color: T.navy, background: 'none', border: 0, cursor: 'pointer', padding: 0, textAlign: 'left' }

function ActionBar({ canWrite, label, onClick }) {
  if (!canWrite) return null
  return <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}><Btn size="sm" onClick={onClick}>{label}</Btn></div>
}
function RiskLine({ label, value }) {
  const v = value ?? 0
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <span style={{ fontSize: 13, color: T.mgrey }}>{label}</span>
      <strong style={{ fontSize: 16, color: v > 0 ? T.red : T.green }}>{v}</strong>
    </div>
  )
}
function ExpiryBadge({ days, status }) {
  if (status !== 'Active') return <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>
  const color = days <= 30 ? T.red : days <= 60 ? T.gold : T.green
  return <span style={{ color, fontWeight: 600, fontSize: 12, whiteSpace: 'nowrap' }}>{days}d</span>
}
function Field({ label, children, flex }) {
  return (
    <label className="block mb-3" style={flex ? { flex: 1 } : undefined}>
      <span className="block text-xs font-medium text-gray-500 mb-1">{label}</span>
      {children}
    </label>
  )
}
function Stat({ label, value }) {
  return (
    <div>
      <div style={{ fontSize: 11, color: T.mgrey, textTransform: 'uppercase', letterSpacing: 0.4 }}>{label}</div>
      <div style={{ fontSize: 14, fontWeight: 700, color: T.navy }}>{value}</div>
    </div>
  )
}

// Loads active customers into a <select>. Value = customer id.
function CustomerSelect({ value, onChange }) {
  const [opts, setOpts] = useState([])
  useEffect(() => {
    crm.listCustomers({ pageSize: 200 }).then(r => setOpts(r.data ?? [])).catch(() => setOpts([]))
  }, [])
  return (
    <select value={value} onChange={e => onChange(e.target.value)} className="input">
      <option value="">Select a customer…</option>
      {opts.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
    </select>
  )
}

/* ── Surveys ── */
function SendSurveyModal({ onClose, onSaved, onErr }) {
  const [f, setF] = useState({ customerId: '', projectId: '', projectName: '' })
  const [busy, setBusy] = useState(false)
  const save = async () => {
    if (!f.customerId) return onErr('Select a customer.')
    setBusy(true)
    try { await crm.sendSurvey({ customerId: f.customerId, projectId: f.projectId || undefined, projectName: f.projectName || undefined }); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title="Send Satisfaction Survey" onClose={onClose} width={440}>
      <Field label="Customer *"><CustomerSelect value={f.customerId} onChange={v => setF(s => ({ ...s, customerId: v }))} /></Field>
      <Field label="Project reference (optional)"><input value={f.projectId} onChange={e => setF(s => ({ ...s, projectId: e.target.value }))} className="input" placeholder="PRJ-…" /></Field>
      <Field label="Project name (optional)"><input value={f.projectName} onChange={e => setF(s => ({ ...s, projectName: e.target.value }))} className="input" /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Sending…' : 'Send'}</Btn></div>
    </Modal>
  )
}
function RespondSurveyModal({ item, onClose, onSaved, onErr }) {
  const [score, setScore] = useState(5)
  const [feedback, setFeedback] = useState('')
  const [busy, setBusy] = useState(false)
  const save = async () => {
    setBusy(true)
    try { await crm.respondSurvey(item.id, { score: Number(score), feedback: feedback || undefined }); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title="Record Survey Response" onClose={onClose} width={420}>
      <p style={{ fontSize: 13, color: T.mgrey, margin: '0 0 12px' }}>{item.customerName} · {item.projectName ?? 'Manual survey'}</p>
      <Field label="Satisfaction score (1–5)">
        <div style={{ display: 'flex', gap: 6 }}>
          {[1, 2, 3, 4, 5].map(n => <button key={n} onClick={() => setScore(n)} className="input" style={{ flex: 1, cursor: 'pointer', fontWeight: 700, background: score === n ? T.gold : undefined, color: score === n ? '#fff' : undefined }}>{n}</button>)}
        </div>
      </Field>
      <Field label="Feedback"><textarea value={feedback} onChange={e => setFeedback(e.target.value)} className="input" rows={2} /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Save'}</Btn></div>
    </Modal>
  )
}

/* ── Service contracts ── */
function ContractModal({ onClose, onSaved, onErr }) {
  const [f, setF] = useState({ customerId: '', contractType: 'Maintenance', description: '', startDate: todayISO(), endDate: '', value: '', billingFrequency: 'Annual', autoRenew: false })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.customerId) return onErr('Select a customer.')
    if (!f.endDate) return onErr('End date is required.')
    setBusy(true)
    try {
      await crm.createServiceContract({ ...f, value: Number(f.value) || 0, description: f.description || undefined })
      onSaved()
    } catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title="New Service Contract" onClose={onClose} width={480}>
      <Field label="Customer *"><CustomerSelect value={f.customerId} onChange={v => upd('customerId', v)} /></Field>
      <Field label="Type"><select value={f.contractType} onChange={e => upd('contractType', e.target.value)} className="input">{CONTRACT_TYPES.map(t => <option key={t} value={t}>{t}</option>)}</select></Field>
      <Field label="Description"><input value={f.description} onChange={e => upd('description', e.target.value)} className="input" /></Field>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Start date" flex><input type="date" value={f.startDate} onChange={e => upd('startDate', e.target.value)} className="input" /></Field>
        <Field label="End date *" flex><input type="date" value={f.endDate} onChange={e => upd('endDate', e.target.value)} className="input" /></Field>
      </div>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Value (KES)" flex><input type="number" value={f.value} onChange={e => upd('value', e.target.value)} className="input" /></Field>
        <Field label="Billing" flex><input value={f.billingFrequency} onChange={e => upd('billingFrequency', e.target.value)} className="input" /></Field>
      </div>
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginBottom: 12 }}>
        <input type="checkbox" checked={f.autoRenew} onChange={e => upd('autoRenew', e.target.checked)} /> Auto-renew
      </label>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Create'}</Btn></div>
    </Modal>
  )
}
function ContractDetailModal({ id, canWrite, onClose, onChanged, onErr }) {
  const [c, setC] = useState(null)
  const [busy, setBusy] = useState(false)
  const [renew, setRenew] = useState({ open: false, newEndDate: '', newValue: '' })
  const reload = useCallback(async () => { try { setC(await crm.getServiceContract(id)) } catch (e) { onErr(e.response?.data?.message ?? 'Failed to load.') } }, [id])
  useEffect(() => { reload() }, [reload])
  const act = async (fn, ...args) => { setBusy(true); try { await fn(id, ...args); await reload(); onChanged() } catch (e) { onErr(e.response?.data?.message ?? 'Action failed.') } finally { setBusy(false) } }
  const doRenew = async () => {
    if (!renew.newEndDate) return onErr('New end date required.')
    await act(crm.renewServiceContract, { newEndDate: renew.newEndDate, newValue: renew.newValue ? Number(renew.newValue) : undefined })
    setRenew({ open: false, newEndDate: '', newValue: '' })
  }
  if (!c) return <Modal title="Service Contract" onClose={onClose} width={520}><p style={{ color: T.mgrey, fontSize: 13 }}>Loading…</p></Modal>
  return (
    <Modal title={c.contractNumber} onClose={onClose} width={540}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
        <Badge variant={CONTRACT_STATUS_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>
        <span style={{ fontSize: 13, color: T.mgrey }}>{c.contractType} · {c.customerName}</span>
      </div>
      {c.description && <p style={{ fontSize: 13, margin: '0 0 12px' }}>{c.description}</p>}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(130px,1fr))', gap: 12, marginBottom: 16 }}>
        <Stat label="Value" value={fmtKes(c.value)} />
        <Stat label="Period" value={`${fmtDate(c.startDate)} – ${fmtDate(c.endDate)}`} />
        <Stat label="Days to expiry" value={c.status === 'Active' ? `${c.daysToExpiry}d` : '—'} />
        <Stat label="Billing" value={c.billingFrequency ?? '—'} />
        <Stat label="Auto-renew" value={c.autoRenew ? 'Yes' : 'No'} />
      </div>
      {canWrite && c.status === 'Active' && (
        <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14 }}>
          {!renew.open ? (
            <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" onClick={() => setRenew(r => ({ ...r, open: true }))} disabled={busy}>Renew</Btn>
              <Btn size="sm" variant="danger" onClick={() => { if (window.confirm('Cancel this contract?')) act(crm.cancelServiceContract) }} disabled={busy}>Cancel Contract</Btn>
            </div>
          ) : (
            <div>
              <div style={{ display: 'flex', gap: 10 }}>
                <Field label="New end date *" flex><input type="date" value={renew.newEndDate} onChange={e => setRenew(r => ({ ...r, newEndDate: e.target.value }))} className="input" /></Field>
                <Field label="New value (optional)" flex><input type="number" value={renew.newValue} onChange={e => setRenew(r => ({ ...r, newValue: e.target.value }))} className="input" /></Field>
              </div>
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                <Btn size="sm" variant="ghost" onClick={() => setRenew({ open: false, newEndDate: '', newValue: '' })}>Back</Btn>
                <Btn size="sm" onClick={doRenew} disabled={busy}>Confirm Renewal</Btn>
              </div>
            </div>
          )}
        </div>
      )}
    </Modal>
  )
}

/* ── Complaints ── */
function RaiseComplaintModal({ onClose, onSaved, onErr }) {
  const [f, setF] = useState({ customerId: '', subject: '', description: '', category: '', severity: 'Medium' })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.customerId) return onErr('Select a customer.')
    if (!f.subject.trim()) return onErr('Subject is required.')
    setBusy(true)
    try { await crm.raiseComplaint({ ...f, subject: f.subject.trim(), description: f.description || undefined, category: f.category || undefined }); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title="Raise Complaint" onClose={onClose} width={480}>
      <Field label="Customer *"><CustomerSelect value={f.customerId} onChange={v => upd('customerId', v)} /></Field>
      <Field label="Subject *"><input value={f.subject} onChange={e => upd('subject', e.target.value)} className="input" /></Field>
      <Field label="Description"><textarea value={f.description} onChange={e => upd('description', e.target.value)} className="input" rows={2} /></Field>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Category" flex><input value={f.category} onChange={e => upd('category', e.target.value)} className="input" /></Field>
        <Field label="Severity" flex><select value={f.severity} onChange={e => upd('severity', e.target.value)} className="input">{COMPLAINT_SEVERITIES.map(sv => <option key={sv} value={sv}>{sv}</option>)}</select></Field>
      </div>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Raise'}</Btn></div>
    </Modal>
  )
}
function ComplaintDetailModal({ id, canWrite, onClose, onChanged, onErr }) {
  const [c, setC] = useState(null)
  const [busy, setBusy] = useState(false)
  const [assign, setAssign] = useState({ open: false, assignedTo: '', assignedToName: '' })
  const [resolution, setResolution] = useState('')
  const reload = useCallback(async () => {
    try { const list = await crm.listComplaints(); setC((list ?? []).find(x => x.id === id) ?? null) }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed to load.') }
  }, [id])
  useEffect(() => { reload() }, [reload])
  const act = async (fn, ...args) => { setBusy(true); try { await fn(id, ...args); await reload(); onChanged() } catch (e) { onErr(e.response?.data?.message ?? 'Action failed.') } finally { setBusy(false) } }
  if (!c) return <Modal title="Complaint" onClose={onClose} width={520}><p style={{ color: T.mgrey, fontSize: 13 }}>Loading…</p></Modal>
  return (
    <Modal title={c.complaintNumber} onClose={onClose} width={540}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
        <Badge variant={COMPLAINT_STATUS_VARIANT[c.status] ?? 'default'}>{complaintStatusLabel(c.status)}</Badge>
        <Badge variant={SEVERITY_VARIANT[c.severity] ?? 'default'}>{c.severity}</Badge>
        <span style={{ fontSize: 13, color: T.mgrey }}>{c.customerName}</span>
      </div>
      <h3 style={{ fontSize: 15, fontWeight: 700, color: T.navy, margin: '0 0 6px' }}>{c.subject}</h3>
      {c.description && <p style={{ fontSize: 13, margin: '0 0 12px' }}>{c.description}</p>}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(130px,1fr))', gap: 12, marginBottom: 16 }}>
        <Stat label="Assigned to" value={c.assignedToName ?? c.assignedTo ?? '—'} />
        <Stat label="Raised" value={fmtDate(c.raisedAt)} />
        <Stat label="Resolved" value={fmtDate(c.resolvedAt)} />
      </div>
      {c.resolution && <p style={{ fontSize: 13, background: T.offwt ?? '#f5f5f5', padding: 10, borderRadius: 6, margin: '0 0 12px' }}><strong>Resolution:</strong> {c.resolution}</p>}

      {canWrite && c.status !== 'Closed' && (
        <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14 }}>
          {c.status === 'Open' && !assign.open && <Btn size="sm" onClick={() => setAssign(a => ({ ...a, open: true }))} disabled={busy}>Assign</Btn>}
          {c.status === 'Open' && assign.open && (
            <div>
              <div style={{ display: 'flex', gap: 10 }}>
                <Field label="Assignee id *" flex><input value={assign.assignedTo} onChange={e => setAssign(a => ({ ...a, assignedTo: e.target.value }))} className="input" /></Field>
                <Field label="Assignee name" flex><input value={assign.assignedToName} onChange={e => setAssign(a => ({ ...a, assignedToName: e.target.value }))} className="input" /></Field>
              </div>
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                <Btn size="sm" variant="ghost" onClick={() => setAssign({ open: false, assignedTo: '', assignedToName: '' })}>Back</Btn>
                <Btn size="sm" onClick={() => { if (!assign.assignedTo.trim()) return onErr('Assignee id required.'); act(crm.assignComplaint, { assignedTo: assign.assignedTo, assignedToName: assign.assignedToName || undefined }) }} disabled={busy}>Assign</Btn>
              </div>
            </div>
          )}
          {c.status === 'Assigned' && <Btn size="sm" onClick={() => act(crm.startComplaint)} disabled={busy}>Start Work</Btn>}
          {c.status === 'InProgress' && (
            <div>
              <Field label="Resolution *"><textarea value={resolution} onChange={e => setResolution(e.target.value)} className="input" rows={2} /></Field>
              <div style={{ display: 'flex', justifyContent: 'flex-end' }}><Btn size="sm" onClick={() => { if (!resolution.trim()) return onErr('Resolution required.'); act(crm.resolveComplaint, { resolution }) }} disabled={busy}>Resolve</Btn></div>
            </div>
          )}
          {c.status === 'Resolved' && <Btn size="sm" onClick={() => act(crm.closeComplaint)} disabled={busy}>Close</Btn>}
        </div>
      )}
    </Modal>
  )
}

/* ── NPS ── */
function SendNpsModal({ onClose, onSaved, onErr }) {
  const [f, setF] = useState({ customerId: '', year: new Date().getFullYear() })
  const [busy, setBusy] = useState(false)
  const save = async () => {
    if (!f.customerId) return onErr('Select a customer.')
    setBusy(true)
    try { await crm.sendNps({ customerId: f.customerId, year: Number(f.year) || undefined }); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title="Send NPS Survey" onClose={onClose} width={420}>
      <Field label="Customer *"><CustomerSelect value={f.customerId} onChange={v => setF(s => ({ ...s, customerId: v }))} /></Field>
      <Field label="Year"><input type="number" value={f.year} onChange={e => setF(s => ({ ...s, year: e.target.value }))} className="input" /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Sending…' : 'Send'}</Btn></div>
    </Modal>
  )
}
function RespondNpsModal({ item, onClose, onSaved, onErr }) {
  const [score, setScore] = useState(9)
  const [feedback, setFeedback] = useState('')
  const [busy, setBusy] = useState(false)
  const cat = score >= 9 ? 'Promoter' : score >= 7 ? 'Passive' : 'Detractor'
  const catColor = score >= 9 ? T.green : score >= 7 ? T.gold : T.red
  const save = async () => {
    setBusy(true)
    try { await crm.respondNps(item.id, { score: Number(score), feedback: feedback || undefined }); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title="Record NPS Response" onClose={onClose} width={440}>
      <p style={{ fontSize: 13, color: T.mgrey, margin: '0 0 12px' }}>{item.customerName} · {item.year}</p>
      <Field label="How likely to recommend? (0–10)"><input type="range" min={0} max={10} value={score} onChange={e => setScore(Number(e.target.value))} style={{ width: '100%' }} /></Field>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <strong style={{ fontSize: 24, color: T.navy }}>{score}</strong>
        <Badge variant={cat === 'Promoter' ? 'green' : cat === 'Passive' ? 'amber' : 'red'}><span style={{ color: catColor }}>{cat}</span></Badge>
      </div>
      <Field label="Feedback"><textarea value={feedback} onChange={e => setFeedback(e.target.value)} className="input" rows={2} /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Save'}</Btn></div>
    </Modal>
  )
}
