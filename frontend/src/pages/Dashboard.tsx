import { useQuery } from '@tanstack/react-query'
import { Link as RouterLink } from 'react-router'
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Link,
  Stack,
  Typography,
} from '@mui/material'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import OpenInNewRoundedIcon from '@mui/icons-material/OpenInNewRounded'
import RocketLaunchRoundedIcon from '@mui/icons-material/RocketLaunchRounded'
import { api } from '../api/client'
import type { EnvironmentStatus, WorkspaceEnvironment } from '../api/types'
import { Layout } from './Layout'

const statusColor: Record<EnvironmentStatus, 'info' | 'success' | 'default' | 'error'> = {
  Provisioning: 'info',
  Running: 'success',
  Expired: 'default',
  Failed: 'error',
}

function timeRemaining(env: WorkspaceEnvironment): string {
  if (env.status === 'Failed') return 'failed'
  if (env.status === 'Expired') return 'expired'

  const expiresAt = new Date(env.createdAt).getTime() + env.ttlMinutes * 60_000
  const minutesLeft = Math.round((expiresAt - Date.now()) / 60_000)
  if (minutesLeft <= 0) return 'expired'
  if (minutesLeft < 60) return `${minutesLeft}m left`
  return `${Math.round(minutesLeft / 60)}h left`
}

function EnvironmentCard({ env }: { env: WorkspaceEnvironment }) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <Box sx={{ minWidth: 0 }}>
            <Typography
              variant="subtitle1"
              noWrap
              title={env.repoUrl}
              sx={{ fontWeight: 700 }}
            >
              {env.repoUrl.replace(/^https?:\/\//, '')}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              created {new Date(env.createdAt).toLocaleString()}
            </Typography>
          </Box>
          <Chip label={env.status} color={statusColor[env.status]} size="small" />
        </Stack>

        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', alignItems: 'center', mt: 2.5 }}
        >
          <Typography variant="body2" color="text.secondary">
            {timeRemaining(env)}
          </Typography>
          {env.publicUrl ? (
            <Button
              component={Link}
              href={env.publicUrl}
              target="_blank"
              rel="noreferrer"
              size="small"
              endIcon={<OpenInNewRoundedIcon fontSize="small" />}
            >
              Open
            </Button>
          ) : (
            <Typography variant="body2" color="text.disabled">
              not ready
            </Typography>
          )}
        </Stack>
      </CardContent>
    </Card>
  )
}

function EmptyState() {
  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 2,
        py: 10,
        color: 'text.secondary',
      }}
    >
      <RocketLaunchRoundedIcon sx={{ fontSize: 48, opacity: 0.5 }} />
      <Typography variant="h6">No environments yet</Typography>
      <Typography variant="body2">Paste a public repo to spin up your first one.</Typography>
      <Button component={RouterLink} to="/new" variant="contained" startIcon={<AddRoundedIcon />}>
        New environment
      </Button>
    </Box>
  )
}

export function Dashboard() {
  const { data: environments, isLoading, error } = useQuery({
    queryKey: ['environments'],
    queryFn: api.listEnvironments,
    refetchInterval: (query) =>
      query.state.data?.some((e) => e.status === 'Provisioning') ? 3000 : 15000,
  })

  return (
    <Layout>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Your environments</Typography>
        {environments && environments.length > 0 && (
          <Button
            component={RouterLink}
            to="/new"
            variant="contained"
            startIcon={<AddRoundedIcon />}
          >
            New environment
          </Button>
        )}
      </Stack>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 10 }}>
          <CircularProgress />
        </Box>
      )}
      {error && <Typography color="error">Failed to load environments.</Typography>}

      {environments && environments.length === 0 && <EmptyState />}

      {environments && environments.length > 0 && (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
            gap: 2,
          }}
        >
          {environments.map((env) => (
            <EnvironmentCard key={env.environmentId} env={env} />
          ))}
        </Box>
      )}
    </Layout>
  )
}
