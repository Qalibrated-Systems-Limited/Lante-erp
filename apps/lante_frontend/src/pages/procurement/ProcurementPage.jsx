import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import * as proc from '../../services/procurement.js'

// ─────────────────────────────────────────────────────────────────────────────
// Procurement & Supply Chain (Module 4) — P1: Approved Supplier Register (ASR).
// Real, wired to procurement-service (/api/v1/procurement/*). Suppliers move
// Pending → conflict check → Approved, or are Blacklisted (MD, reason required).
// PR / Quotation / LPO / GRN tabs land with phases P2–P5.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')

const STATUS_VARIANT = { Pending: 'blue', ConflictFlagged: 'amber', Approved: 'green', Suspended: 'amber', Blacklisted: 'red' }
const DOC_TYPES = ['CertificateOfIncorporation', 'KraPinCertificate', 'TaxCompliance', 'Cr12', 'BankDetails', 'Other']
const PR_STATUS_VARIANT = { Draft: 'default', PendingDeptHead: 'amber', Approved: 'green', Rejected: 'red', Cancelled: 'default' }
const DEPARTMENTS = ['Engineering', 'Projects', 'Finance', 'HR', 'BD', 'ICT', 'Executive', 'Stores']
const emptyLine = () => ({ itemDescription: '', quantity: 1, unit: '', estimatedUnitPrice: '' })

export default function ProcurementPage() {
  const [tab, setTab] = useState('requisitions')
  const [summary, setSummary] = useState(null)
  const [toast, setToast] = useState('')
  const flash = (x) => { setToast(x); setTimeout(() => setToast(''), 3000) }

  const loadSummary = useCallback(() => { proc.asrSummary().then(setSummary).catch(() => {}) }, [])
  useEffect(() => { loadSummary() }, [loadSummary])

  const K = summary ?? {}

  return (
    <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
      {toast && <div style={{ position: 'fixed', bottom: 24, right: 24, zIndex: 1100, background: '#18181b', color: '#fff', fontSize: 13, padding: '11px 18px', borderRadius: 12, boxShadow: '0 8px 24px rgba(0,0,0,.2)' }}>{toast}</div>}

      <div style={{ marginBottom: 16 }}>
        <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>Procurement &amp; Supply Chain</h1>
        <p style={{ fontSize: 13, color: T.mgrey, marginTop: 4 }}>Approved Supplier Register — supplier onboarding, compliance, conflict-of-interest &amp; anti-bribery (PROC-001, PROC-007).</p>
      </div>

      {/* Summary KPIs */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 12, marginBottom: 20 }}>
        <Kpi label="Suppliers" value={K.totalSuppliers ?? 0} />
        <Kpi label="Approved" value={K.approved ?? 0} color={T.green} />
        <Kpi label="Pending" value={K.pending ?? 0} color={T.blue} />
        <Kpi label="Conflict Flagged" value={K.conflictFlagged ?? 0} color={T.amber} />
        <Kpi label="Blacklisted" value={K.blacklisted ?? 0} color={T.red} />
        <Kpi label="Docs Expiring ≤30d" value={K.expiringDocuments ?? 0} color={T.amber} />
        <Kpi label="Gifts This Month" value={K.giftsThisMonth ?? 0} sub={fmtKes(K.giftValueThisMonth)} />
      </div>

      <Tabs
        tabs={[
          { id: 'requisitions', label: 'Requisitions' },
          { id: 'matching', label: '3-Way Match' },
          { id: 'international', label: 'International' },
          { id: 'emergency', label: 'Emergency' },
          { id: 'performance', label: 'Performance' },
          { id: 'suppliers', label: 'Suppliers' },
          { id: 'categories', label: 'Categories' },
          { id: 'gifts', label: 'Gift Register' },
        ]}
        active={tab}
        setActive={setTab}
      />

      {tab === 'requisitions' && <RequisitionsTab flash={flash} />}
      {tab === 'matching' && <MatchingTab flash={flash} />}
      {tab === 'international' && <InternationalTab flash={flash} />}
      {tab === 'emergency' && <EmergencyTab flash={flash} />}
      {tab === 'performance' && <PerformanceTab flash={flash} />}
      {tab === 'suppliers' && <SuppliersTab flash={flash} onChange={loadSummary} />}
      {tab === 'categories' && <CategoriesTab flash={flash} />}
      {tab === 'gifts' && <GiftsTab flash={flash} onChange={loadSummary} />}
    </div>
  )
}

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 22, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

// ── Suppliers ──
function SuppliersTab({ flash, onChange }) {
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [categories, setCategories] = useState([])
  const [filter, setFilter] = useState({ search: '', status: '', approvedOnly: false })
  const [newOpen, setNewOpen] = useState(false)
  const [detailId, setDetailId] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    proc.listSuppliers({
      search: filter.search || undefined,
      status: filter.status || undefined,
      approvedOnly: filter.approvedOnly || undefined,
      pageSize: 100,
    }).then(r => setRows(r.data ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
  }, [filter])

  useEffect(() => { load() }, [load])
  useEffect(() => { proc.listCategories().then(setCategories).catch(() => {}) }, [])

  const catName = (s) => s.categoryName ?? categories.find(c => c.id === s.categoryId)?.categoryName ?? '—'

  return (
    <div>
      <SectionHeader
        title="Approved Supplier Register"
        sub="Only Approved, non-blacklisted suppliers may be used in a Purchase Order."
        action={<Btn size="sm" onClick={() => setNewOpen(true)}>+ New Supplier</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <input placeholder="Search name / KRA PIN / number…" value={filter.search}
          onChange={e => setFilter(f => ({ ...f, search: e.target.value }))}
          style={{ flex: '1 1 220px', height: 40, padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, boxSizing: 'border-box' }} />
        <select value={filter.status} onChange={e => setFilter(f => ({ ...f, status: e.target.value }))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {['Pending', 'ConflictFlagged', 'Approved', 'Suspended', 'Blacklisted'].map(s => <option key={s} value={s}>{s}</option>)}
        </select>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, color: T.dgrey }}>
          <input type="checkbox" checked={filter.approvedOnly} onChange={e => setFilter(f => ({ ...f, approvedOnly: e.target.checked }))} />
          Approved only
        </label>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['#', 'Name', 'Category', 'Status', 'Score', 'Actions']}
          empty="No suppliers registered yet."
          rows={rows.map(s => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{s.supplierNumber}</span>,
            <div><div style={{ fontWeight: 600 }}>{s.name}</div><div style={{ fontSize: 11, color: T.mgrey }}>{s.kraPin ?? '—'}</div></div>,
            catName(s),
            <Badge variant={STATUS_VARIANT[s.status] ?? 'default'}>{s.status}</Badge>,
            s.overallScore != null ? Number(s.overallScore).toFixed(0) : '—',
            <Btn size="sm" variant="outline" onClick={() => setDetailId(s.id)}>Open</Btn>,
          ])}
        />
      )}

      {newOpen && <NewSupplierModal categories={categories} onClose={() => setNewOpen(false)}
        onSaved={() => { setNewOpen(false); load(); onChange(); flash('Supplier created.') }} flash={flash} />}
      {detailId && <SupplierDetailModal id={detailId} categories={categories} onClose={() => setDetailId(null)}
        onChanged={() => { load(); onChange() }} flash={flash} />}
    </div>
  )
}

function NewSupplierModal({ categories, onClose, onSaved, flash }) {
  const [f, setF] = useState({ name: '', kraPin: '', categoryId: '', contactPerson: '', phone: '', email: '', address: '' })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.name.trim()) { flash('Name is required.'); return }
    setBusy(true)
    try { await proc.createSupplier({ ...f, categoryId: f.categoryId || undefined }); onSaved() }
    catch (e) { flash(e.response?.data?.message ?? 'Failed to create supplier.') }
    finally { setBusy(false) }
  }
  return (
    <Modal title="New Supplier" onClose={onClose} width={560}>
      <Input label="Name" value={f.name} onChange={v => upd('name', v)} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="KRA PIN" value={f.kraPin} onChange={v => upd('kraPin', v)} />
        <Select label="Category" value={f.categoryId} onChange={v => upd('categoryId', v)}
          options={[{ value: '', label: '— none —' }, ...categories.map(c => ({ value: c.id, label: c.categoryName }))]} />
        <Input label="Contact Person" value={f.contactPerson} onChange={v => upd('contactPerson', v)} />
        <Input label="Phone" value={f.phone} onChange={v => upd('phone', v)} />
        <Input label="Email" value={f.email} onChange={v => upd('email', v)} />
      </div>
      <Input label="Address" value={f.address} onChange={v => upd('address', v)} />
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 8 }}>
        <Btn variant="outline" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Create Supplier'}</Btn>
      </div>
    </Modal>
  )
}

