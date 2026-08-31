import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext.jsx'
import { Loading } from './ui.jsx'

export default function PlatformRoute({ children }) {
  const { isAuthenticated, user, authChecked } = useAuth()

  if (!authChecked) return <Loading />

  if (!isAuthenticated) return <Navigate to="/platform/login" replace />

  const isPlatform = user?.isPlatformAdmin || user?.userRoles?.includes('Platform Admin')
  if (!isPlatform) return <Navigate to="/dashboard" replace />

  return children
}
