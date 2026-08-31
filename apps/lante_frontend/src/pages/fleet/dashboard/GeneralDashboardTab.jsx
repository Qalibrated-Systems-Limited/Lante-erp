import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Truck, Car, Container, Banknote, TrendingDown, TrendingUp } from 'lucide-react'
import api from '../../../api/axios.js'
import FleetFlowGuideCard from './FleetFlowGuideCard.jsx'

function fmt(n) {
  return new Intl.NumberFormat('en-KE', {
    style: 'currency',
    currency: 'KES',
    maximumFractionDigits: 0
  }).format(n ?? 0)
}

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
          {loading ? (
            <div className="h-7 w-16 bg-gray-100 rounded-md animate-pulse mt-1.5" />
          ) : (
            <p className="text-2xl font-extrabold text-gray-900 mt-1 break-words">{value}</p>
          )}
        </div>
        <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-lg flex-shrink-0 ${color}`}>{icon}</div>
      </div>
    </Tag>
  )
}

/// <summary>Combined overview at the top of the single Fleet Dashboard page — totals and
/// today's financials across both Trucks and Field Vehicles. Per-fleet-type detail (Available/
/// Borrowed breakdown, charts, activity) lives further down the same page, not behind a tab.</summary>
export default function GeneralDashboardTab() {
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [stats, setStats] = useState({
    truckCount: 0, fieldVehicleCount: 0,
    tripsToday: 0, revenueToday: 0, expensesToday: 0, profitToday: 0,
  })

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const today = new Date().toISOString().split('T')[0]
      const [trucksRes, fvRes, tripsRes] = await Promise.allSettled([
        api.get('/api/v1/trucks'),
        api.get('/api/v1/fleet/field-vehicles', { params: { page: 1, pageSize: 1000 } }),
        api.get('/api/v1/trips', { params: { pageNumber: 1, pageSize: 200, startDate: today } }),
      ])

      const trucks = trucksRes.value?.data?.data ?? []
      const fieldVehicles = fvRes.value?.data?.data?.items ?? []
      const todayTrips = tripsRes.value?.data?.data?.items ?? []

      const revenueToday = todayTrips.reduce((s, t) => s + (t.revenue || 0), 0)
      const expensesToday = todayTrips.reduce((s, t) => s + (t.totalCost || 0), 0)

      setStats({
        truckCount: Array.isArray(trucks) ? trucks.length : (trucks?.totalCount ?? 0),
        fieldVehicleCount: fieldVehicles.length,
        tripsToday: todayTrips.length,
        revenueToday,
        expensesToday,
        profitToday: revenueToday - expensesToday,
      })
    } catch {
      // leave defaults
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  return (
    <div className="space-y-4">
      <FleetFlowGuideCard />

      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard icon={<Truck size={20} />} color="bg-blue-50" label="Trucks" value={stats.truckCount} loading={loading} onClick={() => navigate('/modules/fleet/trucks')} />
        <StatCard icon={<Car size={20} />} color="bg-indigo-50" label="Field Vehicles" value={stats.fieldVehicleCount} loading={loading} onClick={() => navigate('/modules/fleet/field-vehicles')} />
        <StatCard icon={<Container size={20} />} color="bg-blue-50" label="Trips Today" value={stats.tripsToday} loading={loading} />
        <StatCard icon={<Banknote size={20} />} color="bg-emerald-50" label="Revenue Today" value={fmt(stats.revenueToday)} loading={loading} />
        <StatCard icon={<TrendingDown size={20} />} color="bg-red-50" label="Expenses Today" value={fmt(stats.expensesToday)} loading={loading} />
        <StatCard icon={<TrendingUp size={20} />} color={stats.profitToday >= 0 ? 'bg-emerald-50' : 'bg-red-50'} label="Profit Today" value={fmt(stats.profitToday)} loading={loading} />
      </div>
    </div>
  )
}