function SupplierDetailModal({ id, categories, onClose, onChanged, flash }) {
  const [s, setS] = useState(null)
  const [docs, setDocs] = useState([])
  const [busy, setBusy] = useState(false)
  const [conflict, setConflict] = useState({ open: false, found: false, notes: '' })
  const [blk, setBlk] = useState({ open: false, reason: '' })
  const [doc, setDoc] = useState({ open: false, documentType: 'Other', documentName: '', fileUrl: '', expiryDate: '' })

  const load = useCallback(() => {
    proc.getSupplier(id).then(setS).catch(() => {})
    proc.getSupplierDocuments(id).then(setDocs).catch(() => {})
  }, [id])
  useEffect(() => { load() }, [load])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); flash(r?.message ?? ok); load(); onChanged() }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!s) return <Modal title="Supplier" onClose={onClose}><Loading /></Modal>
  const catName = categories.find(c => c.id === s.categoryId)?.categoryName ?? s.categoryName

  return (
    <Modal title={`${s.supplierNumber} — ${s.name}`} onClose={onClose} width={680}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
        <Badge variant={STATUS_VARIANT[s.status] ?? 'default'}>{s.status}</Badge>
        {s.isApproved && <span style={{ fontSize: 12, color: T.green }}>● approved</span>}
        {s.blacklistFlag && <span style={{ fontSize: 12, color: T.red }}>● blacklisted</span>}
        {s.conflictChecked && <span style={{ fontSize: 12, color: s.conflictFound ? T.red : T.green }}>● conflict {s.conflictFound ? 'flagged' : 'cleared'}</span>}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px 18px', fontSize: 13, marginBottom: 14 }}>
        <Row k="Category" v={catName ?? '—'} />
        <Row k="KRA PIN" v={s.kraPin ?? '—'} />
        <Row k="Contact" v={s.contactPerson ?? '—'} />
        <Row k="Phone" v={s.phone ?? '—'} />
        <Row k="Email" v={s.email ?? '—'} />
        <Row k="Address" v={s.address ?? '—'} />
        {s.blacklistFlag && <Row k="Blacklist reason" v={s.blacklistReason ?? '—'} />}
        {s.conflictFound && <Row k="Conflict notes" v={s.conflictNotes ?? '—'} />}
        {s.overallScore != null && <Row k="Score" v={Number(s.overallScore).toFixed(0)} />}
      </div>

      {/* Workflow actions */}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', paddingTop: 12, borderTop: `1px solid ${T.lgrey}` }}>
        {!s.blacklistFlag && <Btn size="sm" variant="outline" onClick={() => setConflict({ open: true, found: false, notes: '' })} disabled={busy}>Conflict Check</Btn>}
        {!s.isApproved && !s.blacklistFlag && <Btn size="sm" variant="green" onClick={() => run(() => proc.approveSupplier(id), 'Approved.')} disabled={busy}>Approve</Btn>}
        {!s.blacklistFlag && <Btn size="sm" variant="danger" onClick={() => setBlk({ open: true, reason: '' })} disabled={busy}>Blacklist</Btn>}
        {s.blacklistFlag && <Btn size="sm" variant="outline" onClick={() => run(() => proc.reinstateSupplier(id), 'Reinstated.')} disabled={busy}>Reinstate</Btn>}
        <Btn size="sm" variant="outline" onClick={() => setDoc(d => ({ ...d, open: true }))} disabled={busy}>+ Document</Btn>
      </div>

      {/* Documents */}
      <div style={{ marginTop: 16 }}>
        <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, marginBottom: 8 }}>Compliance Documents</h4>
        {docs.length === 0 ? <p style={{ fontSize: 12, color: T.mgrey }}>No documents uploaded.</p> : (
          <DataTable
            headers={['Type', 'Name', 'Expiry', 'Verified', 'Actions']}
            rows={docs.map(d => [
              d.documentType,
              d.documentName ?? '—',
              fmtDate(d.expiryDate),
              d.verifiedAt ? <span style={{ color: T.green }}>✓</span> : '—',
              d.verifiedAt ? '—' : <Btn size="sm" variant="outline" onClick={() => run(() => proc.verifySupplierDocument(d.id), 'Verified.')} disabled={busy}>Verify</Btn>,
            ])}
          />
        )}
      </div>

      {/* Inline sub-forms */}
      {conflict.open && (
        <div style={{ marginTop: 14, padding: 12, border: `1px solid ${T.lgrey}`, borderRadius: 8 }}>
          <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 13, marginBottom: 8 }}>
            <input type="checkbox" checked={conflict.found} onChange={e => setConflict(c => ({ ...c, found: e.target.checked }))} />
            Conflict of interest found (company staff link)
          </label>
          <Input label="Notes" value={conflict.notes} onChange={v => setConflict(c => ({ ...c, notes: v }))} />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Btn size="sm" variant="outline" onClick={() => setConflict({ open: false, found: false, notes: '' })}>Cancel</Btn>
            <Btn size="sm" onClick={() => { run(() => proc.runConflictCheck(id, { conflictFound: conflict.found, notes: conflict.notes }), 'Conflict check recorded.'); setConflict({ open: false, found: false, notes: '' }) }} disabled={busy}>Record</Btn>
          </div>
        </div>
      )}
      {blk.open && (
        <div style={{ marginTop: 14, padding: 12, border: `1px solid ${T.redL}`, borderRadius: 8 }}>
          <Input label="Blacklist reason (mandatory)" value={blk.reason} onChange={v => setBlk(b => ({ ...b, reason: v }))} required />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Btn size="sm" variant="outline" onClick={() => setBlk({ open: false, reason: '' })}>Cancel</Btn>
            <Btn size="sm" variant="danger" onClick={() => { run(() => proc.blacklistSupplier(id, { reason: blk.reason }), 'Blacklisted.'); setBlk({ open: false, reason: '' }) }} disabled={busy || !blk.reason.trim()}>Blacklist</Btn>
          </div>
        </div>
      )}
      {doc.open && (
        <div style={{ marginTop: 14, padding: 12, border: `1px solid ${T.lgrey}`, borderRadius: 8 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <Select label="Type" value={doc.documentType} onChange={v => setDoc(d => ({ ...d, documentType: v }))} options={DOC_TYPES} />
            <Input label="Name" value={doc.documentName} onChange={v => setDoc(d => ({ ...d, documentName: v }))} />
            <Input label="File URL" value={doc.fileUrl} onChange={v => setDoc(d => ({ ...d, fileUrl: v }))} />
            <Input label="Expiry" type="date" value={doc.expiryDate} onChange={v => setDoc(d => ({ ...d, expiryDate: v }))} />
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Btn size="sm" variant="outline" onClick={() => setDoc(d => ({ ...d, open: false }))}>Cancel</Btn>
            <Btn size="sm" onClick={() => { run(() => proc.uploadSupplierDocument(id, { documentType: doc.documentType, documentName: doc.documentName || undefined, fileUrl: doc.fileUrl, expiryDate: doc.expiryDate || undefined }), 'Document uploaded.'); setDoc({ open: false, documentType: 'Other', documentName: '', fileUrl: '', expiryDate: '' }) }} disabled={busy || !doc.fileUrl.trim()}>Upload</Btn>
          </div>
        </div>
      )}
    </Modal>
  )
}

function Row({ k, v }) {
  return <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}><span style={{ color: T.mgrey }}>{k}</span><span style={{ fontWeight: 500, color: T.dgrey, textAlign: 'right' }}>{v}</span></div>
}

// ── Categories ──
function CategoriesTab({ flash }) {
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [edit, setEdit] = useState(null)   // { id?, categoryName, minScoreThreshold, description }

  const load = useCallback(() => {
    setLoading(true)
    proc.listCategories().then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
  }, [])
  useEffect(() => { load() }, [load])

  const save = async () => {
    if (!edit.categoryName?.trim()) { flash('Category name is required.'); return }
    try {
      const dto = { categoryName: edit.categoryName, minScoreThreshold: edit.minScoreThreshold ? Number(edit.minScoreThreshold) : undefined, description: edit.description || undefined }
      if (edit.id) await proc.updateCategory(edit.id, dto); else await proc.createCategory(dto)
      setEdit(null); load(); flash('Category saved.')
    } catch (e) { flash(e.response?.data?.message ?? 'Failed.') }
  }

  return (
    <div>
      <SectionHeader title="Supplier Categories" sub="Classification + the minimum performance score a supplier must hold to stay approved."
        action={<Btn size="sm" onClick={() => setEdit({ categoryName: '', minScoreThreshold: 60, description: '' })}>+ New Category</Btn>} />
      {loading ? <Loading /> : (
        <DataTable
          headers={['Category', 'Min Score', 'Description', 'Actions']}
          empty="No categories yet."
          rows={rows.map(c => [
            <span style={{ fontWeight: 600 }}>{c.categoryName}</span>,
            Number(c.minScoreThreshold).toFixed(0),
            c.description ?? '—',
            <Btn size="sm" variant="outline" onClick={() => setEdit({ id: c.id, categoryName: c.categoryName, minScoreThreshold: c.minScoreThreshold, description: c.description ?? '' })}>Edit</Btn>,
          ])}
        />
      )}
      {edit && (
        <Modal title={edit.id ? 'Edit Category' : 'New Category'} onClose={() => setEdit(null)}>
          <Input label="Category name" value={edit.categoryName} onChange={v => setEdit(s => ({ ...s, categoryName: v }))} required />
          <Input label="Min score threshold (0–100)" type="number" value={edit.minScoreThreshold} onChange={v => setEdit(s => ({ ...s, minScoreThreshold: v }))} />
          <Input label="Description" value={edit.description} onChange={v => setEdit(s => ({ ...s, description: v }))} />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 8 }}>
            <Btn variant="outline" onClick={() => setEdit(null)}>Cancel</Btn>
            <Btn onClick={save}>Save</Btn>
          </div>
        </Modal>
      )}
    </div>
  )
}

// ── Gifts ──
function GiftsTab({ flash, onChange }) {
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [suppliers, setSuppliers] = useState([])
  const [open, setOpen] = useState(false)
  const [f, setF] = useState({ supplierId: '', giftDescription: '', estimatedValue: '', receivedByName: '' })

  const load = useCallback(() => {
    setLoading(true)
    proc.listGifts().then(r => setRows(r ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
  }, [])
  useEffect(() => { load() }, [load])
  useEffect(() => { proc.listSuppliers({ pageSize: 100 }).then(r => setSuppliers(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    if (!f.supplierId) { flash('Select a supplier.'); return }
    if (!f.giftDescription.trim()) { flash('Describe the gift.'); return }
    try {
      await proc.declareGift({ supplierId: f.supplierId, giftDescription: f.giftDescription, estimatedValue: Number(f.estimatedValue) || 0, receivedByName: f.receivedByName || undefined })
      setOpen(false); setF({ supplierId: '', giftDescription: '', estimatedValue: '', receivedByName: '' }); load(); onChange(); flash('Gift declared.')
    } catch (e) { flash(e.response?.data?.message ?? 'Failed.') }
  }

  return (
    <div>
      <SectionHeader title="Gift &amp; Hospitality Register" sub="PROC-007 anti-bribery — every gift received from a supplier must be declared."
        action={<Btn size="sm" onClick={() => setOpen(true)}>+ Declare Gift</Btn>} />
      {loading ? <Loading /> : (
        <DataTable
          headers={['Date', 'Supplier', 'Description', 'Est. Value', 'Received By']}
          empty="No gifts declared."
          rows={rows.map(g => [
            fmtDate(g.declaredAt),
            g.supplierName ?? '—',
            g.giftDescription,
            fmtKes(g.estimatedValue),
            g.receivedByName ?? g.receivedBy,
          ])}
        />
      )}
      {open && (
        <Modal title="Declare Gift / Hospitality" onClose={() => setOpen(false)}>
          <Select label="Supplier" value={f.supplierId} onChange={v => setF(s => ({ ...s, supplierId: v }))}
            options={[{ value: '', label: '— select —' }, ...suppliers.map(s => ({ value: s.id, label: `${s.supplierNumber} — ${s.name}` }))]} required />
          <Input label="Gift description" value={f.giftDescription} onChange={v => setF(s => ({ ...s, giftDescription: v }))} required />
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <Input label="Estimated value (KES)" type="number" value={f.estimatedValue} onChange={v => setF(s => ({ ...s, estimatedValue: v }))} />
            <Input label="Received by (name)" value={f.receivedByName} onChange={v => setF(s => ({ ...s, receivedByName: v }))} />
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 8 }}>
            <Btn variant="outline" onClick={() => setOpen(false)}>Cancel</Btn>
            <Btn onClick={save}>Declare</Btn>
          </div>
        </Modal>
      )}
    </div>
  )
}

// ── Requisitions (P2) ──
function RequisitionsTab({ flash }) {
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [sum, setSum] = useState(null)
  const [status, setStatus] = useState('')
  const [newOpen, setNewOpen] = useState(false)
  const [detailId, setDetailId] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    proc.listRequisitions({ status: status || undefined, pageSize: 100 })
      .then(r => setRows(r.data ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    proc.prSummary().then(setSum).catch(() => {})
  }, [status])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Draft" value={sum?.draft ?? 0} />
        <Kpi label="Pending" value={sum?.pendingDeptHead ?? 0} color={T.amber} />
        <Kpi label="Approved" value={sum?.approved ?? 0} color={T.green} />
        <Kpi label="Rejected" value={sum?.rejected ?? 0} color={T.red} />
        <Kpi label="Overdue (SLA)" value={sum?.overdue ?? 0} color={T.red} />
        <Kpi label="Pending Value" value={fmtKes(sum?.pendingValue)} />
      </div>

      <SectionHeader title="Purchase Requisitions" sub="Every purchase begins with a PR. Submission runs a hard budget check; Dept-Head reviews within 2 business days."
        action={<Btn size="sm" onClick={() => setNewOpen(true)}>+ New Requisition</Btn>} />

      <div style={{ marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {['Draft', 'PendingDeptHead', 'Approved', 'Rejected', 'Cancelled'].map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['PR #', 'Department', 'Requested By', 'Total', 'Status', 'SLA', 'Actions']}
          empty="No requisitions yet."
          rows={rows.map(pr => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{pr.prNumber}</span>,
            pr.departmentId,
            pr.requestedByName ?? '—',
            fmtKes(pr.totalEstimated),
            <Badge variant={PR_STATUS_VARIANT[pr.status] ?? 'default'}>{pr.status}</Badge>,
            pr.isOverdue ? <span style={{ color: T.red, fontSize: 12 }}>overdue</span> : (pr.slaDueAt ? fmtDate(pr.slaDueAt) : '—'),
            <Btn size="sm" variant="outline" onClick={() => setDetailId(pr.id)}>Open</Btn>,
          ])}
        />
      )}

      {newOpen && <NewPrModal onClose={() => setNewOpen(false)} onSaved={() => { setNewOpen(false); load(); flash('Requisition created (draft).') }} flash={flash} />}
      {detailId && <PrDetailModal id={detailId} onClose={() => setDetailId(null)} onChanged={load} flash={flash} />}
    </div>
  )
}

function NewPrModal({ onClose, onSaved, flash }) {
  const [f, setF] = useState({ departmentId: 'Engineering', budgetName: '', justification: '' })
  const [lines, setLines] = useState([emptyLine()])
  const [busy, setBusy] = useState(false)
  const total = lines.reduce((s, l) => s + (Number(l.quantity) || 0) * (Number(l.estimatedUnitPrice) || 0), 0)

  const setLine = (i, k, v) => setLines(ls => ls.map((l, j) => j === i ? { ...l, [k]: v } : l))
  const save = async () => {
    const clean = lines.filter(l => l.itemDescription.trim())
    if (clean.length === 0) { flash('Add at least one line item.'); return }
    setBusy(true)
    try {
      await proc.createRequisition({
        departmentId: f.departmentId, budgetName: f.budgetName || undefined, justification: f.justification || undefined,
        lines: clean.map(l => ({ itemDescription: l.itemDescription, quantity: Number(l.quantity) || 0, unit: l.unit || undefined, estimatedUnitPrice: Number(l.estimatedUnitPrice) || 0 })),
      })
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Failed to create requisition.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="New Purchase Requisition" onClose={onClose} width={720}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Select label="Department" value={f.departmentId} onChange={v => setF(s => ({ ...s, departmentId: v }))} options={DEPARTMENTS} required />
        <Input label="Budget line" value={f.budgetName} onChange={v => setF(s => ({ ...s, budgetName: v }))} note="Finance budget checked on submit" />
      </div>
      <Input label="Justification" value={f.justification} onChange={v => setF(s => ({ ...s, justification: v }))} />

      <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, margin: '14px 0 8px' }}>Line Items</h4>
      {lines.map((l, i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: '3fr 1fr 1fr 1.4fr auto', gap: 8, alignItems: 'end', marginBottom: 8 }}>
          <Input label={i === 0 ? 'Item' : ''} value={l.itemDescription} onChange={v => setLine(i, 'itemDescription', v)} />
          <Input label={i === 0 ? 'Qty' : ''} type="number" value={l.quantity} onChange={v => setLine(i, 'quantity', v)} />
          <Input label={i === 0 ? 'Unit' : ''} value={l.unit} onChange={v => setLine(i, 'unit', v)} />
          <Input label={i === 0 ? 'Unit Price' : ''} type="number" value={l.estimatedUnitPrice} onChange={v => setLine(i, 'estimatedUnitPrice', v)} />
          <Btn size="sm" variant="ghost" onClick={() => setLines(ls => ls.length > 1 ? ls.filter((_, j) => j !== i) : ls)}>✕</Btn>
        </div>
      ))}
      <Btn size="sm" variant="outline" onClick={() => setLines(ls => [...ls, emptyLine()])}>+ Add line</Btn>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 14, paddingTop: 12, borderTop: `1px solid ${T.lgrey}` }}>
        <span style={{ fontWeight: 700, color: T.navy }}>Total: {fmtKes(total)}</span>
        <div style={{ display: 'flex', gap: 8 }}>
          <Btn variant="outline" onClick={onClose}>Cancel</Btn>
          <Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Create Draft'}</Btn>
        </div>
      </div>
    </Modal>
  )
}

