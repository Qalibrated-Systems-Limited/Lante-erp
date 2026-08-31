import { useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import usePortalTenant from '../../hooks/usePortalTenant'

const API = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

const STATUS_META = {
  Open:        { color: 'bg-blue-100 text-blue-700',   label: 'Open' },
  InProgress:  { color: 'bg-amber-100 text-amber-700', label: 'In Progress' },
  Pending:     { color: 'bg-yellow-100 text-yellow-700', label: 'Pending' },
  Resolved:    { color: 'bg-green-100 text-green-700', label: 'Resolved' },
  Closed:      { color: 'bg-gray-100 text-gray-600',   label: 'Closed' },
}

const PRIORITY_META = {
  Low:      { color: 'bg-gray-100 text-gray-600',    dot: 'bg-gray-400' },
  Medium:   { color: 'bg-blue-100 text-blue-700',    dot: 'bg-blue-500' },
  High:     { color: 'bg-amber-100 text-amber-700',  dot: 'bg-amber-500' },
  Critical: { color: 'bg-red-100 text-red-700',      dot: 'bg-red-500' },
}

function fmtDate(iso) {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('en-KE', {
    day: 'numeric', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

export default function PortalTrackPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { slug, name: brandName, logoUrl } = usePortalTenant()
  const slugQs = slug ? `?slug=${encodeURIComponent(slug)}` : ''

  // Pre-fill reference if navigated from submit success screen
  const [ref, setRef] = useState(location.state?.reference ?? '')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [result, setResult] = useState(null)
  const [attachments, setAttachments] = useState([])
  const [comments, setComments] = useState([])
  const [replyText, setReplyText] = useState('')
  const [replySending, setReplySending] = useState(false)
  const [replyError, setReplyError] = useState(null)
  const [lightbox, setLightbox] = useState(null)
  const [uploadFiles, setUploadFiles] = useState([])
  const [uploading, setUploading] = useState(false)
  const [uploadError, setUploadError] = useState(null)
  const [uploadSuccess, setUploadSuccess] = useState(false)

  const MAX_PHOTOS = 3
  const MAX_SIZE = 1 * 1024 * 1024

  function handleFileSelect(e) {
    setUploadError(null)
    setUploadSuccess(false)
    const incoming = Array.from(e.target.files ?? [])
    const remaining = MAX_PHOTOS - attachments.length
    const selected = incoming.slice(0, remaining).map(f => {
      const err = f.size > MAX_SIZE ? 'File too large (max 1 MB)'
        : !f.type.startsWith('image/') ? 'Only images allowed'
        : null
      return { file: f, preview: URL.createObjectURL(f), error: err }
    })
    setUploadFiles(selected)
    e.target.value = ''
  }

  async function handleUpload() {
    const valid = uploadFiles.filter(p => !p.error)
    if (!valid.length || !result?.ticketId) return
    setUploading(true)
    setUploadError(null)
    try {
      const fd = new FormData()
      valid.forEach(p => fd.append('files', p.file))
      const uploadPath = result.isInternal ? 'internal/attachments' : 'attachments'
      const res = await fetch(`${API}/api/v1/portal/${uploadPath}/${result.ticketId}${slugQs}`, {
        method: 'POST',
        body: fd,
      })
      if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        throw new Error(body?.message ?? 'Upload failed.')
      }
      const body = await res.json()
      setAttachments(prev => [...prev, ...(body?.data ?? [])])
      setUploadFiles([])
      setUploadSuccess(true)
    } catch (err) {
      setUploadError(err.message)
    } finally {
      setUploading(false)
    }
  }

  async function handleTrack(e) {
    e.preventDefault()
    const trimmed = ref.trim().toUpperCase()
    if (!trimmed || trimmed.length < 6) {
      setError('Please enter a valid reference number (at least 6 characters).')
      return
    }

    setLoading(true)
    setError(null)
    setResult(null)
    setAttachments([])
    setComments([])
    setReplyText('')
    setReplyError(null)
    setUploadFiles([])
    setUploadError(null)
    setUploadSuccess(false)

    try {
      const res = await fetch(`${API}/api/v1/portal/track/${trimmed}${slugQs}`)
      const body = await res.json()
      if (!res.ok) {
        setError(body?.message ?? 'No submission found with that reference number.')
        return
      }
      setResult(body.data)

      // Fetch attachments in parallel — best effort
      try {
        const attRes = await fetch(`${API}/api/v1/portal/track/${trimmed}/attachments${slugQs}`)
        if (attRes.ok) {
          const attBody = await attRes.json()
          setAttachments(attBody?.data ?? [])
        }
      } catch { /* ignore */ }

      // Fetch the public conversation — best effort
      try {
        const cmtRes = await fetch(`${API}/api/v1/portal/track/${trimmed}/comments${slugQs}`)
        if (cmtRes.ok) {
          const cmtBody = await cmtRes.json()
          setComments(cmtBody?.data ?? [])
        }
      } catch { /* ignore */ }
    } catch {
      setError('Unable to reach the server. Please try again shortly.')
    } finally {
      setLoading(false)
    }
  }

  async function handleReply() {
    if (!replyText.trim() || !result) return
    setReplySending(true)
    setReplyError(null)
    try {
      const res = await fetch(`${API}/api/v1/portal/track/${result.reference}/reply${slugQs}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: replyText.trim() }),
      })
      if (!res.ok) {
        const b = await res.json().catch(() => ({}))
        throw new Error(b?.message ?? 'Could not send your reply. Please try again.')
      }
      setReplyText('')
      // Refresh conversation + status (a reply un-pends the ticket).
      const [cmtRes, stRes] = await Promise.all([
        fetch(`${API}/api/v1/portal/track/${result.reference}/comments${slugQs}`),
        fetch(`${API}/api/v1/portal/track/${result.reference}${slugQs}`),
      ])
      if (cmtRes.ok) setComments((await cmtRes.json())?.data ?? [])
      if (stRes.ok) setResult((await stRes.json())?.data ?? result)
    } catch (e) {
      setReplyError(e.message)
    } finally {
      setReplySending(false)
    }
  }

  const statusMeta = result ? (STATUS_META[result.status] ?? { color: 'bg-gray-100 text-gray-600', label: result.status }) : null
  const priorityMeta = result ? (PRIORITY_META[result.priority] ?? { color: 'bg-gray-100 text-gray-600', dot: 'bg-gray-400' }) : null

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header */}
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-3xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <button onClick={() => navigate('/portal')} className="flex items-center gap-3 group">
            <img src={logoUrl || "/qc-logo.png"} alt={brandName || 'Lante'} className="w-9 h-9 object-contain" />
            <div className="text-left">
              <p className="font-extrabold text-zinc-950 text-sm leading-none group-hover:text-amber-600 transition-colors">
                {brandName || 'Lante'}
              </p>
              <p className="text-xs text-gray-400">Client Portal</p>
            </div>
          </button>
          <button
            onClick={() => navigate(result?.isInternal ? '/staff' : '/portal/submit')}
            className="text-sm font-medium text-amber-600 hover:text-amber-800 transition-colors"
          >
            New Submission →
          </button>
        </div>
      </header>

      <main className="flex-1 max-w-3xl mx-auto w-full px-4 sm:px-6 py-12">
        {/* Hero */}
        <div className="text-center mb-10">
          <span className="inline-flex items-center gap-1.5 bg-amber-50 text-amber-700 text-xs font-semibold px-3 py-1.5 rounded-full border border-amber-200 mb-4">
            <span className="w-1.5 h-1.5 bg-amber-500 rounded-full inline-block" />
            Track Submission
          </span>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-zinc-950 mb-3 leading-tight">
            Check Your Submission
          </h1>
          <p className="text-gray-500 text-base max-w-lg mx-auto leading-relaxed">
            Enter the reference number you received after submitting your request to view its current status.
          </p>
        </div>

        {/* Search form */}
        <div className="bg-white rounded-2xl border border-gray-200 p-6 sm:p-8 mb-6">
          <form onSubmit={handleTrack} className="flex flex-col sm:flex-row gap-3">
            <div className="flex-1">
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                Reference Number
              </label>
              <input
                type="text"
                value={ref}
                onChange={e => setRef(e.target.value.toUpperCase())}
                placeholder="e.g. A3F92B1C"
                maxLength={12}
                className="input font-mono tracking-widest uppercase"
                autoFocus
              />
            </div>
            <div className="sm:pt-7">
              <button
                type="submit"
                disabled={loading}
                className="w-full sm:w-auto px-6 py-2.5 bg-amber-500 hover:bg-amber-400 disabled:bg-amber-300 text-white font-bold rounded-xl transition-colors text-sm"
              >
                {loading ? (
                  <span className="flex items-center gap-2">
                    <svg className="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                    </svg>
                    Searching…
                  </span>
                ) : 'Track'}
              </button>
            </div>
          </form>

          {error && (
            <div className="mt-4 flex items-start gap-3 bg-red-50 border border-red-200 rounded-xl p-4">
              <svg className="w-5 h-5 text-red-500 shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01M12 3a9 9 0 100 18A9 9 0 0012 3z" />
              </svg>
              <p className="text-sm text-red-700">{error}</p>
            </div>
          )}
        </div>

        {/* Result card */}
        {result && (
          <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden">
            {/* Card header */}
            <div className="bg-zinc-950 px-6 py-5 flex items-center justify-between">
              <div>
                <p className="text-xs text-zinc-400 font-semibold uppercase tracking-wide mb-1">Reference</p>
                <p className="font-mono text-xl font-extrabold text-white tracking-widest">{result.reference}</p>
              </div>
              {statusMeta && (
                <span className={`text-sm font-bold px-4 py-1.5 rounded-full ${statusMeta.color}`}>
                  {statusMeta.label}
                </span>
              )}
            </div>

            {/* Details */}
            <div className="px-6 py-6 divide-y divide-gray-100">
              <Row label="Subject" value={result.subject} />
              <Row label="Category" value={result.category} />
              <Row label="Priority">
                {priorityMeta && (
                  <span className={`inline-flex items-center gap-1.5 text-sm font-semibold px-2.5 py-0.5 rounded-full ${priorityMeta.color}`}>
                    <span className={`w-1.5 h-1.5 rounded-full ${priorityMeta.dot}`} />
                    {result.priority}
                  </span>
                )}
              </Row>
              <Row label="Submitted" value={fmtDate(result.submittedAt)} />
              <Row label="Last Updated" value={fmtDate(result.updatedAt)} />

              {result.isResolved && result.resolutionNotes && (
                <div className="pt-5">
                  <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Resolution Notes</p>
                  <div className="bg-green-50 border border-green-200 rounded-xl p-4">
                    <p className="text-sm text-green-800 leading-relaxed whitespace-pre-line">
                      {result.resolutionNotes}
                    </p>
                  </div>
                </div>
              )}

              {result.isResolved && !result.resolutionNotes && (
                <div className="pt-5">
                  <div className="bg-green-50 border border-green-200 rounded-xl p-4 flex items-center gap-3">
                    <svg className="w-5 h-5 text-green-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                    <p className="text-sm text-green-700 font-medium">This submission has been resolved.</p>
                  </div>
                </div>
              )}

              {/* Attached photos */}
              {attachments.length > 0 && (
                <div className="pt-5">
                  <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">
                    Attached Photos ({attachments.length})
                  </p>
                  <div className="grid grid-cols-3 gap-2">
                    {attachments.map((a, i) => (
                      <button
                        key={i}
                        type="button"
                        onClick={() => setLightbox(`${API}${a.fileUrl}`)}
                        className="rounded-xl overflow-hidden border border-gray-200 aspect-square hover:opacity-90 transition-opacity"
                      >
                        <img
                          src={`${API}${a.fileUrl}`}
                          alt={a.fileName}
                          className="w-full h-full object-cover"
                        />
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Conversation + reply (#1 customer reply loop) */}
              <div className="pt-5">
                <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">Conversation</p>
                {comments.length === 0 ? (
                  <p className="text-sm text-gray-400">No messages yet. If we need more information we'll post it here.</p>
                ) : (
                  <div className="space-y-3">
                    {comments.map(c => {
                      const mine = c.authorUserId === 'portal-customer'
                      return (
                        <div key={c.id} className={`rounded-xl p-3 text-sm ${mine ? 'bg-amber-50 border border-amber-200 ml-6' : 'bg-gray-50 border border-gray-200 mr-6'}`}>
                          <div className="flex items-center justify-between mb-1">
                            <span className="text-xs font-semibold text-gray-500">{mine ? 'You' : 'Support'}</span>
                            <span className="text-xs text-gray-400">{fmtDate(c.createdAt)}</span>
                          </div>
                          <p className="text-gray-800 whitespace-pre-line">{c.content}</p>
                        </div>
                      )
                    })}
                  </div>
                )}

                {!result.isResolved && (
                  <div className="mt-4">
                    {replyError && (
                      <div className="mb-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-xl px-3 py-2">{replyError}</div>
                    )}
                    <textarea
                      value={replyText}
                      onChange={e => setReplyText(e.target.value)}
                      rows={3}
                      placeholder="Add a reply to your ticket…"
                      className="w-full px-3 py-2.5 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 resize-none"
                    />
                    <div className="flex justify-end mt-2">
                      <button
                        type="button"
                        onClick={handleReply}
                        disabled={replySending || !replyText.trim()}
                        className="px-5 py-2 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-sm font-semibold rounded-lg transition-colors"
                      >
                        {replySending ? 'Sending…' : 'Send Reply'}
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* Upload photos — only if fewer than 3 attached and ticket not closed */}
            {!result.isResolved && attachments.length < 3 && (
              <div className="pt-5">
                <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">
                  Add Photos {attachments.length > 0 ? `(${3 - attachments.length} remaining)` : '(optional, max 3)'}
                </p>

                {uploadSuccess && (
                  <div className="mb-3 flex items-center gap-2 bg-green-50 border border-green-200 rounded-xl px-4 py-2.5 text-sm text-green-700 font-medium">
                    <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path d="M5 13l4 4L19 7"/></svg>
                    Photos uploaded successfully.
                  </div>
                )}

                {uploadError && (
                  <div className="mb-3 text-sm text-red-600 bg-red-50 border border-red-200 rounded-xl px-4 py-2.5">{uploadError}</div>
                )}

                <div className="flex flex-wrap gap-2 mb-3">
                  {uploadFiles.map((p, i) => (
                    <div key={i} className="relative w-20 h-20 rounded-lg overflow-hidden border border-gray-200">
                      <img src={p.preview} alt="" className="w-full h-full object-cover" />
                      {p.error && <div className="absolute inset-0 bg-red-500/60 flex items-center justify-center"><span className="text-white text-xs font-bold text-center px-1">{p.error}</span></div>}
                      <button onClick={() => setUploadFiles(f => f.filter((_, j) => j !== i))} className="absolute top-0.5 right-0.5 w-5 h-5 bg-black/60 rounded-full flex items-center justify-center text-white text-xs">✕</button>
                    </div>
                  ))}
                </div>

                <div className="flex gap-2">
                  <label className="cursor-pointer px-4 py-2 border border-gray-300 rounded-xl text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
                    Choose Photos
                    <input type="file" accept="image/*" multiple className="hidden" onChange={handleFileSelect} />
                  </label>
                  {uploadFiles.filter(p => !p.error).length > 0 && (
                    <button
                      onClick={handleUpload}
                      disabled={uploading}
                      className="px-4 py-2 bg-amber-500 hover:bg-amber-400 disabled:bg-amber-300 text-white text-sm font-bold rounded-xl transition-colors"
                    >
                      {uploading ? 'Uploading…' : `Upload ${uploadFiles.filter(p => !p.error).length} Photo${uploadFiles.filter(p => !p.error).length > 1 ? 's' : ''}`}
                    </button>
                  )}
                </div>
              </div>
            )}

            {/* Footer */}
            <div className="border-t border-gray-100 bg-gray-50 px-6 py-4 flex flex-col sm:flex-row items-center justify-between gap-3">
              <p className="text-xs text-gray-500">
                Need help? Contact us at{' '}
                <a href="mailto:info@lante.co.ke" className="text-amber-600 hover:underline font-medium">
                  info@lante.co.ke
                </a>
              </p>
              <button
                onClick={() => { setResult(null); setRef(''); setAttachments([]); setUploadFiles([]); setUploadError(null); setUploadSuccess(false) }}
                className="text-sm font-medium text-gray-500 hover:text-gray-700 transition-colors"
              >
                Track another submission →
              </button>
            </div>
          </div>
        )}

        {/* Help box — shown when no result yet */}
        {!result && (
          <div className="bg-zinc-950 rounded-2xl p-6 flex flex-col sm:flex-row items-start gap-4">
            <div className="text-3xl shrink-0">📬</div>
            <div>
              <h3 className="font-bold text-white text-base mb-1">Haven't submitted yet?</h3>
              <p className="text-sm text-zinc-300 leading-relaxed mb-3">
                Use our portal to submit a complaint, share feedback, or tell us about a project you have in mind.
              </p>
              <button
                onClick={() => navigate('/portal/submit')}
                className="px-4 py-2 bg-amber-500 hover:bg-amber-400 text-white text-sm font-bold rounded-xl transition-colors"
              >
                Make a Submission →
              </button>
            </div>
          </div>
        )}
      </main>

      <footer className="border-t border-gray-200 bg-white py-5 px-6 text-center">
        <p className="text-xs text-gray-400">
          {brandName || 'Lante'} &copy; {new Date().getFullYear()} &mdash; All rights reserved.
          &nbsp;|&nbsp;
          <a href="mailto:info@lante.co.ke" className="hover:text-amber-600 transition-colors">info@lante.co.ke</a>
        </p>
      </footer>

      {/* Lightbox */}
      {lightbox && (
        <div
          className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4"
          onClick={() => setLightbox(null)}
        >
          <img
            src={lightbox}
            alt="Attachment"
            className="max-w-full max-h-full rounded-xl shadow-2xl"
            onClick={e => e.stopPropagation()}
          />
          <button
            onClick={() => setLightbox(null)}
            className="absolute top-4 right-4 w-9 h-9 bg-white/20 hover:bg-white/30 rounded-full flex items-center justify-center transition-colors"
          >
            <svg className="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
              <path d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
      )}
    </div>
  )
}

function Row({ label, value, children }) {
  return (
    <div className="py-3 flex items-start gap-4">
      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide w-28 shrink-0 pt-0.5">{label}</p>
      {children ?? <p className="text-sm text-gray-800 font-medium">{value ?? '—'}</p>}
    </div>
  )
}
