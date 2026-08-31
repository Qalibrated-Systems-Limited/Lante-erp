import { createContext, useContext, useState, useCallback, useEffect } from 'react'
import { hasPermission as checkPermission } from '../utils/permissions.js'
import api from '../api/axios.js'
import { setAuthClearer } from '../authSession.js'

const AuthContext = createContext(null)

function loadStoredAuth() {
  const token = localStorage.getItem('lante_token')
  const userRaw = localStorage.getItem('lante_user')
  if (token && userRaw) {
    try {
      return { token, user: JSON.parse(userRaw) }
    } catch {
      return { token: null, user: null }
    }
  }
  return { token: null, user: null }
}

export function AuthProvider({ children }) {
  const stored = loadStoredAuth()
  const [token, setToken] = useState(stored.token)
  const [user, setUser] = useState(stored.user)
  // Per-tenant module on/off registry (Administration → Modules). null = not yet loaded, so
  // isModuleEnabled fails open (shows the nav item) until the real answer arrives or if the
  // fetch fails — a broken/slow request must never hide a module outright.
  const [disabledModules, setDisabledModules] = useState(null)
  // A token string existing in localStorage doesn't mean it's still valid (expired, or the
  // session was revoked server-side) — without this, ProtectedRoute would render the dashboard
  // on a stale token before the first API call's 401 redirects away, producing a visible flash.
  // Piggybacks on the system-modules call below (any authenticated GET works) rather than a
  // dedicated endpoint. No token at all means there's nothing to verify — checked immediately.
  const [authChecked, setAuthChecked] = useState(!stored.token)

  useEffect(() => {
    if (!token) { setDisabledModules(null); setAuthChecked(true); return }
    api.get('/api/v1/system-modules')
      .then(res => {
        const modules = res.data?.data ?? []
        setDisabledModules(modules.filter(m => !m.isEnabled).map(m => m.moduleId))
      })
      .catch(err => {
        setDisabledModules([])
        // A 401 means this token is actually invalid — flip isAuthenticated now rather than
        // waiting on the axios interceptor's redirect, which only fires reactively later.
        if (err.response?.status === 401) {
          setToken(null)
          setUser(null)
        }
      })
      .finally(() => setAuthChecked(true))
  }, [token])

  const isModuleEnabled = useCallback(
    (moduleId) => !disabledModules?.includes(moduleId),
    [disabledModules]
  )

  const login = useCallback((newToken, newUser) => {
    localStorage.setItem('lante_token', newToken)
    localStorage.setItem('lante_user', JSON.stringify(newUser))
    setToken(newToken)
    setUser(newUser)
  }, [])

  // Registers this provider's own state-clear with axios.js's 401 interceptor (see
  // authSession.js) — a session expiring mid-use is caught by ANY API call's 401, not just the
  // one-time check above, and needs to flip isAuthenticated to false in React state too, not
  // just clear localStorage, or route guards and per-user context state (alerts/notifications)
  // never notice the session actually ended.
  useEffect(() => {
    setAuthClearer(() => { setToken(null); setUser(null) })
    return () => setAuthClearer(null)
  }, [])

  const updateBranchContext = useCallback((branchId, isCompanyAdmin) => {
    setUser(prev => {
      if (!prev) return prev
      const updated = { ...prev, branchId, isCompanyAdmin }
      localStorage.setItem('lante_user', JSON.stringify(updated))
      return updated
    })
  }, [])

  const logout = useCallback(() => {
    // Capture the token before clearing storage — axios's request interceptor
    // runs as a microtask, so by the time it would read localStorage the
    // removeItem below has already run. Attach it explicitly instead of
    // relying on the interceptor. Fire-and-forget: sign-out clears the local
    // session immediately regardless of whether the server call succeeds.
    const currentToken = localStorage.getItem('lante_token')
    localStorage.removeItem('lante_token')
    localStorage.removeItem('lante_user')
    setToken(null)
    setUser(null)
    if (currentToken) {
      api.post('/api/v1/auth/logout', null, { headers: { Authorization: `Bearer ${currentToken}` } }).catch(() => {})
    }
  }, [])

  const hasPermission = useCallback(
    (permission) => checkPermission(user?.permissions, permission),
    [user]
  )

  // 'all' | 'dept' | 'own'
  const ticketScope = user?.permissions?.includes('system.admin') || user?.permissions?.includes('tickets.read.all')
    ? 'all'
    : user?.permissions?.includes('tickets.read.dept')
    ? 'dept'
    : 'own'

  const isAdmin         = user?.permissions?.includes('system.admin')
  const isPlatformAdmin = user?.userRoles?.includes('Platform Admin') ?? false
  const isCompanyAdmin  = user?.isCompanyAdmin ?? false
  const tenantId        = user?.tenantId ?? null
  const tenantName      = user?.tenantName ?? null
  const branchId        = user?.branchId ?? null
  const branchName      = user?.branchName ?? null
  const hqBranchId      = user?.hqBranchId ?? null
  const departmentId    = user?.departmentId ?? null
  const departmentIds   = user?.departmentIds ?? []

  // Returns true if the current user can interact with a given department.
  // Admins see everything; others are limited to their departmentIds list.
  const canInteractWithDepartment = (deptId) => {
    if (isAdmin) return true
    if (!deptId) return false
    return departmentIds.includes(deptId)
  }

  return (
    <AuthContext.Provider value={{
      user, token, login, logout, updateBranchContext,
      isAuthenticated: !!token,
      authChecked,
      hasPermission, ticketScope, isModuleEnabled,
      departmentId, departmentIds,
      isAdmin, isPlatformAdmin, isCompanyAdmin,
      tenantId, tenantName, branchId, branchName, hqBranchId,
      canInteractWithDepartment
    }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
