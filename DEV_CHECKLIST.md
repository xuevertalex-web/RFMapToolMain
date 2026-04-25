# Pre-change smoke gate

Command:
- `./SmokeGate.cmd`

Must pass before changes.
Must pass after changes.

Baseline tests expected:
- `AnalysisFallback_ModelTimeout_IndexedContextSummary_StructuredObservability`
- `AnalysisFallback_LlmRequestFailed_IndexedContextSummary_StructuredObservability`
- `Analysis_NormalModelResponse_NoFallbackTimeline`

If smoke gate is red before edits: do not start a new change, first record baseline failure.
If smoke gate turns red after edits: revert or fix current change.

Scope guard:
- do not add production tools for external environment workaround
- environment tool failures like `rg Access denied` are not production bugs unless existing production code uses that tool
- do not add new tools without a separate explicit task
