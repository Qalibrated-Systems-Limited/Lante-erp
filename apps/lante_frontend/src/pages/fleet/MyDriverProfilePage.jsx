import { useState, useEffect, useCallback } from 'react'
import FleetNav from './FleetNav.jsx'
import SinglePhotoPicker from '../../components/SinglePhotoPicker.jsx'
import EditPhotoSlot from '../../components/EditPhotoSlot.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'

const EMPTY_PHOTOS = { profilePhoto: null, licenceFront: null, licenceBack: null, idFront: null, idBack: null }
// Maps each photo's state key to its upload endpoint segment (note: "licence", matching the API's spelling).
const PHOTO_ENDPOINTS = {
  profilePhoto: 'profile-photo',
  licenceFront: 'licence-front',
  licenceBack: 'licence-back',
  idFront: 'id-front',
  idBack: 'id-back',
}
const PHOTO_URL_KEYS = {
  profilePhoto: 'profilePhotoUrl',
  licenceFront: 'licenseFrontImageUrl',
  licenceBack: 'licenseBackImageUrl',
  idFront: 'idFrontImageUrl',
  idBack: 'idBackImageUrl',
}
const PHOTO_LABELS = {
  profilePhoto: 'Profile Photo',
  licenceFront: 'Licence — Front',
  licenceBack: 'Licence — Back',
  idFront: 'National ID — Front',
  idBack: 'National ID — Back',
}
// status is a number from the API: 0=Draft, 1=Pending, 2=Approved, 3=Rejected
const STATUS = {
  0: { label: 'Draft',           cls: 'bg-gray-50 text-gray-600 border-gray-200' },
  1: { label: 'Pending Review',  cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  2: { label: 'Approved',        cls: 'bg-green-50 text-green-700 border-green-200' },
  3: { label: 'Rejected',        cls: 'bg-red-50 text-red-700 border-red-200' },
}

export default function MyDriverProfilePage() {
  const { user } = useAuth()
  const [profile, setProfile] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')

  const [form, setForm]       = useState({ fullName: '', phoneNumber: '', idNumber: '', licenseNumber: '', licenseExpiryDate: '' })
  const [photos, setPhotos]   = useState(EMPTY_PHOTOS)
  const [saving, setSaving]   = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError]   = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get(`/api/v1/DriverProfiles/driver/${user.id}/current`)
      const p = res.data?.data ?? null
      setProfile(p)
      setForm({
        fullName: p?.fullName ?? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim(),
        phoneNumber: p?.phoneNumber ?? '',
        idNumber: p?.idNumber ?? '',
        licenseNumber: p?.licenseNumber ?? '',
        licenseExpiryDate: p?.licenseExpiryDate ? p.licenseExpiryDate.slice(0, 10) : '',
      })
    } catch (err) {
      if (err.response?.status === 404) {
        setProfile(null)
        setForm({ fullName: `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim(), phoneNumber: '', idNumber: '', licenseNumber: '', licenseExpiryDate: '' })
      } else {
        setError('Failed to load your driver profile.')
      }
    } finally {
      setLoading(false)
    }
  }, [user.id, user.firstName, user.lastName])

  useEffect(() => { load() }, [load])

  const isNew     = !profile
  // Draft (0) or Rejected (3) — the two states a driver can still edit and (re)submit from.
  const canEdit   = isNew || profile.status === 0 || profile.status === 3
  const canSubmit = !isNew && (profile.status === 0 || profile.status === 3)
  const statusMeta = profile ? (STATUS[profile.status] ?? STATUS[0]) : null

  async function handleSave(e) {
    e.preventDefault()
    setFormError('')
    if (!form.fullName || !form.licenseNumber || !form.licenseExpiryDate || (isNew && !form.idNumber)) {
      setFormError('Full name, ID number, license number and license expiry are required.')
      return
    }
    setSaving(true)
    try {
      let profileId = profile?.id
      if (isNew) {
        const res = await api.post('/api/v1/DriverProfiles', {
          driverId: user.id,
          fullName: form.fullName,
          phoneNumber: form.phoneNumber,
          idNumber: form.idNumber,
          licenseNumber: form.licenseNumber,
          licenseExpiryDate: new Date(form.licenseExpiryDate).toISOString(),
        })
        profileId = res.data?.data?.id
      } else {
        await api.put(`/api/v1/DriverProfiles/${profileId}`, {
          fullName: form.fullName,
          phoneNumber: form.phoneNumber,
          licenseNumber: form.licenseNumber,
          licenseExpiryDate: new Date(form.licenseExpiryDate).toISOString(),
        })
      }
      if (profileId) {
        await Promise.all(
          Object.entries(photos)
            .filter(([, file]) => file)
            .map(([key, file]) => {
              const fd = new FormData()
              fd.append('image', file)
              return api.post(`/api/v1/DriverProfiles/${profileId}/${PHOTO_ENDPOINTS[key]}`, fd).catch(() => {})
            })
        )
      }
      setPhotos(EMPTY_PHOTOS)
      await load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to save your profile.')
    } finally {
      setSaving(false)
    }
  }

  async function handleSubmitForReview() {
    setSubmitting(true)
    setError('')
    try {
      await api.post(`/api/v1/DriverProfiles/${profile.id}/submit`)
      await load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to submit your profile for review.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="w-full px-4 sm:px-6 py-8 max-w-2xl">
      <FleetNav />

      <Collapsible title="About My Profile" dismissKey="fleet.pageInfo.myProfile.dismissed">
        <p className="text-sm text-gray-700">
          Fill in your details and documents, then submit for review. A Fleet Manager or Admin must
          approve your profile before you can be assigned to trips.
        </p>
      </Collapsible>

      <div className="flex items-center gap-3 mb-4">
        <h1 className="text-2xl font-extrabold text-navy">My Driver Profile</h1>
        {statusMeta && (
          <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border ${statusMeta.cls}`}>
            {statusMeta.label}
          </span>
        )}
      </div>

      {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{error}</div>}

      {loading ? (
        <div className="h-64 bg-white rounded-2xl border animate-pulse" />
      ) : (
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6 space-y-4">
          {profile?.status === 3 && profile.rejectionReason && (
            <p className="text-sm text-red-600 bg-red-50 border border-red-100 rounded-lg px-3 py-2">
              Rejected: {profile.rejectionReason}. Update your details below and submit again.
            </p>
          )}
          {profile?.status === 1 && (
            <p className="text-sm text-amber-700 bg-amber-50 border border-amber-100 rounded-lg px-3 py-2">
              Submitted — awaiting review by a Fleet Manager or Admin.
            </p>
          )}
          {profile?.status === 2 && (
            <p className="text-sm text-green-700 bg-green-50 border border-green-100 rounded-lg px-3 py-2">
              Approved — you're all set to be assigned to trips.
            </p>
          )}

          {formError && <div className="text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2">{formError}</div>}

          <form onSubmit={handleSave} className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">Full Name *</label>
              <input value={form.fullName} onChange={e => setForm(f => ({ ...f, fullName: e.target.value }))}
                disabled={!canEdit} required
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold disabled:bg-gray-50 disabled:text-gray-500" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">Phone Number</label>
              <input value={form.phoneNumber} onChange={e => setForm(f => ({ ...f, phoneNumber: e.target.value }))}
                disabled={!canEdit}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold disabled:bg-gray-50 disabled:text-gray-500" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">ID Number {isNew && '*'}</label>
              <input value={form.idNumber} onChange={e => setForm(f => ({ ...f, idNumber: e.target.value }))}
                disabled={!isNew} required={isNew}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold disabled:bg-gray-50 disabled:text-gray-500" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">License Number *</label>
              <input value={form.licenseNumber} onChange={e => setForm(f => ({ ...f, licenseNumber: e.target.value }))}
                disabled={!canEdit} required
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold disabled:bg-gray-50 disabled:text-gray-500" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">License Expiry Date *</label>
              <input type="date" value={form.licenseExpiryDate} onChange={e => setForm(f => ({ ...f, licenseExpiryDate: e.target.value }))}
                disabled={!canEdit} required
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold disabled:bg-gray-50 disabled:text-gray-500" />
            </div>

            <div className="border-t border-gray-100 pt-4 space-y-3">
              <p className="text-xs font-semibold text-gray-600 uppercase tracking-wider">
                Documents {isNew && <span className="text-gray-400 font-normal normal-case">(optional, can add later)</span>}
              </p>
              {Object.keys(PHOTO_LABELS).map(key => (
                <div key={key}>
                  <label className="block text-xs font-medium text-gray-500 mb-1">{PHOTO_LABELS[key]}</label>
                  {canEdit ? (
                    isNew ? (
                      <SinglePhotoPicker file={photos[key]} onChange={file => setPhotos(p => ({ ...p, [key]: file }))} />
                    ) : (
                      <EditPhotoSlot currentUrl={profile[PHOTO_URL_KEYS[key]]} file={photos[key]}
                        onChange={file => setPhotos(p => ({ ...p, [key]: file }))} />
                    )
                  ) : profile[PHOTO_URL_KEYS[key]] ? (
                    <img src={profile[PHOTO_URL_KEYS[key]]} alt={PHOTO_LABELS[key]}
                      className="w-14 h-14 object-cover rounded-lg border border-gray-200" />
                  ) : (
                    <p className="text-sm text-gray-400">Not uploaded</p>
                  )}
                </div>
              ))}
            </div>

            {canEdit && (
              <div className="flex gap-3 pt-2">
                <button type="submit" disabled={saving}
                  className="flex-1 py-2.5 text-sm font-semibold rounded-xl text-white bg-navy hover:bg-navy-dark disabled:opacity-50 transition-colors">
                  {saving ? 'Saving…' : isNew ? 'Save Profile' : 'Save Changes'}
                </button>
                {canSubmit && (
                  <button type="button" onClick={handleSubmitForReview} disabled={submitting || saving}
                    className="flex-1 py-2.5 text-sm font-semibold rounded-xl text-white bg-green-600 hover:bg-green-700 disabled:opacity-50 transition-colors">
                    {submitting ? 'Submitting…' : 'Submit for Review'}
                  </button>
                )}
              </div>
            )}
          </form>
        </div>
      )}
    </main>
  )
}
