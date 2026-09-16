export type EnvironmentStatus = 'Provisioning' | 'Running' | 'Expired' | 'Failed'

export interface WorkspaceEnvironment {
  environmentId: string
  owner: string
  repoUrl: string
  status: EnvironmentStatus
  ttlMinutes: number
  createdAt: string
  publicUrl?: string
}
