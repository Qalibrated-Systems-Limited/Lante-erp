// Small presentational pieces for the ticket detail page: the action modal, hero/meta bits,
// and the portal-submission description renderer.

export function ActionModal({ type, data, setData, onConfirm, onCancel, loading, error, departments = [], users = [] }) {
  const titles = {
    resolve:    'Resolve Ticket',
    assign:     'Assign to Technician (Creates Field Assignment)',
    escalate:   'Escalate Ticket',
    status:     `Change Status to ${data.newStatus ?? ''}`,
    close:      'Close Ticket',
    reopen:     'Reopen Ticket',
    department: 'Assign to Department',
  }

  function set(field, value) {
    setData(d => ({ ...d, [field]: value }))
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-bold text-navy mb-4">{titles[type]}</h2>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3 mb-4">
            {error}
          </div>
        )}

        {type === 'resolve' && (
          <div className="space-y-3">
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-1">Resolution notes</label>
              <textarea
                value={data.notes ?? ''}
                onChange={e => set('notes', e.target.value)}
                placeholder="What was done to resolve it…"
                rows={3}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
              />
            </div>
            {/* D3-3 — root cause is mandatory; the backend rejects a resolve without one. */}
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-1">Root cause <span className="text-red-500">*</span></label>
              <textarea
                value={data.rootCause ?? ''}
                onChange={e => set('rootCause', e.target.value)}
                placeholder="Why did it happen? (required)"
                rows={3}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
              />
            </div>
          </div>
        )}

        {type === 'assign' && (
          <div className="space-y-3">
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-1">Assign To</label>
              <select
                value={data.userId ?? ''}
                onChange={e => {
                  const selected = users.find(u => u.id === e.target.value)
                  setData(prev => ({
                    ...prev,
                    userId: e.target.value,
                    assigneeName: selected ? `${selected.firstName} ${selected.lastName}`.trim() : prev.assigneeName,
                    departmentId: selected?.departmentId ?? prev.departmentId ?? '',
                  }))
                }}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
              >
                <option value="">Select user…</option>
                {users.filter(u => u.isActive).map(u => (
                  <option key={u.id} value={u.id}>
                    {u.firstName} {u.lastName}{u.departmentName ? ` — ${u.departmentName}` : ''}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-1">Department (auto-filled from user)</label>
              <select
                value={data.departmentId ?? ''}
                onChange={e => setData(prev => ({ ...prev, departmentId: e.target.value }))}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
              >
                <option value="">Keep current department</option>
                {departments.map(d => (
                  <option key={d.id} value={d.id}>{d.name}</option>
                ))}
              </select>
            </div>
            <input
              type="text"
              value={data.notes ?? ''}
              onChange={e => setData(prev => ({ ...prev, notes: e.target.value }))}
              placeholder="Notes (optional)"
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
            />
          </div>
        )}

        {type === 'escalate' && (
          <div className="space-y-3">
            <select
              value={data.level ?? 0}
              onChange={e => set('level', e.target.value)}
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
            >
              <option value={0}>Supervisor</option>
              <option value={1}>Department Head</option>
              <option value={2}>MD</option>
            </select>
            <input
              type="text"
              value={data.userId ?? ''}
              onChange={e => set('userId', e.target.value)}
              placeholder="Escalate to User ID"
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
            />
            <textarea
              value={data.reason ?? ''}
              onChange={e => set('reason', e.target.value)}
              placeholder="Reason for escalation…"
              rows={3}
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
            />
          </div>
        )}

        {(type === 'status') && (
          <textarea
            value={data.notes ?? ''}
            onChange={e => set('notes', e.target.value)}
            placeholder="Notes (optional)…"
            rows={3}
            className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
          />
        )}

        {(type === 'close' || type === 'reopen') && (
          <p className="text-sm text-gray-500">
            Are you sure you want to {type} this ticket?
          </p>
        )}

        {type === 'department' && (
          <div className="space-y-3">
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-1">Department</label>
              <select
                value={data.departmentId ?? ''}
                onChange={e => set('departmentId', e.target.value)}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
              >
                <option value="">Select department…</option>
                {departments.map(d => (
                  <option key={d.id} value={d.id}>{d.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-1">Notes (optional)</label>
              <input
                type="text"
                value={data.notes ?? ''}
                onChange={e => set('notes', e.target.value)}
                placeholder="Reason for reassignment…"
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
              />
            </div>
            <p className="text-xs text-gray-400">
              The department manager, all admins, and the MD will be notified by email.
            </p>
          </div>
        )}

        <div className="flex items-center justify-end gap-3 mt-5">
          <button
            onClick={onCancel}
            className="px-4 py-2 text-sm font-medium text-gray-600 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={loading}
            className="px-5 py-2 bg-navy hover:bg-navy-dark disabled:opacity-50 text-white text-sm font-semibold rounded-lg transition-colors"
          >
            {loading ? 'Saving…' : 'Confirm'}
          </button>
        </div>
      </div>
    </div>
  )
}

export function HeroFact({ label, value, warn }) {
  return (
    <div className="min-w-0">
      <p className="text-[10px] font-semibold uppercase tracking-wider text-white/50 mb-0.5">{label}</p>
      <p className={`text-sm font-semibold truncate ${warn ? 'text-red-300' : 'text-white'}`}>{value}</p>
    </div>
  )
}

export function ActionBtn({ label, color, onClick }) {
  const colors = {
    blue:   'bg-blue-50 text-blue-700 hover:bg-blue-100',
    yellow: 'bg-yellow-50 text-yellow-700 hover:bg-yellow-100',
    green:  'bg-green-50 text-green-700 hover:bg-green-100',
    gray:   'bg-gray-50 text-gray-700 hover:bg-gray-100',
    orange: 'bg-orange-50 text-orange-700 hover:bg-orange-100',
    purple: 'bg-purple-50 text-purple-700 hover:bg-purple-100',
    indigo: 'bg-indigo-50 text-indigo-700 hover:bg-indigo-100',
    red:    'bg-red-50 text-red-700 hover:bg-red-100',
  }
  return (
    <button
      onClick={onClick}
      className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors ${colors[color] ?? colors.gray}`}
    >
      {label}
    </button>
  )
}

export function MetaRow({ label, value, warn }) {
  return (
    <div className="flex justify-between gap-2">
      <span className="text-xs text-gray-400 shrink-0">{label}</span>
      <span className={`text-xs font-medium text-right ${warn ? 'text-red-600' : 'text-gray-700'}`}>
        {value}
      </span>
    </div>
  )
}

const PORTAL_HEADER = '=== CLIENT PORTAL SUBMISSION ==='
const PORTAL_DIVIDER = '================================'

function parsePortalDescription(desc) {
  if (!desc || !desc.includes(PORTAL_HEADER)) return null
  const lines = desc.split('\n')
  const meta = {}
  let messageLines = []
  let inMessage = false
  for (const line of lines) {
    if (line.startsWith(PORTAL_HEADER) || line.startsWith(PORTAL_DIVIDER)) continue
    if (inMessage) {
      messageLines.push(line)
      continue
    }
    const colon = line.indexOf(':')
    if (colon > -1) {
      const key = line.slice(0, colon).trim()
      const val = line.slice(colon + 1).trim()
      if (['Name', 'Email', 'Company', 'Phone', 'Type'].includes(key)) {
        meta[key] = val === '—' ? '' : val
      } else {
        // Hit the message body separator — everything after is the message
        inMessage = true
        if (line.trim()) messageLines.push(line)
      }
    } else if (line.trim()) {
      inMessage = true
      messageLines.push(line)
    }
  }
  return { meta, message: messageLines.join('\n').trim() }
}

const TYPE_BADGE = {
  Complaint:      'bg-red-100 text-red-700',
  Feedback:       'bg-blue-100 text-blue-700',
  ProjectInquiry: 'bg-amber-100 text-amber-700',
  General:        'bg-gray-100 text-gray-600',
}

export function TicketDescription({ description }) {
  const portal = parsePortalDescription(description)

  if (!portal) {
    return (
      <p className="text-sm text-gray-600 leading-relaxed whitespace-pre-line">
        {description || <span className="text-gray-300 italic">No description provided.</span>}
      </p>
    )
  }

  const { meta, message } = portal
  const typeBadge = TYPE_BADGE[meta.Type] ?? 'bg-gray-100 text-gray-600'
  const typeLabel = meta.Type === 'ProjectInquiry' ? 'Project Inquiry' : (meta.Type || 'General')

  return (
    <div className="space-y-4">
      {/* Portal badge */}
      <div className="flex items-center gap-2">
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 bg-amber-50 border border-amber-200 text-amber-700 text-xs font-semibold rounded-full">
          <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3.055 11H5a2 2 0 012 2v1a2 2 0 002 2h2a2 2 0 002-2v-1a2 2 0 012-2h1.945M8 3.935V5.5A2.5 2.5 0 0010.5 8h3A2.5 2.5 0 0016 5.5V3.935M20 12a8 8 0 11-16 0 8 8 0 0116 0z" />
          </svg>
          Client Portal Submission
        </span>
        <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${typeBadge}`}>
          {typeLabel}
        </span>
      </div>

      {/* Client info card */}
      <div className="bg-gray-50 rounded-xl border border-gray-200 overflow-hidden">
        <div className="px-4 py-3 border-b border-gray-200 bg-white">
          <p className="text-xs font-bold text-navy uppercase tracking-wider">Client Information</p>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-0">
          {[
            { icon: '👤', label: 'Name',    val: meta.Name },
            { icon: '✉️', label: 'Email',   val: meta.Email,   link: meta.Email ? `mailto:${meta.Email}` : null },
            { icon: '🏢', label: 'Company', val: meta.Company  },
            { icon: '📞', label: 'Phone',   val: meta.Phone,   link: meta.Phone ? `tel:${meta.Phone}` : null },
          ].map(({ icon, label, val, link }) => val ? (
            <div key={label} className="flex items-start gap-3 px-4 py-3 border-b border-gray-100 last:border-0 sm:odd:border-r">
              <span className="text-base shrink-0">{icon}</span>
              <div className="min-w-0">
                <p className="text-xs text-gray-400 font-medium">{label}</p>
                {link
                  ? <a href={link} className="text-sm font-semibold text-navy hover:underline truncate block">{val}</a>
                  : <p className="text-sm font-semibold text-gray-800 truncate">{val}</p>
                }
              </div>
            </div>
          ) : null)}
        </div>
      </div>

      {/* Message */}
      {message && (
        <div>
          <p className="text-xs font-bold text-navy uppercase tracking-wider mb-2">Message</p>
          <p className="text-sm text-gray-700 leading-relaxed whitespace-pre-line bg-white border border-gray-200 rounded-xl px-4 py-3">
            {message}
          </p>
        </div>
      )}
    </div>
  )
}
