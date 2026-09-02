import { useEffect, useState, type FormEvent } from 'react'
import './App.css'

interface Property {
  id: number
  name: string
  address: string
}

function App() {
  const [properties, setProperties] = useState<Property[]>([])
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    fetch('http://localhost:5085/api/properties')
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Request failed: ${response.status}`)
        }

        return response.json()
      })
      .then((data: Property[]) => {
        setProperties(data)
      })
      .catch((requestError: Error) => {
        setError(requestError.message)
      })
  }, [])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      const response = await fetch('http://localhost:5085/api/properties', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ name, address }),
      })

      if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`)
      }

      const createdProperty: Property = await response.json()

      setProperties((currentProperties) => [
        ...currentProperties,
        createdProperty,
      ])

      setName('')
      setAddress('')
    } catch (requestError) {
      const message =
        requestError instanceof Error
          ? requestError.message
          : 'An unexpected error occurred'

      setError(message)
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main id="center">
      <h1>Upkeep</h1>
      <p>Home maintenance management</p>

      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="property-name">Name</label>
          <input
            id="property-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
          />
        </div>

        <div>
          <label htmlFor="property-address">Address</label>
          <input
            id="property-address"
            value={address}
            onChange={(event) => setAddress(event.target.value)}
            required
          />
        </div>

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Adding...' : 'Add property'}
        </button>
      </form>

      {error && <p>Unable to complete request: {error}</p>}

      <h2>Properties</h2>

      {properties.length === 0 && !error ? (
        <p>Loading properties...</p>
      ) : (
        <ul>
          {properties.map((property) => (
            <li key={property.id}>
              <strong>{property.name}</strong> — {property.address}
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}

export default App