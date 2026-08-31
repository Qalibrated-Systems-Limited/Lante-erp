import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useNotifications } from '../../context/NotificationContext.jsx'

const TYPE_LABELS = {
  assigned:         'Assigned to you',
  status_changed:   'Status changed',
  comment_added:    'New comment',
  escalated:        'Escalated',
  resolved:         'Resolved',
  platform_broadcast: 'Announcement',
}

const TYPE_COLORS = {
  assigned:         'bg-blue-100 text-blue-700',
  status_changed:   'bg-purple-100 text-purple-700',
  comment_added:    'bg-gray-100 text-gray-700',
  escalated:        'bg-red-100 text-red-700',
  resolved:         'bg-green-100 text-green-700',
  platform_broadcast: 'bg-amber-100 text-amber-700',
}

function fmtRelative(dateStr) {
  const diff = Date.now() - new Date(dateStr).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  const days = Math.floor(hrs / 24)
  return `${days}d ago`
}

const FILTERS = [
  { key: 'all',    label: 'All' },
  { key: 'unread', label: 'Unread' },
  { key: 'read',   label: 'Read' },
]

export default function NotificationsPage() {
  const { notifications, markRead, markUnread, markAllRead, unreadCount } = useNotifications()
  const navigate = useNavigate()
  const [filter, setFilter] = useState('all')

  const filtered = useMemo(() => {
    if (filter === 'unread') return notifications.filter(n => !n.isRead)
    if (filter === 'read')   return notifications.filter(n => n.isRead)
    return notifications
  }, [notifications, filter])

  function handleClick(n) {
    if (!n.isRead) markRead(n.id)
    if (n.ticketId) navigate(`/modules/ticketing/${n.ticketId}`)
  }

  function toggleRead(e, n) {
    e.stopPropagation()
    if (n.isRead) markUnread(n.id)
    else markRead(n.id)
  }

  return (
    <>
      <div className="max-w-2xl mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Notifications</h1>
            {unreadCount > 0 && (
              <p className="text-sm text-gray-500 mt-0.5">{unreadCount} unread</p>
            )}
          </div>
          {unreadCount > 0 && (
            <button
              onClick={markAllRead}
              className="text-sm text-amber-600 hover:text-amber-700 font-medium"
            >
              Mark all as read
            </button>
          )}
        </div>

        {/* Filter tabs */}
        <div className="flex items-center gap-1 mb-6 border-b border-gray-200">
          {FILTERS.map(f => (
            <button
              key={f.key}
              onClick={() => setFilter(f.key)}
              className={`px-3 py-2 text-sm font-medium border-b-2 -mb-px transition-colors
                ${filter === f.key ? 'border-amber-500 text-amber-700' : 'border-transparent text-gray-500 hover:text-gray-700'}`}
            >
              {f.label}
            </button>
          ))}
        </div>

        {/* List */}
        {filtered.length === 0 ? (
          <div className="text-center py-20">
            <div className="w-16 h-16 rounded-full bg-gray-100 flex items-center justify-center mx-auto mb-4">
              <svg className="w-8 h-8 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                  d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
                />
              </svg>
            </div>
            <p className="text-gray-500 font-medium">
              {filter === 'unread' ? 'No unread notifications' : filter === 'read' ? 'No read notifications' : 'No notifications'}
            </p>
            <p className="text-gray-400 text-sm mt-1">You'll be notified when tickets are assigned, updated, or commented on.</p>
          </div>
        ) : (
          <div className="space-y-2">
            {filtered.map(n => (
              <div
                key={n.id}
                onClick={() => handleClick(n)}
                className={`w-full text-left rounded-xl border px-4 py-4 flex items-start gap-3 transition-colors hover:bg-gray-50 cursor-pointer
                  ${!n.isRead ? 'bg-amber-50 border-amber-200' : 'bg-white border-gray-200'}`}
              >
                {/* Unread dot */}
                <div className="mt-1.5 shrink-0">
                  {!n.isRead
                    ? <div className="w-2 h-2 rounded-full bg-amber-500" />
                    : <div className="w-2 h-2 rounded-full bg-transparent" />
                  }
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap mb-1">
                    <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${TYPE_COLORS[n.type] ?? 'bg-gray-100 text-gray-600'}`}>
                      {TYPE_LABELS[n.type] ?? n.type}
                    </span>
                    <span className="text-xs text-gray-400 ml-auto">{fmtRelative(n.createdAt)}</span>
                  </div>
                  <p className={`text-sm truncate ${!n.isRead ? 'font-semibold text-gray-900' : 'font-medium text-gray-700'}`}>
                    {n.title}
                  </p>
                  <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{n.message}</p>
                  {n.ticketTitle && (
                    <p className="text-xs text-gray-400 mt-1 truncate">Ticket: {n.ticketTitle}</p>
                  )}
                </div>

                <button
                  onClick={(e) => toggleRead(e, n)}
                  title={n.isRead ? 'Mark as unread' : 'Mark as read'}
                  className="shrink-0 mt-1 text-xs font-medium text-gray-400 hover:text-amber-600 transition-colors whitespace-nowrap"
                >
                  {n.isRead ? 'Mark unread' : 'Mark read'}
                </button>
              </div>
            ))}
          </div>
        )}
      </div>
    </>
  )
}
