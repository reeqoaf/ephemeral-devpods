import { useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link as RouterLink, useParams } from 'react-router'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Divider,
  IconButton,
  Link,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material'
import { keyframes } from '@mui/material/styles'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import OpenInNewRoundedIcon from '@mui/icons-material/OpenInNewRounded'
import PlayArrowRoundedIcon from '@mui/icons-material/PlayArrowRounded'
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded'
import RestartAltRoundedIcon from '@mui/icons-material/RestartAltRounded'
import StopRoundedIcon from '@mui/icons-material/StopRounded'
import { ApiError, api } from '../api/client'
import type { WorkspaceEnvironment } from '../api/types'
import { useEnvironmentActions } from '../api/useEnvironmentActions'
import { Countdown } from '../components/Countdown'
import { DeleteEnvironmentDialog } from '../components/DeleteEnvironmentDialog'
import { EditorActions } from '../components/EditorActions'
import { EnvironmentStatusChip } from '../components/EnvironmentStatusChip'
import { formatCpu, formatMemory, formatTtl } from '../lib/environmentOptions'
import { availableActions, displayName, isSettling } from '../lib/environments'
import { Layout } from './Layout'

const spin = keyframes`
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
`

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Box sx={{ minWidth: 0, wordBreak: 'break-word', typography: 'body2' }}>{children}</Box>
    </>
  )
}

function NotFound() {
  return (
    <Stack sx={{ alignItems: 'center', gap: 2, py: 10, color: 'text.secondary' }}>
      <Typography variant="h6">Environment not found</Typography>
      <Typography variant="body2">It may have been deleted, or it isn't yours.</Typography>
      <Button component={RouterLink} to="/" variant="contained">
        Back to environments
      </Button>
    </Stack>
  )
}

