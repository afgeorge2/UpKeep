import { useEffect, useState } from 'react'
import './App.css'

interface WeatherForecast {
  date: string
  temperatureC: number
  temperatureF: number
  summary: string | null
}

function App() {
  const [forecasts, setForecasts] = useState<WeatherForecast[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetch('http://localhost:5085/weatherforecast')
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Request failed: ${response.status}`)
        }

        return response.json()
      })
      .then((data: WeatherForecast[]) => {
        setForecasts(data)
      })
      .catch((requestError: Error) => {
        setError(requestError.message)
      })
  }, [])

  return (
    <main id="center">
      <h1>Upkeep</h1>
      <p>Home maintenance management</p>

      {error && <p>Unable to load forecasts: {error}</p>}

      {forecasts.length === 0 && !error ? (
        <p>Loading forecasts...</p>
      ) : (
        <ul>
          {forecasts.map((forecast) => (
            <li key={forecast.date}>
              {forecast.date}: {forecast.temperatureC}°C — {forecast.summary}
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}

export default App
