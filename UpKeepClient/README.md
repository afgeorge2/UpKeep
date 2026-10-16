# UpKeep frontend

React 19 and TypeScript interface, served by Vite. Use the [project README](../README.md) for prerequisites, database setup, and the launcher that starts both applications.

## Run the frontend

From this directory (`UpKeepClient`):

```powershell
npm ci
npm run dev
```

Open [http://localhost:5173](http://localhost:5173). Start the API separately as described in the [backend README](../UpKeep/README.md), or use `dev.ps1` from the repository root.

Browser requests use relative `/api` URLs. [vite.config.ts](vite.config.ts) proxies them to `http://localhost:5085` during development. If the interface loads but properties fail, check the API and database connection. Keep port 5173 available when using the root launcher.

## Commands

Run these in `UpKeepClient`:

| Command | Purpose |
| --- | --- |
| `npm run dev` | Prepare OCR assets and start the development server |
| `npm test` | Run equipment-catalog tests with Node's test runner |
| `npm run build` | Prepare OCR assets, check TypeScript, and build into `dist` |
| `npm run lint` | Run ESLint |
| `npm run preview` | Serve an existing production build locally |

Preview is for inspecting the built frontend; it is not a production deployment. This repository configures the API proxy under Vite's development `server` settings. A deployed frontend needs an appropriate `/api` route or reverse proxy to the backend.

## Code map

| File | Responsibility |
| --- | --- |
| [src/App.tsx](src/App.tsx) | Properties, task creation and editing, completion, history, filters, and pagination |
| [src/UpcomingMaintenance.tsx](src/UpcomingMaintenance.tsx) | Overview across properties and time-budget suggestions |
| [src/taskPlanning.ts](src/taskPlanning.ts) | Shared task-detail defaults and budget selection |
| [src/TaskDetails.tsx](src/TaskDetails.tsx) | Time, supplies, instructions, purchase notes, and recurrence basis |
| [src/PostponeTask.tsx](src/PostponeTask.tsx) | Postponement controls |
| [src/GuidedPlan.tsx](src/GuidedPlan.tsx) | Editable starting schedules for common maintenance |
| [src/EquipmentLabelPlan.tsx](src/EquipmentLabelPlan.tsx) | Local OCR and equipment-label planning |
| [src/equipmentCatalog.ts](src/equipmentCatalog.ts) | Supported equipment matches and sourced task drafts |
| [src/AiEquipmentPlan.tsx](src/AiEquipmentPlan.tsx) | AI lookup, draft review, and task creation |
| [src/App.css](src/App.css) and [src/index.css](src/index.css) | Application and global styling |

## Equipment identification

The local label reader runs Tesseract in the browser and matches the supported catalog. [scripts/prepare-ocr.mjs](scripts/prepare-ocr.mjs) copies the worker, WebAssembly files, and English language data from installed packages into `public/ocr`. The dev and build commands run this preparation automatically. Those generated files are ignored by Git.

AI identification is a separate backend-assisted flow. The interface checks `/api/equipment/status`, accepts photos or model text, and requires review before adding suggested tasks. Credentials belong in the backend environment; see [AI setup](../AI-SETUP.md).

## Validation scope

[tests/equipmentCatalog.test.mjs](tests/equipmentCatalog.test.mjs) tests model matching, label extraction, and draft provenance. The current frontend test suite does not automate browser interactions.

For UI changes, run the test, build, and lint commands above, then check the affected flow in the browser. Use disposable records when testing saves, completion, undo, or deletion. For planning changes, check local-date handling, estimated-time selection, and both recurrence modes alongside the backend tests.
