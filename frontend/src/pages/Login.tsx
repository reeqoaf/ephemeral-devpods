import { Navigate, useSearchParams } from 'react-router'
import { Alert, Button, Card, CardContent, Stack, Typography } from '@mui/material'
import MicrosoftIcon from '@mui/icons-material/Microsoft'
import GitHubIcon from '@mui/icons-material/GitHub'
import { authUrls } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { describeAuthError, safeReturnUrl } from '../auth/authErrors'
import { Layout } from './Layout'

export function Login() {
  const { user, isLoading } = useAuth()
  const [params] = useSearchParams()
  const returnUrl = safeReturnUrl(params.get('returnUrl'))
  const error = describeAuthError(params.get('error'))

  if (!isLoading && user) {
    return <Navigate to={returnUrl} replace />
  }

  return (
    <Layout>
      <Card sx={{ maxWidth: 420, mx: 'auto', mt: 6 }}>
        <CardContent>
          <Stack spacing={2}>
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              Sign in
            </Typography>
            <Typography color="text.secondary">
              Sign in to see and manage your environments.
            </Typography>
            {error && <Alert severity="error">{error}</Alert>}
            <Button
              variant="contained"
              size="large"
              startIcon={<MicrosoftIcon />}
              href={authUrls.login('Microsoft', returnUrl)}
            >
              Sign in with Microsoft
            </Button>
            <Button
              variant="outlined"
              size="large"
              startIcon={<GitHubIcon />}
              href={authUrls.login('GitHub', returnUrl)}
            >
              Sign in with GitHub
            </Button>
            <Typography variant="caption" color="text.secondary">
              GitHub sign-in works once the account has been linked from Account settings.
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    </Layout>
  )
}
