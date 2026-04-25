const webviewClientRunNormalization = `function normalizeText(value, fallback) {
        const text = String(value === undefined || value === null ? '' : value).trim();
        return text || fallback || 'not available';
      }

      function normalizeOptionalText(value) {
        return String(value === undefined || value === null ? '' : value).trim();
      }

      function normalizeDurationText(structured) {
        if (!structured || typeof structured !== 'object') {
          return 'not available';
        }
        const rawDuration = structured.durationMs;
        if (Number.isFinite(rawDuration) && rawDuration >= 0) {
          return String(Math.floor(rawDuration)) + ' ms';
        }
        return normalizeText(structured.duration, 'not available');
      }

      function normalizeFinalStatusValue(structured, ok, failed) {
        const finalStatus = normalizeOptionalText(structured && structured.finalStatus).toLowerCase();
        const fallbackReason = normalizeOptionalText(structured && structured.fallbackReason);
        const fallbackMode = normalizeOptionalText(structured && structured.fallbackMode);

        if (failed) {
          return 'error';
        }
        if (finalStatus === 'fallback-success') {
          return 'fallback-success';
        }
        if (finalStatus === 'success') {
          return 'success';
        }
        if (finalStatus === 'error' || finalStatus === 'failed' || finalStatus === 'failure') {
          return 'error';
        }
        if (ok && (fallbackReason || fallbackMode)) {
          return 'fallback-success';
        }
        if (ok) {
          return 'success';
        }
        return 'running';
      }

      function normalizeBuildText(structured, buildSucceeded, buildStarted) {
        const buildText = normalizeOptionalText(structured && structured.buildText);
        if (buildText) {
          return buildText;
        }
        return normalizeBooleanBuild(buildSucceeded, buildStarted);
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

      function isTimeoutFallbackReason(fallbackReason) {
        return String(fallbackReason || '').trim().toUpperCase() === 'MODEL_TIMEOUT';
      }

      function getFallbackResultText(fallbackReason) {
        if (!fallbackReason) {
          return '';
        }
        return isTimeoutFallbackReason(fallbackReason)
          ? 'Локальная модель не завершила запрос вовремя; использован fallback по индексированному контексту.'
          : 'Запрос к локальной модели завершился ошибкой; использован fallback по индексированному контексту.';
      }

      function normalizeFallbackTimelineConsistency(timeline, fallbackReason) {
        if (!Array.isArray(timeline) || !fallbackReason) {
          return Array.isArray(timeline) ? timeline : [];
        }

        const timeoutExpected = isTimeoutFallbackReason(fallbackReason);
        const hasTimeout = timeline.some(event => String(event && event.stage || '').trim() === 'ModelCallTimedOut');
        const withoutTimeout = timeline.filter(event => String(event && event.stage || '').trim() !== 'ModelCallTimedOut');
        if (!timeoutExpected) {
          return withoutTimeout;
        }

        if (hasTimeout) {
          return timeline;
        }

        const timeoutEvent = {
          stage: 'ModelCallTimedOut',
          status: 'timed_out',
          message: 'Model call timed out',
          timestamp: ''
        };

        const insertBeforeIndex = withoutTimeout.findIndex(event => {
          const stage = String(event && event.stage || '').trim();
          return stage === 'AnalysisFallbackStarted' || stage === 'RunCompleted';
        });

        if (insertBeforeIndex < 0) {
          return withoutTimeout.concat(timeoutEvent);
        }

        return withoutTimeout.slice(0, insertBeforeIndex).concat(timeoutEvent, withoutTimeout.slice(insertBeforeIndex));
      }

      function buildDerivedTimeline(runStatus, structured, fallbackReason, fallbackMode, provider, model, duration, buildText) {
        if (!structured || typeof structured !== 'object' || runStatus === 'running' || runStatus === 'error') {
          return [];
        }

        const events = [];
        events.push({
          stage: 'status',
          status: runStatus,
          message: 'Derived from structured metadata',
          timestamp: '',
          isDerived: true
        });

        if (fallbackReason || fallbackMode) {
          events.push({
            stage: 'fallback',
            status: fallbackMode || 'active',
            message: [fallbackReason, fallbackMode].filter(Boolean).join(' | '),
            timestamp: '',
            isDerived: true
          });
        }

        if (provider || model || duration) {
          events.push({
            stage: 'model',
            status: provider || 'not available',
            message: [model, duration].filter(Boolean).join(' | '),
            timestamp: '',
            isDerived: true
          });
        }

        events.push({
          stage: 'build',
          status: buildText || 'not run',
          message: 'Build stage state',
          timestamp: '',
          isDerived: true
        });

        return events;
      }

      function buildDerivedSummary(structured, status, fallbackReason, fallbackMode, provider, model, duration, buildText) {
        const summaryText = fallbackReason ? '' : normalizeOptionalText(structured && structured.summaryText);
        const summary = fallbackReason ? '' : normalizeOptionalText(structured && structured.summary);
        const details = [];
        details.push('Final status: ' + status);
        if (fallbackReason) details.push('Fallback reason: ' + fallbackReason);
        if (fallbackMode) details.push('Fallback mode: ' + fallbackMode);
        if (fallbackReason) details.push('Fallback result: ' + getFallbackResultText(fallbackReason));
        if (provider || model) details.push('Model: ' + [provider, model].filter(Boolean).join(' / '));
        if (duration && duration !== 'not available') details.push('Duration: ' + duration);
        details.push('Build: ' + (buildText || 'not run'));
        return [summaryText, summary, details.join('; ')].filter(Boolean).join('\\n');
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
        const status = normalizeFinalStatusValue(structured, ok, failed);
        const provider = normalizeText(structured && (structured.provider || structured.modelProvider), 'not available');
        const model = normalizeText(structured && structured.model, 'not available');
        const duration = normalizeDurationText(structured);
        const fallbackReason = normalizeOptionalText(structured && structured.fallbackReason);
        const fallbackMode = normalizeOptionalText(structured && structured.fallbackMode);
        const finalStatus = normalizeOptionalText(structured && structured.finalStatus);
        const embeddingsStatus = normalizeOptionalText(structured && (structured.embeddingsStatus || structured.EmbeddingsStatus));
        const timeline = normalizeFallbackTimelineConsistency(normalizeTraceEvents(structured), fallbackReason);
        const buildText = normalizeBuildText(structured, buildSucceeded, buildStarted);
        const derivedTimeline = timeline.length === 0
          ? buildDerivedTimeline(status, structured, fallbackReason, fallbackMode, provider, model, duration, buildText)
          : [];
        const summary = buildDerivedSummary(structured, finalStatus || status, fallbackReason, fallbackMode, provider, model, duration, buildText);

        return {
          ok,
          failed,
          cancelled: String(message && message.error || '').toLowerCase().includes('stopped by user'),
          status,
          task,
          taskPreview: task ? (task.length > 140 ? task.slice(0, 137) + '...' : task) : 'not available',
          workspace: normalizeText(structured && (structured.workspace || structured.workspaceRoot), 'not available'),
          duration,
          provider,
          model,
          fallbackReason,
          fallbackMode,
          finalStatus,
          embeddingsStatus,
          degradedFlags: Array.isArray(structured && structured.degradedFlags) ? structured.degradedFlags.map(String) : [],
          summary,
          messageText: fallbackReason
            ? getFallbackResultText(fallbackReason)
            : normalizeText(structured && structured.message || message.result || message.error, ''),
          buildSucceeded,
          buildStarted,
          buildText,
          changedFiles,
          changedHints,
          changedRanges,
          changedKinds,
          timeline: timeline.length ? timeline : derivedTimeline,
          timelineDerived: timeline.length === 0 && derivedTimeline.length > 0,
          diagnostics: normalizeDiagnostics(structured),
          failure: failed ? normalizeFailureSummary(message || {}, structured) : null
        };
      }`;

function getWebviewClientRunNormalization() {
  return webviewClientRunNormalization;
}

module.exports = { getWebviewClientRunNormalization };
