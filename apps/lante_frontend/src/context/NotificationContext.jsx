import { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react'
import api from '../api/axios.js'
import { useAuth } from './AuthContext.jsx'

const NotificationContext = createContext(null)

const POLL_INTERVAL = 30_000 // 30 seconds

export function NotificationProvider({ children }) {
  const { isAuthenticated } = useAuth()
  const [notifications, setNotifications] = useState([])
  const [unreadCount, setUnreadCount] = useState(0)
  const timerRef = useRef(null)

  const fetchNotifications = useCallback(async () => {
    if (!isAuthenticated) return
    try {
      const [listRes, countRes] = await Promise.all([
        api.get('/api/v1/notifications'),
        api.get('/api/v1/notifications/unread-count'),
      ])
      const items = listRes.data?.data ?? []
      setNotifications(items)
      setUnreadCount(countRes.data?.data?.count ?? 0)
    } catch {
      // Silently fail — notifications are non-critical
    }
  }, [isAuthenticated])

  useEffect(() => {
    // Clear on every auth transition, not just logout — this SPA never full-page-reloads on
    // login/logout, so without this, User A's already-fetched notifications stay sitting in
    // this context's state and render under User B's session the moment they log in on the
    // same tab, for however long it takes the next poll to overwrite them (or indefinitely, if
    // that poll ever fails, since the fetch's catch block never clears anything either).
    setNotifications([])
    setUnreadCount(0)
    if (!isAuthenticated) return
    fetchNotifications()
    timerRef.current = setInterval(fetchNotifications, POLL_INTERVAL)
    return () => clearInterval(timerRef.current)
  }, [isAuthenticated, fetchNotifications])

  const markRead = useCallback(async (id) => {
    try {
      await api.patch(`/api/v1/notifications/${id}/read`)
      setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n))
      setUnreadCount(prev => Math.max(0, prev - 1))
    } catch {
      // ignore
    }
  }, [])

  const markUnread = useCallback(async (id) => {
    try {
      await api.patch(`/api/v1/notifications/${id}/unread`)
      setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: false } : n))
      setUnreadCount(prev => prev + 1)
    } catch {
      // ignore
    }
  }, [])

  const markAllRead = useCallback(async () => {
    try {
      await api.patch('/api/v1/notifications/read-all')
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })))
      setUnreadCount(0)
    } catch {
      // ignore
    }
  }, [])

  return (
    <NotificationContext.Provider value={{ notifications, unreadCount, markRead, markAllRead, refresh: fetchNotifications }}>
      {children}
    </NotificationContext.Provider>
  )
}

export function useNotifications() {
  const ctx = useContext(NotificationContext)
  if (!ctx) throw new Error('useNotifications must be used within NotificationProvider')
  return ctx
}
