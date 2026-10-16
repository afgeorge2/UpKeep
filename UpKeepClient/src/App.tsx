import { apiFetch as fetch } from './api'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import './App.css'
import TaskDetails from './TaskDetails'
import { defaultTaskDetails, type TaskDetailsValue } from './taskPlanning'
import GuidedPlan from './GuidedPlan'
import EquipmentPlan from './EquipmentLabelPlan'
import AiEquipmentPlan from './AiEquipmentPlan'
import PostponeTask from './PostponeTask'
import UpcomingMaintenance from './UpcomingMaintenance'

const apiBaseUrl = '/api'

interface Property { id: number; name: string; address: string }
interface MaintenanceTask extends TaskDetailsValue { isSkipped: boolean; previousCompletionNotes?: string | null; id: number; title: string; description: string; dueDate: string; isCompleted: boolean; propertyId: number; repeatEveryMonths: number; statusMessage?: string | null; completedAt: string | null; completionNotes: string | null }
const repeatOptions = [0, 1, 3, 6, 12]
const repeatLabel = (months: number) => months === 0 ? 'Does not repeat' : months === 1 ? 'Monthly' : months === 12 ? 'Yearly' : `Every ${months} months`

type TaskFilter = 'Open' | 'Completed' | 'Skipped' | 'All'

const getErrorMessage = (error: unknown) => error instanceof Error ? error.message : 'An unexpected error occurred'

function formatDueDate(value: string) {
  const [year, month, day] = value.split('-').map(Number)
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(year, month - 1, day))
}

