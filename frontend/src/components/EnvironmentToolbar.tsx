import { Button, IconButton, InputAdornment, MenuItem, Stack, TextField, Typography } from '@mui/material'
import ClearRoundedIcon from '@mui/icons-material/ClearRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
import {
  ALL,
  SORT_OPTIONS,
  parseSortKey,
  type EnvironmentFilters,
  type FilterOption,
  type SortKey,
} from '../lib/environments'

/** Search, CPU/RAM filters and sorting for the dashboard. Fully controlled; the page owns the state. */
export function EnvironmentToolbar({
  filters,
  onFiltersChange,
  sort,
  onSortChange,
  cpuOptions,
  memoryOptions,
  shown,
  total,
}: {
  filters: EnvironmentFilters
  onFiltersChange: (patch: Partial<EnvironmentFilters>) => void
  sort: SortKey
  onSortChange: (sort: SortKey) => void
  cpuOptions: FilterOption[]
  memoryOptions: FilterOption[]
  shown: number
  total: number
}) {
  const filtered = filters.query !== '' || filters.cpu !== ALL || filters.memory !== ALL

  return (
    <Stack sx={{ gap: 1, mb: 3 }}>
      <Stack direction="row" sx={{ gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
        <TextField
          size="small"
          placeholder="Search by name or repo"
          value={filters.query}
          onChange={(e) => onFiltersChange({ query: e.target.value })}
          sx={{ flex: '1 1 220px', minWidth: 200, maxWidth: 360 }}
          slotProps={{
            htmlInput: { 'aria-label': 'Search environments' },
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchRoundedIcon fontSize="small" />
                </InputAdornment>
              ),
              endAdornment: filters.query ? (
                <InputAdornment position="end">
                  <IconButton
                    size="small"
                    edge="end"
                    aria-label="Clear search"
                    onClick={() => onFiltersChange({ query: '' })}
                  >
                    <ClearRoundedIcon fontSize="small" />
                  </IconButton>
                </InputAdornment>
              ) : undefined,
            },
          }}
        />

        <TextField
          select
          size="small"
          label="CPU"
          value={filters.cpu}
          onChange={(e) => onFiltersChange({ cpu: e.target.value })}
          sx={{ minWidth: 130 }}
        >
          <MenuItem value={ALL}>All</MenuItem>
          {cpuOptions.map((option) => (
            <MenuItem key={option.value} value={option.value}>
              {option.label}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          select
          size="small"
          label="RAM"
          value={filters.memory}
          onChange={(e) => onFiltersChange({ memory: e.target.value })}
          sx={{ minWidth: 130 }}
        >
          <MenuItem value={ALL}>All</MenuItem>
          {memoryOptions.map((option) => (
            <MenuItem key={option.value} value={option.value}>
              {option.label}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          select
          size="small"
          label="Sort by"
          value={sort}
          onChange={(e) => onSortChange(parseSortKey(e.target.value))}
          sx={{ minWidth: 170, ml: { sm: 'auto' } }}
        >
          {SORT_OPTIONS.map((option) => (
            <MenuItem key={option.value} value={option.value}>
              {option.label}
            </MenuItem>
          ))}
        </TextField>
      </Stack>

      <Stack direction="row" sx={{ alignItems: 'center', gap: 1, minHeight: 32 }}>
        <Typography variant="body2" color="text.secondary">
          {filtered ? `Showing ${shown} of ${total}` : `${total} ${total === 1 ? 'environment' : 'environments'}`}
        </Typography>
        {filtered && (
          <Button
            size="small"
            onClick={() => onFiltersChange({ query: '', cpu: ALL, memory: ALL })}
          >
            Clear filters
          </Button>
        )}
      </Stack>
    </Stack>
  )
}
