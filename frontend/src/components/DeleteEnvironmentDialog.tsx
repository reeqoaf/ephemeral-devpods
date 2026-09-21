import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from '@mui/material'

/**
 * Confirms destroying an environment: unlike Stop, this removes the container for good, so it can't be
 * started again. The caller owns the mutation (see useEnvironmentActions) and passes its state in.
 */
export function DeleteEnvironmentDialog({
  name,
  open,
  pending,
  error,
  onConfirm,
  onClose,
}: {
  name: string
  open: boolean
  pending: boolean
  error: Error | null
  onConfirm: () => void
  onClose: () => void
}) {
  const close = () => {
    if (!pending) onClose()
  }

  return (
    <Dialog open={open} onClose={close} maxWidth="xs" fullWidth>
      <DialogTitle>Delete this environment?</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ wordBreak: 'break-word' }}>
          {name} will be shut down and its container removed. Anything not pushed is lost, and it
          can't be started again. To keep it around, use Stop instead.
        </DialogContentText>
        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error.message}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={close} disabled={pending}>
          Cancel
        </Button>
        <Button onClick={onConfirm} color="error" variant="contained" disabled={pending}>
          {pending ? 'Deleting…' : 'Delete environment'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
