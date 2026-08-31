import { useState, useEffect } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Tabs, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Requisitions — ported from the QSL Next.js reference (RequisitionsModule +
// api/requisitions/route.js), not screenshots (none exist yet): the real
// lifecycle (pending_approval -> approved -> issuing -> closed, or ->
// rejected), the 2-level supervisor->store_manager approval chain (default
// requisitions.approval_levels), and the issue-against-stock flow all mirror
// that source. Distinct from Procurement's Purchase Requisitions — this is
// "give me N of item X already in the store," not buying from a supplier.
// UI-only shell: MOCK item/location catalog (self-contained here; Stores'
// own catalog is a separate page with its own local state in this app).
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const APPROVAL_LEVELS = ['supervisor', 'store_manager']
const CURRENT_USER = 'Henry Adar'

const MOCK_ITEMS = [
  { id: 'item_1', code: 'CAL-001', name: 'Digital Vernier Caliper', unit: 'each' },
  { id: 'item_2', code: 'PPE-014', name: 'Safety Helmet', unit: 'each' },
  { id: 'item_3', code: 'STA-007', name: 'A4 Paper Ream', unit: 'ream' },
  { id: 'item_4', code: 'CAL-018', name: 'Pressure Gauge Calibrator', unit: 'each' },
]
const MOCK_LOCATIONS = [
  { id: 'loc_1', name: 'Nairobi HQ Main Store' },
  { id: 'loc_2', name: 'Kisumu Branch Store' },
  { id: 'loc_3', name: 'Calibration Lab Store' },
  { id: 'loc_4', name: 'Site Stock - Mobile' },
]
// Mock available balances per item/location — Requisitions has its own
// self-contained catalog (see file header), so this stands in for a live
// stock lookup until these modules share state.
const INITIAL_STOCK = { item_1: { loc_1: 12, loc_3: 5 }, item_2: { loc_1: 40 }, item_3: { loc_1: 25, loc_2: 10 }, item_4: { loc_3: 3 } }
const availableAt = (stock, itemId, locationId) => stock[itemId]?.[locationId] || 0
const availableTotal = (stock, itemId) => Object.values(stock[itemId] || {}).reduce((s, q) => s + q, 0)

const STATUS_BADGE = { pending_approval: 'amber', approved: 'blue', issuing: 'amber', closed: 'green', rejected: 'red' }

export default function RequisitionsPage() {
  const [tab, setTab] = useState('list')
  const [requisitions, setRequisitions] = useState([])
  const [stock, setStock] = useState(INITIAL_STOCK)
  const [createOpen, setCreateOpen] = useState(false)
  const [detailFor, setDetailFor] = useState(null)
  const [msg, setMsg] = useState(null)
  const [tabLoading, setTabLoading] = useState(false)

  useEffect(() => {
    setTabLoading(true)
    const t = setTimeout(() => setTabLoading(false), 400)
    return () => clearTimeout(t)
  }, [tab])

  const pendingApproval = requisitions.filter(r => r.status === 'pending_approval')
  const detail = requisitions.find(r => r.id === detailFor) || null

  function createRequisition(form) {
    const reqNo = `SREQ-${String(requisitions.length + 1).padStart(5, '0')}`
    const lines = form.lines.filter(l => l.itemId && l.quantity).map(l => ({ itemId: l.itemId, quantityRequested: Number(l.quantity), quantityIssued: 0 }))
    setRequisitions(rs => [...rs, {
      id: reqNo, reqNo, department: form.department, purpose: form.purpose, priority: form.priority,
      status: 'pending_approval', currentApproverRole: APPROVAL_LEVELS[0], requestedBy: CURRENT_USER,
      createdAt: new Date().toISOString(), lines, approvals: [], rejectionReason: null,
    }])
    setCreateOpen(false)
    setMsg({ type: 'success', text: `Requisition ${reqNo} submitted for approval.` })
  }

  function approve(req) {
    const idx = APPROVAL_LEVELS.indexOf(req.currentApproverRole)
    const nextLevel = APPROVAL_LEVELS[idx + 1]
    const approvalEntry = { level: req.currentApproverRole, approverName: CURRENT_USER, decision: 'approved', comments: null, decidedAt: new Date().toISOString() }
    setRequisitions(rs => rs.map(r => r.id !== req.id ? r : {
      ...r, approvals: [...r.approvals, approvalEntry],
      status: nextLevel ? 'pending_approval' : 'approved',
      currentApproverRole: nextLevel || r.currentApproverRole,
    }))
    setMsg({ type: 'success', text: nextLevel ? `Approved at this level. Awaiting ${nextLevel}.` : 'Fully approved — ready for issuance.' })
  }

  function reject(req, reason) {
    const approvalEntry = { level: req.currentApproverRole, approverName: CURRENT_USER, decision: 'rejected', comments: reason, decidedAt: new Date().toISOString() }
    setRequisitions(rs => rs.map(r => r.id !== req.id ? r : { ...r, status: 'rejected', rejectionReason: reason, approvals: [...r.approvals, approvalEntry] }))
    setDetailFor(null)
    setMsg({ type: 'success', text: 'Requisition rejected.' })
  }

  function issueLine(req, itemId, locationId, qty) {
    if (!locationId || !qty) { setMsg({ type: 'error', text: 'Select a location and quantity' }); return }
    const available = availableAt(stock, itemId, locationId)
    if (available < qty) { setMsg({ type: 'error', text: `Insufficient stock — available: ${available}` }); return }
    setStock(s => ({ ...s, [itemId]: { ...s[itemId], [locationId]: available - qty } }))
    setRequisitions(rs => rs.map(r => r.id !== req.id ? r : {
      ...r,
      status: r.status === 'approved' ? 'issuing' : r.status,
      lines: r.lines.map(l => l.itemId === itemId ? { ...l, quantityIssued: (l.quantityIssued || 0) + qty } : l),
    }))
    setMsg({ type: 'success', text: 'Issued.' })
  }

  function closeRequisition(req) {
    setRequisitions(rs => rs.map(r => r.id !== req.id ? r : { ...r, status: 'closed' }))
    setDetailFor(null)
    setMsg({ type: 'success', text: 'Requisition closed.' })
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}
        <Alert type="info">Internal store requisitions — request items already in stock. Routes through approval, then the store issues against it, and it closes once every line is fully issued.</Alert>

        <div style={{ ...KPI_GRID, marginBottom: 22 }}>
          <Kpi label="Total Requisitions" value={requisitions.length} icon="📝" />
          <Kpi label="Pending My Approval" value={pendingApproval.length} icon="⏳" variant={pendingApproval.length ? 'amber' : 'green'} />
          <Kpi label="Approved / Issuing" value={requisitions.filter(r => ['approved', 'issuing'].includes(r.status)).length} icon="📦" />
          <Kpi label="Closed" value={requisitions.filter(r => r.status === 'closed').length} icon="✅" variant="green" />
        </div>

        <Tabs tabs={[
          { id: 'list', label: 'All Requisitions' },
          { id: 'pending', label: `Pending My Approval (${pendingApproval.length})` },
        ]} active={tab} setActive={setTab} />

        <div style={{ display: 'flex', justifyContent: 'flex-end', margin: '12px 0' }}>
          <Btn size="sm" onClick={() => setCreateOpen(true)}>+ New Requisition</Btn>
        </div>

        {tabLoading ? <Loading /> : tab === 'list' ? (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Req No.', 'Department', 'Purpose', 'Priority', 'Lines', 'Status', 'Created', 'Action']}
              rows={requisitions.map(r => [
                <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{r.reqNo}</span>,
                r.department, <span style={{ fontSize: 12 }}>{r.purpose}</span>,
                <Badge variant={r.priority === 'urgent' ? 'red' : 'default'}>{r.priority}</Badge>,
                r.lines.length,
                <Badge variant={STATUS_BADGE[r.status] || 'default'}>{r.status.replace('_', ' ')}</Badge>,
                fmt.date(r.createdAt),
                <Btn size="sm" variant="ghost" onClick={() => setDetailFor(r.id)}>View</Btn>,
              ])}
            />
          </Card>
        ) : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Req No.', 'Department', 'Requested By', 'Priority', 'Lines', 'Action']}
              empty="Nothing pending your approval."
              rows={pendingApproval.map(r => [
                <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{r.reqNo}</span>,
                r.department, r.requestedBy,
                <Badge variant={r.priority === 'urgent' ? 'red' : 'default'}>{r.priority}</Badge>, r.lines.length,
                <Btn size="sm" variant="gold" onClick={() => setDetailFor(r.id)}>Review</Btn>,
              ])}
            />
          </Card>
        )}

        {createOpen && <NewRequisitionModal stock={stock} onClose={() => setCreateOpen(false)} onSubmit={createRequisition} />}

        {detail && (
          <RequisitionDetailModal detail={detail} stock={stock} onClose={() => setDetailFor(null)}
            onApprove={() => approve(detail)} onReject={(reason) => reject(detail, reason)}
            onIssueLine={(itemId, locationId, qty) => issueLine(detail, itemId, locationId, qty)}
            onCloseRequisition={() => closeRequisition(detail)} />
        )}
      </div>
    </>
  )
}

