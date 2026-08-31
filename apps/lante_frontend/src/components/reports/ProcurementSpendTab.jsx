import { useEffect, useState, useCallback } from 'react'
import Chart from 'react-apexcharts'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Alert, SectionHeader, DataTable, Loading, EmptyState, Input, Select } from '../ui.jsx'
import { getProcurementSpend } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

export default function ProcurementSpendTab({ canExport }) {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [supplierId, setSupplierId] = useState('')
  const [category, setCategory] = useState('')
  const [supplierOptions, setSupplierOptions] = useState([{ value: '', label: 'All Suppliers' }])
  const [categoryOptions, setCategoryOptions] = useState([{ value: '', label: 'All Categories' }])
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    const params = {}
    if (from) params.from = from
    if (to) params.to = to
    if (supplierId) params.supplierId = supplierId
    if (category) params.category = category
    getProcurementSpend(Object.keys(params).length ? params : undefined)
      .then(res => {
        setData(res)
        if (!supplierId) {
          const bySupplier = res?.bySupplier ?? []
          setSupplierOptions([{ value: '', label: 'All Suppliers' }, ...bySupplier.map(s => ({ value: s.supplierId, label: s.supplierName }))])
        }
        if (!category) {
          const byCategory = res?.byCategory ?? []
          setCategoryOptions([{ value: '', label: 'All Categories' }, ...byCategory.map(c => ({ value: c.categoryName, label: c.categoryName }))])
        }
      })
      .catch(() => setError('Failed to load procurement spend.'))
      .finally(() => setLoading(false))
  }, [from, to, supplierId, category])

  useEffect(() => { load() }, [load])

  const bySupplier = data?.bySupplier ?? []
  const byCategory = data?.byCategory ?? []

  function exportSupplier() {
    exportToExcel({
      title: 'Procurement Spend — By Supplier',
      filename: 'procurement-spend-by-supplier',
      sheetName: 'By Supplier',
      columns: [
        { header: 'Supplier', accessor: r => r.supplierName, width: 30 },
        { header: 'Total Spend', accessor: r => r.totalLandedCost, width: 18 },
      ],
      rows: bySupplier,
    })
  }
  function exportCategory() {
    exportToExcel({
      title: 'Procurement Spend — By Category',
      filename: 'procurement-spend-by-category',
      sheetName: 'By Category',
      columns: [
        { header: 'Category', accessor: r => r.categoryName, width: 26 },
        { header: 'Total Spend', accessor: r => r.totalLandedCost, width: 18 },
      ],
      rows: byCategory,
    })
  }
  function exportSupplierPdf() {
    exportReportToPdf({
      title: 'Procurement Spend — By Supplier',
      filename: 'procurement-spend-by-supplier',
      summary: [{ label: 'Grand Total Spend', value: fmt.kes(data?.grandTotal) }],
      sections: [{
        heading: 'By Supplier',
        columns: [
          { header: 'Supplier', accessor: r => r.supplierName },
          { header: 'Total Spend', accessor: r => fmt.kes(r.totalLandedCost) },
        ],
        rows: bySupplier,
      }],
    })
  }
  function exportCategoryPdf() {
    exportReportToPdf({
      title: 'Procurement Spend — By Category',
      filename: 'procurement-spend-by-category',
      summary: [{ label: 'Grand Total Spend', value: fmt.kes(data?.grandTotal) }],
      sections: [{
        heading: 'By Category',
        columns: [
          { header: 'Category', accessor: r => r.categoryName },
          { header: 'Total Spend', accessor: r => fmt.kes(r.totalLandedCost) },
        ],
        rows: byCategory,
      }],
    })
  }

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 180 }}><Input label="From" type="date" value={from} onChange={setFrom} /></div>
        <div style={{ minWidth: 180 }}><Input label="To" type="date" value={to} onChange={setTo} /></div>
        <div style={{ minWidth: 200 }}><Select label="Supplier" value={supplierId} onChange={setSupplierId} options={supplierOptions} /></div>
        <div style={{ minWidth: 180 }}><Select label="Category" value={category} onChange={setCategory} options={categoryOptions} /></div>
        <Btn size="sm" onClick={load} style={{ marginBottom: 14 }}>Refresh</Btn>
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : !data || (bySupplier.length === 0 && byCategory.length === 0) ? (
        <EmptyState title="No procurement spend data" sub="Try widening the date range or clearing filters." />
      ) : (
        <>
          <div style={{ ...KPI_GRID, marginBottom: 22 }}>
            <Kpi label="Grand Total Spend" value={fmt.kes(data.grandTotal)} icon="💸" />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(340px,1fr))', gap: 18 }}>
            <div>
              <SectionHeader title="Spend by Supplier" action={canExport && bySupplier.length > 0 && <div style={{ display: 'flex', gap: 8 }}>
                <Btn size="sm" variant="ghost" onClick={exportSupplier}>⬇ Export</Btn>
                <Btn size="sm" variant="ghost" onClick={exportSupplierPdf}>⬇ PDF</Btn>
              </div>} />
              {bySupplier.length > 0 && (
                <Card style={{ marginBottom: 14 }}>
                  <Chart type="bar" height={220}
                    series={[{ name: 'Spend', data: bySupplier.map(s => s.totalLandedCost ?? 0) }]}
                    options={{
                      chart: { toolbar: { show: false } },
                      colors: [T.navy],
                      plotOptions: { bar: { borderRadius: 4, columnWidth: '50%' } },
                      dataLabels: { enabled: false },
                      xaxis: { categories: bySupplier.map(s => s.supplierName), labels: { style: { fontSize: '10px' } } },
                      yaxis: { labels: { formatter: v => `${(v / 1000).toFixed(0)}k` } },
                      tooltip: { y: { formatter: v => fmt.kes(v) } },
                      grid: { borderColor: T.lgrey },
                    }} />
                </Card>
              )}
              <Card style={{ padding: 0, overflow: 'hidden' }}>
                <DataTable headers={['Supplier', 'Total Spend']} empty="No supplier spend."
                  rows={bySupplier.map(s => [<strong>{s.supplierName}</strong>, fmt.kes(s.totalLandedCost)])} />
              </Card>
            </div>
            <div>
              <SectionHeader title="Spend by Category" action={canExport && byCategory.length > 0 && <div style={{ display: 'flex', gap: 8 }}>
                <Btn size="sm" variant="ghost" onClick={exportCategory}>⬇ Export</Btn>
                <Btn size="sm" variant="ghost" onClick={exportCategoryPdf}>⬇ PDF</Btn>
              </div>} />
              {byCategory.length > 0 && (
                <Card style={{ marginBottom: 14 }}>
                  <Chart type="bar" height={220}
                    series={[{ name: 'Spend', data: byCategory.map(c => c.totalLandedCost ?? 0) }]}
                    options={{
                      chart: { toolbar: { show: false } },
                      colors: [T.gold],
                      plotOptions: { bar: { borderRadius: 4, columnWidth: '50%' } },
                      dataLabels: { enabled: false },
                      xaxis: { categories: byCategory.map(c => c.categoryName), labels: { style: { fontSize: '10px' } } },
                      yaxis: { labels: { formatter: v => `${(v / 1000).toFixed(0)}k` } },
                      tooltip: { y: { formatter: v => fmt.kes(v) } },
                      grid: { borderColor: T.lgrey },
                    }} />
                </Card>
              )}
              <Card style={{ padding: 0, overflow: 'hidden' }}>
                <DataTable headers={['Category', 'Total Spend']} empty="No category spend."
                  rows={byCategory.map(c => [<strong>{c.categoryName}</strong>, fmt.kes(c.totalLandedCost)])} />
              </Card>
            </div>
          </div>
        </>
      )}
    </>
  )
}
