import { useState, useEffect, useCallback } from 'react'
import { Download, User } from 'lucide-react'
import FleetNav from './FleetNav.jsx'
import SinglePhotoPicker from '../../components/SinglePhotoPicker.jsx'
import EditPhotoSlot from '../../components/EditPhotoSlot.jsx'
import api from '../../api/axios.js'
import { exportToPdf, exportToExcel, DRIVER_COLUMNS } from '../../utils/export.js'
import Collapsible from '../../components/Collapsible.jsx'
import Pagination from '../../components/Pagination.jsx'

const PAGE_SIZE = 20
const EMPTY_PHOTOS = { profilePhoto: null, licenceFront: null, licenceBack: null, idFront: null, idBack: null }
// Maps each photo's state key to its upload endpoint segment (note: "licence", matching the API's spelling).
const PHOTO_ENDPOINTS = {
  profilePhoto: 'profile-photo',
  licenceFront: 'licence-front',
  licenceBack: 'licence-back',
  idFront: 'id-front',
  idBack: 'id-back',
}

// status is a number from the API: 0=Draft, 1=Pending, 2=Approved, 3=Rejected
const STATUS = {
  0: { label: 'Draft',    cls: 'bg-gray-50 text-gray-600 border-gray-200' },
  1: { label: 'Pending',  cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  2: { label: 'Approved', cls: 'bg-green-50 text-green-700 border-green-200' },
  3: { label: 'Rejected', cls: 'bg-red-50 text-red-700 border-red-200' },
}

function licenseExpiryBadge(days) {
  if (days == null) return null
  if (days <= 0)  return { label: 'Expired',         cls: 'bg-red-50 text-red-700 border-red-200' }
  if (days <= 30) return { label: `${days}d left`,   cls: 'bg-amber-50 text-amber-700 border-amber-200' }
  return                 { label: 'Valid',            cls: 'bg-green-50 text-green-700 border-green-200' }
}

const PHOTO_URL_KEYS = {
  profilePhoto: 'profilePhotoUrl',
  licenceFront: 'licenseFrontImageUrl',
  licenceBack: 'licenseBackImageUrl',
  idFront: 'idFrontImageUrl',
  idBack: 'idBackImageUrl',
}

export default function DriverProfilesPage() {
  const [drivers, setDrivers]     = useState([])
  const [loading, setLoading]     = useState(true)
  const [error, setError]         = useState('')
  const [page, setPage]           = useState(1)
  const [exporting, setExporting] = useState(false)
  const [workingId, setWorkingId] = useState(null)
  const [reviewInput, setReviewInput] = useState({ id: null, action: null, notes: '' })
  const [createTarget, setCreateTarget] = useState(null) // the user being onboarded
  const [createForm, setCreateForm] = useState({ fullName: '', phoneNumber: '', idNumber: '', licenseNumber: '', licenseExpiryDate: '' })
  const [createPhotos, setCreatePhotos] = useState(EMPTY_PHOTOS)
  const [creating, setCreating]   = useState(false)
  const [createError, setCreateError] = useState('')
  const [editTarget, setEditTarget] = useState(null) // the profile being viewed/edited
  const [editForm, setEditForm] = useState({ fullName: '', phoneNumber: '', licenseNumber: '', licenseExpiryDate: '' })
  const [editPhotos, setEditPhotos] = useState(EMPTY_PHOTOS)
  const [saving, setSaving] = useState(false)
  const [editError, setEditError] = useState('')
  const [deleteTarget, setDeleteTarget] = useState(null) // profile being deleted

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const usersRes  = await api.get('/api/v1/users', { params: { pageSize: 200 } })
      const allUsers  = usersRes.data?.data?.items ?? []
      const fleetStaff = allUsers.filter(u =>
        Array.isArray(u.roles) && u.roles.includes('Fleet Staff')
      )

      const profiles = await Promise.all(
        fleetStaff.map(u =>
          api.get(`/api/v1/DriverProfiles/driver/${u.id}/current`)
            .then(res => res.data?.data ?? null)
            .catch(() => null)
        )
      )

      setDrivers(fleetStaff.map((u, i) => ({ user: u, profile: profiles[i] })))
    } catch {
      setError('Failed to load driver profiles.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  const STATUS_LABEL = { 0: 'Draft', 1: 'Pending', 2: 'Approved', 3: 'Rejected' }

  // Already fully in memory (no server pagination for this cross-service,
  // role-filtered list — see plan) — paginate the array client-side.
  const totalCount = drivers.length
  const pagedDrivers = drivers.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  function toExportRow({ user, profile }) {
    return {
      fullName: profile?.fullName || `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim(),
      email: user.email,
      phoneNumber: profile?.phoneNumber,
      idNumber: profile?.idNumber,
      licenseNumber: profile?.licenseNumber,
      licenseExpiryDate: profile?.licenseExpiryDate,
      statusLabel: profile ? (STATUS_LABEL[profile.status] ?? '—') : 'No profile',
    }
  }

  const handleExportPdf = async () => {
    setExporting(true)
    try {
      await exportToPdf({
        title: "Driver Profiles",
        subtitle: `${drivers.length} driver${drivers.length !== 1 ? 's' : ''}`,
        columns: DRIVER_COLUMNS,
        rows: drivers.map(toExportRow),
        filename: `Driver-Profiles-${new Date().toISOString().slice(0,10)}`,
        theme: 'navy',
        docModule: 'FLEET',
      })
    } catch {
      setError('Failed to export PDF.')
    } finally {
      setExporting(false)
    }
  }

  const handleExportExcel = () => {
    setExporting(true)
    try {
      exportToExcel({
        title: "Driver Profiles",
        columns: DRIVER_COLUMNS,
        rows: drivers.map(toExportRow),
        filename: `Driver-Profiles-${new Date().toISOString().slice(0,10)}`,
        sheetName: "Drivers"
      })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  async function handleReview(e) {
    e.preventDefault()
    const { id, action, notes } = reviewInput
    if (!id || !action) return
    setWorkingId(id)
    try {
      await api.post(`/api/v1/DriverProfiles/${id}/${action}`, { reviewedBy: 'Admin', notes })
      setReviewInput({ id: null, action: null, notes: '' })
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Action failed.')
    } finally {
      setWorkingId(null)
    }
  }

  async function handleSubmit(id) {
    setWorkingId(id)
    try {
      await api.post(`/api/v1/DriverProfiles/${id}/submit`)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to submit profile for review.')
    } finally {
      setWorkingId(null)
    }
  }

  function openCreate(user) {
    setCreateTarget(user)
    setCreateForm({ fullName: `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim(), phoneNumber: '', idNumber: '', licenseNumber: '', licenseExpiryDate: '' })
    setCreatePhotos(EMPTY_PHOTOS)
    setCreateError('')
  }

  async function handleCreate(e) {
    e.preventDefault()
    setCreateError('')
    if (!createForm.fullName || !createForm.idNumber || !createForm.licenseNumber || !createForm.licenseExpiryDate) {
      setCreateError('Full name, ID number, license number and license expiry are required.')
      return
    }
    setCreating(true)
    try {
      const res = await api.post('/api/v1/DriverProfiles', {
        driverId: createTarget.id,
        fullName: createForm.fullName,
        phoneNumber: createForm.phoneNumber,
        idNumber: createForm.idNumber,
        licenseNumber: createForm.licenseNumber,
        licenseExpiryDate: new Date(createForm.licenseExpiryDate).toISOString(),
      })
      const profileId = res.data?.data?.id
      if (profileId) {
        await Promise.all(
          Object.entries(createPhotos)
            .filter(([, file]) => file)
            .map(([key, file]) => {
              const fd = new FormData()
              fd.append('image', file)
              return api.post(`/api/v1/DriverProfiles/${profileId}/${PHOTO_ENDPOINTS[key]}`, fd).catch(() => {})
            })
        )
      }
      setCreateTarget(null)
      load()
    } catch (err) {
      setCreateError(err.response?.data?.message ?? 'Failed to add driver.')
    } finally {
      setCreating(false)
    }
  }

  function openEdit(profile) {
    setEditTarget(profile)
    setEditForm({
      fullName: profile.fullName ?? '',
      phoneNumber: profile.phoneNumber ?? '',
      licenseNumber: profile.licenseNumber ?? '',
      licenseExpiryDate: profile.licenseExpiryDate ? profile.licenseExpiryDate.slice(0, 10) : '',
    })
    setEditPhotos(EMPTY_PHOTOS)
    setEditError('')
  }

  async function handleEdit(e) {
    e.preventDefault()
    setEditError('')
    if (!editForm.fullName || !editForm.licenseNumber || !editForm.licenseExpiryDate) {
      setEditError('Full name, license number and license expiry are required.')
      return
    }
    setSaving(true)
    try {
      await api.put(`/api/v1/DriverProfiles/${editTarget.id}`, {
        fullName: editForm.fullName,
        phoneNumber: editForm.phoneNumber,
        licenseNumber: editForm.licenseNumber,
        licenseExpiryDate: new Date(editForm.licenseExpiryDate).toISOString(),
      })
      await Promise.all(
        Object.entries(editPhotos)
          .filter(([, file]) => file)
          .map(([key, file]) => {
            const fd = new FormData()
            fd.append('image', file)
            return api.post(`/api/v1/DriverProfiles/${editTarget.id}/${PHOTO_ENDPOINTS[key]}`, fd).catch(() => {})
          })
      )
      setEditTarget(null)
      load()
    } catch (err) {
      setEditError(err.response?.data?.message ?? 'Failed to save changes.')
    } finally {
      setSaving(false)
    }
  }

  function openDelete(profile) {
    setDeleteTarget(profile)
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    setWorkingId(deleteTarget.id)
    try {
      await api.delete(`/api/v1/DriverProfiles/${deleteTarget.id}`)
      setDeleteTarget(null)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete driver profile.')
    } finally {
      setWorkingId(null)
    }
  }

  const activeCount = drivers.filter(d => d.profile?.isCurrent).length

  return (
    <>
      <main className="w-full px-4 sm:px-6 py-8">
          <FleetNav />

        <Collapsible title="About Driver Profiles" dismissKey="fleet.pageInfo.drivers.dismissed">
          <p className="text-sm text-gray-700">
            Driver profiles must be approved before a driver can be assigned to a trip; licence/ID expiry is tracked here.
          </p>
        </Collapsible>

        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-extrabold text-navy">Driver Profiles</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {loading ? 'Loading…' : `${drivers.length} driver${drivers.length !== 1 ? 's' : ''} · ${activeCount} active`}
            </p>
          </div>
          <div className="flex items-center gap-3">
            <button
                onClick={handleExportExcel}
                disabled={exporting || totalCount === 0}
                className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50"
            >
              <Download size={14} /> Excel
            </button>
            <button
                onClick={handleExportPdf}
                disabled={exporting || totalCount === 0}
                className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50"
            >
              <Download size={14} /> PDF
            </button>
          </div>
        </div>

        {/* Review modal */}
        {reviewInput.id && (
          <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
            <form onSubmit={handleReview} className="bg-white rounded-2xl shadow-2xl p-6 w-full max-w-sm space-y-4">
              <h3 className="font-bold text-gray-900 capitalize">{reviewInput.action} Profile</h3>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">
                  {reviewInput.action === 'approve' ? 'Approval Notes (optional)' : 'Rejection Reason'}
                </label>
                <textarea rows={3} value={reviewInput.notes}
                  onChange={e => setReviewInput(r => ({ ...r, notes: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
                  placeholder="Add notes…" />
              </div>
              <div className="flex gap-3">
                <button type="submit" disabled={workingId === reviewInput.id}
                  className={`flex-1 py-2.5 text-sm font-semibold rounded-xl text-white disabled:opacity-50 transition-colors ${
                    reviewInput.action === 'approve' ? 'bg-green-600 hover:bg-green-700' : 'bg-red-600 hover:bg-red-700'
                  }`}>
                  {workingId === reviewInput.id ? 'Processing…' : `Confirm ${reviewInput.action}`}
                </button>
                <button type="button" onClick={() => setReviewInput({ id: null, action: null, notes: '' })}
                  className="px-4 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50">Cancel</button>
              </div>
            </form>
          </div>
        )}

        {/* Create driver modal */}
        {createTarget && (
          <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
            <form onSubmit={handleCreate} className="bg-white rounded-2xl shadow-2xl p-6 w-full max-w-md max-h-[90vh] overflow-y-auto space-y-4">
              <h3 className="font-bold text-gray-900">Add Driver — {createTarget.email}</h3>
              {createError && <div className="text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2">{createError}</div>}
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Full Name *</label>
                <input value={createForm.fullName} onChange={e => setCreateForm(f => ({ ...f, fullName: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Phone Number</label>
                <input value={createForm.phoneNumber} onChange={e => setCreateForm(f => ({ ...f, phoneNumber: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">ID Number *</label>
                <input value={createForm.idNumber} onChange={e => setCreateForm(f => ({ ...f, idNumber: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">License Number *</label>
                <input value={createForm.licenseNumber} onChange={e => setCreateForm(f => ({ ...f, licenseNumber: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">License Expiry Date *</label>
                <input type="date" value={createForm.licenseExpiryDate} onChange={e => setCreateForm(f => ({ ...f, licenseExpiryDate: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" required />
              </div>

              <div className="border-t border-gray-100 pt-4 space-y-3">
                <p className="text-xs font-semibold text-gray-600 uppercase tracking-wider">Documents <span className="text-gray-400 font-normal normal-case">(optional)</span></p>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Profile Photo</label>
                  <SinglePhotoPicker file={createPhotos.profilePhoto} onChange={file => setCreatePhotos(p => ({ ...p, profilePhoto: file }))} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Licence — Front</label>
                  <SinglePhotoPicker file={createPhotos.licenceFront} onChange={file => setCreatePhotos(p => ({ ...p, licenceFront: file }))} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Licence — Back</label>
                  <SinglePhotoPicker file={createPhotos.licenceBack} onChange={file => setCreatePhotos(p => ({ ...p, licenceBack: file }))} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">National ID — Front</label>
                  <SinglePhotoPicker file={createPhotos.idFront} onChange={file => setCreatePhotos(p => ({ ...p, idFront: file }))} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">National ID — Back</label>
                  <SinglePhotoPicker file={createPhotos.idBack} onChange={file => setCreatePhotos(p => ({ ...p, idBack: file }))} />
                </div>
              </div>

              <div className="flex gap-3">
                <button type="submit" disabled={creating}
                  className="flex-1 py-2.5 text-sm font-semibold rounded-xl text-white bg-navy hover:bg-navy-dark disabled:opacity-50 transition-colors">
                  {creating ? 'Adding…' : 'Add Driver'}
                </button>
                <button type="button" onClick={() => setCreateTarget(null)}
                  className="px-4 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50">Cancel</button>
              </div>
            </form>
          </div>
        )}

        {/* Edit driver modal */}
        {editTarget && (
          <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
            <form onSubmit={handleEdit} className="bg-white rounded-2xl shadow-2xl p-6 w-full max-w-md max-h-[90vh] overflow-y-auto space-y-4">
              <h3 className="font-bold text-gray-900">{editForm.fullName || 'Driver Profile'}</h3>
              {editError && <div className="text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2">{editError}</div>}
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Full Name *</label>
                <input value={editForm.fullName} onChange={e => setEditForm(f => ({ ...f, fullName: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Phone Number</label>
                <input value={editForm.phoneNumber} onChange={e => setEditForm(f => ({ ...f, phoneNumber: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">License Number *</label>
                <input value={editForm.licenseNumber} onChange={e => setEditForm(f => ({ ...f, licenseNumber: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">License Expiry Date *</label>
                <input type="date" value={editForm.licenseExpiryDate} onChange={e => setEditForm(f => ({ ...f, licenseExpiryDate: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold" required />
              </div>

              <div className="border-t border-gray-100 pt-4 space-y-3">
                <p className="text-xs font-semibold text-gray-600 uppercase tracking-wider">Documents</p>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Profile Photo</label>
                  <EditPhotoSlot currentUrl={editTarget[PHOTO_URL_KEYS.profilePhoto]} file={editPhotos.profilePhoto}
                    onChange={file => setEditPhotos(p => ({ ...p, profilePhoto: file }))} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Licence — Front</label>
                  <EditPhotoSlot currentUrl={editTarget[PHOTO_URL_KEYS.licenceFront]} file={editPhotos.licenceFront}
                    onChange={file => setEditPhotos(p => ({ ...p, licenceFront: file }))} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Licence — Back</label>
                  <EditPhotoSlot currentUrl={editTarget[PHOTO_URL_KEYS.licenceBack]} file={editPhotos.licenceBack}
                    onChange={file => setEditPhotos(p => ({ ...p, licenceBack: file }))} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">National ID — Front</label>
                  <EditPhotoSlot currentUrl={editTarget[PHOTO_URL_KEYS.idFront]} file={editPhotos.idFront}
                    onChange={file => setEditPhotos(p => ({ ...p, idFront: file }))} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">National ID — Back</label>
                  <EditPhotoSlot currentUrl={editTarget[PHOTO_URL_KEYS.idBack]} file={editPhotos.idBack}
                    onChange={file => setEditPhotos(p => ({ ...p, idBack: file }))} />
                </div>
              </div>

              <div className="flex gap-3">
                <button type="submit" disabled={saving}
                  className="flex-1 py-2.5 text-sm font-semibold rounded-xl text-white bg-navy hover:bg-navy-dark disabled:opacity-50 transition-colors">
                  {saving ? 'Saving…' : 'Save Changes'}
                </button>
                <button type="button" onClick={() => setEditTarget(null)}
                  className="px-4 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50">Cancel</button>
              </div>
            </form>
          </div>
        )}

        {/* Delete confirmation modal */}
        {deleteTarget && (
          <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-red-100 flex items-center justify-center flex-shrink-0">
                  <svg className="w-5 h-5 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </div>
                <div>
                  <h3 className="text-base font-bold text-gray-900">Delete Driver Profile</h3>
                  <p className="text-xs text-gray-500 mt-0.5">This action cannot be undone.</p>
                </div>
              </div>
              <p className="text-sm text-gray-600">
                Are you sure you want to delete the driver profile for{' '}
                <span className="font-semibold text-gray-900">{deleteTarget.fullName}</span>?
              </p>
              <div className="flex gap-3 pt-2">
                <button onClick={() => setDeleteTarget(null)}
                  className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors">
                  Cancel
                </button>
                <button onClick={confirmDelete} disabled={workingId === deleteTarget.id}
                  className="flex-1 py-2.5 bg-red-600 hover:bg-red-700 text-white text-sm font-bold rounded-xl transition-colors disabled:opacity-50">
                  {workingId === deleteTarget.id ? 'Deleting…' : 'Yes, Delete'}
                </button>
              </div>
            </div>
          </div>
        )}

        {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{error}</div>}

        {loading ? (
          <div className="space-y-4">
            {[1,2,3].map(i => <div key={i} className="h-28 bg-white rounded-2xl border animate-pulse" />)}
          </div>
        ) : drivers.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
            <div className="flex justify-center mb-3 text-gray-400"><User size={40} /></div>
            <h3 className="font-semibold text-gray-700">No drivers found</h3>
            <p className="text-sm text-gray-400 mt-1">No users with the Fleet Staff role exist yet.</p>
          </div>
        ) : (
          <div className="space-y-4">
            {pagedDrivers.map(({ user, profile }) => {
              const hasProfile  = !!profile
              const statusMeta  = hasProfile ? (STATUS[profile.status] ?? STATUS[0]) : null
              const expiryBadge = hasProfile ? licenseExpiryBadge(profile.daysUntilLicenseExpiry) : null
              const name        = hasProfile
                ? (profile.fullName || `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim())
                : `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim()

              return (
                <div key={user.id}
                  onClick={() => hasProfile && openEdit(profile)}
                  className={`bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden ${hasProfile ? 'cursor-pointer hover:border-gold transition-colors' : ''}`}>

                  {/* Main row */}
                  <div className="flex items-start gap-4 p-5">

                    {/* Avatar */}
                    <div className="shrink-0">
                      {hasProfile && profile.profilePhotoUrl ? (
                        <img src={profile.profilePhotoUrl} alt={name}
                          className="w-14 h-14 rounded-xl object-cover border border-gray-200" />
                      ) : (
                        <div className="w-14 h-14 rounded-xl bg-gray-100 flex items-center justify-center text-gray-400"><User size={24} /></div>
                      )}
                    </div>

                    {/* Info */}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap mb-1">
                        <p className="font-bold text-gray-900 text-base">{name || '—'}</p>
                        {hasProfile ? (
                          <>
                            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border ${statusMeta.cls}`}>
                              {statusMeta.label}
                            </span>
                            {profile.isCurrent && (
                              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-green-50 text-green-700 border border-green-200">
                                Active
                              </span>
                            )}
                          </>
                        ) : (
                          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border border-gray-200 bg-gray-50 text-gray-500">
                            No profile
                          </span>
                        )}
                      </div>

                      <div className="grid grid-cols-2 sm:grid-cols-3 gap-x-6 gap-y-1 text-sm text-gray-500">
                        <span><span className="font-medium text-gray-700">Email:</span> {user.email || '—'}</span>
                        {hasProfile && <>
                          <span><span className="font-medium text-gray-700">Phone:</span> {profile.phoneNumber || '—'}</span>
                          <span><span className="font-medium text-gray-700">ID No:</span> {profile.idNumber || '—'}</span>
                          <span><span className="font-medium text-gray-700">License:</span> {profile.licenseNumber || '—'}</span>
                          <span className="flex items-center gap-1.5 flex-wrap">
                            <span className="font-medium text-gray-700">Expiry:</span>
                            <span>{profile.licenseExpiryDate ? new Date(profile.licenseExpiryDate).toLocaleDateString('en-KE', { day: 'numeric', month: 'short', year: 'numeric' }) : '—'}</span>
                            {expiryBadge && (
                              <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold border ${expiryBadge.cls}`}>
                                {expiryBadge.label}
                              </span>
                            )}
                          </span>
                          {profile.licenseClasses?.length > 0 && (
                            <span><span className="font-medium text-gray-700">Classes:</span> {profile.licenseClasses.join(', ')}</span>
                          )}
                        </>}
                      </div>

                      {/* Rejection reason */}
                      {hasProfile && profile.rejectionReason && (
                        <p className="mt-2 text-xs text-red-600 bg-red-50 border border-red-100 rounded-lg px-3 py-1.5">
                          Rejected: {profile.rejectionReason}
                        </p>
                      )}
                    </div>

                    {/* Actions */}
                    <div className="flex flex-col gap-1.5 shrink-0">
                      {hasProfile && profile.status === 0 && (
                        <button onClick={e => { e.stopPropagation(); handleSubmit(profile.id) }}
                          disabled={workingId === profile.id}
                          className="text-xs px-3 py-1.5 bg-navy hover:bg-navy-dark text-white rounded-lg font-medium transition-colors disabled:opacity-50">
                          {workingId === profile.id ? 'Submitting…' : 'Submit for Review'}
                        </button>
                      )}
                      {hasProfile && profile.status === 1 && (
                        <>
                          <button onClick={e => { e.stopPropagation(); setReviewInput({ id: profile.id, action: 'approve', notes: '' }) }}
                            className="text-xs px-3 py-1.5 bg-green-600 hover:bg-green-700 text-white rounded-lg font-medium transition-colors">
                            Approve
                          </button>
                          <button onClick={e => { e.stopPropagation(); setReviewInput({ id: profile.id, action: 'reject', notes: '' }) }}
                            className="text-xs px-3 py-1.5 bg-red-600 hover:bg-red-700 text-white rounded-lg font-medium transition-colors">
                            Reject
                          </button>
                        </>
                      )}
                      {!hasProfile && (
                        <button onClick={e => { e.stopPropagation(); openCreate(user) }}
                          className="text-xs px-3 py-1.5 bg-navy hover:bg-navy-dark text-white rounded-lg font-medium transition-colors">
                          Add Driver
                        </button>
                      )}
                      {hasProfile && (
                        <button onClick={e => { e.stopPropagation(); openDelete(profile) }}
                          className="text-xs px-3 py-1.5 border border-red-200 rounded-lg hover:bg-red-50 text-red-600 font-medium transition-colors">
                          Delete
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              )
            })}
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </div>
        )}
      </main>
    </>
  )
}
