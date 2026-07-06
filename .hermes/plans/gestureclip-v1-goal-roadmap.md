# GestureClip v1.0 Goal Roadmap

> **For Hermes:** Use the product goal in `docs/PRODUCT_GOAL.md` and the reusable prompt in `docs/SELF_OPTIMIZATION_PROMPT.md`. Execute task-by-task with tests and release closure.

**Goal:** Make GestureClip a small-user-friendly, local-first Windows office accelerator that is safe to install, easy to understand, stable in core actions, and release-ready after every visible change.

**Architecture:** Keep the WPF desktop app local-first. Prioritize ViewModel-level behavior tests and XAML source contract tests. Avoid large rewrites; improve one user journey at a time.

**Tech Stack:** .NET 8, WPF, xUnit, GitHub Releases, Windows x64 self-contained zip.

---

## Phase 1 — Stability Lockdown

### Task 1: Make settings clicks impossible to freeze

- Protect gesture/action binding selection from recursive WPF updates.
- Keep UI lists virtualized where possible.
- Tests: settings ViewModel selection tests + XAML contract tests.

### Task 2: Audit remaining UI-heavy lists

- Clipboard history list.
- Gesture binding list.
- Any list with thumbnails or custom drawing.
- Add tests or performance safeguards for async/virtualized rendering.

### Task 3: Human-readable failure states

- Permission problems.
- Hotkey conflicts.
- Clipboard access failures.
- Update download failures.

## Phase 2 — First-run Small-user Journey

### Task 4: Add first-run quick start card

- Explain `Ctrl + \``.
- Explain double-click copy vs paste button.
- Explain right-button gestures.
- Explain local data promise.
- Allow dismissing; save preference.

### Task 5: Add “I need help” diagnostics shortcut

- One button to export diagnostics package.
- Copy local data/log path.
- Plain-language result text.

## Phase 3 — Update and Trust

### Task 6: One-click update UX polish

- Separate check update from install update.
- Show current version and latest version.
- Explain local data is preserved.

### Task 7: Release notes quality gate

- Every release note must include: fixed pain, how to use, local data impact, known notes.

## Phase 4 — v1.0 Release Candidate

### Task 8: Regression checklist pass

- Run tests.
- Manual checklist from `docs/regression-checklist.md`.
- Publish RC release.

## Always-on Validation

```bash
git diff --check
dotnet test ./GestureClip.sln
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/publish-win-x64.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/check-release.ps1
```
