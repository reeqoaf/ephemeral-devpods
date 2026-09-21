import { createContext, useContext } from 'react'
import type { Me } from '../api/types'

export interface AuthState {
  /** The signed-in user, or null when signed out. */
  user: Me | null
  isLoading: boolean
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth must be used inside <AuthProvider>')
  return value
}
