import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Loading, EmptyState, Progress, Input } from '../ui.jsx'
import { getBudgetVariance } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

// Status strings aren't fully pinned down by the backend spec yet — map
// defensively by keyword so any of OnTrack/Warning/Over, Achieved/Behind, etc.
// still land on a sensible RAG colour.
function ragVariant(status) {
  const s = (status ?? '').toLowerCase()
  if (s.includes('over') || s.includes('red') || s.includes('behind') || s.includes('breach')) return 'red'
  if (s.includes('warn') || s.includes('amber') || s.includes('risk')) return 'amber'
  if (s.includes('achiev') || s.includes('ontrack') || s.includes('on track') || s.includes('green')) return 'green'
  return 'default'
}

export default function BudgetVarianceTab({ canExport }) {
  const [fiscalYearId, setFiscalYearId] = useState('')
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    getBudgetVariance(fiscalYearId ? { fiscalYearId } : undefined)
      .then(setData)
      .catch(() => setError('Failed to load budget vs actual.'))
      .finally(() => setLoading(false))
  }, [fiscalYearId])

  useEffect(() => { load() }, [load])

  const budgets = data?.budgets ?? []
  const targets = data?.revenueTargets ?? []

  function exportBudgets() {
    exportToExcel({
      title: 'Budget vs Actual',
      filename: 'budget-variance',
      sheetName: 'Budgets',
      columns: [
        { header: 'Department', accessor: r => r.departmentName, width: 26 },
        { header: 'Cost Centre', accessor: r => r.costCentreLabel, width: 22 },
        { header: 'Annual Budget', accessor: r => r.annualAmount, width: 18 },
        { header: 'Actual', accessor: r => r.actual, width: 18 },
        { header: 'Variance', accessor: r => r.variance, width: 18 },
        { header: '% Consumed', accessor: r => r.consumedPct, width: 14 },
        { header: 'Status', accessor: r => r.status, width: 14 },
      ],
      rows: budgets,
    })
  }
  function exportTargets() {
    exportToExcel({
      title: 'Revenue Targets',
      filename: 'revenue-targets',
      sheetName: 'Revenue Targets',
      columns: [
        { header: 'Scope', accessor: r => r.scope, width: 24 },
        { header: 'Annual Target', accessor: r => r.annualAmount, width: 18 },
        { header: 'Actual', accessor: r => r.actual, width: 18 },
        { header: '% Achieved', accessor: r => r.achieved, width: 14 },
        { header: 'Status', accessor: r => r.status, width: 14 },
      ],
      rows: targets,
    })
  }
  function exportBudgetsPdf() {
    exportReportToPdf({
      title: 'Budget vs Actual',
      filename: 'budget-variance',
      sections: [{
        heading: 'Department Budgets vs Actual',
        columns: [
          { header: 'Department', accessor: r => r.departmentName },
          { header: 'Cost Centre', accessor: r => r.costCentreLabel ?? '—' },
          { header: 'Annual Budget', accessor: r => fmt.kes(r.annualAmount) },
          { header: 'Actual', accessor: r => fmt.kes(r.actual) },
          { header: 'Variance', accessor: r => fmt.kes(r.variance) },
          { header: '% Consumed', accessor: r => `${r.consumedPct ?? 0}%` },
          { header: 'Status', accessor: r => r.status },
        ],
        rows: budgets,
      }],
    })
  }
  function exportTargetsPdf() {
    exportReportToPdf({
      title: 'Revenue Targets vs Actual',
      filename: 'revenue-targets',
      sections: [{
        heading: 'Revenue Targets vs Actual',
        columns: [
          { header: 'Scope', accessor: r => r.scope },
          { header: 'Annual Target', accessor: r => fmt.kes(r.annualAmount) },
          { header: 'Actual', accessor: r => fmt.kes(r.actual) },
          { header: '% Achieved', accessor: r => `${r.achieved ?? 0}%` },
          { header: 'Status', accessor: r => r.status },
        ],
        rows: targets,
      }],
    })
  }

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 220 }}>
          <Input label="Fiscal Year ID (optional)" value={fiscalYearId} onChange={setFiscalYearId} placeholder="Leave blank for current year" />
        </div>
        <Btn size="sm" onClick={load} style={{ marginBottom: 14 }}>Refresh</Btn>
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : !data ? (
        <EmptyState title="No budget data" sub="Try a different fiscal year." />
      ) : (
        <>
          <SectionHeader title="Department Budgets vs Actual" sub="Amber at 80% consumed, red at 100%"
            action={canExport && <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="ghost" onClick={exportBudgets}>⬇ Export</Btn>
              <Btn size="sm" variant="ghost" onClick={exportBudgetsPdf}>⬇ PDF</Btn>
            </div>} />
          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 22 }}>
            <DataTable headers={['Department', 'Cost Centre', 'Annual Budget', 'Actual', '% Consumed', 'Variance', 'Status']}
              empty="No budgets found."
              rows={budgets.map(b => [
                <strong>{b.departmentName}</strong>, b.costCentreLabel || '—',
                fmt.kes(b.annualAmount), fmt.kes(b.actual),
                <div style={{ minWidth: 120 }}>
                  <Progress value={(b.consumedPct ?? 0) / 100} />
                  <div style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{b.consumedPct ?? 0}%</div>
                </div>,
                <span style={{ color: b.variance < 0 ? T.red : T.dgrey }}>{fmt.kes(b.variance)}</span>,
                <Badge variant={ragVariant(b.status)}>{b.status}</Badge>,
              ])} />
          </Card>

          <SectionHeader title="Revenue Targets vs Actual"
            action={canExport && <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="ghost" onClick={exportTargets}>⬇ Export</Btn>
              <Btn size="sm" variant="ghost" onClick={exportTargetsPdf}>⬇ PDF</Btn>
            </div>} />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Scope', 'Annual Target', 'Actual', '% Achieved', 'Status']}
              empty="No revenue targets found."
              rows={targets.map(t => [
                <strong>{t.scope}</strong>, fmt.kes(t.annualAmount), fmt.kes(t.actual),
                <span style={{ fontWeight: 700, color: (t.achieved ?? 0) >= 100 ? T.green : T.navy }}>{t.achieved ?? 0}%</span>,
                <Badge variant={ragVariant(t.status)}>{t.status}</Badge>,
              ])} />
          </Card>
        </>
      )}
    </>
  )
}
