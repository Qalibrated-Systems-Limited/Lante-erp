import { useState, useEffect, useCallback } from 'react'
import api from '../../../api/axios.js'
import ReactApexChart from 'react-apexcharts'

function fmt(n) {
  return new Intl.NumberFormat('en-KE', {
    style: 'currency',
    currency: 'KES',
    maximumFractionDigits: 0
  }).format(n ?? 0)
}

const TOP_N = 8

export default function MaterialsDashboardTab() {
  const [topMaterials, setTopMaterials] = useState([])
  const [tripsWithMaterial, setTripsWithMaterial] = useState(0)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      // Same "most recent 500 trips" batch the Trucks tab's revenue trend uses —
      // there's no backend aggregation endpoint in this codebase, every dashboard
      // chart here groups a fetched page client-side.
      const res = await api.get('/api/v1/trips', { params: { pageNumber: 1, pageSize: 500 } })
      const trips = res.data?.data?.items || []

      const materialMap = {}
      let withMaterial = 0

      trips.forEach(trip => {
        if (!trip.materialId) return
        withMaterial += 1
        if (!materialMap[trip.materialId]) {
          materialMap[trip.materialId] = {
            materialId: trip.materialId,
            name: trip.materialName || 'Unnamed material',
            trips: 0,
            cost: 0,
          }
        }
        materialMap[trip.materialId].trips += 1
        materialMap[trip.materialId].cost += trip.materialCost || 0
      })

      const ranked = Object.values(materialMap)
        .sort((a, b) => b.trips - a.trips)
        .slice(0, TOP_N)

      setTopMaterials(ranked)
      setTripsWithMaterial(withMaterial)
    } catch (err) {
      console.error(err)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  const options = {
    chart: { type: 'bar', height: 320, toolbar: { show: false } },
    plotOptions: { bar: { horizontal: true, borderRadius: 6 } },
    colors: ['#C8960C'],
    xaxis: { categories: topMaterials.map(m => m.name) },
    dataLabels: { enabled: false },
    tooltip: {
      y: {
        formatter: (val, { dataPointIndex }) =>
          `${val} trip${val === 1 ? '' : 's'} · ${fmt(topMaterials[dataPointIndex]?.cost)} total`,
      },
    },
  }

  const series = [{ name: 'Trips', data: topMaterials.map(m => m.trips) }]

  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
      <div className="flex items-baseline justify-between gap-2 mb-4">
        <h2 className="font-semibold">Most Used Materials</h2>
        {!loading && (
          <span className="text-xs text-gray-400">{tripsWithMaterial} loaded trips</span>
        )}
      </div>
      {loading ? (
        <div className="h-80 flex items-center justify-center text-gray-400">Loading…</div>
      ) : topMaterials.length > 0 ? (
        <ReactApexChart options={options} series={series} type="bar" height={320} />
      ) : (
        <p className="text-gray-400 py-12 text-center">No trips with a recorded material yet</p>
      )}
    </div>
  )
}
