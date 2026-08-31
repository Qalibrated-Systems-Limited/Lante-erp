import { useEffect, useState } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Alert, DataTable, Loading } from '../ui.jsx'
import { getCashFlow } from '../../services/finance.js'

const d = (iso) => fmt.date(iso)

export default function CashFlowTab({ notify }) {
  const [cf, setCf] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getCashFlow({ weeks: 8 }).then(setCf).catch(() => notify?.('Failed to load cash flow.', 'error')).finally(() => setLoading(false))
  }, [notify])

  if (loading || !cf) return <Loading />

  return (
    <>
      <Alert type="info">Projected weekly cash position: open invoice balances land in their due week; supplier invoices by payment terms. Overdue items are shown separately — not assumed collected next week.</Alert>
      <div style={{ ...KPI_GRID, marginBottom: 18 }}>
        <Kpi label="Cash Now (GL Bank+Cash)" value={fmt.kes(cf.cashNow)} icon="💵" />
        <Kpi label="Overdue Receivables" value={fmt.kes(cf.overdueReceivables)} sub="chase these — not in forecast" icon="📥" variant={cf.overdueReceivables ? 'amber' : undefined} />
        <Kpi label="Overdue Payables" value={fmt.kes(cf.overduePayables)} sub="due now — not in forecast" icon="📤" variant={cf.overduePayables ? 'amber' : undefined} />
        <Kpi label="Lowest Projected Balance" value={fmt.kes(cf.lowestProjectedBalance)} icon={cf.lowestProjectedBalance < 0 ? '🚨' : '📊'} variant={cf.lowestProjectedBalance < 0 ? 'red' : 'green'} />
      </div>
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable headers={['Week', 'Period', 'Expected In', 'Expected Out', 'Net', 'Projected Balance']}
          rows={cf.weeks.map(w => [
            <strong>{w.label}</strong>,
            <span style={{ fontSize: 12, color: T.mgrey }}>{d(w.periodStart)} – {d(w.periodEnd)}</span>,
            <span style={{ color: T.green }}>{w.expectedIn ? fmt.kes(w.expectedIn) : '—'}</span>,
            <span style={{ color: T.red }}>{w.expectedOut ? fmt.kes(w.expectedOut) : '—'}</span>,
            <strong style={{ color: w.net < 0 ? T.red : w.net > 0 ? T.green : T.mgrey }}>{w.net === 0 ? '—' : `${w.net > 0 ? '+' : ''}${fmt.kes(w.net)}`}</strong>,
            <strong style={{ color: w.projectedBalance < 0 ? T.red : T.navy }}>{fmt.kes(w.projectedBalance)}</strong>,
          ])} />
      </Card>
    </>
  )
}
