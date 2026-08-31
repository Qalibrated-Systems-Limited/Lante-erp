import { useState, useEffect, useCallback } from 'react'
import Chart from 'react-apexcharts'
import * as ops from '../../../services/operations.js'

// PR4a — earned value and the S-curve.
//
// Three series, assigned in fixed order and never cycled: planned, earned, actual. The palette is the
// validated categorical set (all-pairs CVD ΔE 9.2, normal-vision 24.0 on a light surface). Aqua sits
// below 3:1 against white, so the relief rule applies — hence the always-present legend, the direct
// end labels, and the table view below the chart. Identity is never carried by colour alone.
const SERIES = {
  pv: { key: 'pv', label: 'Planned value', color: '#2a78d6' },
  ev: { key: 'ev', label: 'Earned value',  color: '#eb6834' },
  ac: { key: 'ac', label: 'Actual cost',   color: '#1baf7a' },
}

const kes0 = (v) => `KES ${Number(v ?? 0).toLocaleString('en-KE', { maximumFractionDigits: 0 })}`
const short = (v) => {
  const n = Number(v ?? 0)
  if (Math.abs(n) >= 1e6) return `${(n / 1e6).toFixed(1)}M`
  if (Math.abs(n) >= 1e3) return `${Math.round(n / 1e3)}k`
  return String(Math.round(n))
}
const fmtDate = (d) => new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: '2-digit' })

// An index is good above 1 and bad below it, but the number alone does not say which — so every tile
// ships the plain-language verdict the API computed, and a shape, not just a colour.
function IndexTile({ label, value, verdict, hint }) {
  const n = value == null ? null : Number(value)
  const tone = n == null ? 'text-gray-500'
    : n >= 0.995 ? 'text-emerald-700' : n >= 0.9 ? 'text-amber-700' : 'text-red-700'
  const mark = n == null ? '' : n >= 0.995 ? '▲' : '▼'
  return (
    <div className="border border-gray-200 rounded-lg px-4 py-3">
      <p className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">{label}</p>
      <p className={`text-2xl font-bold mt-0.5 tabular-nums ${tone}`}>
        {n == null ? '—' : n.toFixed(2)} <span className="text-sm">{mark}</span>
      </p>
      <p className="text-[11px] text-gray-500 mt-0.5">{verdict}</p>
      {hint && <p className="text-[11px] text-gray-400 mt-0.5">{hint}</p>}
    </div>
  )
}

function MoneyTile({ label, value, sub, tone }) {
  const toneCls = tone === 'bad' ? 'text-red-700' : tone === 'good' ? 'text-emerald-700' : 'text-gray-900'
  return (
    <div className="border border-gray-200 rounded-lg px-4 py-3">
      <p className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">{label}</p>
      <p className={`text-lg font-bold mt-0.5 tabular-nums ${toneCls}`}>{kes0(value)}</p>
      {sub && <p className="text-[11px] text-gray-400 mt-0.5">{sub}</p>}
    </div>
  )
}

