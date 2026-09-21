import { Navigate, Outlet, useLocation } from 'react-router'
import { Box, CircularProgress } from '@mui/material'
import { useAuth } from './AuthContext'

/** Layout route: renders child routes only for a signed-in user, otherwise sends them to /login. */
export function RequireAuth() {
  const { user, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
        <CircularProgress />
      </Box>
    )
  }

  if (!user) {
    const returnUrl = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?returnUrl=${returnUrl}`} replace />
  }

  return <Outlet />
}
