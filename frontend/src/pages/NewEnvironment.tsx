import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { Box, Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material'
import RocketLaunchRoundedIcon from '@mui/icons-material/RocketLaunchRounded'
import { api } from '../api/client'
import { Layout } from './Layout'

export function NewEnvironment() {
  const [repoUrl, setRepoUrl] = useState('')
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: () => api.createEnvironment(repoUrl),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['environments'] })
      navigate('/')
    },
  })

  return (
    <Layout>
      <Box sx={{ maxWidth: 480, mx: 'auto' }}>
        <Stack spacing={1} sx={{ alignItems: 'center', mb: 3, textAlign: 'center' }}>
          <RocketLaunchRoundedIcon color="primary" sx={{ fontSize: 36 }} />
          <Typography variant="h4">New environment</Typography>
          <Typography variant="body2" color="text.secondary">
            Paste a public Git repo. We'll parse its devcontainer config and spin it up.
          </Typography>
        </Stack>

        <Card variant="outlined">
          <CardContent sx={{ p: 3 }}>
            <Stack spacing={2}>
              <TextField
                label="Public repo URL"
                placeholder="https://github.com/owner/repo"
                value={repoUrl}
                onChange={(e) => setRepoUrl(e.target.value)}
                fullWidth
                autoFocus
              />
              {mutation.isError && (
                <Typography color="error" variant="body2">
                  {(mutation.error as Error).message}
                </Typography>
              )}
              <Stack direction="row" spacing={2}>
                <Button
                  variant="contained"
                  disabled={!repoUrl || mutation.isPending}
                  onClick={() => mutation.mutate()}
                  fullWidth
                >
                  {mutation.isPending ? 'Creating…' : 'Create'}
                </Button>
                <Button onClick={() => navigate('/')} color="inherit">
                  Cancel
                </Button>
              </Stack>
            </Stack>
          </CardContent>
        </Card>
      </Box>
    </Layout>
  )
}