function PrDetailModal({ id, onClose, onChanged, flash }) {
  const [pr, setPr] = useState(null)
  const [busy, setBusy] = useState(false)
  const [rej, setRej] = useState({ open: false, reason: '' })

  const load = useCallback(() => { proc.getRequisition(id).then(setPr).catch(() => {}) }, [id])
  useEffect(() => { load() }, [load])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); flash(r?.message ?? ok); load(); onChanged() }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!pr) return <Modal title="Requisition" onClose={onClose}><Loading /></Modal>

  return (
    <Modal title={`${pr.prNumber} — ${pr.departmentId}`} onClose={onClose} width={680}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
        <Badge variant={PR_STATUS_VARIANT[pr.status] ?? 'default'}>{pr.status}</Badge>
        {pr.isOverdue && <span style={{ fontSize: 12, color: T.red }}>● SLA overdue</span>}
        {pr.escalatedAt && <span style={{ fontSize: 12, color: T.red }}>● escalated to MD</span>}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px 18px', fontSize: 13, marginBottom: 12 }}>
        <Row k="Requested by" v={pr.requestedByName ?? '—'} />
        <Row k="Budget line" v={pr.budgetName ?? '—'} />
        <Row k="Submitted" v={pr.submittedAt ? fmtDate(pr.submittedAt) : '—'} />
        <Row k="SLA due" v={pr.slaDueAt ? fmtDate(pr.slaDueAt) : '—'} />
        {pr.rejectionReason && <Row k="Rejection reason" v={pr.rejectionReason} />}
      </div>
      {pr.justification && <p style={{ fontSize: 12, color: T.mgrey, marginBottom: 12 }}><b>Justification:</b> {pr.justification}</p>}

      <DataTable
        headers={['Item', 'Qty', 'Unit', 'Unit Price', 'Line Total']}
        rows={(pr.lines ?? []).map(l => [l.itemDescription, l.quantity, l.unit ?? '—', fmtKes(l.estimatedUnitPrice), fmtKes(l.lineTotal)])}
      />
      <p style={{ textAlign: 'right', fontWeight: 700, color: T.navy, marginTop: 8 }}>Total: {fmtKes(pr.totalEstimated)}</p>

      {pr.status === 'Approved' && <QuotationPanel prId={id} prTotal={pr.totalEstimated} flash={flash} />}
      {pr.status === 'Approved' && <LpoPanel prId={id} flash={flash} />}

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', paddingTop: 12, borderTop: `1px solid ${T.lgrey}` }}>
        {pr.status === 'Draft' && <Btn size="sm" onClick={() => run(() => proc.submitRequisition(id), 'Submitted.')} disabled={busy}>Submit</Btn>}
        {pr.status === 'PendingDeptHead' && <Btn size="sm" variant="green" onClick={() => run(() => proc.reviewRequisition(id, { approve: true }), 'Approved.')} disabled={busy}>Approve</Btn>}
        {pr.status === 'PendingDeptHead' && <Btn size="sm" variant="danger" onClick={() => setRej({ open: true, reason: '' })} disabled={busy}>Reject</Btn>}
        {pr.status === 'PendingDeptHead' && pr.isOverdue && !pr.escalatedAt && <Btn size="sm" variant="outline" onClick={() => run(() => proc.escalateRequisition(id), 'Escalated.')} disabled={busy}>Escalate to MD</Btn>}
      </div>

      {rej.open && (
        <div style={{ marginTop: 14, padding: 12, border: `1px solid ${T.redL}`, borderRadius: 8 }}>
          <Input label="Rejection reason (mandatory)" value={rej.reason} onChange={v => setRej(r => ({ ...r, reason: v }))} required />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Btn size="sm" variant="outline" onClick={() => setRej({ open: false, reason: '' })}>Cancel</Btn>
            <Btn size="sm" variant="danger" disabled={busy || !rej.reason.trim()}
              onClick={() => { run(() => proc.reviewRequisition(id, { approve: false, reason: rej.reason }), 'Rejected.'); setRej({ open: false, reason: '' }) }}>Reject</Btn>
          </div>
        </div>
      )}
    </Modal>
  )
}

// ── P3: Quotation & comparative analysis panel (shown on an Approved PR) ──
function QuotationPanel({ prId, prTotal, flash }) {
  const [cmp, setCmp] = useState(null)
  const [suppliers, setSuppliers] = useState([])
  const [busy, setBusy] = useState(false)
  const [rec, setRec] = useState({ open: false, supplierId: '', total: '', pdf: '' })
  const [reason, setReason] = useState('')
  const [chosen, setChosen] = useState('')
  const [scores, setScores] = useState({}) // quotationId -> {p,q,d}

  const load = useCallback(() => { proc.getComparison(prId).then(setCmp).catch(() => {}) }, [prId])
  useEffect(() => { load() }, [load])
  useEffect(() => { proc.listSuppliers({ approvedOnly: true, pageSize: 100 }).then(r => setSuppliers(r.data ?? [])).catch(() => {}) }, [])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); flash(r?.message ?? ok); load() }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!cmp) return null
  const completed = cmp.status === 'Completed'
  const enough = cmp.receivedQuotes >= cmp.requiredQuotes

  return (
    <div style={{ marginTop: 16, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8, marginBottom: 10 }}>
        <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, margin: 0 }}>Sourcing &amp; Quotations</h4>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <Badge variant={cmp.band === 'DirectLpo' ? 'green' : cmp.band === 'ThreeQuotesMd' ? 'red' : 'blue'}>{cmp.band}</Badge>
          <span style={{ fontSize: 12, color: enough ? T.green : T.amber }}>{cmp.receivedQuotes}/{cmp.requiredQuotes} quotes</span>
          {completed && <Badge variant="green">Comparison completed</Badge>}
        </div>
      </div>
      {cmp.mdApprovalRequired && <p style={{ fontSize: 11, color: T.red, marginBottom: 8 }}>Above KES 500,000 — MD approval + Board resolution required at LPO (P4).</p>}

      {cmp.quotations.length > 0 && (
        <DataTable
          headers={['Quote', 'Supplier', 'Total', 'Price', 'Quality', 'Delivery', 'Score', completed ? 'Pick' : 'Actions']}
          rows={cmp.quotations.map(q => [
            <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{q.quoteNumber}</span>,
            <span>{q.supplierName}{q.isRecommended && <span style={{ color: T.green }}> ★</span>}</span>,
            fmtKes(q.totalQuoted),
            completed ? (q.priceScore ?? '—') : <ScoreInput v={scores[q.id]?.p ?? q.priceScore} on={v => setScores(s => ({ ...s, [q.id]: { ...s[q.id], p: v } }))} />,
            completed ? (q.qualityScore ?? '—') : <ScoreInput v={scores[q.id]?.q ?? q.qualityScore} on={v => setScores(s => ({ ...s, [q.id]: { ...s[q.id], q: v } }))} />,
            completed ? (q.deliveryScore ?? '—') : <ScoreInput v={scores[q.id]?.d ?? q.deliveryScore} on={v => setScores(s => ({ ...s, [q.id]: { ...s[q.id], d: v } }))} />,
            q.totalScore != null ? Number(q.totalScore).toFixed(0) : '—',
            completed
              ? (q.isRecommended ? '✓' : '')
              : <div style={{ display: 'flex', gap: 4, justifyContent: 'flex-end', alignItems: 'center' }}>
                  <input type="radio" name={`rec-${prId}`} checked={chosen === q.id} onChange={() => setChosen(q.id)} />
                  <Btn size="sm" variant="outline" disabled={busy} onClick={() => {
                    const s = scores[q.id] ?? {}
                    run(() => proc.scoreQuotation(q.id, { priceScore: Number(s.p) || 0, qualityScore: Number(s.q) || 0, deliveryScore: Number(s.d) || 0 }), 'Scored.')
                  }}>Score</Btn>
                </div>,
          ])}
        />
      )}

      {!completed && (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 10, alignItems: 'center' }}>
          <Btn size="sm" variant="outline" disabled={busy} onClick={() => setRec(r => ({ ...r, open: !r.open }))}>+ Record Quote</Btn>
          {cmp.band !== 'DirectLpo' && (
            <input placeholder="Selection reason for recommended quote" value={reason} onChange={e => setReason(e.target.value)}
              style={{ flex: '1 1 220px', height: 34, padding: '6px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
          )}
          <Btn size="sm" variant="green" disabled={busy || !enough} onClick={() =>
            run(() => proc.completeComparison(prId, { recommendedQuotationId: cmp.band === 'DirectLpo' ? undefined : chosen, selectionReason: reason }),
              'Comparison completed.')}>Complete Comparison</Btn>
        </div>
      )}

      {rec.open && !completed && (
        <div style={{ marginTop: 12, padding: 12, border: `1px solid ${T.lgrey}`, borderRadius: 8 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 10 }}>
            <Select label="Supplier (ASR-approved)" value={rec.supplierId} onChange={v => setRec(r => ({ ...r, supplierId: v }))}
              options={[{ value: '', label: '— select —' }, ...suppliers.map(s => ({ value: s.id, label: `${s.supplierNumber} — ${s.name}` }))]} />
            <Input label="Total quoted" type="number" value={rec.total} onChange={v => setRec(r => ({ ...r, total: v }))} />
            <Input label="Quote PDF URL" value={rec.pdf} onChange={v => setRec(r => ({ ...r, pdf: v }))} />
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Btn size="sm" variant="outline" onClick={() => setRec({ open: false, supplierId: '', total: '', pdf: '' })}>Cancel</Btn>
            <Btn size="sm" disabled={busy || !rec.supplierId || !rec.total} onClick={() => {
              run(() => proc.recordQuotation(prId, { supplierId: rec.supplierId, totalQuoted: Number(rec.total) || 0, quotePdfUrl: rec.pdf || undefined, lines: [] }), 'Quote recorded.')
              setRec({ open: false, supplierId: '', total: '', pdf: '' })
            }}>Record</Btn>
          </div>
        </div>
      )}
    </div>
  )
}

