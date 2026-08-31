import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Truck, User, Tag, Package, Camera, MapPin, Flag, Calendar, Ruler, Banknote, Ticket } from 'lucide-react'
import SinglePhotoPicker from '../../components/SinglePhotoPicker.jsx'
import ImagePicker from '../../components/ImagePicker.jsx'
import api from '../../api/axios.js'

const STEPS = ['Select\nTruck', 'Enter\nRoute', 'Add\nDetails', 'Review']

// Mirrors FleetService.Core.Entities.TripType's enums (TripType.cs) — numeric
// values match the C# enum declaration order, since the API serializes them
// as numbers (no JsonStringEnumConverter registered).
const TRIP_CATEGORY = { LOADED: 0, EMPTY: 1, MAINTENANCE: 2, OTHER: 3 }
const MATERIAL_REQUIREMENT = { NONE: 0, OPTIONAL: 1, MANDATORY: 2 }

function ProgressStep({ index, label, active }) {
  return (
    <div className="flex flex-col items-center gap-1.5 flex-1">
      <div className={`w-10 h-10 rounded-full flex items-center justify-center text-base font-extrabold border-2 transition-colors ${
        active
          ? 'bg-navy border-navy text-white'
          : 'bg-white border-gray-200 text-gray-400'
      }`}>
        {index + 1}
      </div>
      <span className={`text-[11px] font-semibold text-center leading-tight whitespace-pre-line ${active ? 'text-navy' : 'text-gray-400'}`}>
        {label}
      </span>
    </div>
  )
}

function FieldCard({ children, instruction }) {
  return (
    <div>
      <div className="bg-white rounded-2xl border border-zinc-300/40 shadow-sm p-5">
        {children}
      </div>
      {instruction && (
        <p className="text-xs text-zinc-900/70 mt-1.5 ml-1">{instruction}</p>
      )}
    </div>
  )
}

function Label({ children, required }) {
  return (
    <label className="block text-sm font-semibold text-gray-700 mb-1.5">
      {children} {required && <span className="text-red-500">*</span>}
    </label>
  )
}

