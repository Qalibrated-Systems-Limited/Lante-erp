import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Alert, SectionHeader, DataTable, Loading } from '../ui.jsx'
import { getTrialBalance } from '../../services/finance.js'

const dateInput = { padding: '7px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }
const row = { display: 'flex', justifyContent: 'space-between', marginTop: 14, fontWeight: 700, color: T.navy }

// Balance Sheet — derived from the same trial-balance data (assets vs liabilities + equity + P/L).
export default function BalanceSheetTab() {
  const [asOf, setAsOf] = useState(new Date().toISOString().slice(0, 10))
  const [tb, setTb] = useState(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(() => {
    setLoading(true)
    getTrialBalance({ asOf }).then(setTb).catch(() => setTb(null)).finally(() => setLoading(false))
  }, [asOf])
  useEffect(() => { load() }, [load])

  if (loading || !tb) return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 10 }}>
        <span style={{ fontSize: 13, color: T.dgrey }}>As of:</span>
        <input type="date" value={asOf} onChange={e => setAsOf(e.target.value)} style={dateInput} />
      </div>
      <Loading />
    </>
  )

  const net = r => r.debit - r.credit
  const assets = tb.rows.filter(r => r.classification === 'Asset').map(r => ({ ...r, bal: net(r) }))
  const liabilities = tb.rows.filter(r => r.classification === 'Liability').map(r => ({ ...r, bal: -net(r) }))
  const equity = tb.rows.filter(r => r.classification === 'Equity').map(r => ({ ...r, bal: -net(r) }))
  const incomeNet = tb.rows.filter(r => r.classification === 'Income').reduce((s, r) => s - net(r), 0)
  const expenseNet = tb.rows.filter(r => r.classification === 'Expense').reduce((s, r) => s + net(r), 0)
  const profit = incomeNet - expenseNet

  const totalAssets = assets.reduce((s, r) => s + r.bal, 0)
  const totalLiab = liabilities.reduce((s, r) => s + r.bal, 0)
  const totalEquity = equity.reduce((s, r) => s + r.bal, 0) + profit
  const balanced = Math.round(totalAssets) === Math.round(totalLiab + totalEquity)

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 10 }}>
        <span style={{ fontSize: 13, color: T.dgrey }}>As of:</span>
        <input type="date" value={asOf} onChange={e => setAsOf(e.target.value)} style={dateInput} />
      </div>
      <Alert type={balanced ? 'success' : 'error'}>{balanced ? '✓ Assets = Liabilities + Equity.' : '⚠ Balance sheet does not balance.'}</Alert>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 20 }}>
        <Card>
          <SectionHeader title="Assets" sub={`As of ${fmt.date(asOf)}`} />
          <DataTable headers={['Code', 'Account', 'Balance']} empty="No asset balances."
            rows={assets.map(r => [<span style={{ fontFamily: T.mono, fontSize: 12 }}>{r.accountCode}</span>, r.accountName, fmt.kes(r.bal)])} />
          <div style={{ ...row, fontSize: 15 }}><span>Total Assets</span><span>{fmt.kes(totalAssets)}</span></div>
        </Card>
        <Card>
          <SectionHeader title="Liabilities & Equity" sub={`As of ${fmt.date(asOf)}`} />
          <DataTable headers={['Code', 'Account', 'Balance']} empty="No liability balances."
            rows={liabilities.map(r => [<span style={{ fontFamily: T.mono, fontSize: 12 }}>{r.accountCode}</span>, r.accountName, fmt.kes(r.bal)])} />
          <div style={row}><span>Total Liabilities</span><span>{fmt.kes(totalLiab)}</span></div>
          {equity.map(r => (
            <div key={r.accountCode} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', fontSize: 13 }}>
              <span>{r.accountName}</span><span>{fmt.kes(r.bal)}</span>
            </div>
          ))}
          <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 0', borderTop: `1px solid ${T.lgrey}`, marginTop: 8, fontSize: 13 }}>
            <span>Current Year Profit/(Loss)</span><span style={{ color: profit >= 0 ? T.green : T.red }}>{fmt.kes(profit)}</span>
          </div>
          <div style={row}><span>Total Equity</span><span>{fmt.kes(totalEquity)}</span></div>
          <div style={{ ...row, fontSize: 15 }}><span>Total Liabilities + Equity</span><span>{fmt.kes(totalLiab + totalEquity)}</span></div>
        </Card>
      </div>
    </>
  )
}