function ScoreInput({ v, on }) {
  return <input type="number" value={v ?? ''} onChange={e => on(e.target.value)} placeholder="0–100"
    style={{ width: 62, height: 30, padding: '4px 6px', border: `1.5px solid ${T.lgrey}`, borderRadius: 6, fontSize: 12 }} />
}

// ── P4: LPO generation & approval panel (shown on an Approved PR) ──
const PO_STATUS_VARIANT = { Draft: 'default', PendingApproval: 'amber', Approved: 'blue', Rejected: 'red', Issued: 'green', Cancelled: 'default' }
const STEP_VARIANT = { Pending: 'amber', Approved: 'green', Rejected: 'red' }
const MATCH_VARIANT = { Pending: 'blue', Matched: 'green', Exception: 'red' }
const EX_TYPE_LABEL = {
  NotFullyReceived: 'Goods not fully received',
  NoInvoice: 'No supplier invoice',
  PriceMismatch: 'Price mismatch',
  TotalExceedsPo: 'Invoice exceeds LPO',
}

function LpoPanel({ prId, flash }) {
  const [po, setPo] = useState(null)
  const [sourcing, setSourcing] = useState(null)
  const [suppliers, setSuppliers] = useState([])
  const [busy, setBusy] = useState(false)
  const [genSupplier, setGenSupplier] = useState('')
  const [br, setBr] = useState({ ref: '', url: '' })
  const [rej, setRej] = useState({ open: false, reason: '' })
  const [loaded, setLoaded] = useState(false)

  const load = useCallback(() => {
    proc.getPoByRequisition(prId).then(setPo).catch(() => setPo(null)).finally(() => setLoaded(true))
    proc.getSourcing(prId).then(setSourcing).catch(() => {})
  }, [prId])
  useEffect(() => { load() }, [load])
  useEffect(() => { proc.listSuppliers({ approvedOnly: true, pageSize: 100 }).then(r => setSuppliers(r.data ?? [])).catch(() => {}) }, [])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); flash(r?.message ?? ok); load() }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!loaded) return null
  const isDirect = sourcing?.band === 'DirectLpo'

  return (
    <div style={{ marginTop: 16, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
      <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, marginBottom: 10 }}>Local Purchase Order (LPO)</h4>

      {!po && (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
          {isDirect && (
            <select value={genSupplier} onChange={e => setGenSupplier(e.target.value)}
              style={{ height: 36, padding: '0 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12, minWidth: 200 }}>
              <option value="">Select supplier (direct LPO)…</option>
              {suppliers.map(s => <option key={s.id} value={s.id}>{s.supplierNumber} — {s.name}</option>)}
            </select>
          )}
          <Btn size="sm" disabled={busy || (isDirect && !genSupplier)}
            onClick={() => run(() => proc.generateLpo(prId, isDirect ? { supplierId: genSupplier } : {}), 'LPO generated.')}>Generate LPO</Btn>
          <span style={{ fontSize: 11, color: T.mgrey }}>Requires a completed quotation comparison.</span>
        </div>
      )}

      {po && (
        <div>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginBottom: 10 }}>
            <span style={{ fontFamily: 'monospace', fontSize: 12, fontWeight: 700 }}>{po.poNumber}</span>
            <Badge variant={PO_STATUS_VARIANT[po.status] ?? 'default'}>{po.status}</Badge>
            <span style={{ fontSize: 12, color: T.mgrey }}>{po.supplierName} · {fmtKes(po.totalAmount)}</span>
            {po.status === 'Issued' && <span style={{ fontSize: 11, color: T.mgrey }}>● commitment {fmtKes(po.totalAmount)}</span>}
          </div>

          {/* Approval chain */}
          <DataTable
            headers={['#', 'Role', 'Status', 'Approver', 'Signed']}
            rows={(po.approvals ?? []).map(a => [
              a.sequence,
              a.role,
              <Badge variant={STEP_VARIANT[a.status] ?? 'default'}>{a.status}</Badge>,
              a.approverName ?? a.approverId ?? '—',
              a.signed ? <span style={{ color: T.green }}>✓</span> : '—',
            ])}
          />

          {po.boardResolutionRequired && (
            <div style={{ marginTop: 10, padding: 10, border: `1px solid ${po.boardResolutionRef ? T.greenL : T.amberL}`, borderRadius: 8 }}>
              {po.boardResolutionRef
                ? (
                  <p style={{ fontSize: 12, color: po.boardResolutionVerified ? T.green : T.amber, margin: 0 }}>
                    Board resolution attached: <b>{po.boardResolutionRef}</b>{' '}
                    {po.boardResolutionVerified
                      ? '· verified against Compliance'
                      : '· NOT verified against Compliance (captured locally)'}
                    {po.boardResolutionUrl && <> · <a href={po.boardResolutionUrl} target="_blank" rel="noreferrer" style={{ color: T.blue }}>scanned copy</a></>}
                  </p>
                )
                : (
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
                    <span style={{ fontSize: 12, color: T.amber, flex: '1 1 100%' }}>Board Resolution required before the MD can sign (LPO &gt; 500k).</span>
                    <input placeholder="Resolution ref" value={br.ref} onChange={e => setBr(b => ({ ...b, ref: e.target.value }))}
                      style={{ height: 34, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
                    <input placeholder="Document URL" value={br.url} onChange={e => setBr(b => ({ ...b, url: e.target.value }))}
                      style={{ height: 34, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
                    <Btn size="sm" variant="outline" disabled={busy || !br.ref.trim()}
                      onClick={() => run(() => proc.attachBoardResolution(po.id, { resolutionRef: br.ref, url: br.url || undefined }), 'Board resolution attached.')}>Attach</Btn>
                  </div>
                )}
            </div>
          )}

          {po.status === 'PendingApproval' && (
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 12 }}>
              <span style={{ fontSize: 12, color: T.mgrey, alignSelf: 'center' }}>Next approver: <b>{po.nextApprovalRole}</b></span>
              <Btn size="sm" variant="green" disabled={busy} onClick={() => run(() => proc.signLpo(po.id, { approve: true }), 'Signed.')}>Sign &amp; Approve</Btn>
              <Btn size="sm" variant="danger" disabled={busy} onClick={() => setRej({ open: true, reason: '' })}>Reject</Btn>
            </div>
          )}
          {po.status === 'Rejected' && po.rejectionReason && <p style={{ fontSize: 12, color: T.red, marginTop: 8 }}>Rejected: {po.rejectionReason}</p>}
          {po.status === 'Issued' && (
            <div style={{ marginTop: 8, display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
              <span style={{ fontSize: 12, color: T.green }}>LPO issued{po.issuedAt ? ` on ${fmtDate(po.issuedAt)}` : ''}.</span>
              <Badge variant={po.receiptStatus === 'FullyReceived' ? 'green' : po.receiptStatus === 'PartiallyReceived' ? 'amber' : 'default'}>
                {po.receiptStatus === 'NotReceived' ? 'Awaiting goods' : po.receiptStatus === 'PartiallyReceived' ? 'Partially received' : 'Goods received'}
              </Badge>
              {po.lastGrnRef && <span style={{ fontSize: 11, color: T.mgrey }}>GRN {String(po.lastGrnRef).slice(0, 8)}</span>}
              <Btn size="sm" variant="outline" disabled={busy}
                onClick={() => run(() => proc.runMatch(po.id), '3-way match run.')}>Run 3-Way Match</Btn>
              <span style={{ fontSize: 11, color: T.mgrey }}>Result appears on the 3-Way Match tab.</span>
            </div>
          )}

          {rej.open && (
            <div style={{ marginTop: 12, padding: 10, border: `1px solid ${T.redL}`, borderRadius: 8 }}>
              <Input label="Rejection reason (mandatory)" value={rej.reason} onChange={v => setRej(r => ({ ...r, reason: v }))} required />
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
                <Btn size="sm" variant="outline" onClick={() => setRej({ open: false, reason: '' })}>Cancel</Btn>
                <Btn size="sm" variant="danger" disabled={busy || !rej.reason.trim()}
                  onClick={() => { run(() => proc.signLpo(po.id, { approve: false, reason: rej.reason }), 'Rejected.'); setRej({ open: false, reason: '' }) }}>Reject</Btn>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ── P6: 3-way match & payment handoff ──────────────────────────────────────────
// LPO vs goods received (Stores GRN, pushed in by the P5 callback) vs the Finance
// supplier invoice. A clean match hands a payment voucher to Finance; a failed
// check raises a matching exception that blocks payment until it is resolved.
function MatchingTab({ flash }) {
  const [rows, setRows] = useState([])
  const [exs, setExs] = useState([])
  const [sum, setSum] = useState(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [detailId, setDetailId] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    proc.listMatches({ status: status || undefined, pageSize: 100 })
      .then(r => setRows(r.data ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    proc.matchSummary().then(setSum).catch(() => {})
    proc.listMatchExceptions({ status: 'Open' }).then(r => setExs(r ?? [])).catch(() => setExs([]))
  }, [status])
  useEffect(() => { load() }, [load])

  const runPending = async () => {
    setBusy(true)
    try { const r = await proc.runPendingMatches(); flash(r?.message ?? 'Matching engine run.'); load() }
    catch (e) { flash(e.response?.data?.message ?? 'Could not run the matching engine.') }
    finally { setBusy(false) }
  }

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Matched" value={sum?.matched ?? 0} color={T.green} />
        <Kpi label="With Exceptions" value={sum?.withExceptions ?? 0} color={T.red} />
        <Kpi label="Open Exceptions" value={sum?.openExceptions ?? 0} color={T.amber} />
        <Kpi label="Awaiting Voucher" value={sum?.awaitingVoucher ?? 0} color={T.blue} />
        <Kpi label="Vouchers Raised" value={sum?.vouchersRaised ?? 0} />
        <Kpi label="Unmatched LPOs" value={sum?.unmatchedLpos ?? 0} color={T.amber} />
        <Kpi label="Matched Value" value={fmtKes(sum?.matchedValue)} />
      </div>

      <SectionHeader
        title="3-Way Match"
        sub="LPO vs goods received vs supplier invoice — the last gate before payment. A clean match hands a voucher to Finance."
        action={<Btn size="sm" disabled={busy} onClick={runPending}>Run Matching Engine</Btn>}
      />

      <div style={{ marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {['Pending', 'Matched', 'Exception'].map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['LPO #', 'Supplier', 'LPO Value', 'Invoice', 'Invoice Value', 'Status', 'Exceptions', 'Voucher', 'Actions']}
          empty="No LPO has been matched yet — run the matching engine once goods are received and Finance has the invoice."
          rows={rows.map(m => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{m.poNumber}</span>,
            m.supplierName ?? '—',
            fmtKes(m.poTotal),
            m.invoiceNumber ?? <span style={{ color: T.mgrey }}>—</span>,
            m.invoiceTotal ? fmtKes(m.invoiceTotal) : '—',
            <Badge variant={MATCH_VARIANT[m.status] ?? 'default'}>{m.status}</Badge>,
            m.openExceptions > 0 ? <span style={{ color: T.red, fontSize: 12 }}>{m.openExceptions} open</span> : '—',
            m.paymentVoucherNo ? <span style={{ color: T.green, fontSize: 12 }}>{m.paymentVoucherNo}</span> : '—',
            <Btn size="sm" variant="outline" onClick={() => setDetailId(m.id)}>Open</Btn>,
          ])}
        />
      )}

      {exs.length > 0 && (
        <div style={{ marginTop: 24 }}>
          <SectionHeader title="Open Matching Exceptions" sub="Payment stays blocked until a Finance Manager records a resolution." />
          <DataTable
            headers={['LPO #', 'Exception', 'Detail', 'Raised', 'Actions']}
            rows={exs.map(e => [
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{e.poNumber ?? '—'}</span>,
              <Badge variant="red">{EX_TYPE_LABEL[e.exceptionType] ?? e.exceptionType}</Badge>,
              <span style={{ fontSize: 12, color: T.mgrey }}>{e.detail}</span>,
              fmtDate(e.raisedAt),
              <Btn size="sm" variant="outline" onClick={() => setDetailId(e.threeWayMatchId)}>Review</Btn>,
            ])}
          />
        </div>
      )}

      {detailId && <MatchDetailModal id={detailId} onClose={() => setDetailId(null)} onChanged={load} flash={flash} />}
    </div>
  )
}

function MatchDetailModal({ id, onClose, onChanged, flash }) {
  const [m, setM] = useState(null)
  const [busy, setBusy] = useState(false)
  const [resolving, setResolving] = useState({ id: null, note: '' })

  const load = useCallback(() => { proc.getMatch(id).then(setM).catch(() => setM(null)) }, [id])
  useEffect(() => { load() }, [load])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); flash(r?.message ?? ok); load(); onChanged?.() }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!m) return <Modal title="3-Way Match" onClose={onClose}><Loading /></Modal>

  const open = (m.exceptions ?? []).filter(e => e.status === 'Open')

  return (
    <Modal title={`3-Way Match — ${m.poNumber}`} onClose={onClose} width={780}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap', marginBottom: 14 }}>
        <Badge variant={MATCH_VARIANT[m.status] ?? 'default'}>{m.status}</Badge>
        <span style={{ fontSize: 13, color: T.mgrey }}>{m.supplierName ?? '—'}</span>
        {m.paymentVoucherNo && <span style={{ fontSize: 12, color: T.green }}>● voucher {m.paymentVoucherNo}</span>}
      </div>

      {/* The three legs */}
      <DataTable
        headers={['Leg', 'Source', 'Value', 'Check']}
        rows={[
          ['Order (LPO)', `${m.poNumber} — procurement`, fmtKes(m.poTotal), <span style={{ color: T.green }}>✓</span>],
          ['Goods received', `Stores GRN — ${m.receiptStatus}`, `${Number(m.receivedQty ?? 0).toLocaleString()} accepted`,
            m.receivedOk ? <span style={{ color: T.green }}>✓ fully received</span> : <span style={{ color: T.red }}>✗ incomplete</span>],
          ['Supplier invoice', m.invoiceNumber ? `${m.invoiceNumber} — Finance` : 'not found in Finance',
            m.invoiceTotal ? fmtKes(m.invoiceTotal) : '—',
            m.priceOk ? <span style={{ color: T.green }}>✓ agrees with the LPO</span>
              : !m.totalOk ? <span style={{ color: T.red }}>✗ exceeds the LPO</span>
                : <span style={{ color: T.red }}>✗ does not agree</span>],
        ]}
      />

      {(m.exceptions ?? []).length > 0 && (
        <div style={{ marginTop: 16 }}>
          <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, marginBottom: 8 }}>Matching Exceptions</h4>
          <DataTable
            headers={['Exception', 'Detail', 'Status', 'Resolution', 'Actions']}
            rows={(m.exceptions ?? []).map(e => [
              <Badge variant={e.status === 'Open' ? 'red' : 'green'}>{EX_TYPE_LABEL[e.exceptionType] ?? e.exceptionType}</Badge>,
              <span style={{ fontSize: 12, color: T.mgrey }}>{e.detail}</span>,
              e.status,
              <span style={{ fontSize: 12, color: T.mgrey }}>{e.resolution ?? '—'}</span>,
              e.status === 'Open'
                ? <Btn size="sm" variant="outline" onClick={() => setResolving({ id: e.id, note: '' })}>Resolve</Btn>
                : '—',
            ])}
          />
        </div>
      )}

      {resolving.id && (
        <div style={{ marginTop: 12, padding: 10, border: `1px solid ${T.amberL}`, borderRadius: 8 }}>
          <Input label="Resolution (mandatory) — credit note, short delivery accepted, invoice corrected…"
            value={resolving.note} onChange={v => setResolving(r => ({ ...r, note: v }))} required />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Btn size="sm" variant="outline" onClick={() => setResolving({ id: null, note: '' })}>Cancel</Btn>
            <Btn size="sm" disabled={busy || !resolving.note.trim()}
              onClick={() => { run(() => proc.resolveMatchException(resolving.id, { resolution: resolving.note }), 'Exception resolved.'); setResolving({ id: null, note: '' }) }}>
              Resolve
            </Btn>
          </div>
          <p style={{ fontSize: 11, color: T.mgrey, marginTop: 6 }}>Clearing the last exception releases the LPO for payment — the override is recorded in the audit trail.</p>
        </div>
      )}

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 18, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
        <Btn size="sm" variant="outline" disabled={busy}
          onClick={() => run(() => proc.runMatch(m.poId), 'Match re-run.')}>Re-run Match</Btn>
        {m.canRaiseVoucher && (
          <Btn size="sm" variant="green" disabled={busy}
            onClick={() => run(() => proc.raisePaymentVoucher(m.id), 'Payment voucher raised in Finance.')}>Raise Payment Voucher</Btn>
        )}
        {!m.canRaiseVoucher && !m.paymentVoucherNo && (
          <span style={{ fontSize: 12, color: T.mgrey, alignSelf: 'center' }}>
            {open.length > 0 ? `Resolve the ${open.length} open exception(s) before payment.`
              : !m.supplierInvoiceId ? 'No Finance supplier invoice is linked to this LPO.'
                : 'The match must be clean before a voucher can be raised.'}
          </span>
        )}
      </div>
    </Modal>
  )
}

// ── P7: international sourcing (PROC-003) ─────────────────────────────────────
// Foreign-currency detail on an issued LPO: FX terms, the T/T advance (MD approval
// mandatory before funds move), shipment + customs, and the landed-cost build-up
// (purchase price + freight + duty + clearing + port) ÷ quantity received.
const INTL_VARIANT = {
  Draft: 'default', ProformaReceived: 'blue', TtApproved: 'amber', TtSent: 'amber',
  Shipped: 'blue', Cleared: 'purple', Costed: 'green',
}
const COST_TYPES = ['Freight', 'ImportDuty', 'Clearing', 'PortCharges', 'Insurance', 'Other']
const COST_LABEL = {
  Freight: 'Freight', ImportDuty: 'Import duty', Clearing: 'Clearing agent',
  PortCharges: 'Port & handling', Insurance: 'Insurance', Other: 'Other',
}

function InternationalTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [status, setStatus] = useState('')
  const [newOpen, setNewOpen] = useState(false)
  const [detailId, setDetailId] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    proc.listIntlPos({ status: status || undefined, pageSize: 100 })
      .then(r => setRows(r.data ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    proc.intlSummary().then(setSum).catch(() => {})
  }, [status])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Orders" value={sum?.total ?? 0} />
        <Kpi label="Awaiting MD (T/T)" value={sum?.awaitingTtApproval ?? 0} color={T.amber} />
        <Kpi label="Approved, Not Sent" value={sum?.ttApprovedNotSent ?? 0} color={T.blue} />
        <Kpi label="In Transit" value={sum?.inTransit ?? 0} color={T.blue} />
        <Kpi label="Costed" value={sum?.costed ?? 0} color={T.green} />
        <Kpi label="Landed Value" value={fmtKes(sum?.totalLandedValueKes)} />
        <Kpi label="T/T Outstanding" value={fmtKes(sum?.ttOutstandingKes)} color={T.amber} />
      </div>

      <SectionHeader
        title="International Sourcing"
        sub="Landed cost = (purchase price + freight + duty + clearing + port charges) ÷ quantity received. T/T advances need MD approval before funds move."
        action={<Btn size="sm" onClick={() => setNewOpen(true)}>+ International Order</Btn>}
      />

      <div style={{ marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {Object.keys(INTL_VARIANT).map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['LPO #', 'Supplier', 'FX Price', 'KES Value', 'Landed Total', 'Per Unit', 'BL / ETA', 'Status', 'Actions']}
          empty="No international orders yet — attach FX detail to an LPO to start."
          rows={rows.map(o => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{o.poNumber}</span>,
            o.supplierName ?? '—',
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{o.currencyCode} {Number(o.purchasePriceFx ?? 0).toLocaleString()}</span>,
            fmtKes(o.purchasePriceKes),
            fmtKes(o.totalLandedCostKes),
            fmtKes(o.landedCostPerUnitKes),
            <span style={{ fontSize: 12, color: T.mgrey }}>{o.blNumber ?? '—'}{o.eta ? ` · ${fmtDate(o.eta)}` : ''}</span>,
            <Badge variant={INTL_VARIANT[o.status] ?? 'default'}>{o.status}</Badge>,
            <Btn size="sm" variant="outline" onClick={() => setDetailId(o.id)}>Open</Btn>,
          ])}
        />
      )}

      {newOpen && <NewIntlModal onClose={() => setNewOpen(false)} onSaved={() => { setNewOpen(false); load() }} flash={flash} />}
      {detailId && <IntlDetailModal id={detailId} onClose={() => setDetailId(null)} onChanged={load} flash={flash} />}
    </div>
  )
}

