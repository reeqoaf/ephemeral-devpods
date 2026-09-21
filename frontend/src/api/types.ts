export type EnvironmentStatus = 'Provisioning' | 'Running' | 'Stopped' | 'Expired' | 'Failed'

export type TunnelPhase = 'Starting' | 'AwaitingLogin' | 'Ready'

/** Which account the environment's VS Code tunnel signs in with. */
export type TunnelProvider = 'GitHub' | 'Microsoft'

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

/** A port the app listens on inside the container, and the port on the host it is published at. */
export interface PortMapping {
  containerPort: number
  hostPort: number
}

export interface WorkspaceEnvironment {
  environmentId: string
  repoUrl: string
  /** Null on environments created before names existed. */
  name?: string | null
  status: EnvironmentStatus
  ttlMinutes: number
  createdAt: string
  expiresAt: string
  /** Null on environments created before limits existed (they run unlimited). */
  cpuCores?: number | null
  memoryMb?: number | null
  tunnelProvider: TunnelProvider
  /** Empty for environments created before host ports were selectable, or without forwarded ports. */
  portMappings: PortMapping[]
  publicUrl?: string
  tunnel?: TunnelInfo | null
}

/** What step 1 of the create flow learns about a repo. */
export interface RepoCheck {
  repoUrl: string
  owner: string
  repo: string
  suggestedName: string
  image?: string | null
  /** Default host port for each forwarded port (the same number as the container port). */
  portMappings: PortMapping[]
  /** Local Docker only: the user picks the host ports. Otherwise there's nothing to choose. */
  hostPortsSelectable: boolean
}

export interface CreateEnvironmentInput {
  repoUrl: string
  name: string
  ttlMinutes: number
  cpuCores: number
  memoryMb: number
  tunnelProvider: TunnelProvider
  /** Only sent when the check said the host ports are selectable. */
  portMappings?: PortMapping[]
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
  /** False when the backend restricts running environments to admins and this user isn't one. */
  canProvision: boolean
  identities: LinkedIdentity[]
}
