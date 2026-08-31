import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { listMacros, createMacro, updateMacro, deleteMacro, getCategories } from '../../services/ticketing.js'
import api from '../../api/axios.js'

const EMPTY_FORM = { name: '', description: '', content: '', isGlobal: true, categoryId: '' }

export default function MacrosPage() {
  const navigate = useNavigate()
  const [macros, setMacros] = useState([])
  const [categories, setCategories] = useState([])
  const [loading, setLoading] = useState(true)
  const [modal, setModal] = useState(null) // null | 'create' | { ...macro }
  const [form, setForm] = useState(EMPTY_FORM)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  function set(field, value) { setForm(f => ({ ...f, [field]: value })) }

  async function load() {
    setLoading(true)
    try {
      const [macros, categories] = await Promise.all([listMacros(), getCategories()])
      setMacros(macros ?? [])
      setCategories(categories ?? [])
    } catch { setError('Failed to load macros.') }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [])

  function openCreate() { setForm(EMPTY_FORM); setModal('create'); setError('') }
  function openEdit(macro) { setForm({ name: macro.name, description: macro.description ?? '', content: macro.content, isGlobal: macro.isGlobal, categoryId: macro.categoryId ?? '' }); setModal(macro); setError('') }

  async function handleSave(e) {
    e.preventDefault()
    if (!form.name.trim() || !form.content.trim()) { setError('Name and content are required.'); return }
    setSaving(true); setError('')
    try {
      const payload = { ...form, isGlobal: Boolean(form.isGlobal), categoryId: form.categoryId || null }
      if (modal === 'create') {
        await createMacro(payload)
      } else {
        await updateMacro(modal.id, payload)
      }
      setModal(null)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to save.')
    } finally { setSaving(false) }
  }

  async function handleDelete(id) {
    if (!window.confirm('Delete this macro?')) return
    try {
      await deleteMacro(id)
      setMacros(m => m.filter(x => x.id !== id))
    } catch { setError('Failed to delete.') }
  }

  return (
    <>
      <main className="flex-1 max-w-5xl mx-auto w-full px-4 sm:px-6 py-8">
        <div className="flex items-center gap-3 mb-6">
          <button onClick={() => navigate('/modules/ticketing')}
            className="p-2 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <div className="flex-1">
            <h1 className="text-xl font-extrabold text-zinc-950">Macros</h1>
            <p className="text-sm text-gray-500">Predefined responses agents can apply to tickets</p>
          </div>
          <button onClick={openCreate}
            className="inline-flex items-center gap-2 px-4 py-2.5 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-lg transition-colors">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            New Macro
          </button>
        </div>

        {error && !modal && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-4 py-3 text-sm mb-4">{error}</div>
        )}

        {loading ? (
          <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-20 bg-white rounded-xl border border-gray-100 animate-pulse" />)}</div>
        ) : macros.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-16">
            <div className="text-3xl mb-2">📝</div>
            <p className="font-medium text-gray-600">No macros yet</p>
            <p className="text-sm text-gray-400 mt-1">Create macros to speed up agent responses</p>
          </div>
        ) : (
          <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="text-left px-5 py-3 font-semibold text-gray-500 text-xs uppercase tracking-wider">Name</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-500 text-xs uppercase tracking-wider hidden md:table-cell">Preview</th>
                  <th className="text-left px-4 py-3 font-semibold text-gray-500 text-xs uppercase tracking-wider hidden sm:table-cell">Scope</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {macros.map(m => (
                  <tr key={m.id} className="hover:bg-gray-50">
                    <td className="px-5 py-4">
                      <div className="font-medium text-gray-800">{m.name}</div>
                      {m.description && <div className="text-xs text-gray-400 mt-0.5">{m.description}</div>}
                    </td>
                    <td className="px-4 py-4 text-gray-500 hidden md:table-cell">
                      <span className="line-clamp-2 text-xs">{m.content}</span>
                    </td>
                    <td className="px-4 py-4 hidden sm:table-cell">
                      {m.isGlobal
                        ? <span className="px-2 py-0.5 bg-green-50 text-green-700 text-xs font-medium rounded-full">Global</span>
                        : <span className="px-2 py-0.5 bg-blue-50 text-blue-700 text-xs font-medium rounded-full">Category</span>
                      }
                    </td>
                    <td className="px-4 py-4 text-right">
                      <div className="flex justify-end gap-1">
                        <button onClick={() => openEdit(m)}
                          className="p-1.5 text-gray-300 hover:text-amber-500 rounded transition-colors">
                          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                          </svg>
                        </button>
                        <button onClick={() => handleDelete(m.id)}
                          className="p-1.5 text-gray-300 hover:text-red-500 rounded transition-colors">
                          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                          </svg>
                        </button>
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
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg p-6">
            <h2 className="text-lg font-bold text-zinc-950 mb-4">
              {modal === 'create' ? 'New Macro' : 'Edit Macro'}
            </h2>
            {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3 mb-4">{error}</div>
            )}
            <form onSubmit={handleSave} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Name *</label>
                <input type="text" value={form.name} onChange={e => set('name', e.target.value)}
                  placeholder="e.g. Standard Greeting"
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Description</label>
                <input type="text" value={form.description} onChange={e => set('description', e.target.value)}
                  placeholder="When to use this macro…"
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Content *</label>
                <textarea value={form.content} onChange={e => set('content', e.target.value)}
                  rows={5} placeholder="The response text…"
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 resize-none" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Scope</label>
                  <select value={form.isGlobal ? '1' : '0'} onChange={e => set('isGlobal', e.target.value === '1')}
                    className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400">
                    <option value="1">Global (all tickets)</option>
                    <option value="0">Category specific</option>
                  </select>
                </div>
                {!form.isGlobal && (
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Category</label>
                    <select value={form.categoryId} onChange={e => set('categoryId', e.target.value)}
                      className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400">
                      <option value="">Select…</option>
                      {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                    </select>
                  </div>
                )}
              </div>
              <div className="flex items-center justify-end gap-3 pt-2">
                <button type="button" onClick={() => setModal(null)}
                  className="px-4 py-2 text-sm border border-gray-200 rounded-lg text-gray-600 hover:bg-gray-50">
                  Cancel
                </button>
                <button type="submit" disabled={saving}
                  className="px-5 py-2 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-sm font-semibold rounded-lg">
                  {saving ? 'Saving…' : 'Save Macro'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  )
}
