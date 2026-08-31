import { useState, useEffect } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Kpi, KPI_GRID, Modal } from '../../components/ui.jsx'
import * as crm from '../../services/crm.js'
import {
  useLegal, NDA_STATUS_VARIANT, FRAMEWORK_STATUS_VARIANT, SUBCONTRACT_STATUS_VARIANT,
  CARRIER_STATUS_VARIANT, VETTING_VARIANT,
} from '../../hooks/crm/useLegal.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const fmtKes = (n) => (n == null ? '—' : new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n))
const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const todayISO = () => new Date().toISOString().slice(0, 10)

function ExpiryTag({ days }) {
  if (days == null) return <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>
  const color = days < 0 ? T.red : days <= 30 ? T.red : days <= 60 ? T.gold : T.green
  return <span style={{ color, fontWeight: 600, fontSize: 12, whiteSpace: 'nowrap' }}>{days < 0 ? 'expired' : `${days}d`}</span>
}

export default function LegalPage() {
  const L = useLegal()
  const [tab, setTab] = useState('overview')
  const [modal, setModal] = useState(null)
  const [toast, setToast] = useState('')
  const flash = (x) => { setToast(x); setTimeout(() => setToast(''), 3000) }
  const s = L.summary
  const close = () => setModal(null)
  const done = (msg) => { close(); flash(msg); L.reload() }

  const act = async (fn, id, okMsg) => { try { await fn(id); flash(okMsg); L.reload() } catch (e) { flash(e.response?.data?.message ?? 'Failed.') } }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>Legal &amp; Contract Register</h1>
            <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>NDAs, framework &amp; subcontractor agreements, carrier vetting</p>
          </div>
          <Btn size="sm" variant="ghost" onClick={L.reload} disabled={L.loading}>{L.loading ? 'Refreshing…' : '↻ Refresh'}</Btn>
        </div>
        {L.error && <Card style={{ marginBottom: 16, borderColor: T.red }}><span style={{ color: T.red, fontSize: 13 }}>{L.error}</span></Card>}

        <Tabs tabs={[
          { id: 'overview', label: 'Overview' },
          { id: 'ndas', label: 'NDAs' },
          { id: 'frameworks', label: 'Framework' },
          { id: 'subcontracts', label: 'Subcontractors' },
          { id: 'carriers', label: 'Carriers' },
        ]} active={tab} setActive={setTab} />

        {tab === 'overview' && (
          <div>
            <div style={{ ...KPI_GRID, marginBottom: 20 }}>
              <Kpi label="Active NDAs" value={L.loading ? '…' : (s?.activeNdas ?? 0)} sub={`${s?.ndasExpiringSoon ?? 0} expiring ≤60d`} icon="🔒" />
              <Kpi label="Framework Agreements" value={L.loading ? '…' : (s?.activeFrameworks ?? 0)} sub={`${s?.reviewsDue ?? 0} reviews due`} icon="📜" variant="blue" />
              <Kpi label="Subcontractors" value={L.loading ? '…' : (s?.activeSubcontracts ?? 0)} sub={`${s?.subcontractInsuranceExpiring ?? 0} insurance expiring`} icon="🧰" variant={s?.subcontractInsuranceExpiring > 0 ? 'amber' : 'green'} />
              <Kpi label="Carriers" value={L.loading ? '…' : (s?.carriers ?? 0)} sub={`${s?.carriersPendingVetting ?? 0} pending vetting`} icon="🚚" variant={s?.carriersPendingVetting > 0 ? 'amber' : 'green'} />
            </div>
            <Card>
              <SectionHeader title="Compliance Watchlist" sub="Documents needing attention" />
              <div style={{ display: 'grid', gap: 10 }}>
                <RiskLine label="NDAs expiring (≤60d)" value={s?.ndasExpiringSoon} />
                <RiskLine label="Framework reviews due" value={s?.reviewsDue} />
                <RiskLine label="Frameworks expiring (≤60d)" value={s?.frameworksExpiringSoon} />
                <RiskLine label="Subcontractor insurance expiring" value={s?.subcontractInsuranceExpiring} />
                <RiskLine label="Carriers pending vetting" value={s?.carriersPendingVetting} />
                <RiskLine label="Carrier compliance expiring (≤30d)" value={s?.carrierComplianceExpiring} />
              </div>
            </Card>
          </div>
        )}

        {tab === 'ndas' && (
          <div>
            <Bar canWrite={L.canWrite} label="+ New NDA" onClick={() => setModal({ kind: 'nda' })} />
            <Card>
              <DataTable headers={['NDA #', 'Counterparty', 'Signed', 'Expiry', 'Countdown', 'Status', L.canWrite ? '' : null].filter(x => x !== null)}
                empty={L.loading ? 'Loading…' : 'No NDAs registered.'}
                rows={L.ndas.map(n => [
                  n.ndaNumber, n.counterpartyName, fmtDate(n.signedDate), fmtDate(n.expiryDate),
                  <ExpiryTag days={n.daysToExpiry} />, <Badge variant={NDA_STATUS_VARIANT[n.status] ?? 'default'}>{n.status}</Badge>,
                  ...(L.canWrite ? [<RowActions>
                    <Btn size="sm" variant="ghost" onClick={() => setModal({ kind: 'nda', item: n })}>Edit</Btn>
                    {n.status === 'Active' && <Btn size="sm" variant="danger" onClick={() => { if (window.confirm('Terminate NDA?')) act(crm.terminateNda, n.id, 'NDA terminated.') }}>Terminate</Btn>}
                  </RowActions>] : []),
                ])} />
            </Card>
          </div>
        )}

        {tab === 'frameworks' && (
          <div>
            <Bar canWrite={L.canWrite} label="+ New Agreement" onClick={() => setModal({ kind: 'framework' })} />
            <Card>
              <DataTable headers={['Ref', 'Counterparty', 'Title', 'Period', 'Review', 'Value', 'Status', L.canWrite ? '' : null].filter(x => x !== null)}
                empty={L.loading ? 'Loading…' : 'No framework agreements.'}
                rows={L.frameworks.map(f => [
                  f.agreementNumber, f.counterpartyName, f.title,
                  <span style={{ fontSize: 12, whiteSpace: 'nowrap' }}>{fmtDate(f.startDate)} – {fmtDate(f.endDate)}</span>,
                  fmtDate(f.performanceReviewDate), fmtKes(f.value),
                  <Badge variant={FRAMEWORK_STATUS_VARIANT[f.status] ?? 'default'}>{f.status}</Badge>,
                  ...(L.canWrite ? [<RowActions>
                    <Btn size="sm" variant="ghost" onClick={() => setModal({ kind: 'framework', item: f })}>Edit</Btn>
                    {(f.status === 'Active' || f.status === 'UnderReview') && <Btn size="sm" variant="danger" onClick={() => { if (window.confirm('Terminate agreement?')) act(crm.terminateFramework, f.id, 'Agreement terminated.') }}>Terminate</Btn>}
                  </RowActions>] : []),
                ])} />
            </Card>
          </div>
        )}

        {tab === 'subcontracts' && (
          <div>
            <Bar canWrite={L.canWrite} label="+ New Subcontract" onClick={() => setModal({ kind: 'subcontract' })} />
            <Card>
              <DataTable headers={['Ref', 'Subcontractor', 'Project', 'Period', 'Insurance', 'Value', 'Status', L.canWrite ? '' : null].filter(x => x !== null)}
                empty={L.loading ? 'Loading…' : 'No subcontractor agreements.'}
                rows={L.subcontracts.map(s2 => [
                  s2.agreementNumber, s2.subcontractorName, s2.projectId ?? '—',
                  <span style={{ fontSize: 12, whiteSpace: 'nowrap' }}>{fmtDate(s2.startDate)} – {fmtDate(s2.endDate)}</span>,
                  s2.insuranceExpiryDate ? <span style={{ color: s2.insuranceExpired ? T.red : undefined, fontSize: 12 }}>{fmtDate(s2.insuranceExpiryDate)}{s2.insuranceExpired ? ' ⚠' : ''}</span> : '—',
                  fmtKes(s2.value), <Badge variant={SUBCONTRACT_STATUS_VARIANT[s2.status] ?? 'default'}>{s2.status}</Badge>,
                  ...(L.canWrite ? [<RowActions>
                    <Btn size="sm" variant="ghost" onClick={() => setModal({ kind: 'subcontract', item: s2 })}>Edit</Btn>
                    {s2.status === 'Active' && <Btn size="sm" variant="danger" onClick={() => { if (window.confirm('Terminate agreement?')) act(crm.terminateSubcontract, s2.id, 'Agreement terminated.') }}>Terminate</Btn>}
                  </RowActions>] : []),
                ])} />
            </Card>
          </div>
        )}

        {tab === 'carriers' && (
          <div>
            <Bar canWrite={L.canWrite} label="+ New Carrier" onClick={() => setModal({ kind: 'carrier' })} />
            <Card>
              <DataTable headers={['Ref', 'Carrier', 'NTSA Expiry', 'Insurance', 'Inspection', 'Vetting', 'Usable', 'Status']}
                empty={L.loading ? 'Loading…' : 'No carriers registered.'}
                rows={L.carriers.map(c => [
                  <button onClick={() => setModal({ kind: 'carrier-detail', id: c.id })} style={{ fontWeight: 600, color: T.navy, background: 'none', border: 0, cursor: 'pointer', padding: 0 }}>{c.agreementNumber}</button>,
                  c.carrierName,
                  <span style={{ fontSize: 12 }}>{fmtDate(c.ntsaLicenceExpiry)}</span>,
                  <span style={{ fontSize: 12 }}>{fmtDate(c.goodsInTransitInsuranceExpiry)}</span>,
                  <span style={{ fontSize: 12 }}>{fmtDate(c.vehicleInspectionExpiry)}</span>,
                  <Badge variant={VETTING_VARIANT[c.vettingStatus] ?? 'default'}>{c.vettingStatus}</Badge>,
                  c.isUsable ? <Badge variant="green">Yes</Badge> : <Badge variant="red">No</Badge>,
                  <Badge variant={CARRIER_STATUS_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>,
                ])} />
            </Card>
          </div>
        )}
      </div>

      {modal?.kind === 'nda' && <NdaModal item={modal.item} onClose={close} onSaved={() => done(modal.item ? 'NDA updated.' : 'NDA registered.')} onErr={flash} />}
      {modal?.kind === 'framework' && <FrameworkModal item={modal.item} onClose={close} onSaved={() => done(modal.item ? 'Agreement updated.' : 'Agreement registered.')} onErr={flash} />}
      {modal?.kind === 'subcontract' && <SubcontractModal item={modal.item} onClose={close} onSaved={() => done(modal.item ? 'Agreement updated.' : 'Agreement registered.')} onErr={flash} />}
      {modal?.kind === 'carrier' && <CarrierModal onClose={close} onSaved={() => done('Carrier registered.')} onErr={flash} />}
      {modal?.kind === 'carrier-detail' && <CarrierDetailModal id={modal.id} canWrite={L.canWrite} onClose={close} onChanged={L.reload} onErr={flash} />}
    </>
  )
}

