import { useState } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Modal, Alert, Input, Btn } from '../ui.jsx'
import { createJournal } from '../../services/finance.js'

const cell = { padding: '9px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, boxSizing: 'border-box', width: '100%' }
const emptyLine = () => ({ accountId: '', debit: '', credit: '' })

// New journal (FIN-002) — preparer drafts a balanced entry; two other users review + approve to post.
export default function JournalModal({ accounts, onClose, onCreated, notify }) {
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10))
  const [desc, setDesc] = useState('')
  const [lines, setLines] = useState([emptyLine(), emptyLine()])
  const [accrual, setAccrual] = useState(false)
  const [autoReverse, setAutoReverse] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const postable = accounts.filter(a => a.isDirectPosting)
  const totalDr = lines.reduce((s, l) => s + (+l.debit || 0), 0)
  const totalCr = lines.reduce((s, l) => s + (+l.credit || 0), 0)
  const balanced = Math.round(totalDr * 100) === Math.round(totalCr * 100) && totalDr > 0
  const filled = lines.filter(l => l.accountId && ((+l.debit || 0) > 0 || (+l.credit || 0) > 0))
  const canSave = !!desc && !!date && balanced && filled.length >= 2

  const setLine = (i, patch) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)))

  async function save() {
    setSaving(true); setError('')
    try {
      await createJournal({
        entryDate: date,
        description: desc,
        isAccrual: accrual,
        autoReverseDate: accrual && autoReverse ? autoReverse : null,
        lines: filled.map(l => ({ accountId: l.accountId, debit: +l.debit || 0, credit: +l.credit || 0 })),
      })
      notify?.('Journal draft created — awaiting review.')
      onCreated?.()
      onClose()
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to create journal.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title="New Journal Entry (FIN-002)" onClose={onClose} width={640}>
      <Alert type="info">Drafted by you (preparer). It must be reviewed and approved by two other users before it posts.</Alert>
      {error && <Alert type="error">{error}</Alert>}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 12 }}>
        <Input label="Date" type="date" value={date} onChange={setDate} required />
        <Input label="Description" value={desc} onChange={setDesc} required placeholder="e.g. June rent accrual" />
      </div>

      <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', margin: '4px 0 6px' }}>Lines</div>
      {lines.map((l, i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: '2.5fr 1fr 1fr 24px', gap: 8, marginBottom: 6, alignItems: 'center' }}>
          <select value={l.accountId} onChange={e => setLine(i, { accountId: e.target.value })} style={cell}>
            <option value="">Account…</option>
            {postable.map(a => <option key={a.id} value={a.id}>{a.code} — {a.name}</option>)}
          </select>
          <input placeholder="Debit" type="number" min="0" value={l.debit} onChange={e => setLine(i, { debit: e.target.value, credit: '' })} style={cell} />
          <input placeholder="Credit" type="number" min="0" value={l.credit} onChange={e => setLine(i, { credit: e.target.value, debit: '' })} style={cell} />
          <button onClick={() => setLines(ls => ls.length > 2 ? ls.filter((_, j) => j !== i) : ls)}
            title="Remove line" style={{ background: 'none', border: 'none', color: T.mgrey, cursor: 'pointer', fontSize: 16 }}>×</button>
        </div>
      ))}
      <Btn size="sm" variant="ghost" onClick={() => setLines([...lines, emptyLine()])}>+ Add line</Btn>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 24, margin: '10px 0', fontSize: 13, fontWeight: 700 }}>
        <span style={{ color: T.mgrey }}>Debit: <span style={{ color: T.navy }}>{fmt.kes(totalDr)}</span></span>
        <span style={{ color: T.mgrey }}>Credit: <span style={{ color: T.navy }}>{fmt.kes(totalCr)}</span></span>
        <span style={{ color: balanced ? T.green : T.red }}>{balanced ? '✓ Balanced' : 'Unbalanced'}</span>
      </div>

      <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13 }}>
        <input type="checkbox" checked={accrual} onChange={e => setAccrual(e.target.checked)} /> Accrual — auto-reverse on a future date
      </label>
      {accrual && <div style={{ marginTop: 8 }}><Input label="Auto-reverse date" type="date" value={autoReverse} onChange={setAutoReverse} /></div>}

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 14 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={!canSave || saving}>{saving ? 'Creating…' : 'Create Draft'}</Btn>
      </div>
    </Modal>
  )
}
