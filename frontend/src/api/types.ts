export type EnvironmentStatus = 'Provisioning' | 'Running' | 'Expired' | 'Failed'

export interface WorkspaceEnvironment {
  environmentId: string
  repoUrl: string
  status: EnvironmentStatus
  ttlMinutes: number
  createdAt: string
  publicUrl?: string
}

export type IdentityProvider = 'Microsoft' | 'GitHub'

export interface LinkedIdentity {
  provider: IdentityProvider
  displayName: string
  linkedAt: string
}

export interface Me {
  userId: string
  displayName: string
  email: string | null
  identities: LinkedIdentity[]
}
