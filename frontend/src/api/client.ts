import type {
  CreateEnvironmentInput,
  IdentityProvider,
  Me,
  RepoCheck,
  WorkspaceEnvironment,
} from './types'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/**
 * Thin fetch wrapper. Auth is a backend-issued HttpOnly session cookie, sent
 * automatically by the browser, so there's no token handling here. A 401
 * surfaces as an ApiError; the query client (see queryClient.ts) reacts to it
 * by marking the user signed out, which sends the router to /login.
 */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })

  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new ApiError(response.status, body?.error ?? `Request failed (${response.status})`)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

export const api = {
  listEnvironments: () => request<WorkspaceEnvironment[]>('/environments'),

  /** Also asks the backend to refresh the environment's live status, so it doubles as "refresh". */
  getEnvironment: (environmentId: string) =>
    request<WorkspaceEnvironment>(`/environments/${environmentId}`),

  /** Step 1 of the create flow: validates the repo without creating anything. */
  checkRepo: (repoUrl: string) =>
    request<RepoCheck>('/environments/check', {
      method: 'POST',
      body: JSON.stringify({ repoUrl }),
    }),

  createEnvironment: (input: CreateEnvironmentInput) =>
    request<WorkspaceEnvironment>('/environments', {
      method: 'POST',
      body: JSON.stringify(input),
    }),

  stopEnvironment: (environmentId: string) =>
    request<WorkspaceEnvironment>(`/environments/${environmentId}/stop`, { method: 'POST' }),

  startEnvironment: (environmentId: string) =>
    request<WorkspaceEnvironment>(`/environments/${environmentId}/start`, { method: 'POST' }),

  restartEnvironment: (environmentId: string) =>
    request<WorkspaceEnvironment>(`/environments/${environmentId}/restart`, { method: 'POST' }),

  extendEnvironment: (environmentId: string) =>
    request<WorkspaceEnvironment>(`/environments/${environmentId}/extend`, {
      method: 'POST',
    }),

  deleteEnvironment: (environmentId: string) =>
    request<void>(`/environments/${environmentId}`, { method: 'DELETE' }),

  /** The signed-in user, or null when signed out (401). */
  me: async (): Promise<Me | null> => {
    try {
      return await request<Me>('/me')
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return null
      throw error
    }
  },

  logout: () => request<void>('/auth/logout', { method: 'POST' }),

  unlinkIdentity: (provider: IdentityProvider) =>
    request<void>(`/me/identities/${provider.toLowerCase()}`, { method: 'DELETE' }),
}

/** Sign-in / link start URLs. These are full-page navigations (OAuth redirects), not fetches. */
export const authUrls = {
  login: (provider: IdentityProvider, returnUrl: string) =>
    `/api/auth/login/${provider.toLowerCase()}?returnUrl=${encodeURIComponent(returnUrl)}`,
  link: (provider: IdentityProvider, returnUrl: string) =>
    `/api/auth/link/${provider.toLowerCase()}?returnUrl=${encodeURIComponent(returnUrl)}`,
}
