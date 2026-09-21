import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { Box, Button, CircularProgress, Stack, Typography } from '@mui/material'
import RocketLaunchRoundedIcon from '@mui/icons-material/RocketLaunchRounded'
import SearchOffRoundedIcon from '@mui/icons-material/SearchOffRounded'
import { api } from '../api/client'
import { EnvironmentCard } from '../components/EnvironmentCard'
import { EnvironmentToolbar } from '../components/EnvironmentToolbar'
import { NewEnvironmentMenu } from '../components/NewEnvironmentMenu'
import {
  ALL,
  cpuFilterOptions,
  filterEnvironments,
  isSettling,
  memoryFilterOptions,
  parseSortKey,
  sortEnvironments,
  type EnvironmentFilters,
  type SortKey,
} from '../lib/environments'
import { Layout } from './Layout'

function EmptyState() {
  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 2,
        py: 10,
        color: 'text.secondary',
      }}
    >
      <RocketLaunchRoundedIcon sx={{ fontSize: 48, opacity: 0.5 }} />
      <Typography variant="h6">No environments yet</Typography>
      <Typography variant="body2">Paste a public repo to spin up your first one.</Typography>
      <NewEnvironmentMenu />
    </Box>
  )
}

function NoMatches({ onClear }: { onClear: () => void }) {
  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 1.5,
        py: 8,
        color: 'text.secondary',
      }}
    >
      <SearchOffRoundedIcon sx={{ fontSize: 40, opacity: 0.5 }} />
      <Typography variant="h6">No environments match</Typography>
      <Button onClick={onClear}>Clear filters</Button>
    </Box>
  )
}

export function Dashboard() {
  const { data: environments, isLoading, error } = useQuery({
    queryKey: ['environments'],
    queryFn: api.listEnvironments,
    refetchInterval: (query) => (query.state.data?.some(isSettling) ? 3000 : 15000),
  })

  // Search, filters and sort live in the URL so they survive opening an environment and coming back.
  const [params, setParams] = useSearchParams()
  const query = params.get('q') ?? ''
  const cpuParam = params.get('cpu') ?? ALL
  const memoryParam = params.get('ram') ?? ALL
  const sort = parseSortKey(params.get('sort'))

  const cpuOptions = useMemo(() => cpuFilterOptions(environments ?? []), [environments])
  const memoryOptions = useMemo(() => memoryFilterOptions(environments ?? []), [environments])

  // A stale URL value (e.g. no environment has that size any more) must not leave the select blank.
  const filters = useMemo<EnvironmentFilters>(
    () => ({
      query,
      cpu: cpuOptions.some((option) => option.value === cpuParam) ? cpuParam : ALL,
      memory: memoryOptions.some((option) => option.value === memoryParam) ? memoryParam : ALL,
    }),
    [query, cpuParam, memoryParam, cpuOptions, memoryOptions],
  )

  const visible = useMemo(
    () => sortEnvironments(filterEnvironments(environments ?? [], filters), sort),
    [environments, filters, sort],
  )

  const updateParams = (patch: Record<string, string>, defaults: Record<string, string>) =>
    setParams(
      (previous) => {
        const next = new URLSearchParams(previous)
        for (const [key, value] of Object.entries(patch)) {
          if (value === defaults[key]) next.delete(key)
          else next.set(key, value)
        }
        return next
      },
      { replace: true },
    )

  const updateFilters = (patch: Partial<EnvironmentFilters>) =>
    updateParams(
      {
        ...(patch.query !== undefined && { q: patch.query }),
        ...(patch.cpu !== undefined && { cpu: patch.cpu }),
        ...(patch.memory !== undefined && { ram: patch.memory }),
      },
      { q: '', cpu: ALL, ram: ALL },
    )

  const updateSort = (key: SortKey) => updateParams({ sort: key }, { sort: 'newest' })

  const hasEnvironments = !!environments && environments.length > 0

  return (
    <Layout>
      <Stack
        direction="row"
        sx={{ justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2, mb: 3 }}
      >
        <Typography variant="h4">Your environments</Typography>
        {hasEnvironments && <NewEnvironmentMenu />}
      </Stack>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 10 }}>
          <CircularProgress />
        </Box>
      )}
      {error && <Typography color="error">Failed to load environments.</Typography>}

      {environments && environments.length === 0 && <EmptyState />}

      {hasEnvironments && (
        <>
          <EnvironmentToolbar
            filters={filters}
            onFiltersChange={updateFilters}
            sort={sort}
            onSortChange={updateSort}
            cpuOptions={cpuOptions}
            memoryOptions={memoryOptions}
            shown={visible.length}
            total={environments.length}
          />

          {visible.length === 0 ? (
            <NoMatches onClear={() => updateFilters({ query: '', cpu: ALL, memory: ALL })} />
          ) : (
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fill, minmax(min(100%, 320px), 1fr))',
                gap: 2,
              }}
            >
              {visible.map((env) => (
                <EnvironmentCard key={env.environmentId} env={env} />
              ))}
            </Box>
          )}
        </>
      )}
    </Layout>
  )
}
