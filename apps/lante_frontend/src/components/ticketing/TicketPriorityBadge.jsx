const PRIORITY_STYLES = {
  Low:      'bg-gray-100 text-gray-600',
  Medium:   'bg-blue-100 text-blue-700',
  High:     'bg-orange-100 text-orange-700',
  Critical: 'bg-red-100 text-red-700',
}

const PRIORITY_DOTS = {
  Low:      'bg-gray-400',
  Medium:   'bg-blue-500',
  High:     'bg-orange-500',
  Critical: 'bg-red-500',
}

export default function TicketPriorityBadge({ priority }) {
  const label = typeof priority === 'number'
    ? ['Low','Medium','High','Critical'][priority] ?? 'Unknown'
    : priority ?? 'Unknown'

  const cls = PRIORITY_STYLES[label] ?? 'bg-gray-100 text-gray-600'
  const dot = PRIORITY_DOTS[label] ?? 'bg-gray-400'

  return (
    <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold ${cls}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${dot}`} />
      {label}
    </span>
  )
}
