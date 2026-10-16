let csrfToken: string | null = null

export function setCsrfToken(token: string | null) {
  csrfToken = token
}

export async function apiFetch(input: string, init: RequestInit = {}) {
  if (!input.startsWith('/api/')) {
    throw new Error('Expected an UpKeep API path.')
  }

  const headers = new Headers(init.headers)
  const method = (init.method ?? 'GET').toUpperCase()

  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    if (!csrfToken) {
      throw new Error(
        'Your session needs to be refreshed. Please sign in again.',
      )
    }

    headers.set('X-CSRF-TOKEN', csrfToken)
  }

  const response = await fetch(input, {
    ...init,
    headers,
    credentials: 'same-origin',
  })

  if (response.status === 401) {
    csrfToken = null
    window.dispatchEvent(new Event('upkeep-session-expired'))
    throw new Error('Your session expired. Please sign in again.')
  }

  return response
}
