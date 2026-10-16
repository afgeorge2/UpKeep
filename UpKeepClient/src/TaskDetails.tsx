import type { TaskDetailsValue } from './taskPlanning'
export default function TaskDetails({ value, onChange, prefix }: { value: TaskDetailsValue; onChange: (value: TaskDetailsValue) => void; prefix: string }) {
  return <details className="task-details-form"><summary>Time, supplies, instructions & scheduling</summary><div className="form-grid">
    <div className="field"><label htmlFor={prefix + '-minutes'}>Estimated minutes</label><input id={prefix + '-minutes'} type="number" min={1} max={1440} required value={value.estimatedMinutes} onChange={e => onChange({ ...value, estimatedMinutes: Number(e.target.value) })} /></div>
    <div className="field"><label htmlFor={prefix + '-basis'}>When a repeating task is completed</label><select id={prefix + '-basis'} value={String(value.repeatFromCompletion)} onChange={e => onChange({ ...value, repeatFromCompletion: e.target.value === 'true' })}><option value="false">Keep the scheduled cadence</option><option value="true">Count from completion date</option></select></div>
    <div className="field description-field"><label htmlFor={prefix + '-supplies'}>Supplies & specifications</label><textarea id={prefix + '-supplies'} maxLength={1000} value={value.supplies} placeholder="Filter size, part number, tools…" onChange={e => onChange({ ...value, supplies: e.target.value })} /></div>
    <div className="field description-field"><label htmlFor={prefix + '-instructions'}>Instructions</label><textarea id={prefix + '-instructions'} maxLength={3000} value={value.instructions} onChange={e => onChange({ ...value, instructions: e.target.value })} /></div>
    <div className="field description-field"><label htmlFor={prefix + '-purchase'}>Last purchase</label><input id={prefix + '-purchase'} maxLength={500} value={value.lastPurchase} placeholder="Date, store, item and price" onChange={e => onChange({ ...value, lastPurchase: e.target.value })} /></div>
  </div></details>
}
