import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, SectionHeader, DataTable, Loading, EmptyState, Input } from '../ui.jsx'
import { getManagementAccounts } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

// Defensive row readers — balance sheet line shape isn't nailed down yet, so
// accept either {account, amount} (like income/expenses) or {name, balance}.
const rowLabel  = (r) => r?.account ?? r?.name ?? r?.accountName ?? '—'
const rowAmount = (r) => r?.amount ?? r?.balance ?? r?.total ?? 0

export default function ManagementAccountsTab({ canExport }) {
  const [periodId, setPeriodId] = useState('')
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    getManagementAccounts(periodId ? { periodId } : undefined)
      .then(setData)
      .catch(() => setError('Failed to load management accounts.'))
      .finally(() => setLoading(false))
  }, [periodId])

  useEffect(() => { load() }, [load])

  const byDept = data?.byDepartment ?? []
  const income = data?.income ?? []
  const expenses = data?.expenses ?? []
  const tb = data?.trialBalance ?? null
  const bs = data?.balanceSheet ?? null

  function exportDept() {
    exportToExcel({
      title: 'Management Accounts — By Department',
      filename: `management-accounts-${data?.periodName ?? 'period'}`,
      sheetName: 'By Department',
      columns: [
        { header: 'Cost Centre', accessor: r => r.costCentre, width: 26 },
        { header: 'Income', accessor: r => r.income, width: 16 },
        { header: 'Expense', accessor: r => r.expense, width: 16 },
        { header: 'Net', accessor: r => r.net, width: 16 },
      ],
      rows: byDept,
    })
  }
  function exportTrialBalance() {
    exportToExcel({
      title: 'Trial Balance',
      filename: `trial-balance-${tb?.asOf ?? ''}`,
      sheetName: 'Trial Balance',
      columns: [
        { header: 'Code', accessor: r => r.accountCode, width: 14 },
        { header: 'Account', accessor: r => r.accountName, width: 30 },
        { header: 'Classification', accessor: r => r.classification, width: 18 },
        { header: 'Debit', accessor: r => r.debit, width: 16 },
        { header: 'Credit', accessor: r => r.credit, width: 16 },
      ],
      rows: tb?.rows ?? [],
    })
  }
  function exportDeptPdf() {
    exportReportToPdf({
      title: 'Management Accounts — By Department',
      filename: `management-accounts-${data?.periodName ?? 'period'}`,
      summary: [
        { label: 'Period', value: data?.periodName ?? '—' },
        { label: 'Income Total', value: fmt.kes(data?.incomeTotal) },
        { label: 'Expense Total', value: fmt.kes(data?.expenseTotal) },
        { label: 'Net Profit', value: fmt.kes(data?.netProfit) },
      ],
      sections: [{
        heading: 'P&L by Department',
        columns: [
          { header: 'Cost Centre', accessor: r => r.costCentre },
          { header: 'Income', accessor: r => fmt.kes(r.income) },
          { header: 'Expense', accessor: r => fmt.kes(r.expense) },
          { header: 'Net', accessor: r => fmt.kes(r.net) },
        ],
        rows: byDept,
      }],
    })
  }
  function exportTrialBalancePdf() {
    exportReportToPdf({
      title: 'Trial Balance',
      filename: `trial-balance-${tb?.asOf ?? ''}`,
      summary: [
        { label: 'As Of', value: tb?.asOf ? fmt.date(tb.asOf) : '—' },
        { label: 'Total Debit', value: fmt.kes(tb?.totalDebit) },
        { label: 'Total Credit', value: fmt.kes(tb?.totalCredit) },
        { label: 'Balanced', value: tb?.isBalanced ? 'Yes' : 'No' },
      ],
      sections: [{
        heading: 'Trial Balance',
        columns: [
          { header: 'Code', accessor: r => r.accountCode },
          { header: 'Account', accessor: r => r.accountName },
          { header: 'Classification', accessor: r => r.classification },
          { header: 'Debit', accessor: r => fmt.kes(r.debit) },
          { header: 'Credit', accessor: r => fmt.kes(r.credit) },
        ],
        rows: tb?.rows ?? [],
      }],
    })
  }

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 220 }}>
          <Input label="Period ID (optional)" value={periodId} onChange={setPeriodId} placeholder="Leave blank for current period" />
        </div>
        <Btn size="sm" onClick={load} style={{ marginBottom: 14 }}>Refresh</Btn>
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : !data ? (
        <EmptyState title="No management accounts data" sub="Try a different period." />
      ) : (
        <>
          <div style={{ ...KPI_GRID, marginBottom: 22 }}>
            <Kpi label="Period" value={data.periodName ?? '—'} icon="🗓️" />
            <Kpi label="Income Total" value={fmt.kes(data.incomeTotal)} icon="📈" variant="green" />
            <Kpi label="Expense Total" value={fmt.kes(data.expenseTotal)} icon="📉" variant="red" />
            <Kpi label="Net Profit" value={fmt.kes(data.netProfit)} icon="💰" variant={data.netProfit >= 0 ? 'green' : 'red'} />
          </div>

          <SectionHeader title="P&L by Department" sub="Income, expense and net by cost centre"
            action={canExport && <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="ghost" onClick={exportDept}>⬇ Export</Btn>
              <Btn size="sm" variant="ghost" onClick={exportDeptPdf}>⬇ PDF</Btn>
            </div>} />
          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 22 }}>
            <DataTable headers={['Cost Centre', 'Income', 'Expense', 'Net']} empty="No departmental data."
              rows={byDept.map(d => [
                <strong>{d.costCentre}</strong>, fmt.kes(d.income), fmt.kes(d.expense),
                <span style={{ color: d.net < 0 ? T.red : T.green, fontWeight: 700 }}>{fmt.kes(d.net)}</span>,
              ])} />
          </Card>

          <div className="grid-2col" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(320px,1fr))', gap: 18, marginBottom: 22 }}>
            <div>
              <SectionHeader title="Income" />
              <Card style={{ padding: 0, overflow: 'hidden' }}>
                <DataTable headers={['Account', 'Amount']} empty="No income lines."
                  rows={income.map(i => [i.account, fmt.kes(i.amount)])} />
              </Card>
            </div>
            <div>
              <SectionHeader title="Expenses" />
              <Card style={{ padding: 0, overflow: 'hidden' }}>
                <DataTable headers={['Account', 'Amount']} empty="No expense lines."
                  rows={expenses.map(e => [e.account, fmt.kes(e.amount)])} />
              </Card>
            </div>
          </div>

          <SectionHeader title="Trial Balance" sub={tb?.asOf ? `As of ${fmt.date(tb.asOf)}` : undefined}
            action={canExport && <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="ghost" onClick={exportTrialBalance}>⬇ Export</Btn>
              <Btn size="sm" variant="ghost" onClick={exportTrialBalancePdf}>⬇ PDF</Btn>
            </div>} />
          {tb && !tb.isBalanced && <Alert type="warning">Trial balance does not balance — total debit ({fmt.kes(tb.totalDebit)}) ≠ total credit ({fmt.kes(tb.totalCredit)}).</Alert>}
          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 22 }}>
            <DataTable headers={['Code', 'Account', 'Classification', 'Debit', 'Credit']} empty="No trial balance rows."
              rows={(tb?.rows ?? []).map(r => [
                <span style={{ fontFamily: T.mono, fontSize: 12 }}>{r.accountCode}</span>, r.accountName,
                <Badge>{r.classification}</Badge>, fmt.kes(r.debit), fmt.kes(r.credit),
              ])} />
            {tb && (
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 13px', background: T.offwt, borderTop: `1px solid ${T.lgrey}`, fontWeight: 700, fontSize: 13 }}>
                <span>Totals</span>
                <span>{fmt.kes(tb.totalDebit)} / {fmt.kes(tb.totalCredit)} — <Badge variant={tb.isBalanced ? 'green' : 'red'}>{tb.isBalanced ? 'Balanced' : 'Out of balance'}</Badge></span>
              </div>
            )}
          </Card>

          <SectionHeader title="Balance Sheet" sub="Derived from the chart of accounts" />
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(280px,1fr))', gap: 18 }}>
            {[['Assets', bs?.assets], ['Liabilities', bs?.liabilities], ['Equity', bs?.equity]].map(([label, rows]) => (
              <div key={label}>
                <div style={{ fontSize: 13, fontWeight: 700, color: T.navy, marginBottom: 8 }}>{label}</div>
                <Card style={{ padding: 0, overflow: 'hidden' }}>
                  <DataTable headers={['Account', 'Amount']} empty={`No ${label.toLowerCase()}.`}
                    rows={(rows ?? []).map(r => [rowLabel(r), fmt.kes(rowAmount(r))])} />
                </Card>
              </div>
            ))}
          </div>
        </>
      )}
    </>
  )
}
