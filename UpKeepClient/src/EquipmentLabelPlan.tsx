import { apiFetch as fetch } from './api'
import { useEffect, useRef, useState } from 'react'
import { equipmentManual, equipmentTasks, modelFromLabel, normalizeModel, supportedModel } from './equipmentCatalog'
import type { EquipmentTask } from './equipmentCatalog'

export default function EquipmentPlan({ propertyId, onAdded }: { propertyId: number; onAdded: () => void }) {
  const [model, setModel] = useState('')
  const [drafts, setDrafts] = useState<EquipmentTask[]>([])
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const active = useRef(true)
  const pending = useRef(false)
  useEffect(() => { active.current = true; return () => { active.current = false } }, [])

  async function readPhoto(file?: File) {
    if (!file || pending.current) return
    setDrafts([])
    setModel('')
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 15 * 1024 * 1024) {
      setMessage('Choose a JPG, PNG, or WebP photo under 15 MB, or enter the model below.')
      return
    }
    pending.current = true
    setBusy(true)
    setMessage('Reading the label on your device…')
    let worker: Awaited<ReturnType<typeof import('tesseract.js').createWorker>> | undefined
    try {
      const { createWorker } = await import('tesseract.js')
      const base = new URL(`${import.meta.env.BASE_URL}ocr/`, window.location.origin).href
      worker = await createWorker('eng', 1, { workerPath: base + 'worker.min.js', corePath: base, langPath: base, cacheMethod: 'none', logger: event => {
        if (active.current && event.status === 'recognizing text') setMessage(`Reading label… ${Math.round(event.progress * 100)}%`)
      } })
      if (!active.current) return
      const bitmap = await createImageBitmap(file)
      const canvas = document.createElement('canvas')
      const scale = Math.min(3, 2400 / Math.max(bitmap.width, bitmap.height))
      canvas.width = Math.round(bitmap.width * scale)
      canvas.height = Math.round(bitmap.height * scale)
      const context = canvas.getContext('2d')
      if (!context) { bitmap.close(); throw new Error('Image preparation failed') }
      context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
      bitmap.close()
      const result = await worker.recognize(canvas)
      if (!active.current) return
      const candidate = modelFromLabel(result.data.text)
      setModel(candidate)
      setMessage(candidate ? 'Check the model against your label, then confirm below.' : 'No supported model was found. Enter the model from the label below.')
    } catch {
      if (active.current) setMessage('Could not read this photo. Try a clearer image or enter the model below.')
    } finally {
      await worker?.terminate()
      pending.current = false
      if (active.current) setBusy(false)
    }
  }

  function confirmModel() {
    setDrafts([])
    if (!supportedModel(model)) {
      setMessage('This model does not have a verified plan yet. You can still use the general maintenance plan below or add a task manually.')
      return
    }
    const date = new Date()
    const today = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
    setModel(normalizeModel(model))
    setDrafts(equipmentTasks(model, today))
    setMessage('Review the tasks and dates. Time estimates are UpKeep estimates. Filter size and replacement parts need your confirmation.')
  }

  function edit(id: string, patch: Partial<EquipmentTask>) {
    setDrafts(current => current.map(task => task.id === id ? { ...task, ...patch } : task))
  }

  async function save() {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setMessage('')
    let added = 0
    let skipped = 0
    try {
      const url = `/api/properties/${propertyId}/maintenance-tasks`
      const response = await fetch(url)
      if (!response.ok) throw new Error('Could not check existing tasks. Please try again.')
      const existing = await response.json() as { title: string; isCompleted: boolean }[]
      for (const task of drafts.filter(task => task.selected)) {
        if (existing.some(item => !item.isCompleted && (item.title === task.title || (task.id === 'service' && item.title === task.title.replace('Arrange HVAC service', 'Arrange annual service'))))) skipped++
        else {
          const saved = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(task) })
          if (!saved.ok) throw new Error('Could not save all tasks. Tasks already added are kept; retry to add the remaining tasks.')
          existing.push({ title: task.title, isCompleted: false })
          added++
        }
        if (active.current) edit(task.id, { selected: false })
      }
      if (active.current) setMessage(`${added} tasks added.${skipped ? ` ${skipped} matching open tasks already exist.` : ''}`)
    } catch (error) {
      if (active.current) setMessage(error instanceof Error ? error.message : 'Could not save the plan.')
    } finally {
      pending.current = false
      if (active.current) { setBusy(false); onAdded() }
    }
  }

  return <details className="guided-plan equipment-plan">
    <summary>Add a plan from an equipment label</summary>
    <p>Upload a label photo or enter a model. Photo text is read on your device; the photo and serial number are not saved.</p>
    <fieldset disabled={busy}>
      <div className="form-grid">
        <div className="field"><label htmlFor="equipment-photo">Equipment label photo</label><input id="equipment-photo" type="file" accept="image/jpeg,image/png,image/webp" onChange={event => { void readPhoto(event.target.files?.[0]); event.target.value = '' }} /></div>
        <div className="field"><label htmlFor="equipment-model">Model number</label><input id="equipment-model" maxLength={60} value={model} placeholder="e.g. CVPVA2414XMCEAAA" onChange={event => { setModel(event.target.value); setDrafts([]); setMessage('') }} /></div>
      </div>
      <p>First supported equipment: CVPVA2414XMC evaporator coil, including the CVPVA2414XMCEAAA label. The manual covers the base model; the suffix does not identify installed accessories.</p>
      <button className="primary-button" type="button" disabled={!model.trim()} onClick={confirmModel}>Confirm model & review tasks</button>
      {drafts.length > 0 && <>
        <p><a href={equipmentManual} target="_blank" rel="noreferrer">Manufacturer’s manual — maintenance on page 7</a></p>
        <p>Set a first due date and repeat schedule for each task separately. Completing a task creates its next occurrence automatically.</p>
        <p>The coil label does not tell us the installed filter size. Add it to supplies if known, or leave it blank for now.</p>
        {drafts.map(task => <div className="plan-option" key={task.id}>
          <label><input type="checkbox" checked={task.selected} onChange={event => edit(task.id, { selected: event.target.checked })} />{task.title}</label>
          {task.selected && <div className="form-grid">
            <div className="field"><label htmlFor={task.id + '-date'}>First due date</label><input id={task.id + '-date'} type="date" value={task.dueDate} onChange={event => edit(task.id, { dueDate: event.target.value })} /></div>
            <div className="field"><label htmlFor={task.id + '-repeat'}>{task.id === 'filter' ? 'Filter repeat schedule' : 'Service repeat schedule'}</label><select id={task.id + '-repeat'} value={task.repeatEveryMonths} onChange={event => edit(task.id, { repeatEveryMonths: Number(event.target.value) })}><option value={1}>Monthly</option><option value={3}>Every 3 months</option><option value={6}>Every 6 months (twice a year)</option><option value={12}>Yearly</option></select></div>
            <div className="field"><label htmlFor={task.id + '-minutes'}>Estimated minutes (editable)</label><input id={task.id + '-minutes'} type="number" min={1} max={1440} value={task.estimatedMinutes} onChange={event => edit(task.id, { estimatedMinutes: Number(event.target.value) })} /></div>
            <div className="field description-field"><label htmlFor={task.id + '-supplies'}>Supplies & specifications</label><textarea id={task.id + '-supplies'} maxLength={1000} value={task.supplies} placeholder="Filter size and type, if known" onChange={event => edit(task.id, { supplies: event.target.value })} /></div>
            <div className="field description-field"><label htmlFor={task.id + '-instructions'}>Instructions</label><textarea id={task.id + '-instructions'} maxLength={3000} value={task.instructions} onChange={event => edit(task.id, { instructions: event.target.value })} /></div>
          </div>}
        </div>)}
        <button className="primary-button" type="button" disabled={!drafts.some(task => task.selected) || drafts.some(task => task.selected && (!task.dueDate || !Number.isInteger(task.repeatEveryMonths) || task.repeatEveryMonths < 1 || task.repeatEveryMonths > 120 || !Number.isInteger(task.estimatedMinutes) || task.estimatedMinutes < 1 || task.estimatedMinutes > 1440))} onClick={() => void save()}>Add selected tasks to my plan</button>
      </>}
    </fieldset>
    {message && <p role="status">{message}</p>}
  </details>
}

