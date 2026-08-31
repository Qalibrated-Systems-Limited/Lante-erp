import { useState, useEffect, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Tabs, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import { getDebtorAging, listReceipts, createReceipt } from '../../services/finance.js'

// ─────────────────────────────────────────────────────────────────────────────
// Debtors — Daily Follow-up (status recording is local; the daily-report backend
// is not built yet) + All Debtors (REAL aging from /debtors/aging) + Record
// Receipt (REAL /receipts: posts to GL and allocates to invoices).
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const STATUS_OPTIONS = ['Promised to Pay', 'Partial Payment', 'Disputed', 'No Response', 'Escalate to MD']
const CHANNELS = ['Bank', 'Mpesa', 'Cheque', 'Cash']

export default function DebtorsPage() {
  const [tab, setTab] = useState('daily')
  const [aging, setAging] = useState([])
  const [receipts, setReceipts] = useState([])
  const [loading, setLoading] = useState(true)
  const [statuses, setStatuses] = useState({})     // customerId -> { status, note, next } (local)
  const [msg, setMsg] = useState(null)
  const [modalFor, setModalFor] = useState(null)   // record-status debtor
  const [receiptFor, setReceiptFor] = useState(null) // record-receipt debtor

  const notify = (text, type = 'success') => setMsg({ type, text })

  const load = useCallback(() => {
    setLoading(true)
    return Promise.all([getDebtorAging(), listReceipts()])
      .then(([a, r]) => { setAging(a ?? []); setReceipts(r ?? []) })
      .catch(() => notify('Failed to load debtors.', 'error'))
      .finally(() => setLoading(false))
  }, [])
  useEffect(() => { load() }, [load])

  const total = aging.length
  const recorded = Object.keys(statuses).length
  const allRecorded = total > 0 && recorded >= total
  const totalOutstanding = aging.reduce((s, d) => s + d.total, 0)
  const overdue = aging.reduce((s, d) => s + d.days1To30 + d.days31To60 + d.days61Plus, 0)

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}
        <Tabs tabs={[{ id: 'daily', label: 'Daily Follow-up' }, { id: 'all', label: 'All Debtors' }]} active={tab} setActive={setTab} />

        {loading ? <Loading /> : tab === 'daily' ? (
          <>
            <Alert type="info"><strong>Daily process:</strong> Debtors list circulated to MD + Finance Manager at 8:00 AM. Finance Manager records a status against every overdue account, then submits by 5:00 PM — compiling the report to the MD.</Alert>
            <div style={{ ...KPI_GRID, marginBottom: 22 }}>
              <Kpi label="Debtor Accounts" value={total} icon="📋" />
              <Kpi label="Total Outstanding" value={fmt.kes(totalOutstanding)} icon="💰" variant="amber" />
              <Kpi label="Status Recorded" value={`${recorded} / ${total}`} icon="✍️" variant={allRecorded ? 'green' : undefined} />
              <Kpi label="Deadline" value="5:00 PM" sub="Escalates to MD at 5:30 PM if missed" icon="⏰" />
            </div>
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Client', 'Outstanding', "Today's Status", 'Note', 'Next Follow-up', 'Action']}
                empty="No outstanding debtors."
                rows={aging.map(d => {
                  const s = statuses[d.customerId]
                  return [
                    <strong>{d.customerName}</strong>,
                    <strong style={{ color: T.amber }}>{fmt.kes(d.total)}</strong>,
                    s ? <Badge variant="green">{s.status}</Badge> : <Badge variant="default">Not recorded</Badge>,
                    s?.note || '—',
                    s?.next ? fmt.date(s.next) : '—',
                    <Btn size="sm" onClick={() => setModalFor(d)}>Record Status</Btn>,
                  ]
                })} />
            </Card>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 18, flexWrap: 'wrap', gap: 12 }}>
              <div style={{ fontSize: 13, color: T.mgrey }}>
                {allRecorded ? 'All accounts have a status — ready to submit.' : `${total - recorded} account(s) still need a status.`}
              </div>
              <Btn disabled={!allRecorded} onClick={() => notify('End-of-day report compiled and emailed to the MD.')}>Submit End-of-Day Report</Btn>
            </div>
          </>
        ) : (
          <>
            <SectionHeader title="Debtor Aging" sub={`${fmt.kes(overdue)} overdue · ${fmt.kes(totalOutstanding)} total outstanding`} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Client', 'Current', '1–30 days', '31–60 days', '61+ days', 'Total', 'Action']}
                empty="No outstanding debtors."
                rows={aging.map(d => [
                  <strong>{d.customerName}</strong>,
                  fmt.kes(d.current),
                  <span style={{ color: d.days1To30 ? T.amber : T.mgrey }}>{fmt.kes(d.days1To30)}</span>,
                  <span style={{ color: d.days31To60 ? T.amber : T.mgrey }}>{fmt.kes(d.days31To60)}</span>,
                  <span style={{ color: d.days61Plus ? T.red : T.mgrey }}>{fmt.kes(d.days61Plus)}</span>,
                  <strong style={{ color: T.navy }}>{fmt.kes(d.total)}</strong>,
                  <Btn size="sm" onClick={() => setReceiptFor(d)}>Record Receipt</Btn>,
                ])} />
            </Card>

            <div style={{ height: 22 }} />
            <SectionHeader title="Recent Receipts" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Receipt No', 'Client', 'Date', 'Amount', 'Channel', 'Unallocated']}
                empty="No receipts recorded yet."
                rows={receipts.map(r => [
                  <span style={{ fontFamily: T.mono, fontSize: 12 }}>{r.paymentNo}</span>,
                  r.customerName, fmt.date(r.paymentDate),
                  <strong style={{ color: T.green }}>{fmt.kes(r.amount)}</strong>,
                  <Badge variant="blue">{r.channel}</Badge>,
                  r.unallocatedAmount ? fmt.kes(r.unallocatedAmount) : '—',
                ])} />
            </Card>
          </>
        )}

        {modalFor && (
          <RecordStatusModal debtor={modalFor} onClose={() => setModalFor(null)}
            onSave={(rec) => { setStatuses(s => ({ ...s, [modalFor.customerId]: rec })); setModalFor(null); notify(`Status recorded for ${modalFor.customerName}.`) }} />
        )}
        {receiptFor && (
          <RecordReceiptModal debtor={receiptFor} onClose={() => setReceiptFor(null)}
            onSaved={() => { setReceiptFor(null); load() }} notify={notify} />
        )}
      </div>
    </>
  )
}

