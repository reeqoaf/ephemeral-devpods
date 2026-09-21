import { useState } from 'react'
import { Link as RouterLink } from 'react-router'
import {
  Box,
  Button,
  Card,
  CardActions,
  CardContent,
  Chip,
  Divider,
  IconButton,
  Link,
  ListItemIcon,
  Menu,
  MenuItem,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import MemoryRoundedIcon from '@mui/icons-material/MemoryRounded'
import MoreVertRoundedIcon from '@mui/icons-material/MoreVertRounded'
import OpenInNewRoundedIcon from '@mui/icons-material/OpenInNewRounded'
import PlayArrowRoundedIcon from '@mui/icons-material/PlayArrowRounded'
import RestartAltRoundedIcon from '@mui/icons-material/RestartAltRounded'
import SpeedRoundedIcon from '@mui/icons-material/SpeedRounded'
import StopRoundedIcon from '@mui/icons-material/StopRounded'
import type { WorkspaceEnvironment } from '../api/types'
import { useEnvironmentActions, type EnvironmentAction } from '../api/useEnvironmentActions'
import { formatCpu, formatMemory } from '../lib/environmentOptions'
import { availableActions, displayName, repoSlug } from '../lib/environments'
import { Countdown } from './Countdown'
import { DeleteEnvironmentDialog } from './DeleteEnvironmentDialog'
import { EditorActions } from './EditorActions'
import { EnvironmentStatusChip } from './EnvironmentStatusChip'

/**
 * One environment on the dashboard. The whole card opens the details page through a stretched title
 * link (an ::after overlay); everything interactive inside sits above it with `zIndex`.
 */
export function EnvironmentCard({ env }: { env: WorkspaceEnvironment }) {
  const actions = useEnvironmentActions(env.environmentId)
  const can = availableActions(env.status)
  const name = displayName(env)
  const detailsPath = `/environments/${env.environmentId}`

  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)
  const [deleteOpen, setDeleteOpen] = useState(false)

  const closeMenu = () => setMenuAnchor(null)
  const runAndClose = (action: Exclude<EnvironmentAction, 'delete'>) => {
    closeMenu()
    actions.run(action)
  }

  return (
    <Card
      variant="outlined"
      sx={{
        position: 'relative',
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        transition: 'border-color 120ms',
        '&:hover': { borderColor: 'primary.main' },
      }}
    >
      <CardContent sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start', gap: 1 }}>
          <Box sx={{ minWidth: 0 }}>
            <Link
              component={RouterLink}
              to={detailsPath}
              underline="none"
              color="inherit"
              sx={{ '&::after': { content: '""', position: 'absolute', inset: 0 } }}
            >
              <Typography variant="subtitle1" noWrap title={name} sx={{ fontWeight: 700 }}>
                {name}
              </Typography>
            </Link>
            <Typography variant="body2" color="text.secondary" noWrap title={env.repoUrl}>
              {repoSlug(env.repoUrl)}
            </Typography>
          </Box>
          <EnvironmentStatusChip status={env.status} />
        </Stack>

        <Stack direction="row" sx={{ gap: 0.75, flexWrap: 'wrap' }}>
          <Chip
            size="small"
            variant="outlined"
            icon={<SpeedRoundedIcon />}
            label={formatCpu(env.cpuCores)}
          />
          <Chip
            size="small"
            variant="outlined"
            icon={<MemoryRoundedIcon />}
            label={formatMemory(env.memoryMb)}
          />
        </Stack>

        <Countdown env={env} />

        {env.status === 'Running' && env.tunnel && (
          <Box sx={{ position: 'relative', zIndex: 1 }}>
            <EditorActions tunnel={env.tunnel} provider={env.tunnelProvider} />
          </Box>
        )}

        {actions.error && !deleteOpen && (
          <Typography variant="caption" color="error">
            {actions.error.message}
          </Typography>
        )}
      </CardContent>

      <Divider />
      <CardActions sx={{ position: 'relative', zIndex: 1, justifyContent: 'space-between', px: 2, py: 1 }}>
        {env.publicUrl && env.status === 'Running' ? (
          <Button
            size="small"
            component={Link}
            href={env.publicUrl}
            target="_blank"
            rel="noreferrer"
            endIcon={<OpenInNewRoundedIcon fontSize="small" />}
          >
            Open app
          </Button>
        ) : (
          <span />
        )}

        <Tooltip title="More actions">
          <IconButton
            size="small"
            aria-label={`Actions for ${name}`}
            aria-haspopup="menu"
            disabled={actions.pending}
            onClick={(e) => setMenuAnchor(e.currentTarget)}
          >
            <MoreVertRoundedIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </CardActions>

      <Menu anchorEl={menuAnchor} open={menuAnchor !== null} onClose={closeMenu}>
        <MenuItem component={RouterLink} to={detailsPath}>
          <ListItemIcon>
            <InfoOutlinedIcon fontSize="small" />
          </ListItemIcon>
          Details
        </MenuItem>
        {can.start && (
          <MenuItem onClick={() => runAndClose('start')}>
            <ListItemIcon>
              <PlayArrowRoundedIcon fontSize="small" />
            </ListItemIcon>
            Start
          </MenuItem>
        )}
        {can.restart && (
          <MenuItem onClick={() => runAndClose('restart')}>
            <ListItemIcon>
              <RestartAltRoundedIcon fontSize="small" />
            </ListItemIcon>
            Restart
          </MenuItem>
        )}
        {can.stop && (
          <MenuItem onClick={() => runAndClose('stop')}>
            <ListItemIcon>
              <StopRoundedIcon fontSize="small" />
            </ListItemIcon>
            Stop
          </MenuItem>
        )}
        {can.delete && (
          <MenuItem
            onClick={() => {
              closeMenu()
              setDeleteOpen(true)
            }}
            sx={{ color: 'error.main' }}
          >
            <ListItemIcon sx={{ color: 'inherit' }}>
              <DeleteOutlineRoundedIcon fontSize="small" />
            </ListItemIcon>
            Delete
          </MenuItem>
        )}
      </Menu>

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
    </Card>
  )
}
