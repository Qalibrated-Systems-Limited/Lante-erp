import { useState, useEffect, useCallback } from 'react'
import { Car, CheckCircle2, X, Clock, Plus, Search } from 'lucide-react'
import FleetNav from './FleetNav.jsx'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'

function currentUserName(user) {
  return [user?.firstName, user?.lastName].filter(Boolean).join(' ') || user?.email || 'Unknown user'
}

function fmtDate(d) {
  return d ? new Date(d).toLocaleString('en-KE', { dateStyle: 'medium', timeStyle: 'short' }) : '—'
}

// A fleet manager's approvals queue — every pending field-vehicle dispatch request across all
// Operations assignments, not scoped to one. Without this page, a request made from the
// Operations assignment page had no surface a fleet manager could ever see it on: that page is
// gated on operations.read.own, which Fleet Manager/Staff don't hold, so they had no route to it.
const EMPTY_FORM = { assignmentId: '', fieldVehicleId: '', driverName: '', departureDatetime: '', fuelLevelOut: 'Full', notes: '' }

export default function FleetDispatchRequestsPage() {
  const { user, hasPermission } = useAuth()
  const canApprove = hasPermission('fleet.write')
  // fleet.dispatch.request is what actually gates POSTing a dispatch — separate from fleet.write,
  // which only gates approve/reject. Fleet Manager holds both; check the real one for this button.
  const canRequest = hasPermission('fleet.dispatch.request')

  const [requests, setRequests] = useState([])
  const [loading, setLoading] = useState(true)
  const [actingId, setActingId] = useState(null)
  const [toast, setToast] = useState(null)

  const [showNewModal, setShowNewModal] = useState(false)
  const [newForm, setNewForm] = useState(EMPTY_FORM)
  const [saving, setSaving] = useState(false)
  const [vehicles, setVehicles] = useState([])
  const [assignmentQuery, setAssignmentQuery] = useState('')
  const [assignmentResults, setAssignmentResults] = useState([])
  const [searchingAssignments, setSearchingAssignments] = useState(false)

  const showToast = useCallback((type, message) => {
    setToast({ type, message })
    setTimeout(() => setToast(null), 4000)
  }, [])

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true)
    try {
      const res = await api.get('/api/v1/fleet/field-vehicles/dispatches/pending')
      setRequests(res.data?.data ?? [])
    } catch {
      if (!silent) setRequests([])
    } finally {
      if (!silent) setLoading(false)
    }
  }, [])

  // Poll so this queue reflects a request someone else just approved/rejected (or a new one that
  // just came in) without a manual refresh — this is a shared queue, and acting on a request
  // another manager already handled would otherwise 400 with a confusing error. Skipped while an
  // approve/reject is in flight so it can't race that action's own optimistic list update.
  useEffect(() => {
    load()
    const id = setInterval(() => { if (!actingId) load(true) }, 30_000)
    return () => clearInterval(id)
  }, [load, actingId])

  async function handleApprove(id) {
    setActingId(id)
    try {
      await api.post(`/api/v1/fleet/field-vehicles/dispatches/${id}/approve`, { approvedBy: currentUserName(user) })
      setRequests(reqs => reqs.filter(r => r.id !== id))
      showToast('success', 'Dispatch approved — the vehicle is now marked in use.')
    } catch (err) {
      showToast('error', err?.response?.data?.message ?? 'Failed to approve dispatch request.')
    } finally {
      setActingId(null)
    }
  }

  async function handleReject(id) {
    setActingId(id)
    try {
      await api.post(`/api/v1/fleet/field-vehicles/dispatches/${id}/reject`)
      setRequests(reqs => reqs.filter(r => r.id !== id))
      showToast('success', 'Dispatch request rejected.')
    } catch (err) {
      showToast('error', err?.response?.data?.message ?? 'Failed to reject dispatch request.')
    } finally {
      setActingId(null)
    }
  }

  function openNewModal() {
    setNewForm(EMPTY_FORM)
    setAssignmentQuery('')
    setAssignmentResults([])
    setShowNewModal(true)
    api.get('/api/v1/fleet/field-vehicles', { params: { pageSize: 100, status: 'Available', excludeOpenDispatch: true } })
      .then(res => setVehicles(res.data?.data ?? []))
      .catch(() => setVehicles([]))
  }

  // Debounced — an external driver's assignment could be one of hundreds; search rather than
  // load every assignment company-wide up front.
  useEffect(() => {
    if (!showNewModal || assignmentQuery.trim().length < 2) { setAssignmentResults([]); return }
    setSearchingAssignments(true)
    const t = setTimeout(() => {
      api.get('/api/v1/assignments/search-lite', { params: { search: assignmentQuery, pageSize: 10 } })
        .then(res => setAssignmentResults(res.data?.data?.items ?? []))
        .catch(() => setAssignmentResults([]))
        .finally(() => setSearchingAssignments(false))
    }, 350)
    return () => clearTimeout(t)
  }, [assignmentQuery, showNewModal])

  async function handleCreateRequest(e) {
    e.preventDefault()
    if (!newForm.assignmentId || !newForm.fieldVehicleId || !newForm.driverName || !newForm.departureDatetime) return
    setSaving(true)
    try {
      await api.post('/api/v1/fleet/field-vehicles/dispatches', {
        assignmentId: newForm.assignmentId,
        fieldVehicleId: newForm.fieldVehicleId,
        driverName: newForm.driverName,
        departureDatetime: new Date(newForm.departureDatetime).toISOString(),
        fuelLevelOut: newForm.fuelLevelOut,
        notes: newForm.notes || null,
      })
      setShowNewModal(false)
      showToast('success', 'Vehicle request created.')
      load()
    } catch (err) {
      showToast('error', err?.response?.data?.message ?? 'Failed to create the request.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <main className="w-full px-4 sm:px-6 py-8">
        <FleetNav />

        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Vehicle Requests &amp; Approvals</h1>
            <p className="text-sm text-gray-500 mt-0.5">Field-vehicle dispatch requests from Operations assignments, waiting on a fleet decision.</p>
          </div>
          {canRequest && (
            <button
              onClick={openNewModal}
              className="inline-flex items-center gap-1.5 px-4 py-2.5 rounded-xl bg-gray-900 hover:bg-gray-800 text-white text-sm font-semibold self-start sm:self-auto"
            >
              <Plus size={16} /> Request Vehicle
            </button>
          )}
        </div>

        <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden">
          {loading ? (
            <div className="flex items-center justify-center py-20 text-gray-400 text-sm">Loading…</div>
          ) : requests.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-20 text-gray-400">
              <div className="flex justify-center mb-3 text-gray-400"><CheckCircle2 size={40} /></div>
              <p className="font-medium">No pending requests</p>
              <p className="text-sm mt-1">New field-vehicle dispatch requests will show up here.</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-100">
              {requests.map(r => (
                <div key={r.id} className="flex flex-col sm:flex-row sm:items-center gap-4 px-5 py-4">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <Car size={16} className="text-gray-400 flex-shrink-0" />
                      <p className="text-sm font-semibold text-gray-900 truncate">
                        {r.vehicleRegistration} — {r.vehicleMake} {r.vehicleModel}
                      </p>
                    </div>
                    <p className="text-xs text-gray-500 mt-1">
                      Requested by <span className="font-medium text-gray-700">{r.driverName}</span> for assignment <span className="font-mono">{r.assignmentId}</span>
                    </p>
                    <p className="text-xs text-gray-400 mt-0.5 flex items-center gap-1">
                      <Clock size={12} /> Departure {fmtDate(r.departureDatetime)} · Odometer {Number(r.departureOdometer).toLocaleString()} km
                    </p>
                    {r.notes && <p className="text-xs text-gray-400 mt-0.5 italic">"{r.notes}"</p>}
                  </div>
                  {canApprove ? (
                    <div className="flex items-center gap-2 flex-shrink-0">
                      <button
                        onClick={() => handleReject(r.id)}
                        disabled={actingId === r.id}
                        className="inline-flex items-center gap-1.5 px-3 py-2 rounded-xl border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold disabled:opacity-50"
                      >
                        <X size={14} /> Reject
                      </button>
                      <button
                        onClick={() => handleApprove(r.id)}
                        disabled={actingId === r.id}
                        className="inline-flex items-center gap-1.5 px-3 py-2 rounded-xl bg-amber-400 hover:bg-amber-500 text-black text-sm font-semibold disabled:opacity-50"
                      >
                        <CheckCircle2 size={14} /> Approve
                      </button>
                    </div>
                  ) : (
                    <span className="flex-shrink-0 text-xs font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700">Awaiting fleet approval</span>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </main>

      {showNewModal && (
        <div className="fixed inset-0 z-[400] bg-black/40 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
              <h2 className="font-bold text-gray-900">Request Vehicle</h2>
              <button onClick={() => setShowNewModal(false)} className="text-gray-400 hover:text-gray-600"><X size={20} /></button>
            </div>
            <form onSubmit={handleCreateRequest} className="p-5 flex flex-col gap-3.5">
              <p className="text-xs text-gray-500 -mt-1">Use this to submit a request on behalf of a driver who can't log in themselves (e.g. an external/contract driver) — the vehicle isn't marked in use until this is approved.</p>

              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Assignment *</label>
                <div className="relative">
                  <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                  {newForm.assignmentId && <CheckCircle2 size={14} className="absolute right-3 top-1/2 -translate-y-1/2 text-green-500" />}
                  <input
                    value={assignmentQuery}
                    onChange={e => { setAssignmentQuery(e.target.value); setNewForm(f => ({ ...f, assignmentId: '' })) }}
                    placeholder="Search by assignment title or location…"
                    className="input pl-8 pr-8"
                  />
                  {assignmentQuery.trim().length >= 2 && !newForm.assignmentId && (
                    <div className="absolute z-10 left-0 right-0 mt-1 bg-white border border-gray-100 rounded-lg shadow-md overflow-hidden max-h-40 overflow-y-auto">
                      {searchingAssignments ? (
                        <p className="text-xs text-gray-400 px-3 py-2">Searching…</p>
                      ) : assignmentResults.length === 0 ? (
                        <p className="text-xs text-gray-400 px-3 py-2">No matching assignments.</p>
                      ) : assignmentResults.map(a => (
                        <button
                          type="button"
                          key={a.id}
                          onClick={() => { setNewForm(f => ({ ...f, assignmentId: a.id })); setAssignmentQuery(a.title) }}
                          className="w-full text-left px-3 py-2 text-sm hover:bg-gray-50 border-b border-gray-50 last:border-0"
                        >
                          {a.title}{a.locationName ? <span className="text-gray-400"> — {a.locationName}</span> : null}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>

              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Vehicle *</label>
                <select required value={newForm.fieldVehicleId} onChange={e => setNewForm(f => ({ ...f, fieldVehicleId: e.target.value }))} className="input">
                  <option value="">Select vehicle…</option>
                  {vehicles.map(v => (
                    <option key={v.id} value={v.id}>{v.registrationNumber} — {v.make} {v.model} ({v.type})</option>
                  ))}
                </select>
                {vehicles.length === 0 && <p className="text-xs text-amber-600 mt-1">No available vehicles right now.</p>}
              </div>

              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Driver Name *</label>
                <input required value={newForm.driverName} onChange={e => setNewForm(f => ({ ...f, driverName: e.target.value }))} className="input" placeholder="Name of the driver" />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-xs font-semibold text-gray-600 block mb-1">Departure Date *</label>
                  <input required type="date" value={newForm.departureDatetime} onChange={e => setNewForm(f => ({ ...f, departureDatetime: e.target.value }))} className="input" />
                </div>
                <div>
                  <label className="text-xs font-semibold text-gray-600 block mb-1">Fuel Level Out</label>
                  <select value={newForm.fuelLevelOut} onChange={e => setNewForm(f => ({ ...f, fuelLevelOut: e.target.value }))} className="input">
                    {['Full', '3/4', '1/2', '1/4', 'Empty'].map(l => <option key={l}>{l}</option>)}
                  </select>
                </div>
              </div>

              <div>
                <label className="text-xs font-semibold text-gray-600 block mb-1">Notes</label>
                <textarea rows={2} value={newForm.notes} onChange={e => setNewForm(f => ({ ...f, notes: e.target.value }))} className="input resize-none" />
              </div>

              <div className="flex gap-2.5 justify-end mt-1">
                <button type="button" onClick={() => setShowNewModal(false)} className="px-4 py-2 rounded-lg border border-gray-200 text-sm text-gray-700 hover:bg-gray-50">Cancel</button>
                <button type="submit" disabled={saving} className="px-5 py-2 rounded-lg bg-gray-900 hover:bg-gray-800 text-white text-sm font-semibold disabled:opacity-70">
                  {saving ? 'Submitting…' : 'Submit Request'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {toast && (
        <div className={`fixed bottom-6 right-6 z-[500] flex items-center gap-3 px-5 py-3 rounded-xl shadow-lg text-sm font-medium transition-all ${
          toast.type === 'success' ? 'bg-green-500 text-white' : 'bg-red-500 text-white'
        }`}>
          {toast.type === 'success' ? <CheckCircle2 size={18} /> : <X size={18} />}
          {toast.message}
        </div>
      )}
    </>
  )
}
