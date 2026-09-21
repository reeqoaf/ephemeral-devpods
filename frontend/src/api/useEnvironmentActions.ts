import { useMutation, useQueryClient, type MutateOptions } from '@tanstack/react-query'
import { api } from './client'
import type { WorkspaceEnvironment } from './types'

export type EnvironmentAction = 'stop' | 'start' | 'restart' | 'delete'

/** Stop/start/restart return the updated environment; delete returns nothing. */
type ActionResult = WorkspaceEnvironment | void

const actionRequests: Record<EnvironmentAction, (environmentId: string) => Promise<ActionResult>> = {
  stop: api.stopEnvironment,
  start: api.startEnvironment,
  restart: api.restartEnvironment,
  delete: api.deleteEnvironment,
}

/**
 * Lifecycle actions for one environment, shared by its dashboard card and its details page. One
 * mutation runs at a time, so `pending` / `error` describe whichever action was last requested.
 */
export function useEnvironmentActions(environmentId: string) {
  const queryClient = useQueryClient()

  const mutation = useMutation<ActionResult, Error, EnvironmentAction>({
    mutationFn: (action) => actionRequests[action](environmentId),
    onSuccess: async (updated) => {
      if (updated) {
        queryClient.setQueryData(['environment', environmentId], updated)
      }
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['environments'] }),
        queryClient.invalidateQueries({ queryKey: ['environment', environmentId] }),
      ])
    },
  })

  return {
    run: (
      action: EnvironmentAction,
      options?: MutateOptions<ActionResult, Error, EnvironmentAction>,
    ) => mutation.mutate(action, options),
    /** The action currently in flight, if any. */
    pendingAction: mutation.isPending ? mutation.variables : undefined,
    pending: mutation.isPending,
    error: mutation.error,
    reset: mutation.reset,
  }
}
