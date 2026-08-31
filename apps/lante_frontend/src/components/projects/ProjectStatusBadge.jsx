// Mirrors OperationsService.Core.Enums.ProjectStatus exactly (9 values — note the two
// distinct pending-approval states, not a single "PendingApproval").
const STYLES = {
  Draft:                  'bg-gray-100 text-gray-600',
  Planning:               'bg-blue-100 text-blue-700',
  PendingMdApproval:      'bg-amber-100 text-amber-700',
  PendingFinanceApproval: 'bg-amber-100 text-amber-700',
  Active:                 'bg-green-100 text-green-700',
  OnHold:                 'bg-orange-100 text-orange-700',
  Completed:              'bg-purple-100 text-purple-700',
  Closed:                 'bg-zinc-100 text-zinc-950',
  Cancelled:              'bg-red-100 text-red-700',
}

const LABELS = ['Draft','Planning','PendingMdApproval','PendingFinanceApproval','Active','OnHold','Completed','Closed','Cancelled']
const DISPLAY = { PendingMdApproval: 'Pending MD Approval', PendingFinanceApproval: 'Pending Finance Approval' }

export default function ProjectStatusBadge({ status }) {
  const label = typeof status === 'number' ? (LABELS[status] ?? 'Unknown') : (status ?? 'Unknown')
  const cls = STYLES[label] ?? 'bg-gray-100 text-gray-600'
  const display = DISPLAY[label] ?? label
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ${cls}`}>
      {display}
    </span>
  )
}
