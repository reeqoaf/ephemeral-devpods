import type { PortMapping } from '../api/types'

// Mirrors HostPortSelector.cs, which is the source of truth on the backend.
export const MIN_HOST_PORT = 1
export const MAX_HOST_PORT = 65535

/** What the user typed for each forwarded container port, keyed by container port. */
export type HostPortInputs = Record<number, string>

export function toHostPortInputs(mappings: PortMapping[]): HostPortInputs {
  return Object.fromEntries(mappings.map((m) => [m.containerPort, String(m.hostPort)]))
}

/** A message per container port whose input is invalid, or null when it's fine. */
export function hostPortErrors(inputs: HostPortInputs): Record<number, string | null> {
  const parsed = Object.entries(inputs).map(([container, text]) => ({
    container: Number(container),
    port: /^\d+$/.test(text.trim()) ? Number(text.trim()) : NaN,
  }))

  const counts = new Map<number, number>()
  for (const { port } of parsed) counts.set(port, (counts.get(port) ?? 0) + 1)

  return Object.fromEntries(
    parsed.map(({ container, port }) => [
      container,
      Number.isNaN(port)
        ? 'Enter a port number'
        : port < MIN_HOST_PORT || port > MAX_HOST_PORT
          ? `Use a port from ${MIN_HOST_PORT} to ${MAX_HOST_PORT}`
          : counts.get(port)! > 1
            ? 'Each port needs its own host port'
            : null,
    ]),
  )
}

export function toPortMappings(inputs: HostPortInputs): PortMapping[] {
  return Object.entries(inputs).map(([container, text]) => ({
    containerPort: Number(container),
    hostPort: Number(text.trim()),
  }))
}