/* ── shared ── */
function Bar({ canWrite, label, onClick }) {
  if (!canWrite) return null
  return <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}><Btn size="sm" onClick={onClick}>{label}</Btn></div>
}
function RowActions({ children }) { return <div style={{ display: 'flex', gap: 6 }}>{children}</div> }
function RiskLine({ label, value }) {
  const v = value ?? 0
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <span style={{ fontSize: 13, color: T.mgrey }}>{label}</span>
      <strong style={{ fontSize: 16, color: v > 0 ? T.red : T.green }}>{v}</strong>
    </div>
  )
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
function CustomerSelect({ value, onChange }) {
  const [opts, setOpts] = useState([])
  useEffect(() => { crm.listCustomers({ pageSize: 200 }).then(r => setOpts(r.data ?? [])).catch(() => setOpts([])) }, [])
  return (
    <select value={value} onChange={e => onChange(e.target.value)} className="input">
      <option value="">— none / external —</option>
      {opts.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
    </select>
  )
}

/* ── NDA ── */
function NdaModal({ item, onClose, onSaved, onErr }) {
  const [f, setF] = useState(item
    ? { counterpartyName: item.counterpartyName, customerId: item.customerId ?? '', purpose: item.purpose ?? '', signedDate: item.signedDate?.slice(0, 10) ?? todayISO(), expiryDate: item.expiryDate?.slice(0, 10) ?? '', fileUrl: item.fileUrl ?? '' }
    : { counterpartyName: '', customerId: '', purpose: '', signedDate: todayISO(), expiryDate: '', fileUrl: '' })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.counterpartyName.trim()) return onErr('Counterparty name is required.')
    setBusy(true)
    const dto = { ...f, customerId: f.customerId || undefined, expiryDate: f.expiryDate || undefined, purpose: f.purpose || undefined, fileUrl: f.fileUrl || undefined }
    try { item ? await crm.updateNda(item.id, dto) : await crm.createNda(dto); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title={item ? `Edit ${item.ndaNumber}` : 'Register NDA'} onClose={onClose} width={460}>
      <Field label="Counterparty *"><input value={f.counterpartyName} onChange={e => upd('counterpartyName', e.target.value)} className="input" /></Field>
      <Field label="Linked customer (optional)"><CustomerSelect value={f.customerId} onChange={v => upd('customerId', v)} /></Field>
      <Field label="Purpose"><input value={f.purpose} onChange={e => upd('purpose', e.target.value)} className="input" /></Field>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Signed date *" flex><input type="date" value={f.signedDate} onChange={e => upd('signedDate', e.target.value)} className="input" /></Field>
        <Field label="Expiry (blank = +3yr)" flex><input type="date" value={f.expiryDate} onChange={e => upd('expiryDate', e.target.value)} className="input" /></Field>
      </div>
      <Field label="File URL"><input value={f.fileUrl} onChange={e => upd('fileUrl', e.target.value)} className="input" placeholder="https://…" /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Save'}</Btn></div>
    </Modal>
  )
}

