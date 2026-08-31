import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'
import { Loading } from './ui.jsx'

// Guards routes that should only be reachable when signed out (login screens).
// An already-authenticated user hitting /login or /platform/login is bounced
// straight to their dashboard instead of seeing the form again.
export default function GuestRoute({ children }) {
  const { isAuthenticated, isPlatformAdmin, authChecked } = useAuth()

  // Wait for the stored token to actually be verified — otherwise a stale/expired token
  // would bounce straight to /dashboard here, which then immediately bounces back to
  // /login once ProtectedRoute's own check resolves.
  if (!authChecked) return <Loading />

  if (isAuthenticated) {
    return <Navigate to={isPlatformAdmin ? '/platform/dashboard' : '/dashboard'} replace />
  }

  return children
}
