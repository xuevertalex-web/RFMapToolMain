# Security Invariants

This document lists security invariants that are treated as stable guarantees in the current codebase.
Each invariant is tied to an accepted stable checkpoint tag.

## Stable checkpoints

- `approval-lifecycle-v2-stable`
- `unified-command-policy-stable`
- `capability-tier-binding-stable`
- `approval-ledger-integrity-v1-stable`
- `single-runtime-ledger-owner-stable`
- `approval-ledger-durability-stable`
- `approval-proposal-identity-stable`
- `approval-diagnostics-sanitized-stable`

## Invariants guaranteed by checkpoint

### `approval-lifecycle-v2-stable`

- Approval token format is `APPROVED:<proposalId>`.
- `APPROVED:true` is denied.
- Approval state is fail-closed when unhealthy.
- Consumed/expired replay remains denied across restart.
- Read/analysis paths remain available where policy allows.

### `unified-command-policy-stable`

- Command policy authority is canonical and centralized.
- High-risk commands require explicit approval.
- Unsupported shell/meta syntax is denied and non-approvable.
- Hard-blocked/invalid command classes do not start execution.
- Runner and guard diagnostics use aligned reason-code semantics.

### `capability-tier-binding-stable`

- Capability taxonomy and deterministic classifier are present.
- Capability metadata is surfaced in permission and execution results.
- Approval proposals/ledger carry capability fingerprint v1 metadata.
- Approval-gated execution enforces exact capability fingerprint match.
- Post-cutover missing fingerprint fails closed.

### `approval-ledger-integrity-v1-stable`

- Ledger events use deterministic SHA-256 integrity chaining.
- Anchor file tracks trusted chain tail hash.
- Startup verifies chain + anchor before replay.
- Integrity mismatch causes unhealthy ledger and fail-closed approval gating.
- Compaction verifies source and compacted output before replacement.

### `single-runtime-ledger-owner-stable`

- A single runtime owner lock is required per runtime root.
- Concurrent runtime contention for the same runtime root fails closed for approval-gated flows.
- Shared multi-writer mode is not supported.

### `approval-ledger-durability-stable`

- Ledger append uses explicit stream append + durable flush.
- Append ordering is preserved: verify -> durable append -> anchor update.
- Anchor update uses temp + flush + replace/move path.
- Crash windows remain fail-closed (availability loss possible, not fail-open).

### `approval-proposal-identity-stable`

- Proposal identity uses deterministic versioned canonical hashing (`proposal-v1:<sha256-lowerhex>`).
- RunCommand identity includes executable + args when metadata is available.
- Issue and validation paths use the same canonical identity logic.
- Narrow legacy pre-cutover proposalId compatibility is preserved.
- Post-cutover non-canonical proposal IDs are denied.

### `approval-diagnostics-sanitized-stable`

- Public `APPROVAL_STATE_UNAVAILABLE` messages expose only sanitized deterministic codes.
- Expected codes:
  - `owner_lock_unavailable`
  - `load_failed`
  - `compact_failed`
  - `issue_append_failed`
  - `consume_append_failed`
  - `expired_append_failed`
  - `denied_append_failed`
  - `init_failed`
  - `unknown_state_unavailable`
- Raw OS exception tails, absolute paths, commands, tokens, secrets, and model output are not exposed via these public approval-state messages.

## Out of scope / non-goals

- No distributed or multi-process shared-writer consensus model.
- No cryptographic signing/HMAC/key-anchored trust root for ledger contents.
- No protection against local admin/kernel compromise.
- No hidden recovery or fail-open fallback for approval authority.