/* ── Framework ── */
function FrameworkModal({ item, onClose, onSaved, onErr }) {
  const [f, setF] = useState(item
    ? { counterpartyName: item.counterpartyName, customerId: item.customerId ?? '', title: item.title, scope: item.scope ?? '', startDate: item.startDate?.slice(0, 10) ?? todayISO(), endDate: item.endDate?.slice(0, 10) ?? '', performanceReviewDate: item.performanceReviewDate?.slice(0, 10) ?? '', value: item.value ?? '', fileUrl: item.fileUrl ?? '' }
    : { counterpartyName: '', customerId: '', title: '', scope: '', startDate: todayISO(), endDate: '', performanceReviewDate: '', value: '', fileUrl: '' })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.counterpartyName.trim()) return onErr('Counterparty is required.')
    if (!f.title.trim()) return onErr('Title is required.')
    if (!f.endDate) return onErr('End date is required.')
    setBusy(true)
    const dto = { ...f, customerId: f.customerId || undefined, scope: f.scope || undefined, performanceReviewDate: f.performanceReviewDate || undefined, value: f.value ? Number(f.value) : undefined, fileUrl: f.fileUrl || undefined }
    try { item ? await crm.updateFramework(item.id, dto) : await crm.createFramework(dto); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title={item ? `Edit ${item.agreementNumber}` : 'Register Framework Agreement'} onClose={onClose} width={480}>
      <Field label="Counterparty *"><input value={f.counterpartyName} onChange={e => upd('counterpartyName', e.target.value)} className="input" /></Field>
      <Field label="Linked customer (optional)"><CustomerSelect value={f.customerId} onChange={v => upd('customerId', v)} /></Field>
      <Field label="Title *"><input value={f.title} onChange={e => upd('title', e.target.value)} className="input" /></Field>
      <Field label="Scope"><textarea value={f.scope} onChange={e => upd('scope', e.target.value)} className="input" rows={2} /></Field>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Start *" flex><input type="date" value={f.startDate} onChange={e => upd('startDate', e.target.value)} className="input" /></Field>
        <Field label="End *" flex><input type="date" value={f.endDate} onChange={e => upd('endDate', e.target.value)} className="input" /></Field>
      </div>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Performance review date" flex><input type="date" value={f.performanceReviewDate} onChange={e => upd('performanceReviewDate', e.target.value)} className="input" /></Field>
        <Field label="Value (KES)" flex><input type="number" value={f.value} onChange={e => upd('value', e.target.value)} className="input" /></Field>
      </div>
      <Field label="File URL"><input value={f.fileUrl} onChange={e => upd('fileUrl', e.target.value)} className="input" /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Save'}</Btn></div>
    </Modal>
  )
}

