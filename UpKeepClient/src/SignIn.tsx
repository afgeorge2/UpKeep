import { useEffect, useState, type ReactNode } from 'react'
import './App.css'
import { apiFetch, setCsrfToken } from './api'

interface Session {
  authenticated: boolean
  configured: boolean
  name: string | null
  csrfToken: string | null
}

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'Could not connect to UpKeep.'

export default function SignIn({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null)
  const [error, setError] = useState('')
  const [isSigningOut, setIsSigningOut] = useState(false)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    const controller = new AbortController()

    const handleExpiredSession = () => {
      setCsrfToken(null)
      setSession((current) => current
        ? { ...current, authenticated: false, csrfToken: null }
        : null)
      setError('Your session expired. Sign in to continue.')
    }

    window.addEventListener('upkeep-session-expired', handleExpiredSession)

    void fetch('/api/auth/session', {
      cache: 'no-store',
      credentials: 'same-origin',
      signal: controller.signal,
    })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error('Could not check your sign-in status.')
        }

        const currentSession = await response.json() as Session
        if (!controller.signal.aborted) {
          setError('')
          setCsrfToken(currentSession.csrfToken)
          setSession(currentSession)
        }
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(errorMessage(cause))
        }
      })

    return () => {
      controller.abort()
      window.removeEventListener(
        'upkeep-session-expired',
        handleExpiredSession,
      )
    }
  }, [reload])

  async function logout() {
    setIsSigningOut(true)
    setError('')

    try {
      const response = await apiFetch('/api/auth/logout', { method: 'POST' })
      if (!response.ok) {
        throw new Error('Could not sign out. Please try again.')
      }

      setCsrfToken(null)
      setSession((current) => current
        ? { ...current, authenticated: false, csrfToken: null }
        : null)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setIsSigningOut(false)
    }
  }

  if (session?.authenticated) {
    return (
      <>
        {session.configured && (
          <div className="account-bar">
            <span>Signed in as {session.name || 'UpKeep member'}</span>
            <button
              className="account-button"
              disabled={isSigningOut}
              onClick={() => void logout()}
              type="button"
            >
              {isSigningOut ? 'Signing out…' : 'Sign out'}
            </button>
            {error && <span className="error" role="alert">{error}</span>}
          </div>
        )}
        {children}
      </>
    )
  }

  const failed = new URLSearchParams(window.location.search)
    .get('signin') === 'failed'

  return (
    <main className="signin-panel">
      <p className="eyebrow">Home maintenance</p>
      <h1>Welcome to UpKeep</h1>
      <p>Keep your properties and maintenance plans in one private place.</p>

      {!session && !error && <p role="status">Checking your session…</p>}

      {session?.configured && (
        <>
          <p>Sign in or create an account to manage your household.</p>
          <a className="primary-button signin-link" href="/api/auth/login">
            Continue to sign in
          </a>
        </>
      )}

      {session && !session.configured && (
        <p role="status">
          Sign-in has not been configured for this environment yet.
        </p>
      )}

      {failed && (
        <p className="error" role="alert">
          Sign-in could not be completed. Please try again.
        </p>
      )}

      {error && <p className="error" role="alert">{error}</p>}

      {(error || (session && !session.configured)) && (
        <button
          className="account-button"
          onClick={() => {
            setError('')
            setSession(null)
            setReload((value) => value + 1)
          }}
          type="button"
        >
          Check again
        </button>
      )}
    </main>
  )
}
