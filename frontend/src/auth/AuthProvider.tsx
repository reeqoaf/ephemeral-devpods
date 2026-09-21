import { useMemo, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { ME_QUERY_KEY } from '../queryClient'
import { AuthContext, type AuthState } from './AuthContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: api.me,
    retry: false,
    staleTime: 5 * 60 * 1000,
  })

  const value = useMemo<AuthState>(
    () => ({
      user: data ?? null,
      isLoading,
      logout: async () => {
        await api.logout()
        await queryClient.cancelQueries()
        // Update ['me'] in place rather than queryClient.clear(): clearing removes the query this provider's
        // observer is attached to, so the "signed out" update would never reach it.
        queryClient.setQueryData(ME_QUERY_KEY, null)
        // Drop everything else so the next user on this browser can never see the previous user's data.
        queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== ME_QUERY_KEY[0] })
      },
    }),
    [data, isLoading, queryClient],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
