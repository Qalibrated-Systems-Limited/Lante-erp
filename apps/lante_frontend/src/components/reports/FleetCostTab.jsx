import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Alert, SectionHeader, DataTable, Loading, EmptyState, Input, Select } from '../ui.jsx'
import { getFleetCostUtilisation } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

export default function FleetCostTab({ canExport }) {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [truckId, setTruckId] = useState('')
  const [truckOptions, setTruckOptions] = useState([{ value: '', label: 'All Trucks' }])
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    const params = {}
    if (from) params.from = from
    if (to) params.to = to
    if (truckId) params.truckId = truckId
    getFleetCostUtilisation(Object.keys(params).length ? params : undefined)
      .then(res => {
        setData(res)
        // Seed the truck picker's options the first time (unfiltered) results come back.
        if (!truckId) {
          const trucks = res?.trucks ?? []
          setTruckOptions([{ value: '', label: 'All Trucks' }, ...trucks.map(t => ({ value: t.truckId, label: t.licensePlate }))])
        }
      })
      .catch(() => setError('Failed to load fleet cost & utilisation.'))
      .finally(() => setLoading(false))
  }, [from, to, truckId])

  useEffect(() => { load() }, [load])

  const trucks = data?.trucks ?? []
  const totals = data?.totals ?? {}

  function doExport() {
    exportToExcel({
      title: 'Fleet Cost & Utilisation',
      filename: 'fleet-cost-utilisation',
      sheetName: 'Trucks',
      columns: [
        { header: 'Plate Number', accessor: r => r.licensePlate, width: 16 },
        { header: 'Trips', accessor: r => r.tripCount, width: 12 },
        { header: 'Total Cost', accessor: r => r.totalCost, width: 16 },
        { header: 'Total Revenue', accessor: r => r.totalRevenue, width: 16 },
        { header: 'Total Profit', accessor: r => r.totalProfit, width: 16 },
        { header: 'Total Mileage', accessor: r => r.totalMileage, width: 16 },
      ],
      rows: trucks,
    })
  }
  function doExportPdf() {
    exportReportToPdf({
      title: 'Fleet Cost & Utilisation Report',
      filename: 'fleet-cost-utilisation',
      summary: [
        { label: 'Total Cost', value: fmt.kes(totals.totalCost) },
        { label: 'Total Revenue', value: fmt.kes(totals.totalRevenue) },
        { label: 'Total Profit', value: fmt.kes(totals.totalProfit) },
        { label: 'Total Mileage', value: fmt.num(totals.totalMileage) },
      ],
      sections: [{
        heading: 'Per-Truck Cost & Utilisation',
        columns: [
          { header: 'Plate Number', accessor: r => r.licensePlate },
          { header: 'Trips', accessor: r => fmt.num(r.tripCount) },
          { header: 'Total Cost', accessor: r => fmt.kes(r.totalCost) },
          { header: 'Total Revenue', accessor: r => fmt.kes(r.totalRevenue) },
          { header: 'Total Profit', accessor: r => fmt.kes(r.totalProfit) },
          { header: 'Total Mileage', accessor: r => fmt.num(r.totalMileage) },
        ],
        rows: trucks,
      }],
    })
  }

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 180 }}><Input label="From" type="date" value={from} onChange={setFrom} /></div>
        <div style={{ minWidth: 180 }}><Input label="To" type="date" value={to} onChange={setTo} /></div>
        <div style={{ minWidth: 200 }}><Select label="Truck" value={truckId} onChange={setTruckId} options={truckOptions} /></div>
        <Btn size="sm" onClick={load} style={{ marginBottom: 14 }}>Refresh</Btn>
        {canExport && trucks.length > 0 && <Btn size="sm" variant="ghost" onClick={doExport} style={{ marginBottom: 14 }}>⬇ Export</Btn>}
        {canExport && trucks.length > 0 && <Btn size="sm" variant="ghost" onClick={doExportPdf} style={{ marginBottom: 14 }}>⬇ PDF</Btn>}
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : trucks.length === 0 ? (
        <EmptyState title="No fleet cost data" sub="Try widening the date range." />
      ) : (
        <>
          <div style={{ ...KPI_GRID, marginBottom: 22 }}>
            <Kpi label="Total Cost" value={fmt.kes(totals.totalCost)} icon="🧾" variant="red" />
            <Kpi label="Total Revenue" value={fmt.kes(totals.totalRevenue)} icon="💵" variant="green" />
            <Kpi label="Total Profit" value={fmt.kes(totals.totalProfit)} icon="📈" variant={(totals.totalProfit ?? 0) >= 0 ? 'green' : 'red'} />
            <Kpi label="Total Mileage" value={fmt.num(totals.totalMileage)} icon="🛣️" />
          </div>

          <SectionHeader title="Per-Truck Cost & Utilisation" />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Plate Number', 'Trips', 'Total Cost', 'Total Revenue', 'Total Profit', 'Total Mileage']}
              rows={trucks.map(t => [
                <strong>{t.licensePlate}</strong>, fmt.num(t.tripCount), fmt.kes(t.totalCost), fmt.kes(t.totalRevenue),
                <span style={{ color: (t.totalProfit ?? 0) < 0 ? T.red : T.green, fontWeight: 700 }}>{fmt.kes(t.totalProfit)}</span>,
                fmt.num(t.totalMileage),
              ])} />
          </Card>
        </>
      )}
    </>
  )
}
