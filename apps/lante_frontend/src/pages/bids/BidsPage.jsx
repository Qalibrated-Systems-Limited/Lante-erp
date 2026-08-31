import { useState } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, SectionHeader, DataTable, Modal, Input } from '../../components/ui.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Bids & Pre-Sales — ported from the QSL Next.js reference (BidsModule +
// api/bids/route.js), not just the screenshots: the 12 real CSE-001..CSE-012
// Stage 2B requirements, their mandatory/scored/conditional types, and the
// PSB-004 auto-stop rule (any mandatory DOES NOT MEET → bid STOPPED,
// permanently — the real API never un-stops a bid) all mirror that source.
// UI-only shell: MOCK bid list.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'

const REQUIREMENTS = [
  { code: 'CSE-001', name: 'NCA Registration (applicable category)', type: 'mandatory' },
  { code: 'CSE-002', name: 'Tax Compliance Certificate — current', type: 'mandatory' },
  { code: 'CSE-003', name: 'ISO Accreditation (where required)', type: 'mandatory' },
  { code: 'CSE-004', name: 'Minimum Turnover threshold met', type: 'mandatory' },
  { code: 'CSE-005', name: 'Bid Bond / Tender Security capable', type: 'mandatory' },
  { code: 'CSE-006', name: 'EBK Practising Certificate', type: 'scored' },
  { code: 'CSE-007', name: 'Proof of Similar Works (3 references)', type: 'scored' },
  { code: 'CSE-008', name: 'Key Personnel CVs submitted', type: 'scored' },
  { code: 'CSE-009', name: 'Financial Statements (3 years)', type: 'conditional' },
  { code: 'CSE-010', name: 'Insurance — Professional Indemnity', type: 'scored' },
  { code: 'CSE-011', name: 'Sub-contracting plan (if applicable)', type: 'conditional' },
  { code: 'CSE-012', name: 'HSE Policy and Method Statement', type: 'mandatory' },
]
const POSITIONS = ['MEETS', 'WILL MEET', 'DOES NOT MEET', 'PENDING']
const posColor = { MEETS: 'green', 'WILL MEET': 'amber', 'DOES NOT MEET': 'red', PENDING: 'default' }
const posRowBg = { MEETS: 'greenL', 'WILL MEET': 'amberL', 'DOES NOT MEET': 'redL', PENDING: 'offwt' }
const typeBadge = { mandatory: 'red', scored: 'blue', conditional: 'amber' }

export default function BidsPage() {
  const [bids, setBids] = useState([])
  const [selectedRef, setSelectedRef] = useState(null)
  const [newBidOpen, setNewBidOpen] = useState(false)
  const [msg, setMsg] = useState(null)

  const totalBids = bids.length
  const pipeline = bids.reduce((sum, b) => sum + (Number(b.value) || 0), 0)
  const stage2bClear = bids.filter(b => b.complianceClear).length
  const stopped = bids.filter(b => b.stopped).length
  const selected = bids.find(b => b.ref === selectedRef) || null

  function handleCreateBid(bid) {
    const compliance = REQUIREMENTS.map(r => ({ ...r, position: 'PENDING' }))
    setBids(bs => [...bs, { ...bid, ref: `BID-${String(bs.length + 1).padStart(3, '0')}`, stage: 'Stage 2B', compliance, stopped: false, stoppedReason: null, complianceClear: false }])
    setNewBidOpen(false)
  }

  function updateCompliance(code, position) {
    setBids(bs => bs.map(b => {
      if (b.ref !== selectedRef) return b
      const compliance = b.compliance.map(c => c.code === code ? { ...c, position } : c)
      const failedMandatory = compliance.find(c => c.type === 'mandatory' && c.position === 'DOES NOT MEET')
      if (failedMandatory) {
        setMsg({ type: 'error', text: `🛑 BID STOPPED: Mandatory requirement failed: ${failedMandatory.name}. PSB-004 — mandatory DOES NOT MEET.` })
        return { ...b, compliance, stopped: true, stoppedReason: `Mandatory requirement failed: ${failedMandatory.name}` }
      }
      const allChecked = compliance.every(c => c.position !== 'PENDING')
      return { ...b, compliance, complianceClear: allChecked && !b.stopped }
    }))
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}

        {selected ? (
          <BidDetail bid={selected} onBack={() => setSelectedRef(null)} onUpdate={updateCompliance} />
        ) : (
          <>
            <Alert type="error"><strong>PSB-004:</strong> Stage 2B gate enforced. DOES NOT MEET on any mandatory = auto-STOP. No bypass possible.</Alert>

            <div style={{ ...KPI_GRID, marginBottom: 22 }}>
              <Kpi label="Total Bids" value={totalBids} icon="📋" />
              <Kpi label="Pipeline" value={fmt.kes(pipeline)} icon="💼" />
              <Kpi label="Stage 2B Clear" value={stage2bClear} icon="✅" variant="green" />
              <Kpi label="Stopped" value={stopped} icon="🛑" variant="red" />
            </div>

            <SectionHeader title="Bid Pipeline" action={<Btn onClick={() => setNewBidOpen(true)}>+ New Bid</Btn>} />

            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Ref', 'Bid Name', 'Client', 'Value', 'Stage', 'Compliance', 'Deadline', 'Action']}
                rows={bids.map(b => [
                  <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{b.ref}</span>,
                  <strong>{b.name}</strong>, b.entity, <strong>{fmt.kes(Number(b.value) || 0)}</strong>,
                  <Badge variant={b.stopped ? 'red' : 'blue'}>{b.stopped ? 'STOPPED' : b.stage}</Badge>,
                  <Badge variant={b.complianceClear ? 'green' : b.stopped ? 'red' : 'default'}>{b.complianceClear ? '✅ CLEAR' : b.stopped ? '🛑 STOP' : '⏳ Pending'}</Badge>,
                  b.deadline ? fmt.date(b.deadline) : '—',
                  <Btn variant="ghost" size="sm" onClick={() => setSelectedRef(b.ref)}>Stage 2B</Btn>,
                ])}
              />
            </Card>
          </>
        )}

        {newBidOpen && <NewBidModal onClose={() => setNewBidOpen(false)} onSubmit={handleCreateBid} />}
      </div>
    </>
  )
}

