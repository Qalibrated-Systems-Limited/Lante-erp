import { useState } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Modal, Alert, Input, Select, Btn } from '../ui.jsx'
import { createSupplierInvoice } from '../../services/finance.js'

const cell = { padding: '8px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, boxSizing: 'border-box', width: '100%' }
const EXPENSE = [
  { value: '5100', label: '5100 · Cost of Services' },
  { value: '5200', label: '5200 · Salaries & Wages' },
  { value: '5300', label: '5300 · Rent & Utilities' },
  { value: '5400', label: '5400 · Motor Vehicle & Fleet' },
  { value: '5500', label: '5500 · Administrative' },
]
const emptyLine = () => ({ description: '', quantity: 1, unitPrice: '', taxCode: 'A', expenseAccountCode: '5100' })
const addDays = (iso, d) => { const x = new Date(iso); x.setDate(x.getDate() + d); return x.toISOString().slice(0, 10) }

export default function SupplierInvoiceModal({ suppliers, taxCategories, onClose, onCreated, notify }) {
  const today = new Date().toISOString().slice(0, 10)
  const [supplierId, setSupplierId] = useState('')
  const [supplierInvoiceNo, setSupplierInvoiceNo] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(today)
  const [dueDate, setDueDate] = useState(addDays(today, 30))
  const [lpo, setLpo] = useState('')
  const [lines, setLines] = useState([emptyLine()])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const rateOf = code => (taxCategories.find(t => t.code === code)?.rate ?? 0)
  const calc = l => { const sub = (+l.quantity || 0) * (+l.unitPrice || 0); return { sub, vat: sub * rateOf(l.taxCode), total: sub + sub * rateOf(l.taxCode) } }
  const subtotal = lines.reduce((s, l) => s + calc(l).sub, 0)
  const vat = lines.reduce((s, l) => s + calc(l).vat, 0)
  const filled = lines.filter(l => l.description && +l.unitPrice > 0)
  const canSave = !!supplierId && !!supplierInvoiceNo && filled.length > 0

  const setLine = (i, patch) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)))

  async function save() {
    setSaving(true); setError('')
    try {
      await createSupplierInvoice({
        supplierId, supplierInvoiceNo, invoiceDate, dueDate, lpoReference: lpo || null,
        lines: filled.map(l => ({ description: l.description, quantity: +l.quantity || 1, unitPrice: +l.unitPrice || 0, taxCode: l.taxCode, expenseAccountCode: l.expenseAccountCode })),
      })
      notify?.('Supplier invoice recorded. Approve it to post to payables.')
      onCreated?.(); onClose()
    } catch (e) { setError(e.response?.data?.message ?? 'Failed to record supplier invoice.') }
    finally { setSaving(false) }
  }

  return (
    <Modal title="New Supplier Invoice" onClose={onClose} width={680}>
      {error && <Alert type="error">{error}</Alert>}
      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 12 }}>
        <Select label="Supplier" value={supplierId} onChange={setSupplierId} required
          options={[{ value: '', label: 'Select supplier…' }, ...suppliers.map(s => ({ value: s.id, label: s.name }))]} />
        <Input label="Supplier Invoice No" value={supplierInvoiceNo} onChange={setSupplierInvoiceNo} required placeholder="Their ref" />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12 }}>
        <Input label="Invoice Date" type="date" value={invoiceDate} onChange={v => { setInvoiceDate(v); setDueDate(addDays(v, 30)) }} required />
        <Input label="Due Date" type="date" value={dueDate} onChange={setDueDate} />
        <Input label="LPO Reference (optional)" value={lpo} onChange={setLpo} placeholder="from Procurement" />
      </div>

      <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', margin: '4px 0 6px' }}>Lines</div>
      {lines.map((l, i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: '2.4fr 0.7fr 1fr 0.8fr 1.6fr 0.9fr 20px', gap: 6, marginBottom: 6, alignItems: 'center' }}>
          <input placeholder="Description" value={l.description} onChange={e => setLine(i, { description: e.target.value })} style={cell} />
          <input placeholder="Qty" type="number" min="0" value={l.quantity} onChange={e => setLine(i, { quantity: e.target.value })} style={cell} />
          <input placeholder="Unit price" type="number" min="0" value={l.unitPrice} onChange={e => setLine(i, { unitPrice: e.target.value })} style={cell} />
          <select value={l.taxCode} onChange={e => setLine(i, { taxCode: e.target.value })} style={cell}>
            {taxCategories.map(t => <option key={t.code} value={t.code}>{t.code}</option>)}
          </select>
          <select value={l.expenseAccountCode} onChange={e => setLine(i, { expenseAccountCode: e.target.value })} style={cell}>
            {EXPENSE.map(x => <option key={x.value} value={x.value}>{x.label}</option>)}
          </select>
          <span style={{ fontSize: 12, textAlign: 'right', color: T.dgrey }}>{fmt.kes(calc(l).total)}</span>
          <button onClick={() => setLines(ls => ls.length > 1 ? ls.filter((_, j) => j !== i) : ls)} title="Remove"
            style={{ background: 'none', border: 'none', color: T.mgrey, cursor: 'pointer', fontSize: 16 }}>×</button>
        </div>
      ))}
      <Btn size="sm" variant="ghost" onClick={() => setLines([...lines, emptyLine()])}>+ Add line</Btn>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 24, margin: '10px 0', fontSize: 13 }}>
        <span style={{ color: T.mgrey }}>Subtotal: <strong style={{ color: T.navy }}>{fmt.kes(subtotal)}</strong></span>
        <span style={{ color: T.mgrey }}>Input VAT: <strong style={{ color: T.navy }}>{fmt.kes(vat)}</strong></span>
        <span style={{ color: T.mgrey }}>Total: <strong style={{ color: T.navy }}>{fmt.kes(subtotal + vat)}</strong></span>
      </div>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={!canSave || saving}>{saving ? 'Recording…' : 'Record Invoice'}</Btn>
      </div>
    </Modal>
  )
}
