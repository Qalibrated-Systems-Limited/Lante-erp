import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Modal, Input, Select, SectionHeader, DataTable, Loading } from '../ui.jsx'
import {
  listReconciliations, getReconciliation, createReconciliation,
  postBankItem, completeReconciliation,
} from '../../services/finance.js'

const LINE_VARIANT = { Matched: 'green', PostedAsJournal: 'blue', Unmatched: 'amber' }
const signed = (n) => `${n < 0 ? '−' : ''}${fmt.kes(Math.abs(n))}`

export default function BankRecTab({ notify }) {
  const [recs, setRecs] = useState([])
  const [sel, setSel] = useState(null)        // full recon detail
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(null)
  const [modal, setModal] = useState(null)    // 'new' | { post: line }

  const loadList = useCallback(() => {
    setLoading(true)
    return listReconciliations()
      .then(r => setRecs(r ?? []))
      .catch(() => notify?.('Failed to load reconciliations.', 'error'))
      .finally(() => setLoading(false))
  }, [notify])
  useEffect(() => { loadList() }, [loadList])

  const open = async (id) => {
    setBusy(id)
    try { setSel(await getReconciliation(id)) }
    catch { notify?.('Failed to load reconciliation.', 'error') }
    finally { setBusy(null) }
  }
  const refreshSel = async () => { if (sel) setSel(await getReconciliation(sel.id)) }

  async function complete() {
    setBusy('complete')
    try { await completeReconciliation(sel.id); notify?.('Reconciliation completed.'); await refreshSel(); await loadList() }
    catch (e) { notify?.(e.response?.data?.message ?? 'Cannot complete yet.', 'error') }
    finally { setBusy(null) }
  }

  if (loading) return <Loading />

  return (
    <>
      <Alert type="info"><strong>Bank Reconciliation:</strong> import a statement, auto-match lines to GL bank movements, post bank-only items (charges/interest) as journals, and confirm the residual difference is zero. Book-only items (deposits in transit / outstanding cheques) remain as reconciling items. Completing is blocked until every bank line is accounted for.</Alert>

      <SectionHeader title="Reconciliations" action={<Btn onClick={() => setModal('new')}>+ New Reconciliation</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable headers={['Ref', 'Account', 'Statement Date', 'Closing Balance', 'Difference', 'Status', '']}
          empty="No reconciliations yet."
          rows={recs.map(r => [
            <span style={{ fontFamily: T.mono, fontSize: 12 }}>{r.refNo}</span>,
            r.bankAccountCode,
            fmt.date(r.statementDate),
            <strong>{signed(r.statementClosingBalance)}</strong>,
            <strong style={{ color: Math.abs(r.difference) < 0.01 ? T.green : T.red }}>{signed(r.difference)}</strong>,
            <Badge variant={r.status === 'Completed' ? 'green' : 'amber'}>{r.status}</Badge>,
            <Btn size="sm" variant="ghost" onClick={() => open(r.id)} disabled={busy === r.id}>{busy === r.id ? '…' : 'Open'}</Btn>,
          ])} />
      </Card>

      {sel && <Detail rec={sel} busy={busy} onPost={line => setModal({ post: line })} onComplete={complete} onClose={() => setSel(null)} />}

      {modal === 'new' && <NewModal onClose={() => setModal(null)} onSaved={async (id) => { setModal(null); await loadList(); await open(id) }} notify={notify} />}
      {modal?.post && (
        <PostModal line={modal.post} recId={sel.id}
          onClose={() => setModal(null)}
          onSaved={async () => { setModal(null); await refreshSel(); await loadList(); notify?.('Posted to the ledger & matched.') }}
          notify={notify} />
      )}
    </>
  )
}

function Detail({ rec, busy, onPost, onComplete, onClose }) {
  const reconciled = Math.abs(rec.difference) < 0.01
  return (
    <>
      <div style={{ height: 22 }} />
      <SectionHeader title={`${rec.refNo} · ${rec.bankAccountCode} · ${fmt.date(rec.statementDate)}`}
        action={<div style={{ display: 'flex', gap: 8 }}>
          <Btn variant="ghost" onClick={onClose}>Close</Btn>
          {rec.status !== 'Completed' && <Btn variant="green" onClick={onComplete} disabled={!reconciled || rec.unmatchedStatementCount > 0 || busy === 'complete'}>{busy === 'complete' ? 'Completing…' : 'Complete'}</Btn>}
        </div>} />

      <div style={{ ...KPI_GRID, marginBottom: 16 }}>
        <Kpi label="Book Balance (GL)" value={signed(rec.bookBalance)} icon="📘" />
        <Kpi label="Statement Closing" value={signed(rec.statementClosingBalance)} icon="🏦" />
        <Kpi label="Adjusted (both sides)" value={signed(rec.adjustedBankBalance)} icon="⚖️" />
        <Kpi label="Difference" value={signed(rec.difference)} icon={reconciled ? '✅' : '⚠️'} variant={reconciled ? 'green' : 'red'} />
      </div>

      <SectionHeader title="Statement Lines" sub={`${rec.matchedCount} matched · ${rec.unmatchedStatementCount} unmatched (bank-only)`} />
      <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 18 }}>
        <DataTable headers={['Date', 'Description', 'Reference', 'Amount', 'Status', '']}
          empty="No statement lines."
          rows={rec.lines.map(l => [
            fmt.date(l.txnDate),
            l.description,
            <span style={{ fontSize: 12, color: T.mgrey }}>{l.reference || '—'}</span>,
            <strong style={{ color: l.amount < 0 ? T.red : T.green }}>{signed(l.amount)}</strong>,
            <Badge variant={LINE_VARIANT[l.matchStatus] || 'default'}>{l.matchStatus}</Badge>,
            l.matchStatus === 'Unmatched' && rec.status !== 'Completed'
              ? <Btn size="sm" onClick={() => onPost(l)}>Post to GL</Btn>
              : <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>,
          ])} />
      </Card>

      <SectionHeader title="Outstanding / In-Transit (book-only)" sub="In the ledger but not yet on the statement — normal timing differences." />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable headers={['Date', 'Description', 'Amount']}
          empty="Nothing outstanding — the ledger fully matches the statement."
          rows={rec.unmatchedBookEntries.map(g => [
            fmt.date(g.entryDate),
            g.description || '—',
            <strong style={{ color: g.amount < 0 ? T.red : T.green }}>{signed(g.amount)}</strong>,
          ])} />
      </Card>
    </>
  )
}

