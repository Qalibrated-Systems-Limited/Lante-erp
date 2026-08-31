import axios from 'axios'
import { navigateTo } from '../navigation.js'
import { clearAuthSession } from '../authSession.js'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'https://lante.africa',
  timeout: 20000,
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('lante_token')
  // An Authorization header set explicitly on the call WINS. The first-login password change sends a
  // short-lived password-change token while a stale session token may still be sitting in localStorage,
  // and overwriting it here would silently send the wrong credential (#263).
  if (token && !config.headers?.Authorization) {
    config.headers.Authorization = `Bearer ${token}`
  }
  // Single-login model (2026-07): tenant is resolved server-side by the user's email (invite-created
  // directory row in public.Users), then carried in the JWT `schema` claim after login. No pre-login
  // subdomain hint is sent — everyone signs in at lante.africa.
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const path = window.location.pathname
    if (error.response?.status === 401 && !path.startsWith('/login') && !path.startsWith('/platform/login')) {
      localStorage.removeItem('lante_token')
      localStorage.removeItem('lante_user')
      // Clears AuthContext's in-memory token/user too, not just localStorage — otherwise
      // isAuthenticated stays stale-true until the next full page load, so a session that
      // expires mid-use never flips the route guards or per-user context state (alerts,
      // notifications) that depend on that transition.
      clearAuthSession()
      navigateTo('/login', { replace: true })
    }
    return Promise.reject(error)
  }
)

export default api
