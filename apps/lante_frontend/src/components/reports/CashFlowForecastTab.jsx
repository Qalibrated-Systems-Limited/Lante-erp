import { useEffect, useState, useCallback } from 'react'
import Chart from 'react-apexcharts'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Alert, SectionHeader, DataTable, Loading, EmptyState, Input, Select } from '../ui.jsx'
import { getCashFlowForecast } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

const WEEK_OPTIONS = [4, 8, 13, 26].map(n => ({ value: String(n), label: `${n} weeks` }))

export default function CashFlowForecastTab({ canExport }) {
  const [asOf, setAsOf] = useState('')
  const [weeks, setWeeks] = useState('13')
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    getCashFlowForecast({ ...(asOf ? { asOf } : {}), weeks })
      .then(setData)
      .catch(() => setError('Failed to load cash flow forecast.'))
      .finally(() => setLoading(false))
  }, [asOf, weeks])

  useEffect(() => { load() }, [load])

  const rows = data?.weeks ?? []

  function doExport() {
    exportToExcel({
      title: 'Cash Flow Forecast',
      filename: `cash-flow-forecast-${data?.asOf ?? ''}`,
      sheetName: 'Cash Flow',
      columns: [
        { header: 'Week Start', accessor: r => fmt.date(r.weekStart), width: 16 },
        { header: 'Cash In', accessor: r => r.cashIn, width: 16 },
        { header: 'Cash Out', accessor: r => r.cashOut, width: 16 },
        { header: 'Net Cash', accessor: r => r.netCash, width: 16 },
        { header: 'Running Balance', accessor: r => r.runningBalance, width: 18 },
      ],
      rows,
    })
  }
  function doExportPdf() {
    exportReportToPdf({
      title: 'Cash Flow Forecast',
      filename: `cash-flow-forecast-${data?.asOf ?? ''}`,
      summary: [
        { label: 'As Of', value: data?.asOf ? fmt.date(data.asOf) : '—' },
        { label: 'Horizon', value: `${weeks} weeks` },
        { label: 'Overdue Receivables', value: fmt.kes(data?.overdueReceivables) },
        { label: 'Overdue Payables', value: fmt.kes(data?.overduePayables) },
      ],
      sections: [{
        heading: 'Forecast Detail',
        columns: [
          { header: 'Week Start', accessor: r => fmt.date(r.weekStart) },
          { header: 'Cash In', accessor: r => fmt.kes(r.cashIn) },
          { header: 'Cash Out', accessor: r => fmt.kes(r.cashOut) },
          { header: 'Net Cash', accessor: r => fmt.kes(r.netCash) },
          { header: 'Running Balance', accessor: r => fmt.kes(r.runningBalance) },
        ],
        rows,
      }],
    })
  }

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 200 }}>
          <Input label="As Of" type="date" value={asOf} onChange={setAsOf} />
        </div>
        <div style={{ minWidth: 160 }}>
          <Select label="Horizon" value={weeks} onChange={setWeeks} options={WEEK_OPTIONS} />
        </div>
        <Btn size="sm" onClick={load} style={{ marginBottom: 14 }}>Refresh</Btn>
        {canExport && rows.length > 0 && <Btn size="sm" variant="ghost" onClick={doExport} style={{ marginBottom: 14 }}>⬇ Export</Btn>}
        {canExport && rows.length > 0 && <Btn size="sm" variant="ghost" onClick={doExportPdf} style={{ marginBottom: 14 }}>⬇ PDF</Btn>}
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : !data || rows.length === 0 ? (
        <EmptyState title="No forecast data" sub="Try a different as-of date or horizon." />
      ) : (
        <>
          <div style={{ ...KPI_GRID, marginBottom: 22 }}>
            <Kpi label="Overdue Receivables" value={fmt.kes(data.overdueReceivables)} icon="📥" variant={data.overdueReceivables ? 'amber' : 'green'} />
            <Kpi label="Overdue Payables" value={fmt.kes(data.overduePayables)} icon="📤" variant={data.overduePayables ? 'amber' : 'green'} />
          </div>

          <SectionHeader title="Weekly Net Cash & Running Balance" sub={data.asOf ? `As of ${fmt.date(data.asOf)}` : undefined} />
          <Card style={{ marginBottom: 22 }}>
            <Chart
              type="line"
              height={280}
              series={[
                { name: 'Net Cash', type: 'column', data: rows.map(w => w.netCash ?? 0) },
                { name: 'Running Balance', type: 'line', data: rows.map(w => w.runningBalance ?? 0) },
              ]}
              options={{
                chart: { toolbar: { show: false }, stacked: false },
                colors: [T.gold, T.navy],
                stroke: { width: [0, 3], curve: 'smooth' },
                markers: { size: 3 },
                plotOptions: { bar: { columnWidth: '55%', borderRadius: 3 } },
                xaxis: { categories: rows.map(w => fmt.date(w.weekStart)), labels: { style: { fontSize: '10px' } } },
                yaxis: { labels: { formatter: v => `${(v / 1000).toFixed(0)}k` } },
                tooltip: { y: { formatter: v => fmt.kes(v) } },
                legend: { position: 'top', fontSize: '11px' },
                grid: { borderColor: T.lgrey },
              }}
            />
          </Card>

          <SectionHeader title="Forecast Detail"
            action={canExport && <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="ghost" onClick={doExport}>⬇ Export</Btn>
              <Btn size="sm" variant="ghost" onClick={doExportPdf}>⬇ PDF</Btn>
            </div>} />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Week Start', 'Cash In', 'Cash Out', 'Net Cash', 'Running Balance']}
              rows={rows.map(w => [
                fmt.date(w.weekStart),
                <span style={{ color: T.green }}>{fmt.kes(w.cashIn)}</span>,
                <span style={{ color: T.red }}>{fmt.kes(w.cashOut)}</span>,
                <strong style={{ color: (w.netCash ?? 0) < 0 ? T.red : T.green }}>{fmt.kes(w.netCash)}</strong>,
                <strong style={{ color: (w.runningBalance ?? 0) < 0 ? T.red : T.navy }}>{fmt.kes(w.runningBalance)}</strong>,
              ])} />
          </Card>
        </>
      )}
    </>
  )
}
