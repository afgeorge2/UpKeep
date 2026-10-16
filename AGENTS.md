# Working on UpKeep

## Context

- For setup, launch failures, or a project overview, read [README.md](README.md).
- For API, persistence, or scheduling changes, read [UpKeep/README.md](UpKeep/README.md).
- For browser behavior, frontend tooling, or OCR changes, read [UpKeepClient/README.md](UpKeepClient/README.md).
- For AI equipment identification or provider configuration, read [AI-SETUP.md](AI-SETUP.md).

## Project rules

- Keep task access scoped to the owning property. Update API contracts and frontend consumers together when changing payloads.
- Preserve recurrence anchors, month-end dates, completion-based scheduling, and undo behavior. Repeated completion requests must not generate duplicate successors; undo must retain successors that were edited or completed.
- Treat due dates as local calendar dates and completion timestamps as UTC instants. Preserve the caller's local date when calculating completion-based recurrence.
- Preserve saved data when changing the schema. Generate and review EF migrations and the snapshot; use disposable data for destructive verification.
- Keep provider credentials in backend configuration. Preserve the distinction between local OCR and AI requests, including draft review and source provenance. Unknown equipment specifications must remain unknown rather than being guessed.
- Keep generated OCR assets in the existing preparation workflow; edit the source or preparation script instead of generated copies.

## Validation and completion

- For backend changes, run `dotnet test` from the repository root. For frontend changes, run `npm test`, `npm run build`, and `npm run lint` in `UpKeepClient`.
- Cover scheduling fixes with API regression tests using explicit dates. Check database schema changes against a disposable SQL Server database because the API test host uses EF's in-memory provider.
- Verify affected UI flows in the browser using disposable records. Distinguish simulated AI tests from a real provider check, and report checks that could not run.
- For documentation-only changes, check commands, paths, links, and consistency with the code; application tests are unnecessary unless behavior also changed.
- Update the relevant README when changing setup or behavior. Keep setup details there and link to them from this file.