/* ── Subcontract ── */
function SubcontractModal({ item, onClose, onSaved, onErr }) {
  const [f, setF] = useState(item
    ? { subcontractorName: item.subcontractorName, projectId: item.projectId ?? '', scopeOfWork: item.scopeOfWork ?? '', startDate: item.startDate?.slice(0, 10) ?? todayISO(), endDate: item.endDate?.slice(0, 10) ?? '', value: item.value ?? '', insuranceExpiryDate: item.insuranceExpiryDate?.slice(0, 10) ?? '', fileUrl: item.fileUrl ?? '' }
    : { subcontractorName: '', projectId: '', scopeOfWork: '', startDate: todayISO(), endDate: '', value: '', insuranceExpiryDate: '', fileUrl: '' })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.subcontractorName.trim()) return onErr('Subcontractor name is required.')
    if (!f.endDate) return onErr('End date is required.')
    setBusy(true)
    const dto = { ...f, projectId: f.projectId || undefined, scopeOfWork: f.scopeOfWork || undefined, value: f.value ? Number(f.value) : undefined, insuranceExpiryDate: f.insuranceExpiryDate || undefined, fileUrl: f.fileUrl || undefined }
    try { item ? await crm.updateSubcontract(item.id, dto) : await crm.createSubcontract(dto); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title={item ? `Edit ${item.agreementNumber}` : 'Register Subcontractor'} onClose={onClose} width={480}>
      <Field label="Subcontractor name *"><input value={f.subcontractorName} onChange={e => upd('subcontractorName', e.target.value)} className="input" /></Field>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Project ref" flex><input value={f.projectId} onChange={e => upd('projectId', e.target.value)} className="input" placeholder="PRJ-…" /></Field>
        <Field label="Value (KES)" flex><input type="number" value={f.value} onChange={e => upd('value', e.target.value)} className="input" /></Field>
      </div>
      <Field label="Scope of work"><textarea value={f.scopeOfWork} onChange={e => upd('scopeOfWork', e.target.value)} className="input" rows={2} /></Field>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Start *" flex><input type="date" value={f.startDate} onChange={e => upd('startDate', e.target.value)} className="input" /></Field>
        <Field label="End *" flex><input type="date" value={f.endDate} onChange={e => upd('endDate', e.target.value)} className="input" /></Field>
      </div>
      <Field label="Insurance expiry"><input type="date" value={f.insuranceExpiryDate} onChange={e => upd('insuranceExpiryDate', e.target.value)} className="input" /></Field>
      <Field label="File URL"><input value={f.fileUrl} onChange={e => upd('fileUrl', e.target.value)} className="input" /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Save'}</Btn></div>
    </Modal>
  )
}

