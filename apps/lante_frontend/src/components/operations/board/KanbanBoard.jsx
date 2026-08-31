import { useState, useEffect, useCallback } from 'react'
import * as ops from '../../../services/operations.js'

// PR4c — tasks by status, with drag-and-drop to move them. Drag is convenient, not required: every
// card also carries explicit move buttons, because drag-and-drop is unusable by keyboard and awkward
// on a touchscreen, which is what half the field staff are holding.

const COLUMNS = [
  { key: 'NotStarted', label: 'Not started', accent: 'border-t-gray-300' },
  { key: 'InProgress', label: 'In progress', accent: 'border-t-blue-400' },
  { key: 'Blocked',    label: 'Blocked',     accent: 'border-t-red-400' },
  { key: 'Done',       label: 'Done',        accent: 'border-t-emerald-400' },
]

const fmt = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short' }) : null)

export default function KanbanBoard({ projectId, canWrite, usersMap = {} }) {
  const [board, setBoard] = useState(null)
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)
  const [dragId, setDragId] = useState(null)
  const [overCol, setOverCol] = useState(null)

  const load = useCallback(async () => {
    try { setBoard(await ops.getProjectBoard(projectId)) }
    catch { setErr('Could not load the board.') }
  }, [projectId])

  useEffect(() => { load() }, [load])

  const move = async (task, status) => {
    if (!canWrite || task.status === status) return
    setBusy(true); setErr('')
    try {
      // Status-only update. PR1's member-level PreCondition on the update map means the omitted
      // fields are left alone rather than being reset to their type defaults.
      await ops.updateProjectTask(projectId, task.milestoneId, task.id, { status })
      await load()
    } catch (e) {
      setErr(e?.response?.data?.message || 'Could not move that task.')
    } finally {
      setBusy(false); setDragId(null); setOverCol(null)
    }
  }

  if (err && !board) return <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2.5">{err}</div>
  if (!board) return <p className="text-sm text-gray-400 py-6">Loading…</p>

  const tasks = board.tasks ?? []

  return (
    <div className="space-y-3">
      {err && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2.5">{err}</div>}
      {tasks.length === 0 && (
        <div className="text-center py-10 border border-dashed border-gray-200 rounded-lg">
          <div className="text-2xl mb-1">🗂️</div>
          <p className="text-sm font-semibold text-gray-700">No tasks yet</p>
          <p className="text-xs text-gray-400 mt-1">Tasks added under a milestone appear here.</p>
        </div>
      )}

      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        {COLUMNS.map(col => {
          const items = tasks.filter(t => t.status === col.key)
          return (
            <div
                key={col.key}
                onDragOver={(e) => { if (canWrite) { e.preventDefault(); setOverCol(col.key) } }}
                onDragLeave={() => setOverCol(c => (c === col.key ? null : c))}
                onDrop={() => {
                  const t = tasks.find(x => x.id === dragId)
                  if (t) move(t, col.key)
                }}
                className={`rounded-lg border border-t-4 ${col.accent} bg-gray-50/60 p-2 min-h-[120px] transition-colors ${
                  overCol === col.key ? 'bg-amber-50 border-amber-300' : 'border-gray-200'}`}>
              <div className="flex items-center justify-between px-1 pb-2">
                <span className="text-xs font-semibold text-gray-700">{col.label}</span>
                <span className="text-[11px] text-gray-400 tabular-nums">{items.length}</span>
              </div>

              <div className="space-y-2">
                {items.map(t => (
                  <div
                      key={t.id}
                      draggable={canWrite}
                      onDragStart={() => setDragId(t.id)}
                      onDragEnd={() => { setDragId(null); setOverCol(null) }}
                      className={`bg-white border border-gray-200 rounded-md p-2.5 ${canWrite ? 'cursor-grab active:cursor-grabbing' : ''} ${
                        dragId === t.id ? 'opacity-50' : ''}`}>
                    <p className="text-xs font-semibold text-gray-900">{t.title}</p>
                    {t.milestoneTitle && <p className="text-[11px] text-gray-400 mt-0.5">{t.milestoneTitle}</p>}

                    <div className="flex items-center gap-2 flex-wrap mt-1.5">
                      {t.assignedToUserId && (
                        <span className="text-[11px] text-gray-500">
                          {usersMap[t.assignedToUserId] ?? `${t.assignedToUserId.slice(0, 8)}…`}
                        </span>
                      )}
                      {t.estimatedHours > 0 && <span className="text-[11px] text-gray-400">{t.estimatedHours}h</span>}
                      {t.dueDate && (
                        // Overdue is stated in words as well as colour — a red date alone is not a
                        // signal to anyone who cannot see red.
                        <span className={`text-[11px] ${t.isOverdue ? 'text-red-700 font-semibold' : 'text-gray-400'}`}>
                          {t.isOverdue ? `overdue · ${fmt(t.dueDate)}` : `due ${fmt(t.dueDate)}`}
                        </span>
                      )}
                    </div>

                    {canWrite && (
                      <div className="flex gap-1.5 mt-2 pt-1.5 border-t border-gray-50">
                        {COLUMNS.filter(c => c.key !== t.status).map(c => (
                          <button key={c.key} onClick={() => move(t, c.key)} disabled={busy}
                                  title={`Move to ${c.label}`}
                                  className="text-[10px] text-gray-400 hover:text-amber-700 font-semibold">
                            → {c.label}
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