function NewRequisitionModal({ stock, onClose, onSubmit }) {
  const [f, setF] = useState({ department: '', purpose: '', priority: 'normal', lines: [{ itemId: '', quantity: '' }] })
  const canSubmit = f.department.trim().length > 0 && f.purpose.trim().length > 0 && f.lines.some(l => l.itemId && l.quantity)

  function updateLine(i, field, value) {
    setF(prev => ({ ...prev, lines: prev.lines.map((l, idx) => idx === i ? { ...l, [field]: value } : l) }))
  }

  return (
    <Modal title="New Store Requisition" onClose={onClose} width={620}>
      <Input label="Department" value={f.department} onChange={v => setF({ ...f, department: v })} required />
      <Input label="Purpose" value={f.purpose} onChange={v => setF({ ...f, purpose: v })} required />
      <Select label="Priority" value={f.priority} onChange={v => setF({ ...f, priority: v })} options={[{ value: 'normal', label: 'Normal' }, { value: 'urgent', label: 'Urgent' }]} />

      <div style={{ fontSize: 12, fontWeight: 700, color: T.navy, margin: '12px 0 6px' }}>Items Requested</div>
      {f.lines.map((line, i) => (
        <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 8, alignItems: 'flex-end' }}>
          <div style={{ flex: 2 }}>
            <Select label={i === 0 ? 'Item' : ''} value={line.itemId} onChange={v => updateLine(i, 'itemId', v)}
              options={[{ value: '', label: 'Select...' }, ...MOCK_ITEMS.map(it => ({ value: it.id, label: `${it.code} — ${it.name} (${availableTotal(stock, it.id)} ${it.unit} avail.)` }))]} />
          </div>
          <div style={{ flex: 1 }}>
            <Input label={i === 0 ? 'Quantity' : ''} type="number" value={line.quantity} onChange={v => updateLine(i, 'quantity', v)} />
          </div>
        </div>
      ))}
      <Btn size="sm" variant="ghost" onClick={() => setF({ ...f, lines: [...f.lines, { itemId: '', quantity: '' }] })}>+ Add Line</Btn>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 16 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn disabled={!canSubmit} onClick={() => onSubmit(f)}>Submit for Approval</Btn>
      </div>
    </Modal>
  )
}

