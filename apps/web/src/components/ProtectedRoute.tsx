import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { isAuthenticated } from '../api/client';

/**
 * Wrapper that redirects unauthenticated users to the login page.
 * Stores the attempted location so they can be returned after login.
 */
export function ProtectedRoute() {
  const location = useLocation();

  if (!isAuthenticated()) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
}