import { useEffect, useState } from 'react'
import './App.css'

interface Property {
  id: number
  name: string
  address: string
}

function App() {
  const [properties, setProperties] = useState<Property[]>([])
  const [error, setError] = useState<string | null>(null)

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

  return (
    <main id="center">
      <h1>Upkeep</h1>
      <p>Home maintenance management</p>

      {error && <p>Unable to load properties: {error}</p>}

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