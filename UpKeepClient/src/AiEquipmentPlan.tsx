import { apiFetch as fetch } from './api'
import { useEffect, useRef, useState } from 'react'

interface Suggestion { title: string; description: string; repeatEveryMonths: number; estimatedMinutes: number; supplies: string; instructions: string; sourceUrl: string }
interface Analysis { model: string; equipmentType: string; identificationNote: string; filterSpecification: string; filterEvidence: string; followUp: string; tasks: Suggestion[]; sources: { title: string; url: string }[] }
interface Draft extends Suggestion { selected: boolean; dueDate: string; supplies: string }
const today = () => { const date = new Date(); return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}` }

async function photoData(file?: File): Promise<string> {
  if (!file) return ''
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 8 * 1024 * 1024) throw new Error('Choose a JPG, PNG, or WebP photo under 8 MB.')
  // Re-encode pixels to reduce transfer size and omit camera metadata.
  const bitmap = await createImageBitmap(file)
  try {
    const canvas = document.createElement('canvas')
    const scale = Math.min(1, 2000 / Math.max(bitmap.width, bitmap.height))
    canvas.width = Math.round(bitmap.width * scale); canvas.height = Math.round(bitmap.height * scale)
    const context = canvas.getContext('2d')
    if (!context) throw new Error('Could not prepare photo.')
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
    return canvas.toDataURL('image/jpeg', 0.9)
  } finally { bitmap.close() }
}

export default function AiEquipmentPlan({ propertyId, onAdded }: { propertyId: number; onAdded: () => void }) {
  const [available, setAvailable] = useState<boolean | null>(null)
  const [model, setModel] = useState('')
  const [photo, setPhoto] = useState<File>()
  const [filterPhoto, setFilterPhoto] = useState<File>()
  const [analysis, setAnalysis] = useState<Analysis | null>(null)
  const [drafts, setDrafts] = useState<Draft[]>([])
  const [confirmed, setConfirmed] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const operation = useRef<AbortController | null>(null)
  useEffect(() => {
    const controller = new AbortController()
    void fetch('/api/equipment/status', { signal: controller.signal }).then(async response => {
      if (!response.ok) throw new Error()
      setAvailable((await response.json()).available === true)
    }).catch(() => { if (!controller.signal.aborted) setAvailable(false) })
    return () => { controller.abort(); operation.current?.abort() }
  }, [])

  function reset() { setAnalysis(null); setDrafts([]); setFilterPhoto(undefined); setConfirmed(false); setMessage('') }
  async function analyze() {
    if (operation.current) return
    const controller = new AbortController(); operation.current = controller
    setBusy(true); setMessage('Identifying equipment and looking up documentation…'); setConfirmed(false); setAnalysis(null); setDrafts([])
    try {
      const response = await fetch('/api/equipment/analyze', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, signal: controller.signal,
        body: JSON.stringify({ model, photo: await photoData(photo), filterPhoto: await photoData(filterPhoto) }),
      })
      if (!response.ok) {
        const error = await response.json().catch(() => null)
        throw new Error(error?.message ?? (response.status === 429 ? 'Too many lookups. Please wait a minute and try again.' : 'Could not analyze the equipment. Please try again.'))
      }
      const result = await response.json() as Analysis
      setAnalysis(result)
      setDrafts(result.tasks.map(task => ({ ...task, selected: true, dueDate: today() })))
      setMessage('Review the identification, dates, and instructions before adding tasks. Times are estimates.')
    } catch (error) { if (!controller.signal.aborted) setMessage(error instanceof Error ? error.message : 'Lookup failed.') }
    finally { operation.current = null; if (!controller.signal.aborted) setBusy(false) }
  }

  async function save() {
    if (operation.current || !confirmed || !analysis) return
    const controller = new AbortController(); operation.current = controller; setBusy(true); setMessage('')
    let added = 0
    try {
      const url = `/api/properties/${propertyId}/maintenance-tasks`
      const response = await fetch(url, { signal: controller.signal })
      if (!response.ok) throw new Error('Could not check existing tasks.')
      const existing = await response.json() as { title: string; isCompleted: boolean }[]
      for (const [index, task] of drafts.entries()) {
        if (!task.selected) continue
        if (!existing.some(item => !item.isCompleted && item.title === task.title)) {
          const instructions = `${task.instructions}\nEquipment: ${analysis.model || analysis.equipmentType}\n${task.sourceUrl ? `Source: ${task.sourceUrl}` : 'General guidance; no model-specific manual source confirmed.'}`
          if (instructions.length > 3000) throw new Error('Please shorten the instructions before saving.')
          const saved = await fetch(url, { method: 'POST', signal: controller.signal, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...task, instructions }) })
          if (!saved.ok) throw new Error('Could not save all tasks. Tasks already added are kept. Retry for the remaining tasks.')
          added++; existing.push({ title: task.title, isCompleted: false })
        }
        setDrafts(current => current.map((draft, n) => n === index ? { ...draft, selected: false } : draft))
      }
      setMessage(`${added} tasks added. Matching open tasks were kept.`)
    } catch (error) { if (!controller.signal.aborted) setMessage(error instanceof Error ? error.message : 'Could not save tasks.') }
    finally { operation.current = null; if (!controller.signal.aborted) { setBusy(false); onAdded() } }
  }
  const edit = (index: number, patch: Partial<Draft>) => setDrafts(current => current.map((task, n) => n === index ? { ...task, ...patch } : task))
  return <details className="guided-plan equipment-plan">
    <summary>Identify equipment with AI</summary>
    <p>Take a photo of the unit or its model label, or enter a model number. We’ll look for maintenance guidance and filter information.</p>
    <p>Photos and model text are sent to OpenAI when you choose Analyze. UpKeep does not save the photos. Include only the equipment in the photo.</p>
    {available === null && <p role="status">Checking AI connection…</p>}
    {available === false && <p role="status">AI lookup is not connected yet. You can use the local equipment-label reader below.</p>}
    <fieldset disabled={busy || available !== true}>
      <div className="form-grid">
        <div className="field"><label htmlFor="ai-photo">Unit or model-label photo</label><input id="ai-photo" type="file" accept="image/jpeg,image/png,image/webp" onChange={event => { setPhoto(event.target.files?.[0]); reset() }} /></div>
        <div className="field"><label htmlFor="ai-model">Model number (optional with photo)</label><input id="ai-model" maxLength={100} value={model} onChange={event => { setModel(event.target.value); reset() }} /></div>
      </div>
      <button className="primary-button" type="button" disabled={!photo && !model.trim()} onClick={() => void analyze()}>Analyze equipment</button>
      {analysis && <>
        <h3>{analysis.equipmentType || 'Equipment review'}{analysis.model && ` · ${analysis.model}`}</h3>
        <p>{analysis.identificationNote}</p>
        <p><strong>Filter information:</strong> {analysis.filterSpecification || 'Not established from the available information.'}</p>
        {analysis.filterEvidence && <p>{analysis.filterEvidence}</p>}
        {analysis.followUp && <p>{analysis.followUp} You can add the maintenance tasks now and check filter details later.</p>}
        <div className="field"><label htmlFor="ai-filter-photo">Optional filter housing or return-grille photo</label><input id="ai-filter-photo" type="file" accept="image/jpeg,image/png,image/webp" onChange={event => setFilterPhoto(event.target.files?.[0])} /><p>Photograph visible markings first; you do not need to remove the filter to start.</p></div>
        {filterPhoto && <button className="undo-button" type="button" onClick={() => void analyze()}>Analyze with additional photo (replaces drafts)</button>}
        {analysis.sources.length > 0 && <ul>{analysis.sources.map(source => <li key={source.url}><a href={source.url} target="_blank" rel="noreferrer">{source.title || source.url}</a></li>)}</ul>}
        <div className="plan-option"><label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} />I reviewed the equipment identification and these task suggestions.</label></div>
        {drafts.map((task, index) => <div className="plan-option" key={index}>
          <label><input type="checkbox" checked={task.selected} onChange={event => edit(index, { selected: event.target.checked })} />{task.title}</label>
          {task.selected && <div className="form-grid">
            <div className="field"><label htmlFor={`ai-date-${index}`}>First due date</label><input id={`ai-date-${index}`} type="date" value={task.dueDate} onChange={event => edit(index, { dueDate: event.target.value })} /></div>
            <div className="field"><label htmlFor={`ai-repeat-${index}`}>Repeat every (months; 0 = once)</label><input id={`ai-repeat-${index}`} type="number" min={0} max={120} value={task.repeatEveryMonths} onChange={event => edit(index, { repeatEveryMonths: Number(event.target.value) })} /></div>
            <div className="field"><label htmlFor={`ai-time-${index}`}>Estimated minutes</label><input id={`ai-time-${index}`} type="number" min={1} max={1440} value={task.estimatedMinutes} onChange={event => edit(index, { estimatedMinutes: Number(event.target.value) })} /></div>
            <div className="field description-field"><label htmlFor={`ai-supplies-${index}`}>Supplies (confirm compatibility before buying)</label><textarea id={`ai-supplies-${index}`} maxLength={1000} value={task.supplies} onChange={event => edit(index, { supplies: event.target.value })} /></div>
            <div className="field description-field"><label htmlFor={`ai-instructions-${index}`}>Instructions</label><textarea id={`ai-instructions-${index}`} maxLength={2000} value={task.instructions} onChange={event => edit(index, { instructions: event.target.value })} /></div>
            <p>{task.sourceUrl ? <a href={task.sourceUrl} target="_blank" rel="noreferrer">Supporting source</a> : 'General guidance — no model-specific source confirmed.'}</p>
          </div>}
        </div>)}
        <button className="primary-button" type="button" disabled={!confirmed || !drafts.some(task => task.selected) || drafts.some(task => task.selected && (!task.dueDate || !Number.isInteger(task.repeatEveryMonths) || task.repeatEveryMonths < 0 || task.repeatEveryMonths > 120 || !Number.isInteger(task.estimatedMinutes) || task.estimatedMinutes < 1 || task.estimatedMinutes > 1440))} onClick={() => void save()}>Add reviewed tasks</button>
      </>}
    </fieldset>
    {message && <p role="status">{message}</p>}
  </details>
}

