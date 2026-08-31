import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'
import * as ops from '../../services/operations.js'

// PR4c — who is carrying what, across every live project.
//
// A ranked horizontal bar per person rather than a chart library: this is a magnitude comparison of a
// single measure over a handful of rows, where the bar IS the chart and every value is worth labelling
// directly. Capacity is drawn as a reference mark, not a second series — two scales on one axis is the
// thing that makes workload views lie.

const HOURS_PER_DAY = 8

const fmtDate = (d) => new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short' })
const iso = (d) => d.toISOString().slice(0, 10)

export default function WorkloadPage() {
  const today = new Date()
  const [from, setFrom] = useState(iso(today))
  const [to, setTo] = useState(iso(new Date(today.getTime() + 27 * 864e5)))
  const [data, setData] = useState(null)
  const [users, setUsers] = useState({})
  const [err, setErr] = useState('')

  const load = useCallback(async () => {
    setErr('')
    try { setData(await ops.getWorkload(from, to)) }
    catch (e) { setErr(e?.response?.data?.message || 'Could not load workload.') }
  }, [from, to])

  useEffect(() => { load() }, [load])

  useEffect(() => {
    api.get('/api/v1/users?pageSize=200').then(res => {
      const d = res.data?.data
      const list = Array.isArray(d) ? d : (d?.items ?? [])
      setUsers(Object.fromEntries(list.map(u => [
        u.id, [u.firstName, u.lastName].filter(Boolean).join(' ') || u.email || u.id,
      ])))
    }).catch(() => {})
  }, [])

  const capacity = (data?.workingDays ?? 0) * HOURS_PER_DAY
  const peak = Math.max(capacity, ...(data?.rows ?? []).map(r => r.estimatedHours || 0), 1)

  return (
    <main className="flex-1 max-w-5xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
      <div className="mb-6">
        <h1 className="text-2xl font-extrabold text-zinc-950">Workload</h1>
        <p className="text-sm text-gray-500 mt-1">
          Open tasks per person across active projects. A task counts if any part of it falls in the
          window, so long-running work still shows against the person carrying it.
        </p>
      </div>

      <div className="flex items-end gap-3 flex-wrap mb-5">
        <label className="text-[11px] font-semibold text-gray-500">From
          <input type="date" className="input mt-0.5" value={from} onChange={e => setFrom(e.target.value)} />
        </label>
        <label className="text-[11px] font-semibold text-gray-500">To
          <input type="date" className="input mt-0.5" value={to} onChange={e => setTo(e.target.value)} />
        </label>
        {data && (
          <p className="text-xs text-gray-400 pb-2">
            {data.workingDays} working days · nominal capacity {capacity}h per person
          </p>
        )}
      </div>

      {err && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2.5 mb-4">{err}</div>}
      {!data && !err && <p className="text-sm text-gray-400">Loading…</p>}

      {data && (data.unassignedCount > 0) && (
        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 mb-4">
          <p className="text-xs text-amber-900">
            ⚠ <strong>{data.unassignedCount} task(s)</strong> in this window have nobody assigned
            {data.unassignedHours > 0 && ` (${data.unassignedHours}h estimated)`}. Unowned work is the
            load this view cannot show you.
          </p>
        </div>
      )}

      {data && data.rows.length === 0 && (
        <div className="text-center py-10 border border-dashed border-gray-200 rounded-lg">
          <div className="text-2xl mb-1">👥</div>
          <p className="text-sm font-semibold text-gray-700">Nobody has open work in this window</p>
        </div>
      )}

      <div className="space-y-3">
        {(data?.rows ?? []).map(r => {
          const hours = r.estimatedHours || 0
          const pct = Math.min(100, (hours / peak) * 100)
          const capPct = Math.min(100, (capacity / peak) * 100)
          const over = capacity > 0 && hours > capacity
          return (
            <div key={r.userId} className="border border-gray-200 rounded-lg px-4 py-3">
              <div className="flex items-baseline justify-between gap-3 flex-wrap">
                <span className="text-sm font-semibold text-gray-900">{users[r.userId] ?? `${r.userId.slice(0, 8)}…`}</span>
                <span className="text-xs text-gray-500 tabular-nums">
                  {hours}h · {r.taskCount} task(s) · {r.projectCount} project(s)
                </span>
              </div>

              <div className="relative h-2.5 bg-gray-100 rounded mt-2">
                <div className={`h-2.5 rounded ${over ? 'bg-red-500' : 'bg-blue-500'}`} style={{ width: `${pct}%` }} />
                {capacity > 0 && (
                  <div className="absolute top-[-3px] bottom-[-3px] w-px bg-gray-500" style={{ left: `${capPct}%` }}
                       title={`Nominal capacity ${capacity}h`} />
                )}
              </div>

              <div className="flex items-center gap-3 flex-wrap mt-1.5">
                {/* Over-capacity is stated, not just coloured red. */}
                {over && <span className="text-[11px] font-semibold text-red-700">⚠ over nominal capacity</span>}
                {r.overdueCount > 0 && <span className="text-[11px] font-semibold text-red-700">{r.overdueCount} overdue</span>}
                {r.unestimatedCount > 0 && (
                  <span className="text-[11px] text-amber-700">
                    {r.unestimatedCount} task(s) with no estimate — the bar understates this
                  </span>
                )}
                <span className="text-[11px] text-gray-400 truncate">{r.projectNames.join(' · ')}</span>
              </div>
            </div>
          )
        })}
      </div>
    </main>
  )
}
