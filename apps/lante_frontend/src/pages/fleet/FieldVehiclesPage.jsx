import { useState, useEffect, useCallback } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Download, Car, CheckCircle2, ArrowRightLeft, Wrench, AlertTriangle, X } from 'lucide-react'
import FleetNav from './FleetNav.jsx'
import ImagePicker from '../../components/ImagePicker.jsx'
import api from '../../api/axios.js'
import { exportToPdf, exportToExcel, FIELD_VEHICLE_COLUMNS } from '../../utils/export.js'
import Collapsible from '../../components/Collapsible.jsx'
import Pagination from '../../components/Pagination.jsx'

const PAGE_SIZE = 20
const EXPORT_CAP = 100

const STATUS_BADGE = {
  Available: 'bg-green-100 text-green-700',
  Dispatched: 'bg-blue-100 text-blue-700',
  UnderMaintenance: 'bg-amber-100 text-amber-700',
  Decommissioned: 'bg-gray-100 text-gray-500',
}

const VEHICLE_TYPES = ['Probox', 'Pickup', 'Saloon', 'SUV', 'Van', 'Other']
const STATUSES = ['Available', 'Dispatched', 'UnderMaintenance', 'Decommissioned']

// Mirrors FleetService.Core.Entities.FieldVehicleStatus — numeric values match the C# enum
// declaration order, since UpdateFieldVehicleDto binds the raw numeric value (no JsonStringEnumConverter).
const FIELD_VEHICLE_STATUS = { AVAILABLE: 0, DISPATCHED: 1, UNDER_MAINTENANCE: 2, DECOMMISSIONED: 3 }

// Same due/not-due rule the Trucks page uses — mileage wins outright once configured,
// date is only a fallback.
function isServiceDue(v) {
  if (v.nextServiceOdometer != null) return v.isServiceDueByMileage === true
  if (!v.nextServiceDate) return false
  const d = new Date(v.nextServiceDate)
  if (isNaN(d)) return false
  return Math.ceil((d.getTime() - Date.now()) / 86400000) <= 30
}

async function uploadImages(files, vehicleId) {
  let failures = 0
  for (const file of files) {
    const fd = new FormData()
    fd.append('image', file)
    await api.post(`/api/v1/FieldVehiclePhotos/field-vehicle/${vehicleId}`, fd).catch(() => { failures++ })
  }
  return failures
}

