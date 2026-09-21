/** Human-readable text for the `?error=` codes the backend's OAuth callback redirects with. */
const MESSAGES: Record<string, string> = {
  github_not_linked:
    "That GitHub account isn't linked to any user yet. Sign in with Microsoft first, then link GitHub from Account settings.",
  microsoft_not_linked: "That Microsoft account isn't linked to any user.",
  invalid_state: 'The sign-in attempt expired or was invalid. Please try again.',
  access_denied: 'Sign-in was cancelled.',
  provider_error: "The provider couldn't complete the request. Please try again.",
  session_mismatch: 'Your session changed while linking. Please sign in and try again.',
  already_linked: 'That account is already linked to a different user.',
  provider_already_linked: 'You already have an account linked for that provider. Unlink it first.',
}

export function describeAuthError(code: string | null): string | null {
  if (!code) return null
  return MESSAGES[code] ?? 'Something went wrong. Please try again.'
}

/** Only same-origin relative paths are honoured as a post-login destination. */
export function safeReturnUrl(candidate: string | null, fallback = '/'): string {
  if (!candidate || !candidate.startsWith('/') || candidate.startsWith('//') || candidate.startsWith('/\\')) {
    return fallback
  }
  return candidate
}
