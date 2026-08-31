import { useState, useEffect, useCallback } from 'react'
import * as ops from '../../../services/operations.js'

// PR1 — the project schedule: a Gantt against the approved baseline, plus the finish-to-start
// links that make a critical path meaningful.

const DAY = 86400000

function fmt(iso) {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: '2-digit' })
}

// A slip is only meaningful against a baseline; before one exists the column stays blank rather
// than showing a reassuring zero.
function VarianceChip({ days }) {
  if (days == null) return <span className="text-xs text-gray-400">—</span>
  const late = days > 0
  const cls = late ? 'bg-red-50 text-red-700' : days < 0 ? 'bg-emerald-50 text-emerald-700' : 'bg-gray-100 text-gray-600'
  return (
    <span className={`text-[11px] font-semibold px-1.5 py-0.5 rounded ${cls}`}>
      {days > 0 ? `+${days}d` : days < 0 ? `${days}d` : 'on time'}
    </span>
  )
}

export default function ScheduleTab({ projectId, canApprove, canWrite, onToast }) {
  const [data, setData]       = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')
  const [busy, setBusy]       = useState(false)
  const [link, setLink]       = useState({ predecessorId: '', successorId: '', lagDays: 0 })
  const [linkError, setLinkError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { setData(await ops.getProjectSchedule(projectId)) }
    catch { setError('Failed to load the schedule.') }
    finally { setLoading(false) }
  }, [projectId])

  useEffect(() => { load() }, [load])

  const baseline = async () => {
    setBusy(true)
    try { const r = await ops.setProjectBaseline(projectId); onToast?.(`Baseline captured for ${r.milestonesBaselined} milestone(s).`); await load() }
    catch (e) { onToast?.(e.response?.data?.message ?? 'Could not set the baseline.') }
    finally { setBusy(false) }
  }

  const addLink = async () => {
    if (!link.predecessorId || !link.successorId) { setLinkError('Pick both milestones.'); return }
    setBusy(true); setLinkError('')
    try {
      await ops.linkMilestones(projectId, { ...link, lagDays: Number(link.lagDays) || 0 })
      setLink({ predecessorId: '', successorId: '', lagDays: 0 }); await load()
    } catch (e) {
      // The API explains cycles by naming the path — keep it on screen, it's the whole value.
      setLinkError(e.response?.data?.message ?? 'Could not create the link.')
    } finally { setBusy(false) }
  }

  const removeLink = async (depId) => {
    setBusy(true)
    try { await ops.unlinkMilestones(projectId, depId); await load() }
    catch (e) { onToast?.(e.response?.data?.message ?? 'Could not remove the link.') }
    finally { setBusy(false) }
  }

  if (loading) return <p className="text-sm text-gray-500 py-8">Loading schedule…</p>
  if (error)   return <div className="px-4 py-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">{error}</div>
  if (!data)   return null

  const items = data.items ?? []
  if (items.length === 0)
    return <p className="text-sm text-gray-500 py-8">No milestones yet — add some on the Milestones tab to build a schedule.</p>

  // Time axis spans plan and baseline together, so a slipped bar and its baseline stay on-screen.
  const dates = items.flatMap(i => [i.startDate, i.dueDate, i.baselineStart, i.baselineDue])
                     .filter(Boolean).map(d => new Date(d).getTime())
  const min = Math.min(...dates), max = Math.max(...dates)
  const span = Math.max(DAY, max - min)
  const pct = (t) => ((new Date(t).getTime() - min) / span) * 100

  const months = []
  for (let d = new Date(min); d.getTime() <= max; d.setMonth(d.getMonth() + 1)) {
    months.push({ label: d.toLocaleDateString('en-GB', { month: 'short' }), left: pct(new Date(d)) })
  }

  const byId = Object.fromEntries(items.map(i => [i.id, i]))

  return (
    <div className="space-y-6">

      {/* Header stats */}
      <div className="flex flex-wrap items-center gap-4 justify-between">
        <div className="flex flex-wrap gap-6 text-sm">
          <div><span className="text-gray-500">Plan </span>
            <span className="font-semibold">{fmt(data.plannedStart)} → {fmt(data.plannedFinish)}</span></div>
          <div><span className="text-gray-500">Worst slip </span>
            <VarianceChip days={data.worstVarianceDays} /></div>
          <div><span className="text-gray-500">Baseline </span>
            {data.baselineSetAt
              ? <span className="font-semibold">{fmt(data.baselineSetAt)}</span>
              : <span className="text-amber-700 font-semibold">not set</span>}</div>
        </div>
        {canApprove && !data.baselineSetAt && (
          <button onClick={baseline} disabled={busy}
                  className="px-4 py-2 bg-navy text-white text-sm font-semibold rounded-lg disabled:opacity-60">
            {busy ? 'Working…' : 'Set baseline'}
          </button>
        )}
      </div>

      {!data.baselineSetAt && (
        <div className="px-4 py-3 bg-amber-50 border border-amber-200 rounded-lg text-amber-900 text-sm">
          No baseline yet, so slippage can't be measured. Setting one freezes today's milestone dates
          as the plan of record — after that it only moves through an approved change.
        </div>
      )}

      {/* Gantt */}
      <div className="border border-gray-200 rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <div className="min-w-[880px]">
            {/* Month scale */}
            <div className="relative h-7 bg-gray-50 border-b border-gray-200">
              <div className="absolute inset-y-0 left-0 w-[280px] border-r border-gray-200 flex items-center px-3">
                <span className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Milestone</span>
              </div>
              <div className="absolute inset-y-0 left-[280px] right-0">
                {months.map((m, i) => (
                  <span key={i} className="absolute top-1.5 text-[11px] text-gray-500"
                        style={{ left: `${m.left}%` }}>{m.label}</span>
                ))}
              </div>
            </div>

            {items.map(i => {
              const l = pct(i.startDate ?? i.dueDate)
              const r = pct(i.dueDate)
              const width = Math.max(1.2, r - l)
              const hasBaseline = i.baselineStart || i.baselineDue
              const bl = hasBaseline ? pct(i.baselineStart ?? i.baselineDue) : 0
              const br = hasBaseline ? pct(i.baselineDue ?? i.baselineStart) : 0

              return (
                <div key={i.id} className="relative h-12 border-b border-gray-100 last:border-b-0 hover:bg-gray-50/60">
                  <div className="absolute inset-y-0 left-0 w-[280px] border-r border-gray-200 flex flex-col justify-center px-3">
                    <div className="flex items-center gap-1.5">
                      {/* Critical path is encoded in form as well as colour, so it survives a
                          greyscale print and doesn't rely on hue alone. */}
                      {i.isCritical && <span title="On the critical path" className="text-red-600 font-bold text-xs">▲</span>}
                      <span className="text-sm font-medium text-gray-800 truncate">{i.title}</span>
                    </div>
                    <div className="flex items-center gap-2 mt-0.5">
                      <span className="text-[11px] text-gray-400">{i.progressPct}%</span>
                      <VarianceChip days={i.scheduleVarianceDays} />
                    </div>
                  </div>

                  <div className="absolute inset-y-0 left-[280px] right-0">
                    {/* Baseline sits behind as a thin ghost bar — the plan of record to compare against. */}
                    {hasBaseline && (
                      <div className="absolute h-1.5 rounded bg-gray-300 top-[13px]"
                           style={{ left: `${bl}%`, width: `${Math.max(1.2, br - bl)}%` }}
                           title={`Baseline ${fmt(i.baselineStart)} → ${fmt(i.baselineDue)}`} />
                    )}
                    <div className={`absolute h-5 rounded top-[22px] ${i.isCritical ? 'bg-red-500/85' : 'bg-navy/80'}`}
                         style={{ left: `${l}%`, width: `${width}%` }}
                         title={`${fmt(i.startDate)} → ${fmt(i.dueDate)}`}>
                      <div className="h-full rounded-l bg-white/35" style={{ width: `${i.progressPct}%` }} />
                    </div>
                  </div>
                </div>
              )
            })}
          </div>
        </div>

        <div className="flex flex-wrap gap-4 px-3 py-2 bg-gray-50 border-t border-gray-200 text-[11px] text-gray-500">
          <span className="flex items-center gap-1.5"><i className="inline-block w-4 h-2 rounded bg-navy/80" /> planned</span>
          <span className="flex items-center gap-1.5"><i className="inline-block w-4 h-1.5 rounded bg-gray-300" /> baseline</span>
          <span className="flex items-center gap-1.5"><span className="text-red-600 font-bold">▲</span> critical path</span>
          <span className="flex items-center gap-1.5"><i className="inline-block w-4 h-2 rounded bg-white/60 border border-gray-300" /> shaded portion = progress</span>
        </div>
      </div>

      {/* Dependencies */}
      <div>
        <h4 className="text-sm font-bold text-gray-800 mb-2">Dependencies</h4>
        <p className="text-xs text-gray-500 mb-3">
          Finish-to-start only: the successor cannot begin until the predecessor finishes.
        </p>

        {(data.dependencies ?? []).length === 0
          ? <p className="text-sm text-gray-400 mb-3">None yet — without links every milestone is independent and there's no critical path.</p>
          : (
            <ul className="space-y-1.5 mb-4">
              {data.dependencies.map(d => (
                <li key={d.id} className="flex items-center gap-2 text-sm">
                  <span className="font-medium">{byId[d.predecessorId]?.title ?? '—'}</span>
                  <span className="text-gray-400">→</span>
                  <span className="font-medium">{byId[d.successorId]?.title ?? '—'}</span>
                  {d.lagDays > 0 && <span className="text-xs text-gray-500">+{d.lagDays}d lag</span>}
                  {canWrite && (
                    <button onClick={() => removeLink(d.id)} disabled={busy}
                            className="ml-2 text-xs text-red-600 hover:underline disabled:opacity-50">remove</button>
                  )}
                </li>
              ))}
            </ul>
          )}

        {canWrite && (
          <div className="flex flex-wrap items-end gap-2">
            <label className="text-xs text-gray-500">
              <span className="block mb-1">After</span>
              <select value={link.predecessorId} onChange={e => setLink(l => ({ ...l, predecessorId: e.target.value }))}
                      className="input min-w-[190px]">
                <option value="">Select…</option>
                {items.map(i => <option key={i.id} value={i.id}>{i.title}</option>)}
              </select>
            </label>
            <label className="text-xs text-gray-500">
              <span className="block mb-1">start</span>
              <select value={link.successorId} onChange={e => setLink(l => ({ ...l, successorId: e.target.value }))}
                      className="input min-w-[190px]">
                <option value="">Select…</option>
                {items.map(i => <option key={i.id} value={i.id}>{i.title}</option>)}
              </select>
            </label>
            <label className="text-xs text-gray-500">
              <span className="block mb-1">Lag (days)</span>
              <input type="number" min="0" value={link.lagDays}
                     onChange={e => setLink(l => ({ ...l, lagDays: e.target.value }))} className="input w-24" />
            </label>
            <button onClick={addLink} disabled={busy}
                    className="px-4 py-2 bg-navy text-white text-sm font-semibold rounded-lg disabled:opacity-60">
              Link
            </button>
          </div>
        )}

        {linkError && (
          <div className="mt-3 px-3 py-2 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">
            {linkError}
          </div>
        )}
      </div>
    </div>
  )
}
