import { useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  MenuItem,
  Step,
  StepLabel,
  Stepper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import RocketLaunchRoundedIcon from '@mui/icons-material/RocketLaunchRounded'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { RepoCheck, TunnelProvider } from '../api/types'
import {
  hostPortErrors,
  toHostPortInputs,
  toPortMappings,
  MAX_HOST_PORT,
  MIN_HOST_PORT,
  type HostPortInputs,
} from '../lib/hostPorts'
import {
  CPU_OPTIONS,
  MAX_NAME_LENGTH,
  MEMORY_OPTIONS,
  TTL_OPTIONS,
  TUNNEL_PROVIDER_OPTIONS,
  defaultOption,
  type Option,
} from '../lib/environmentOptions'
import { Layout } from './Layout'

const STEPS = ['Repository', 'Options']

/** A select over a fixed option list; options marked `disabled` are shown but can't be picked. */
function OptionSelect<T extends string | number>({
  label,
  options,
  value,
  onChange,
  helperText,
}: {
  label: string
  options: Option<T>[]
  value: T
  onChange: (value: T) => void
  helperText?: ReactNode
}) {
  return (
    <TextField
      select
      label={label}
      value={value}
      onChange={(e) => {
        const picked = options.find((option) => String(option.value) === e.target.value)
        if (picked) onChange(picked.value)
      }}
      helperText={helperText}
      fullWidth
    >
      {options.map((option) => (
        <MenuItem key={option.value} value={option.value} disabled={option.disabled}>
          {option.label}
          {option.hint && (
            <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: 1 }}>
              {option.hint}
            </Typography>
          )}
        </MenuItem>
      ))}
    </TextField>
  )
}

function RepoSummary({ check }: { check: RepoCheck }) {
  return (
    <Box sx={{ bgcolor: 'action.hover', borderRadius: 2, p: 1.5 }}>
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {check.owner}/{check.repo}
      </Typography>
      <Stack direction="row" sx={{ gap: 0.75, flexWrap: 'wrap', mt: 0.75 }}>
        <Chip size="small" variant="outlined" label={check.image ? `Image: ${check.image}` : 'Built from Dockerfile'} />
        {check.portMappings.map(({ containerPort }) => (
          <Chip key={containerPort} size="small" variant="outlined" label={`Port ${containerPort}`} />
        ))}
      </Stack>
    </Box>
  )
}

