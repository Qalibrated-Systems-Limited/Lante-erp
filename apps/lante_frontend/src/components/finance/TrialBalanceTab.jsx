import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Badge, Alert, SectionHeader, DataTable, Loading } from '../ui.jsx'
import { getTrialBalance } from '../../services/finance.js'

const dateInput = { padding: '7px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }
const CLASS_VARIANT = { Asset: 'blue', Liability: 'amber', Equity: 'purple', Income: 'green', Expense: 'red' }

// Trial Balance — live from the posted general ledger (GET /api/v1/finance/trial-balance).
export default function TrialBalanceTab() {
  const [asOf, setAsOf] = useState(new Date().toISOString().slice(0, 10))
  const [tb, setTb] = useState(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(() => {
    setLoading(true)
    getTrialBalance({ asOf }).then(setTb).catch(() => setTb(null)).finally(() => setLoading(false))
  }, [asOf])
  useEffect(() => { load() }, [load])

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 10 }}>
        <span style={{ fontSize: 13, color: T.dgrey }}>As of:</span>
        <input type="date" value={asOf} onChange={e => setAsOf(e.target.value)} style={dateInput} />
      </div>
      {loading || !tb ? <Loading /> : (
        <>
          <Alert type={tb.isBalanced ? 'success' : 'error'}>
            {tb.isBalanced ? '✓ Trial balance is balanced.' : '⚠ Trial balance is out of balance.'}
          </Alert>
          <SectionHeader title="Trial Balance" sub={`As of ${fmt.date(asOf)} · posted journals only`} />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Code', 'Account', 'Classification', 'Debit', 'Credit']}
              empty="No posted journal activity."
              rows={tb.rows.map(r => [
                <span style={{ fontFamily: T.mono, fontSize: 12 }}>{r.accountCode}</span>,
                r.accountName,
                <Badge variant={CLASS_VARIANT[r.classification] || 'default'}>{r.classification}</Badge>,
                r.debit ? fmt.kes(r.debit) : '—',
                r.credit ? fmt.kes(r.credit) : '—',
              ])}
            />
          </Card>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 32, marginTop: 14, fontWeight: 700, color: T.navy }}>
            <span>Total Debit: {fmt.kes(tb.totalDebit)}</span>
            <span>Total Credit: {fmt.kes(tb.totalCredit)}</span>
          </div>
        </>
      )}
    </>
  )
}
