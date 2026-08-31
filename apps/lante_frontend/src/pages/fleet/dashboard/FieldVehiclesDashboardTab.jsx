import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Car, CheckCircle2, ArrowRightLeft, Wrench } from 'lucide-react'
import api from '../../../api/axios.js'

const STATUS_BADGE = {
  Available: 'bg-green-100 text-green-700',
  Dispatched: 'bg-blue-100 text-blue-700',
  UnderMaintenance: 'bg-amber-100 text-amber-700',
  Decommissioned: 'bg-gray-100 text-gray-500',
}
const STATUS_LABEL = {
  Available: 'Available',
  Dispatched: 'Borrowed',
  UnderMaintenance: 'Under Maintenance',
  Decommissioned: 'Decommissioned',
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

function VehicleRow({ v, onClick }) {
  return (
    <button onClick={onClick} className="w-full flex items-center gap-4 px-4 py-3 hover:bg-gray-50 transition-colors text-left border-b border-gray-50 last:border-0">
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-gray-900 truncate">{v.registrationNumber} — {v.make} {v.model}</p>
        <p className="text-xs text-gray-400">{v.type}</p>
      </div>
      <span className={`text-xs font-semibold px-2.5 py-1 rounded-full ${STATUS_BADGE[v.status] ?? 'bg-gray-100 text-gray-500'}`}>
        {STATUS_LABEL[v.status] ?? v.status}
      </span>
    </button>
  )
}

export default function FieldVehiclesDashboardTab() {
  const navigate = useNavigate()
  const [vehicles, setVehicles] = useState([])
  const [loading, setLoading] = useState(true)

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true)
    try {
      const res = await api.get('/api/v1/fleet/field-vehicles', { params: { page: 1, pageSize: 1000 } })
      setVehicles(res.data?.data?.items ?? [])
    } catch {
      if (!silent) setVehicles([])
    } finally {
      if (!silent) setLoading(false)
    }
  }, [])

  // Poll so a fleet manager sitting on this tab sees an Available→Dispatched transition (or vice
  // versa, on return) as soon as someone approves/rejects/returns a dispatch, without needing to
  // navigate away and back to trigger a refetch. Silent — no loading skeleton flash every 30s.
  useEffect(() => {
    load()
    const id = setInterval(() => load(true), 30_000)
    return () => clearInterval(id)
  }, [load])

  const counts = {
    Available: vehicles.filter(v => v.status === 'Available').length,
    Dispatched: vehicles.filter(v => v.status === 'Dispatched').length,
    UnderMaintenance: vehicles.filter(v => v.status === 'UnderMaintenance').length,
    Decommissioned: vehicles.filter(v => v.status === 'Decommissioned').length,
  }
  const borrowed = vehicles.filter(v => v.status === 'Dispatched')
  const available = vehicles.filter(v => v.status === 'Available')

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <StatCard icon={<Car size={20} />} color="bg-gray-50" label="Total Field Vehicles" value={vehicles.length} loading={loading}
          onClick={() => navigate('/modules/fleet/field-vehicles')} />
        <StatCard icon={<CheckCircle2 size={20} />} color="bg-green-50" label="Available" value={counts.Available} loading={loading}
          onClick={() => navigate('/modules/fleet/field-vehicles?status=Available')} />
        <StatCard icon={<ArrowRightLeft size={20} />} color="bg-blue-50" label="Borrowed (Dispatched)" value={counts.Dispatched} loading={loading}
          onClick={() => navigate('/modules/fleet/field-vehicles?status=Dispatched')} />
        <StatCard icon={<Wrench size={20} />} color="bg-amber-50" label="Under Maintenance" value={counts.UnderMaintenance} loading={loading}
          onClick={() => navigate('/modules/fleet/field-vehicles?status=UnderMaintenance')} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b">
            <h2 className="font-bold text-gray-900">Currently Borrowed</h2>
          </div>
          {loading ? (
            <div className="py-16 text-center text-gray-400">Loading...</div>
          ) : borrowed.length === 0 ? (
            <div className="py-16 text-center text-gray-400">No vehicles are currently borrowed.</div>
          ) : (
            <div className="divide-y divide-gray-100">
              {borrowed.map(v => (
                <VehicleRow key={v.id} v={v} onClick={() => navigate(`/modules/fleet/field-vehicles/${v.id}`)} />
              ))}
            </div>
          )}
        </div>

        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b">
            <h2 className="font-bold text-gray-900">Available Now</h2>
          </div>
          {loading ? (
            <div className="py-16 text-center text-gray-400">Loading...</div>
          ) : available.length === 0 ? (
            <div className="py-16 text-center text-gray-400">No vehicles are currently available.</div>
          ) : (
            <div className="divide-y divide-gray-100">
              {available.map(v => (
                <VehicleRow key={v.id} v={v} onClick={() => navigate(`/modules/fleet/field-vehicles/${v.id}`)} />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
