# Startup and CSV follow-up review

Reviewed on 2026-11-16 against private main commit
`fa36ea00fa899211046c4f9690e5b6476f50a6cd`.
This document records a single-agent static review and synthetic validation;
it is not an independent human security certification or hardware validation.

## Findings and changes

| Finding | Resulting behavior |
|---|---|
| Autostart created a HighestAvailable task targeting the current EXE, including portable/user-writable folders. Repair could silently repoint it. | Removed task creation and startup repair entirely. Explicitly enabled unpackaged startup writes only a quoted per-user Run command with `--startup`. |
| Persisted PreferElevated could trigger elevation on later launches, including startup. | No automatic elevation from saved preferences. Full sensors require an explicit request in onboarding or the widget each session. The legacy preference is preserved when settings are read/saved for compatibility, but is not acted on. |
| Startup could enter onboarding or process setup flags. | `--startup` skips onboarding and suppresses driver/setup/autostart chaining flags. Startup is opt-in and unchecked in the welcome screen. |
| Existing privileged tasks cannot safely be redirected during upgrade. | Read-only legacy task probe, prominent warning, and refusal to enable a new Run entry if a task exists or its state is unknown. No scheduled task is rewritten or removed. |
| Daily CSV files could contain multiple incompatible headers and inconsistent row widths. | Versioned daily files with a full SHA-256 schema suffix. One file has one header/schema, including across restarts. Columns are sorted by key; numeric rows with missing/nonfinite values have empty cells. |
| Display order or same-index replacement could change the meaning of columns. | Schema includes stable row key, section, label, unit and reported device header context. Reordering groups preserves their context; changed reported device metadata selects a separate file. |
| Metadata could break CSV syntax or start a spreadsheet formula. | Quoted/escaped headers, normalized embedded CR/LF, key-first names and formula-prefix neutralization. Numeric data uses invariant culture. |
| A malformed/partial existing CSV or a legacy log could be damaged by append/cleanup. | Verify the bounded expected header bytes and terminal newline before append. Refuse malformed/partial targets without overwrite. Retention matches only recognized v2 log filenames; legacy/unrelated files are preserved. |
| Alert counters survived a completely disappearing sensor or disabled alerts. | Reset absent-sensor consecutive counts and clear disabled alert state. Recovery requires three new hot samples. |

Windows documentation explains that a task's run level determines whether its
actions run elevated. From that, the risk of running replaceable executable or
DLL content through a persistent elevated task is an inference of this review.
See [Task security contexts](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks).
The new Run command also enforces the documented 260-character limit; see
[Run/RunOnce keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys).

## Validation

- Windows x64, CLR4, .NET Framework 4.8 target; .NET SDK 10.0.404.
- Release build of application and test project: **0 errors, 0 warnings**.
- **59 tests passed, 0 failed**; original sensor/EC/deadline tests retained.
- Startup tests use only `IStartupStore` fakes. No real registry, COM scheduler,
  startup settings, elevation or installer operations are invoked by tests.
- CSV tests write synthetic fixtures only under `tests/bin/.../synthetic-tests`
  in the workspace. They cover schema changes, ordering, missing values,
  device context, culture, quoting, duplicate keys, real append/restart/day
  rollover behavior, preserved legacy files and malformed/partial targets.
- Static review traced all startup-setting callers, removed task/elevation
  preference paths, checked CSV column/value ordering and append guards, and
  inspected the final diff. `git diff --check` passed with CRLF awareness.
- Local logs: `artifacts/safety-build.log`, `artifacts/safety-tests.log`.
  Logs, generated binaries, fixtures and SDK/package state are not committed.
- Offline restore/build used the previously audited portable release DLLs and
  the installed .NET Framework targeting pack. Online NuGet restore remains
  unverified in this restricted environment. No workloads or drivers installed.

## Boundaries and remaining limitations

No production app was launched. No user settings, startup entries, scheduled
tasks or existing user files were modified during this work. No hardware,
PawnIO, elevation, live scheduler/registry behavior, UI layout, MSIX startup,
sleep/resume, resource/energy use or installer/deployment behavior was tested.
Only the synthetic runner was executed.

An existing elevated task remains a risk until the user reviews/removes it in
Task Scheduler. This patch cannot revoke a pre-existing privileged launcher
without changing system state; it deliberately does not perform that migration.
The warning is conservative: any task named TempWidget, or an unknown task
query result, requires manual review. No task ownership or ACL is inferred.
Normal Windows/UAC policy and explicitly launching the app elevated remain
outside the startup policy implemented here.

The schema describes reported row/device metadata; identical devices with
identical reported metadata cannot be uniquely distinguished across replacement
or backend identifier reuse. A changed label/context intentionally creates a
new schema file. A corrupt/partial v2 target is left untouched and samples for
that target are skipped until the file is recovered separately. The logger
remains best-effort, not a transactional or durable telemetry database.

The earlier hardware and packaging limitations in `VALIDATION.md`, and the
dependency/installer distribution limitations in `DEPENDENCIES.md`, still apply.
This change is prepared for a draft PR only; no merge or deployment is authorized.