const cell = { padding: '8px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, boxSizing: 'border-box', width: '100%' }
const BANK_ACCOUNTS = [
  { value: '1100', label: '1100 · Cash & Bank — Equity Bank' },
  { value: '1110', label: '1110 · Petty Cash' },
]
const emptyLine = () => ({ txnDate: new Date().toISOString().slice(0, 10), description: '', reference: '', amount: '' })

function NewModal({ onClose, onSaved, notify }) {
  const today = new Date().toISOString().slice(0, 10)
  const [bankAccountCode, setBank] = useState('1100')
  const [statementDate, setDate] = useState(today)
  const [closing, setClosing] = useState('')
  const [lines, setLines] = useState([emptyLine()])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const setLine = (i, patch) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)))
  const filled = lines.filter(l => l.description && l.amount !== '' && !Number.isNaN(+l.amount))
  const lineTotal = filled.reduce((s, l) => s + (+l.amount || 0), 0)
  const canSave = closing !== '' && filled.length > 0

  async function save() {
    setSaving(true); setError('')
    try {
      const rec = await createReconciliation({
        bankAccountCode, statementDate, statementClosingBalance: +closing,
        lines: filled.map(l => ({ txnDate: l.txnDate, description: l.description, reference: l.reference || null, amount: +l.amount })),
      })
      notify?.(`Reconciliation ${rec.refNo} created — ${rec.matchedCount} line(s) auto-matched.`)
      onSaved(rec.id)
    } catch (e) { setError(e.response?.data?.message ?? 'Failed to create reconciliation.') }
    finally { setSaving(false) }
  }

  return (
    <Modal title="New Bank Reconciliation" onClose={onClose} width={720}>
      {error && <Alert type="error">{error}</Alert>}
      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 12 }}>
        <Select label="Bank Account" value={bankAccountCode} onChange={setBank} options={BANK_ACCOUNTS} />
        <Input label="Statement Date" type="date" value={statementDate} onChange={setDate} required />
        <Input label="Closing Balance" type="number" value={closing} onChange={setClosing} required />
      </div>

      <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', margin: '4px 0 6px' }}>Statement Lines <span style={{ textTransform: 'none', fontWeight: 400 }}>(+ into bank, − out)</span></div>
      {lines.map((l, i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: '1.2fr 2.4fr 1.2fr 1fr 20px', gap: 6, marginBottom: 6, alignItems: 'center' }}>
          <input type="date" value={l.txnDate} onChange={e => setLine(i, { txnDate: e.target.value })} style={cell} />
          <input placeholder="Description" value={l.description} onChange={e => setLine(i, { description: e.target.value })} style={cell} />
          <input placeholder="Reference" value={l.reference} onChange={e => setLine(i, { reference: e.target.value })} style={cell} />
          <input placeholder="Amount ±" type="number" value={l.amount} onChange={e => setLine(i, { amount: e.target.value })} style={cell} />
          <button onClick={() => setLines(ls => ls.length > 1 ? ls.filter((_, j) => j !== i) : ls)} title="Remove"
            style={{ background: 'none', border: 'none', color: T.mgrey, cursor: 'pointer', fontSize: 16 }}>×</button>
        </div>
      ))}
      <Btn size="sm" variant="ghost" onClick={() => setLines([...lines, emptyLine()])}>+ Add line</Btn>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 24, margin: '10px 0', fontSize: 13 }}>
        <span style={{ color: T.mgrey }}>Lines total: <strong style={{ color: T.navy }}>{signed(lineTotal)}</strong></span>
        <span style={{ color: T.mgrey }}>vs closing: <strong style={{ color: closing !== '' && Math.abs(lineTotal - +closing) < 0.01 ? T.green : T.dgrey }}>{closing !== '' ? signed(+closing) : '—'}</strong></span>
      </div>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={!canSave || saving}>{saving ? 'Creating…' : 'Create & Auto-match'}</Btn>
      </div>
    </Modal>
  )
}