function getLocalDateString() {
  const today = new Date()
  const year = today.getFullYear()
  const month = String(today.getMonth() + 1).padStart(2, '0')
  const day = String(today.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function getTaskTiming(task: MaintenanceTask) {
  if (task.isCompleted) {
    return { state: 'complete', label: `Due ${formatDueDate(task.dueDate)}` }
  }

  const today = getLocalDateString()

  if (task.dueDate < today) {
    return { state: 'overdue', label: `Overdue · Was due ${formatDueDate(task.dueDate)}` }
  }

  if (task.dueDate === today) {
    return { state: 'today', label: 'Due today' }
  }

  return { state: 'upcoming', label: `Due ${formatDueDate(task.dueDate)}` }
}

function App() {
  const [taskDetails, setTaskDetails] = useState(defaultTaskDetails)
  const [editTaskDetails, setEditTaskDetails] = useState(defaultTaskDetails)
  const [tasksVersion, setTasksVersion] = useState(0)
  const [showOverview, setShowOverview] = useState(true)
  const focusNewTask = useRef(false)
  const taskTitleInput = useRef<HTMLInputElement>(null)
  const propertyNameInput = useRef<HTMLInputElement>(null)
  const [properties, setProperties] = useState<Property[]>([])
  const [selectedPropertyId, setSelectedPropertyId] = useState<number | null>(null)
  const [tasks, setTasks] = useState<MaintenanceTask[]>([])
  const [completionNotice, setCompletionNotice] = useState<{ task: MaintenanceTask; message: string } | null>(null)
  const statusRequestPending = useRef(false)
  const activePropertyId = useRef<number | null>(null)
  const [taskFilter, setTaskFilter] = useState<TaskFilter>('Open')
  const [pageSize, setPageSize] = useState(10)
  const [pagination, setPagination] = useState({ context: '', page: 1 })
  const [propertyName, setPropertyName] = useState('')
  const [propertyAddress, setPropertyAddress] = useState('')
  const [repeatEveryMonths, setRepeatEveryMonths] = useState(0)
  const [editRepeatEveryMonths, setEditRepeatEveryMonths] = useState(0)
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
  const [editingTaskId, setEditingTaskId] = useState<number | null>(null)
  const [editCompletionNotes, setEditCompletionNotes] = useState('')
  const [editTaskTitle, setEditTaskTitle] = useState('')
  const [editTaskDescription, setEditTaskDescription] = useState('')
  const [editTaskDueDate, setEditTaskDueDate] = useState('')
  const [isSavingTask, setIsSavingTask] = useState(false)

  const selectedProperty = properties.find((property) => property.id === selectedPropertyId)
  const propertyTasks = tasks.filter((task) => task.propertyId === selectedPropertyId)
  const completedCount = propertyTasks.filter((task) => task.isCompleted && !task.isSkipped).length
  const skippedCount = propertyTasks.filter((task) => task.isSkipped).length
  const taskCounts = { Open: propertyTasks.length - completedCount - skippedCount, Completed: completedCount, Skipped: skippedCount, All: propertyTasks.length }
  const visibleTasks = propertyTasks
    .filter((task) => taskFilter === 'All' || (taskFilter === 'Skipped' ? task.isSkipped : taskFilter === 'Completed' ? task.isCompleted && !task.isSkipped : !task.isCompleted))
    .sort((a, b) => a.dueDate.localeCompare(b.dueDate))
  const paginationContext = `${selectedPropertyId}:${taskFilter}:${pageSize}`
  const pageCount = Math.max(1, Math.ceil(visibleTasks.length / pageSize))
  const currentPage = pagination.context === paginationContext ? Math.min(pagination.page, pageCount) : 1
  // Reset on view changes and stay on a valid page when tasks are removed.
  if (pagination.context !== paginationContext || pagination.page !== currentPage) {
    setPagination({ context: paginationContext, page: currentPage })
  }
  const pageStart = (currentPage - 1) * pageSize
  const pagedTasks = visibleTasks.slice(pageStart, pageStart + pageSize)

  useEffect(() => {
    if (!showOverview && focusNewTask.current && taskTitleInput.current) {
      focusNewTask.current = false
      taskTitleInput.current.focus()
      taskTitleInput.current.scrollIntoView({ block: 'center', behavior: 'smooth' })
    }
  }, [showOverview, selectedPropertyId])

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
    if (showOverview || selectedPropertyId === null) {
      return
    }
    activePropertyId.current = selectedPropertyId
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
    return () => { controller.abort(); activePropertyId.current = null }
  }, [selectedPropertyId, showOverview, tasksVersion])

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
      setShowOverview(false)
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
        body: JSON.stringify({ title: taskTitle, description: taskDescription, dueDate: taskDueDate, repeatEveryMonths, ...taskDetails }),
      })
      if (!response.ok) throw new Error(`Request failed: ${response.status}`)
      const createdTask: MaintenanceTask = await response.json()
      setTasks((current) => [...current, createdTask].sort((a, b) => a.dueDate.localeCompare(b.dueDate)))
      setTaskTitle('')
      setTaskDescription('')
      setTaskDueDate('')
      setRepeatEveryMonths(0)
      setTaskDetails(defaultTaskDetails)
    } catch (error) {
      setTaskError(getErrorMessage(error))
    } finally {
      setIsAddingTask(false)
    }
  }

  async function handleTaskStatusChange(task: MaintenanceTask, skip = false) {
    if (statusRequestPending.current) return
    statusRequestPending.current = true
    setTaskError(null)
    setUpdatingTaskId(task.id)
    try {
      const response = await fetch(`${apiBaseUrl}/properties/${task.propertyId}/maintenance-tasks/${task.id}/status`, {
        method: 'PATCH', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isCompleted: !task.isCompleted, isSkipped: skip, localDate: getLocalDateString() }),
      })
      if (!response.ok) throw new Error(`Could not ${task.isCompleted ? 'reopen' : 'complete'} the task. Refresh and try again.`)
      const updatedTask: MaintenanceTask = await response.json()
      setCompletionNotice({ task: updatedTask, message: updatedTask.statusMessage ?? `“${updatedTask.title}” ${updatedTask.isSkipped ? 'skipped' : updatedTask.isCompleted ? 'completed' : 'reopened'}.` })
      setTasks((current) => current.map((item) => item.id === updatedTask.id ? updatedTask : item))
      {
        const listResponse = await fetch(`${apiBaseUrl}/properties/${task.propertyId}/maintenance-tasks`)
        if (!listResponse.ok) throw new Error('Task saved, but the task list could not be refreshed. Refresh to try again.')
        const refreshedTasks = await listResponse.json() as MaintenanceTask[]
        if (activePropertyId.current === task.propertyId) setTasks(refreshedTasks)
      }
    } catch (error) {
      setTaskError(getErrorMessage(error))
    } finally {
      statusRequestPending.current = false
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

  function startTaskEdit(task: MaintenanceTask) {
    setTaskError(null)
    setEditingTaskId(task.id)
    setEditRepeatEveryMonths(task.repeatEveryMonths)
    setEditCompletionNotes(task.completionNotes ?? '')
    setEditTaskDetails({ estimatedMinutes: task.estimatedMinutes, supplies: task.supplies, instructions: task.instructions, lastPurchase: task.lastPurchase, repeatFromCompletion: task.repeatFromCompletion })
    setEditTaskTitle(task.title)
    setEditTaskDescription(task.description)
    setEditTaskDueDate(task.dueDate)
  }

  function cancelTaskEdit() {
    setEditingTaskId(null)
  }

  async function handleTaskEditSubmit(
    event: FormEvent<HTMLFormElement>,
    task: MaintenanceTask,
  ) {
    event.preventDefault()
    setTaskError(null)
    setIsSavingTask(true)
    try {
      const response = await fetch(
        `${apiBaseUrl}/properties/${task.propertyId}/maintenance-tasks/${task.id}`,
        {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            title: editTaskTitle,
            description: editTaskDescription,
            dueDate: editTaskDueDate,
            repeatEveryMonths: editRepeatEveryMonths,
            ...editTaskDetails,
            completionNotes: task.isCompleted && !task.isSkipped ? editCompletionNotes : undefined,
          }),
        },
      )
      if (!response.ok) throw new Error(`Request failed: ${response.status}`)

      const updatedTask: MaintenanceTask = await response.json()
      setTasks((current) => current
        .map((item) => item.id === updatedTask.id ? updatedTask : item)
        .sort((a, b) => a.dueDate.localeCompare(b.dueDate)))
      setEditingTaskId(null)
    } catch (error) {
      setTaskError(getErrorMessage(error))
    } finally {
      setIsSavingTask(false)
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
          <button type="button" className={showOverview ? 'property active overview-link' : 'property overview-link'} aria-pressed={showOverview} onClick={() => setShowOverview(true)}><strong>Upcoming maintenance</strong><span>Across all properties</span></button>
          <div className="section-heading"><div><p className="eyebrow">Portfolio</p><h2>Properties</h2></div></div>
          {isLoadingProperties ? <p className="muted">Loading properties…</p> : properties.length === 0 ? (
            <p className="empty-copy">Add your first property to start planning maintenance.</p>
          ) : (
            <nav className="property-list" aria-label="Properties">
              {properties.map((property) => (
                <button className={!showOverview && property.id === selectedPropertyId ? 'property active' : 'property'} key={property.id} onClick={() => { setShowOverview(false); setSelectedPropertyId(property.id) }} type="button">
                  <strong>{property.name}</strong><span>{property.address}</span>
                </button>
              ))}
            </nav>
          )}

          <form className="compact-form" onSubmit={handlePropertySubmit}>
            <h3>Add a property</h3>
            <label htmlFor="property-name">Name</label>
            <input ref={propertyNameInput} id="property-name" maxLength={100} onChange={(event) => setPropertyName(event.target.value)} placeholder="Lake house" required value={propertyName} />
            <label htmlFor="property-address">Address</label>
            <input id="property-address" maxLength={200} onChange={(event) => setPropertyAddress(event.target.value)} placeholder="123 Main Street" required value={propertyAddress} />
            <button className="secondary-button" disabled={isAddingProperty} type="submit">{isAddingProperty ? 'Adding…' : 'Add property'}</button>
            {propertyError && <p className="error" role="alert">{propertyError}</p>}
          </form>
        </aside>

        <section className="task-panel">
          {showOverview ? <UpcomingMaintenance properties={properties} loadingProperties={isLoadingProperties} onAddProperty={() => { propertyNameInput.current?.focus(); propertyNameInput.current?.scrollIntoView({ block: 'center', behavior: 'smooth' }) }} onAddTask={(id) => { focusNewTask.current = true; setSelectedPropertyId(id); setShowOverview(false) }} onSelectProperty={(id) => { setSelectedPropertyId(id); setShowOverview(false) }} /> : selectedProperty ? <>
            <div className="section-heading task-heading">
              <div><p className="eyebrow">Maintenance plan</p><h2>{selectedProperty.name}</h2><p className="muted">{selectedProperty.address}</p></div>
              <div className="property-actions">
                <span className="task-count">{propertyTasks.length} {propertyTasks.length === 1 ? 'task' : 'tasks'}</span>
                <button className="delete-property-button" disabled={isDeletingProperty} onClick={() => void handlePropertyDelete()} type="button">{isDeletingProperty ? 'Deleting…' : 'Delete property'}</button>
              </div>
            </div>

            {propertyDeleteError && <p className="error panel-error" role="alert">{propertyDeleteError}</p>}

            <AiEquipmentPlan key={'ai-' + selectedProperty.id} propertyId={selectedProperty.id} onAdded={() => setTasksVersion(v => v + 1)} />
            <EquipmentPlan key={'equipment-' + selectedProperty.id} propertyId={selectedProperty.id} onAdded={() => setTasksVersion(v => v + 1)} />
            <GuidedPlan key={selectedProperty.id} propertyId={selectedProperty.id} onAdded={() => setTasksVersion(v => v + 1)} />
            <form className="task-form" onSubmit={handleTaskSubmit}>
              <div className="form-grid">
                <div className="field"><label htmlFor="task-title">Task</label><input ref={taskTitleInput} id="task-title" maxLength={100} onChange={(event) => setTaskTitle(event.target.value)} placeholder="Clean the gutters" required value={taskTitle} /></div>
                <div className="field"><label htmlFor="task-due-date">Due date</label><input id="task-due-date" onChange={(event) => setTaskDueDate(event.target.value)} required type="date" value={taskDueDate} /></div>
                <div className="field"><label htmlFor="task-repeat">Repeat</label><select id="task-repeat" value={repeatEveryMonths} onChange={(event) => setRepeatEveryMonths(Number(event.target.value))}>{repeatOptions.map((months) => <option key={months} value={months}>{repeatLabel(months)}</option>)}</select></div>
                <div className="field description-field"><label htmlFor="task-description">Description</label><textarea id="task-description" maxLength={500} onChange={(event) => setTaskDescription(event.target.value)} placeholder="Add any supplies, measurements, or other details." required rows={3} value={taskDescription} /></div>
              </div>
              <TaskDetails prefix="new-task" value={taskDetails} onChange={setTaskDetails} />
              <button className="primary-button" disabled={isAddingTask} type="submit">{isAddingTask ? 'Scheduling…' : 'Schedule task'}</button>
            </form>

            {taskError && <p className="error panel-error" role="alert">{taskError}</p>}
            {completionNotice && completionNotice.task.propertyId === selectedPropertyId && (
              <div className="completion-notice">
                <span role="status">{completionNotice.message}</span>
                {completionNotice.task.isCompleted && propertyTasks.some((task) => task.id === completionNotice.task.id && task.isCompleted) && <button type="button" className="undo-button" disabled={updatingTaskId !== null || deletingTaskId !== null || isSavingTask} onClick={() => void handleTaskStatusChange(completionNotice.task)}>Undo</button>}
                <button type="button" className="cancel-button" aria-label="Dismiss task notification" onClick={() => setCompletionNotice(null)}>Dismiss</button>
              </div>
            )}
            <div className="task-filters" role="group" aria-label="Filter maintenance tasks">
              {(['Open', 'Completed', 'Skipped', 'All'] as const).map((filter) => (
                <button
                  key={filter}
                  className="task-filter"
                  type="button"
                  aria-pressed={taskFilter === filter}
                  onClick={() => setTaskFilter(filter)}
                >
                  {filter} <span className="filter-count">{isLoadingTasks ? '…' : taskCounts[filter]}</span>
                </button>
              ))}
            </div>
            <div className="task-list-controls">
              <label className="page-size" htmlFor="tasks-per-page">Tasks per page
                <select id="tasks-per-page" value={pageSize} onChange={(event) => setPageSize(Number(event.target.value))}>
                  {[5, 10, 20].map((size) => <option key={size} value={size}>{size}</option>)}
                </select>
              </label>
              {!isLoadingTasks && <span className="muted" role="status">{visibleTasks.length === 0 ? '0 tasks' : `${pageStart + 1}–${Math.min(pageStart + pageSize, visibleTasks.length)} of ${visibleTasks.length} tasks`}</span>}
            </div>
            <div className="tasks" aria-live="polite">
              {isLoadingTasks ? <p className="empty-state">Loading maintenance tasks…</p> : propertyTasks.length === 0 ? (
                <div className="empty-state"><strong>No maintenance scheduled</strong><span>Add a task above to start building this property’s plan.</span></div>
              ) : visibleTasks.length === 0 ? (
                <div className="empty-state">
                  <strong>{taskFilter === 'Open' ? 'No open tasks' : taskFilter === 'Skipped' ? 'No skipped tasks' : 'No completed tasks'}</strong>
                  <span>{taskFilter === 'Open' ? 'You’re all caught up. View Completed to see finished work, or schedule a new task above.' : taskFilter === 'Skipped' ? 'Skipped recurring occurrences will appear here.' : 'Completed tasks will appear here when you mark them complete.'}</span>
                </div>
              ) : pagedTasks.map((task) => {
                const timing = getTaskTiming(task)

                return (
                <article className={`task-card ${timing.state}`} key={task.id}>
                  <div className={`status-dot ${timing.state}`} aria-hidden="true" />
                  <div className="task-content">
                    {editingTaskId === task.id ? (
                      <form className="edit-task-form" onSubmit={(event) => void handleTaskEditSubmit(event, task)}>
                        <div className="edit-task-grid">
                          <div className="field"><label htmlFor={`edit-task-title-${task.id}`}>Task</label><input id={`edit-task-title-${task.id}`} maxLength={100} onChange={(event) => setEditTaskTitle(event.target.value)} required value={editTaskTitle} /></div>
                          <div className="field"><label htmlFor={`edit-task-due-date-${task.id}`}>Due date</label><input id={`edit-task-due-date-${task.id}`} onChange={(event) => setEditTaskDueDate(event.target.value)} required type="date" value={editTaskDueDate} /></div>
                          <div className="field"><label htmlFor={`edit-task-repeat-${task.id}`}>Repeat</label><select id={`edit-task-repeat-${task.id}`} value={editRepeatEveryMonths} onChange={(event) => setEditRepeatEveryMonths(Number(event.target.value))}>{[...new Set([...repeatOptions, editRepeatEveryMonths])].sort((a, b) => a - b).map((months) => <option key={months} value={months}>{repeatLabel(months)}</option>)}</select></div>
                          <div className="field description-field"><label htmlFor={`edit-task-description-${task.id}`}>Description</label><textarea id={`edit-task-description-${task.id}`} maxLength={500} onChange={(event) => setEditTaskDescription(event.target.value)} required rows={3} value={editTaskDescription} /></div>
                        </div>
                        {task.isCompleted && !task.isSkipped && <div className="field"><label htmlFor={`completion-notes-${task.id}`}>Completion notes (optional)</label><textarea id={`completion-notes-${task.id}`} maxLength={1000} rows={3} placeholder="Supplies used, who did the work, or other details." value={editCompletionNotes} onChange={(event) => setEditCompletionNotes(event.target.value)} /></div>}
                        <TaskDetails prefix={`edit-details-${task.id}`} value={editTaskDetails} onChange={setEditTaskDetails} />
                        <div className="edit-task-actions">
                          <button className="cancel-button" disabled={isSavingTask} onClick={cancelTaskEdit} type="button">Cancel</button>
                          <button className="primary-button" disabled={isSavingTask} type="submit">{isSavingTask ? 'Saving…' : 'Save changes'}</button>
                        </div>
                      </form>
                    ) : (
                      <>
                        <div className="task-title-row">
                          <h3>{task.title}</h3>
                          <div className="task-actions">
                            <button className={task.isCompleted ? 'status complete' : 'status'} disabled={updatingTaskId !== null || deletingTaskId !== null} onClick={() => void handleTaskStatusChange(task)} type="button">{updatingTaskId === task.id ? 'Saving…' : task.isCompleted ? 'Reopen' : 'Mark complete'}</button>
                            <button className="edit-task-button" disabled={deletingTaskId === task.id || updatingTaskId === task.id} onClick={() => startTaskEdit(task)} type="button">Edit</button>
                            <button aria-label={`Delete ${task.title}`} className="delete-task-button" disabled={deletingTaskId === task.id || updatingTaskId === task.id} onClick={() => void handleTaskDelete(task)} type="button">{deletingTaskId === task.id ? 'Deleting…' : 'Delete'}</button>
                          </div>
                        </div>
                        {task.isCompleted && <div className="completion-details">
                          {task.isSkipped && <strong>Skipped occurrence</strong>}
                          <p>{task.completedAt ? <>Completed <time dateTime={task.completedAt}>{new Date(task.completedAt).toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}</time></> : task.isSkipped ? 'No work recorded for this occurrence' : 'Completion date not recorded'}</p>
                          {task.isSkipped ? null : task.completionNotes ? <p className="completion-notes">{task.completionNotes}</p> : <button type="button" className="edit-task-button" disabled={updatingTaskId !== null || deletingTaskId !== null} onClick={() => startTaskEdit(task)}>Add completion notes</button>}
                        </div>}
                        <p>{task.description}</p>
                        <p>About {task.estimatedMinutes} minutes</p>
                        {(task.supplies || task.instructions || task.lastPurchase || task.previousCompletionNotes) && <details className="saved-details"><summary>Supplies, instructions & previous notes</summary>{task.supplies && <p><strong>Supplies:</strong> {task.supplies}</p>}{task.instructions && <p><strong>Instructions:</strong> {task.instructions}</p>}{task.lastPurchase && <p><strong>Last purchase:</strong> {task.lastPurchase}</p>}{task.previousCompletionNotes && <p><strong>Previous occurrence:</strong> {task.previousCompletionNotes}</p>}</details>}
                        {!task.isCompleted && <div className="schedule-actions"><PostponeTask task={task} onChanged={() => setTasksVersion(v => v + 1)} />{task.repeatEveryMonths > 0 && <button className="edit-task-button" type="button" disabled={updatingTaskId !== null || deletingTaskId !== null} onClick={() => void handleTaskStatusChange(task, true)}>Skip this occurrence</button>}</div>}<time className={`due-date ${timing.state}`} dateTime={task.dueDate}>{timing.label}</time>
                        {task.repeatEveryMonths > 0 && <p>{repeatLabel(task.repeatEveryMonths)} · {task.repeatFromCompletion ? 'Repeats from the completion date.' : 'Keeps the original scheduled cadence.'}</p>}
                      </>
                    )}
                  </div>
                </article>
                )
              })}
            </div>
            {!isLoadingTasks && visibleTasks.length > 0 && (
              <nav className="task-pagination" aria-label="Task pages">
                <button className="task-filter" type="button" disabled={currentPage === 1} onClick={() => setPagination({ context: paginationContext, page: currentPage - 1 })}>Previous</button>
                <span aria-live="polite">Page {currentPage} of {pageCount}</span>
                <button className="task-filter" type="button" disabled={currentPage === pageCount} onClick={() => setPagination({ context: paginationContext, page: currentPage + 1 })}>Next</button>
              </nav>
            )}
          </> : (
            <div className="no-selection"><p className="eyebrow">Maintenance plan</p><h2>Choose a property</h2><p>Add or select a property to see its maintenance tasks.</p></div>
          )}
        </section>
      </section>
    </main>
  )
}

export default App

