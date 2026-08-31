import { useState, useEffect, useCallback } from 'react'
import { FileText, BarChart3, Car, CheckCircle2, Wrench, ClipboardList, Truck } from 'lucide-react'
import FleetNav from './FleetNav.jsx'
import api from '../../api/axios.js'
import { exportToPdf, exportToExcel, TRUCK_COLUMNS } from '../../utils/export.js'
import Collapsible from '../../components/Collapsible.jsx'
import Pagination from '../../components/Pagination.jsx'

const inputCls = 'w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold'
const PAGE_SIZE = 20
const EXPORT_CAP = 100

// Mirrors FleetService.Core.Entities.TruckStatus — numeric values match the C# enum
// declaration order, since the API serializes them as numbers (no JsonStringEnumConverter).
const TRUCK_STATUS = { ACTIVE: 0, IN_MAINTENANCE: 1, DECOMMISSIONED: 2, OUT_OF_SERVICE: 3 }
const TRUCK_STATUS_LABEL = { 0: 'Active', 1: 'In Maintenance', 2: 'Decommissioned', 3: 'Out of Service' }
const STATUS_STYLE = {
  0: 'bg-green-100 text-green-700',
  1: 'bg-amber-100 text-amber-700',
  2: 'bg-gray-100 text-gray-500',
  3: 'bg-red-100 text-red-700',
}

// Days until a date (negative = already past). Returns null for missing dates.
function daysUntil(dateStr) {
  if (!dateStr) return null
  const d = new Date(dateStr)
  if (isNaN(d)) return null
  return Math.ceil((d.getTime() - Date.now()) / 86400000)
}

function fmtDate(dateStr) {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  return isNaN(d) ? '—' : d.toLocaleDateString('en-KE')
}

// Same due/not-due rule the table itself uses to color the Next Service cell —
// mileage wins outright once configured, date is only a fallback.
function isServiceDue(t) {
  if (t.nextServiceOdometer != null) return t.isServiceDueByMileage === true
  const d = daysUntil(t.nextServiceDate)
  return d !== null && d <= 30
}

function isInsuranceExpiring(t) {
  const d = daysUntil(t.insuranceExpiryDate)
  return d !== null && d <= 30
}

