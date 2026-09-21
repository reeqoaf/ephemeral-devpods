import { useState, type ReactNode } from 'react'
import { Link as RouterLink, useNavigate } from 'react-router'
import {
  AppBar,
  Avatar,
  Box,
  IconButton,
  ListItemIcon,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
} from '@mui/material'
import DarkModeRoundedIcon from '@mui/icons-material/DarkModeRounded'
import LightModeRoundedIcon from '@mui/icons-material/LightModeRounded'
import DnsRoundedIcon from '@mui/icons-material/DnsRounded'
import ManageAccountsRoundedIcon from '@mui/icons-material/ManageAccountsRounded'
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded'
import { useColorMode } from '../theme/ColorModeProvider'
import { useAuth } from '../auth/AuthContext'

export function Layout({ children }: { children: ReactNode }) {
  const { mode, toggle } = useColorMode()
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)

  const closeMenu = () => setMenuAnchor(null)

  const handleLogout = async () => {
    closeMenu()
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <Box sx={{ minHeight: '100%', bgcolor: 'background.default' }}>
      <AppBar
        position="sticky"
        color="transparent"
        elevation={0}
        sx={{
          borderBottom: (t) =>
            `1px solid ${t.palette.mode === 'dark' ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.06)'}`,
          backdropFilter: 'blur(8px)',
        }}
      >
        <Toolbar sx={{ gap: 1 }}>
          <Box
            component={RouterLink}
            to="/"
            aria-label="ephemeral-devpods home"
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              color: 'inherit',
              textDecoration: 'none',
            }}
          >
            <DnsRoundedIcon color="primary" />
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              ephemeral-devpods
            </Typography>
          </Box>
          <Box sx={{ flexGrow: 1 }} />
          <Tooltip title={mode === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}>
            <IconButton onClick={toggle} color="inherit">
              {mode === 'dark' ? <LightModeRoundedIcon /> : <DarkModeRoundedIcon />}
            </IconButton>
          </Tooltip>
          {user && (
            <>
              <Tooltip title={user.displayName}>
                <IconButton onClick={(e) => setMenuAnchor(e.currentTarget)} aria-label="Account menu">
                  <Avatar sx={{ width: 32, height: 32, bgcolor: 'primary.main', fontSize: 16 }}>
                    {user.displayName.charAt(0).toUpperCase()}
                  </Avatar>
                </IconButton>
              </Tooltip>
              <Menu anchorEl={menuAnchor} open={menuAnchor !== null} onClose={closeMenu}>
                <MenuItem
                  onClick={() => {
                    closeMenu()
                    navigate('/settings')
                  }}
                >
                  <ListItemIcon>
                    <ManageAccountsRoundedIcon fontSize="small" />
                  </ListItemIcon>
                  Account
                </MenuItem>
                <MenuItem onClick={handleLogout}>
                  <ListItemIcon>
                    <LogoutRoundedIcon fontSize="small" />
                  </ListItemIcon>
                  Sign out
                </MenuItem>
              </Menu>
            </>
          )}
        </Toolbar>
      </AppBar>
      <Box sx={{ maxWidth: 1100, mx: 'auto', px: { xs: 2, sm: 4 }, py: 4 }}>{children}</Box>
    </Box>
  )
}
