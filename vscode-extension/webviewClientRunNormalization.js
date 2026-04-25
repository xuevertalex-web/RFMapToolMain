const webviewClientRunNormalization = `function normalizeText(value, fallback) {
        const text = String(value === undefined || value === null ? '' : value).trim();
        return text || fallback || 'not available';
      }

      function normalizeBooleanBuild(value, started) {
        if (started === false) return 'not started';
        if (value === true) return 'succeeded';
        if (value === false) return 'failed';
        return 'not run';
      }

      function normalizeChangedFilesForRun(structured) {
        if (!structured || typeof structured !== 'object' || !Array.isArray(structured.changedFiles)) {
          return [];
        }
        return structured.changedFiles.map(normalizeChangedFileEntry).filter(file => String(file.path || '').trim());
      }

      function normalizeTraceEvents(structured) {
        if (!structured || typeof structured !== 'object') {
          return [];
        }

        const rawEvents = Array.isArray(structured.timeline)
          ? structured.timeline
          : Array.isArray(structured.traceEvents)
            ? structured.traceEvents
            : Array.isArray(structured.events)
              ? structured.events
              : [];

        return rawEvents
          .filter(event => event && typeof event === 'object')
          .map(event => ({
            stage: normalizeText(event.stage || event.name || event.type, 'not available'),
            status: normalizeText(event.status || event.outcome, 'not available'),
            message: normalizeText(event.message || event.text || event.detail, ''),
            timestamp: normalizeText(event.timestamp || event.time, '')
          }));
      }

      function normalizeDiagnostics(structured) {
        if (!structured || typeof structured !== 'object') {
          return [];
        }

        const rawDiagnostics = Array.isArray(structured.diagnostics)
          ? structured.diagnostics
          : Array.isArray(structured.buildDiagnostics)
            ? structured.buildDiagnostics
            : [];

        return rawDiagnostics
          .filter(item => item && typeof item === 'object')
          .map(item => ({
            severity: normalizeText(item.severity || item.level, 'not available'),
            code: normalizeText(item.code, ''),
            message: normalizeText(item.message || item.text, 'not available'),
            file: normalizeText(item.file || item.path || item.filePath, ''),
            line: Number.isFinite(item.line) ? Math.max(1, Math.floor(item.line)) : null,
            column: Number.isFinite(item.column) ? Math.max(1, Math.floor(item.column)) : null
          }));
      }

      function normalizeFailureSummary(message, structured) {
        const source = structured && typeof structured === 'object' ? structured : {};
        return {
          rootCauseCode: normalizeText(source.rootCauseCode || source.failureCode || source.reasonCode || message.error, 'not available'),
          failedStage: normalizeText(source.failedStage || source.stage, 'not available'),
          lastSuccessfulStep: normalizeText(source.lastSuccessfulStep || source.lastSuccessfulStage, 'not available'),
          failedStep: normalizeText(source.failedStep || source.firstFailedStep, 'not available'),
          reasonCode: normalizeText(source.reasonCode || source.code, 'not available'),
          explanation: normalizeText(source.explanation || source.message || message.result || message.error, 'not available'),
          pipelineStoppedReason: normalizeText(source.pipelineStoppedReason || source.stopReason || source.whyStopped, 'not available'),
          downstreamNotStarted: normalizeText(source.downstreamNotStarted || source.skippedDownstream, 'not available'),
          loopStage: normalizeText(source.loopStage, 'not available'),
          iterations: Number.isFinite(source.iterationsUsed) && Number.isFinite(source.maxIterations)
            ? String(source.iterationsUsed) + ' / ' + String(source.maxIterations)
            : 'not available',
          lastKnownAction: normalizeText(source.lastKnownAction, 'not available'),
          modelCallStarted: typeof source.modelCallStarted === 'boolean' ? String(source.modelCallStarted) : 'not available',
          patchStarted: typeof source.patchStarted === 'boolean' ? String(source.patchStarted) : 'not available',
          buildStarted: typeof source.buildStarted === 'boolean' ? String(source.buildStarted) : 'not available'
        };
      }

      function normalizeRunResult(message) {
        const structured = message && message.structuredResult && typeof message.structuredResult === 'object'
          ? message.structuredResult
          : null;
        const ok = message && message.ok === true;
        const failed = message && message.ok === false;
        const buildSucceeded = structured ? structured.buildSucceeded : null;
        const buildStarted = structured && typeof structured.buildStarted === 'boolean' ? structured.buildStarted : null;
        const changedFiles = normalizeChangedFilesForRun(structured);
        const changedHints = structured && Array.isArray(structured.changedHints) ? structured.changedHints : [];
        const changedRanges = structured && Array.isArray(structured.changedRanges) ? structured.changedRanges : [];
        const changedKinds = structured && Array.isArray(structured.changedKinds) ? structured.changedKinds : [];
        const task = String(currentRunTask || (taskInput && taskInput.value ? taskInput.value : '')).trim();

        return {
          ok,
          failed,
          cancelled: String(message && message.error || '').toLowerCase().includes('stopped by user'),
          status: failed ? 'failed' : ok ? 'success' : 'running',
          task,
          taskPreview: task ? (task.length > 140 ? task.slice(0, 137) + '...' : task) : 'not available',
          workspace: normalizeText(structured && (structured.workspace || structured.workspaceRoot), 'not available'),
          duration: normalizeText(structured && (structured.duration || structured.durationMs), 'not available'),
          provider: normalizeText(structured && (structured.provider || structured.modelProvider), 'not available'),
          model: normalizeText(structured && structured.model, 'not available'),
          fallbackReason: normalizeText(structured && structured.fallbackReason, ''),
          fallbackMode: normalizeText(structured && structured.fallbackMode, ''),
          finalStatus: normalizeText(structured && structured.finalStatus, ''),
          degradedFlags: Array.isArray(structured && structured.degradedFlags) ? structured.degradedFlags.map(String) : [],
          summary: normalizeText(structured && structured.summary, ''),
          messageText: normalizeText(structured && structured.message || message.result || message.error, ''),
          buildSucceeded,
          buildStarted,
          buildText: normalizeBooleanBuild(buildSucceeded, buildStarted),
          changedFiles,
          changedHints,
          changedRanges,
          changedKinds,
          timeline: normalizeTraceEvents(structured),
          diagnostics: normalizeDiagnostics(structured),
          failure: failed ? normalizeFailureSummary(message || {}, structured) : null
        };
      }`;

function getWebviewClientRunNormalization() {
  return webviewClientRunNormalization;
}

module.exports = { getWebviewClientRunNormalization };
