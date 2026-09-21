import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, CardContent, Chip, Divider, Stack, Typography } from '@mui/material'
import MicrosoftIcon from '@mui/icons-material/Microsoft'
import GitHubIcon from '@mui/icons-material/GitHub'
import { api, authUrls } from '../api/client'
import type { IdentityProvider } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { describeAuthError } from '../auth/authErrors'
import { ME_QUERY_KEY } from '../queryClient'
import { Layout } from './Layout'

const PROVIDERS: { provider: IdentityProvider; icon: ReactNode; canUnlink: boolean }[] = [
  { provider: 'Microsoft', icon: <MicrosoftIcon />, canUnlink: false },
  { provider: 'GitHub', icon: <GitHubIcon />, canUnlink: true },
]

export function Account() {
  const { user } = useAuth()
  const [params] = useSearchParams()
  const queryClient = useQueryClient()

  const unlink = useMutation({
    mutationFn: (provider: IdentityProvider) => api.unlinkIdentity(provider),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY }),
  })

  if (!user) return null

  const callbackError = describeAuthError(params.get('error'))

  return (
    <Layout>
      <Stack spacing={3} sx={{ maxWidth: 640 }}>
        <div>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            Account
          </Typography>
          <Typography color="text.secondary">
            {user.displayName}
            {user.email ? ` · ${user.email}` : ''}
          </Typography>
        </div>

        {callbackError && <Alert severity="error">{callbackError}</Alert>}
        {unlink.error && <Alert severity="error">{unlink.error.message}</Alert>}

        <Card>
          <CardContent>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
              Sign-in methods
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Any linked account signs you in to this same user and its environments.
            </Typography>
            <Stack divider={<Divider flexItem />} spacing={2}>
              {PROVIDERS.map(({ provider, icon, canUnlink }) => {
                const linked = user.identities.find((i) => i.provider === provider)
                return (
                  <Stack key={provider} direction="row" spacing={2} sx={{ alignItems: 'center' }}>
                    {icon}
                    <Stack sx={{ flexGrow: 1 }}>
                      <Typography sx={{ fontWeight: 500 }}>{provider}</Typography>
                      <Typography variant="body2" color="text.secondary">
                        {linked ? linked.displayName : 'Not linked'}
                      </Typography>
                    </Stack>
                    {linked && !canUnlink && <Chip size="small" label="Primary" />}
                    {linked && canUnlink && (
                      <Button
                        color="error"
                        disabled={unlink.isPending}
                        onClick={() => unlink.mutate(provider)}
                      >
                        Unlink
                      </Button>
                    )}
                    {!linked && (
                      <Button variant="outlined" href={authUrls.link(provider, '/settings')}>
                        Link {provider}
                      </Button>
                    )}
                  </Stack>
                )
              })}
            </Stack>
          </CardContent>
        </Card>
      </Stack>
    </Layout>
  )
}
