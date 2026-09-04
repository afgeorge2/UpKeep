import { useEffect, useState, type FormEvent } from 'react'
import './App.css'

const apiBaseUrl = '/api'

interface Property { id: number; name: string; address: string }
interface MaintenanceTask { id: number; title: string; description: string; dueDate: string; isCompleted: boolean; propertyId: number }

const getErrorMessage = (error: unknown) => error instanceof Error ? error.message : 'An unexpected error occurred'

function formatDueDate(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(year, month - 1, day))
}

function App() {
  const [properties, setProperties] = useState<Property[]>([])
  const [selectedPropertyId, setSelectedPropertyId] = useState<number | null>(null)
  const [tasks, setTasks] = useState<MaintenanceTask[]>([])
  const [propertyName, setPropertyName] = useState('')
  const [propertyAddress, setPropertyAddress] = useState('')
  const [taskTitle, setTaskTitle] = useState('')
  const [taskDescription, setTaskDescription] = useState('')
  const [taskDueDate, setTaskDueDate] = useState('')
  const [propertyError, setPropertyError] = useState<string | null>(null)
  const [propertyDeleteError, setPropertyDeleteError] = useState<string | null>(null)
  const [taskError, setTaskError] = useState<string | null>(null)
  const [isLoadingProperties, setIsLoadingProperties] = useState(true)
  const [isLoadingTasks, setIsLoadingTasks] = useState(false)
  const [isAddingProperty, setIsAddingProperty] = useState(false)
  const [isDeletingProperty, setIsDeletingProperty] = useState(false)
  const [isAddingTask, setIsAddingTask] = useState(false)
  const [updatingTaskId, setUpdatingTaskId] = useState<number | null>(null)
  const [deletingTaskId, setDeletingTaskId] = useState<number | null>(null)

  const selectedProperty = properties.find((property) => property.id === selectedPropertyId)

  useEffect(() => {
    async function loadProperties() {
      try {
        const response = await fetch(`${apiBaseUrl}/properties`)
        if (!response.ok) throw new Error(`Request failed: ${response.status}`)
        const data: Property[] = await response.json()
        setProperties(data)
        setSelectedPropertyId(data[0]?.id ?? null)
      } catch (error) {
        setPropertyError(getErrorMessage(error))
      } finally {
        setIsLoadingProperties(false)
      }
    }
    void loadProperties()
  }, [])

  useEffect(() => {
    if (selectedPropertyId === null) {
      return
    }
    const controller = new AbortController()
    async function loadTasks() {
      setIsLoadingTasks(true)
      setTaskError(null)
      try {
        const response = await fetch(`${apiBaseUrl}/properties/${selectedPropertyId}/maintenance-tasks`, { signal: controller.signal })
        if (!response.ok) throw new Error(`Request failed: ${response.status}`)
        setTasks(await response.json() as MaintenanceTask[])
      } catch (error) {
        if (!(error instanceof DOMException && error.name === 'AbortError')) setTaskError(getErrorMessage(error))
      } finally {
        if (!controller.signal.aborted) setIsLoadingTasks(false)
      }
    }
    void loadTasks()
    return () => controller.abort()
  }, [selectedPropertyId])

  async function handlePropertySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPropertyError(null)
    setIsAddingProperty(true)
    try {
      const response = await fetch(`${apiBaseUrl}/properties`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: propertyName, address: propertyAddress }),
      })
      if (!response.ok) throw new Error(`Request failed: ${response.status}`)
      const createdProperty: Property = await response.json()
      setProperties((current) => [...current, createdProperty])
      setSelectedPropertyId(createdProperty.id)
      setPropertyName('')
      setPropertyAddress('')
    } catch (error) {
      setPropertyError(getErrorMessage(error))
    } finally {
      setIsAddingProperty(false)
    }
  }

  async function handleTaskSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (selectedPropertyId === null) return
    setTaskError(null)
    setIsAddingTask(true)
    try {
      const response = await fetch(`${apiBaseUrl}/properties/${selectedPropertyId}/maintenance-tasks`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ title: taskTitle, description: taskDescription, dueDate: taskDueDate }),
      })
      if (!response.ok) throw new Error(`Request failed: ${response.status}`)
      const createdTask: MaintenanceTask = await response.json()
      setTasks((current) => [...current, createdTask].sort((a, b) => a.dueDate.localeCompare(b.dueDate)))
      setTaskTitle('')
      setTaskDescription('')
      setTaskDueDate('')
    } catch (error) {
      setTaskError(getErrorMessage(error))
    } finally {
      setIsAddingTask(false)
    }
  }

  async function handleTaskStatusChange(task: MaintenanceTask) {
    setTaskError(null)
    setUpdatingTaskId(task.id)
    try {
      const response = await fetch(`${apiBaseUrl}/properties/${task.propertyId}/maintenance-tasks/${task.id}/status`, {
        method: 'PATCH', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isCompleted: !task.isCompleted }),
      })
      if (!response.ok) throw new Error(`Request failed: ${response.status}`)
      const updatedTask: MaintenanceTask = await response.json()
      setTasks((current) => current.map((item) => item.id === updatedTask.id ? updatedTask : item))
    } catch (error) {
      setTaskError(getErrorMessage(error))
    } finally {
      setUpdatingTaskId(null)
    }
  }

  async function handlePropertyDelete() {
    if (!selectedProperty) return

    const shouldDelete = window.confirm(
      `Delete "${selectedProperty.name}" and all of its maintenance tasks? This cannot be undone.`,
    )

    if (!shouldDelete) return

    setPropertyDeleteError(null)
    setIsDeletingProperty(true)
    try {
      const response = await fetch(`${apiBaseUrl}/properties/${selectedProperty.id}`, {
        method: 'DELETE',
      })
      if (!response.ok) throw new Error(`Request failed: ${response.status}`)

      const remainingProperties = properties.filter(
        (property) => property.id !== selectedProperty.id,
      )
      setProperties(remainingProperties)
      setSelectedPropertyId(remainingProperties[0]?.id ?? null)
      setTasks([])
    } catch (error) {
      setPropertyDeleteError(getErrorMessage(error))
    } finally {
      setIsDeletingProperty(false)
    }
  }

  async function handleTaskDelete(task: MaintenanceTask) {
    const shouldDelete = window.confirm(
      `Delete "${task.title}"? This cannot be undone.`,
    )

    if (!shouldDelete) return

    setTaskError(null)
    setDeletingTaskId(task.id)
    try {
      const response = await fetch(
        `${apiBaseUrl}/properties/${task.propertyId}/maintenance-tasks/${task.id}`,
        { method: 'DELETE' },
      )
      if (!response.ok) throw new Error(`Request failed: ${response.status}`)

      setTasks((current) => current.filter((item) => item.id !== task.id))
    } catch (error) {
      setTaskError(getErrorMessage(error))
    } finally {
      setDeletingTaskId(null)
    }
  }

  return (
    <main className="app-shell">
      <header className="app-header">
        <div><p className="eyebrow">Home maintenance</p><h1>UpKeep</h1><p className="intro">Keep every property and its next job in one place.</p></div>
        <div className="summary" aria-label="Account summary"><span>{properties.length}</span>{properties.length === 1 ? ' property' : ' properties'}</div>
      </header>

      <section className="workspace">
        <aside className="sidebar">
          <div className="section-heading"><div><p className="eyebrow">Portfolio</p><h2>Properties</h2></div></div>
          {isLoadingProperties ? <p className="muted">Loading properties…</p> : properties.length === 0 ? (
            <p className="empty-copy">Add your first property to start planning maintenance.</p>
          ) : (
            <nav className="property-list" aria-label="Properties">
              {properties.map((property) => (
                <button className={property.id === selectedPropertyId ? 'property active' : 'property'} key={property.id} onClick={() => setSelectedPropertyId(property.id)} type="button">
                  <strong>{property.name}</strong><span>{property.address}</span>
                </button>
              ))}
            </nav>
          )}

          <form className="compact-form" onSubmit={handlePropertySubmit}>
            <h3>Add a property</h3>
            <label htmlFor="property-name">Name</label>
            <input id="property-name" maxLength={100} onChange={(event) => setPropertyName(event.target.value)} placeholder="Lake house" required value={propertyName} />
            <label htmlFor="property-address">Address</label>
            <input id="property-address" maxLength={200} onChange={(event) => setPropertyAddress(event.target.value)} placeholder="123 Main Street" required value={propertyAddress} />
            <button className="secondary-button" disabled={isAddingProperty} type="submit">{isAddingProperty ? 'Adding…' : 'Add property'}</button>
            {propertyError && <p className="error" role="alert">{propertyError}</p>}
          </form>
        </aside>

        <section className="task-panel">
          {selectedProperty ? <>
            <div className="section-heading task-heading">
              <div><p className="eyebrow">Maintenance plan</p><h2>{selectedProperty.name}</h2><p className="muted">{selectedProperty.address}</p></div>
              <div className="property-actions">
                <span className="task-count">{tasks.length} {tasks.length === 1 ? 'task' : 'tasks'}</span>
                <button className="delete-property-button" disabled={isDeletingProperty} onClick={() => void handlePropertyDelete()} type="button">{isDeletingProperty ? 'Deleting…' : 'Delete property'}</button>
              </div>
            </div>

            {propertyDeleteError && <p className="error panel-error" role="alert">{propertyDeleteError}</p>}

            <form className="task-form" onSubmit={handleTaskSubmit}>
              <div className="form-grid">
                <div className="field"><label htmlFor="task-title">Task</label><input id="task-title" maxLength={100} onChange={(event) => setTaskTitle(event.target.value)} placeholder="Clean the gutters" required value={taskTitle} /></div>
                <div className="field"><label htmlFor="task-due-date">Due date</label><input id="task-due-date" onChange={(event) => setTaskDueDate(event.target.value)} required type="date" value={taskDueDate} /></div>
                <div className="field description-field"><label htmlFor="task-description">Description</label><textarea id="task-description" maxLength={500} onChange={(event) => setTaskDescription(event.target.value)} placeholder="Add any supplies, measurements, or other details." required rows={3} value={taskDescription} /></div>
              </div>
              <button className="primary-button" disabled={isAddingTask} type="submit">{isAddingTask ? 'Scheduling…' : 'Schedule task'}</button>
            </form>

            {taskError && <p className="error panel-error" role="alert">{taskError}</p>}
            <div className="tasks" aria-live="polite">
              {isLoadingTasks ? <p className="empty-state">Loading maintenance tasks…</p> : tasks.length === 0 ? (
                <div className="empty-state"><strong>No maintenance scheduled</strong><span>Add a task above to start building this property’s plan.</span></div>
              ) : tasks.map((task) => (
                <article className="task-card" key={task.id}>
                  <div className={task.isCompleted ? 'status-dot complete' : 'status-dot'} aria-hidden="true" />
                  <div className="task-content">
                    <div className="task-title-row">
                      <h3>{task.title}</h3>
                      <div className="task-actions">
                        <button className={task.isCompleted ? 'status complete' : 'status'} disabled={updatingTaskId === task.id || deletingTaskId === task.id} onClick={() => void handleTaskStatusChange(task)} type="button">{updatingTaskId === task.id ? 'Saving…' : task.isCompleted ? 'Completed' : 'Mark complete'}</button>
                        <button aria-label={`Delete ${task.title}`} className="delete-task-button" disabled={deletingTaskId === task.id || updatingTaskId === task.id} onClick={() => void handleTaskDelete(task)} type="button">{deletingTaskId === task.id ? 'Deleting…' : 'Delete'}</button>
                      </div>
                    </div>
                    <p>{task.description}</p><time dateTime={task.dueDate}>Due {formatDueDate(task.dueDate)}</time>
                  </div>
                </article>
              ))}
            </div>
          </> : (
            <div className="no-selection"><p className="eyebrow">Maintenance plan</p><h2>Choose a property</h2><p>Add or select a property to see its maintenance tasks.</p></div>
          )}
        </section>
      </section>
    </main>
  )
}

export default App
