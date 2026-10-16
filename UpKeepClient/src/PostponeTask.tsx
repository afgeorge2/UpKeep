import { apiFetch as fetch } from './api'
import { useState } from 'react'
export default function PostponeTask({ task, onChanged }: { task: { id: number; propertyId: number; dueDate: string }; onChanged: () => void }) {
  const [date, setDate] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  async function save() {
    setBusy(true); setMessage('')
    try {
      const response = await fetch('/api/properties/' + task.propertyId + '/maintenance-tasks/' + task.id + '/postpone', { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ dueDate: date }) })
      if (!response.ok) throw new Error('Could not postpone. Choose a later date and refresh if the task changed.')
      setDate(''); setMessage('Task postponed.'); onChanged()
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Could not postpone.') }
    finally { setBusy(false) }
  }
  return <details><summary>Postpone</summary><label htmlFor={'postpone-' + task.id}>New due date</label><input id={'postpone-' + task.id} type="date" value={date} min={task.dueDate} onChange={e => setDate(e.target.value)} /><button className="undo-button" type="button" disabled={busy || !date || date <= task.dueDate} onClick={() => void save()}>Save new date</button>{message && <p role="status">{message}</p>}</details>
}