/* ── Carrier ── */
function CarrierModal({ onClose, onSaved, onErr }) {
  const [f, setF] = useState({ carrierName: '', ntsaLicenceNumber: '', ntsaLicenceExpiry: '', goodsInTransitInsuranceExpiry: '', vehicleInspectionExpiry: '', fileUrl: '' })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.carrierName.trim()) return onErr('Carrier name is required.')
    setBusy(true)
    const dto = { carrierName: f.carrierName, ntsaLicenceNumber: f.ntsaLicenceNumber || undefined, ntsaLicenceExpiry: f.ntsaLicenceExpiry || undefined, goodsInTransitInsuranceExpiry: f.goodsInTransitInsuranceExpiry || undefined, vehicleInspectionExpiry: f.vehicleInspectionExpiry || undefined, fileUrl: f.fileUrl || undefined }
    try { await crm.createCarrier(dto); onSaved() } catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  return (
    <Modal title="Register Carrier" onClose={onClose} width={480}>
      <Field label="Carrier name *"><input value={f.carrierName} onChange={e => upd('carrierName', e.target.value)} className="input" /></Field>
      <Field label="NTSA licence number"><input value={f.ntsaLicenceNumber} onChange={e => upd('ntsaLicenceNumber', e.target.value)} className="input" /></Field>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="NTSA expiry" flex><input type="date" value={f.ntsaLicenceExpiry} onChange={e => upd('ntsaLicenceExpiry', e.target.value)} className="input" /></Field>
        <Field label="Insurance expiry" flex><input type="date" value={f.goodsInTransitInsuranceExpiry} onChange={e => upd('goodsInTransitInsuranceExpiry', e.target.value)} className="input" /></Field>
      </div>
      <Field label="Vehicle inspection expiry"><input type="date" value={f.vehicleInspectionExpiry} onChange={e => upd('vehicleInspectionExpiry', e.target.value)} className="input" /></Field>
      <Field label="File URL"><input value={f.fileUrl} onChange={e => upd('fileUrl', e.target.value)} className="input" /></Field>
      <p style={{ fontSize: 12, color: T.mgrey }}>New carriers start as <strong>Pending vetting</strong> and cannot be used until approved with in-date compliance documents.</p>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Register'}</Btn></div>
    </Modal>
  )
}
function CarrierDetailModal({ id, canWrite, onClose, onChanged, onErr }) {
  const [c, setC] = useState(null)
  const [busy, setBusy] = useState(false)
  const [notes, setNotes] = useState('')
  const reload = async () => { try { setC(await crm.getCarrier(id)) } catch (e) { onErr(e.response?.data?.message ?? 'Failed to load.') } }
  useEffect(() => { reload() }, [id]) // eslint-disable-line react-hooks/exhaustive-deps
  const doVet = async (approve) => { setBusy(true); try { await crm.vetCarrier(id, { approve, notes: notes || undefined }); await reload(); onChanged() } catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) } }
  const doSuspend = async () => { setBusy(true); try { await crm.suspendCarrier(id); await reload(); onChanged() } catch (e) { onErr(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) } }
  if (!c) return <Modal title="Carrier" onClose={onClose} width={520}><p style={{ color: T.mgrey, fontSize: 13 }}>Loading…</p></Modal>
  return (
    <Modal title={`${c.agreementNumber} · ${c.carrierName}`} onClose={onClose} width={540}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
        <Badge variant={VETTING_VARIANT[c.vettingStatus] ?? 'default'}>{c.vettingStatus}</Badge>
        <Badge variant={CARRIER_STATUS_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>
        {c.isUsable ? <Badge variant="green">Usable</Badge> : <Badge variant="red">Not usable</Badge>}
        {c.complianceExpired && <Badge variant="red">Compliance expired</Badge>}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(150px,1fr))', gap: 12, marginBottom: 16 }}>
        <Stat label="NTSA licence" value={c.ntsaLicenceNumber ?? '—'} />
        <Stat label="NTSA expiry" value={fmtDate(c.ntsaLicenceExpiry)} />
        <Stat label="Insurance expiry" value={fmtDate(c.goodsInTransitInsuranceExpiry)} />
        <Stat label="Inspection expiry" value={fmtDate(c.vehicleInspectionExpiry)} />
      </div>
      {c.vettingNotes && <p style={{ fontSize: 13, margin: '0 0 12px' }}><strong>Vetting notes:</strong> {c.vettingNotes}</p>}
      {canWrite && (
        <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14 }}>
          {c.vettingStatus === 'Pending' && (
            <>
              <Field label="Vetting notes"><textarea value={notes} onChange={e => setNotes(e.target.value)} className="input" rows={2} /></Field>
              <div style={{ display: 'flex', gap: 8 }}>
                <Btn size="sm" variant="green" onClick={() => doVet(true)} disabled={busy}>Approve</Btn>
                <Btn size="sm" variant="danger" onClick={() => doVet(false)} disabled={busy}>Reject</Btn>
              </div>
            </>
          )}
          {c.vettingStatus === 'Approved' && c.status === 'Active' && <Btn size="sm" variant="danger" onClick={() => { if (window.confirm('Suspend carrier?')) doSuspend() }} disabled={busy}>Suspend</Btn>}
        </div>
      )}
    </Modal>
  )
}
