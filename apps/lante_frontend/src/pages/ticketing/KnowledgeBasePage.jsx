import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { listKb, getKbArticle, createKbArticle, updateKbArticle, deleteKbArticle } from '../../services/ticketing.js'
import { useAuth } from '../../context/AuthContext.jsx'

const EMPTY = { title: '', category: '', problem: '', resolutionSteps: '', keywords: '', isPublished: true }

export default function KnowledgeBasePage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canManage = hasPermission ? hasPermission('tickets.write') : true

  // Detail view when an :id is present.
  const [article, setArticle] = useState(null)
  const [detailLoading, setDetailLoading] = useState(false)

  // List view.
  const [articles, setArticles] = useState([])
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(false)
  const [form, setForm] = useState(null)   // null = form hidden; object = create/edit
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const loadList = useCallback(async () => {
    setLoading(true)
    try {
      setArticles(await listKb() ?? [])
    } catch { setArticles([]) } finally { setLoading(false) }
  }, [])

  useEffect(() => {
    if (id) {
      setDetailLoading(true)
      getKbArticle(id)
        .then(a => setArticle(a ?? null))
        .catch(() => setArticle(null))
        .finally(() => setDetailLoading(false))
    } else {
      loadList()
    }
  }, [id, loadList])

  const filtered = query.trim()
    ? articles.filter(a => `${a.title} ${a.category} ${a.problem} ${a.keywords ?? ''}`.toLowerCase().includes(query.toLowerCase()))
    : articles

  async function save() {
    setError('')
    if (!form.title.trim() || !form.problem.trim() || !form.resolutionSteps.trim()) {
      setError('Title, problem and resolution steps are required.'); return
    }
    setSaving(true)
    try {
      if (form.id) await updateKbArticle(form.id, form)
      else await createKbArticle(form)
      setForm(null)
      loadList()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to save the article.')
    } finally { setSaving(false) }
  }

  async function remove(articleId) {
    if (!window.confirm('Delete this article?')) return
    try { await deleteKbArticle(articleId); loadList() } catch { /* ignore */ }
  }

  // ── Detail view ────────────────────────────────────────────────────────────
  if (id) {
    return (
      <>
        <main className="flex-1 max-w-3xl mx-auto w-full px-4 sm:px-6 py-8">
          <button onClick={() => navigate('/modules/ticketing/kb')} className="text-sm text-gray-500 hover:text-gray-700 mb-4">← Back to Knowledge Base</button>
          {detailLoading ? (
            <div className="h-40 bg-white rounded-xl border border-gray-100 animate-pulse" />
          ) : !article ? (
            <div className="bg-white rounded-xl border border-gray-200 p-8 text-center text-gray-500">Article not found.</div>
          ) : (
            <article className="bg-white rounded-xl border border-gray-200 p-6">
              <p className="text-xs font-semibold text-amber-600 uppercase tracking-wider">{article.category}</p>
              <h1 className="text-xl font-extrabold text-zinc-950 mt-1">{article.title}</h1>
              <p className="text-xs text-gray-400 mt-1">👁 {article.viewCount} views</p>
              <div className="mt-5">
                <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-1">Problem</p>
                <p className="text-sm text-gray-700 whitespace-pre-wrap">{article.problem}</p>
              </div>
              <div className="mt-5">
                <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-1">Resolution Steps</p>
                <p className="text-sm text-gray-700 whitespace-pre-wrap">{article.resolutionSteps}</p>
              </div>
              {article.keywords && <p className="mt-5 text-xs text-gray-400">Keywords: {article.keywords}</p>}
            </article>
          )}
        </main>
      </>
    )
  }

  // ── List / management view ─────────────────────────────────────────────────
  return (
    <>
      <main className="flex-1 max-w-4xl mx-auto w-full px-4 sm:px-6 py-8">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-xl font-extrabold text-zinc-950">Knowledge Base</h1>
            <p className="text-sm text-gray-500">Self-help articles that deflect repeat tickets</p>
          </div>
          {canManage && (
            <button onClick={() => { setForm({ ...EMPTY }); setError('') }}
              className="px-4 py-2 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-lg">+ New Article</button>
          )}
        </div>

        <input type="text" value={query} onChange={e => setQuery(e.target.value)}
          placeholder="Search articles…"
          className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm mb-5 focus:outline-none focus:ring-2 focus:ring-amber-400" />

        {form && (
          <div className="bg-white rounded-xl border border-amber-200 p-5 mb-5 space-y-3">
            <p className="text-sm font-bold text-navy">{form.id ? 'Edit Article' : 'New Article'}</p>
            {error && <p className="text-xs text-red-600">{error}</p>}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <input placeholder="Title *" value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))}
                className="px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              <input placeholder="Category" value={form.category} onChange={e => setForm(f => ({ ...f, category: e.target.value }))}
                className="px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
            </div>
            <textarea placeholder="Problem *" rows={2} value={form.problem} onChange={e => setForm(f => ({ ...f, problem: e.target.value }))}
              className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm resize-none focus:outline-none focus:ring-2 focus:ring-amber-400" />
            <textarea placeholder="Resolution steps *" rows={4} value={form.resolutionSteps} onChange={e => setForm(f => ({ ...f, resolutionSteps: e.target.value }))}
              className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm resize-none focus:outline-none focus:ring-2 focus:ring-amber-400" />
            <input placeholder="Keywords (comma-separated)" value={form.keywords} onChange={e => setForm(f => ({ ...f, keywords: e.target.value }))}
              className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
            <div className="flex items-center gap-3">
              <button disabled={saving} onClick={save} className="px-4 py-2 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-sm font-semibold rounded-lg">{saving ? 'Saving…' : 'Save'}</button>
              <button onClick={() => setForm(null)} className="px-4 py-2 border border-gray-200 text-gray-600 text-sm font-medium rounded-lg hover:bg-gray-50">Cancel</button>
            </div>
          </div>
        )}

        {loading ? (
          <div className="space-y-3">{[1, 2, 3].map(i => <div key={i} className="h-16 bg-white rounded-xl border border-gray-100 animate-pulse" />)}</div>
        ) : filtered.length === 0 ? (
          <div className="bg-white rounded-2xl border border-dashed border-gray-300 py-16 text-center text-gray-400">No articles yet.</div>
        ) : (
          <div className="space-y-2">
            {filtered.map(a => (
              <div key={a.id} className="bg-white rounded-xl border border-gray-200 p-4 flex items-center justify-between gap-3">
                <button onClick={() => navigate(`/modules/ticketing/kb/${a.id}`)} className="min-w-0 text-left flex-1">
                  <p className="font-semibold text-navy truncate">{a.title}</p>
                  <p className="text-xs text-gray-500 truncate">{a.category} · 👁 {a.viewCount}{!a.isPublished && ' · Draft'}</p>
                </button>
                {canManage && (
                  <div className="flex items-center gap-2 shrink-0">
                    <button onClick={() => { setForm({ ...a }); setError('') }} className="text-xs text-gray-500 hover:text-navy">Edit</button>
                    <button onClick={() => remove(a.id)} className="text-xs text-red-500 hover:text-red-700">Delete</button>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </main>
    </>
  )
}
