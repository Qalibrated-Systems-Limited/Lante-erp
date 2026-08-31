import { useState, useEffect, useCallback, useMemo } from 'react'
import * as ops from '../../../services/operations.js'

// PR4c — a month grid of what is due. Milestones and tasks are distinguished by a glyph and a word,
// not only by colour, so the calendar survives greyscale printing and colour-blind readers.

const DAY_NAMES = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']
const MONTH = (d) => d.toLocaleDateString('en-KE', { month: 'long', year: 'numeric' })
const iso = (d) => d.toISOString().slice(0, 10)

/** Monday-first grid covering the whole month plus the padding days either side. */
function monthGrid(anchor) {
  const first = new Date(Date.UTC(anchor.getUTCFullYear(), anchor.getUTCMonth(), 1))
  const lead = (first.getUTCDay() + 6) % 7                 // Monday = 0
  const startAt = new Date(first); startAt.setUTCDate(1 - lead)

  const cells = []
  for (let i = 0; i < 42; i++) {
    const d = new Date(startAt); d.setUTCDate(startAt.getUTCDate() + i)
    cells.push(d)
  }
  return cells
}

export default function ProjectCalendar({ projectId }) {
  const [board, setBoard] = useState(null)
  const [anchor, setAnchor] = useState(() => new Date())
  const [err, setErr] = useState('')

  const load = useCallback(async () => {
    try { setBoard(await ops.getProjectBoard(projectId)) }
    catch { setErr('Could not load the calendar.') }
  }, [projectId])

  useEffect(() => { load() }, [load])

  // Index by due date once, rather than filtering every event for each of the 42 cells.
  const byDay = useMemo(() => {
    const map = {}
    const push = (date, item) => {
      if (!date) return
      const k = iso(new Date(date))
      ;(map[k] ??= []).push(item)
    }
    for (const m of board?.milestones ?? [])
      push(m.dueDate, { kind: 'milestone', title: m.title, overdue: m.isOverdue, meta: `${m.progressPct}%` })
    for (const t of board?.tasks ?? [])
      push(t.dueDate, { kind: 'task', title: t.title, overdue: t.isOverdue, meta: t.status })
    return map
  }, [board])

  if (err && !board) return <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2.5">{err}</div>
  if (!board) return <p className="text-sm text-gray-400 py-6">Loading…</p>

  const cells = monthGrid(anchor)
  const thisMonth = anchor.getUTCMonth()
  const todayKey = iso(new Date())

  const step = (n) => {
    const d = new Date(anchor); d.setUTCMonth(d.getUTCMonth() + n); setAnchor(d)
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <h3 className="text-sm font-semibold text-gray-900">{MONTH(anchor)}</h3>
        <div className="flex items-center gap-1">
          <button onClick={() => step(-1)} className="text-xs border border-gray-200 rounded px-2 py-1 hover:bg-gray-50">←</button>
          <button onClick={() => setAnchor(new Date())} className="text-xs border border-gray-200 rounded px-2 py-1 hover:bg-gray-50">Today</button>
          <button onClick={() => step(1)} className="text-xs border border-gray-200 rounded px-2 py-1 hover:bg-gray-50">→</button>
        </div>
      </div>

      <div className="flex gap-4 text-[11px] text-gray-500">
        <span>◆ milestone</span>
        <span>• task</span>
        <span className="text-red-700 font-semibold">! overdue</span>
      </div>

      <div className="overflow-x-auto">
        <div className="min-w-[640px]">
          <div className="grid grid-cols-7 gap-px bg-gray-100 border border-gray-200 rounded-t-lg overflow-hidden">
            {DAY_NAMES.map(d => (
              <div key={d} className="bg-gray-50 px-2 py-1.5 text-[11px] font-semibold text-gray-500 text-center">{d}</div>
            ))}
          </div>
          <div className="grid grid-cols-7 gap-px bg-gray-100 border border-t-0 border-gray-200 rounded-b-lg overflow-hidden">
            {cells.map((d) => {
              const key = iso(d)
              const items = byDay[key] ?? []
              const muted = d.getUTCMonth() !== thisMonth
              return (
                <div key={key} className={`bg-white min-h-[92px] p-1.5 ${muted ? 'opacity-40' : ''}`}>
                  <div className="flex items-center justify-between">
                    <span className={`text-[11px] tabular-nums ${
                      key === todayKey ? 'bg-amber-500 text-white rounded-full w-5 h-5 grid place-items-center font-bold' : 'text-gray-400'}`}>
                      {d.getUTCDate()}
                    </span>
                    {items.length > 2 && <span className="text-[10px] text-gray-400">{items.length}</span>}
                  </div>
                  <div className="space-y-1 mt-1">
                    {items.slice(0, 3).map((it, i) => (
                      <div key={i}
                           title={`${it.kind}: ${it.title}${it.overdue ? ' (overdue)' : ''}`}
                           className={`text-[10px] leading-tight truncate rounded px-1 py-0.5 ${
                             it.overdue ? 'bg-red-50 text-red-800 font-semibold'
                             : it.kind === 'milestone' ? 'bg-amber-50 text-amber-900' : 'bg-gray-50 text-gray-600'}`}>
                        {it.overdue ? '! ' : it.kind === 'milestone' ? '◆ ' : '• '}{it.title}
                      </div>
                    ))}
                    {items.length > 3 && <div className="text-[10px] text-gray-400 px-1">+{items.length - 3} more</div>}
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      </div>
    </div>
  )
}
