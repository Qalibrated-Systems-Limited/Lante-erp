import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'
import { Loading } from './ui.jsx'

export default function ProtectedRoute({ children, permission }) {
  const { isAuthenticated, hasPermission, authChecked } = useAuth()
  const location = useLocation()

  // Don't commit to rendering children or redirecting until we know the stored token is
  // actually still valid — otherwise a stale token briefly paints the dashboard before the
  // first API call's 401 sends it back to /login.
  if (!authChecked) return <Loading />

  if (!isAuthenticated) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />

  if (permission && !hasPermission(permission)) {
    return <Navigate to="/dashboard" replace state={{ forbidden: true }} />
  }

  return children
}
