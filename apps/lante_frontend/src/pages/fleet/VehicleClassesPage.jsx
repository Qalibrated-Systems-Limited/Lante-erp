import { useState, useEffect, useCallback } from 'react'
import { Truck } from 'lucide-react'
import FleetNav from './FleetNav.jsx'
import api from '../../api/axios.js'
import Collapsible from '../../components/Collapsible.jsx'

const inputCls = 'w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold'
const BLANK = { name: '', description: '' }

export default function VehicleClassesPage() {
  const [vehicleClasses, setVehicleClasses] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [modal, setModal] = useState(null)   // null | 'create' | { ...vehicleClass }
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  const [showDeleteModal, setShowDeleteModal] = useState(false)
  const [classToDelete, setClassToDelete] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/vehicleclasses')
      setVehicleClasses(res.data?.data ?? [])
    } catch {
      setError('Failed to load vehicle classes.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  function openCreate() {
    setForm(BLANK)
    setFormErr('')
    setModal('create')
  }

  function openEdit(vc) {
    setForm({ name: vc.name, description: vc.description ?? '' })
    setFormErr('')
    setModal(vc)
  }

  async function handleSave(e) {
    e.preventDefault()
    setFormErr('')
    if (!form.name.trim()) {
      setFormErr('Name is required.')
      return
    }
    setSaving(true)
    try {
      const payload = { name: form.name.trim(), description: form.description.trim() || null }
      if (modal === 'create') {
        await api.post('/api/v1/vehicleclasses', payload)
      } else {
        await api.put(`/api/v1/vehicleclasses/${modal.id}`, payload)
      }
      setModal(null)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to save vehicle class.')
    } finally {
      setSaving(false)
    }
  }

  function openDeleteModal(vc) {
    setClassToDelete(vc)
    setShowDeleteModal(true)
  }

  async function confirmDelete() {
    if (!classToDelete) return
    try {
      await api.delete(`/api/v1/vehicleclasses/${classToDelete.id}`)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete vehicle class.')
    } finally {
      setShowDeleteModal(false)
      setClassToDelete(null)
    }
  }

  return (
    <>
      <main className="w-full px-4 sm:px-6 py-8">
        <FleetNav />

        <Collapsible title="About Vehicle Classes" dismissKey="fleet.pageInfo.vehicleClasses.dismissed">
          <p className="text-sm text-gray-700">
            The vehicle class options offered when registering a truck (e.g. Truck, Pickup, Van).
          </p>
        </Collapsible>

        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-extrabold text-navy">Vehicle Classes</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {loading ? 'Loading…' : `${vehicleClasses.length} class${vehicleClasses.length !== 1 ? 'es' : ''}`}
            </p>
          </div>

          <button
            onClick={openCreate}
            className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl transition-colors shadow"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            New Class
          </button>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-6">
            {error}
          </div>
        )}

        {loading ? (
          <div className="space-y-3">
            {[1, 2, 3].map(i => <div key={i} className="h-16 bg-white rounded-2xl border animate-pulse" />)}
          </div>
        ) : vehicleClasses.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
            <div className="flex justify-center mb-3 text-gray-400"><Truck size={40} /></div>
            <h3 className="font-semibold text-gray-700">No vehicle classes yet</h3>
            <p className="text-sm text-gray-400 mt-1">Create one to get started.</p>
          </div>
        ) : (
          <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="text-left px-6 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider">Class Name</th>
                  <th className="text-left px-6 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider">Description</th>
                  <th className="px-6 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {vehicleClasses.map(vc => (
                  <tr key={vc.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-6 py-4 font-semibold text-gray-900">{vc.name}</td>
                    <td className="px-6 py-4 text-gray-500">{vc.description || '—'}</td>
                    <td className="px-6 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button onClick={() => openEdit(vc)} className="text-xs px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 font-medium">Edit</button>
                        <button onClick={() => openDeleteModal(vc)} className="text-xs px-3 py-1.5 border border-red-200 text-red-600 hover:bg-red-50 rounded-lg font-medium">Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </main>

      {modal && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <form onSubmit={handleSave} className="bg-white rounded-2xl shadow-2xl p-6 w-full max-w-md space-y-4">
            <h3 className="font-bold text-gray-900 text-lg">
              {modal === 'create' ? 'New Vehicle Class' : `Edit — ${modal.name}`}
            </h3>

            {formErr && (
              <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2">{formErr}</div>
            )}

            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">Name *</label>
              <input
                value={form.name}
                onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Flatbed Truck"
                className={inputCls}
                required
                autoFocus
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">Description</label>
              <textarea
                value={form.description}
                onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                rows={3}
                placeholder="Optional description…"
                className={inputCls + ' resize-none'}
              />
            </div>

            <div className="flex gap-3 pt-2">
              <button type="submit" disabled={saving} className="flex-1 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-colors">
                {saving ? 'Saving…' : modal === 'create' ? 'Create Class' : 'Save Changes'}
              </button>
              <button type="button" onClick={() => setModal(null)} className="px-5 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50">
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      {showDeleteModal && classToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-5">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-red-100 flex items-center justify-center flex-shrink-0">
                <svg className="w-5 h-5 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
              </div>
              <div>
                <h3 className="text-base font-bold text-gray-900">Delete Vehicle Class</h3>
                <p className="text-xs text-gray-500 mt-0.5">This action cannot be undone.</p>
              </div>
            </div>

            <p className="text-sm text-gray-600">
              Are you sure you want to permanently delete the vehicle class{' '}
              <span className="font-semibold text-gray-900">"{classToDelete.name}"</span>?
            </p>

            <div className="flex gap-3 pt-2">
              <button onClick={() => { setShowDeleteModal(false); setClassToDelete(null) }} className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors">
                Cancel
              </button>
              <button onClick={confirmDelete} className="flex-1 py-2.5 bg-red-600 hover:bg-red-700 text-white text-sm font-bold rounded-xl transition-colors">
                Yes, Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
