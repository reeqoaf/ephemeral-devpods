import { Chip } from '@mui/material'
import type { EnvironmentStatus } from '../api/types'

const statusColor: Record<EnvironmentStatus, 'info' | 'success' | 'warning' | 'default' | 'error'> = {
  Provisioning: 'info',
  Running: 'success',
  Stopped: 'warning',
  Expired: 'default',
  Failed: 'error',
}

export function EnvironmentStatusChip({ status }: { status: EnvironmentStatus }) {
  return <Chip label={status} color={statusColor[status]} size="small" />
}
