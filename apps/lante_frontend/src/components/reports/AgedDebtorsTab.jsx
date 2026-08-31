import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Alert, SectionHeader, DataTable, Loading, EmptyState, Input } from '../ui.jsx'
import { getAgedDebtors } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

export default function AgedDebtorsTab({ canExport }) {
  const [asOf, setAsOf] = useState('')
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    getAgedDebtors(asOf ? { asOf } : undefined)
      .then(r => setRows(r ?? []))
      .catch(() => setError('Failed to load aged debtors.'))
      .finally(() => setLoading(false))
  }, [asOf])

  useEffect(() => { load() }, [load])

  const totals = rows.reduce((acc, r) => ({
    current: acc.current + (r.current ?? 0),
    days1To30: acc.days1To30 + (r.days1To30 ?? 0),
    days31To60: acc.days31To60 + (r.days31To60 ?? 0),
    days61Plus: acc.days61Plus + (r.days61Plus ?? 0),
    total: acc.total + (r.total ?? 0),
  }), { current: 0, days1To30: 0, days31To60: 0, days61Plus: 0, total: 0 })

  function doExport() {
    exportToExcel({
      title: 'Aged Debtors',
      filename: `aged-debtors-${asOf || 'today'}`,
      sheetName: 'Aged Debtors',
      columns: [
        { header: 'Customer', accessor: r => r.customerName, width: 28 },
        { header: 'Current', accessor: r => r.current, width: 16 },
        { header: '1-30 Days', accessor: r => r.days1To30, width: 16 },
        { header: '31-60 Days', accessor: r => r.days31To60, width: 16 },
        { header: '61+ Days', accessor: r => r.days61Plus, width: 16 },
        { header: 'Total', accessor: r => r.total, width: 18 },
      ],
      rows,
    })
  }
  function doExportPdf() {
    exportReportToPdf({
      title: 'Aged Debtors',
      filename: `aged-debtors-${asOf || 'today'}`,
      summary: [
        { label: 'As Of', value: asOf ? fmt.date(asOf) : 'Today' },
        { label: 'Total Outstanding', value: fmt.kes(totals.total) },
        { label: 'Current', value: fmt.kes(totals.current) },
        { label: '61+ Days', value: fmt.kes(totals.days61Plus) },
      ],
      sections: [{
        heading: 'Aged Debtors',
        columns: [
          { header: 'Customer', accessor: r => r.customerName },
          { header: 'Current', accessor: r => fmt.kes(r.current) },
          { header: '1-30 Days', accessor: r => fmt.kes(r.days1To30) },
          { header: '31-60 Days', accessor: r => fmt.kes(r.days31To60) },
          { header: '61+ Days', accessor: r => fmt.kes(r.days61Plus) },
          { header: 'Total', accessor: r => fmt.kes(r.total) },
        ],
        rows,
      }],
    })
  }

  return (
    <>
      <Alert type="info">Ageing buckets are <strong>Current / 1-30 / 31-60 / 61+ days</strong> as provided by the backend — this is not a 90-day split.</Alert>

      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 200 }}>
          <Input label="As Of" type="date" value={asOf} onChange={setAsOf} />
        </div>
        <Btn size="sm" onClick={load} style={{ marginBottom: 14 }}>Refresh</Btn>
        {canExport && rows.length > 0 && <Btn size="sm" variant="ghost" onClick={doExport} style={{ marginBottom: 14 }}>⬇ Export</Btn>}
        {canExport && rows.length > 0 && <Btn size="sm" variant="ghost" onClick={doExportPdf} style={{ marginBottom: 14 }}>⬇ PDF</Btn>}
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : rows.length === 0 ? (
        <EmptyState title="No debtor balances" sub="No outstanding customer balances as of this date." />
      ) : (
        <>
          <SectionHeader title="Aged Debtors" sub={asOf ? `As of ${fmt.date(asOf)}` : 'As of today'} />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Customer', 'Current', '1-30', '31-60', '61+', 'Total']}
              rows={rows.map(r => [
                <strong>{r.customerName}</strong>,
                fmt.kes(r.current), fmt.kes(r.days1To30), fmt.kes(r.days31To60),
                <span style={{ color: (r.days61Plus ?? 0) > 0 ? T.red : T.dgrey }}>{fmt.kes(r.days61Plus)}</span>,
                <strong>{fmt.kes(r.total)}</strong>,
              ])} />
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 18, padding: '10px 13px', background: T.offwt, borderTop: `1px solid ${T.lgrey}`, fontWeight: 700, fontSize: 13 }}>
              <span>Current {fmt.kes(totals.current)}</span>
              <span>1-30 {fmt.kes(totals.days1To30)}</span>
              <span>31-60 {fmt.kes(totals.days31To60)}</span>
              <span style={{ color: T.red }}>61+ {fmt.kes(totals.days61Plus)}</span>
              <span>Total {fmt.kes(totals.total)}</span>
            </div>
          </Card>
        </>
      )}
    </>
  )
}
