import assert from 'node:assert/strict'
import test from 'node:test'
import { equipmentTasks, modelFromLabel, supportedModel } from '../src/equipmentCatalog.ts'

test('matches only the verified base model and known label variant', () => {
  assert.equal(supportedModel(' cvpva2414xmc-eaaa '), true)
  assert.equal(supportedModel('CVPVA2414XMC'), true)
  for (const model of ['CVPVA2414XMCOTHER', 'CVPVA2417XMC', 'CVPVAZATAXMCEARA', 'unknown']) {
    assert.equal(supportedModel(model), false)
    assert.deepEqual(equipmentTasks(model, '2026-09-10'), [])
  }
})

test('extracts model without serial data and does not guess OCR corrections', () => {
  assert.equal(modelFromLabel('MODEL NO. CVPVA2414XMCEAAA\nSERIAL NO. 12345'), 'CVPVA2414XMCEAAA')
  assert.equal(modelFromLabel('CVPVA 2414 XMC'), 'CVPVA2414XMC')
  assert.equal(modelFromLabel('CVPVAZATAXMCEARA'), '')
  assert.equal(modelFromLabel('SERIAL NO. 12345'), '')
})

test('drafts retain provenance while leaving unknown filter supplies empty', () => {
  const tasks = equipmentTasks('cvpva2414xmceaaa', '2026-09-10')
  assert.deepEqual(tasks.map(task => task.repeatEveryMonths), [1, 6])
  assert.equal(tasks[0].supplies, '')
  for (const task of tasks) {
    assert.equal(task.dueDate, '2026-09-10')
    assert.match(task.instructions, /CVPVA2414XMCEAAA/)
    assert.match(task.instructions, /https:\/\/resource.carrierenterprise.com/)
    assert.ok(task.title.length <= 100)
  }
})
