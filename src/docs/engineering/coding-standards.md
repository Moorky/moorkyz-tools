# Coding Standards

These standards govern implementation details only; they do not create product scope or supersede approved decisions.

## Correctness and maintainability

- Prefer clear, explicit, small solutions over cleverness, speculative optimization, or abstractions that only forward calls.
- Fit the existing code, toolchain, public contracts, and naming conventions. Do not weaken compiler or lint settings to make a change pass.
- Make failures explicit. Validate untrusted values at boundaries, preserve useful error context, and clean up resources on failures and cancellation.
- Preserve compatibility unless a change is approved. Public API changes must follow contract versioning and decision authority.
- Add a dependency only for a concrete need after checking stability, maintenance, compatibility, license, security, and bundle/runtime impact.

## TypeScript and package boundaries

- Keep strict type checking enabled. Avoid `any`, unchecked casts, `@ts-ignore`, and non-null assertions as shortcuts.
- Use explicit types for public API, configuration, events, and data crossing trust boundaries; infer straightforward local types.
- Model mutually exclusive states with unions and handle them exhaustively.
- Keep standalone tool engines independent of the portal framework and avoid browser imports of filesystem/server code.
- Keep browser imports SSR-safe: module evaluation must not require browser globals or start work.
- Do not bundle local-service or filesystem dependencies into browser workspaces.

## Runtime behavior

- Keep state deterministic and host authority intact. Do not add hidden global listeners, polling, timers, observers, storage, or network activity.
- Optional functionality must not acquire feature-owned work or resources until explicitly enabled; release them on disable/disposal.
- Prefer platform APIs and existing helpers. Add shared abstractions only after their contract and callers are clear.
- Avoid broad catches, silent defaults, success-shaped fallbacks, and ignored errors.
- Use comments for non-obvious constraints and rationale, not to narrate obvious code.

## UI and accessibility

- Use semantic controls, accessible names and states, visible focus, predictable keyboard behavior, and stable interaction targets.
- Respect responsive layouts, right-to-left and zoom requirements where specified, and `prefers-reduced-motion`.
- Do not communicate state by color alone. Verify meaningful error, empty, loading, and disabled states for interactive work.
- Follow the current root contract and the relevant tool guide; historical SDK specifications are not active requirements.

## Documentation

Update contracts, examples, and implementation docs when behavior changes. Label proposals as proposals and distinguish implemented commands from planned commands. Avoid copying framework-specific conventions from other projects unless this project has approved them.
