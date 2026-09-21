import type { TunnelProvider } from '../api/types'

export interface Option<T> {
  value: T
  label: string
  disabled?: boolean
  /** Shown next to a disabled option, e.g. "Coming soon". */
  hint?: string
}

// These mirror EnvironmentOptions.cs on the backend, which is the source of truth: it rejects anything
// outside its allowed sets. Widening a list here without widening it there just produces a 400.
export const MAX_NAME_LENGTH = 64
export const TTL_OPTIONS: Option<number>[] = [{ value: 60, label: '1 hour' }]
export const CPU_OPTIONS: Option<number>[] = [{ value: 2, label: '2 vCPU' }]
export const MEMORY_OPTIONS: Option<number>[] = [{ value: 4096, label: '4 GB' }]
export const TUNNEL_PROVIDER_OPTIONS: Option<TunnelProvider>[] = [
  { value: 'GitHub', label: 'GitHub' },
  { value: 'Microsoft', label: 'Microsoft', disabled: true, hint: 'Coming soon' },
]

/** The first option that can actually be picked. */
export function defaultOption<T>(options: Option<T>[]): T {
  return (options.find((option) => !option.disabled) ?? options[0]).value
}

export function formatCpu(cores?: number | null): string {
  return cores ? `${cores} vCPU` : 'Unlimited'
}

export function formatMemory(megabytes?: number | null): string {
  if (!megabytes) return 'Unlimited'
  return megabytes >= 1024 ? `${megabytes / 1024} GB` : `${megabytes} MB`
}

export function formatTtl(minutes: number): string {
  if (minutes < 60) return `${minutes} min`
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  const hoursLabel = `${hours} ${hours === 1 ? 'hour' : 'hours'}`
  return rest === 0 ? hoursLabel : `${hoursLabel} ${rest} min`
}
