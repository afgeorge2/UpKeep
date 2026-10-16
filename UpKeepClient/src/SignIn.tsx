import { useEffect, useState, type ReactNode } from 'react'
import { apiFetch, setCsrfToken } from './api'
import './App.css'

interface Session { authenticated: boolean; configured: boolean; name: string | null; signInUrl: string; csrfToken: string | null }

export default function SignIn({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [reload, setReload] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    const expired = () => {
      setCsrfToken(null)
      setSession(current => current ? { ...current, authenticated: false, csrfToken: null } : null)
      setError('Your session expired. Sign in to continue.')
    }
    window.addEventListener('upkeep-session-expired', expired)
    void fetch('/api/auth/session', { signal: controller.signal, cache: 'no-store' }).then(async response => {
      if (!response.ok) throw new Error('Could not check your sign-in status.')
      const value = await response.json() as Session
      if (!controller.signal.aborted) { setCsrfToken(value.csrfToken); setSession(value) }
    }).catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Could not connect.') })
    return () => { controller.abort(); window.removeEventListener('upkeep-session-expired', expired) }
  }, [reload])

  async function logout() {
    setBusy(true); setError('')
    try {
      const response = await apiFetch('/api/auth/logout', { method: 'POST' })
      if (!response.ok) throw new Error('Could not sign out. Please try again.')
      setCsrfToken(null)
      setSession(current => current ? { ...current, authenticated: false, csrfToken: null } : null)
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Could not sign out.') }
    finally { setBusy(false) }
  }

  if (session?.authenticated) return <>
    <div className="account-bar"><span>Signed in as {session.name || 'UpKeep member'}</span><button className="task-filter" disabled={busy} onClick={() => void logout()}>Sign out</button>{error && <span role="alert">{error}</span>}</div>
    {children}
  </>

  const failed = new URLSearchParams(window.location.search).get('signin') === 'failed'
  return <main className="signin-panel">
    <p className="eyebrow">HOME MAINTENANCE</p><h1>Welcome to UpKeep</h1>
    <p>Keep your properties, maintenance plans, and completed work in one place.</p>
    {!session && !error && <p role="status">Checking your session…</p>}
    {session?.configured && <><p>Sign in or create an account to manage your household.</p><a className="primary-button signin-link" href={session.signInUrl}>Continue to sign in</a></>}
    {session && !session.configured && <p role="status">Sign-in is being set up. Your saved maintenance data is protected and will be available after your account is connected.</p>}
    {failed && <p role="alert">Sign-in could not be completed. Please try again.</p>}
    {error && <p role="alert">{error}</p>}
    {(error || !session?.configured) && <button className="task-filter" onClick={() => { setError(''); setReload(value => value + 1) }}>Check again</button>}
  </main>
}
