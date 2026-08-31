import { useEffect, useState } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Badge, DataTable, Loading, Alert } from '../ui.jsx'
import { getChartOfAccounts } from '../../services/finance.js'

const CLASS_VARIANT = { Asset: 'blue', Liability: 'amber', Equity: 'purple', Income: 'green', Expense: 'red' }

// Chart of Accounts — live from the finance service (GET /api/v1/finance/chart-of-accounts).
export default function CoaTab() {
  const [accounts, setAccounts] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    getChartOfAccounts()
      .then(a => setAccounts(a ?? []))
      .catch(() => setError('Failed to load chart of accounts.'))
      .finally(() => setLoading(false))
  }, [])

  const postable = accounts.filter(a => a.isDirectPosting).length
  const banks = accounts.filter(a => a.isBank).length

  if (loading) return <Loading />
  if (error) return <Alert type="error">{error}</Alert>

  return (
    <>
      <div style={{ ...KPI_GRID, marginBottom: 18 }}>
        <Kpi label="Accounts" value={accounts.length} icon="📚" />
        <Kpi label="Postable (leaf)" value={postable} icon="✍️" variant="blue" />
        <Kpi label="Bank & Cash" value={banks} icon="🏦" variant="green" />
      </div>
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Code', 'Account Name', 'Type', 'Classification', 'Posting']}
          empty="No accounts."
          rows={accounts.map(a => [
            <span style={{ fontFamily: T.mono, fontSize: 12 }}>{a.code}</span>,
            a.isDirectPosting
              ? a.name
              : <strong style={{ color: T.navy }}>{a.name}</strong>,
            a.accountType,
            <Badge variant={CLASS_VARIANT[a.classification] || 'default'}>{a.classification}</Badge>,
            a.isDirectPosting
              ? <Badge variant="green">Direct</Badge>
              : <span style={{ color: T.mgrey, fontSize: 12 }}>Header</span>,
          ])}
        />
      </Card>
    </>
  )
}