function NewIntlModal({ onClose, onSaved, flash }) {
  const [pos, setPos] = useState([])
  const [currencies, setCurrencies] = useState([])
  const [f, setF] = useState({ poId: '', currencyCode: '', purchasePriceFx: '', quantityOrdered: '', exchangeRate: '', proformaInvoiceUrl: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    // Only LPOs that have no international detail yet are sensible targets.
    proc.listPurchaseOrders({ pageSize: 100 }).then(r => setPos(r.data ?? [])).catch(() => {})
    proc.listCurrencies().then(c => setCurrencies(c ?? [])).catch(() => setCurrencies([]))
  }, [])

  const picked = currencies.find(c => c.code === f.currencyCode)
  const rate = Number(f.exchangeRate) || picked?.rate || 0
  const kes = (Number(f.purchasePriceFx) || 0) * rate

  const save = async () => {
    if (!f.poId || !f.currencyCode) { flash('Pick the LPO and the currency.'); return }
    if (!(Number(f.purchasePriceFx) > 0)) { flash('Enter the foreign-currency price.'); return }
    if (!(Number(f.quantityOrdered) > 0)) { flash('Enter the ordered quantity.'); return }
    setBusy(true)
    try {
      const r = await proc.createIntlPo({
        poId: f.poId,
        currencyCode: f.currencyCode,
        purchasePriceFx: Number(f.purchasePriceFx),
        quantityOrdered: Number(f.quantityOrdered),
        exchangeRate: f.exchangeRate ? Number(f.exchangeRate) : undefined,
        proformaInvoiceUrl: f.proformaInvoiceUrl || undefined,
      })
      flash(r?.message ?? 'International detail captured.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not save.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="New International Order" onClose={onClose} width={620}>
      <Select label="LPO" value={f.poId} onChange={v => setF(x => ({ ...x, poId: v }))}
        options={[{ value: '', label: 'Select an LPO…' },
          ...pos.map(p => ({ value: p.id, label: `${p.poNumber} — ${p.supplierName ?? ''} (${p.status})` }))]} />

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Currency" value={f.currencyCode} onChange={v => setF(x => ({ ...x, currencyCode: v }))}
          options={[{ value: '', label: 'Select…' },
            ...currencies.map(c => ({ value: c.code, label: `${c.code} — ${c.rate} KES` }))]} />
        <Input label="Purchase price (FX)" type="number" value={f.purchasePriceFx}
          onChange={v => setF(x => ({ ...x, purchasePriceFx: v }))} required />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Quantity ordered" type="number" value={f.quantityOrdered}
          onChange={v => setF(x => ({ ...x, quantityOrdered: v }))} required />
        <Input label={`Rate override (KES per 1 ${f.currencyCode || 'unit'})`} type="number" value={f.exchangeRate}
          onChange={v => setF(x => ({ ...x, exchangeRate: v }))} />
      </div>

      <Input label="Proforma invoice URL" value={f.proformaInvoiceUrl}
        onChange={v => setF(x => ({ ...x, proformaInvoiceUrl: v }))} />

      {currencies.length === 0 && (
        <p style={{ fontSize: 11, color: T.amber, marginTop: 4 }}>
          No rates available from Finance — enter the rate explicitly, otherwise the order will be rejected.
        </p>
      )}
      {rate > 0 && (
        <p style={{ fontSize: 12, color: T.mgrey, marginTop: 6 }}>
          Converts at <b>{rate}</b> → <b>{fmtKes(kes)}</b>
          {f.exchangeRate ? ' (manual rate)' : ' (Finance rate)'}
        </p>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
        <Btn variant="outline" onClick={onClose}>Cancel</Btn>
        <Btn disabled={busy} onClick={save}>Save</Btn>
      </div>
    </Modal>
  )
}

function IntlDetailModal({ id, onClose, onChanged, flash }) {
  const [o, setO] = useState(null)
  const [busy, setBusy] = useState(false)
  const [currencies, setCurrencies] = useState([])
  const [comp, setComp] = useState({ componentType: 'Freight', amountFx: '', currencyCode: 'KES', exchangeRate: '', notes: '' })
  const [ship, setShip] = useState({ blNumber: '', eta: '' })
  const [tt, setTt] = useState('')
  const [cust, setCust] = useState({ idfNumber: '', entryNumber: '', importDutyKes: '', clearingAgentFeeKes: '', portChargesKes: '' })

  const load = useCallback(() => { proc.getIntlPo(id).then(setO).catch(() => setO(null)) }, [id])
  useEffect(() => { load(); proc.listCurrencies().then(c => setCurrencies(c ?? [])).catch(() => {}) }, [load])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); flash(r?.message ?? ok); load(); onChanged?.() }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!o) return <Modal title="International Order" onClose={onClose}><Loading /></Modal>

  const componentTotal = (o.components ?? []).reduce((s, c) => s + Number(c.kesAmount ?? 0), 0)

  return (
    <Modal title={`International — ${o.poNumber}`} onClose={onClose} width={820}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap', marginBottom: 14 }}>
        <Badge variant={INTL_VARIANT[o.status] ?? 'default'}>{o.status}</Badge>
        <span style={{ fontSize: 13, color: T.mgrey }}>{o.supplierName ?? '—'}</span>
        <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{o.currencyCode} {Number(o.purchasePriceFx ?? 0).toLocaleString()} @ {o.exchangeRateAtOrder}</span>
        <span style={{ fontSize: 12, color: T.mgrey }}>LPO {o.poStatus} · {o.receiptStatus}</span>
      </div>

      {/* Landed cost build-up */}
      <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, marginBottom: 8 }}>Landed cost build-up</h4>
      <DataTable
        headers={['Component', 'Amount', 'Rate', 'KES', 'Incurred', 'Actions']}
        empty="No additional costs yet — the landed cost is the purchase price alone."
        rows={[
          ['Purchase price', `${o.currencyCode} ${Number(o.purchasePriceFx ?? 0).toLocaleString()}`,
            o.exchangeRateAtOrder, fmtKes(o.purchasePriceKes), '—', '—'],
          ...(o.components ?? []).map(c => [
            <span>{COST_LABEL[c.componentType] ?? c.componentType}{c.fromCustoms && <span style={{ fontSize: 10, color: T.mgrey }}> · customs</span>}</span>,
            `${c.currencyCode} ${Number(c.amountFx ?? 0).toLocaleString()}`,
            c.exchangeRate,
            fmtKes(c.kesAmount),
            fmtDate(c.incurredOn),
            c.fromCustoms
              ? <span style={{ fontSize: 11, color: T.mgrey }}>via customs</span>
              : <Btn size="sm" variant="danger" disabled={busy}
                  onClick={() => run(() => proc.removeLandedCost(c.id), 'Component removed.')}>Remove</Btn>,
          ]),
        ]}
      />

      <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap', marginTop: 10, padding: '10px 12px', background: T.offwt, borderRadius: 8 }}>
        <span style={{ fontSize: 12, color: T.mgrey }}>Additional costs <b>{fmtKes(componentTotal)}</b></span>
        <span style={{ fontSize: 12, color: T.mgrey }}>Total landed <b style={{ color: T.navy }}>{fmtKes(o.totalLandedCostKes)}</b></span>
        <span style={{ fontSize: 12, color: T.mgrey }}>
          ÷ {Number(o.quantityBasis ?? 0).toLocaleString()} units {o.quantityFromReceipt ? '(received)' : '(ordered)'}
        </span>
        <span style={{ fontSize: 12, color: T.mgrey }}>Per unit <b style={{ color: T.green }}>{fmtKes(o.landedCostPerUnitKes)}</b></span>
      </div>

      {/* Add a cost component */}
      <div style={{ marginTop: 12, display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
        <select value={comp.componentType} onChange={e => setComp(c => ({ ...c, componentType: e.target.value }))}
          style={{ height: 36, padding: '0 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }}>
          {COST_TYPES.map(t => <option key={t} value={t}>{COST_LABEL[t]}</option>)}
        </select>
        <input placeholder="Amount" type="number" value={comp.amountFx} onChange={e => setComp(c => ({ ...c, amountFx: e.target.value }))}
          style={{ height: 36, width: 110, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
        <select value={comp.currencyCode} onChange={e => setComp(c => ({ ...c, currencyCode: e.target.value }))}
          style={{ height: 36, padding: '0 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }}>
          <option value="KES">KES</option>
          {currencies.filter(c => c.code !== 'KES').map(c => <option key={c.code} value={c.code}>{c.code}</option>)}
        </select>
        <input placeholder="Rate (opt)" type="number" value={comp.exchangeRate} onChange={e => setComp(c => ({ ...c, exchangeRate: e.target.value }))}
          style={{ height: 36, width: 100, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
        <input placeholder="Notes" value={comp.notes} onChange={e => setComp(c => ({ ...c, notes: e.target.value }))}
          style={{ height: 36, flex: '1 1 140px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
        <Btn size="sm" variant="outline" disabled={busy || !(Number(comp.amountFx) > 0)}
          onClick={() => { run(() => proc.addLandedCost(o.id, {
            componentType: comp.componentType, amountFx: Number(comp.amountFx), currencyCode: comp.currencyCode,
            exchangeRate: comp.exchangeRate ? Number(comp.exchangeRate) : undefined, notes: comp.notes || undefined,
          }), 'Cost added.'); setComp(c => ({ ...c, amountFx: '', notes: '' })) }}>Add cost</Btn>
      </div>

      {/* T/T advance */}
      <div style={{ marginTop: 18, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
        <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, marginBottom: 8 }}>
          T/T advance <span style={{ fontWeight: 400, color: T.mgrey }}>— MD approval required before funds move</span>
        </h4>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
          {o.ttAmountFx
            ? <span style={{ fontSize: 12 }}>
                <b>{o.currencyCode} {Number(o.ttAmountFx).toLocaleString()}</b>
                {o.ttRequestedAt && <span style={{ color: T.mgrey }}> · requested {fmtDate(o.ttRequestedAt)}</span>}
                {o.ttApprovedAt
                  ? <span style={{ color: T.green }}> · MD approved {fmtDate(o.ttApprovedAt)}</span>
                  : <span style={{ color: T.amber }}> · awaiting MD</span>}
                {o.ttSentAt && <span style={{ color: T.green }}> · sent {fmtDate(o.ttSentAt)}</span>}
                {o.ttVoucherNo && <span style={{ color: T.green }}> · voucher {o.ttVoucherNo}</span>}
              </span>
            : <span style={{ fontSize: 12, color: T.mgrey }}>No advance requested.</span>}
        </div>

        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 10, alignItems: 'center' }}>
          {o.canRequestTt && (
            <>
              <input placeholder={`Advance (${o.currencyCode})`} type="number" value={tt} onChange={e => setTt(e.target.value)}
                style={{ height: 36, width: 150, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
              <Btn size="sm" variant="outline" disabled={busy || !(Number(tt) > 0)}
                onClick={() => { run(() => proc.requestTt(o.id, { amountFx: Number(tt) }), 'Advance requested.'); setTt('') }}>
                Request advance
              </Btn>
            </>
          )}
          {o.canApproveTt && (
            <Btn size="sm" variant="green" disabled={busy}
              onClick={() => run(() => proc.approveTt(o.id), 'Advance approved.')}>MD approve advance</Btn>
          )}
          {o.canSendTt && (
            <Btn size="sm" disabled={busy}
              onClick={() => run(() => proc.sendTt(o.id), 'Advance sent.')}>Send T/T &amp; raise voucher</Btn>
          )}
          {!o.canRequestTt && !o.ttAmountFx && (
            <span style={{ fontSize: 11, color: T.mgrey }}>The LPO must be issued before an advance can be requested.</span>
          )}
        </div>
      </div>

      {/* Shipment */}
      <div style={{ marginTop: 18, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
        <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, marginBottom: 8 }}>Shipment</h4>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
          <input placeholder={o.blNumber || 'BL / airway bill no.'} value={ship.blNumber}
            onChange={e => setShip(s => ({ ...s, blNumber: e.target.value }))}
            style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
          <input type="date" value={ship.eta} onChange={e => setShip(s => ({ ...s, eta: e.target.value }))}
            style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
          <Btn size="sm" variant="outline" disabled={busy || (!ship.blNumber && !ship.eta)}
            onClick={() => run(() => proc.updateIntlShipment(o.id, {
              blNumber: ship.blNumber || undefined, eta: ship.eta || undefined,
            }), 'Shipment updated.')}>Update shipment</Btn>
        </div>
      </div>

      {/* Customs */}
      <div style={{ marginTop: 18, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
        <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, marginBottom: 8 }}>
          Customs declaration <span style={{ fontWeight: 400, color: T.mgrey }}>— feeds the duty, clearing and port components</span>
        </h4>
        {o.customs && (
          <p style={{ fontSize: 12, color: T.mgrey, marginBottom: 8 }}>
            IDF <b>{o.customs.idfNumber}</b>{o.customs.entryNumber ? ` · entry ${o.customs.entryNumber}` : ''} · declared {fmtDate(o.customs.declaredAt)}
            {' '}· duty {fmtKes(o.customs.importDutyKes)}, clearing {fmtKes(o.customs.clearingAgentFeeKes)}, port {fmtKes(o.customs.portChargesKes)}
          </p>
        )}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 8, alignItems: 'end' }}>
          <input placeholder="IDF number" value={cust.idfNumber} onChange={e => setCust(c => ({ ...c, idfNumber: e.target.value }))}
            style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
          <input placeholder="Entry no." value={cust.entryNumber} onChange={e => setCust(c => ({ ...c, entryNumber: e.target.value }))}
            style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
          <input placeholder="Duty KES" type="number" value={cust.importDutyKes} onChange={e => setCust(c => ({ ...c, importDutyKes: e.target.value }))}
            style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
          <input placeholder="Clearing KES" type="number" value={cust.clearingAgentFeeKes} onChange={e => setCust(c => ({ ...c, clearingAgentFeeKes: e.target.value }))}
            style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
          <input placeholder="Port KES" type="number" value={cust.portChargesKes} onChange={e => setCust(c => ({ ...c, portChargesKes: e.target.value }))}
            style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
          <Btn size="sm" variant="outline" disabled={busy || !cust.idfNumber.trim()}
            onClick={() => run(() => proc.declareCustoms(o.id, {
              idfNumber: cust.idfNumber, entryNumber: cust.entryNumber || undefined,
              importDutyKes: Number(cust.importDutyKes) || 0,
              clearingAgentFeeKes: Number(cust.clearingAgentFeeKes) || 0,
              portChargesKes: Number(cust.portChargesKes) || 0,
            }), 'Customs declared.')}>{o.customs ? 'Re-lodge' : 'Declare'}</Btn>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 18, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
        {o.status !== 'Costed'
          ? <>
              <Btn size="sm" variant="green" disabled={busy || o.receiptStatus !== 'FullyReceived'}
                onClick={() => run(() => proc.finaliseLandedCost(o.id), 'Landed cost finalised.')}>Finalise landed cost</Btn>
              {o.receiptStatus !== 'FullyReceived' &&
                <span style={{ fontSize: 11, color: T.mgrey, alignSelf: 'center' }}>Goods must be fully received first.</span>}
            </>
          : <span style={{ fontSize: 12, color: T.green }}>Landed cost finalised at {fmtKes(o.landedCostPerUnitKes)} per unit.</span>}
      </div>
    </Modal>
  )
}

// ── P8: emergency procurement (PROC-004) ─────────────────────────────────────
// The quotation step is waived and nothing else. The MD must authorise before the
// purchase is made (never retrospectively), justification + waiver document are
// mandatory, a post-hoc requisition is due within 24h, and every case goes into
// the monthly board pack. Receipt, 3-way match and payment authority still apply.
const thisPeriod = () => new Date().toISOString().slice(0, 7)

function EmergencyTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [flag, setFlag] = useState('')
  const [declareOpen, setDeclareOpen] = useState(false)
  const [detailId, setDetailId] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    proc.listEmergencies({ flag: flag || undefined, pageSize: 100 })
      .then(r => setRows(r.data ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    proc.emergencySummary().then(setSum).catch(() => {})
  }, [flag])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Declared" value={sum?.total ?? 0} />
        <Kpi label="Awaiting MD" value={sum?.awaitingMdApproval ?? 0} color={T.red} />
        <Kpi label="Post-hoc Due" value={sum?.postHocOutstanding ?? 0} color={T.amber} />
        <Kpi label="Post-hoc Overdue" value={sum?.postHocOverdue ?? 0} color={T.red} />
        <Kpi label="Raised Late" value={sum?.postHocRaisedLate ?? 0} color={T.amber} />
        <Kpi label="Board Pack Pending" value={sum?.boardPackPending ?? 0} color={T.blue} />
        <Kpi label="Value This Month" value={fmtKes(sum?.valueThisMonth)} sub={`${fmtKes(sum?.totalValue)} all time`} />
      </div>

      <SectionHeader
        title="Emergency Procurement"
        sub="Only the quotation requirement is waived. MD authorisation must precede the purchase; a post-hoc requisition is due within 24 hours; every case is reported to the board."
        action={<Btn size="sm" variant="danger" onClick={() => setDeclareOpen(true)}>Declare Emergency</Btn>}
      />

      <div style={{ marginBottom: 14 }}>
        <select value={flag} onChange={e => setFlag(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All emergencies</option>
          <option value="AwaitingMd">Awaiting MD authorisation</option>
          <option value="PostHocOutstanding">Post-hoc requisition outstanding</option>
          <option value="BoardPackPending">Not yet in a board pack</option>
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['LPO #', 'Supplier', 'Value', 'Declared', 'MD', 'Post-hoc PR', 'Board Pack', 'Actions']}
          empty="No emergency procurements declared."
          rows={rows.map(e => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{e.poNumber}</span>,
            e.supplierName ?? '—',
            fmtKes(e.totalAmount),
            fmtDate(e.declaredAt),
            e.mdApproved
              ? <Badge variant="green">Authorised</Badge>
              : <Badge variant="red">Awaiting MD</Badge>,
            e.postHocPrNumber
              ? <span style={{ fontSize: 12 }}>{e.postHocPrNumber}{e.postHocRaisedLate && <span style={{ color: T.amber }}> · late</span>}</span>
              : <span style={{ color: e.postHocOverdue ? T.red : T.amber, fontSize: 12 }}>{e.postHocOverdue ? 'overdue' : 'due'}</span>,
            e.boardPackPeriod ?? <span style={{ color: T.mgrey }}>—</span>,
            <Btn size="sm" variant="outline" onClick={() => setDetailId(e.id)}>Open</Btn>,
          ])}
        />
      )}

      {declareOpen && <DeclareEmergencyModal onClose={() => setDeclareOpen(false)} onSaved={() => { setDeclareOpen(false); load() }} flash={flash} />}
      {detailId && <EmergencyDetailModal id={detailId} onClose={() => setDetailId(null)} onChanged={load} flash={flash} />}
    </div>
  )
}

function DeclareEmergencyModal({ onClose, onSaved, flash }) {
  const [suppliers, setSuppliers] = useState([])
  const [f, setF] = useState({ supplierId: '', totalAmount: '', departmentId: 'Engineering', emergencyReason: '', quotationWaiverUrl: '', waiverReason: '', description: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => { proc.listSuppliers({ approvedOnly: true, pageSize: 100 }).then(r => setSuppliers(r.data ?? [])).catch(() => {}) }, [])

  const save = async () => {
    if (!f.supplierId) { flash('Select an ASR-approved supplier.'); return }
    if (!(Number(f.totalAmount) > 0)) { flash('Enter the purchase value.'); return }
    if (!f.emergencyReason.trim()) { flash('A written justification is mandatory.'); return }
    if (!f.quotationWaiverUrl.trim()) { flash('The quotation-waiver document is mandatory.'); return }
    if (!f.waiverReason.trim()) { flash('State why the quotation requirement is waived.'); return }
    setBusy(true)
    try {
      const r = await proc.declareEmergency({ ...f, totalAmount: Number(f.totalAmount) })
      flash(r?.message ?? 'Emergency declared.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not declare.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Declare Emergency Procurement" onClose={onClose} width={640}>
      <div style={{ padding: 10, background: T.redL, borderRadius: 8, marginBottom: 14 }}>
        <p style={{ fontSize: 12, color: T.red, margin: 0 }}>
          Only the quotation requirement is waived. The MD must authorise this before the purchase is made —
          approval cannot be given retrospectively. A post-hoc requisition is due within 24 hours and this
          purchase will appear in the monthly board pack.
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Supplier (ASR-approved)" value={f.supplierId} onChange={v => setF(x => ({ ...x, supplierId: v }))}
          options={[{ value: '', label: 'Select…' }, ...suppliers.map(s => ({ value: s.id, label: `${s.supplierNumber} — ${s.name}` }))]} required />
        <Input label="Purchase value (KES)" type="number" value={f.totalAmount}
          onChange={v => setF(x => ({ ...x, totalAmount: v }))} required />
      </div>

      <Select label="Department" value={f.departmentId} onChange={v => setF(x => ({ ...x, departmentId: v }))}
        options={DEPARTMENTS} />

      <Input label="Written justification for the emergency" value={f.emergencyReason}
        onChange={v => setF(x => ({ ...x, emergencyReason: v }))} required />
      <Input label="Quotation-waiver document URL" value={f.quotationWaiverUrl}
        onChange={v => setF(x => ({ ...x, quotationWaiverUrl: v }))} required />
      <Input label="Why the quotation requirement is waived" value={f.waiverReason}
        onChange={v => setF(x => ({ ...x, waiverReason: v }))} required />

      {Number(f.totalAmount) > 500000 && (
        <p style={{ fontSize: 11, color: T.amber }}>Above 500,000 — a Board Resolution is still required before the MD can authorise.</p>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
        <Btn variant="outline" onClick={onClose}>Cancel</Btn>
        <Btn variant="danger" disabled={busy} onClick={save}>Declare</Btn>
      </div>
    </Modal>
  )
}

function EmergencyDetailModal({ id, onClose, onChanged, flash }) {
  const [e, setE] = useState(null)
  const [busy, setBusy] = useState(false)
  const [mdRef, setMdRef] = useState('')
  const [period, setPeriod] = useState(thisPeriod())
  const [pr, setPr] = useState({ departmentId: '', justification: '' })

  const load = useCallback(() => { proc.getEmergency(id).then(setE).catch(() => setE(null)) }, [id])
  useEffect(() => { load() }, [load])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); flash(r?.message ?? ok); load(); onChanged?.() }
    catch (err) { flash(err.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!e) return <Modal title="Emergency Procurement" onClose={onClose}><Loading /></Modal>

  return (
    <Modal title={`Emergency — ${e.poNumber}`} onClose={onClose} width={760}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap', marginBottom: 14 }}>
        <Badge variant={e.awaitingMdApproval ? 'red' : 'green'}>{e.awaitingMdApproval ? 'Awaiting MD' : 'MD authorised'}</Badge>
        <Badge variant="default">{e.poStatus}</Badge>
        <span style={{ fontSize: 13, color: T.mgrey }}>{e.supplierName} · {fmtKes(e.totalAmount)}</span>
        <span style={{ fontSize: 12, color: T.mgrey }}>declared {fmtDate(e.declaredAt)}</span>
      </div>

      <DataTable
        headers={['Control', 'Status']}
        rows={[
          ['Written justification', <span style={{ fontSize: 12 }}>{e.emergencyReason}</span>],
          ['Quotation waiver', <span style={{ fontSize: 12 }}>{e.waiverReason}
            {e.quotationWaiverUrl && <> · <a href={e.quotationWaiverUrl} target="_blank" rel="noreferrer" style={{ color: T.blue }}>document</a></>}</span>],
          ['MD authorisation', e.mdApprovedAt
            ? <span style={{ fontSize: 12, color: T.green }}>ref {e.mdApprovalRef} · {fmtDate(e.mdApprovedAt)}</span>
            : <span style={{ fontSize: 12, color: T.red }}>outstanding — the purchase may not be made yet</span>],
          ['Post-hoc requisition (24h)', e.postHocPrNumber
            ? <span style={{ fontSize: 12, color: e.postHocRaisedLate ? T.amber : T.green }}>
                {e.postHocPrNumber} · raised {fmtDate(e.postHocRaisedAt)}{e.postHocRaisedLate ? ' — late, reported as a breach' : ' — within the window'}
              </span>
            : <span style={{ fontSize: 12, color: e.postHocOverdue ? T.red : T.amber }}>
                due by {fmtDate(e.postHocDueAt)}{e.postHocOverdue ? ' — OVERDUE' : ''}
              </span>],
          ['Goods receipt', <span style={{ fontSize: 12, color: T.mgrey }}>{e.receiptStatus} (unchanged by the emergency route)</span>],
          ['Board pack', e.boardPackPeriod
            ? <span style={{ fontSize: 12, color: T.green }}>reported in {e.boardPackPeriod}</span>
            : <span style={{ fontSize: 12, color: T.blue }}>not yet reported</span>],
        ]}
      />

      {/* MD authorisation */}
      {e.awaitingMdApproval && (
        <div style={{ marginTop: 16, padding: 10, border: `1px solid ${T.redL}`, borderRadius: 8 }}>
          <p style={{ fontSize: 12, color: T.red, marginTop: 0 }}>
            MD authorisation issues the LPO. It cannot be given after the purchase — authorise before anything is bought.
          </p>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
            <input placeholder="MD authorisation reference" value={mdRef} onChange={ev => setMdRef(ev.target.value)}
              style={{ height: 36, flex: '1 1 220px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <Btn size="sm" variant="green" disabled={busy || !mdRef.trim()}
              onClick={() => run(() => proc.mdApproveEmergency(e.id, { mdApprovalRef: mdRef }), 'MD authorisation recorded.')}>
              Record MD authorisation
            </Btn>
          </div>
        </div>
      )}

      {/* Post-hoc requisition */}
      {!e.postHocPrNumber && (
        <div style={{ marginTop: 16, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
          <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, marginBottom: 8 }}>
            Raise the post-hoc requisition
            <span style={{ fontWeight: 400, color: e.postHocOverdue ? T.red : T.mgrey }}>
              {' '}— due by {fmtDate(e.postHocDueAt)}{e.postHocOverdue ? ' (overdue; still required)' : ''}
            </span>
          </h4>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
            <select value={pr.departmentId} onChange={ev => setPr(p => ({ ...p, departmentId: ev.target.value }))}
              style={{ height: 36, padding: '0 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }}>
              <option value="">Department…</option>
              {DEPARTMENTS.map(d => <option key={d} value={d}>{d}</option>)}
            </select>
            <input placeholder="Justification (optional)" value={pr.justification}
              onChange={ev => setPr(p => ({ ...p, justification: ev.target.value }))}
              style={{ height: 36, flex: '1 1 200px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <Btn size="sm" variant="outline" disabled={busy}
              onClick={() => run(() => proc.raisePostHocPr(e.id, {
                departmentId: pr.departmentId || undefined, justification: pr.justification || undefined,
              }), 'Post-hoc requisition raised.')}>Raise requisition</Btn>
          </div>
          <p style={{ fontSize: 11, color: T.mgrey, marginTop: 6 }}>
            A single line is synthesised from the LPO value unless you raise it from the Requisitions tab.
          </p>
        </div>
      )}

      {/* Board pack */}
      <div style={{ marginTop: 16, paddingTop: 14, borderTop: `1px solid ${T.lgrey}`, display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
        <input placeholder="YYYY-MM" value={period} onChange={ev => setPeriod(ev.target.value)}
          style={{ height: 36, width: 110, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
        <Btn size="sm" variant="outline" disabled={busy || !/^\d{4}-\d{2}$/.test(period)}
          onClick={() => run(() => proc.markEmergencyBoardPack(e.id, { period }), 'Marked for the board pack.')}>
          {e.boardPackPeriod ? 'Re-assign board pack' : 'Mark reported to board'}
        </Btn>
      </div>
    </Modal>
  )
}

// ── P9: supplier performance review (PROC-002) ───────────────────────────────
// Biannual auto-scoring from transaction data already held: Quality 30 (GRN
// rejection rate), Delivery 25 (on-time), Pricing 25 (invoice accuracy + quote
// competitiveness), Compliance 20 (document currency, gifts). A component with no
// data is NOT scored as zero — the overall is rescaled over the assessed weight.
// <60 warns; <40 escalates to the MD, who blacklists via the ASR.
const OUTCOME_VARIANT = { Satisfactory: 'green', Warning: 'amber', MdEscalation: 'red', NotAssessed: 'default' }
const OUTCOME_LABEL = { Satisfactory: 'Satisfactory', Warning: 'Warning', MdEscalation: 'MD escalation', NotAssessed: 'No data' }
const currentHalf = () => { const d = new Date(); return `${d.getFullYear()}-${d.getMonth() <= 5 ? 'H1' : 'H2'}` }

function PerformanceTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [period, setPeriod] = useState(currentHalf())
  const [outcome, setOutcome] = useState('')
  const [busy, setBusy] = useState(false)
  const [detailId, setDetailId] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    proc.listReviews({ period: period || undefined, outcome: outcome || undefined, pageSize: 200 })
      .then(r => setRows(r.data ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    proc.reviewSummary(period || undefined).then(setSum).catch(() => {})
  }, [period, outcome])
  useEffect(() => { load() }, [load])

  const run = async () => {
    setBusy(true)
    try { const r = await proc.runReviews({ period }); flash(r?.message ?? 'Review run.'); load() }
    catch (e) { flash(e.response?.data?.message ?? 'Could not run the review.') }
    finally { setBusy(false) }
  }

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Reviewed" value={sum?.reviewed ?? 0} sub={sum?.latestPeriod ?? '—'} />
        <Kpi label="Satisfactory" value={sum?.satisfactory ?? 0} color={T.green} />
        <Kpi label="Warning (<60)" value={sum?.warning ?? 0} color={T.amber} />
        <Kpi label="MD Escalation (<40)" value={sum?.mdEscalation ?? 0} color={T.red} />
        <Kpi label="Not Assessed" value={sum?.notAssessed ?? 0} />
        <Kpi label="Below Category Min" value={sum?.belowCategoryThreshold ?? 0} color={T.amber} />
        <Kpi label="Average Score" value={sum?.averageScore != null ? `${sum.averageScore}/100` : '—'} />
      </div>

      <SectionHeader
        title="Supplier Performance"
        sub="Scored automatically from transaction data: Quality 30 · Delivery 25 · Pricing 25 · Compliance 20. Blacklisting stays an MD decision in the supplier register."
        action={<Btn size="sm" disabled={busy || !/^\d{4}-H[12]$/.test(period)} onClick={run}>Run {period} Review</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <input placeholder="YYYY-H1" value={period} onChange={e => setPeriod(e.target.value)}
          style={{ height: 40, width: 120, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }} />
        <select value={outcome} onChange={e => setOutcome(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All outcomes</option>
          {Object.keys(OUTCOME_VARIANT).map(o => <option key={o} value={o}>{OUTCOME_LABEL[o]}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Supplier', 'Quality /30', 'Delivery /25', 'Pricing /25', 'Compliance /20', 'Overall', 'Evidence', 'Outcome', 'Actions']}
          empty={`No reviews for ${period} — run the review to score suppliers.`}
          rows={rows.map(r => [
            <span style={{ fontSize: 13 }}>{r.supplierName ?? '—'}</span>,
            r.qualityScore != null ? r.qualityScore : <span style={{ color: T.mgrey }}>n/a</span>,
            r.deliveryScore != null ? r.deliveryScore : <span style={{ color: T.mgrey }}>n/a</span>,
            r.pricingScore != null ? r.pricingScore : <span style={{ color: T.mgrey }}>n/a</span>,
            r.complianceScore != null ? r.complianceScore : <span style={{ color: T.mgrey }}>n/a</span>,
            <b style={{ color: r.outcome === 'MdEscalation' ? T.red : r.outcome === 'Warning' ? T.amber : T.navy }}>
              {r.outcome === 'NotAssessed' ? '—' : `${r.overallScore}/100`}
            </b>,
            <span style={{ fontSize: 11, color: T.mgrey }}>{r.assessedWeight}/100 pts</span>,
            <span>
              <Badge variant={OUTCOME_VARIANT[r.outcome] ?? 'default'}>{OUTCOME_LABEL[r.outcome] ?? r.outcome}</Badge>
              {r.belowCategoryThreshold && <span style={{ fontSize: 10, color: T.amber, marginLeft: 4 }}>below min</span>}
            </span>,
            <Btn size="sm" variant="outline" onClick={() => setDetailId(r.id)}>Open</Btn>,
          ])}
        />
      )}

      {detailId && <ReviewDetailModal id={detailId} onClose={() => setDetailId(null)} onChanged={load} flash={flash} />}
    </div>
  )
}

function ReviewDetailModal({ id, onClose, onChanged, flash }) {
  const [r, setR] = useState(null)
  const [busy, setBusy] = useState(false)
  const [notes, setNotes] = useState('')

  const load = useCallback(() => { proc.getReview(id).then(setR).catch(() => setR(null)) }, [id])
  useEffect(() => { load() }, [load])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const res = await fn(); flash(res?.message ?? ok); load(); onChanged?.() }
    catch (e) { flash(e.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!r) return <Modal title="Performance Review" onClose={onClose}><Loading /></Modal>

  const na = <span style={{ color: T.mgrey }}>not assessed — no data in the period</span>

  return (
    <Modal title={`${r.supplierName} — ${r.reviewPeriod}`} onClose={onClose} width={780}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap', marginBottom: 14 }}>
        <Badge variant={OUTCOME_VARIANT[r.outcome] ?? 'default'}>{OUTCOME_LABEL[r.outcome] ?? r.outcome}</Badge>
        <span style={{ fontSize: 20, fontWeight: 800, color: r.outcome === 'MdEscalation' ? T.red : T.navy }}>
          {r.outcome === 'NotAssessed' ? '—' : `${r.overallScore}/100`}
        </span>
        <span style={{ fontSize: 12, color: T.mgrey }}>scored on {r.assessedWeight} of 100 points of evidence</span>
        {r.categoryMinScore != null && (
          <span style={{ fontSize: 12, color: r.belowCategoryThreshold ? T.amber : T.mgrey }}>
            category minimum {r.categoryMinScore}
          </span>
        )}
      </div>

      {r.notes && <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0 }}>{r.notes}</p>}

      <DataTable
        headers={['Criterion', 'Score', 'Basis']}
        rows={[
          ['Quality /30', r.qualityScore ?? '—',
            r.qualityScore == null ? na
              : <span style={{ fontSize: 12, color: T.mgrey }}>
                  {r.rejectRatePct}% rejected — {r.rejectedQty} of {Number(r.acceptedQty) + Number(r.rejectedQty)} units over {r.receivedPoCount} receipt(s)
                </span>],
          ['Delivery /25', r.deliveryScore ?? '—',
            r.deliveryScore == null ? na
              : <span style={{ fontSize: 12, color: T.mgrey }}>
                  {r.onTimeCount} on time / {r.lateCount} late
                  {r.deliveryFromPromisedDates
                    ? ' — against promised dates'
                    : ` — no promised dates recorded, approximated from ${r.avgLeadTimeDays ?? '—'}d average lead time`}
                </span>],
          ['Pricing /25', r.pricingScore ?? '—',
            r.pricingScore == null ? na
              : <span style={{ fontSize: 12, color: T.mgrey }}>
                  {r.matchCleanCount}/{r.matchCount} invoices matched cleanly · {r.lowestQuoteCount}/{r.quoteCount} quotes competitive
                </span>],
          ['Compliance /20', r.complianceScore ?? '—',
            <span style={{ fontSize: 12, color: T.mgrey }}>
              {r.coreDocsValid}/{r.coreDocsRequired} core documents current · {r.giftCount} gift(s) declared
              {r.conflictFound ? ' · conflict of interest on file' : ''}
            </span>],
        ]}
      />

      <div style={{ marginTop: 12, padding: '10px 12px', background: T.offwt, borderRadius: 8, display: 'flex', gap: 14, flexWrap: 'wrap' }}>
        <span style={{ fontSize: 12, color: T.mgrey }}>Orders in period <b>{r.poCount}</b></span>
        <span style={{ fontSize: 12, color: T.mgrey }}>Quotes <b>{r.quoteCount}</b></span>
        <span style={{ fontSize: 12, color: T.mgrey }}>Reviewed <b>{fmtDate(r.reviewDate)}</b></span>
      </div>

      {r.recommendBlacklist && (
        <div style={{ marginTop: 14, padding: 10, border: `1px solid ${T.redL}`, borderRadius: 8 }}>
          <p style={{ fontSize: 12, color: T.red, marginTop: 0 }}>
            Scored below 40 — this must be escalated to the MD. Blacklisting itself remains an MD action in the
            Suppliers tab, where a reason is mandatory; this review does not blacklist on its own.
          </p>
          {r.mdEscalatedAt
            ? <p style={{ fontSize: 12, color: T.mgrey, margin: 0 }}>Escalated {fmtDate(r.mdEscalatedAt)}.</p>
            : (
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
                <input placeholder="Notes for the MD (optional)" value={notes} onChange={e => setNotes(e.target.value)}
                  style={{ height: 36, flex: '1 1 220px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
                <Btn size="sm" variant="danger" disabled={busy}
                  onClick={() => run(() => proc.escalateReview(r.id, { notes: notes || undefined }), 'Escalated to the MD.')}>
                  Escalate to MD
                </Btn>
              </div>
            )}
        </div>
      )}

      <div style={{ marginTop: 16, paddingTop: 14, borderTop: `1px solid ${T.lgrey}` }}>
        <Btn size="sm" variant="outline" disabled={busy}
          onClick={() => run(() => proc.runSupplierReview(r.supplierId, { period: r.reviewPeriod }), 'Re-scored.')}>
          Re-score this supplier
        </Btn>
      </div>
    </Modal>
  )
}
