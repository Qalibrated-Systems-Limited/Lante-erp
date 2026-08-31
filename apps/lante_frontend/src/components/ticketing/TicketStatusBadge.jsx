const STATUS_STYLES = {
  New:        'bg-blue-100 text-blue-700',
  Assigned:   'bg-purple-100 text-purple-700',
  InProgress: 'bg-amber-100 text-amber-700',
  Pending:    'bg-yellow-100 text-yellow-700',
  Escalated:  'bg-red-100 text-red-700',
  Resolved:   'bg-green-100 text-green-700',
  Closed:     'bg-gray-100 text-gray-600',
  Reopened:   'bg-orange-100 text-orange-700',
}

export default function TicketStatusBadge({ status }) {
  const label = typeof status === 'number'
    ? ['New','Assigned','InProgress','Pending','Escalated','Resolved','Closed','Reopened'][status] ?? 'Unknown'
    : status ?? 'Unknown'

  const cls = STATUS_STYLES[label] ?? 'bg-gray-100 text-gray-600'

  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ${cls}`}>
      {label}
    </span>
  )
}