function KpiCard({ label, value, icon, tone = 'navy', onClick, active }) {
  const toneCls = {
    navy:  'bg-navy/10 text-navy',
    green: 'bg-green-100 text-green-700',
    amber: 'bg-amber-100 text-amber-700',
    red:   'bg-red-100 text-red-700',
  }[tone]
  const ringCls = {
    navy:  'border-navy ring-2 ring-navy/20',
    green: 'border-green-600 ring-2 ring-green-600/20',
    amber: 'border-amber-500 ring-2 ring-amber-500/20',
    red:   'border-red-600 ring-2 ring-red-600/20',
  }[tone]
  return (
      <div
        onClick={onClick}
        className={`bg-white rounded-2xl border shadow-sm p-4 ${active ? ringCls : 'border-gray-100'} ${onClick ? 'cursor-pointer hover:shadow-md transition-shadow' : ''}`}
      >
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">{label}</p>
            <p className="text-2xl font-extrabold text-gray-900 mt-1 break-words">{value}</p>
          </div>
          <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-lg flex-shrink-0 ${toneCls}`}>{icon}</div>
        </div>
      </div>
  )
}

export default function FleetTrucksPage() {
  const [trucks, setTrucks]         = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [allTrucks, setAllTrucks]   = useState([]) // unpaged — KPIs must reflect the whole fleet, not just this page
  const [drivers, setDrivers]       = useState([])
  const [vehicleClasses, setVehicleClasses] = useState([])
  const [showAddClass, setShowAddClass] = useState(false)
  const [newClassForm, setNewClassForm] = useState({ name: '', description: '' })
  const [savingClass, setSavingClass] = useState(false)
  const [classErr, setClassErr] = useState('')
  const [loading, setLoading]       = useState(true)
  const [error, setError]           = useState('')
  const [page, setPage]             = useState(1)
  const [exporting, setExporting]   = useState(false)
  const [showForm, setShowForm]     = useState(false)
  const [saving, setSaving]         = useState(false)
  const [formErr, setFormErr]       = useState('')
  const [editId, setEditId]         = useState(null)
  const [statusFilter, setStatusFilter] = useState(null) // null | 'active' | 'serviceDue' | 'insuranceExpiring'

  const EMPTY_FORM = {
    licensePlate: '', model: '', driverId: '',
    vehicleClassId: '', insuranceExpiryDate: '', nextServiceDate: '', odometer: '', status: TRUCK_STATUS.ACTIVE,
    lastServiceDate: '', lastServiceOdometer: '', serviceIntervalKm: '',
  }
  const [form, setForm] = useState(EMPTY_FORM)

  const loadVehicleClasses = useCallback(async () => {
    try {
      const res = await api.get('/api/v1/vehicleclasses')
      setVehicleClasses(res.data?.data ?? [])
    } catch {
      setVehicleClasses([])
    }
  }, [])

  useEffect(() => { loadVehicleClasses() }, [loadVehicleClasses])

  async function handleCreateClass(e) {
    e.preventDefault()
    setClassErr('')
    if (!newClassForm.name.trim()) {
      setClassErr('Name is required.')
      return
    }
    setSavingClass(true)
    try {
      const res = await api.post('/api/v1/vehicleclasses', {
        name: newClassForm.name.trim(),
        description: newClassForm.description.trim() || null,
      })
      const created = res.data?.data
      setVehicleClasses(cs => [...cs, created])
      setForm(f => ({ ...f, vehicleClassId: created.id }))
      setNewClassForm({ name: '', description: '' })
      setShowAddClass(false)
    } catch (err) {
      setClassErr(err.response?.data?.message ?? 'Failed to create vehicle class.')
    } finally {
      setSavingClass(false)
    }
  }

  // Delete Modal State
  const [showDeleteModal, setShowDeleteModal] = useState(false)
  const [truckToDelete, setTruckToDelete] = useState(null)

  // Quick status-change state
  const [statusWorkingId, setStatusWorkingId] = useState(null)
  const [serviceTarget, setServiceTarget] = useState(null) // truck being marked serviced
  const [serviceOdometer, setServiceOdometer] = useState('')
  const [servicing, setServicing] = useState(false)
  const [serviceErr, setServiceErr] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [pagedRes, allRes, usersRes] = await Promise.all([
        api.get('/api/v1/trucks', { params: { pageNumber: page, pageSize: PAGE_SIZE } }),
        api.get('/api/v1/trucks'),
        api.get('/api/v1/users', { params: { pageSize: 200 } }),
      ])
      setTrucks(pagedRes.data?.data?.items ?? [])
      setTotalCount(pagedRes.data?.data?.totalCount ?? 0)
      setAllTrucks(allRes.data?.data ?? [])

      const allUsers = usersRes.data?.data?.items ?? []
      const fleetStaff = allUsers.filter(u => Array.isArray(u.roles) && u.roles.includes('Fleet Staff'))
      const profiles = await Promise.all(
          fleetStaff.map(u =>
              api.get(`/api/v1/DriverProfiles/driver/${u.id}/current`)
                  .then(res => res.data?.data ?? null)
                  .catch(() => null)
          )
      )
      setDrivers(fleetStaff.map((u, i) => ({
        id: u.id,
        name: profiles[i]?.fullName || `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.email,
      })))
    } catch {
      setError('Failed to load trucks.')
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  function driverName(driverId) {
    return drivers.find(d => d.id === driverId)?.name ?? (driverId ? 'Assigned' : '—')
  }

  async function fetchExportRows() {
    const res = await api.get('/api/v1/trucks', { params: { pageNumber: 1, pageSize: EXPORT_CAP } })
    const rows = (res.data?.data?.items ?? []).map(t => ({ ...t, driverName: driverName(t.driverId) }))
    const total = res.data?.data?.totalCount ?? rows.length
    return { rows, capped: total > rows.length, total }
  }

  // Export Handlers
  const handleExportPdf = async () => {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: "Fleet Trucks",
        subtitle: capped ? `Showing first ${rows.length} of ${total} vehicles` : `${total} vehicle${total !== 1 ? 's' : ''} in fleet`,
        columns: TRUCK_COLUMNS,
        rows,
        filename: `Fleet-Trucks-${new Date().toISOString().slice(0,10)}`,
        theme: 'navy',
        docModule: 'FLEET',
      })
    } catch {
      setError('Failed to export PDF.')
    } finally {
      setExporting(false)
    }
  }

  const handleExportExcel = async () => {
    setExporting(true)
    try {
      const { rows } = await fetchExportRows()
      exportToExcel({
        title: "Fleet Trucks",
        columns: TRUCK_COLUMNS,
        rows,
        filename: `Fleet-Trucks-${new Date().toISOString().slice(0,10)}`,
        sheetName: "Trucks"
      })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  function openCreate() {
    setEditId(null)
    setForm(EMPTY_FORM)
    setFormErr('')
    setShowForm(true)
  }

  function openEdit(truck) {
    setEditId(truck.id)
    setForm({
      licensePlate: truck.licensePlate,
      model: truck.model,
      driverId: truck.driverId ?? '',
      vehicleClassId: truck.vehicleClassId ?? '',
      insuranceExpiryDate: truck.insuranceExpiryDate ? truck.insuranceExpiryDate.slice(0, 10) : '',
      nextServiceDate: truck.nextServiceDate ? truck.nextServiceDate.slice(0, 10) : '',
      odometer: truck.odometer ?? '',
      status: truck.status ?? TRUCK_STATUS.ACTIVE,
      lastServiceDate: truck.lastServiceDate ? truck.lastServiceDate.slice(0, 10) : '',
      lastServiceOdometer: truck.lastServiceOdometer ?? '',
      serviceIntervalKm: truck.serviceIntervalKm ?? '',
    })
    setFormErr('')
    setShowForm(true)
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setFormErr('')
    if (!form.licensePlate || !form.model) {
      setFormErr('License plate and model are required.')
      return
    }
    setSaving(true)
    try {
      const payload = {
        ...form,
        driverId: form.driverId || null,
        vehicleClassId: form.vehicleClassId || null,
        insuranceExpiryDate: form.insuranceExpiryDate || null,
        nextServiceDate: form.nextServiceDate || null,
        odometer: form.odometer === '' ? null : Number(form.odometer),
        status: Number(form.status),
        lastServiceDate: form.lastServiceDate || null,
        lastServiceOdometer: form.lastServiceOdometer === '' ? null : Number(form.lastServiceOdometer),
        serviceIntervalKm: form.serviceIntervalKm === '' ? null : Number(form.serviceIntervalKm),
      }
      if (editId) {
        await api.put(`/api/v1/trucks/${editId}`, payload)
      } else {
        await api.post('/api/v1/trucks', payload)
      }
      setShowForm(false)
      setEditId(null)
      setForm(EMPTY_FORM)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to save truck.')
    } finally {
      setSaving(false)
    }
  }

  // Delete Handlers
  function openDeleteModal(truck) {
    setTruckToDelete(truck)
    setShowDeleteModal(true)
  }

  async function confirmDelete() {
    if (!truckToDelete) return
    try {
      await api.delete(`/api/v1/trucks/${truckToDelete.id}`)
      load()
    } catch {
      setError('Failed to remove truck.')
    } finally {
      setShowDeleteModal(false)
      setTruckToDelete(null)
    }
  }

  // Quick status-change handlers — the update endpoint takes the full truck payload
  // (no partial-patch route exists), so these are built from the row's own current fields.
  function putTruckUpdate(truck, overrides) {
    return api.put(`/api/v1/trucks/${truck.id}`, {
      licensePlate: truck.licensePlate,
      model: truck.model,
      driverId: truck.driverId || null,
      vehicleClassId: truck.vehicleClassId || null,
      insuranceExpiryDate: truck.insuranceExpiryDate || null,
      nextServiceDate: truck.nextServiceDate || null,
      odometer: truck.odometer ?? null,
      status: truck.status,
      lastServiceDate: truck.lastServiceDate || null,
      lastServiceOdometer: truck.lastServiceOdometer ?? null,
      serviceIntervalKm: truck.serviceIntervalKm ?? null,
      ...overrides,
    })
  }

  async function quickUpdateStatus(truck, newStatus) {
    setStatusWorkingId(truck.id)
    try {
      await putTruckUpdate(truck, { status: newStatus })
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to update truck status.')
    } finally {
      setStatusWorkingId(null)
    }
  }

  function openMarkServiced(truck) {
    setServiceTarget(truck)
    setServiceOdometer(truck.odometer != null ? String(truck.odometer) : '')
    setServiceErr('')
  }

  async function confirmMarkServiced(e) {
    e.preventDefault()
    setServiceErr('')
    if (serviceOdometer === '' || Number(serviceOdometer) < 0) {
      setServiceErr('Enter a valid odometer reading.')
      return
    }
    setServicing(true)
    try {
      await putTruckUpdate(serviceTarget, {
        status: TRUCK_STATUS.ACTIVE,
        odometer: Number(serviceOdometer),
        lastServiceDate: new Date().toISOString(),
        lastServiceOdometer: Number(serviceOdometer),
      })
      setServiceTarget(null)
      load()
    } catch (err) {
      setServiceErr(err.response?.data?.message ?? 'Failed to record service.')
    } finally {
      setServicing(false)
    }
  }

  function set(field, val) {
    setForm(f => ({ ...f, [field]: val }))
  }

  function toggleFilter(key) {
    setStatusFilter(f => f === key ? null : key)
    setPage(1)
  }

  // KPIs (match the deployed Fleet Register) computed from the whole fleet
  // (allTrucks), not just the current page. Service/insurance count anything
  // due within 30 days or already overdue.
  const fleetSize = allTrucks.length
  const activeCount = allTrucks.filter(t => (t.status ?? TRUCK_STATUS.ACTIVE) === TRUCK_STATUS.ACTIVE).length
  const serviceDue = allTrucks.filter(isServiceDue).length
  const insuranceExpiring = allTrucks.filter(isInsuranceExpiring).length

  // A KPI card click filters the table down to just that set, computed from the
  // whole fleet (allTrucks) since the server-paged `trucks` only covers one page.
  const FILTER_PREDICATES = {
    active: t => (t.status ?? TRUCK_STATUS.ACTIVE) === TRUCK_STATUS.ACTIVE,
    serviceDue: isServiceDue,
    insuranceExpiring: isInsuranceExpiring,
  }
  const filteredTrucks = statusFilter ? allTrucks.filter(FILTER_PREDICATES[statusFilter]) : null
  const displayTrucks = filteredTrucks ? filteredTrucks.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE) : trucks
  const displayTotal = filteredTrucks ? filteredTrucks.length : totalCount
  const FILTER_LABEL = { active: 'Active', serviceDue: 'Service Due', insuranceExpiring: 'Insurance Expiring' }

  return (
      <>
        <main className="w-full px-4 sm:px-6 py-8">
          <FleetNav />

          <Collapsible title="About the Truck Registry" dismissKey="fleet.pageInfo.trucks.dismissed">
            <p className="text-sm text-gray-700">
              The truck registry — insurance and service dates here drive the dashboard's expiry alerts.
            </p>
          </Collapsible>

          {/* Header */}
          <div className="flex items-center justify-between mb-6">
            <div>
              <h1 className="text-2xl font-extrabold text-navy">Trucks</h1>
              <p className="text-sm text-gray-500 mt-0.5">
                {loading ? 'Loading…' : statusFilter
                  ? <>{displayTotal} vehicle{displayTotal !== 1 ? 's' : ''} · {FILTER_LABEL[statusFilter]}{' '}
                      <button onClick={() => setStatusFilter(null)} className="text-navy underline underline-offset-2 hover:text-navy-dark">Clear filter</button></>
                  : `${totalCount} vehicle${totalCount !== 1 ? 's' : ''} in fleet`}
              </p>
            </div>

            <div className="flex items-center gap-3">
              {/* Export Buttons */}
              <button
                  onClick={handleExportPdf}
                  disabled={exporting || totalCount === 0}
                  className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50"
              >
                <FileText size={14} /> PDF
              </button>

              <button
                  onClick={handleExportExcel}
                  disabled={exporting || totalCount === 0}
                  className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50"
              >
                <BarChart3 size={14} /> Excel
              </button>

              {/* Add Truck Button */}
              <button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl transition-colors shadow"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Add Truck
              </button>
            </div>
          </div>

          {/* KPIs — Fleet Register */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
            <KpiCard label="Fleet Size"          value={fleetSize}         icon={<Car size={20} />} tone="navy"
              onClick={() => setStatusFilter(null)} active={!statusFilter} />
            <KpiCard label="Active"              value={activeCount}       icon={<CheckCircle2 size={20} />} tone="green"
              onClick={() => toggleFilter('active')} active={statusFilter === 'active'} />
            <KpiCard label="Service Due"         value={serviceDue}        icon={<Wrench size={20} />} tone="amber"
              onClick={() => toggleFilter('serviceDue')} active={statusFilter === 'serviceDue'} />
            <KpiCard label="Insurance Expiring"  value={insuranceExpiring} icon={<ClipboardList size={20} />} tone="red"
              onClick={() => toggleFilter('insuranceExpiring')} active={statusFilter === 'insuranceExpiring'} />
          </div>

          {/* Inline Form */}
          {showForm && (
              <form onSubmit={handleSubmit} className="bg-white rounded-2xl border border-gray-200 p-5 mb-5 shadow-sm">
                <h2 className="text-sm font-semibold text-gray-700 mb-4">
                  {editId ? 'Edit Truck' : 'Register New Truck'}
                </h2>
                {formErr && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2 mb-3">{formErr}</div>}

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">License Plate *</label>
                    <input
                        value={form.licensePlate}
                        onChange={e => set('licensePlate', e.target.value)}
                        placeholder="e.g. KCB 123A"
                        className={inputCls}
                        required
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Model *</label>
                    <input
                        value={form.model}
                        onChange={e => set('model', e.target.value)}
                        placeholder="e.g. Isuzu FRR"
                        className={inputCls}
                        required
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Assign Driver (optional)</label>
                    <select
                        value={form.driverId}
                        onChange={e => set('driverId', e.target.value)}
                        className={inputCls}
                    >
                      <option value="">— No driver assigned —</option>
                      {drivers.map(d => (
                          <option key={d.id} value={d.id}>{d.name}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Class</label>
                    <div className="flex gap-2">
                      <select value={form.vehicleClassId} onChange={e => set('vehicleClassId', e.target.value)} className={inputCls} style={{ height: 42 }}>
                        <option value="">— Select class —</option>
                        {vehicleClasses.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                      </select>
                      <button
                        type="button"
                        onClick={() => { setClassErr(''); setNewClassForm({ name: '', description: '' }); setShowAddClass(true) }}
                        className="px-3 text-sm rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 font-bold whitespace-nowrap"
                        style={{ height: 42 }}
                        title="Add a new vehicle class"
                      >
                        + Add Class
                      </button>
                    </div>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Status</label>
                    <select value={form.status} onChange={e => set('status', Number(e.target.value))} className={inputCls}>
                      {Object.entries(TRUCK_STATUS_LABEL).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Odometer (km)</label>
                    <input type="number" min="0" value={form.odometer} onChange={e => set('odometer', e.target.value)} placeholder="e.g. 84500" className={inputCls} />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Insurance Expiry</label>
                    <input type="date" value={form.insuranceExpiryDate} onChange={e => set('insuranceExpiryDate', e.target.value)} className={inputCls} />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Next Service (by date)</label>
                    <input type="date" value={form.nextServiceDate} onChange={e => set('nextServiceDate', e.target.value)} className={inputCls} />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Last Service Date</label>
                    <input type="date" value={form.lastServiceDate} onChange={e => set('lastServiceDate', e.target.value)} className={inputCls} />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Last Service Odometer (km)</label>
                    <input type="number" min="0" value={form.lastServiceOdometer} onChange={e => set('lastServiceOdometer', e.target.value)} placeholder="e.g. 84000" className={inputCls} />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Service Interval (km)</label>
                    <input type="number" min="0" value={form.serviceIntervalKm} onChange={e => set('serviceIntervalKm', e.target.value)} placeholder="e.g. 500" className={inputCls} />
                    <p className="text-[11px] text-gray-400 mt-1">Next service is due every this many km after the last service odometer reading.</p>
                  </div>
                </div>

                <div className="flex gap-3 mt-4">
                  <button
                      type="submit"
                      disabled={saving}
                      className="px-5 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-colors"
                  >
                    {saving ? 'Saving…' : editId ? 'Update Truck' : 'Register Truck'}
                  </button>
                  <button
                      type="button"
                      onClick={() => { setShowForm(false); setEditId(null) }}
                      className="px-5 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                </div>
              </form>
          )}

          {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">
                {error}
              </div>
          )}

          {loading ? (
              <div className="space-y-3">
                {[1,2,3].map(i => <div key={i} className="h-16 bg-white rounded-xl border animate-pulse" />)}
              </div>
          ) : displayTrucks.length === 0 ? (
              <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
                <div className="flex justify-center mb-3 text-gray-400"><Truck size={40} /></div>
                <h3 className="font-semibold text-gray-700">{statusFilter ? 'No trucks match this filter' : 'No trucks registered'}</h3>
                <p className="text-sm text-gray-400 mt-1">{statusFilter ? 'Try a different KPI card, or clear the filter.' : 'Add your first vehicle to the fleet.'}</p>
              </div>
          ) : (
              <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                  <tr className="bg-gray-50 border-b border-gray-100">
                    <th className="text-left px-5 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider">Reg No</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider">Make</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider hidden xl:table-cell">Class</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider hidden md:table-cell">Driver</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider hidden lg:table-cell">Insurance Expiry</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider hidden lg:table-cell">Next Service</th>
                    <th className="text-right px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider hidden xl:table-cell">Odometer</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider">Status</th>
                    <th className="px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider text-right">Actions</th>
                  </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                  {displayTrucks.map(t => (
                      <tr key={t.id} className="hover:bg-gray-50 transition-colors">
                        <td className="px-5 py-4 font-semibold text-gray-900">{t.licensePlate}</td>
                        <td className="px-4 py-4 text-gray-600">{t.model}</td>
                        <td className="px-4 py-4 text-gray-600 hidden xl:table-cell">{vehicleClasses.find(c => c.id === t.vehicleClassId)?.name ?? '—'}</td>
                        <td className="px-4 py-4 text-gray-600 text-sm hidden md:table-cell">
                          {t.driverId ? (drivers.find(d => d.id === t.driverId)?.name ?? `${t.driverId.slice(0,8)}…`) : '—'}
                        </td>
                        {(() => {
                          const insD = daysUntil(t.insuranceExpiryDate)
                          const svcD = daysUntil(t.nextServiceDate)
                          const mileageTracked = t.nextServiceOdometer != null
                          const mileageDue = t.isServiceDueByMileage === true
                          const warn = d => d === null ? 'text-gray-400' : d < 0 ? 'text-red-600 font-semibold' : d <= 30 ? 'text-amber-600 font-semibold' : 'text-gray-600'
                          // Once a truck has mileage tracking configured, mileage decides due/not-due
                          // outright — the calendar date becomes secondary display info, not a signal.
                          const svcColor = mileageTracked ? (mileageDue ? 'text-red-600 font-semibold' : 'text-gray-600') : warn(svcD)
                          return (
                            <>
                              <td className={`px-4 py-4 hidden lg:table-cell ${warn(insD)}`}>{fmtDate(t.insuranceExpiryDate)}</td>
                              <td className={`px-4 py-4 hidden lg:table-cell ${svcColor}`}>
                                {mileageTracked ? (
                                  <>
                                    {Number(t.nextServiceOdometer).toLocaleString()} km
                                    <div className="text-[11px] text-gray-400">{mileageDue ? 'Due now' : `at ${Number(t.odometer).toLocaleString()} km now`}</div>
                                  </>
                                ) : (
                                  fmtDate(t.nextServiceDate)
                                )}
                              </td>
                            </>
                          )
                        })()}
                        <td className="px-4 py-4 text-right text-gray-600 hidden xl:table-cell">
                          {t.odometer != null ? `${Number(t.odometer).toLocaleString()} km` : '—'}
                        </td>
                        <td className="px-4 py-4">
                          <span className={`inline-flex px-2.5 py-1 rounded-full text-xs font-semibold ${STATUS_STYLE[t.status] ?? 'bg-gray-100 text-gray-600'}`}>
                            {TRUCK_STATUS_LABEL[t.status] ?? t.status ?? 'Active'}
                          </span>
                        </td>
                        <td className="px-4 py-4 text-right">
                          <div className="flex flex-wrap items-center justify-end gap-1.5">
                            {isServiceDue(t) && (
                              <button
                                  onClick={() => openMarkServiced(t)}
                                  className="text-xs px-3 py-1.5 bg-navy hover:bg-navy-dark text-white rounded-lg font-medium"
                              >
                                Mark Serviced
                              </button>
                            )}
                            {t.status !== TRUCK_STATUS.IN_MAINTENANCE && (
                              <button
                                  onClick={() => quickUpdateStatus(t, TRUCK_STATUS.IN_MAINTENANCE)}
                                  disabled={statusWorkingId === t.id}
                                  className="text-xs px-3 py-1.5 border border-amber-200 rounded-lg hover:bg-amber-50 text-amber-700 font-medium disabled:opacity-50"
                              >
                                Maintenance
                              </button>
                            )}
                            {t.status !== TRUCK_STATUS.ACTIVE && (
                              <button
                                  onClick={() => quickUpdateStatus(t, TRUCK_STATUS.ACTIVE)}
                                  disabled={statusWorkingId === t.id}
                                  className="text-xs px-3 py-1.5 border border-green-200 rounded-lg hover:bg-green-50 text-green-700 font-medium disabled:opacity-50"
                              >
                                Activate
                              </button>
                            )}
                            {t.status !== TRUCK_STATUS.DECOMMISSIONED && (
                              <button
                                  onClick={() => quickUpdateStatus(t, TRUCK_STATUS.DECOMMISSIONED)}
                                  disabled={statusWorkingId === t.id}
                                  className="text-xs px-3 py-1.5 border border-gray-300 rounded-lg hover:bg-gray-100 text-gray-600 font-medium disabled:opacity-50"
                              >
                                Decommission
                              </button>
                            )}
                            <button
                                onClick={() => openEdit(t)}
                                className="text-xs px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 font-medium"
                            >
                              Edit
                            </button>
                            <button
                                onClick={() => openDeleteModal(t)}
                                className="text-xs px-3 py-1.5 border border-red-200 rounded-lg hover:bg-red-50 text-red-600 font-medium"
                            >
                              Remove
                            </button>
                          </div>
                        </td>
                      </tr>
                  ))}
                  </tbody>
                </table>
                <Pagination page={page} pageSize={PAGE_SIZE} totalCount={displayTotal} onPageChange={setPage} />
              </div>
          )}
        </main>

        {/* Quick-add Vehicle Class Modal */}
        {showAddClass && (
            <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
              <form onSubmit={handleCreateClass} className="bg-white rounded-2xl shadow-2xl p-6 w-full max-w-md space-y-4">
                <h3 className="font-bold text-gray-900 text-lg">New Vehicle Class</h3>
                {classErr && (
                    <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2">{classErr}</div>
                )}
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Name *</label>
                  <input
                      value={newClassForm.name}
                      onChange={e => setNewClassForm(f => ({ ...f, name: e.target.value }))}
                      placeholder="e.g. Flatbed Truck"
                      className={inputCls}
                      required
                      autoFocus
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Description</label>
                  <textarea
                      value={newClassForm.description}
                      onChange={e => setNewClassForm(f => ({ ...f, description: e.target.value }))}
                      rows={2}
                      placeholder="Optional description…"
                      className={inputCls + ' resize-none'}
                  />
                </div>
                <div className="flex gap-3 pt-2">
                  <button type="submit" disabled={savingClass} className="flex-1 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-colors">
                    {savingClass ? 'Creating…' : 'Create Class'}
                  </button>
                  <button type="button" onClick={() => setShowAddClass(false)} className="px-5 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50">
                    Cancel
                  </button>
                </div>
              </form>
            </div>
        )}

        {/* Delete Confirmation Modal */}
        {showDeleteModal && truckToDelete && (
            <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
              <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-5">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-red-100 flex items-center justify-center flex-shrink-0">
                    <svg className="w-5 h-5 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-gray-900">Remove Truck</h3>
                    <p className="text-xs text-gray-500 mt-0.5">This action cannot be undone.</p>
                  </div>
                </div>

                <p className="text-sm text-gray-600">
                  Are you sure you want to remove the truck{' '}
                  <span className="font-semibold text-gray-900">{truckToDelete.licensePlate}</span>
                  {' '}({truckToDelete.model}) from the fleet?
                </p>

                <div className="flex gap-3 pt-2">
                  <button
                      onClick={() => {
                        setShowDeleteModal(false)
                        setTruckToDelete(null)
                      }}
                      className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                      onClick={confirmDelete}
                      className="flex-1 py-2.5 bg-red-600 hover:bg-red-700 text-white text-sm font-bold rounded-xl transition-colors"
                  >
                    Yes, Remove Truck
                  </button>
                </div>
              </div>
            </div>
        )}

        {/* Mark Serviced Modal */}
        {serviceTarget && (
            <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
              <form onSubmit={confirmMarkServiced} className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-4">
                <div>
                  <h3 className="text-base font-bold text-gray-900">Mark Serviced</h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Records today as the last service date for{' '}
                    <span className="font-semibold text-gray-700">{serviceTarget.licensePlate}</span> and sets it back to Active.
                  </p>
                </div>
                {serviceErr && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2">{serviceErr}</div>}
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Odometer Reading Now (km) *</label>
                  <input
                      type="number" min="0" autoFocus
                      value={serviceOdometer}
                      onChange={e => setServiceOdometer(e.target.value)}
                      className={inputCls}
                      required
                  />
                </div>
                <div className="flex gap-3 pt-2">
                  <button type="button" onClick={() => setServiceTarget(null)}
                      className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors">
                    Cancel
                  </button>
                  <button type="submit" disabled={servicing}
                      className="flex-1 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-colors">
                    {servicing ? 'Saving…' : 'Confirm Serviced'}
                  </button>
                </div>
              </form>
            </div>
        )}
      </>
  )
}