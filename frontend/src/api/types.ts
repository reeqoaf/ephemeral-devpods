export type EnvironmentStatus = 'Provisioning' | 'Running' | 'Expired' | 'Failed'

export type TunnelPhase = 'Starting' | 'AwaitingLogin' | 'Ready'

/** The environment's VS Code tunnel. Only present while the environment is Running. */
export interface TunnelInfo {
  phase: TunnelPhase
  /** Set while AwaitingLogin: the code to enter at `verificationUrl`. */
  deviceCode?: string | null
  verificationUrl?: string | null
  /** Set once Ready. */
  webEditorUrl?: string | null
  localEditorUrl?: string | null
}

export interface WorkspaceEnvironment {
  environmentId: string
  repoUrl: string
  status: EnvironmentStatus
  ttlMinutes: number
  createdAt: string
  publicUrl?: string
  tunnel?: TunnelInfo | null
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