export function NewEnvironment() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const queryClient = useQueryClient()

  const [step, setStep] = useState(0)
  const [repoUrl, setRepoUrl] = useState('')
  const [check, setCheck] = useState<RepoCheck | null>(null)

  const [name, setName] = useState('')
  const [ttlMinutes, setTtlMinutes] = useState(() => defaultOption(TTL_OPTIONS))
  const [cpuCores, setCpuCores] = useState(() => defaultOption(CPU_OPTIONS))
  const [memoryMb, setMemoryMb] = useState(() => defaultOption(MEMORY_OPTIONS))
  const [tunnelProvider, setTunnelProvider] = useState<TunnelProvider>(() =>
    defaultOption(TUNNEL_PROVIDER_OPTIONS),
  )
  const [hostPorts, setHostPorts] = useState<HostPortInputs>({})

  const checkRepo = useMutation({
    mutationFn: (url: string) => api.checkRepo(url),
    onSuccess: (result) => {
      setCheck(result)
      setName(result.suggestedName)
      setHostPorts(toHostPortInputs(result.portMappings))
      setStep(1)
    },
  })

  const create = useMutation({
    mutationFn: () =>
      api.createEnvironment({
        repoUrl: check!.repoUrl,
        name: name.trim(),
        ttlMinutes,
        cpuCores,
        memoryMb,
        tunnelProvider,
        ...(check!.hostPortsSelectable && { portMappings: toPortMappings(hostPorts) }),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['environments'] })
      navigate('/')
    },
  })

  const trimmedUrl = repoUrl.trim()

  const submitRepo = (event: FormEvent) => {
    event.preventDefault()
    if (!trimmedUrl || checkRepo.isPending) return

    // Same URL as the last successful check: nothing to re-validate.
    if (check?.repoUrl === trimmedUrl) {
      setStep(1)
      return
    }
    checkRepo.mutate(trimmedUrl)
  }

  const submitOptions = (event: FormEvent) => {
    event.preventDefault()
    if (name.trim() && !create.isPending) create.mutate()
  }

  const nameTooLong = name.length > MAX_NAME_LENGTH
  const portErrors = hostPortErrors(hostPorts)
  const portsInvalid = !!check?.hostPortsSelectable && Object.values(portErrors).some(Boolean)

  return (
    <Layout>
      <Box sx={{ maxWidth: 560, mx: 'auto' }}>
        <Stack spacing={1} sx={{ alignItems: 'center', mb: 3, textAlign: 'center' }}>
          <RocketLaunchRoundedIcon color="primary" sx={{ fontSize: 36 }} />
          <Typography variant="h4">New pod</Typography>
          <Typography variant="body2" color="text.secondary">
            Paste a public Git repo. We'll parse its devcontainer config and spin it up.
          </Typography>
        </Stack>

        <Stepper activeStep={step} sx={{ mb: 3 }}>
          {STEPS.map((label) => (
            <Step key={label}>
              <StepLabel>{label}</StepLabel>
            </Step>
          ))}
        </Stepper>

        {user && !user.canProvision && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            Admin access is required to run environments.
          </Alert>
        )}

        <Card variant="outlined">
          <CardContent sx={{ p: 3 }}>
            {step === 0 && (
              <form onSubmit={submitRepo} noValidate>
                <Stack spacing={2}>
                  <TextField
                    label="Public repo URL"
                    placeholder="https://github.com/owner/repo"
                    value={repoUrl}
                    onChange={(e) => {
                      setRepoUrl(e.target.value)
                      if (check && e.target.value.trim() !== check.repoUrl) setCheck(null)
                      checkRepo.reset()
                    }}
                    fullWidth
                    autoFocus
                  />
                  {checkRepo.error && <Alert severity="error">{checkRepo.error.message}</Alert>}
                  <Stack direction="row" spacing={2}>
                    <Button
                      type="submit"
                      variant="contained"
                      disabled={!trimmedUrl || checkRepo.isPending}
                      fullWidth
                    >
                      {checkRepo.isPending ? 'Checking…' : 'Check repository'}
                    </Button>
                    <Button onClick={() => navigate('/')} color="inherit">
                      Cancel
                    </Button>
                  </Stack>
                </Stack>
              </form>
            )}

            {step === 1 && check && (
              <form onSubmit={submitOptions} noValidate>
                <Stack spacing={2.5}>
                  <RepoSummary check={check} />

                  <TextField
                    label="Name"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    error={nameTooLong || name.trim() === ''}
                    helperText={`${name.length}/${MAX_NAME_LENGTH}`}
                    required
                    fullWidth
                    autoFocus
                  />

                  <OptionSelect
                    label="Lifetime"
                    options={TTL_OPTIONS}
                    value={ttlMinutes}
                    onChange={setTtlMinutes}
                    helperText="The pod is removed automatically after this."
                  />

                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
                    <OptionSelect label="CPU" options={CPU_OPTIONS} value={cpuCores} onChange={setCpuCores} />
                    <OptionSelect label="RAM" options={MEMORY_OPTIONS} value={memoryMb} onChange={setMemoryMb} />
                  </Stack>

                  <OptionSelect
                    label="VS Code tunnel sign-in"
                    options={TUNNEL_PROVIDER_OPTIONS}
                    value={tunnelProvider}
                    onChange={setTunnelProvider}
                    helperText="The account you'll use to open this pod in VS Code."
                  />

                  {check.hostPortsSelectable &&
                    check.portMappings.map(({ containerPort }, index) => (
                      <TextField
                        key={containerPort}
                        label={
                          check.portMappings.length > 1
                            ? `Host port for container port ${containerPort}`
                            : 'Host port'
                        }
                        type="number"
                        value={hostPorts[containerPort] ?? ''}
                        onChange={(e) => setHostPorts((ports) => ({ ...ports, [containerPort]: e.target.value }))}
                        error={!!portErrors[containerPort]}
                        helperText={
                          portErrors[containerPort] ??
                          `Container port ${containerPort} will be reachable at localhost:${hostPorts[containerPort] || '…'}.` +
                            (index === 0 ? ' Pick ports no other app is using.' : '')
                        }
                        slotProps={{ htmlInput: { min: MIN_HOST_PORT, max: MAX_HOST_PORT } }}
                        fullWidth
                      />
                    ))}

                  {create.error && <Alert severity="error">{create.error.message}</Alert>}

                  <Stack direction="row" spacing={2}>
                    <Button onClick={() => setStep(0)} color="inherit" disabled={create.isPending}>
                      Back
                    </Button>
                    <Button
                      type="submit"
                      variant="contained"
                      disabled={!name.trim() || nameTooLong || portsInvalid || create.isPending}
                      fullWidth
                    >
                      {create.isPending ? 'Creating…' : 'Create'}
                    </Button>
                  </Stack>
                </Stack>
              </form>
            )}
          </CardContent>
        </Card>
      </Box>
    </Layout>
  )
}