function RequisitionDetailModal({ detail, stock, onClose, onApprove, onReject, onIssueLine, onCloseRequisition }) {
  const [issueForm, setIssueForm] = useState({ itemId: '', locationId: '', quantity: '' })
  const allIssued = detail.lines.every(l => (l.quantityIssued || 0) >= l.quantityRequested)

  return (
    <Modal title={`${detail.reqNo} — ${detail.department}`} onClose={onClose} width={700}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 14 }}>
        <div>
          <div style={{ fontSize: 13, color: T.mgrey }}>{detail.purpose}</div>
          <div style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>Requested by {detail.requestedBy} · {fmt.date(detail.createdAt)}</div>
        </div>
        <Badge variant={STATUS_BADGE[detail.status] || 'default'}>{detail.status.replace('_', ' ')}</Badge>
      </div>

      <SectionHeader title="Items" />
      <DataTable headers={['Item', 'Requested', 'Issued', 'Available', 'Action']}
        rows={detail.lines.map(l => {
          const item = MOCK_ITEMS.find(it => it.id === l.itemId)
          const canIssue = detail.status === 'approved' || detail.status === 'issuing'
          return [
            <span><strong>{item?.code}</strong> {item?.name}</span>,
            `${l.quantityRequested} ${item?.unit}`,
            <strong style={{ color: (l.quantityIssued || 0) >= l.quantityRequested ? T.green : T.amber }}>{l.quantityIssued || 0} {item?.unit}</strong>,
            availableTotal(stock, l.itemId),
            canIssue ? (
              <div style={{ display: 'flex', gap: 4 }}>
                <select style={{ fontSize: 11, padding: 4, borderRadius: 4, border: `1px solid ${T.lgrey}` }}
                  onChange={e => setIssueForm({ ...issueForm, itemId: l.itemId, locationId: e.target.value })}>
                  <option value="">Location…</option>
                  {MOCK_LOCATIONS.map(loc => <option key={loc.id} value={loc.id}>{loc.name}</option>)}
                </select>
                <input style={{ fontSize: 11, padding: 4, width: 60, borderRadius: 4, border: `1px solid ${T.lgrey}` }} type="number" placeholder="Qty"
                  onChange={e => setIssueForm({ ...issueForm, itemId: l.itemId, quantity: e.target.value })} />
                <Btn size="sm" onClick={() => onIssueLine(l.itemId, issueForm.locationId, Number(issueForm.quantity))}>Issue</Btn>
              </div>
            ) : '—',
          ]
        })}
      />

      <SectionHeader title="Approval History" sub="Complete audit trail of every decision" />
      {detail.approvals.length === 0 ? <p style={{ fontSize: 12, color: T.mgrey }}>No approval actions yet.</p> : (
        detail.approvals.map((a, i) => (
          <div key={i} style={{ padding: '8px 0', borderBottom: `1px solid ${T.lgrey}`, fontSize: 12, display: 'flex', justifyContent: 'space-between' }}>
            <span>{a.level} — <strong>{a.approverName}</strong> {a.decision} {a.comments ? `("${a.comments}")` : ''}</span>
            <span style={{ color: T.mgrey }}>{fmt.date(a.decidedAt)}</span>
          </div>
        ))
      )}

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 16 }}>
        {detail.status === 'pending_approval' && <>
          <Btn variant="ghost" onClick={() => { const reason = window.prompt('Reason for rejection:'); if (reason) onReject(reason) }}>Reject</Btn>
          <Btn onClick={onApprove}>Approve</Btn>
        </>}
        {detail.status === 'issuing' && allIssued && <Btn onClick={onCloseRequisition}>Close Requisition</Btn>}
      </div>
    </Modal>
  )
}
