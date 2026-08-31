// Customer Feedback tab for the ticketing page — CSAT summary + D6-3 CS-overview panels.

const RATING_LABEL = { 5: 'Excellent', 4: 'Good', 3: 'Average', 2: 'Poor', 1: 'Very Poor' }
const RATING_COLOR = { 5: 'text-green-600', 4: 'text-green-600', 3: 'text-amber-600', 2: 'text-red-600', 1: 'text-red-600' }

function Stars({ n }) {
  return (
    <span className="whitespace-nowrap" title={`${n}/5`}>
      {[1, 2, 3, 4, 5].map(i => (
        <span key={i} className={i <= n ? 'text-gold' : 'text-gray-300'}>★</span>
      ))}
    </span>
  )
}

export function CustomerFeedback({ data, loading, onExport, exporting, canExport }) {
  if (loading || !data) {
    return (
      <div className="space-y-3">
        {[1, 2, 3].map(i => <div key={i} className="h-24 bg-white rounded-xl border border-gray-100 animate-pulse" />)}
      </div>
    )
  }
  if (data.total === 0) {
    return (
      <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
        <div className="text-4xl mb-3">⭐</div>
        <h3 className="font-semibold text-gray-700">No feedback yet</h3>
        <p className="text-sm text-gray-400 mt-1">Ratings appear here once customers rate resolved tickets.</p>
      </div>
    )
  }
  const maxBar = Math.max(...Object.values(data.dist), 1)
  return (
    <div className="space-y-6">
      {/* Summary KPIs */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-4">
          <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider">Average Rating</p>
          <div className="flex items-baseline gap-2 mt-1">
            <p className="text-2xl font-extrabold text-navy">{data.avg.toFixed(1)}</p>
            <span className="text-sm"><Stars n={Math.round(data.avg)} /></span>
          </div>
        </div>
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-4">
          <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider">Good / Excellent</p>
          <p className="text-2xl font-extrabold text-green-600 mt-1">{data.goodPct}%</p>
        </div>
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-4">
          <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider">Total Ratings</p>
          <p className="text-2xl font-extrabold text-navy mt-1">{data.total}</p>
        </div>
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-4">
          <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider">Low Ratings (≤2)</p>
          <p className="text-2xl font-extrabold text-red-600 mt-1">{(data.dist[1] ?? 0) + (data.dist[2] ?? 0)}</p>
        </div>
      </div>

      {/* Distribution */}
      <div className="bg-white rounded-xl border border-gray-200 p-5">
        <h3 className="font-bold text-navy mb-4">Rating Distribution</h3>
        <div className="space-y-2">
          {[5, 4, 3, 2, 1].map(n => (
            <div key={n} className="flex items-center gap-3">
              <span className="w-20 text-xs font-semibold text-gray-600">{n} ★ <span className="text-gray-400 font-normal">{RATING_LABEL[n]}</span></span>
              <div className="flex-1 h-2.5 bg-lgrey rounded-full overflow-hidden">
                <div className={`h-full ${n >= 4 ? 'bg-green-500' : n === 3 ? 'bg-amber-500' : 'bg-red-500'}`} style={{ width: `${((data.dist[n] ?? 0) / maxBar) * 100}%` }} />
              </div>
              <span className="w-8 text-right text-xs font-semibold text-gray-700">{data.dist[n] ?? 0}</span>
            </div>
          ))}
        </div>
      </div>

      {/* D6-3 — CS dashboard panels */}
      {data.overview && (
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h3 className="font-bold text-navy mb-4">Customer-Service Overview</h3>
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
            <div className="rounded-xl bg-lgrey/40 p-3">
              <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider">Open Tickets</p>
              <p className="text-2xl font-extrabold text-navy mt-1">{data.overview.openTotal ?? 0}</p>
            </div>
            <div className="rounded-xl bg-lgrey/40 p-3">
              <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider">SLA Compliance</p>
              <p className="text-2xl font-extrabold text-green-600 mt-1">{Math.round(data.overview.slaCompliancePct ?? 0)}%</p>
            </div>
            <div className="rounded-xl bg-lgrey/40 p-3">
              <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider">Avg Resolution</p>
              <p className="text-2xl font-extrabold text-navy mt-1">{(data.overview.avgResolutionHours ?? 0).toFixed(1)}<span className="text-sm font-semibold text-gray-400"> h</span></p>
            </div>
            <div className="rounded-xl bg-lgrey/40 p-3">
              <p className="text-[11px] text-gray-500 font-semibold uppercase tracking-wider">Survey Response</p>
              <p className="text-2xl font-extrabold text-navy mt-1">{data.responseRate ?? 0}%</p>
              <p className="text-[11px] text-gray-400">{data.overview.surveyResponses ?? 0}/{data.overview.surveysSent ?? 0} sent</p>
            </div>
          </div>
          {(data.overview.openByCategory ?? []).length > 0 && (
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2">Open by Category</p>
              <div className="space-y-1.5">
                {data.overview.openByCategory.map(c => (
                  <div key={c.categoryId} className="flex items-center justify-between text-sm">
                    <span className="text-gray-700 truncate">{c.categoryName}</span>
                    <span className="text-gray-500">
                      {c.open} open
                      {c.agingOver72h > 0 && <span className="text-red-600 font-semibold"> · {c.agingOver72h} aging &gt;72h</span>}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Feedback list */}
      <div className="flex items-center justify-between">
        <h3 className="font-bold text-navy">Recent Feedback</h3>
        {canExport && (
          <button
            onClick={() => onExport('pdf')}
            disabled={exporting}
            className="inline-flex items-center gap-2 px-4 py-2 border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-lg transition-colors disabled:opacity-50"
          >
            <span className="text-red-500 font-bold text-xs">PDF</span> Export (with ratings)
          </button>
        )}
      </div>
      <div className="grid gap-4" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(330px, 1fr))' }}>
        {data.rated.map(f => (
          <div key={f.ticketId} className="bg-white rounded-xl border border-gray-200 p-5">
            <div className="flex items-start justify-between gap-3 mb-2">
              <p className="font-bold text-navy leading-snug truncate">{f.title}</p>
              <span className="text-sm"><Stars n={f.rating} /></span>
            </div>
            <p className="text-xs text-gray-500 mb-2">
              <span className="font-mono">#{f.ticketId?.slice(0, 8).toUpperCase()}</span> · <span className={`font-semibold ${RATING_COLOR[f.rating]}`}>{RATING_LABEL[f.rating]}</span>
              {f.submittedAt ? ` · ${new Date(f.submittedAt).toLocaleDateString()}` : ''}
            </p>
            {f.comment && <p className="text-sm text-gray-700 italic">“{f.comment}”</p>}
          </div>
        ))}
      </div>
    </div>
  )
}