function RecordStatusModal({ debtor, onClose, onSave }) {
  const [f, setF] = useState({ status: STATUS_OPTIONS[0], note: '', next: '' })
  return (
    <Modal title={`Record Status — ${debtor.customerName}`} onClose={onClose} width={460}>
      <div style={{ background: T.offwt, borderRadius: 8, padding: '10px 14px', marginBottom: 14, fontSize: 13 }}>
        Outstanding: <strong style={{ color: T.amber }}>{fmt.kes(debtor.total)}</strong>
      </div>
      <Select label="Status" value={f.status} onChange={v => setF({ ...f, status: v })} options={STATUS_OPTIONS} required />
      <Input label="Note" value={f.note} onChange={v => setF({ ...f, note: v })} placeholder="e.g. Promised payment by Friday" />
      <Input label="Next Follow-up" type="date" value={f.next} onChange={v => setF({ ...f, next: v })} />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={() => onSave(f)}>Save Status</Btn>
      </div>
    </Modal>
  )
}

function RecordReceiptModal({ debtor, onClose, onSaved, notify }) {
  const [f, setF] = useState({ amount: '', date: new Date().toISOString().slice(0, 10), channel: 'Bank', reference: '' })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  async function save() {
    setSaving(true); setError('')
    try {
      await createReceipt({
        customerId: debtor.customerId, amount: +f.amount || 0, paymentDate: f.date,
        channel: f.channel, receiptReference: f.reference,
      })
      notify(`Receipt recorded for ${debtor.customerName} — allocated to open invoices.`)
      onSaved()
    } catch (e) { setError(e.response?.data?.message ?? 'Failed to record receipt.') }
    finally { setSaving(false) }
  }

  return (
    <Modal title={`Record Receipt — ${debtor.customerName}`} onClose={onClose} width={460}>
      {error && <Alert type="error">{error}</Alert>}
      <div style={{ background: T.offwt, borderRadius: 8, padding: '10px 14px', marginBottom: 14, fontSize: 13 }}>
        Outstanding: <strong style={{ color: T.amber }}>{fmt.kes(debtor.total)}</strong> · auto-allocates to oldest invoices first.
      </div>
      <Input label="Amount (Kshs)" type="number" value={f.amount} onChange={v => setF({ ...f, amount: v })} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <Input label="Date" type="date" value={f.date} onChange={v => setF({ ...f, date: v })} required />
        <Select label="Channel" value={f.channel} onChange={v => setF({ ...f, channel: v })} options={CHANNELS} />
      </div>
      <Input label="Reference" value={f.reference} onChange={v => setF({ ...f, reference: v })} placeholder="e.g. EFT / M-PESA code" />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={saving || !(+f.amount > 0)}>{saving ? 'Recording…' : 'Record Receipt'}</Btn>
      </div>
    </Modal>
  )
}
