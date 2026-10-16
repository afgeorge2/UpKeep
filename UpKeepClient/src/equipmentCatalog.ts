export const equipmentManual = 'https://resource.carrierenterprise.com/is/content/Watscocom/non-branded_cpvpa-cspva_en_ii'
export const normalizeModel = (value: string) => value.toUpperCase().replace(/[\s-]/g, '')
export const supportedModel = (value: string) => /^(CVPVA2414XMC|CVPVA2414XMCEAAA)$/.test(normalizeModel(value))

export function modelFromLabel(text: string) {
  return text.toUpperCase().match(/\bCVPVA\s*2414\s*XMC(?:EAAA)?\b/)?.[0].replace(/\s/g, '') ?? ''
}

export function equipmentTasks(model: string, dueDate: string) {
  if (!supportedModel(model)) return []
  const equipment = normalizeModel(model)
  const source = `Equipment: ${equipment}. Source: ${equipmentManual} (Care and Maintenance, page 7).`
  return [
    { id: 'filter', selected: true, title: `Filter care — ${equipment}`, description: 'Monthly filter care for the HVAC system serving this evaporator coil.', dueDate, repeatEveryMonths: 1, estimatedMinutes: 15, supplies: '', instructions: `Clean or replace the system filter monthly, following the installed filter and system instructions. Confirm the filter size and type before buying supplies. ${source}` },
    { id: 'service', selected: true, title: `Arrange HVAC service — ${equipment}`, description: 'Arrange equipment maintenance with a qualified HVAC provider.', dueDate, repeatEveryMonths: 6, estimatedMinutes: 10, supplies: 'Equipment model and service provider contact details.', instructions: `Plan service every six months, or adjust with your HVAC provider. This is an editable planning preference; the manual specifies maintenance at least annually. The time estimate covers arranging the appointment, not the service visit. ${source}` },
  ]
}

export type EquipmentTask = ReturnType<typeof equipmentTasks>[number]
