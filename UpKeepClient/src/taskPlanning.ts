export interface TaskDetailsValue {
  estimatedMinutes: number
  supplies: string
  instructions: string
  lastPurchase: string
  repeatFromCompletion: boolean
}
export const defaultTaskDetails: TaskDetailsValue = { estimatedMinutes: 30, supplies: '', instructions: '', lastPurchase: '', repeatFromCompletion: false }

export function planWithinBudget<T extends { estimatedMinutes: number }>(tasks: T[], budget: number) {
  return tasks.reduce<{ tasks: T[]; remainingMinutes: number }>((plan, task) => task.estimatedMinutes <= plan.remainingMinutes
    ? { tasks: [...plan.tasks, task], remainingMinutes: plan.remainingMinutes - task.estimatedMinutes }
    : plan, { tasks: [], remainingMinutes: budget })
}
