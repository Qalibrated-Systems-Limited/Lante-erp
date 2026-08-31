import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Ruler, Car, Calendar } from 'lucide-react'
import api from '../../../api/axios.js'
import ReactApexChart from 'react-apexcharts'

function fmt(n) {
  return new Intl.NumberFormat('en-KE', {
    style: 'currency',
    currency: 'KES',
    maximumFractionDigits: 0
  }).format(n ?? 0)
}

// Compact Stat Card
function StatCard({ label, value, color, icon, loading, onClick }) {
  const Tag = onClick ? 'button' : 'div'
  return (
      <Tag
        onClick={onClick}
        className={`bg-white rounded-2xl border border-gray-100 shadow-sm p-4 text-left w-full transition-all ${onClick ? 'hover:shadow-md hover:border-gray-200 hover:-translate-y-0.5 cursor-pointer' : ''}`}
      >
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">{label}</p>
            {loading || value == null ? (
              <div className="h-7 w-16 bg-gray-100 rounded-md animate-pulse mt-1.5" />
            ) : (
              <p className="text-2xl font-extrabold text-gray-900 mt-1 break-words">{value}</p>
            )}
          </div>
          <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-lg flex-shrink-0 ${color}`}>
            {icon}
          </div>
        </div>
      </Tag>
  )
}

function TripRow({ trip, onClick }) {
  const STATUS = {
    Pending:    { dot: 'bg-blue-400', label: 'Pending' },
    InProgress: { dot: 'bg-amber-400', label: 'In Progress' },
    Completed:  { dot: 'bg-green-400', label: 'Completed' },
    Cancelled:  { dot: 'bg-red-400', label: 'Cancelled' },
  }
  const s = STATUS[trip.status] ?? { dot: 'bg-gray-400', label: trip.status || 'Unknown' }

  return (
      <button
          onClick={onClick}
          className="w-full flex items-center gap-4 px-4 py-3 hover:bg-gray-50 transition-colors text-left border-b border-gray-50 last:border-0 group"
      >
        <span className={`flex-shrink-0 w-2.5 h-2.5 rounded-full ${s.dot}`} />
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-gray-900 truncate">{trip.startLocation} → {trip.endLocation}</p>
          <p className="text-xs text-gray-400">{s.label}</p>
        </div>
        {trip.totalCost > 0 && <p className="text-xs font-semibold text-gray-500">{fmt(trip.totalCost)}</p>}
      </button>
  )
}

export default function TrucksDashboardTab() {
  const navigate = useNavigate()

  const [todayStats, setTodayStats] = useState({ trips: 0, revenue: 0, mileage: 0, expenses: 0, profit: 0 })
  const [topTrucksToday, setTopTrucksToday] = useState([])
  const [recentTrips, setRecentTrips] = useState([])
  const [pendingVehicleRequests, setPendingVehicleRequests] = useState(0)

  const [revenueSeries, setRevenueSeries] = useState([])
  const [statusSeries, setStatusSeries] = useState([])

  const [loading, setLoading] = useState(true)

  // Generate last 12 months labels
  const getLast12Months = () => {
    const labels = []
    const date = new Date()
    for (let i = 11; i >= 0; i--) {
      const d = new Date(date.getFullYear(), date.getMonth() - i, 1)
      labels.push(d.toLocaleString('default', { month: 'short' }))
    }
    return labels
  }

  const loadAll = useCallback(async () => {
    setLoading(true)

    try {
      const today = new Date().toISOString().split('T')[0]

      // Fetch more trips for monthly calculation
      const [todayRes, recentRes, allRecentRes, pendingRes] = await Promise.allSettled([
        api.get('/api/v1/trips', { params: { pageNumber: 1, pageSize: 200, startDate: today } }),
        api.get('/api/v1/trips', { params: { pageNumber: 1, pageSize: 10 } }),
        api.get('/api/v1/trips', { params: { pageNumber: 1, pageSize: 500 } }),   // Used for monthly trend
        api.get('/api/v1/fleet/field-vehicles/dispatches/pending-count'),
      ])

      const todayTrips = todayRes.value?.data?.data?.items || []
      const recentTripsData = recentRes.value?.data?.data?.items || []
      const allTripsForTrend = allRecentRes.value?.data?.data?.items || []
      setPendingVehicleRequests(pendingRes.value?.data?.data?.count ?? 0)

      // === Today's Summary ===
      let totalRevenueToday = 0
      let totalMileageToday = 0
      let totalExpensesToday = 0
      const truckMap = {}

      todayTrips.forEach(trip => {
        totalRevenueToday += trip.revenue || 0
        totalMileageToday += trip.totalMileage || 0
        totalExpensesToday += trip.totalCost || 0

        if (trip.truckId) {
          if (!truckMap[trip.truckId]) {
            truckMap[trip.truckId] = { truckId: trip.truckId, revenue: 0, mileage: 0, name: `Truck ${trip.truckId.slice(0,8)}` }
          }
          truckMap[trip.truckId].revenue += trip.revenue || 0
          truckMap[trip.truckId].mileage += trip.totalMileage || 0
        }
      })

      const topTrucks = Object.values(truckMap)
          .sort((a, b) => b.revenue - a.revenue)
          .slice(0, 6)

      // Resolve license plates for the top trucks — trips only carry the truckId.
      await Promise.all(topTrucks.map(t =>
        api.get(`/api/v1/trucks/${t.truckId}`)
          .then(res => { const truck = res.data?.data; if (truck) t.name = truck.licensePlate })
          .catch(() => {})
      ))

      setTodayStats({
        trips: todayTrips.length,
        revenue: totalRevenueToday,
        mileage: Math.round(totalMileageToday),
        expenses: totalExpensesToday,
        profit: totalRevenueToday - totalExpensesToday,
      })

      setTopTrucksToday(topTrucks)
      setRecentTrips(recentTripsData)

      // === Real Monthly Revenue Trend (Last 12 Months) ===
      const monthlyRevenue = {}
      const now = new Date()

      allTripsForTrend.forEach(trip => {
        if (!trip.date) return
        const tripDate = new Date(trip.date)
        const key = `${tripDate.getFullYear()}-${String(tripDate.getMonth() + 1).padStart(2, '0')}`

        if (!monthlyRevenue[key]) monthlyRevenue[key] = 0
        monthlyRevenue[key] += trip.revenue || 0
      })

      // Prepare last 12 months data
      const revenueData = []
      for (let i = 11; i >= 0; i--) {
        const d = new Date(now.getFullYear(), now.getMonth() - i, 1)
        const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
        revenueData.push(monthlyRevenue[key] || 0)
      }

      setRevenueSeries([{ name: "Revenue", data: revenueData }])

      // Status for pie chart (today)
      const inProgress = todayTrips.filter(t => t.status === 'InProgress').length
      const pending = todayTrips.filter(t => t.status === 'Pending').length
      const completed = todayTrips.filter(t => t.status === 'Completed').length

      setStatusSeries([inProgress, pending, completed])

    } catch (err) {
      console.error(err)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    loadAll()
  }, [loadAll])

  // Separate lightweight poll just for the pending-requests count — loadAll() above also builds
  // trip charts/revenue trends, too heavy to re-run every 30s (and would flicker the charts).
  // This is the one number that needs to feel live: it's what tells a fleet manager sitting on
  // this tab that a new request came in, or that one they just approved/rejected elsewhere
  // (another tab, another manager) has cleared.
  useEffect(() => {
    const id = setInterval(() => {
      api.get('/api/v1/fleet/field-vehicles/dispatches/pending-count')
        .then(res => setPendingVehicleRequests(res.data?.data?.count ?? 0))
        .catch(() => {})
    }, 30_000)
    return () => clearInterval(id)
  }, [])

  const monthLabels = getLast12Months()

  const revenueOptions = {
    chart: {
      type: 'area',
      height: 280,
      toolbar: { show: false },
      animations: { enabled: true, speed: 800 }
    },
    colors: ['#1B3A5C'],
    stroke: { curve: 'smooth', width: 3 },
    dataLabels: { enabled: false },
    xaxis: {
      categories: monthLabels,
      labels: { style: { colors: '#6b7280', fontSize: '12px' } }
    },
    yaxis: {
      labels: { formatter: val => fmt(val), style: { colors: '#6b7280' } }
    },
    tooltip: { y: { formatter: val => fmt(val) } },
    fill: { type: 'gradient', gradient: { opacityFrom: 0.6, opacityTo: 0.1 } }
  }

  const statusOptions = {
    chart: { type: 'donut', height: 280 },
    labels: ['In Progress', 'Pending', 'Completed'],
    colors: ['#C8960C', '#2E5F8A', '#16A34A'],
    legend: { position: 'bottom' },
    plotOptions: { pie: { donut: { size: '70%' } } },
    dataLabels: { enabled: false }
  }

  const truckRevenueOptions = {
    chart: { type: 'bar', height: 320, toolbar: { show: false } },
    plotOptions: { bar: { horizontal: true, borderRadius: 6 } },
    colors: ['#C8960C'],
    xaxis: { categories: topTrucksToday.map(t => t.name) },
    tooltip: { y: { formatter: val => fmt(val) } }
  }

  const truckRevenueSeries = [{
    name: "Revenue Today",
    data: topTrucksToday.map(t => t.revenue)
  }]

  return (
    <div className="space-y-6">
      {/* Truck-specific figures not already covered by the Overview row above */}
      <div className="grid grid-cols-2 gap-3">
        <StatCard icon={<Ruler size={20} />} color="bg-orange-50" label="Mileage Today" value={`${todayStats.mileage} km`} loading={loading} />
        <StatCard
          icon={<Car size={20} />}
          color={pendingVehicleRequests > 0 ? 'bg-amber-50' : 'bg-gray-50'}
          label="Pending Vehicle Requests"
          value={pendingVehicleRequests}
          loading={loading}
          onClick={() => navigate('/modules/fleet/requests')}
        />
      </div>

      {/* Top Trucks Today */}
      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
        <h2 className="font-semibold mb-4">Top Performing Trucks Today</h2>
        {topTrucksToday.length > 0 ? (
            <ReactApexChart options={truckRevenueOptions} series={truckRevenueSeries} type="bar" height={320} />
        ) : (
            <p className="text-gray-400 py-12 text-center">No trips recorded today</p>
        )}
      </div>

      {/* Charts */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
          <h2 className="text-lg font-semibold mb-4">Revenue Trend (Last 12 Months)</h2>
          <ReactApexChart
              options={revenueOptions}
              series={revenueSeries}
              type="area"
              height={280}
          />
        </div>

        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
          <h2 className="text-lg font-semibold mb-4">Today's Trips by Status</h2>
          {todayStats.trips > 0 ? (
              <ReactApexChart
                  options={statusOptions}
                  series={statusSeries}
                  type="donut"
                  height={280}
              />
          ) : (
              <p className="text-gray-400 py-12 text-center">No trips recorded today</p>
          )}
        </div>
      </div>

      {/* Recent Activity */}
      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
        <div className="px-5 py-4 border-b">
          <h2 className="font-bold text-gray-900">Recent Activity</h2>
        </div>
        {loading ? (
            <div className="py-16 text-center text-gray-400">Loading...</div>
        ) : recentTrips.length === 0 ? (
            <div className="py-16 text-center">
              <div className="flex justify-center mb-4 opacity-30"><Calendar size={48} /></div>
              <p className="text-gray-400">No recent trips</p>
            </div>
        ) : (
            <div className="divide-y divide-gray-100">
              {recentTrips.map(t => (
                  <TripRow key={t.id} trip={t} onClick={() => navigate(`/modules/fleet/trips/${t.id}`)} />
              ))}
            </div>
        )}
      </div>
    </div>
  )
}
