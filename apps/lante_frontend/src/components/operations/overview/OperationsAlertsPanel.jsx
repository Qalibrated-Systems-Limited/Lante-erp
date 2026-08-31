import { useNavigate } from 'react-router-dom'
import { useOperationsAlerts } from '../../../hooks/operations/useOperationsAlerts.js'

const CARDS = [
  { key: 'budget',      icon: '💸', label: 'Projects over budget',      to: '/modules/operations/projects' },
  { key: 'calibration', icon: '⚖️', label: 'Standards due / expired',   to: '/modules/operations/reference-standards' },
  { key: 'negligence',  icon: '⚠️', label: 'Open negligence',           to: '/modules/operations/negligence' },
  { key: 'timesheets',  icon: '⏱️', label: 'Timesheets to approve',     to: '/modules/operations/timesheets' },
]

// O11.7 — "needs attention" strip on the operations overview. Each card counts a worker-engine
// outcome and links to its page; non-zero counts are highlighted amber/red.
export default function OperationsAlertsPanel() {
  const navigate = useNavigate()
  const { alerts, loading } = useOperationsAlerts()

  return (
    <div className="mb-6">
      <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wider mb-3">Needs Attention</h2>
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {CARDS.map(c => {
          const n = alerts[c.key] ?? 0
          const active = n > 0
          return (
            <button key={c.key} onClick={() => navigate(c.to)}
              className={`text-left rounded-xl border p-4 transition-colors ${active ? 'border-amber-300 bg-amber-50 hover:bg-amber-100' : 'border-gray-200 bg-white hover:bg-gray-50'}`}>
              <div className="flex items-center justify-between mb-1">
                <span className="text-xl">{c.icon}</span>
                <span className={`text-2xl font-extrabold ${active ? 'text-amber-700' : 'text-gray-300'}`}>
                  {loading ? '…' : n}
                </span>
              </div>
              <p className="text-xs font-semibold text-gray-500 leading-tight">{c.label}</p>
            </button>
          )
        })}
      </div>
    </div>
  )
}
