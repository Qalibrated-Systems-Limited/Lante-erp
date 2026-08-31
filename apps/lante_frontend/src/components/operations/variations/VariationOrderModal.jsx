import { useState, useMemo } from 'react'
import { Modal, Input, Select, Btn, Alert } from '../../ui.jsx'

const CATEGORIES = ['Labour', 'Materials', 'Equipment', 'Fleet', 'Subcontractor', 'Other']
const money = (n) => (Number(n) || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

// O11.3 — create / edit a variation order with line items and live subtotal/VAT/total.
export default function VariationOrderModal({ vo, onClose, onSave }) {
  const editing = !!vo?.id
  const [f, setF] = useState({
    title:          vo?.title ?? '',
    reason:         vo?.reason ?? '',
    description:    vo?.description ?? '',
    isBillable:     vo?.isBillable ?? true,
    budgetCategory: vo?.budgetCategory ?? 'Other',
    vatRate:        vo?.vatRate ?? 0.16,
  })
  const [lines, setLines] = useState(
    vo?.lines?.length
      ? vo.lines.map(l => ({ description: l.description, quantity: l.quantity, unitPrice: l.unitPrice }))
      : [{ description: '', quantity: 1, unitPrice: '' }],
  )
  const [busy, setBusy]   = useState(false)
  const [error, setError] = useState('')
  const set = (k) => (v) => setF(prev => ({ ...prev, [k]: v }))

  const totals = useMemo(() => {
    const subtotal = lines.reduce((s, l) => s + (Number(l.quantity) || 0) * (Number(l.unitPrice) || 0), 0)
    const vat = subtotal * (Number(f.vatRate) || 0)
    return { subtotal, vat, total: subtotal + vat }
  }, [lines, f.vatRate])

  const setLine = (i, k, v) => setLines(prev => prev.map((l, j) => (j === i ? { ...l, [k]: v } : l)))
  const addLine = () => setLines(prev => [...prev, { description: '', quantity: 1, unitPrice: '' }])
  const rmLine  = (i) => setLines(prev => prev.filter((_, j) => j !== i))

  const submit = async () => {
    if (!f.title.trim()) { setError('A title is required.'); return }
    const clean = lines.filter(l => l.description.trim() && Number(l.unitPrice) > 0)
    if (clean.length === 0) { setError('Add at least one line item with a description and price.'); return }
    setBusy(true); setError('')
    try {
      await onSave({
        title: f.title.trim(), reason: f.reason.trim() || null, description: f.description.trim() || null,
        isBillable: f.isBillable, budgetCategory: f.budgetCategory, vatRate: Number(f.vatRate) || 0.16,
        lines: clean.map(l => ({ description: l.description.trim(), quantity: Number(l.quantity) || 1, unitPrice: Number(l.unitPrice) })),
      }, vo?.id)
      onClose()
    } catch (e) {
      setError(e?.response?.data?.message || 'Could not save the variation order.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={editing ? `Edit ${vo.number}` : 'New Variation Order'} onClose={onClose} width={720}>
      {error && <Alert type="error">{error}</Alert>}
      <Input label="Title" value={f.title} onChange={set('title')} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <Input label="Reason" value={f.reason} onChange={set('reason')} />
        <Select label="Budget category" value={f.budgetCategory} onChange={set('budgetCategory')}
          options={CATEGORIES.map(c => ({ value: c, label: c }))} />
      </div>
      <Input label="Description" value={f.description} onChange={set('description')} />

      <div style={{ fontSize: 12, fontWeight: 600, color: '#5b6b7c', margin: '10px 0 6px' }}>Line items</div>
      {lines.map((l, i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 70px 100px 90px 28px', gap: 8, alignItems: 'center', marginBottom: 8 }}>
          <input placeholder="Description" value={l.description} onChange={e => setLine(i, 'description', e.target.value)}
            style={{ padding: '8px 10px', border: '1.5px solid #e6eaee', borderRadius: 6, fontSize: 13 }} />
          <input type="number" placeholder="Qty" value={l.quantity} onChange={e => setLine(i, 'quantity', e.target.value)}
            style={{ padding: '8px 10px', border: '1.5px solid #e6eaee', borderRadius: 6, fontSize: 13 }} />
          <input type="number" placeholder="Unit price" value={l.unitPrice} onChange={e => setLine(i, 'unitPrice', e.target.value)}
            style={{ padding: '8px 10px', border: '1.5px solid #e6eaee', borderRadius: 6, fontSize: 13 }} />
          <span style={{ fontSize: 12, color: '#5b6b7c', textAlign: 'right' }}>{money((Number(l.quantity) || 0) * (Number(l.unitPrice) || 0))}</span>
          <button onClick={() => rmLine(i)} disabled={lines.length === 1} style={{ background: 'none', border: 'none', color: '#c0392b', cursor: 'pointer', fontSize: 16 }}>×</button>
        </div>
      ))}
      <Btn size="sm" variant="ghost" onClick={addLine}>+ Add line</Btn>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 14, flexWrap: 'wrap', gap: 12 }}>
        <label style={{ fontSize: 12, color: '#5b6b7c', display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
          <input type="checkbox" checked={f.isBillable} onChange={e => set('isBillable')(e.target.checked)} />
          Billable to client
        </label>
        <div style={{ textAlign: 'right', fontSize: 13, color: '#5b6b7c' }}>
          <div>Subtotal: <b>{money(totals.subtotal)}</b></div>
          <div>VAT ({Math.round((Number(f.vatRate) || 0) * 100)}%): {money(totals.vat)}</div>
          <div style={{ color: '#1b3a5c', fontSize: 15, fontWeight: 800 }}>Total: {money(totals.total)}</div>
        </div>
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 14 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn variant="primary" onClick={submit} disabled={busy}>{busy ? 'Saving…' : editing ? 'Save' : 'Create'}</Btn>
      </div>
    </Modal>
  )
}
