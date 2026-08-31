import { useState, useEffect, useCallback, useRef } from 'react'
import * as ops from '../../../services/operations.js'

// PR1 — the signed contract for a project. Upload stores the file and stamps it on the project in a
// single call, so there is never a window where the file exists but the project doesn't know about it.

const API_BASE = import.meta.env.VITE_API_URL || ''

function size(bytes) {
  if (!bytes) return ''
  const kb = bytes / 1024
  return kb < 1024 ? `${Math.round(kb)} KB` : `${(kb / 1024).toFixed(1)} MB`
}

export default function ContractCard({ projectId, contractAttachmentId, canWrite, onUploaded }) {
  const [docs, setDocs]     = useState([])
  const [busy, setBusy]     = useState(false)
  const [error, setError]   = useState('')
  const fileRef = useRef(null)

  const load = useCallback(async () => {
    try { setDocs(await ops.getProjectDocuments(projectId) ?? []) }
    catch { /* the card is supplementary; a failed list must not break the overview */ }
  }, [projectId])

  useEffect(() => { load() }, [load])

  const upload = async (file) => {
    if (!file) return
    setBusy(true); setError('')
    try {
      await ops.uploadProjectContract(projectId, file, 'Signed contract')
      await load()
      onUploaded?.()
    } catch (e) {
      setError(e.response?.data?.message ?? 'Upload failed.')
    } finally {
      setBusy(false)
      if (fileRef.current) fileRef.current.value = ''   // let the same file be re-picked after a failure
    }
  }

  const contract = docs.find(d => d.id === contractAttachmentId)
  const others   = docs.filter(d => d.id !== contractAttachmentId)

  return (
    <div className="bg-white border border-gray-200 rounded-xl p-5">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-bold text-gray-800">Contract &amp; documents</h3>
        {canWrite && (
          <>
            <input ref={fileRef} type="file" className="hidden"
                   onChange={e => upload(e.target.files?.[0])}
                   accept=".pdf,.doc,.docx,.png,.jpg,.jpeg" />
            <button onClick={() => fileRef.current?.click()} disabled={busy}
                    className="px-3 py-1.5 bg-navy text-white text-xs font-semibold rounded-lg disabled:opacity-60">
              {busy ? 'Uploading…' : contract ? 'Replace contract' : 'Upload contract'}
            </button>
          </>
        )}
      </div>

      {error && <p className="mb-3 px-3 py-2 bg-red-50 border border-red-200 rounded text-red-700 text-xs">{error}</p>}

      {contract ? (
        <a href={`${API_BASE}${contract.storageUrl}`} target="_blank" rel="noreferrer"
           className="flex items-center gap-3 px-3 py-2.5 border border-emerald-200 bg-emerald-50/60 rounded-lg hover:bg-emerald-50">
          <span className="text-lg">📄</span>
          <span className="flex-1 min-w-0">
            <span className="block text-sm font-medium text-gray-800 truncate">{contract.fileName}</span>
            <span className="block text-[11px] text-gray-500">Signed contract · {size(contract.fileSizeBytes)}</span>
          </span>
          <span className="text-xs text-navy font-semibold">Open ↗</span>
        </a>
      ) : (
        <p className="text-sm text-gray-400 py-2">
          No contract on file{canWrite ? ' — upload the signed copy so it travels with the project.' : '.'}
        </p>
      )}

      {others.length > 0 && (
        <div className="mt-3 pt-3 border-t border-gray-100">
          <p className="text-[11px] uppercase tracking-wide text-gray-400 font-semibold mb-1.5">Other documents</p>
          <ul className="space-y-1">
            {others.map(d => (
              <li key={d.id}>
                <a href={`${API_BASE}${d.storageUrl}`} target="_blank" rel="noreferrer"
                   className="text-sm text-gray-600 hover:text-navy hover:underline">
                  {d.fileName} <span className="text-[11px] text-gray-400">{size(d.fileSizeBytes)}</span>
                </a>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
