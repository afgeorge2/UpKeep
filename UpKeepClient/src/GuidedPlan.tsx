import { apiFetch as fetch } from './api'
import { useState } from 'react'
const templates = [
  { equipment: 'HVAC / air filters', title: 'Check HVAC filter', description: 'Check the filter condition and replacement guidance for your system.', repeatEveryMonths: 1, estimatedMinutes: 15, supplies: 'Record the filter size and model here.', instructions: 'Follow your system manual. Adjust the interval for your filter type and household.' },
  { equipment: 'Gutters', title: 'Inspect gutters from the ground', description: 'Look for visible blockage, leaks, and disconnected downspouts.', repeatEveryMonths: 6, estimatedMinutes: 20, supplies: 'Phone or binoculars for a ground-level inspection.', instructions: 'Inspect from the ground. Arrange professional access if elevated work is needed.' },
  { equipment: 'Sump pump', title: 'Review sump pump maintenance', description: 'Review the manufacturer’s inspection and testing schedule.', repeatEveryMonths: 3, estimatedMinutes: 20, supplies: 'Pump model and owner’s manual.', instructions: 'Use the manufacturer’s instructions for your specific pump and backup system.' },
  { equipment: 'Deck / patio', title: 'Inspect deck or patio', description: 'Look for loose parts, surface damage, and drainage problems.', repeatEveryMonths: 6, estimatedMinutes: 30, supplies: 'Phone for inspection photos.', instructions: 'Record visible issues and arrange repairs as needed.' }
]
const today = () => { const d = new Date(); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0') }
export default function GuidedPlan({ propertyId, onAdded }: { propertyId: number; onAdded: () => void }) {
  const [items, setItems] = useState(templates.map(t => ({ ...t, selected: false, dueDate: today() })))
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  async function addPlan() {
    setBusy(true); setMessage('')
    let added = 0
    try {
      const url = '/api/properties/' + propertyId + '/maintenance-tasks'
      const existingResponse = await fetch(url)
      if (!existingResponse.ok) throw new Error('Could not check existing tasks. Try again.')
      const existing = await existingResponse.json() as { title: string; isCompleted: boolean }[]
      for (const item of items.filter(i => i.selected)) {
        if (!existing.some(task => !task.isCompleted && task.title === item.title)) {
          const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(item) })
          if (!response.ok) throw new Error('Could not add ' + item.title + '. Review your dates and try again. Tasks already added are kept.')
          added++
        }
        setItems(current => current.map(i => i.title === item.title ? { ...i, selected: false } : i))
      }
      setMessage(added + ' tasks added. Existing open tasks with matching names were kept.')
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Could not save plan.') }
    finally { setBusy(false); onAdded() }
  }
  return <details className="guided-plan"><summary>Build a maintenance plan</summary><p>Choose what this property has. Review the first dates and intervals before adding. These are starting points; use your equipment manuals and local conditions to adjust them.</p>
    {items.map((item, index) => <div className="plan-option" key={item.title}>
      <label><input type="checkbox" disabled={busy} checked={item.selected} onChange={e => setItems(current => current.map((i, n) => n === index ? { ...i, selected: e.target.checked } : i))} />{item.equipment}</label>
      {item.selected && <div className="form-grid"><div className="field"><label htmlFor={'plan-date-' + index}>First due date</label><input id={'plan-date-' + index} type="date" required value={item.dueDate} disabled={busy} onChange={e => setItems(current => current.map((i, n) => n === index ? { ...i, dueDate: e.target.value } : i))} /></div><div className="field"><label htmlFor={'plan-repeat-' + index}>Repeat every (months)</label><input id={'plan-repeat-' + index} type="number" min={1} max={120} value={item.repeatEveryMonths} disabled={busy} onChange={e => setItems(current => current.map((i, n) => n === index ? { ...i, repeatEveryMonths: Number(e.target.value) } : i))} /></div><p>{item.description} About {item.estimatedMinutes} minutes.</p></div>}
    </div>)}
    <button className="primary-button" type="button" disabled={busy || !items.some(i => i.selected) || items.some(i => i.selected && (!i.dueDate || i.repeatEveryMonths < 1 || i.repeatEveryMonths > 120))} onClick={() => void addPlan()}>{busy ? 'Adding…' : 'Add selected tasks'}</button>
    {message && <p role="status">{message}</p>}
  </details>
}

