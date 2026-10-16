import { apiFetch as fetch } from './api'
import { useEffect, useRef, useState } from 'react'
import { planWithinBudget } from './taskPlanning'

interface Task {
  estimatedMinutes: number
  supplies: string
  instructions: string
  lastPurchase: string
  id: number
  title: string
  description: string
  dueDate: string
  propertyId: number
  isCompleted: boolean
  repeatEveryMonths: number
  statusMessage?: string | null
}

function localDate() {
  const date = new Date()
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function TaskGroup({ title, tasks, pageSize, busy, onComplete, onSelectProperty, properties }: {
  title: string
  tasks: Task[]
  pageSize: number
  busy: boolean
  onComplete: (task: Task) => void
  onSelectProperty: (id: number) => void
  properties: { id: number; name: string }[]
}) {
  const [page, setPage] = useState(1)
  const today = localDate()
  const pages = Math.max(1, Math.ceil(tasks.length / pageSize))
  const currentPage = Math.min(page, pages)
  if (page !== currentPage) setPage(currentPage)
  return (
    <section className="overview-group" aria-label={title}>
      <div className="section-heading"><h3>{title}</h3><span className="task-count">{tasks.length}</span></div>
      {tasks.length === 0 ? <p className="muted">No tasks in this group.</p> : (
        <div className="tasks">
          {tasks.slice((currentPage - 1) * pageSize, currentPage * pageSize).map((task) => (
            <article className={`task-card ${task.dueDate < today ? 'overdue' : task.dueDate === today ? 'today' : ''}`} key={task.id}>
              <div className="task-content">
                <button type="button" className="overview-property" onClick={() => onSelectProperty(task.propertyId)}>{properties.find((property) => property.id === task.propertyId)?.name ?? 'View property'}</button>
                <div className="task-title-row"><h4>{task.title}</h4><button type="button" className="status" disabled={busy} onClick={() => onComplete(task)}>Mark complete</button></div>
                <p>{task.description}</p><p>About {task.estimatedMinutes} minutes</p>
                {(task.supplies || task.instructions || task.lastPurchase) && <details className="saved-details"><summary>What you need</summary>{task.supplies && <p><strong>Supplies:</strong> {task.supplies}</p>}{task.instructions && <p>{task.instructions}</p>}{task.lastPurchase && <p><strong>Last purchase:</strong> {task.lastPurchase}</p>}</details>}
                <time dateTime={task.dueDate}>Due {new Date(`${task.dueDate}T00:00:00`).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}</time>
              </div>
            </article>
          ))}
        </div>
      )}
      {pages > 1 && <nav className="task-pagination" aria-label={`${title} pages`}>
        <button className="task-filter" type="button" disabled={currentPage === 1} onClick={() => setPage(currentPage - 1)}>Previous</button>
        <span>Page {currentPage} of {pages}</span>
        <button className="task-filter" type="button" disabled={currentPage === pages} onClick={() => setPage(currentPage + 1)}>Next</button>
      </nav>}
    </section>
  )
}

export default function UpcomingMaintenance({ properties, onSelectProperty, onAddTask, onAddProperty, loadingProperties }: {
  properties: { id: number; name: string }[]
  onSelectProperty: (id: number) => void
  onAddTask: (id: number) => void
  onAddProperty: () => void
  loadingProperties: boolean
}) {
  const [taskPropertyId, setTaskPropertyId] = useState<number | null>(null)
  const taskProperty = properties.find((property) => property.id === taskPropertyId) ?? properties[0]
  const [timeBudget, setTimeBudget] = useState(0)
  const [today, setToday] = useState(localDate)
  const [tasks, setTasks] = useState<Task[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  const [pageSize, setPageSize] = useState(10)
  const [notice, setNotice] = useState<{ task: Task; message: string } | null>(null)

  useEffect(() => {
    const timer = window.setInterval(() => setToday(localDate()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const response = await fetch(`/api/maintenance-tasks/upcoming?today=${today}`, { signal: controller.signal })
        if (!response.ok) throw new Error('Could not load upcoming maintenance. Please try again.')
        setTasks(await response.json() as Task[])
      } catch (cause) {
        if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Could not load maintenance.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [today, reload])

  async function changeStatus(task: Task) {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setError(null)
    try {
      const response = await fetch(`/api/properties/${task.propertyId}/maintenance-tasks/${task.id}/status`, {
        method: 'PATCH', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isCompleted: !task.isCompleted, localDate: localDate() }),
      })
      if (!response.ok) throw new Error('Could not update the task. Please try again.')
      const updatedTask = await response.json() as Task
      setNotice({ task: updatedTask, message: updatedTask.statusMessage ?? `“${updatedTask.title}” ${updatedTask.isCompleted ? 'completed' : 'reopened'}.` })
      setTasks((current) => current.filter((item) => item.id !== task.id))
      setReload((current) => current + 1)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not update the task.')
    } finally {
      pending.current = false
      setBusy(false)
    }
  }

  const { tasks: suggestedTasks, remainingMinutes } = planWithinBudget(tasks, timeBudget)
  const groups = timeBudget > 0 ? [{ title: 'Fits your available time', tasks: suggestedTasks }] : [
    { title: 'Overdue', tasks: tasks.filter((task) => task.dueDate < today) },
    { title: 'Due today', tasks: tasks.filter((task) => task.dueDate === today) },
    { title: 'Next 7 days', tasks: tasks.filter((task) => task.dueDate > today) },
  ]

  return (
    <>
      <div className="section-heading overview-heading">
        <div><p className="eyebrow">Across all properties</p><h2>Upcoming maintenance</h2><p className="muted">Overdue work, today’s tasks, and the next seven days.</p></div>
        <div className="overview-add-task">
          {properties.length > 1 && <div className="field"><label htmlFor="overview-task-property">Property</label><select id="overview-task-property" value={taskProperty?.id ?? ''} onChange={(event) => setTaskPropertyId(Number(event.target.value))}>{properties.map((property) => <option key={property.id} value={property.id}>{property.name}</option>)}</select></div>}
          <button className="primary-button" type="button" disabled={loadingProperties} onClick={() => taskProperty ? onAddTask(taskProperty.id) : onAddProperty()}>{loadingProperties || taskProperty ? 'Add task' : 'Add a property to start'}</button>
        </div>
      </div>
      {notice && <div className="completion-notice">
        <span role="status">{notice.message}</span>
        {notice.task.isCompleted && <button className="undo-button" type="button" disabled={busy || loading} onClick={() => void changeStatus(notice.task)}>Undo</button>}
        <button className="cancel-button" type="button" aria-label="Dismiss task notification" onClick={() => setNotice(null)}>Dismiss</button>
      </div>}
      {error && <div className="error panel-error" role="alert">{error} <button className="undo-button" type="button" onClick={() => setReload((current) => current + 1)}>Refresh</button></div>}
      <div className="weekly-plan"><h3>Your weekly check-in</h3><label htmlFor="time-budget">How much time do you have?</label><select id="time-budget" value={timeBudget} onChange={e => setTimeBudget(Number(e.target.value))}><option value={0}>Show all upcoming work</option>{[15, 30, 60, 120].map(minutes => <option key={minutes} value={minutes}>{minutes} minutes</option>)}</select><p>Suggestions use estimated time, with earlier due dates considered first. Longer tasks remain in the full overview.</p>{timeBudget > 0 && !loading && !error && <p role="status">{suggestedTasks.length} {suggestedTasks.length === 1 ? 'task' : 'tasks'} · about {timeBudget - remainingMinutes} minutes{suggestedTasks.length === 0 ? '. No tasks fit; choose more time or show all work.' : ''}</p>}</div>
      <div className="task-list-controls"><label className="page-size" htmlFor="overview-page-size">Tasks per group
        <select id="overview-page-size" value={pageSize} onChange={(event) => setPageSize(Number(event.target.value))}>{[5, 10, 20].map((size) => <option key={size} value={size}>{size}</option>)}</select>
      </label></div>
      {loading ? <p className="empty-state" role="status">Loading upcoming maintenance…</p> : error ? null : <>
        {tasks.length === 0 && <p className="empty-copy">You’re all caught up through the next seven days. Select a property to plan more maintenance.</p>}
        {groups.map((group) => <TaskGroup key={`${group.title}:${pageSize}`} {...group} properties={properties} onSelectProperty={onSelectProperty} pageSize={pageSize} busy={busy} onComplete={(task) => void changeStatus(task)} />)}
      </>}
    </>
  )
}

