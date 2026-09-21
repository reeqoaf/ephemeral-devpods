import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { ApiError } from './api/client'

export const ME_QUERY_KEY = ['me'] as const

/**
 * Any 401 from any query or mutation means the session is gone (expired, logged
 * out in another tab). Mark the user signed out so RequireAuth sends the router
 * to /login, instead of each page handling it.
 */
function handleUnauthorized(error: unknown) {
  if (error instanceof ApiError && error.status === 401) {
    queryClient.setQueryData(ME_QUERY_KEY, null)
  }
}

export const queryClient: QueryClient = new QueryClient({
  queryCache: new QueryCache({ onError: handleUnauthorized }),
  mutationCache: new MutationCache({ onError: handleUnauthorized }),
})