export default function CreateTripPage() {
  const navigate = useNavigate()

  const [submitting, setSubmitting] = useState(false)
  const [error, setError]           = useState('')
  const [trucks, setTrucks]         = useState([])
  const [drivers, setDrivers]       = useState([])
  const [tripTypes, setTripTypes]   = useState([])
  const [materials, setMaterials]   = useState([])
  const [loadingRefs, setLoadingRefs] = useState(true)

  const [form, setForm] = useState({
    truckId:        '',
    driverId:       '',
    tripTypeId:     '',
    startLocation:  '',
    endLocation:    '',
    startMileage:   '',
    revenue:        '',
    date:           new Date().toISOString().slice(0, 10),
    linkedTicketId: '',
    materialId:     '',
  })
  const [odometerStartPhoto, setOdometerStartPhoto] = useState(null)
  const [materialPhoto, setMaterialPhoto] = useState(null)
  const [tripPhotos, setTripPhotos] = useState([])

  const [showAddMaterial, setShowAddMaterial] = useState(false)
  const [newMaterialForm, setNewMaterialForm] = useState({ name: '', description: '' })
  const [savingMaterial, setSavingMaterial] = useState(false)
  const [materialErr, setMaterialErr] = useState('')

  async function handleCreateMaterial(e) {
    e.preventDefault()
    setMaterialErr('')
    if (!newMaterialForm.name.trim()) {
      setMaterialErr('Name is required.')
      return
    }
    setSavingMaterial(true)
    try {
      const res = await api.post('/api/v1/materials', {
        name: newMaterialForm.name.trim(),
        description: newMaterialForm.description.trim() || null,
      })
      const created = res.data?.data
      setMaterials(ms => [...ms, created])
      set('materialId', created.id)
      setNewMaterialForm({ name: '', description: '' })
      setShowAddMaterial(false)
    } catch (err) {
      setMaterialErr(err.response?.data?.message ?? 'Failed to create material.')
    } finally {
      setSavingMaterial(false)
    }
  }

  useEffect(() => {
    Promise.all([
      api.get('/api/v1/trucks').catch(() => ({ data: { data: [] } })),
      api.get('/api/v1/DriverProfiles').catch(() => ({ data: { data: [] } })),
      api.get('/api/v1/triptypes', { params: { isActive: true } }).catch(() => ({ data: { data: [] } })),
      api.get('/api/v1/materials').catch(() => ({ data: { data: [] } })),
    ]).then(([tr, dr, tt, mt]) => {
      setTrucks(tr.data?.data ?? [])
      setDrivers(dr.data?.data ?? [])
      setTripTypes(tt.data?.data ?? [])
      setMaterials(mt.data?.data ?? [])
    }).finally(() => setLoadingRefs(false))
  }, [])

  function set(field, value) { setForm(f => ({ ...f, [field]: value })) }

  function handleTruckChange(truckId) {
    const truck = trucks.find(t => t.id === truckId)
    const assignedDriverId = truck?.driverId
    const driverIsKnown = assignedDriverId && drivers.some(d => (d.driverId ?? d.id) === assignedDriverId)
    setForm(f => ({ ...f, truckId, driverId: driverIsKnown ? assignedDriverId : f.driverId }))
  }

  const stepFilled = [
    !!form.truckId && !!form.driverId,
    !!form.startLocation && !!form.endLocation,
    !!form.tripTypeId,
    true,
  ]

  const selectedTruck = trucks.find(t => t.id === form.truckId)
  const selectedTripType = tripTypes.find(tt => tt.id === form.tripTypeId)

  // Only Loaded trips carry a fare — Empty/Maintenance/Other default to 0, matching the mobile app.
  const showRevenue = selectedTripType?.category === TRIP_CATEGORY.LOADED
  // Material visibility/requirement follows the trip type's own MaterialRequirement, not a guess from its name.
  const showMaterial = selectedTripType && selectedTripType.materialRequirement !== MATERIAL_REQUIREMENT.NONE
  const materialRequired = selectedTripType?.materialRequirement === MATERIAL_REQUIREMENT.MANDATORY

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')
    if (!form.truckId || !form.driverId)          { setError('Please select a truck and driver.'); return }
    if (!form.startLocation || !form.endLocation) { setError('Start and end location are required.'); return }
    if (!form.tripTypeId)                          { setError('Please select a trip type.'); return }
    if (form.startMileage === '')                  { setError('Start mileage is required.'); return }
    if (!odometerStartPhoto)                       { setError('An odometer photo is required.'); return }
    if (showRevenue && form.revenue === '')        { setError('Revenue is required.'); return }
    if (materialRequired && !form.materialId)      { setError('Please select a material for this trip type.'); return }
    if (selectedTruck?.odometer != null && parseFloat(form.startMileage) < Number(selectedTruck.odometer)) {
      setError(`Start mileage can't be less than the truck's last recorded mileage (${Number(selectedTruck.odometer).toLocaleString()} km).`)
      return
    }
    if (selectedTruck?.odometer != null && parseFloat(form.startMileage) - Number(selectedTruck.odometer) > 2000) {
      setError(`Start mileage is ${(parseFloat(form.startMileage) - Number(selectedTruck.odometer)).toLocaleString()} km above the truck's last recorded mileage — that's more than the 2,000 km sanity limit per entry. Double-check the value.`)
      return
    }

    setSubmitting(true)
    try {
      const fd = new FormData()
      fd.append('truckId', form.truckId)
      fd.append('driverId', form.driverId)
      fd.append('tripTypeId', form.tripTypeId)
      fd.append('startLocation', form.startLocation)
      fd.append('endLocation', form.endLocation)
      fd.append('startMileage', form.startMileage)
      fd.append('revenue', showRevenue ? form.revenue : '0')
      fd.append('date', new Date(form.date).toISOString())
      if (form.linkedTicketId) fd.append('linkedTicketId', form.linkedTicketId)
      if (showMaterial && form.materialId) fd.append('materialId', form.materialId)
      if (odometerStartPhoto) fd.append('odometerStartPhoto', odometerStartPhoto)
      if (showMaterial && materialPhoto) fd.append('materialPhoto', materialPhoto)
      tripPhotos.forEach(file => fd.append('photos', file))

      const res = await api.post('/api/v1/trips', fd)
      const id  = res.data?.data?.id
      navigate(id ? `/modules/fleet/trips/${id}` : '/modules/fleet/trips')
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to create trip.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>
      <main className="max-w-2xl mx-auto w-full px-4 sm:px-6 py-6">

        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <button onClick={() => navigate('/modules/fleet/trips')}
            className="p-2 hover:bg-gray-100 rounded-xl text-gray-500 transition-colors">
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <h1 className="text-2xl font-extrabold text-navy">Create New Trip</h1>
        </div>

        {/* Progress card */}
        <div className="bg-zinc-50 rounded-2xl border-2 border-zinc-300/50 p-5 mb-6 shadow-sm">
          <p className="text-sm font-extrabold text-navy mb-4">Trip Setup Progress</p>
          <div className="flex items-start">
            {STEPS.map((label, i) => (
              <div key={i} className="flex items-center flex-1">
                <ProgressStep index={i} label={label} active={stepFilled[i]} />
                {i < STEPS.length - 1 && (
                  <div className={`h-0.5 flex-1 mt-[-12px] mx-1 rounded ${stepFilled[i] ? 'bg-zinc-600' : 'bg-gray-200'}`} />
                )}
              </div>
            ))}
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 rounded-2xl px-5 py-3 text-sm">{error}</div>
          )}

          {/* Truck */}
          <FieldCard instruction="Select the vehicle assigned to this trip">
            <Label required><span className="inline-flex items-center gap-1.5"><Truck size={14} />Truck</span></Label>
            {loadingRefs ? (
              <LoadingField label="Loading trucks…" />
            ) : trucks.length > 0 ? (
              <select value={form.truckId} onChange={e => handleTruckChange(e.target.value)}
                className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600" required>
                <option value="">Choose your truck…</option>
                {trucks.map(t => <option key={t.id} value={t.id}>{t.licensePlate} — {t.model}</option>)}
              </select>
            ) : (
              <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 text-sm text-amber-800">
                No trucks registered yet —{' '}
                <button type="button" onClick={() => navigate('/modules/fleet/trucks')} className="font-bold underline">
                  register one
                </button> first.
              </div>
            )}
          </FieldCard>

          {/* Driver */}
          <FieldCard instruction="Select the driver assigned to this trip">
            <Label required><span className="inline-flex items-center gap-1.5"><User size={14} />Driver</span></Label>
            {loadingRefs ? (
              <LoadingField label="Loading drivers…" />
            ) : drivers.length > 0 ? (
              <select value={form.driverId} onChange={e => set('driverId', e.target.value)}
                className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600" required>
                <option value="">Choose driver…</option>
                {drivers.filter(d => d.isCurrent).map(d => (
                  <option key={d.id} value={d.driverId ?? d.id}>
                    {d.fullName ?? d.licenseNumber ?? d.driverId ?? d.id}
                  </option>
                ))}
              </select>
            ) : (
              <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 text-sm text-amber-800">
                No driver profiles registered yet —{' '}
                <button type="button" onClick={() => navigate('/modules/fleet/drivers')} className="font-bold underline">
                  register one
                </button> first.
              </div>
            )}
          </FieldCard>

          {/* Trip Type */}
          <FieldCard instruction="Select the type of trip being made">
            <Label required><span className="inline-flex items-center gap-1.5"><Tag size={14} />Trip Type</span></Label>
            {loadingRefs ? (
              <LoadingField label="Loading trip types…" />
            ) : tripTypes.length > 0 ? (
              <select value={form.tripTypeId} onChange={e => set('tripTypeId', e.target.value)}
                className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600" required>
                <option value="">Select trip type…</option>
                {tripTypes.map(tt => <option key={tt.id} value={tt.id}>{tt.name}</option>)}
              </select>
            ) : (
              <input type="text" value={form.tripTypeId} onChange={e => set('tripTypeId', e.target.value)}
                placeholder="Trip type ID"
                className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600" required />
            )}
          </FieldCard>

          {/* Material — shown only when the selected trip type actually carries cargo */}
          {showMaterial && (
            <FieldCard instruction={materialRequired ? "What's being hauled on this trip" : "What's being hauled on this trip (optional)"}>
              <Label required={materialRequired}><span className="inline-flex items-center gap-1.5"><Package size={14} />Material</span></Label>
              {loadingRefs ? (
                <LoadingField label="Loading materials…" />
              ) : (
                <div className="flex gap-2">
                  <select value={form.materialId} onChange={e => set('materialId', e.target.value)}
                    className="w-full px-4 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600" style={{ height: 48 }} required={materialRequired}>
                    <option value="">{materialRequired ? 'Select material…' : 'No material'}</option>
                    {materials.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
                  </select>
                  <button
                    type="button"
                    onClick={() => { setMaterialErr(''); setNewMaterialForm({ name: '', description: '' }); setShowAddMaterial(true) }}
                    className="px-4 text-sm rounded-xl border border-gray-200 text-gray-600 hover:bg-gray-50 font-bold whitespace-nowrap"
                    style={{ height: 48 }}
                    title="Add a new material"
                  >
                    + Add Material
                  </button>
                </div>
              )}
              <div className="mt-4">
                <Label><span className="inline-flex items-center gap-1.5"><Camera size={14} />Photo of Loaded Material</span> <span className="text-gray-400 font-normal">(optional)</span></Label>
                <SinglePhotoPicker file={materialPhoto} onChange={setMaterialPhoto} />
              </div>
            </FieldCard>
          )}

          {/* Start Location */}
          <FieldCard instruction="Departure location for this trip">
            <Label required><span className="inline-flex items-center gap-1.5"><MapPin size={14} />Start Location</span></Label>
            <input
              type="text"
              value={form.startLocation}
              onChange={e => set('startLocation', e.target.value)}
              placeholder="e.g. Nairobi Depot"
              className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600"
              required
            />
          </FieldCard>

          {/* End Location */}
          <FieldCard instruction="Destination of the trip">
            <Label required><span className="inline-flex items-center gap-1.5"><Flag size={14} />End Location</span></Label>
            <input
              type="text"
              value={form.endLocation}
              onChange={e => set('endLocation', e.target.value)}
              placeholder="e.g. Mombasa"
              className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600"
              required
            />
          </FieldCard>

          {/* Date & Mileage */}
          <FieldCard instruction="Enter starting odometer reading">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label required><span className="inline-flex items-center gap-1.5"><Calendar size={14} />Trip Date</span></Label>
                <input
                  type="date"
                  value={form.date}
                  onChange={e => set('date', e.target.value)}
                  className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600"
                  required
                />
              </div>
              <div>
                <Label required><span className="inline-flex items-center gap-1.5"><Ruler size={14} />Start Mileage (km)</span></Label>
                <input
                  type="number"
                  step="0.1"
                  min={selectedTruck?.odometer ?? 0}
                  value={form.startMileage}
                  onChange={e => set('startMileage', e.target.value)}
                  placeholder="e.g. 45230"
                  className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600"
                  required
                />
                {selectedTruck?.odometer != null && (
                  <p className="text-xs text-gray-400 mt-1">Truck's last recorded mileage: {Number(selectedTruck.odometer).toLocaleString()} km</p>
                )}
              </div>
            </div>
            <div className="mt-4">
              <Label required><span className="inline-flex items-center gap-1.5"><Camera size={14} />Odometer Photo</span></Label>
              <SinglePhotoPicker file={odometerStartPhoto} onChange={setOdometerStartPhoto} />
            </div>
          </FieldCard>

          {/* Revenue — only Loaded trips carry a fare; Empty/Maintenance/Other default to 0 */}
          {showRevenue && (
            <FieldCard instruction="Agreed fare / contract value for this trip">
              <Label required><span className="inline-flex items-center gap-1.5"><Banknote size={14} />Revenue (KES)</span></Label>
              <input
                type="number"
                step="0.01"
                min="0"
                value={form.revenue}
                onChange={e => set('revenue', e.target.value)}
                placeholder="e.g. 25000"
                className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600"
                required
              />
            </FieldCard>
          )}

          {/* Trip Photos — general vehicle photos, e.g. the full truck */}
          <FieldCard instruction="Additional photos, e.g. of the full truck (optional)">
            <ImagePicker label={<span className="inline-flex items-center gap-1.5"><Truck size={14} />Trip Photos</span>} files={tripPhotos} onChange={setTripPhotos} />
          </FieldCard>

          {/* Linked Ticket */}
          <FieldCard instruction="When the trip starts/completes, the linked ticket is updated automatically">
            <Label><span className="inline-flex items-center gap-1.5"><Ticket size={14} />Linked Ticket ID</span> <span className="text-gray-400 font-normal">(optional)</span></Label>
            <input
              type="text"
              value={form.linkedTicketId}
              onChange={e => set('linkedTicketId', e.target.value)}
              placeholder="Ticket ID if linked to a support ticket…"
              className="w-full px-4 py-3 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-zinc-600"
            />
          </FieldCard>

          {/* Submit */}
          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={() => navigate('/modules/fleet/trips')}
              className="flex-1 py-3.5 border-2 border-zinc-300 text-navy text-base font-semibold rounded-2xl hover:bg-zinc-50 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting || !form.truckId || !form.driverId || !form.tripTypeId || !form.startLocation || !form.endLocation || form.startMileage === '' || !odometerStartPhoto || (showRevenue && form.revenue === '') || (materialRequired && !form.materialId)}
              className="flex-1 py-3.5 bg-navy hover:bg-navy-dark text-white text-base font-semibold rounded-2xl transition-colors disabled:opacity-50 shadow-lg"
            >
              {submitting ? (
                <span className="flex items-center justify-center gap-2">
                  <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/>
                  </svg>
                  Creating…
                </span>
              ) : 'Create Trip'}
            </button>
          </div>
        </form>
      </main>

      {showAddMaterial && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40">
          <form onSubmit={handleCreateMaterial} className="bg-white rounded-2xl shadow-2xl p-6 w-full max-w-md space-y-4">
            <h3 className="font-bold text-gray-900 text-lg">New Material</h3>
            {materialErr && (
              <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2">{materialErr}</div>
            )}
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">Name *</label>
              <input
                value={newMaterialForm.name}
                onChange={e => setNewMaterialForm(f => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Sand"
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
                required
                autoFocus
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">Description</label>
              <textarea
                value={newMaterialForm.description}
                onChange={e => setNewMaterialForm(f => ({ ...f, description: e.target.value }))}
                rows={2}
                placeholder="Optional description…"
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
              />
            </div>
            <div className="flex gap-3 pt-2">
              <button type="submit" disabled={savingMaterial} className="flex-1 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-colors">
                {savingMaterial ? 'Creating…' : 'Create Material'}
              </button>
              <button type="button" onClick={() => setShowAddMaterial(false)} className="px-5 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50">
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}
    </>
  )
}

function LoadingField({ label }) {
  return (
    <div className="flex items-center gap-2 text-sm text-gray-400 py-2">
      <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/>
      </svg>
      {label}
    </div>
  )
}
