import { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react'
import api from '../api/axios.js'
import { useAuth } from './AuthContext.jsx'

const AlertContext = createContext(null)

const POLL_INTERVAL = 30_000 // 30 seconds

export function AlertProvider({ children }) {
  const { isAuthenticated } = useAuth()
  const [openAlerts, setOpenAlerts] = useState([])
  const timerRef = useRef(null)

  const fetchAlerts = useCallback(async () => {
    if (!isAuthenticated) return
    try {
      const res = await api.get('/api/v1/alerts')
      setOpenAlerts(res.data?.data ?? [])
    } catch {
      // Silently fail — alerts are non-critical
    }
  }, [isAuthenticated])

  useEffect(() => {
    // Clear on every auth transition, not just logout — this SPA never full-page-reloads on
    // login/logout, so without this, User A's already-fetched alerts stay sitting in this
    // context's state and render under User B's session the moment they log in on the same
    // tab, for however long it takes the next poll to overwrite them (or indefinitely, if that
    // poll ever fails, since the fetch's catch block never clears anything either).
    setOpenAlerts([])
    if (!isAuthenticated) return
    fetchAlerts()
    timerRef.current = setInterval(fetchAlerts, POLL_INTERVAL)
    return () => clearInterval(timerRef.current)
  }, [isAuthenticated, fetchAlerts])

  const markSeen = useCallback(async (id) => {
    try {
      await api.patch(`/api/v1/alerts/${id}/seen`)
      setOpenAlerts(prev => prev.map(a => a.id === id ? { ...a, isSeen: true } : a))
    } catch {
      // ignore
    }
  }, [])

  const acknowledge = useCallback(async (id) => {
    try {
      await api.patch(`/api/v1/alerts/${id}/acknowledge`)
      setOpenAlerts(prev => prev.filter(a => a.id !== id))
    } catch {
      // ignore
    }
  }, [])

  return (
    <AlertContext.Provider value={{ openAlerts, openCount: openAlerts.length, markSeen, acknowledge, refresh: fetchAlerts }}>
      {children}
    </AlertContext.Provider>
  )
}

export function useAlerts() {
  const ctx = useContext(AlertContext)
  if (!ctx) throw new Error('useAlerts must be used within AlertProvider')
  return ctx
}
