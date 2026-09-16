import type { WorkspaceEnvironment } from './types'

/**
 * Thin fetch wrapper. Auth is Easy Auth on the Function App: the session
 * cookie is sent automatically by the browser, so there's no token handling
 * here — just redirect to Easy Auth's login endpoint on a 401.
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

  if (response.status === 401) {
    window.location.href = `/.auth/login/aad?post_login_redirect_uri=${encodeURIComponent(window.location.pathname)}`
    throw new Error('Unauthenticated')
  }

  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${await response.text()}`)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}

export const api = {
  listEnvironments: () => request<WorkspaceEnvironment[]>('/environments'),

  createEnvironment: (repoUrl: string) =>
    request<WorkspaceEnvironment>('/environments', {
      method: 'POST',
      body: JSON.stringify({ repoUrl }),
    }),

  extendEnvironment: (environmentId: string) =>
    request<WorkspaceEnvironment>(`/environments/${environmentId}/extend`, {
      method: 'POST',
    }),

  deleteEnvironment: (environmentId: string) =>
    request<void>(`/environments/${environmentId}`, { method: 'DELETE' }),
}
