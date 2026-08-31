const COLORS = {
  Pending:    'bg-gray-50 text-gray-600 border-gray-200',
  Accepted:   'bg-blue-50 text-blue-700 border-blue-200',
  Declined:   'bg-red-50 text-red-700 border-red-200',
  InProgress: 'bg-amber-50 text-amber-700 border-amber-200',
  Completed:  'bg-green-50 text-green-700 border-green-200',
  Cancelled:  'bg-red-50 text-red-600 border-red-200',
}

const LABELS = {
  Pending:    'Pending',
  Accepted:   'Accepted',
  Declined:   'Declined',
  InProgress: 'In Progress',
  Completed:  'Completed',
  Cancelled:  'Cancelled',
}

export default function AssignmentStatusBadge({ status }) {
  const cls = COLORS[status] ?? 'bg-gray-50 text-gray-600 border-gray-200'
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border ${cls}`}>
      {LABELS[status] ?? status}
    </span>
  )
}
