import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import FleetNav from '../fleet/FleetNav.jsx'
import ImagePicker from '../../components/ImagePicker.jsx'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'

const API_URL = import.meta.env.VITE_API_URL || ''

const STATUS_BADGE = {
  Available: 'bg-green-100 text-green-700',
  Dispatched: 'bg-blue-100 text-blue-700',
  UnderMaintenance: 'bg-amber-100 text-amber-700',
  Decommissioned: 'bg-gray-100 text-gray-500',
}

const VEHICLE_TYPES = ['Probox', 'Pickup', 'Saloon', 'SUV', 'Van', 'Other']
const STATUSES = ['Available', 'Dispatched', 'UnderMaintenance', 'Decommissioned']

async function uploadImages(files, vehicleId) {
  for (const file of files) {
    const fd = new FormData()
    fd.append('file', file)
    fd.append('entityType', 'FieldVehicle')
    fd.append('entityId', vehicleId)
    await api.post('/api/v1/attachments', fd).catch(() => {})
  }
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
  lastServiceDate: '', nextServiceDate: '', lastServiceOdometer: '', insuranceExpiry: '', inspectionExpiryDate: '',
}

export default function FieldVehiclesPage() {
  const { hasPermission } = useAuth()
  const navigate = useNavigate()
  const canApprove = hasPermission('operations.approve')

  const [vehicles, setVehicles] = useState([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')

  const [showForm, setShowForm] = useState(false)
  const [editTarget, setEditTarget] = useState(null)
  const [form, setForm] = useState(EMPTY_FORM)
  const [saving, setSaving] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [deleting, setDeleting] = useState(false)
  const [toast, setToast] = useState(null)

  const showToast = useCallback((type, message) => {
    setToast({ type, message })
    setTimeout(() => setToast(null), 4000)
  }, [])

  // Images state
  const [pendingImages, setPendingImages] = useState([])
  const [existingImages, setExistingImages] = useState([])
  const [imagesLoading, setImagesLoading] = useState(false)
  const [deletingImageId, setDeletingImageId] = useState(null)

  const pageSize = 20

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const params = new URLSearchParams({ page, pageSize })
      if (search) params.append('search', search)
      if (statusFilter) params.append('status', statusFilter)
      const res = await api.get(`/api/v1/field-vehicles?${params}`)
      const data = res.data?.data
      setVehicles(data?.items ?? [])
      setTotal(data?.totalCount ?? 0)
    } catch {
      setVehicles([])
    } finally {
      setLoading(false)
    }
  }, [page, search, statusFilter])

  useEffect(() => { load() }, [load])

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
      nextServiceDate: v.nextServiceDate ? v.nextServiceDate.substring(0, 10) : '',
      lastServiceOdometer: v.lastServiceOdometer ?? '',
      insuranceExpiry: v.insuranceExpiry ? v.insuranceExpiry.substring(0, 10) : '',
      inspectionExpiryDate: v.inspectionExpiryDate ? v.inspectionExpiryDate.substring(0, 10) : '',
    })
    setPendingImages([])
    setExistingImages([])
    setShowForm(true)
    // Load existing images
    setImagesLoading(true)
    try {
      const res = await api.get(`/api/v1/attachments?entityType=FieldVehicle&entityId=${v.id}`)
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
      await api.delete(`/api/v1/attachments/${imageId}`)
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
        type: form.type,
        color: form.color || null,
        currentOdometer: Number(form.currentOdometer),
        notes: form.notes || null,
        lastServiceDate: form.lastServiceDate || null,
        nextServiceDate: form.nextServiceDate || null,
        lastServiceOdometer: form.lastServiceOdometer !== '' ? Number(form.lastServiceOdometer) : null,
        insuranceExpiry: form.insuranceExpiry || null,
        inspectionExpiryDate: form.inspectionExpiryDate || null,
      }

      let vehicleId
      if (editTarget) {
        await api.put(`/api/v1/field-vehicles/${editTarget.id}`, body)
        vehicleId = editTarget.id
      } else {
        const res = await api.post('/api/v1/field-vehicles', body)
        vehicleId = res.data?.data?.id
      }

      if (pendingImages.length > 0 && vehicleId) {
        await uploadImages(pendingImages, vehicleId)
      }
      showToast('success', editTarget ? 'Vehicle updated successfully.' : 'Vehicle added successfully.')
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
      await api.delete(`/api/v1/field-vehicles/${deleteTarget.id}`)
      showToast('success', 'Vehicle deleted.')
      setDeleteTarget(null)
      load()
    } catch {
      showToast('error', 'Failed to delete vehicle.')
    } finally {
      setDeleting(false)
    }
  }

  const totalPages = Math.max(1, Math.ceil(total / pageSize))

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <FleetNav />
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Field Vehicles</h1>
            <p className="text-sm text-gray-500 mt-0.5">Registry of field vehicles used by technicians on assignments</p>
          </div>
          {canApprove && (
            <button
              onClick={openCreate}
              className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-amber-400 hover:bg-amber-500 text-black font-semibold text-sm transition-colors"
            >
              + Add Vehicle
            </button>
          )}
        </div>

        {/* Filters */}
        <div className="flex flex-col sm:flex-row gap-3 mb-5">
          <input
            type="text"
            placeholder="Search by plate, make or model..."
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1) }}
            className="flex-1 border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
          />
          <select
            value={statusFilter}
            onChange={e => { setStatusFilter(e.target.value); setPage(1) }}
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
              <div className="text-4xl mb-3">🚗</div>
              <p className="font-medium">No vehicles found</p>
              {canApprove && <p className="text-sm mt-1">Add your first field vehicle to get started.</p>}
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
                  {canApprove && <th className="px-4 py-3" />}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {vehicles.map(v => (
                  <VehicleRow
                    key={v.id}
                    vehicle={v}
                    canApprove={canApprove}
                    onClick={() => navigate(`/modules/operations/field-vehicles/${v.id}`)}
                    onEdit={e => openEdit(v, e)}
                    onDelete={e => { e.stopPropagation(); setDeleteTarget(v) }}
                  />
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between mt-4 text-sm text-gray-500">
            <span>{total} vehicle{total !== 1 ? 's' : ''}</span>
            <div className="flex gap-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
                className="px-3 py-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-40">Prev</button>
              <span className="px-3 py-1.5">{page} / {totalPages}</span>
              <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
                className="px-3 py-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-40">Next</button>
            </div>
          </div>
        )}
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
                  <label className="block text-xs font-semibold text-gray-600 mb-1">Next Service Date</label>
                  <input
                    type="date"
                    value={form.nextServiceDate}
                    onChange={e => setForm(f => ({ ...f, nextServiceDate: e.target.value }))}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                  />
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
                      <a href={`${API_URL}${img.url}`} target="_blank" rel="noopener noreferrer">
                        <img
                          src={`${API_URL}${img.url}`}
                          alt={img.fileName ?? 'Vehicle photo'}
                          className="w-20 h-20 object-cover rounded-xl border border-gray-100 block"
                          onError={e => { e.target.parentElement.parentElement.style.display = 'none' }}
                        />
                      </a>
                      {canApprove && (
                        <button
                          type="button"
                          onClick={() => handleDeleteImage(img.id)}
                          disabled={deletingImageId === img.id}
                          className="absolute -top-1.5 -right-1.5 w-5 h-5 rounded-full bg-red-500 text-white text-xs flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity disabled:opacity-50"
                        >
                          ×
                        </button>
                      )}
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
          toast.type === 'success' ? 'bg-green-500 text-white' : 'bg-red-500 text-white'
        }`}>
          <span>{toast.type === 'success' ? '✓' : '✕'}</span>
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
    </>
  )
}

// ── VehicleRow: lazy-loads first photo thumbnail ───────────────────────────────

function VehicleRow({ vehicle: v, canApprove, onClick, onEdit, onDelete }) {
  return (
    <tr onClick={onClick} className="hover:bg-amber-50/40 transition-colors cursor-pointer">
      <td className="px-4 py-3 w-12">
        <div className="w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center text-gray-400 text-lg">🚗</div>
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
      {canApprove && (
        <td className="px-4 py-3">
          <div className="flex items-center justify-end gap-2">
            <button onClick={onEdit} className="text-xs text-blue-600 hover:text-blue-800 font-medium">Edit</button>
            <button onClick={onDelete} className="text-xs text-red-500 hover:text-red-700 font-medium">Delete</button>
          </div>
        </td>
      )}
    </tr>
  )
}
