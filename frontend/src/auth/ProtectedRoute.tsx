import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './contexte'

/** Redirige vers la page de connexion, en memorisant la destination voulue. */
export function ProtectedRoute() {
  const { estConnecte } = useAuth()
  const emplacement = useLocation()

  if (!estConnecte) {
    return <Navigate to="/admin/connexion" state={{ retour: emplacement.pathname }} replace />
  }

  return <Outlet />
}
