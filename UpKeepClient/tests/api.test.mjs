import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { apiFetch, setCsrfToken } from '../src/api.ts'

const originalFetch = globalThis.fetch
const originalWindow = globalThis.window

afterEach(() => {
  setCsrfToken(null)
  globalThis.fetch = originalFetch
  globalThis.window = originalWindow
})

test('rejects requests outside the UpKeep API', async () => {
  await assert.rejects(
    apiFetch('https://example.com/collect'),
    /Expected an UpKeep API path/,
  )
})

test('sends reads with same-origin credentials', async () => {
  let request
  globalThis.fetch = async (input, init) => {
    request = { input, init }
    return new Response(null, { status: 200 })
  }

  await apiFetch('/api/properties')

  assert.equal(request.input, '/api/properties')
  assert.equal(request.init.credentials, 'same-origin')
  assert.equal(request.init.headers.has('X-CSRF-TOKEN'), false)
})

test('requires and sends the CSRF token for mutations', async () => {
  await assert.rejects(
    apiFetch('/api/properties', { method: 'POST' }),
    /session needs to be refreshed/,
  )

  let request
  globalThis.fetch = async (input, init) => {
    request = { input, init }
    return new Response(null, { status: 201 })
  }
  setCsrfToken('verified-token')

  await apiFetch('/api/properties', { method: 'POST' })

  assert.equal(request.init.headers.get('X-CSRF-TOKEN'), 'verified-token')
})

test('clears the token and announces an expired session on 401', async () => {
  const events = []
  globalThis.window = {
    dispatchEvent(event) {
      events.push(event.type)
    },
  }
  globalThis.fetch = async () => new Response(null, { status: 401 })
  setCsrfToken('stale-token')

  await assert.rejects(
    apiFetch('/api/properties'),
    /session expired/,
  )

  assert.deepEqual(events, ['upkeep-session-expired'])
  await assert.rejects(
    apiFetch('/api/properties', { method: 'DELETE' }),
    /session needs to be refreshed/,
  )
})
