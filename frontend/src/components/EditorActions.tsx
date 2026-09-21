import { useState, type ReactNode } from 'react'
import { Box, Button, IconButton, Link, Stack, Tooltip, Typography } from '@mui/material'
import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import CodeRoundedIcon from '@mui/icons-material/CodeRounded'
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded'
import LaptopMacRoundedIcon from '@mui/icons-material/LaptopMacRounded'
import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded'
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded'
import type { TunnelInfo } from '../api/types'

/** Masked until revealed, so the code isn't shown to anyone glancing at (or screen-sharing) the dashboard. */
function DeviceCode({ code }: { code: string }) {
  const [revealed, setRevealed] = useState(false)
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      // Clipboard can be unavailable (insecure origin, denied permission); the code is still on screen.
    }
  }

  return (
    <Stack direction="row" sx={{ alignItems: 'center', gap: 0.5 }}>
      <Typography
        component="code"
        sx={{ fontFamily: 'monospace', fontSize: 18, fontWeight: 700, letterSpacing: 2 }}
      >
        {revealed ? code : code.replace(/[A-Za-z0-9]/g, '•')}
      </Typography>
      <Tooltip title={revealed ? 'Hide code' : 'Reveal code'}>
        <IconButton
          size="small"
          onClick={() => setRevealed((value) => !value)}
          aria-label={revealed ? 'Hide device code' : 'Reveal device code'}
        >
          {revealed ? <VisibilityOffRoundedIcon fontSize="small" /> : <VisibilityRoundedIcon fontSize="small" />}
        </IconButton>
      </Tooltip>
      <Tooltip title={copied ? 'Copied' : 'Copy code'}>
        <IconButton size="small" onClick={copy} aria-label="Copy device code">
          {copied ? <CheckRoundedIcon fontSize="small" /> : <ContentCopyRoundedIcon fontSize="small" />}
        </IconButton>
      </Tooltip>
    </Stack>
  )
}

/**
 * Web/Local editor buttons for a running environment, or — until the tunnel is signed in — the
 * GitHub device code the user has to enter first.
 */
export function EditorActions({ tunnel }: { tunnel: TunnelInfo }) {
  if (tunnel.phase === 'AwaitingLogin' && tunnel.deviceCode) {
    return (
      <Box sx={{ bgcolor: 'action.hover', borderRadius: 2, p: 1.5 }}>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          Sign in to GitHub to enable the editors
        </Typography>
        <DeviceCode code={tunnel.deviceCode} />
        <Typography variant="caption" color="text.secondary" component="p">
          Enter it at{' '}
          <Link href={tunnel.verificationUrl ?? 'https://github.com/login/device'} target="_blank" rel="noreferrer">
            github.com/login/device
          </Link>{' '}
          with the same account you use in VS Code.
        </Typography>
      </Box>
    )
  }

  const ready = tunnel.phase === 'Ready'
  return (
    <Stack direction="row" sx={{ alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
      <EditorButton
        label="Web editor"
        icon={<CodeRoundedIcon fontSize="small" />}
        href={ready ? tunnel.webEditorUrl : null}
        newTab
      />
      <EditorButton
        label="Local editor"
        icon={<LaptopMacRoundedIcon fontSize="small" />}
        href={ready ? tunnel.localEditorUrl : null}
      />
      {!ready && (
        <Typography variant="caption" color="text.secondary">
          starting tunnel…
        </Typography>
      )}
    </Stack>
  )
}

/** A link-button, or a disabled placeholder while there's no URL to open yet. */
function EditorButton({
  label,
  icon,
  href,
  newTab = false,
}: {
  label: string
  icon: ReactNode
  href?: string | null
  newTab?: boolean
}) {
  if (!href) {
    return (
      <Button size="small" variant="outlined" startIcon={icon} disabled>
        {label}
      </Button>
    )
  }

  return (
    <Button
      size="small"
      variant="outlined"
      startIcon={icon}
      href={href}
      {...(newTab ? { target: '_blank', rel: 'noreferrer' } : {})}
    >
      {label}
    </Button>
  )
}
