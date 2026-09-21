import { useState } from 'react'
import { useNavigate } from 'react-router'
import { Button, ListItemIcon, ListItemText, Menu, MenuItem } from '@mui/material'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import ArrowDropDownRoundedIcon from '@mui/icons-material/ArrowDropDownRounded'
import ViewInArRoundedIcon from '@mui/icons-material/ViewInArRounded'
import WorkspacesRoundedIcon from '@mui/icons-material/WorkspacesRounded'

/** "New environment" split into what can be created: a single pod today, workspaces later. */
export function NewEnvironmentMenu() {
  const navigate = useNavigate()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  return (
    <>
      <Button
        variant="contained"
        startIcon={<AddRoundedIcon />}
        endIcon={<ArrowDropDownRoundedIcon />}
        onClick={(e) => setAnchor(e.currentTarget)}
        aria-haspopup="menu"
        aria-expanded={anchor !== null}
        sx={{ whiteSpace: 'nowrap' }}
      >
        New environment
      </Button>
      <Menu anchorEl={anchor} open={anchor !== null} onClose={() => setAnchor(null)}>
        <MenuItem
          onClick={() => {
            setAnchor(null)
            navigate('/new')
          }}
        >
          <ListItemIcon>
            <ViewInArRoundedIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>New pod</ListItemText>
        </MenuItem>
        <MenuItem disabled>
          <ListItemIcon>
            <WorkspacesRoundedIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText secondary="Coming soon">New workspace</ListItemText>
        </MenuItem>
      </Menu>
    </>
  )
}
