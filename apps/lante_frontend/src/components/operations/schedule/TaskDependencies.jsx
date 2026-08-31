import { useState } from 'react'
import * as ops from '../../../services/operations.js'

// PR1 — finish-to-start links between tasks. Lives beside the task list rather than on the Gantt:
// the Gantt is drawn from milestones, and a project's tasks are far too many to render there.

export default function TaskDependencies({ projectId, tasks, deps, canWrite, onChanged }) {
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState({ predecessorId: '', successorId: '', lagDays: 0 })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const ids = new Set(tasks.map(t => t.id))
  // Only links whose ends are both in this milestone; a link to another milestone's task belongs
  // in that milestone's list, not duplicated here.
  const mine = deps.filter(d => ids.has(d.predecessorId) && ids.has(d.successorId))
  const title = (id) => tasks.find(t => t.id === id)?.title ?? '—'

  const add = async () => {
    if (!form.predecessorId || !form.successorId) { setError('Pick both tasks.'); return }
    setBusy(true); setError('')
    try {
      await ops.linkTasks(projectId, { ...form, lagDays: Number(form.lagDays) || 0 })
      setForm({ predecessorId: '', successorId: '', lagDays: 0 })
      await onChanged?.()
    } catch (e) {
      // The API names the cycle path when it refuses — worth showing verbatim.
      setError(e.response?.data?.message ?? 'Could not create the link.')
    } finally { setBusy(false) }
  }

  const remove = async (depId) => {
    setBusy(true); setError('')
    try { await ops.unlinkTasks(projectId, depId); await onChanged?.() }
    catch (e) { setError(e.response?.data?.message ?? 'Could not remove the link.') }
    finally { setBusy(false) }
  }

  if (tasks.length < 2 && mine.length === 0) return null

  return (
    <div className="mt-2 pt-2 border-t border-gray-100">
      <button onClick={() => setOpen(o => !o)}
              className="text-xs font-semibold text-gray-500 hover:text-gray-700 flex items-center gap-1">
        Task order {mine.length > 0 && <span className="text-gray-400">({mine.length})</span>} {open ? '▾' : '▸'}
      </button>

      {open && (
        <div className="mt-2 space-y-2">
          {mine.length === 0
            ? <p className="text-xs text-gray-400">No ordering set — these tasks can run in any sequence.</p>
            : (
              <ul className="space-y-1">
                {mine.map(d => (
                  <li key={d.id} className="flex items-center gap-1.5 text-xs text-gray-600">
                    <span className="font-medium">{title(d.predecessorId)}</span>
                    <span className="text-gray-300">→</span>
                    <span className="font-medium">{title(d.successorId)}</span>
                    {d.lagDays > 0 && <span className="text-gray-400">+{d.lagDays}d</span>}
                    {canWrite && (
                      <button onClick={() => remove(d.id)} disabled={busy}
                              className="ml-1 text-red-600 hover:underline disabled:opacity-50">remove</button>
                    )}
                  </li>
                ))}
              </ul>
            )}

          {canWrite && (
            <div className="flex flex-wrap items-center gap-1.5">
              <select value={form.predecessorId} onChange={e => setForm(f => ({ ...f, predecessorId: e.target.value }))}
                      className="input text-xs py-1 max-w-[160px]">
                <option value="">after…</option>
                {tasks.map(t => <option key={t.id} value={t.id}>{t.title}</option>)}
              </select>
              <span className="text-gray-300 text-xs">→</span>
              <select value={form.successorId} onChange={e => setForm(f => ({ ...f, successorId: e.target.value }))}
                      className="input text-xs py-1 max-w-[160px]">
                <option value="">start…</option>
                {tasks.map(t => <option key={t.id} value={t.id}>{t.title}</option>)}
              </select>
              <input type="number" min="0" value={form.lagDays} title="Lag in days"
                     onChange={e => setForm(f => ({ ...f, lagDays: e.target.value }))}
                     className="input text-xs py-1 w-16" />
              <button onClick={add} disabled={busy}
                      className="text-xs font-semibold text-navy hover:underline disabled:opacity-50">link</button>
            </div>
          )}

          {error && <p className="text-xs text-red-600">{error}</p>}
        </div>
      )}
    </div>
  )
}