export default function EarnedValueTab({ projectId }) {
  const [data, setData] = useState(null)
  const [err, setErr] = useState('')
  const [showTable, setShowTable] = useState(false)

  const load = useCallback(async () => {
    try { setData(await ops.getProjectEvm(projectId)) }
    catch { setErr('Could not load earned value.') }
  }, [projectId])

  useEffect(() => { load() }, [load])

  if (err) return <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2.5">{err}</div>
  if (!data) return <p className="text-sm text-gray-400 py-6">Loading…</p>

  const m = data.metrics ?? {}
  const curve = data.sCurve ?? []
  // EV and AC stop at today; carrying them forward as zero would draw both series diving to the floor.
  const actual = curve.filter(p => p.isActual)

  const series = [
    { name: SERIES.pv.label, data: curve.map(p => ({ x: new Date(p.date).getTime(), y: Number(p.pv) })) },
    { name: SERIES.ev.label, data: actual.map(p => ({ x: new Date(p.date).getTime(), y: Number(p.ev) })) },
    { name: SERIES.ac.label, data: actual.map(p => ({ x: new Date(p.date).getTime(), y: Number(p.ac) })) },
  ]

  const options = {
    chart: { type: 'line', toolbar: { show: false }, zoom: { enabled: false }, fontFamily: 'inherit' },
    colors: [SERIES.pv.color, SERIES.ev.color, SERIES.ac.color],
    stroke: { width: 2, curve: 'smooth' },          // thin marks
    // A line through one point draws nothing, and early in a project the earned/actual series often
    // have exactly one point — today. Show markers until the series is dense enough to read as a line.
    markers: { size: actual.length <= 2 ? 5 : 0, hover: { size: 6 } },
    dataLabels: { enabled: false },                  // never a number on every point
    grid: { borderColor: '#f1f1ef', strokeDashArray: 3 },
    legend: { position: 'top', horizontalAlign: 'left', markers: { radius: 3 }, fontSize: '12px' },
    xaxis: {
      type: 'datetime',
      axisBorder: { show: false }, axisTicks: { show: false },
      labels: { style: { colors: '#8a8a85', fontSize: '11px' } },
    },
    yaxis: {
      labels: { formatter: short, style: { colors: '#8a8a85', fontSize: '11px' } },
    },
    tooltip: {
      shared: true, intersect: false,               // crosshair + shared tooltip
      x: { format: 'dd MMM yyyy' },
      y: { formatter: (v) => (v == null ? '—' : kes0(v)) },
    },
    annotations: {
      xaxis: [{
        x: new Date(m.asOf).getTime(),
        borderColor: '#c9c9c4',
        strokeDashArray: 4,
        label: { text: 'today', style: { fontSize: '10px', color: '#6b6b66', background: '#f7f7f5' } },
      }],
    },
  }

  return (
    <div className="space-y-5">
      {(m.caveats ?? []).length > 0 && (
        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 space-y-1">
          {m.caveats.map((c, i) => <p key={i} className="text-xs text-amber-900">⚠ {c}</p>)}
        </div>
      )}

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <IndexTile label="Schedule index (SPI)" value={m.spi} verdict={m.scheduleVerdict} hint="Earned ÷ planned" />
        <IndexTile label="Cost index (CPI)" value={m.cpi} verdict={m.costVerdict} hint="Earned ÷ spent" />
        <MoneyTile label="Forecast at completion" value={m.eac}
                   sub={m.eac == null ? 'Needs some spend to forecast' : `Budget ${kes0(m.bac)}`} />
        <MoneyTile label="Forecast variance" value={m.vac}
                   tone={m.vac == null ? undefined : m.vac < 0 ? 'bad' : 'good'}
                   sub={m.vac == null ? '—' : m.vac < 0 ? 'Forecast to overrun' : 'Forecast to come in under'} />
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <MoneyTile label="Budget (BAC)" value={m.bac}
                   sub={m.isBaselined ? 'Approved baseline' : 'Planned budget — not baselined'} />
        <MoneyTile label="Earned (EV)" value={m.ev} sub={`${m.percentComplete ?? 0}% of milestone value`} />
        <MoneyTile label="Spent (AC)" value={m.ac} sub={`${m.percentSpent ?? 0}% of budget`} />
        <MoneyTile label="Still to spend (ETC)" value={m.etc} sub="Forecast minus spent" />
      </div>

      <div className="border border-gray-200 rounded-lg p-4">
        <div className="flex items-center justify-between gap-3 flex-wrap mb-2">
          <div>
            <h3 className="text-sm font-semibold text-gray-900">S-curve</h3>
            <p className="text-[11px] text-gray-400">
              Planned value runs the full baseline; earned and actual stop at today. Historical earned
              value counts signed-off milestones — progress percentages are not versioned, so partial
              progress only appears at today's point.
            </p>
          </div>
          <button onClick={() => setShowTable(t => !t)}
                  className="text-xs font-semibold text-gray-500 hover:text-gray-800 border border-gray-200 rounded px-2 py-1">
            {showTable ? 'Hide table' : 'View as table'}
          </button>
        </div>

        {curve.length === 0 ? (
          <p className="text-xs text-gray-400 py-8 text-center">
            No priced, baselined milestones yet — there is nothing to plot.
          </p>
        ) : (
          <>
            <Chart options={options} series={series} type="line" height={300} />

            {/* Direct end labels: the relief the palette's contrast warning requires, and they let
                the three curves be told apart without relying on the legend swatch colour. */}
            <div className="flex flex-wrap gap-x-5 gap-y-1 mt-1">
              {[SERIES.pv, SERIES.ev, SERIES.ac].map(s => {
                const last = (s.key === 'pv' ? curve : actual).at(-1)
                return (
                  <span key={s.key} className="inline-flex items-center gap-1.5 text-[11px] text-gray-600">
                    <span className="inline-block w-2.5 h-2.5 rounded-sm" style={{ background: s.color }} />
                    <span className="font-semibold">{s.label}</span>
                    <span className="tabular-nums text-gray-500">{kes0(last?.[s.key])}</span>
                  </span>
                )
              })}
            </div>
          </>
        )}

        {showTable && curve.length > 0 && (
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-xs min-w-[420px]">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  {['Date', 'Planned', 'Earned', 'Actual'].map(h => (
                    <th key={h} className="text-left px-3 py-2 font-semibold text-gray-600">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {curve.map(p => (
                  <tr key={p.date}>
                    <td className="px-3 py-1.5 text-gray-700">{fmtDate(p.date)}</td>
                    <td className="px-3 py-1.5 tabular-nums text-gray-700">{kes0(p.pv)}</td>
                    <td className="px-3 py-1.5 tabular-nums text-gray-700">{p.isActual ? kes0(p.ev) : '—'}</td>
                    <td className="px-3 py-1.5 tabular-nums text-gray-700">{p.isActual ? kes0(p.ac) : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
