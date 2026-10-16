import { copyFile, mkdir, readdir } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const root = fileURLToPath(new URL('../', import.meta.url))
const output = path.join(root, 'public/ocr')
await mkdir(output, { recursive: true })
await copyFile(path.join(root, 'node_modules/tesseract.js/dist/worker.min.js'), path.join(output, 'worker.min.js'))
const core = path.join(root, 'node_modules/tesseract.js-core')
for (const name of await readdir(core)) {
  if (name.endsWith('.wasm.js') || name.endsWith('.wasm')) await copyFile(path.join(core, name), path.join(output, name))
}
await copyFile(path.join(root, 'node_modules/@tesseract.js-data/eng/4.0.0/eng.traineddata.gz'), path.join(output, 'eng.traineddata.gz'))