const CONTRA = [
  { value: '5500', label: '5500 · Administrative (bank charges)' },
  { value: '5300', label: '5300 · Rent & Utilities' },
  { value: '4000', label: '4000 · Revenue (interest income)' },
]
function PostModal({ line, recId, onClose, onSaved, notify }) {
  const moneyIn = line.amount > 0
  const [contra, setContra] = useState(moneyIn ? '4000' : '5500')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  async function save() {
    setSaving(true); setError('')
    try { await postBankItem(recId, line.id, { contraAccountCode: contra }); onSaved() }
    catch (e) { setError(e.response?.data?.message ?? 'Failed to post.') }
    finally { setSaving(false) }
  }

  return (
    <Modal title="Post Bank-Only Item" onClose={onClose} width={480}>
      {error && <Alert type="error">{error}</Alert>}
      <Alert type="info">
        <strong>{line.description}</strong> — {signed(line.amount)}.<br />
        {moneyIn
          ? <>Money in: posts <strong>Dr Bank / Cr {contra}</strong>.</>
          : <>Money out: posts <strong>Dr {contra} / Cr Bank</strong>.</>}
      </Alert>
      <Select label="Contra Account" value={contra} onChange={setContra} options={CONTRA} />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={saving}>{saving ? 'Posting…' : 'Post to GL'}</Btn>
      </div>
    </Modal>
  )
}
