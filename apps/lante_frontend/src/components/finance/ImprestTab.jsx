import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Modal, Input, SectionHeader, DataTable, Loading } from '../ui.jsx'
import {
  listImprest, listAdvances, requestImprest, approveImprest,
  disburseImprest, retireImprest, runImprestConversions,
} from '../../services/finance.js'

const STATUS_VARIANT = {
  Requested: 'default', Approved: 'blue', Disbursed: 'amber',
  PartlyRetired: 'amber', Retired: 'green', Converted: 'red', Rejected: 'default',
}

export default function ImprestTab({ notify }) {
  const [imprest, setImprest] = useState([])
  const [advances, setAdvances] = useState([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(null)
  const [modal, setModal] = useState(null)     // 'request' | { retire: row }

  const load = useCallback(() => {
    setLoading(true)
    return Promise.all([listImprest(), listAdvances()])
      .then(([i, a]) => { setImprest(i ?? []); setAdvances(a ?? []) })
      .catch(() => notify?.('Failed to load imprest register.', 'error'))
      .finally(() => setLoading(false))
  }, [notify])
  useEffect(() => { load() }, [load])

  async function act(id, fn, label) {
    setBusy(id)
    try { await fn(id); notify?.(label); await load() }
    catch (e) { notify?.(e.response?.data?.message ?? 'Action failed.', 'error') }
    finally { setBusy(null) }
  }
  async function runConversions() {
    setBusy('conv')
    try {
      const created = await runImprestConversions()
      notify?.(created?.length ? `${created.length} imprest(s) converted to personal advances.` : 'No overdue imprest to convert.', created?.length ? 'success' : 'info')
      await load()
    } catch (e) { notify?.(e.response?.data?.message ?? 'Conversion sweep failed.', 'error') }
    finally { setBusy(null) }
  }

  const outstanding = imprest.filter(i => i.status !== 'Converted').reduce((s, i) => s + (i.unretiredBalance || 0), 0)
  const overdue = imprest.filter(i => i.unretiredBalance > 0 && i.daysToDue != null && i.daysToDue < 0)
  const converted = imprest.filter(i => i.status === 'Converted').length
  const advancesPending = advances.filter(a => a.status === 'Pending').reduce((s, a) => s + (a.amount || 0), 0)

  const rowAction = (r) => {
    if (busy === r.id) return <span style={{ color: T.mgrey, fontSize: 12 }}>…</span>
    if (r.status === 'Requested') return <Btn size="sm" onClick={() => act(r.id, approveImprest, 'Imprest approved.')}>Approve</Btn>
    if (r.status === 'Approved') return <Btn size="sm" variant="gold" onClick={() => act(r.id, disburseImprest, 'Imprest disbursed & posted.')}>Disburse</Btn>
    if (r.status === 'Disbursed' || r.status === 'PartlyRetired') return <Btn size="sm" variant="green" onClick={() => setModal({ retire: r })}>Retire</Btn>
    return <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>
  }
  const dueCell = (r) => {
    if (!r.dueDate) return <span style={{ color: T.mgrey }}>—</span>
    const late = r.unretiredBalance > 0 && r.daysToDue < 0
    return <span style={{ color: late ? T.red : T.dgrey, fontWeight: late ? 700 : 400 }}>
      {fmt.date(r.dueDate)}{r.status !== 'Converted' && r.status !== 'Retired' && r.daysToDue != null ? ` (${r.daysToDue < 0 ? `${-r.daysToDue}d late` : `${r.daysToDue}d`})` : ''}
    </span>
  }

  if (loading) return <Loading />

  return (
    <>
      <Alert type="warning">
        <strong style={{ color: T.amber }}>LT-FIN-CHP-001:</strong> Any imprest not accounted for within <strong>14 calendar days</strong> automatically and irreversibly converts to a personal advance recovered from the next payroll. Disbursement posts Dr Staff Imprest / Cr Bank; retirement posts Dr Expense / Cr Staff Imprest.
      </Alert>
      <div style={{ ...KPI_GRID, marginBottom: 18 }}>
        <Kpi label="Outstanding (unretired)" value={fmt.kes(outstanding)} icon="💼" />
        <Kpi label="Overdue" value={overdue.length} sub={overdue.length ? fmt.kes(overdue.reduce((s, i) => s + i.unretiredBalance, 0)) : 'none'} icon="⏰" variant={overdue.length ? 'red' : undefined} />
        <Kpi label="Converted to Advance" value={converted} icon="🔁" variant={converted ? 'amber' : undefined} />
        <Kpi label="Advances Pending Payroll" value={fmt.kes(advancesPending)} icon="📉" variant={advancesPending ? 'amber' : undefined} />
      </div>

      <SectionHeader title="Imprest Register" sub={`${imprest.length} record(s) · ${overdue.length} overdue`}
        action={<div style={{ display: 'flex', gap: 8 }}>
          <Btn variant="ghost" onClick={runConversions} disabled={busy === 'conv'}>{busy === 'conv' ? 'Running…' : 'Run 14-day Conversions'}</Btn>
          <Btn onClick={() => setModal('request')}>+ Request Imprest</Btn>
        </div>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable headers={['Ref', 'Employee', 'Purpose', 'Amount', 'Retired', 'Unretired', 'Due', 'Status', 'Action']}
          empty="No imprest requested yet."
          rows={imprest.map(r => [
            <span style={{ fontFamily: T.mono, fontSize: 12 }}>{r.refNo}</span>,
            r.employeeName,
            <span style={{ fontSize: 12, color: T.dgrey }}>{r.purpose || '—'}</span>,
            <strong>{fmt.money(r.amount, r.currencyCode)}</strong>,
            <span style={{ color: T.mgrey }}>{r.retiredAmount ? fmt.money(r.retiredAmount, r.currencyCode) : '—'}</span>,
            <span style={{ color: r.unretiredBalance > 0 ? T.dgrey : T.green }}>{fmt.money(r.unretiredBalance, r.currencyCode)}</span>,
            dueCell(r),
            <Badge variant={STATUS_VARIANT[r.status] || 'default'}>{r.status}</Badge>,
            rowAction(r),
          ])} />
      </Card>

      <div style={{ height: 22 }} />
      <SectionHeader title="Personal Advances" sub="Unretired imprest recovered through payroll (HR deducts)." />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable headers={['From Imprest', 'Employee', 'Amount', 'Converted', 'Status']}
          empty="No advances — all imprest retired on time."
          rows={advances.map(a => [
            <span style={{ fontFamily: T.mono, fontSize: 12 }}>{a.imprestRef}</span>,
            a.employeeName,
            <strong style={{ color: T.red }}>{fmt.kes(a.amount)}</strong>,
            fmt.date(a.convertedAt),
            <Badge variant={a.status === 'Deducted' ? 'green' : 'amber'}>{a.status}</Badge>,
          ])} />
      </Card>

      {modal === 'request' && <RequestModal onClose={() => setModal(null)} onSaved={load} notify={notify} />}
      {modal?.retire && <RetireModal row={modal.retire} onClose={() => setModal(null)} onSaved={load} notify={notify} />}
    </>
  )
}

function RequestModal({ onClose, onSaved, notify }) {
  const [employeeName, setEmployeeName] = useState('')
  const [purpose, setPurpose] = useState('')
  const [amount, setAmount] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const canSave = employeeName.trim() && +amount > 0

  async function save() {
    setSaving(true); setError('')
    try {
      await requestImprest({ employeeName: employeeName.trim(), purpose: purpose.trim(), amount: +amount })
      notify?.('Imprest requested.'); onSaved?.(); onClose()
    } catch (e) { setError(e.response?.data?.message ?? 'Failed to request imprest.') }
    finally { setSaving(false) }
  }

  return (
    <Modal title="Request Imprest" onClose={onClose} width={460}>
      {error && <Alert type="error">{error}</Alert>}
      <Input label="Employee" value={employeeName} onChange={setEmployeeName} required placeholder="Full name" />
      <Input label="Purpose" value={purpose} onChange={setPurpose} placeholder="e.g. Site visit — Coast region" />
      <Input label="Amount (KES)" type="number" min="0" value={amount} onChange={setAmount} required />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={!canSave || saving}>{saving ? 'Requesting…' : 'Request'}</Btn>
      </div>
    </Modal>
  )
}

const cell = { padding: '8px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, boxSizing: 'border-box', width: '100%' }
const EXPENSE = [
  { value: '5100', label: '5100 · Cost of Services' },
  { value: '5300', label: '5300 · Rent & Utilities' },
  { value: '5400', label: '5400 · Motor Vehicle & Fleet' },
  { value: '5500', label: '5500 · Administrative' },
]
const emptyLine = () => ({ description: '', amount: '', expenseAccountCode: '5500', receiptUrl: '' })

function RetireModal({ row, onClose, onSaved, notify }) {
  const today = new Date().toISOString().slice(0, 10)
  const [lines, setLines] = useState([emptyLine()])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const setLine = (i, patch) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)))
  const filled = lines.filter(l => l.description && +l.amount > 0)
  const total = filled.reduce((s, l) => s + (+l.amount || 0), 0)
  const over = total > row.unretiredBalance + 0.01
  const canSave = filled.length > 0 && !over

  async function save() {
    setSaving(true); setError('')
    try {
      await retireImprest(row.id, {
        lines: filled.map(l => ({ description: l.description, amount: +l.amount, expenseAccountCode: l.expenseAccountCode, receiptUrl: l.receiptUrl || null, expenseDate: today })),
      })
      notify?.('Imprest retired & posted.'); onSaved?.(); onClose()
    } catch (e) { setError(e.response?.data?.message ?? 'Failed to retire imprest.') }
    finally { setSaving(false) }
  }

  return (
    <Modal title={`Retire ${row.refNo} — ${row.employeeName}`} onClose={onClose} width={640}>
      {error && <Alert type="error">{error}</Alert>}
      <Alert type="info">Unretired balance: <strong>{fmt.money(row.unretiredBalance, row.currencyCode)}</strong>. Enter what was spent, with receipts. Any remaining balance stays outstanding and will convert to an advance after the 14-day window.</Alert>

      <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', margin: '4px 0 6px' }}>Expenses</div>
      {lines.map((l, i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: '2.2fr 1fr 1.6fr 1.4fr 20px', gap: 6, marginBottom: 6, alignItems: 'center' }}>
          <input placeholder="Description" value={l.description} onChange={e => setLine(i, { description: e.target.value })} style={cell} />
          <input placeholder="Amount" type="number" min="0" value={l.amount} onChange={e => setLine(i, { amount: e.target.value })} style={cell} />
          <select value={l.expenseAccountCode} onChange={e => setLine(i, { expenseAccountCode: e.target.value })} style={cell}>
            {EXPENSE.map(x => <option key={x.value} value={x.value}>{x.label}</option>)}
          </select>
          <input placeholder="Receipt ref" value={l.receiptUrl} onChange={e => setLine(i, { receiptUrl: e.target.value })} style={cell} />
          <button onClick={() => setLines(ls => ls.length > 1 ? ls.filter((_, j) => j !== i) : ls)} title="Remove"
            style={{ background: 'none', border: 'none', color: T.mgrey, cursor: 'pointer', fontSize: 16 }}>×</button>
        </div>
      ))}
      <Btn size="sm" variant="ghost" onClick={() => setLines([...lines, emptyLine()])}>+ Add expense</Btn>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 24, margin: '10px 0', fontSize: 13 }}>
        <span style={{ color: T.mgrey }}>Retiring: <strong style={{ color: over ? T.red : T.navy }}>{fmt.money(total, row.currencyCode)}</strong></span>
        <span style={{ color: T.mgrey }}>Remaining after: <strong style={{ color: T.navy }}>{fmt.money(Math.max(0, row.unretiredBalance - total), row.currencyCode)}</strong></span>
      </div>
      {over && <Alert type="error">Retirement exceeds the unretired balance.</Alert>}

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={!canSave || saving}>{saving ? 'Posting…' : 'Retire & Post'}</Btn>
      </div>
    </Modal>
  )
}
