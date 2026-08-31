import { useState, useEffect, useCallback } from 'react'
import { FileText, BarChart3, Tag } from 'lucide-react'
import FleetNav from './FleetNav.jsx'
import api from '../../api/axios.js'
import { exportToPdf, exportToExcel } from '../../utils/export.js'
import Collapsible from '../../components/Collapsible.jsx'
import Pagination from '../../components/Pagination.jsx'
const CATEGORY = { 0: 'Loaded', 1: 'Empty', 2: 'Maintenance', 3: 'Other' }
const EMPTY_TRIP = { 0: 'Not Allowed', 1: 'Allowed', 2: 'Required' }
const MATERIAL_REQ = { 0: 'None', 1: 'Optional', 2: 'Mandatory' }
const PAGE_SIZE = 20

const inputCls = 'w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold'
const selectCls = inputCls + ' bg-white'

const BLANK = { name: '', description: '', isActive: true, category: 0, emptyTripOption: 0, materialRequirement: 0 }

export default function TripTypesPage() {
  const [types, setTypes]       = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading]   = useState(true)
  const [error, setError]       = useState('')
  const [filter, setFilter]     = useState('all')   // 'all' | 'active' | 'inactive'
  const [page, setPage]         = useState(1)

  const [modal, setModal]   = useState(null)   // null | 'create' | { ...tripType }
  const [form, setForm]     = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  // Delete Modal
  const [showDeleteModal, setShowDeleteModal] = useState(false)
  const [typeToDelete, setTypeToDelete] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const params = {
        pageNumber: page,
        pageSize: PAGE_SIZE,
        ...(filter === 'active' ? { isActive: true } : filter === 'inactive' ? { isActive: false } : {}),
      }
      const res = await api.get('/api/v1/triptypes', { params })
      setTypes(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load trip types.')
    } finally {
      setLoading(false)
    }
  }, [page, filter])

  useEffect(() => { load() }, [load])

  function updateFilter(f) {
    setFilter(f)
    setPage(1)
  }

  // Export Functions — fetch the full unpaged list (today's list state now only
  // holds the current page) so export keeps exporting everything matching the filter.
  async function fetchAllForExport() {
    const params = filter === 'active' ? { isActive: true } : filter === 'inactive' ? { isActive: false } : {}
    const res = await api.get('/api/v1/triptypes', { params })
    return res.data?.data ?? []
  }

  const handleExportPdf = async () => {
    const rows = await fetchAllForExport()
    exportToPdf({
      title: "Trip Types",
      subtitle: `${rows.length} trip types • ${filter} filter`,
      columns: [
        { header: 'Name',          accessor: r => r.name },
        { header: 'Description',   accessor: r => r.description ?? '—' },
        { header: 'Category',      accessor: r => CATEGORY[r.category] ?? '—' },
        { header: 'Empty Trip',    accessor: r => EMPTY_TRIP[r.emptyTripOption] ?? '—' },
        { header: 'Material',      accessor: r => MATERIAL_REQ[r.materialRequirement] ?? '—' },
        { header: 'Status',        accessor: r => r.isActive ? 'Active' : 'Inactive' },
      ],
      rows,
      filename: `Trip-Types-${new Date().toISOString().slice(0,10)}`,
      theme: 'navy',
      docModule: 'FLEET',
    })
  }

  const handleExportExcel = async () => {
    const rows = await fetchAllForExport()
    exportToExcel({
      title: "Trip Types",
      columns: [
        { header: 'Name',          accessor: r => r.name, width: 25 },
        { header: 'Description',   accessor: r => r.description ?? '', width: 35 },
        { header: 'Category',      accessor: r => CATEGORY[r.category] ?? '', width: 18 },
        { header: 'Empty Trip',    accessor: r => EMPTY_TRIP[r.emptyTripOption] ?? '', width: 20 },
        { header: 'Material Req.', accessor: r => MATERIAL_REQ[r.materialRequirement] ?? '', width: 18 },
        { header: 'Status',        accessor: r => r.isActive ? 'Active' : 'Inactive', width: 12 },
      ],
      rows,
      filename: `Trip-Types-${new Date().toISOString().slice(0,10)}`,
      sheetName: "Trip Types"
    })
  }

  function openCreate() {
    setForm(BLANK)
    setFormErr('')
    setModal('create')
  }

  function openEdit(t) {
    setForm({
      name:                t.name,
      description:         t.description ?? '',
      isActive:            t.isActive,
      category:            t.category,
      emptyTripOption:     t.emptyTripOption,
      materialRequirement: t.materialRequirement,
    })
    setFormErr('')
    setModal(t)
  }

  function set(field, val) {
    setForm(f => ({ ...f, [field]: val }))
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
      const payload = {
        name:                form.name.trim(),
        description:         form.description.trim() || null,
        isActive:            form.isActive,
        category:            Number(form.category),
        emptyTripOption:     Number(form.emptyTripOption),
        materialRequirement: Number(form.materialRequirement),
      }
      if (modal === 'create') {
        await api.post('/api/v1/triptypes', payload)
      } else {
        await api.put(`/api/v1/triptypes/${modal.id}`, { ...payload, id: modal.id })
      }
      setModal(null)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to save trip type.')
    } finally {
      setSaving(false)
    }
  }

  function openDeleteModal(t) {
    setTypeToDelete(t)
    setShowDeleteModal(true)
  }

  async function confirmDelete() {
    if (!typeToDelete) return
    try {
      await api.delete(`/api/v1/triptypes/${typeToDelete.id}`)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete trip type.')
    } finally {
      setShowDeleteModal(false)
      setTypeToDelete(null)
    }
  }

  async function toggleActive(t) {
    try {
      await api.put(`/api/v1/triptypes/${t.id}`, {
        ...t,
        isActive: !t.isActive
      })
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to update status.')
    }
  }

  return (
      <>
        <main className="w-full px-4 sm:px-6 py-8">
          <FleetNav />

          <Collapsible title="About Trip Types" dismissKey="fleet.pageInfo.tripTypes.dismissed">
            <p className="text-sm text-gray-700">
              Trip types control what a trip requires — whether empty legs are allowed and whether a material must be logged.
            </p>
          </Collapsible>

          {/* Header */}
          <div className="flex items-center justify-between mb-6">
            <div>
              <h1 className="text-2xl font-extrabold text-navy">Trip Types</h1>
              <p className="text-sm text-gray-500 mt-0.5">
                {loading ? 'Loading…' : `${totalCount} type${totalCount !== 1 ? 's' : ''}`}
              </p>
            </div>

            <div className="flex items-center gap-3">
              <button
                  onClick={handleExportPdf}
                  disabled={totalCount === 0}
                  className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50"
              >
                <FileText size={14} /> PDF
              </button>

              <button
                  onClick={handleExportExcel}
                  disabled={totalCount === 0}
                  className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50"
              >
                <BarChart3 size={14} /> Excel
              </button>

              <button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl transition-colors shadow"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                New Type
              </button>
            </div>
          </div>

          {/* Filter */}
          <div className="flex gap-2 mb-6">
            {['all', 'active', 'inactive'].map(f => (
                <button
                    key={f}
                    onClick={() => updateFilter(f)}
                    className={`px-5 py-2 rounded-full text-sm font-semibold capitalize transition-colors ${
                        filter === f
                            ? 'bg-navy text-white'
                            : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                    }`}
                >
                  {f}
                </button>
            ))}
          </div>

          {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-6">
                {error}
              </div>
          )}

          {loading ? (
              <div className="space-y-3">
                {[1,2,3].map(i => <div key={i} className="h-16 bg-white rounded-2xl border animate-pulse" />)}
              </div>
          ) : types.length === 0 ? (
              <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
                <div className="flex justify-center mb-3 text-gray-400"><Tag size={40} /></div>
                <h3 className="font-semibold text-gray-700">No trip types found</h3>
                <p className="text-sm text-gray-400 mt-1">Create one to get started.</p>
              </div>
          ) : (
              <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                  <tr className="bg-gray-50 border-b border-gray-100">
                    <th className="text-left px-6 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider">Name</th>
                    <th className="text-left px-4 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider">Description</th>
                    <th className="text-left px-4 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider">Category</th>
                    <th className="text-left px-4 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider">Empty Trip</th>
                    <th className="text-left px-4 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider">Material</th>
                    <th className="text-center px-4 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider">Status</th>
                    <th className="px-6 py-4 text-xs font-semibold text-gray-600 uppercase tracking-wider text-right">Actions</th>
                  </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                  {types.map(t => (
                      <tr key={t.id} className="hover:bg-gray-50 transition-colors">
                        <td className="px-6 py-4 font-semibold text-gray-900">{t.name}</td>
                        <td className="px-4 py-4 text-gray-500 text-sm">
                          {t.description || '—'}
                        </td>
                        <td className="px-4 py-4">
                          <Chip label={CATEGORY[t.category] ?? '—'} />
                        </td>
                        <td className="px-4 py-4">
                          <Chip label={EMPTY_TRIP[t.emptyTripOption] ?? '—'} />
                        </td>
                        <td className="px-4 py-4">
                          <Chip label={MATERIAL_REQ[t.materialRequirement] ?? '—'} />
                        </td>
                        <td className="px-4 py-4 text-center">
                      <span className={`inline-flex px-3 py-1 rounded-full text-xs font-semibold border ${
                          t.isActive
                              ? 'bg-green-50 text-green-700 border-green-200'
                              : 'bg-gray-100 text-gray-500 border-gray-200'
                      }`}>
                        {t.isActive ? 'Active' : 'Inactive'}
                      </span>
                        </td>
                        <td className="px-6 py-4 text-right">
                          <div className="flex items-center justify-end gap-2">
                            <button
                                onClick={() => openEdit(t)}
                                className="text-xs px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 font-medium"
                            >
                              Edit
                            </button>
                            <button
                                onClick={() => toggleActive(t)}
                                className={`text-xs px-3 py-1.5 rounded-lg font-medium transition-colors ${
                                    t.isActive
                                        ? 'border border-navy/30 text-navy hover:bg-navy/5'
                                        : 'border border-green-200 text-green-700 hover:bg-green-50'
                                }`}
                            >
                              {t.isActive ? 'Deactivate' : 'Activate'}
                            </button>
                            <button
                                onClick={() => openDeleteModal(t)}
                                className="text-xs px-3 py-1.5 border border-red-200 text-red-600 hover:bg-red-50 rounded-lg font-medium transition-colors"
                            >
                              Delete
                            </button>
                          </div>
                        </td>
                      </tr>
                  ))}
                  </tbody>
                </table>
                <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
              </div>
          )}
        </main>

        {/* Create / Edit Modal */}
        {modal && (
            <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
              <form onSubmit={handleSave} className="bg-white rounded-2xl shadow-2xl p-6 w-full max-w-lg space-y-4">
                <h3 className="font-bold text-gray-900 text-lg">
                  {modal === 'create' ? 'New Trip Type' : `Edit — ${modal.name}`}
                </h3>

                {formErr && (
                    <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2">
                      {formErr}
                    </div>
                )}

                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Name *</label>
                  <input
                      value={form.name}
                      onChange={e => set('name', e.target.value)}
                      placeholder="e.g. Delivery Run"
                      className={inputCls}
                      required
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Description</label>
                  <textarea
                      value={form.description}
                      onChange={e => set('description', e.target.value)}
                      rows={2}
                      placeholder="Optional description…"
                      className={inputCls + ' resize-none'}
                  />
                </div>

                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Category</label>
                    <select value={form.category} onChange={e => set('category', e.target.value)} className={selectCls}>
                      {Object.entries(CATEGORY).map(([v, l]) => (
                          <option key={v} value={v}>{l}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Empty Trip</label>
                    <select value={form.emptyTripOption} onChange={e => set('emptyTripOption', e.target.value)} className={selectCls}>
                      {Object.entries(EMPTY_TRIP).map(([v, l]) => (
                          <option key={v} value={v}>{l}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Material</label>
                    <select value={form.materialRequirement} onChange={e => set('materialRequirement', e.target.value)} className={selectCls}>
                      {Object.entries(MATERIAL_REQ).map(([v, l]) => (
                          <option key={v} value={v}>{l}</option>
                      ))}
                    </select>
                  </div>
                </div>

                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                      type="checkbox"
                      checked={form.isActive}
                      onChange={e => set('isActive', e.target.checked)}
                      className="w-4 h-4 accent-navy"
                  />
                  <span className="text-sm font-medium text-gray-700">Active</span>
                </label>

                <div className="flex gap-3 pt-2">
                  <button
                      type="submit"
                      disabled={saving}
                      className="flex-1 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-colors"
                  >
                    {saving ? 'Saving…' : modal === 'create' ? 'Create Type' : 'Save Changes'}
                  </button>
                  <button
                      type="button"
                      onClick={() => setModal(null)}
                      className="px-6 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                </div>
              </form>
            </div>
        )}

        {/* Delete Confirmation Modal */}
        {showDeleteModal && typeToDelete && (
            <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
              <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-5">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-red-100 flex items-center justify-center flex-shrink-0">
                    <svg className="w-5 h-5 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-gray-900">Delete Trip Type</h3>
                    <p className="text-xs text-gray-500 mt-0.5">This action cannot be undone.</p>
                  </div>
                </div>

                <p className="text-sm text-gray-600">
                  Are you sure you want to permanently delete the trip type{' '}
                  <span className="font-semibold text-gray-900">"{typeToDelete.name}"</span>?
                </p>

                <div className="flex gap-3 pt-2">
                  <button
                      onClick={() => {
                        setShowDeleteModal(false)
                        setTypeToDelete(null)
                      }}
                      className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                      onClick={confirmDelete}
                      className="flex-1 py-2.5 bg-red-600 hover:bg-red-700 text-white text-sm font-bold rounded-xl transition-colors"
                  >
                    Yes, Delete
                  </button>
                </div>
              </div>
            </div>
        )}
      </>
  )
}

function Chip({ label }) {
  return (
      <span className="inline-flex items-center px-2.5 py-1 bg-gray-100 text-gray-600 text-xs font-medium rounded-lg">
      {label}
    </span>
  )
}