import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Loading } from '../ui.jsx'
import { getFiscalYears, getPeriodClose, toggleChecklist, closeMonthEnd, reopenMonthEnd, getProfitAndLoss } from '../../services/finance.js'

const STATUS_VARIANT = { Open: 'green', Closed: 'amber', Locked: 'red' }

export default function MonthEndTab({ notify }) {
  const [periods, setPeriods] = useState([])
  const [periodId, setPeriodId] = useState('')
  const [close, setClose] = useState(null)
  const [pnl, setPnl] = useState(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    getFiscalYears().then(ys => {
      const ps = (ys ?? []).flatMap(y => y.periods ?? [])
      setPeriods(ps)
      setPeriodId(ps.find(p => p.name === '2026-06')?.id ?? ps[0]?.id ?? '')
    }).catch(() => notify?.('Failed to load periods.', 'error'))
  }, [notify])

  const load = useCallback(() => {
    if (!periodId) return
    setLoading(true)
    return Promise.all([getPeriodClose(periodId), getProfitAndLoss(periodId)])
      .then(([c, p]) => { setClose(c); setPnl(p) })
      .catch(() => notify?.('Failed to load month-end.', 'error'))
      .finally(() => setLoading(false))
  }, [periodId, notify])
  useEffect(() => { load() }, [load])

  async function toggle(item) {
    setBusy(true)
    try { setClose(await toggleChecklist(item.id, !item.isComplete)) }
    catch (e) { notify?.(e.response?.data?.message ?? 'Failed.', 'error') }
    finally { setBusy(false) }
  }
  async function doClose() {
    setBusy(true)
    try { setClose(await closeMonthEnd(periodId)); notify?.('Period closed & locked.') }
    catch (e) { notify?.(e.response?.data?.message ?? 'Close failed.', 'error') }
    finally { setBusy(false) }
  }
  async function doReopen() {
    setBusy(true)
    try { setClose(await reopenMonthEnd(periodId)); notify?.('Period reopened.') }
    catch (e) { notify?.(e.response?.data?.message ?? 'Reopen failed.', 'error') }
    finally { setBusy(false) }
  }

  return (
    <>
      <Alert type="info"><strong style={{ color: T.blue }}>FIN-003/004:</strong> complete every checklist item, then close to lock the period — no journals can be dated into a closed period.</Alert>
      <div style={{ marginBottom: 18, display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
        <span style={{ fontSize: 13, fontWeight: 600, color: T.dgrey }}>Period:</span>
        <select value={periodId} onChange={e => setPeriodId(e.target.value)} style={{ padding: '8px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {periods.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
        {close && <Badge variant={STATUS_VARIANT[close.status] || 'default'}>{close.status}</Badge>}
      </div>

      {loading || !close || !pnl ? <Loading /> : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 20 }}>
          {/* Close checklist */}
          <Card>
            <SectionHeader title="Close Checklist"
              action={close.status === 'Open'
                ? <Btn size="sm" disabled={busy || !close.canClose} onClick={doClose}>Close Period</Btn>
                : <Btn size="sm" variant="ghost" disabled={busy} onClick={doReopen}>Reopen</Btn>} />
            {close.checklist.map((c, i) => (
              <label key={c.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 0', borderBottom: i < close.checklist.length - 1 ? `1px solid ${T.lgrey}` : 'none', fontSize: 13, cursor: close.status === 'Open' ? 'pointer' : 'default' }}>
                <input type="checkbox" checked={c.isComplete} disabled={close.status !== 'Open' || busy} onChange={() => toggle(c)} />
                <span style={{ color: c.isComplete ? T.mgrey : T.dgrey, textDecoration: c.isComplete ? 'line-through' : 'none' }}>{c.item}</span>
              </label>
            ))}
            {close.status === 'Open' && !close.canClose && (
              <div style={{ fontSize: 12, color: T.mgrey, marginTop: 10 }}>Complete all items to enable close.</div>
            )}
          </Card>

          {/* P&L */}
          <Card>
            <SectionHeader title="Profit & Loss" sub={`Posted journals · ${pnl.periodName}`} />
            <PnlBlock title="Income" lines={pnl.income} total={pnl.incomeTotal} color={T.green} />
            <div style={{ height: 12 }} />
            <PnlBlock title="Expenses" lines={pnl.expenses} total={pnl.expenseTotal} color={T.red} />
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '12px 0', borderTop: `2px solid ${T.lgrey}`, marginTop: 10, fontWeight: 800, fontSize: 15 }}>
              <span style={{ color: T.navy }}>Net Profit / (Loss)</span>
              <span style={{ color: pnl.netProfit >= 0 ? T.green : T.red }}>{fmt.kes(pnl.netProfit)}</span>
            </div>
          </Card>

          {/* P&L by department */}
          <Card style={{ gridColumn: '1 / -1', padding: 0, overflow: 'hidden' }}>
            <div style={{ padding: '16px 20px 0' }}><SectionHeader title="P&L by Department" sub={`Posted journals · ${pnl.periodName}`} /></div>
            <DataTable headers={['Department / Cost Centre', 'Income', 'Expense', 'Net']}
              empty="No posted journals in this period."
              rows={pnl.byDepartment.map(d => [
                <strong>{d.costCentre}</strong>, fmt.kes(d.income),
                <span style={{ color: T.red }}>{fmt.kes(d.expense)}</span>,
                <strong style={{ color: d.net >= 0 ? T.green : T.red }}>{fmt.kes(d.net)}</strong>,
              ])} />
          </Card>
        </div>
      )}
    </>
  )
}

function PnlBlock({ title, lines, total, color }) {
  return (
    <div>
      <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', marginBottom: 4 }}>{title}</div>
      {lines.length === 0 ? <div style={{ fontSize: 12, color: T.mgrey, padding: '6px 0' }}>None posted.</div>
        : lines.map(l => (
          <div key={l.accountCode} style={{ display: 'flex', justifyContent: 'space-between', padding: '5px 0', fontSize: 13 }}>
            <span style={{ color: T.dgrey }}><span style={{ fontFamily: T.mono, fontSize: 11, color: T.mgrey }}>{l.accountCode}</span> {l.accountName}</span>
            <span>{fmt.kes(l.amount)}</span>
          </div>
        ))}
      <div style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderTop: `1px solid ${T.lgrey}`, fontWeight: 700 }}>
        <span style={{ color: T.navy }}>Total {title}</span><span style={{ color }}>{fmt.kes(total)}</span>
      </div>
    </div>
  )
}