function KpiCard({ label, value, icon, tone = 'navy', onClick, active }) {
  const toneCls = {
    navy:  'bg-navy/10 text-navy',
    green: 'bg-green-100 text-green-700',
    blue:  'bg-blue-100 text-blue-700',
    amber: 'bg-amber-100 text-amber-700',
  }[tone]
  const Tag = onClick ? 'button' : 'div'
  return (
    <Tag
      onClick={onClick}
      className={`bg-white rounded-2xl border shadow-sm p-4 text-left w-full transition-all ${active ? 'border-amber-400 ring-1 ring-amber-400' : 'border-gray-100'} ${onClick ? 'hover:shadow-md hover:border-gray-200 hover:-translate-y-0.5 cursor-pointer' : ''}`}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">{label}</p>
          <p className="text-2xl font-extrabold text-gray-900 mt-1 break-words">{value}</p>
        </div>
        <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-lg flex-shrink-0 ${toneCls}`}>{icon}</div>
      </div>
    </Tag>
  )
}

function Modal({ title, onClose, children, wide }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
      <div className={`bg-white rounded-2xl shadow-2xl w-full ${wide ? 'max-w-2xl' : 'max-w-lg'} max-h-[92vh] overflow-y-auto`}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 sticky top-0 bg-white z-10">
          <h2 className="text-base font-bold text-gray-800">{title}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
        </div>
        <div className="px-6 py-5">{children}</div>
      </div>
    </div>
  )
}

const EMPTY_FORM = {
  registrationNumber: '', make: '', model: '',
  year: new Date().getFullYear(), type: 'Probox',
  color: '', currentOdometer: 0, notes: '',
  lastServiceDate: '', lastServiceOdometer: '', serviceIntervalKm: '', insuranceExpiry: '', inspectionExpiryDate: '',
}

export default function FieldVehiclesPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()

  const [vehicles, setVehicles] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [allVehicles, setAllVehicles] = useState([]) // unpaged — KPIs must reflect the whole fleet, not just this page
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  // Seeded from ?status= so a dashboard KPI card can deep-link straight into a filtered view.
  const [statusFilter, setStatusFilter] = useState(() => searchParams.get('status') ?? '')
  const [page, setPage] = useState(1)
  const [exporting, setExporting] = useState(false)

  const [showForm, setShowForm] = useState(false)
  const [editTarget, setEditTarget] = useState(null)
  const [form, setForm] = useState(EMPTY_FORM)
  const [saving, setSaving] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [deleting, setDeleting] = useState(false)
  const [toast, setToast] = useState(null)

  // Quick status-change state
  const [statusWorkingId, setStatusWorkingId] = useState(null)
  const [serviceTarget, setServiceTarget] = useState(null) // vehicle being marked serviced
  const [serviceOdometer, setServiceOdometer] = useState('')
  const [servicing, setServicing] = useState(false)
  const [serviceErr, setServiceErr] = useState('')

  const showToast = useCallback((type, message) => {
    setToast({ type, message })
    setTimeout(() => setToast(null), type === 'warning' ? 7000 : 4000)
  }, [])

  // Images state
  const [pendingImages, setPendingImages] = useState([])
  const [existingImages, setExistingImages] = useState([])
  const [imagesLoading, setImagesLoading] = useState(false)
  const [deletingImageId, setDeletingImageId] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const params = new URLSearchParams({ page, pageSize: PAGE_SIZE })
      if (search) params.append('search', search)
      if (statusFilter) params.append('status', statusFilter)
      const res = await api.get(`/api/v1/fleet/field-vehicles?${params}`)
      setVehicles(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setVehicles([])
      setTotalCount(0)
    } finally {
      setLoading(false)
    }
  }, [page, search, statusFilter])

  useEffect(() => { load() }, [load])

  const loadAllForKpis = useCallback(() => {
    api.get('/api/v1/fleet/field-vehicles', { params: { page: 1, pageSize: 1000 } })
      .then(res => setAllVehicles(res.data?.data?.items ?? []))
      .catch(() => {}) // keep last known counts on a transient failure rather than zeroing them
  }, [])

  // Poll the unpaged fleet so the KPI cards (Available/Borrowed/Under Maintenance) reflect an
  // approval/rejection/return that happened elsewhere without needing a manual refresh. Only the
  // KPI fetch polls, not the paged/filtered table below — that one's driven by the user's own
  // search/filter/page state and shouldn't jump around under them every 30s.
  useEffect(() => {
    loadAllForKpis()
    const id = setInterval(loadAllForKpis, 30_000)
    return () => clearInterval(id)
  }, [loadAllForKpis])

  function updateSearch(value) {
    setSearch(value)
    setPage(1)
  }

  function updateStatusFilter(value) {
    setStatusFilter(value)
    setPage(1)
  }

  async function fetchExportRows() {
    const params = new URLSearchParams({ page: 1, pageSize: EXPORT_CAP })
    if (search) params.append('search', search)
    if (statusFilter) params.append('status', statusFilter)
    const res = await api.get(`/api/v1/fleet/field-vehicles?${params}`)
    const rows = res.data?.data?.items ?? []
    const total = res.data?.data?.totalCount ?? rows.length
    return { rows, capped: total > rows.length, total }
  }

  const handleExportPdf = async () => {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: "Field Vehicles",
        subtitle: capped ? `Showing first ${rows.length} of ${total} matching vehicles` : `${total} matching vehicle${total !== 1 ? 's' : ''}`,
        columns: FIELD_VEHICLE_COLUMNS,
        rows,
        filename: `Field-Vehicles-${new Date().toISOString().slice(0,10)}`,
        theme: 'navy',
        docModule: 'FLEET',
      })
    } catch {
      // no dedicated error state on this page — a failed export silently no-ops
    } finally {
      setExporting(false)
    }
  }

  const handleExportExcel = async () => {
    setExporting(true)
    try {
      const { rows } = await fetchExportRows()
      exportToExcel({
        title: "Field Vehicles",
        columns: FIELD_VEHICLE_COLUMNS,
        rows,
        filename: `Field-Vehicles-${new Date().toISOString().slice(0,10)}`,
        sheetName: "Field Vehicles"
      })
    } catch {
      // no dedicated error state on this page — a failed export silently no-ops
    } finally {
      setExporting(false)
    }
  }

  function openCreate() {
    setEditTarget(null)
    setForm(EMPTY_FORM)
    setPendingImages([])
    setExistingImages([])
    setShowForm(true)
  }

  async function openEdit(v, e) {
    e?.stopPropagation()
    setEditTarget(v)
    setForm({
      registrationNumber: v.registrationNumber,
      make: v.make,
      model: v.model,
      year: v.year,
      type: v.type,
      color: v.color ?? '',
      currentOdometer: v.currentOdometer,
      notes: v.notes ?? '',
      lastServiceDate: v.lastServiceDate ? v.lastServiceDate.substring(0, 10) : '',
      lastServiceOdometer: v.lastServiceOdometer ?? '',
      serviceIntervalKm: v.serviceIntervalKm ?? '',
      insuranceExpiry: v.insuranceExpiry ? v.insuranceExpiry.substring(0, 10) : '',
      inspectionExpiryDate: v.inspectionExpiryDate ? v.inspectionExpiryDate.substring(0, 10) : '',
    })
    setPendingImages([])
    setExistingImages([])
    setShowForm(true)
    // Load existing images
    setImagesLoading(true)
    try {
      const res = await api.get(`/api/v1/FieldVehiclePhotos/field-vehicle/${v.id}`)
      setExistingImages(res.data?.data ?? [])
    } catch {
      setExistingImages([])
    } finally {
      setImagesLoading(false)
    }
  }

  async function handleDeleteImage(imageId) {
    setDeletingImageId(imageId)
    try {
      await api.delete(`/api/v1/FieldVehiclePhotos/${imageId}`)
      setExistingImages(imgs => imgs.filter(i => i.id !== imageId))
    } catch {
      showToast('error', 'Failed to delete image.')
    } finally {
      setDeletingImageId(null)
    }
  }

  async function handleSave(e) {
    e.preventDefault()
    setSaving(true)
    try {
      const body = {
        registrationNumber: form.registrationNumber,
        make: form.make,
        model: form.model,
        year: Number(form.year),
        // FleetService doesn't register a JsonStringEnumConverter (unlike operations-service,
        // which this page was ported from) — its enums bind from the raw numeric value.
        type: VEHICLE_TYPES.indexOf(form.type),
        color: form.color || null,
        currentOdometer: Number(form.currentOdometer),
        notes: form.notes || null,
        lastServiceDate: form.lastServiceDate || null,
        lastServiceOdometer: form.lastServiceOdometer !== '' ? Number(form.lastServiceOdometer) : null,
        serviceIntervalKm: form.serviceIntervalKm !== '' ? Number(form.serviceIntervalKm) : null,
        insuranceExpiry: form.insuranceExpiry || null,
        inspectionExpiryDate: form.inspectionExpiryDate || null,
      }

      let vehicleId
      if (editTarget) {
        await api.put(`/api/v1/fleet/field-vehicles/${editTarget.id}`, body)
        vehicleId = editTarget.id
      } else {
        const res = await api.post('/api/v1/fleet/field-vehicles', body)
        vehicleId = res.data?.data?.id
      }

      let imageFailures = 0
      if (pendingImages.length > 0 && vehicleId) {
        imageFailures = await uploadImages(pendingImages, vehicleId)
      }
      if (imageFailures > 0) {
        showToast('warning', `Vehicle ${editTarget ? 'updated' : 'added'}, but ${imageFailures} photo${imageFailures !== 1 ? 's' : ''} failed to upload — try again from Edit.`)
      } else {
        showToast('success', editTarget ? 'Vehicle updated successfully.' : 'Vehicle added successfully.')
      }
      setShowForm(false)
      load()
    } catch (err) {
      showToast('error', err?.response?.data?.message ?? 'Failed to save vehicle. Please try again.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!deleteTarget) return
    setDeleting(true)
    try {
      await api.delete(`/api/v1/fleet/field-vehicles/${deleteTarget.id}`)
      showToast('success', 'Vehicle deleted.')
      setDeleteTarget(null)
      load()
    } catch {
      showToast('error', 'Failed to delete vehicle.')
    } finally {
      setDeleting(false)
    }
  }

  // Quick status-change handlers — UpdateFieldVehicleDto fields are all optional, and the
  // service only overwrites fields that are present, so a partial payload is safe here (unlike
  // the Trucks page, whose update endpoint requires the full payload).
  async function quickUpdateStatus(vehicle, newStatus) {
    setStatusWorkingId(vehicle.id)
    try {
      await api.put(`/api/v1/fleet/field-vehicles/${vehicle.id}`, { status: newStatus })
      load()
      loadAllForKpis()
    } catch (err) {
      showToast('error', err.response?.data?.message ?? 'Failed to update vehicle status.')
    } finally {
      setStatusWorkingId(null)
    }
  }

  function openMarkServiced(vehicle) {
    setServiceTarget(vehicle)
    setServiceOdometer(vehicle.currentOdometer != null ? String(vehicle.currentOdometer) : '')
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
      await api.put(`/api/v1/fleet/field-vehicles/${serviceTarget.id}`, {
        status: FIELD_VEHICLE_STATUS.AVAILABLE,
        currentOdometer: Number(serviceOdometer),
        lastServiceDate: new Date().toISOString(),
        lastServiceOdometer: Number(serviceOdometer),
      })
      setServiceTarget(null)
      load()
      loadAllForKpis()
    } catch (err) {
      setServiceErr(err.response?.data?.message ?? 'Failed to record service.')
    } finally {
      setServicing(false)
    }
  }

  // KPIs reflect the whole fleet (allVehicles), not just the current page.
  const fleetSize = allVehicles.length
  const availableCount = allVehicles.filter(v => v.status === 'Available').length
  const borrowedCount = allVehicles.filter(v => v.status === 'Dispatched').length
  const maintenanceCount = allVehicles.filter(v => v.status === 'UnderMaintenance').length

  return (
    <>
      <main className="w-full px-4 sm:px-6 py-8">
        <FleetNav />

        <Collapsible title="About Field Vehicles" dismissKey="fleet.pageInfo.fieldVehicles.dismissed">
          <p className="text-sm text-gray-700">
            The registry of light vehicles dispatched to technicians for field assignments — separate from the truck fleet. Service and insurance dates here drive the detail page's expiry alerts.
          </p>
        </Collapsible>

        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Field Vehicles</h1>
            <p className="text-sm text-gray-500 mt-0.5">Registry of field vehicles used by technicians on assignments</p>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={handleExportExcel}
              disabled={exporting || totalCount === 0}
              className="inline-flex items-center gap-2 px-4 py-2 rounded-xl border border-gray-300 bg-white hover:bg-gray-50 text-gray-700 text-sm font-semibold transition-colors disabled:opacity-50"
            >
              <Download size={14} /> Excel
            </button>
            <button
              onClick={handleExportPdf}
              disabled={exporting || totalCount === 0}
              className="inline-flex items-center gap-2 px-4 py-2 rounded-xl border border-gray-300 bg-white hover:bg-gray-50 text-gray-700 text-sm font-semibold transition-colors disabled:opacity-50"
            >
              <Download size={14} /> PDF
            </button>
            <button
              onClick={openCreate}
              className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-amber-400 hover:bg-amber-500 text-black font-semibold text-sm transition-colors"
            >
              + Add Vehicle
            </button>
          </div>
        </div>

        {/* KPIs — Field Vehicle Registry. Clicking a card filters the table below to match. */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
          <KpiCard label="Fleet Size" value={fleetSize} icon={<Car size={20} />} tone="navy"
            active={statusFilter === ''} onClick={() => updateStatusFilter('')} />
          <KpiCard label="Available" value={availableCount} icon={<CheckCircle2 size={20} />} tone="green"
            active={statusFilter === 'Available'} onClick={() => updateStatusFilter('Available')} />
          <KpiCard label="Borrowed" value={borrowedCount} icon={<ArrowRightLeft size={20} />} tone="blue"
            active={statusFilter === 'Dispatched'} onClick={() => updateStatusFilter('Dispatched')} />
          <KpiCard label="Under Maintenance" value={maintenanceCount} icon={<Wrench size={20} />} tone="amber"
            active={statusFilter === 'UnderMaintenance'} onClick={() => updateStatusFilter('UnderMaintenance')} />
        </div>

        {/* Filters */}
        <div className="flex flex-col sm:flex-row gap-3 mb-5">
          <input
            type="text"
            placeholder="Search by plate, make or model..."
            value={search}
            onChange={e => updateSearch(e.target.value)}
            className="flex-1 border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
          />
          <select
            value={statusFilter}
            onChange={e => updateStatusFilter(e.target.value)}
            className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
          >
            <option value="">All Statuses</option>
            {STATUSES.map(s => <option key={s} value={s}>{s === 'UnderMaintenance' ? 'Under Maintenance' : s}</option>)}
          </select>
        </div>

        {/* Table */}
        <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden">
          {loading ? (
            <div className="flex items-center justify-center py-20 text-gray-400 text-sm">Loading…</div>
          ) : vehicles.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-20 text-gray-400">
              <div className="flex justify-center mb-3 text-gray-400"><Car size={40} /></div>
              <p className="font-medium">No vehicles found</p>
              <p className="text-sm mt-1">Add your first field vehicle to get started.</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 uppercase tracking-wider text-xs w-12" />
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 uppercase tracking-wider text-xs">Registration</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 uppercase tracking-wider text-xs">Vehicle</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 uppercase tracking-wider text-xs">Type</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 uppercase tracking-wider text-xs">Odometer (km)</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-600 uppercase tracking-wider text-xs">Status</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {vehicles.map(v => (
                  <VehicleRow
                    key={v.id}
                    vehicle={v}
                    onClick={() => navigate(`/modules/fleet/field-vehicles/${v.id}`)}
                    onEdit={e => openEdit(v, e)}
                    onDelete={e => { e.stopPropagation(); setDeleteTarget(v) }}
                    onMarkServiced={e => { e.stopPropagation(); openMarkServiced(v) }}
                    onQuickStatus={(e, status) => { e.stopPropagation(); quickUpdateStatus(v, status) }}
                    statusWorking={statusWorkingId === v.id}
                    serviceDue={isServiceDue(v)}
                  />
                ))}
              </tbody>
            </table>
          )}
          {!loading && vehicles.length > 0 && (
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          )}
        </div>
      </main>

      {/* Create / Edit Modal */}
      {showForm && (
        <Modal
          title={editTarget ? `Edit — ${editTarget.registrationNumber}` : 'Add Field Vehicle'}
          onClose={() => setShowForm(false)}
          wide
        >
          <form onSubmit={handleSave} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="col-span-2">
                <label className="block text-xs font-semibold text-gray-600 mb-1">Registration Number *</label>
                <input
                  required
                  value={form.registrationNumber}
                  onChange={e => setForm(f => ({ ...f, registrationNumber: e.target.value }))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 uppercase"
                  placeholder="e.g. KCA 123A"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Make *</label>
                <input
                  required value={form.make}
                  onChange={e => setForm(f => ({ ...f, make: e.target.value }))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  placeholder="e.g. Toyota"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Model *</label>
                <input
                  required value={form.model}
                  onChange={e => setForm(f => ({ ...f, model: e.target.value }))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  placeholder="e.g. Probox"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Year *</label>
                <input
                  required type="number" min="1990" max="2099"
                  value={form.year}
                  onChange={e => setForm(f => ({ ...f, year: e.target.value }))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Type *</label>
                <select
                  required value={form.type}
                  onChange={e => setForm(f => ({ ...f, type: e.target.value }))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                >
                  {VEHICLE_TYPES.map(t => <option key={t}>{t}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Color</label>
                <input
                  value={form.color}
                  onChange={e => setForm(f => ({ ...f, color: e.target.value }))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  placeholder="e.g. White"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Current Odometer (km)</label>
                <input
                  type="number" min="0"
                  value={form.currentOdometer}
                  onChange={e => setForm(f => ({ ...f, currentOdometer: e.target.value }))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                />
              </div>
            </div>

            {/* ── Service & Insurance ── */}
            <div className="border-t border-gray-100 pt-4">
              <p className="text-xs font-semibold text-gray-600 mb-3 uppercase tracking-wider">Service &amp; Insurance</p>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-gray-600 mb-1">Last Service Date</label>
                  <input
                    type="date"
                    value={form.lastServiceDate}
                    onChange={e => setForm(f => ({ ...f, lastServiceDate: e.target.value }))}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-600 mb-1">Last Service Odometer (km)</label>
                  <input
                    type="number" min="0"
                    value={form.lastServiceOdometer}
                    onChange={e => setForm(f => ({ ...f, lastServiceOdometer: e.target.value }))}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-600 mb-1">Service Interval (km)</label>
                  <input
                    type="number" min="0"
                    value={form.serviceIntervalKm}
                    onChange={e => setForm(f => ({ ...f, serviceIntervalKm: e.target.value }))}
                    placeholder="e.g. 500"
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  />
                  <p className="text-[11px] text-gray-400 mt-1">Due every this many km after the last service odometer reading.</p>
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-600 mb-1">Insurance Expiry</label>
                  <input
                    type="date"
                    value={form.insuranceExpiry}
                    onChange={e => setForm(f => ({ ...f, insuranceExpiry: e.target.value }))}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-600 mb-1">Inspection Expiry</label>
                  <input
                    type="date"
                    value={form.inspectionExpiryDate}
                    onChange={e => setForm(f => ({ ...f, inspectionExpiryDate: e.target.value }))}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  />
                </div>
                <div className="col-span-2">
                  <label className="block text-xs font-semibold text-gray-600 mb-1">Notes</label>
                  <textarea
                    value={form.notes}
                    onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
                    rows={2}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 resize-none"
                  />
                </div>
              </div>
            </div>
            <div />

            {/* ── Photos section ── */}
            <div className="border-t border-gray-100 pt-4">
              <p className="text-xs font-semibold text-gray-600 mb-3 uppercase tracking-wider">Vehicle Photos</p>

              {/* Existing images */}
              {imagesLoading ? (
                <p className="text-xs text-gray-400 mb-3">Loading photos…</p>
              ) : existingImages.length > 0 ? (
                <div className="flex flex-wrap gap-2 mb-3">
                  {existingImages.map(img => (
                    <div key={img.id} className="relative group">
                      <a href={img.photoUrl} target="_blank" rel="noopener noreferrer">
                        <img
                          src={img.photoUrl}
                          alt={img.caption ?? 'Vehicle photo'}
                          className="w-20 h-20 object-cover rounded-xl border border-gray-100 block"
                          onError={e => { e.target.parentElement.parentElement.style.display = 'none' }}
                        />
                      </a>
                      <button
                        type="button"
                        onClick={() => handleDeleteImage(img.id)}
                        disabled={deletingImageId === img.id}
                        className="absolute -top-1.5 -right-1.5 w-5 h-5 rounded-full bg-red-500 text-white text-xs flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity disabled:opacity-50"
                      >
                        ×
                      </button>
                    </div>
                  ))}
                </div>
              ) : editTarget ? (
                <p className="text-xs text-gray-400 mb-3">No photos uploaded yet.</p>
              ) : null}

              {/* New image picker */}
              <ImagePicker files={pendingImages} onChange={setPendingImages} />
              {pendingImages.length > 0 && (
                <p className="text-xs text-gray-400 mt-1">
                  {pendingImages.length} new photo{pendingImages.length !== 1 ? 's' : ''} will be uploaded on save.
                </p>
              )}
            </div>

            <div className="flex justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={() => setShowForm(false)}
                className="px-4 py-2 rounded-xl border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={saving}
                className="px-5 py-2 rounded-xl bg-amber-400 hover:bg-amber-500 text-black font-semibold text-sm disabled:opacity-60"
              >
                {saving ? 'Saving…' : editTarget ? 'Save Changes' : 'Add Vehicle'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {/* Toast */}
      {toast && (
        <div className={`fixed bottom-6 right-6 z-[500] flex items-center gap-3 px-5 py-3 rounded-xl shadow-lg text-sm font-medium transition-all ${
          toast.type === 'success' ? 'bg-green-500 text-white' : toast.type === 'warning' ? 'bg-amber-500 text-white' : 'bg-red-500 text-white'
        }`}>
          {toast.type === 'success' ? <CheckCircle2 size={18} /> : toast.type === 'warning' ? <AlertTriangle size={18} /> : <X size={18} />}
          {toast.message}
        </div>
      )}

      {/* Delete Confirm */}
      {deleteTarget && (
        <Modal title="Delete Vehicle" onClose={() => setDeleteTarget(null)}>
          <p className="text-sm text-gray-600 mb-5">
            Are you sure you want to delete <strong>{deleteTarget.registrationNumber}</strong>?
            This action cannot be undone.
          </p>
          <div className="flex justify-end gap-3">
            <button
              onClick={() => setDeleteTarget(null)}
              className="px-4 py-2 rounded-xl border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-50"
            >
              Cancel
            </button>
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="px-5 py-2 rounded-xl bg-red-500 hover:bg-red-600 text-white font-semibold text-sm disabled:opacity-60"
            >
              {deleting ? 'Deleting…' : 'Delete'}
            </button>
          </div>
        </Modal>
      )}

      {/* Mark Serviced Modal */}
      {serviceTarget && (
        <Modal title="Mark Serviced" onClose={() => setServiceTarget(null)}>
          <form onSubmit={confirmMarkServiced} className="space-y-4">
            <p className="text-sm text-gray-600">
              Records today as the last service date for{' '}
              <span className="font-semibold text-gray-800">{serviceTarget.registrationNumber}</span> and sets it back to Available.
            </p>
            {serviceErr && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2">{serviceErr}</div>}
            <div>
              <label className="block text-xs font-semibold text-gray-600 mb-1">Odometer Reading Now (km) *</label>
              <input
                  type="number" min="0" autoFocus
                  value={serviceOdometer}
                  onChange={e => setServiceOdometer(e.target.value)}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  required
              />
            </div>
            <div className="flex justify-end gap-3 pt-2">
              <button type="button" onClick={() => setServiceTarget(null)}
                  className="px-4 py-2 rounded-xl border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-50">
                Cancel
              </button>
              <button type="submit" disabled={servicing}
                  className="px-5 py-2 rounded-xl bg-amber-400 hover:bg-amber-500 text-black font-semibold text-sm disabled:opacity-60">
                {servicing ? 'Saving…' : 'Confirm Serviced'}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </>
  )
}

// ── VehicleRow: lazy-loads first photo thumbnail ───────────────────────────────

function VehicleRow({ vehicle: v, onClick, onEdit, onDelete, onMarkServiced, onQuickStatus, statusWorking, serviceDue }) {
  return (
    <tr onClick={onClick} className="hover:bg-amber-50/40 transition-colors cursor-pointer">
      <td className="px-4 py-3 w-12">
        <div className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center text-gray-400"><Car size={20} /></div>
      </td>
      <td className="px-4 py-3 font-mono font-semibold text-gray-800">{v.registrationNumber}</td>
      <td className="px-4 py-3">
        <div className="font-medium text-gray-800">{v.make} {v.model}</div>
        <div className="text-gray-400 text-xs">{v.year}{v.color ? ` · ${v.color}` : ''}</div>
      </td>
      <td className="px-4 py-3 text-gray-600">{v.type}</td>
      <td className="px-4 py-3 text-gray-600">{Number(v.currentOdometer).toLocaleString()}</td>
      <td className="px-4 py-3">
        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${STATUS_BADGE[v.status] ?? 'bg-gray-100 text-gray-500'}`}>
          {v.status === 'UnderMaintenance' ? 'Maintenance' : v.status}
        </span>
      </td>
      <td className="px-4 py-3">
        <div className="flex flex-wrap items-center justify-end gap-1.5">
          {serviceDue && (
            <button
                onClick={onMarkServiced}
                className="text-xs px-3 py-1.5 bg-amber-400 hover:bg-amber-500 text-black rounded-lg font-medium"
            >
              Mark Serviced
            </button>
          )}
          {v.status !== 'UnderMaintenance' && (
            <button
                onClick={e => onQuickStatus(e, FIELD_VEHICLE_STATUS.UNDER_MAINTENANCE)}
                disabled={statusWorking}
                className="text-xs px-3 py-1.5 border border-amber-200 rounded-lg hover:bg-amber-50 text-amber-700 font-medium disabled:opacity-50"
            >
              Maintenance
            </button>
          )}
          {v.status !== 'Available' && (
            <button
                onClick={e => onQuickStatus(e, FIELD_VEHICLE_STATUS.AVAILABLE)}
                disabled={statusWorking}
                className="text-xs px-3 py-1.5 border border-green-200 rounded-lg hover:bg-green-50 text-green-700 font-medium disabled:opacity-50"
            >
              Available
            </button>
          )}
          {v.status !== 'Decommissioned' && (
            <button
                onClick={e => onQuickStatus(e, FIELD_VEHICLE_STATUS.DECOMMISSIONED)}
                disabled={statusWorking}
                className="text-xs px-3 py-1.5 border border-gray-300 rounded-lg hover:bg-gray-100 text-gray-600 font-medium disabled:opacity-50"
            >
              Decommission
            </button>
          )}
          <button onClick={onEdit} className="text-xs text-blue-600 hover:text-blue-800 font-medium">Edit</button>
          <button onClick={onDelete} className="text-xs text-red-500 hover:text-red-700 font-medium">Delete</button>
        </div>
      </td>
    </tr>
  )
}