function EnvironmentView({
  env,
  refreshing,
  onRefresh,
}: {
  env: WorkspaceEnvironment
  refreshing: boolean
  onRefresh: () => void
}) {
  const actions = useEnvironmentActions(env.environmentId)
  const can = availableActions(env.status)
  const name = displayName(env)
  const repoLabel = env.repoUrl.replace(/^https?:\/\//, '')
  const [deleteOpen, setDeleteOpen] = useState(false)

  const running = env.status === 'Running'
  const live = env.status !== 'Expired' && env.status !== 'Failed' // there's still a lifetime left to count down
  const busy = actions.pending

  return (
    <>
      <Button
        component={RouterLink}
        to="/"
        color="inherit"
        size="small"
        startIcon={<ArrowBackRoundedIcon />}
        sx={{ mb: 2 }}
      >
        All environments
      </Button>

      <Stack direction="row" sx={{ alignItems: 'center', gap: 1.5, flexWrap: 'wrap', mb: 0.5 }}>
        <Typography variant="h4" sx={{ wordBreak: 'break-word' }}>
          {name}
        </Typography>
        <EnvironmentStatusChip status={env.status} />
      </Stack>
      <Link href={env.repoUrl} target="_blank" rel="noreferrer" color="text.secondary" variant="body2">
        {repoLabel}
      </Link>

      {actions.error && (
        <Alert severity="error" onClose={actions.reset} sx={{ mt: 3 }}>
          {actions.error.message}
        </Alert>
      )}

      <Card variant="outlined" sx={{ mt: 3 }}>
        <CardContent sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
          <Box>
            <Typography variant="overline" color="text.secondary">
              Open
            </Typography>
            <Stack direction="row" sx={{ alignItems: 'center', gap: 1.5, flexWrap: 'wrap', mt: 0.5 }}>
              {running && env.tunnel ? (
                <EditorActions tunnel={env.tunnel} provider={env.tunnelProvider} />
              ) : (
                <Typography variant="body2" color="text.secondary">
                  Editors are available while the environment is running.
                </Typography>
              )}
              {running && env.publicUrl && (
                <Button
                  size="small"
                  variant="outlined"
                  component={Link}
                  href={env.publicUrl}
                  target="_blank"
                  rel="noreferrer"
                  endIcon={<OpenInNewRoundedIcon fontSize="small" />}
                >
                  Open website
                </Button>
              )}
            </Stack>
          </Box>

          <Divider />

          <Box>
            <Typography variant="overline" color="text.secondary">
              Manage
            </Typography>
            <Stack direction="row" sx={{ alignItems: 'center', gap: 1, flexWrap: 'wrap', mt: 0.5 }}>
              {can.start && (
                <Button
                  variant="contained"
                  size="small"
                  startIcon={<PlayArrowRoundedIcon />}
                  disabled={busy}
                  onClick={() => actions.run('start')}
                >
                  {actions.pendingAction === 'start' ? 'Starting…' : 'Start'}
                </Button>
              )}
              {can.restart && (
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<RestartAltRoundedIcon />}
                  disabled={busy}
                  onClick={() => actions.run('restart')}
                >
                  {actions.pendingAction === 'restart' ? 'Restarting…' : 'Restart'}
                </Button>
              )}
              {can.stop && (
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<StopRoundedIcon />}
                  disabled={busy}
                  onClick={() => actions.run('stop')}
                >
                  {actions.pendingAction === 'stop' ? 'Stopping…' : 'Stop'}
                </Button>
              )}
              <Tooltip title="Refresh status">
                <span>
                  <IconButton size="small" aria-label="Refresh status" onClick={onRefresh} disabled={refreshing}>
                    <RefreshRoundedIcon
                      fontSize="small"
                      sx={{ animation: refreshing ? `${spin} 1s linear infinite` : 'none' }}
                    />
                  </IconButton>
                </span>
              </Tooltip>
              {can.delete && (
                <Button
                  color="error"
                  size="small"
                  startIcon={<DeleteOutlineRoundedIcon />}
                  disabled={busy}
                  onClick={() => setDeleteOpen(true)}
                  sx={{ ml: 'auto' }}
                >
                  Delete
                </Button>
              )}
            </Stack>
          </Box>
        </CardContent>
      </Card>

      <Card variant="outlined" sx={{ mt: 2 }}>
        <CardContent>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', sm: '160px 1fr' },
              columnGap: 3,
              rowGap: { xs: 0.25, sm: 1.5 },
              alignItems: 'baseline',
              '& > p:not(:first-of-type)': { mt: { xs: 1.5, sm: 0 } },
            }}
          >
            <Detail label="Repository">
              <Link href={env.repoUrl} target="_blank" rel="noreferrer">
                {repoLabel}
              </Link>
            </Detail>
            <Detail label="Created">{new Date(env.createdAt).toLocaleString()}</Detail>
            <Detail label="Expires">
              <Box sx={{ mb: live ? 0.75 : 0 }}>{new Date(env.expiresAt).toLocaleString()}</Box>
              {live && <Countdown env={env} precise />}
            </Detail>
            <Detail label="Lifetime">{formatTtl(env.ttlMinutes)}</Detail>
            <Detail label="CPU">{formatCpu(env.cpuCores)}</Detail>
            <Detail label="RAM">{formatMemory(env.memoryMb)}</Detail>
            <Detail label="VS Code sign-in">{env.tunnelProvider}</Detail>
            {env.portMappings.length > 0 && (
              <Detail label="Ports">
                <Stack sx={{ gap: 0.25 }}>
                  {env.portMappings.map(({ containerPort, hostPort }) => (
                    <span key={containerPort}>
                      {containerPort} → localhost:{hostPort}
                    </span>
                  ))}
                </Stack>
              </Detail>
            )}
            <Detail label="Website">
              {env.publicUrl ? (
                <Link href={env.publicUrl} target="_blank" rel="noreferrer">
                  {env.publicUrl}
                </Link>
              ) : (
                <Typography component="span" color="text.disabled">
                  None
                </Typography>
              )}
            </Detail>
            <Detail label="Environment ID">
              <Typography component="code" sx={{ fontFamily: 'monospace', fontSize: 13 }}>
                {env.environmentId}
              </Typography>
            </Detail>
          </Box>
        </CardContent>
      </Card>

      <DeleteEnvironmentDialog
        name={name}
        open={deleteOpen}
        pending={actions.pendingAction === 'delete'}
        error={actions.error}
        onConfirm={() => actions.run('delete', { onSuccess: () => setDeleteOpen(false) })}
        onClose={() => {
          setDeleteOpen(false)
          actions.reset()
        }}
      />
    </>
  )
}

export function EnvironmentDetails() {
  const { id = '' } = useParams()

  const { data: env, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ['environment', id],
    queryFn: () => api.getEnvironment(id),
    refetchInterval: (query) => (query.state.data && isSettling(query.state.data) ? 3000 : 15000),
    // A 404 is an answer, not a blip: show "not found" straight away instead of retrying it.
    retry: (failureCount, err) => !(err instanceof ApiError && err.status === 404) && failureCount < 3,
  })

  const notFound = error instanceof ApiError && error.status === 404

  return (
    <Layout>
      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 10 }}>
          <CircularProgress />
        </Box>
      )}
      {notFound && <NotFound />}
      {error && !notFound && !env && (
        <Stack sx={{ alignItems: 'flex-start', gap: 2 }}>
          <Typography color="error">Failed to load this environment.</Typography>
          <Button onClick={() => refetch()}>Try again</Button>
        </Stack>
      )}
      {env && <EnvironmentView env={env} refreshing={isFetching} onRefresh={() => refetch()} />}
    </Layout>
  )
}
