import { useRef, useState } from 'react'
import { fmtDate } from '../../../utils/export.js'
import * as ticketApi from '../../../services/ticketing.js'

export function PhotosTab({ ticketId, attachments, onLightbox, onUploaded }) {
  const API_URL = import.meta.env.VITE_API_URL ?? ''
  const fileInputRef = useRef(null)
  const [uploading, setUploading] = useState(false)
  const [uploadError, setUploadError] = useState('')
  const maxAllowed = 3 - attachments.length

  async function handleUpload(e) {
    const files = Array.from(e.target.files ?? [])
    if (!files.length) return
    if (files.length > maxAllowed) {
      setUploadError(`You can add at most ${maxAllowed} more photo${maxAllowed === 1 ? '' : 's'} (limit 3 total).`)
      return
    }
    setUploadError('')
    setUploading(true)
    try {
      const form = new FormData()
      files.forEach(f => form.append('files', f))
      await ticketApi.uploadAttachments(ticketId, form)
      await onUploaded()
    } catch (err) {
      setUploadError(err.response?.data?.message ?? 'Upload failed. Please try again.')
    } finally {
      setUploading(false)
      if (fileInputRef.current) fileInputRef.current.value = ''
    }
  }

  return (
    <div className="space-y-4">
      {/* Upload area */}
      {attachments.length < 3 && (
        <div>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/*"
            multiple
            className="hidden"
            onChange={handleUpload}
          />
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            className="w-full flex items-center justify-center gap-2 px-4 py-3 border-2 border-dashed border-gray-200 hover:border-gold rounded-xl text-sm font-medium text-gray-500 hover:text-gold transition-colors disabled:opacity-50"
          >
            {uploading ? (
              <>
                <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                Uploading…
              </>
            ) : (
              <>
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Add photos ({attachments.length}/3)
              </>
            )}
          </button>
          {uploadError && (
            <p className="mt-1.5 text-xs text-red-600">{uploadError}</p>
          )}
        </div>
      )}

      {/* Grid */}
      {attachments.length === 0 ? (
        <div className="text-center py-8">
          <div className="text-3xl mb-2">📷</div>
          <p className="text-sm text-gray-400">No photos yet.</p>
        </div>
      ) : (
        <div>
          <p className="text-xs font-bold text-navy uppercase tracking-wider mb-3">
            Attached Photos ({attachments.length})
          </p>
          <div className="grid grid-cols-3 gap-3">
            {attachments.map((a, i) => (
              <button
                key={i}
                type="button"
                onClick={() => onLightbox(`${API_URL}${a.fileUrl}`)}
                className="relative group rounded-xl overflow-hidden border border-gray-200 aspect-square hover:opacity-90 transition-opacity"
                title={a.fileName}
              >
                <img src={`${API_URL}${a.fileUrl}`} alt={a.fileName} className="w-full h-full object-cover" />
                <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors flex items-center justify-center">
                  <svg className="w-6 h-6 text-white opacity-0 group-hover:opacity-100 transition-opacity drop-shadow" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.5">
                    <path d="M21 21l-4.35-4.35M11 19a8 8 0 100-16 8 8 0 000 16z"/>
                  </svg>
                </div>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

export function CommentsTab({ comments, commentText, setCommentText, onSubmit, loading, macros, showMacroPicker, setShowMacroPicker, onApplyMacro, macroApplying }) {
  return (
    <div className="space-y-4">
      <div>
        {macros.length > 0 && (
          <div className="relative mb-2 inline-block">
            <button type="button" onClick={() => setShowMacroPicker(v => !v)}
              disabled={macroApplying}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold text-purple-700 bg-purple-50 hover:bg-purple-100 rounded-lg transition-colors disabled:opacity-50">
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
              {macroApplying ? 'Applying…' : 'Use Macro'}
            </button>
            {showMacroPicker && (
              <div className="absolute left-0 mt-1 w-64 bg-white border border-gray-200 rounded-xl shadow-lg z-10 overflow-hidden">
                <div className="px-3 py-2 border-b border-gray-100">
                  <p className="text-xs font-semibold text-gray-500">Select a macro</p>
                </div>
                <div className="max-h-48 overflow-y-auto">
                  {macros.map(m => (
                    <button key={m.id} type="button" onClick={() => onApplyMacro(m.id)}
                      className="w-full text-left px-3 py-2.5 hover:bg-offwhite transition-colors">
                      <p className="text-sm font-medium text-gray-700">{m.name}</p>
                      {m.description && <p className="text-xs text-gray-400 truncate">{m.description}</p>}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
        <form onSubmit={onSubmit} className="flex gap-3">
          <textarea
            value={commentText}
            onChange={e => setCommentText(e.target.value)}
            placeholder="Add a comment…"
            rows={2}
            className="flex-1 px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
          />
          <button
            type="submit"
            disabled={loading || !commentText.trim()}
            className="self-end px-4 py-2.5 bg-navy hover:bg-navy-dark disabled:opacity-40 text-white text-sm font-semibold rounded-lg transition-colors"
          >
            {loading ? '…' : 'Post'}
          </button>
        </form>
      </div>

      {comments.length === 0 ? (
        <p className="text-sm text-gray-400 text-center py-6">No comments yet.</p>
      ) : (
        <div className="space-y-3">
          {comments.map(c => (
            <div key={c.id} className="bg-gray-50 rounded-lg p-4">
              <div className="flex items-center justify-between mb-1.5">
                <span className="text-xs font-semibold text-gray-600">{c.authorName ?? c.createdByUserId}</span>
                <span className="text-xs text-gray-400">{fmtDate(c.createdAt)}</span>
              </div>
              <p className="text-sm text-gray-700 leading-relaxed">{c.content}</p>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export function HistoryTab({ history }) {
  if (history.length === 0) {
    return <p className="text-sm text-gray-400 text-center py-6">No history yet.</p>
  }

  return (
    <div className="relative space-y-0">
      {history.map((h, i) => (
        <div key={h.id} className="flex gap-3 pb-4">
          <div className="flex flex-col items-center">
            <div className="w-2.5 h-2.5 rounded-full bg-gold mt-1 flex-shrink-0" />
            {i < history.length - 1 && <div className="w-px flex-1 bg-gray-200 mt-1" />}
          </div>
          <div className="pb-1 min-w-0">
            <p className="text-sm font-medium text-gray-800">{h.action}</p>
            {(h.fromValue || h.toValue) && (
              <p className="text-xs text-gray-500 mt-0.5">
                {h.fromValue && <span className="line-through mr-1">{h.fromValue}</span>}
                {h.toValue && <span className="text-green-700 font-medium">{h.toValue}</span>}
              </p>
            )}
            {h.notes && <p className="text-xs text-gray-400 mt-0.5 italic">{h.notes}</p>}
            <p className="text-xs text-gray-400 mt-0.5">{fmtDate(h.occurredAt)}</p>
          </div>
        </div>
      ))}
    </div>
  )
}
