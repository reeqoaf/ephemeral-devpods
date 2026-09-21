import { Box, LinearProgress, Typography } from '@mui/material'
import type { WorkspaceEnvironment } from '../api/types'
import { useNow } from '../hooks/useNow'

function formatRemaining(ms: number, precise: boolean): string {
  const totalSeconds = Math.max(0, Math.floor(ms / 1000))
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  if (precise) {
    if (hours > 0) return `${hours}h ${String(minutes).padStart(2, '0')}m ${String(seconds).padStart(2, '0')}s`
    return `${minutes}m ${String(seconds).padStart(2, '0')}s`
  }
  if (hours > 0) return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`
  return minutes > 0 ? `${minutes}m` : '<1m'
}

/**
 * Live time-to-expiry with a progress bar of how much of the lifetime is used up. `precise` shows
 * seconds and ticks every second (the details page); the default is minute-level for dashboard cards.
 */
export function Countdown({ env, precise = false }: { env: WorkspaceEnvironment; precise?: boolean }) {
  const now = useNow(precise ? 1000 : 30_000)

  if (env.status === 'Expired' || env.status === 'Failed') {
    return (
      <Typography variant="body2" color="text.secondary">
        {env.status}
      </Typography>
    )
  }

  const createdAt = Date.parse(env.createdAt)
  const expiresAt = Date.parse(env.expiresAt)
  const remaining = expiresAt - now
  const used = Math.min(100, Math.max(0, ((now - createdAt) / (expiresAt - createdAt)) * 100))

  return (
    <Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 0.75 }}>
        {remaining <= 0 ? 'Expiring…' : `${formatRemaining(remaining, precise)} left`}
      </Typography>
      <LinearProgress
        variant="determinate"
        value={used}
        color={used >= 90 ? 'warning' : 'primary'}
        aria-label="Lifetime used"
        sx={{ height: 4, borderRadius: 2 }}
      />
    </Box>
  )
}
