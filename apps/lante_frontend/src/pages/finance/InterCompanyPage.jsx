import { useState } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Tabs, SectionHeader, DataTable, Modal, Input, Select } from '../../components/ui.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Inter-Company — matches the deployed QSL module (IC Transactions +
// Consolidation). UI-only shell: transactions start empty; modal adds rows.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const SISTERS = [
  { value: '', label: 'Select entity…' },
  { value: 'qcl', label: 'Qalibrated Calibration Ltd' },
  { value: 'qel', label: 'Qalibrated Engineering Ltd' },
  { value: 'qfl', label: 'Qalibrated Fleet Ltd' },
]
const TX_TYPES = [
  { value: 'mgmt', label: 'Management Fee (min 5%)' },
  { value: 'accred', label: 'Accreditation Licence (3%)' },
  { value: 'shared', label: 'Shared Services' },
  { value: 'recharge', label: 'Cost Recharge' },
]
const sisterLabel = (v) => SISTERS.find(s => s.value === v)?.label || '—'

export default function InterCompanyPage() {
  const [tab, setTab] = useState('transactions')
  const [txns, setTxns] = useState([])
  const [modal, setModal] = useState(false)
  const [msg, setMsg] = useState(null)

  const totalFees = txns.reduce((s, t) => s + t.fee, 0)

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}
        <Tabs tabs={[{ id: 'transactions', label: 'IC Transactions' }, { id: 'consolidation', label: 'Consolidation' }]} active={tab} setActive={setTab} />

        {tab === 'transactions' && (
          <>
            <Alert type="warning"><strong>ICM-002/003:</strong> No IC transaction without signed ICSA. Min 5% management fee / 3% accreditation licence enforced.</Alert>
            <div style={{ ...KPI_GRID, marginBottom: 18 }}>
              <Kpi label="Total IC Fees" value={fmt.kes(totalFees)} icon="🔗" />
              <Kpi label="Collected" value={fmt.kes(0)} icon="✅" variant="green" />
              <Kpi label="Outstanding" value={fmt.kes(totalFees)} icon="⏳" variant="amber" />
              <Kpi label="Transactions" value={txns.length} icon="📊" />
            </div>
            <SectionHeader title="IC Transaction Register" action={<Btn onClick={() => setModal(true)}>+ New IC Transaction</Btn>} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Sister Company', 'Type', 'Contract Value', 'QSL Fee', 'Min Required', 'Collected', 'Outstanding', 'Status']} empty="No records found."
                rows={txns.map(t => [
                  <strong>{t.sister}</strong>, <Badge variant="navy">{t.type}</Badge>, fmt.kes(t.value),
                  <strong>{fmt.kes(t.fee)}</strong>, fmt.kes(t.value * 0.05), fmt.kes(0),
                  <strong style={{ color: T.amber }}>{fmt.kes(t.fee)}</strong>, <Badge variant="amber">outstanding</Badge>,
                ])} />
            </Card>
          </>
        )}

        {tab === 'consolidation' && (
          <>
            <Alert type="info">Group view across all companies from posted journals (each journal carries a company; pre-existing ones belong to QSL). Inter-company receivables/payables/income are eliminated — QSL's receivable from a sister company is that company's payable, so across the group they net to zero.</Alert>
            <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 10 }}>
              <span style={{ fontSize: 13, color: T.dgrey }}>As of:</span>
              <input type="date" defaultValue="2026-07-09" style={{ padding: '7px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }} />
            </div>
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Company', 'Receivables', 'Payables', 'Income', 'Eliminations', 'Net']} empty="No posted journals to consolidate yet." rows={[]} />
            </Card>
          </>
        )}

        {modal && <NewICTransactionModal onClose={() => setModal(false)} onSave={(t) => { setTxns(x => [t, ...x]); setModal(false); setMsg({ type: 'success', text: 'Inter-company transaction created.' }) }} />}
      </div>
    </>
  )
}

function NewICTransactionModal({ onClose, onSave }) {
  const [f, setF] = useState({ sister: '', type: 'mgmt', value: '', feePct: 5, icsa: false })
  const feePctNum = +f.feePct || 0
  const belowMin = feePctNum < 5
  const canCreate = f.sister && f.value && f.icsa && !belowMin
  return (
    <Modal title="New IC Transaction — ICM-002/003" onClose={onClose} width={480}>
      <Alert type="error">ICM-002: ICSA must be on file. ICM-003: Min 5% fee enforced.</Alert>
      <Select label="Sister Company" value={f.sister} onChange={v => setF({ ...f, sister: v })} required options={SISTERS} />
      <Select label="Transaction Type" value={f.type} onChange={v => setF({ ...f, type: v })} options={TX_TYPES} />
      <Input label="Contract Value (Kshs)" type="number" value={f.value} onChange={v => setF({ ...f, value: v })} required />
      <Input label="Fee % (minimum 5%)" type="number" value={f.feePct} onChange={v => setF({ ...f, feePct: v })} required note={belowMin ? 'Fee must be at least 5% (ICM-003).' : undefined} />
      <label style={{ display: 'flex', alignItems: 'center', gap: 10, background: T.amberL, border: '1px solid #FCD34D', borderRadius: 8, padding: '10px 12px', fontSize: 13, cursor: 'pointer', marginBottom: 8 }}>
        <input type="checkbox" checked={f.icsa} onChange={e => setF({ ...f, icsa: e.target.checked })} />
        ⚠️ Confirm signed ICSA is on file (ICM-002 — mandatory)
      </label>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn disabled={!canCreate} onClick={() => onSave({ sister: sisterLabel(f.sister), type: TX_TYPES.find(t => t.value === f.type)?.label, value: +f.value, fee: +f.value * (feePctNum / 100) })}>Create Transaction</Btn>
      </div>
    </Modal>
  )
}