function BidDetail({ bid, onBack, onUpdate }) {
  return (
    <>
      <span onClick={onBack} style={{ color: T.navy, fontWeight: 600, fontSize: 13, cursor: 'pointer', display: 'inline-block', marginBottom: 16 }}>← Back to Bids</span>

      {bid.stopped && <Alert type="error">🛑 BID STOPPED — PSB-004: Mandatory requirement DOES NOT MEET. Reason: {bid.stoppedReason}</Alert>}

      <Card style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div>
            <div style={{ fontSize: 16, fontWeight: 800, color: T.navy }}>{bid.name}</div>
            <div style={{ fontSize: 12, color: T.mgrey }}>{bid.entity}</div>
          </div>
          <div style={{ textAlign: 'right' }}>
            <Badge variant={bid.stopped ? 'red' : bid.complianceClear ? 'green' : 'blue'}>{bid.stopped ? 'STOPPED' : bid.stage}</Badge>
            <div style={{ fontSize: 13, fontWeight: 700, color: T.navy, marginTop: 6 }}>{fmt.kes(Number(bid.value) || 0)}</div>
          </div>
        </div>
      </Card>

      <Card>
        <SectionHeader title="Stage 2B Compliance Matrix — CSE-001 to CSE-012" />
        <Alert type="warning">PSB-004: DOES NOT MEET on any mandatory item = immediate automatic STOP. Cannot be bypassed.</Alert>
        {bid.compliance.map(c => (
          <div key={c.code} style={{
            display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '9px 12px',
            marginBottom: 8, borderRadius: 8, background: T[posRowBg[c.position]] || T.offwt,
          }}>
            <div style={{ flex: 1, marginRight: 12 }}>
              <div style={{ fontSize: 12, fontWeight: 600 }}>{c.name}</div>
              <div style={{ marginTop: 4 }}><Badge variant={typeBadge[c.type]}>{c.type}</Badge></div>
            </div>
            <div style={{ display: 'flex', gap: 4 }}>
              {POSITIONS.map(p => (
                <button key={p} onClick={() => onUpdate(c.code, p)} style={{
                  padding: '3px 7px', borderRadius: 5, fontSize: 10, fontWeight: 600,
                  border: `1px solid ${c.position === p ? T[posColor[p]] || T.navy : T.lgrey}`,
                  background: c.position === p ? (T[posColor[p]] || T.lgrey) : T.white,
                  color: c.position === p ? T.white : T.mgrey, cursor: 'pointer',
                }}>{p}</button>
              ))}
            </div>
          </div>
        ))}
      </Card>
    </>
  )
}

function NewBidModal({ onClose, onSubmit }) {
  const today = new Date().toISOString().slice(0, 10)
  const [f, setF] = useState({ name: '', entity: '', value: '', deadline: today })
  const canSubmit = f.name.trim().length > 0 && f.entity.trim().length > 0
  return (
    <Modal title="New Bid" onClose={onClose} width={480}>
      <Alert type="info">12-requirement Stage 2B checklist auto-generated on creation.</Alert>
      <Input label="Bid Name" value={f.name} onChange={v => setF({ ...f, name: v })} required />
      <Input label="Procuring Entity" value={f.entity} onChange={v => setF({ ...f, entity: v })} required />
      <Input label="Estimated Value (Kshs)" type="number" value={f.value} onChange={v => setF({ ...f, value: v })} />
      <Input label="Submission Deadline" type="date" value={f.deadline} onChange={v => setF({ ...f, deadline: v })} />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn disabled={!canSubmit} onClick={() => onSubmit({ ...f, name: f.name.trim(), entity: f.entity.trim() })}>Create + Generate Checklist</Btn>
      </div>
    </Modal>
  )
}
