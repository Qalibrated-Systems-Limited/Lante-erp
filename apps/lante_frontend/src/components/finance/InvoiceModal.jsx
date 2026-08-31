import { useState } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Modal, Alert, Input, Select, Btn } from '../ui.jsx'
import { createInvoice } from '../../services/finance.js'

const cell = { padding: '8px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, boxSizing: 'border-box', width: '100%' }
const REVENUE = [
  { value: '4100', label: '4100 · Calibration Revenue' },
  { value: '4110', label: '4110 · Engineering Revenue' },
  { value: '4120', label: '4120 · Inspection Revenue' },
  { value: '4900', label: '4900 · Other Income' },
]
const emptyLine = () => ({ description: '', quantity: 1, unitPrice: '', taxCode: 'A', revenueAccountCode: '4100' })
const addDays = (iso, d) => { const x = new Date(iso); x.setDate(x.getDate() + d); return x.toISOString().slice(0, 10) }

export default function InvoiceModal({ customers, taxCategories, onClose, onCreated, notify }) {
  const today = new Date().toISOString().slice(0, 10)
  const [customerId, setCustomerId] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(today)
  const [dueDate, setDueDate] = useState(addDays(today, 30))
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState([emptyLine()])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const rateOf = code => (taxCategories.find(t => t.code === code)?.rate ?? 0)
  const calc = l => { const sub = (+l.quantity || 0) * (+l.unitPrice || 0); const vat = sub * rateOf(l.taxCode); return { sub, vat, total: sub + vat } }
  const subtotal = lines.reduce((s, l) => s + calc(l).sub, 0)
  const vat = lines.reduce((s, l) => s + calc(l).vat, 0)
  const filled = lines.filter(l => l.description && +l.unitPrice > 0)
  const canSave = !!customerId && !!invoiceDate && filled.length > 0

  const setLine = (i, patch) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)))

  async function save() {
    setSaving(true); setError('')
    try {
      await createInvoice({
        customerId, invoiceDate, dueDate, notes,
        lines: filled.map(l => ({
          description: l.description, quantity: +l.quantity || 1, unitPrice: +l.unitPrice || 0,
          taxCode: l.taxCode, revenueAccountCode: l.revenueAccountCode,
        })),
      })
      notify?.('Invoice drafted. Issue it to post & submit to eTIMS.')
      onCreated?.(); onClose()
    } catch (e) { setError(e.response?.data?.message ?? 'Failed to create invoice.') }
    finally { setSaving(false) }
  }

  return (
    <Modal title="Create Invoice" onClose={onClose} width={680}>
      {error && <Alert type="error">{error}</Alert>}
      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 12 }}>
        <Select label="Client" value={customerId} onChange={setCustomerId} required
          options={[{ value: '', label: 'Select client…' }, ...customers.map(c => ({ value: c.id, label: c.name }))]} />
        <Input label="Invoice Date" type="date" value={invoiceDate} onChange={v => { setInvoiceDate(v); setDueDate(addDays(v, 30)) }} required />
        <Input label="Due Date" type="date" value={dueDate} onChange={setDueDate} />
      </div>

      <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', margin: '4px 0 6px' }}>Lines</div>
      {lines.map((l, i) => {
        const c = calc(l)
        return (
          <div key={i} style={{ display: 'grid', gridTemplateColumns: '2.4fr 0.7fr 1fr 0.8fr 1.4fr 0.9fr 20px', gap: 6, marginBottom: 6, alignItems: 'center' }}>
            <input placeholder="Description" value={l.description} onChange={e => setLine(i, { description: e.target.value })} style={cell} />
            <input placeholder="Qty" type="number" min="0" value={l.quantity} onChange={e => setLine(i, { quantity: e.target.value })} style={cell} />
            <input placeholder="Unit price" type="number" min="0" value={l.unitPrice} onChange={e => setLine(i, { unitPrice: e.target.value })} style={cell} />
            <select value={l.taxCode} onChange={e => setLine(i, { taxCode: e.target.value })} style={cell}>
              {taxCategories.map(t => <option key={t.code} value={t.code}>{t.code}</option>)}
            </select>
            <select value={l.revenueAccountCode} onChange={e => setLine(i, { revenueAccountCode: e.target.value })} style={cell}>
              {REVENUE.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
            </select>
            <span style={{ fontSize: 12, textAlign: 'right', color: T.dgrey }}>{fmt.kes(c.total)}</span>
            <button onClick={() => setLines(ls => ls.length > 1 ? ls.filter((_, j) => j !== i) : ls)} title="Remove"
              style={{ background: 'none', border: 'none', color: T.mgrey, cursor: 'pointer', fontSize: 16 }}>×</button>
          </div>
        )
      })}
      <Btn size="sm" variant="ghost" onClick={() => setLines([...lines, emptyLine()])}>+ Add line</Btn>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 24, margin: '10px 0', fontSize: 13 }}>
        <span style={{ color: T.mgrey }}>Subtotal: <strong style={{ color: T.navy }}>{fmt.kes(subtotal)}</strong></span>
        <span style={{ color: T.mgrey }}>VAT: <strong style={{ color: T.navy }}>{fmt.kes(vat)}</strong></span>
        <span style={{ color: T.mgrey }}>Total: <strong style={{ color: T.navy }}>{fmt.kes(subtotal + vat)}</strong></span>
      </div>
      <Input label="Notes (optional)" value={notes} onChange={setNotes} />

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={!canSave || saving}>{saving ? 'Creating…' : 'Create Invoice'}</Btn>
      </div>
    </Modal>
  )
}
