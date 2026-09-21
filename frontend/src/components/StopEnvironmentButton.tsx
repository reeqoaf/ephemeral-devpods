import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from '@mui/material'
import StopRoundedIcon from '@mui/icons-material/StopRounded'
import { api } from '../api/client'
import type { WorkspaceEnvironment } from '../api/types'

/**
 * Tears the environment down now instead of waiting for its TTL. Environments are fully ephemeral
 * (no restart), so this asks first.
 */
export function StopEnvironmentButton({ env }: { env: WorkspaceEnvironment }) {
  const [open, setOpen] = useState(false)
  const queryClient = useQueryClient()

  const stop = useMutation({
    mutationFn: () => api.deleteEnvironment(env.environmentId),
    onSuccess: async () => {
      setOpen(false)
      await queryClient.invalidateQueries({ queryKey: ['environments'] })
    },
  })

  const close = () => {
    if (stop.isPending) return
    setOpen(false)
    stop.reset()
  }

  return (
    <>
      <Button
        size="small"
        color="error"
        startIcon={<StopRoundedIcon fontSize="small" />}
        onClick={() => setOpen(true)}
      >
        Stop
      </Button>

      <Dialog open={open} onClose={close} maxWidth="xs" fullWidth>
        <DialogTitle>Stop this environment?</DialogTitle>
        <DialogContent>
          <DialogContentText sx={{ wordBreak: 'break-word' }}>
            {env.repoUrl.replace(/^https?:\/\//, '')} will be shut down and its container removed.
            Anything not pushed is lost, and it can't be restarted.
          </DialogContentText>
          {stop.error && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {stop.error.message}
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={close} disabled={stop.isPending}>
            Cancel
          </Button>
          <Button
            onClick={() => stop.mutate()}
            color="error"
            variant="contained"
            disabled={stop.isPending}
          >
            {stop.isPending ? 'Stopping…' : 'Stop environment'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
