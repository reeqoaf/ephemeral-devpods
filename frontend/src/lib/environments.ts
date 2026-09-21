import type { EnvironmentAction } from '../api/useEnvironmentActions'
import type { EnvironmentStatus, WorkspaceEnvironment } from '../api/types'
import { formatCpu, formatMemory } from './environmentOptions'

/** Poll quickly while something is still changing under the user: provisioning, or a tunnel not yet signed in. */
export function isSettling(env: WorkspaceEnvironment): boolean {
  return (
    env.status === 'Provisioning' ||
    (env.status === 'Running' && !!env.tunnel && env.tunnel.phase !== 'Ready')
  )
}

/** "owner/repo" from a repo URL, falling back to the URL itself if it doesn't parse. */
export function repoSlug(repoUrl: string): string {
  try {
    const path = new URL(repoUrl).pathname.replace(/^\/+|\/+$/g, '').replace(/\.git$/i, '')
    return path || repoUrl
  } catch {
    return repoUrl
  }
}

/** The user's name for the environment; older environments have none, so they show their repo. */
export function displayName(env: WorkspaceEnvironment): string {
  return env.name?.trim() || repoSlug(env.repoUrl)
}

/** Which lifecycle actions the environment's current status allows, keyed like `EnvironmentAction`. */
export type AvailableActions = Record<EnvironmentAction, boolean>

/** Mirrors what the backend accepts (EnvironmentLifecycle.cs), so the UI never offers a 409. */
export function availableActions(status: EnvironmentStatus): AvailableActions {
  switch (status) {
    case 'Provisioning':
      return { stop: true, start: false, restart: false, delete: true }
    case 'Running':
      return { stop: true, start: false, restart: true, delete: true }
    case 'Stopped':
    case 'Failed':
      return { stop: false, start: true, restart: false, delete: true }
    case 'Expired':
      return { stop: false, start: false, restart: false, delete: false }
  }
}

export type SortKey = 'newest' | 'oldest' | 'name-asc' | 'name-desc' | 'expiring' | 'status'

export const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'newest', label: 'Newest first' },
  { value: 'oldest', label: 'Oldest first' },
  { value: 'name-asc', label: 'Name A–Z' },
  { value: 'name-desc', label: 'Name Z–A' },
  { value: 'expiring', label: 'Expiring soonest' },
  { value: 'status', label: 'Status' },
]

export function parseSortKey(value: string | null): SortKey {
  return SORT_OPTIONS.find((option) => option.value === value)?.value ?? 'newest'
}

const statusRank: Record<EnvironmentStatus, number> = {
  Running: 0,
  Provisioning: 1,
  Stopped: 2,
  Failed: 3,
  Expired: 4,
}

const byNewest = (a: WorkspaceEnvironment, b: WorkspaceEnvironment) =>
  Date.parse(b.createdAt) - Date.parse(a.createdAt)

const byName = (a: WorkspaceEnvironment, b: WorkspaceEnvironment) =>
  displayName(a).localeCompare(displayName(b), undefined, { sensitivity: 'base', numeric: true })

export function sortEnvironments(
  environments: WorkspaceEnvironment[],
  key: SortKey,
): WorkspaceEnvironment[] {
  const sorted = [...environments]
  switch (key) {
    case 'newest':
      return sorted.sort(byNewest)
    case 'oldest':
      return sorted.sort((a, b) => -byNewest(a, b))
    case 'name-asc':
      return sorted.sort(byName)
    case 'name-desc':
      return sorted.sort((a, b) => -byName(a, b))
    case 'expiring':
      // Already-expired environments have nothing left to count down, so they go last.
      return sorted.sort((a, b) => {
        const expiredA = a.status === 'Expired' ? 1 : 0
        const expiredB = b.status === 'Expired' ? 1 : 0
        return expiredA - expiredB || Date.parse(a.expiresAt) - Date.parse(b.expiresAt)
      })
    case 'status':
      return sorted.sort((a, b) => statusRank[a.status] - statusRank[b.status] || byNewest(a, b))
  }
}

/** 'all' passes everything; otherwise a resource value ('2', '4096') or 'unlimited' for older environments. */
export interface EnvironmentFilters {
  query: string
  cpu: string
  memory: string
}

export const ALL = 'all'
const UNLIMITED = 'unlimited'

const cpuKey = (env: WorkspaceEnvironment) => (env.cpuCores ? String(env.cpuCores) : UNLIMITED)
const memoryKey = (env: WorkspaceEnvironment) => (env.memoryMb ? String(env.memoryMb) : UNLIMITED)

export function filterEnvironments(
  environments: WorkspaceEnvironment[],
  { query, cpu, memory }: EnvironmentFilters,
): WorkspaceEnvironment[] {
  const needle = query.trim().toLowerCase()
  return environments.filter(
    (env) =>
      (needle === '' ||
        displayName(env).toLowerCase().includes(needle) ||
        env.repoUrl.toLowerCase().includes(needle)) &&
      (cpu === ALL || cpuKey(env) === cpu) &&
      (memory === ALL || memoryKey(env) === memory),
  )
}

export interface FilterOption {
  value: string
  label: string
}

/** The distinct values actually present, numerically ordered, with "Unlimited" (older environments) last. */
function distinctOptions(
  environments: WorkspaceEnvironment[],
  key: (env: WorkspaceEnvironment) => string,
  format: (env: WorkspaceEnvironment) => string,
): FilterOption[] {
  const seen = new Map<string, string>()
  for (const env of environments) seen.set(key(env), format(env))

  const rank = (value: string) => (value === UNLIMITED ? Infinity : Number(value))
  return [...seen]
    .sort(([a], [b]) => rank(a) - rank(b))
    .map(([value, label]) => ({ value, label }))
}

export const cpuFilterOptions = (environments: WorkspaceEnvironment[]) =>
  distinctOptions(environments, cpuKey, (env) => formatCpu(env.cpuCores))

export const memoryFilterOptions = (environments: WorkspaceEnvironment[]) =>
  distinctOptions(environments, memoryKey, (env) => formatMemory(env.memoryMb))
