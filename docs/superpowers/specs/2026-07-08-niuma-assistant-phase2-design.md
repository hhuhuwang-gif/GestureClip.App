# Niuma Assistant phase 2 design draft

Date: 2026-07-08
Status: proposed draft, not approved for implementation
Scope: plan only; no assistant feature implementation in this row

## Goal

Turn stable GestureClip into a fast local-first assistant for daily work. Keep first release small: quick actions, clipboard text transforms, gesture-triggered workflows, reusable presets, and configurable input-action-output chains.

## Current code seams

- Gesture actions already route through `GestureBuiltInActionExecutor` and `BuiltInGestureAction`.
- Clipboard capture/search/paste already lives in `ClipboardService` and clipboard overlay view models.
- Gesture bindings already have provider seams through `GesturePresetProvider` and settings keys.
- Settings UI is large and risky; phase 2 should add small settings groups, not redesign the full window.
- Diagnostics must stay privacy-safe: no raw clipboard text/images/files in reports or exported packages.

## Approaches considered

### A. Local-first action pipeline (recommended)

Add a small typed action pipeline: input provider, action executor, output target. Start with deterministic local actions such as text cleanup, casing, JSON formatting, URL open/search, copy/paste, and app/window commands. Later AI actions plug into the same interface behind explicit opt-in.

Trade-off: needs a little architecture first, but avoids hard-coding one-off assistant actions into gesture code.

### B. Overlay-first assistant panel

Build a richer command center first, then wire gestures and clipboard into it.

Trade-off: visible demo faster, but risks becoming a big chat-like app before core automation contracts are stable.

### C. Gesture-only macro expansion

Extend gestures to run command lists without building a general quick action center.

Trade-off: fastest for power users, but poor discoverability and weak clipboard-action UX.

Recommendation: A, with a thin quick action center UI as the first consumer.

## Proposed architecture

### Core contracts

- `AssistantActionDefinition`: id, display name, category, required input kind, output kind, privacy level.
- `AssistantActionRequest`: input source, selected text or clipboard item id, gesture context, user options.
- `AssistantActionResult`: status, preview text, output command, optional replacement text.
- `IAssistantActionExecutor`: executes one action with cancellation.
- `IAssistantWorkflowExecutor`: runs a sequence of actions with stop-on-error and preview support.

### Input sources

- Current clipboard item metadata plus explicit full-content load only when user invokes an action.
- Selected text via copy shortcut with capture suppression.
- Gesture context: pattern, action id, mouse position, foreground app metadata.
- Manual quick action center search/selection.

### Output targets

- Copy result to clipboard.
- Paste result into foreground app.
- Show preview in quick action center.
- Open URL/app/command only from allowlisted preset actions.

## Feature slices

1. Quick action center: searchable local palette for actions and presets.
2. Clipboard text actions: trim, normalize whitespace, case convert, JSON format/minify, URL encode/decode, quote/unquote.
3. Gesture workflow chains: bind a gesture to one or more action ids with typed input/output validation.
4. Preset commands: safe built-in presets such as search selected text, paste latest, format clipboard JSON, open settings, export diagnostics.
5. Settings model: store action definitions and gesture workflow bindings as versioned JSON under existing settings infrastructure.
6. Privacy boundaries: local-only by default; any AI/network action requires explicit per-action opt-in, preview, and diagnostics redaction.
7. Verification: tests for action contracts, workflow execution order, clipboard suppression, settings persistence, diagnostics privacy, and a shell regression pack.

## Privacy rules

- Never write raw clipboard text/image/file content to diagnostics exports or normal logs.
- Logs may include action id, input kind, byte length, success/failure, elapsed time, and redacted error class.
- AI/network actions must be disabled by default and show what content will leave the machine before first use.
- Clipboard full content is loaded just-in-time and discarded after action completion unless user chooses copy/paste output.

## Verification strategy

- Unit tests for action request validation and output target behavior.
- Clipboard tests for suppression during selected-text capture and generated paste.
- Gesture tests for workflow binding dispatch without breaking existing built-in gestures.
- Settings tests for JSON migration, save/reload, and invalid config fallback.
- Diagnostics tests proving action logs/export do not include raw clipboard content.
- Regression script extending `scripts/test-gesture-optimization.ps1` or adding `scripts/test-niuma-assistant.ps1`.

## Non-goals for phase 2 first pass

- No always-on chat window.
- No background upload of clipboard history.
- No arbitrary shell command editor for beginners.
- No cloud AI integration until local deterministic actions and privacy UX pass review.
- No broad SettingsWindow redesign beyond small grouped controls needed for action/workflow settings.

## Approval gate

This draft is ready for user review. Implementation should not start until the user approves this scope or requests changes.
