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

      function normalizeReasonCode(structured) {
        if (!structured || typeof structured !== 'object') {
          return '';
        }
        return normalizeOptionalText(structured.reasonCode || structured.rootCauseCode || structured.failureCode || structured.code);
      }

      function normalizeModelUsedText(provider, model) {
        return normalizeText([provider, model].filter(Boolean).join(' / '), 'not available');
      }

      function normalizeEmbeddingsSummary(embeddingsStatus, degradedFlags) {
        const normalizedStatus = normalizeOptionalText(embeddingsStatus).toLowerCase();
        const flags = Array.isArray(degradedFlags)
          ? degradedFlags.map(item => normalizeOptionalText(item).toLowerCase()).filter(Boolean)
          : [];
        const embeddingsFlagged = flags.some(flag => flag.includes('embed'));

        if (normalizedStatus === 'disabled' || normalizedStatus === 'unavailable') {
          return {
            text: normalizedStatus,
            isWarning: false
          };
        }

        if (normalizedStatus === 'notfound') {
          return {
            text: 'disabled (model not found)',
            isWarning: false
          };
        }

        if (normalizedStatus === 'degraded' || embeddingsFlagged) {
          return {
            text: 'degraded (semantic retrieval limited)',
            isWarning: true
          };
        }

        if (normalizedStatus === 'ready' || normalizedStatus === 'ok' || normalizedStatus === 'active' || normalizedStatus === 'enabled') {
          return {
            text: 'active',
            isWarning: false
          };
        }

        if (normalizedStatus) {
          return {
            text: normalizeOptionalText(embeddingsStatus),
            isWarning: normalizedStatus !== 'not available'
          };
        }

        if (flags.length > 0) {
          return {
            text: 'degraded (semantic retrieval limited)',
            isWarning: true
          };
        }

        return {
          text: 'not available',
          isWarning: false
        };
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

      function normalizeApprovalRequiredActions(structured) {
        if (!structured || typeof structured !== 'object' || !Array.isArray(structured.approvalRequiredActions)) {
          return [];
        }

        return structured.approvalRequiredActions
          .filter(item => item && typeof item === 'object')
          .map(item => ({
            actionType: normalizeText(item.actionType, 'not available'),
            path: normalizeOptionalText(item.path),
            command: normalizeOptionalText(item.command),
            normalizedTarget: normalizeText(item.normalizedTarget, ''),
            riskLevel: normalizeText(item.riskLevel, 'not available'),
            reason: normalizeText(item.reason, 'not available'),
            approvalStatus: normalizeText(item.approvalStatus, 'not available')
          }));
      }

      function normalizeActionLifecycle(structured) {
        if (!structured || typeof structured !== 'object' || !Array.isArray(structured.actionLifecycle)) {
          return [];
        }

        return structured.actionLifecycle
          .filter(item => item && typeof item === 'object')
          .map(item => ({
            actionCorrelationId: normalizeOptionalText(item.actionCorrelationId),
            actionType: normalizeText(item.actionType, 'not available'),
            lifecycleState: normalizeText(item.lifecycleState, 'not available'),
            reasonCode: normalizeOptionalText(item.reasonCode),
            approvalStatus: normalizeOptionalText(item.approvalStatus)
          }));
      }

      function buildActionLifecycleCounts(entries) {
        const counts = {
          requested: 0,
          approvalRequired: 0,
          blocked: 0,
          executed: 0,
          failed: 0
        };
        for (const entry of entries || []) {
          const state = String(entry.lifecycleState || '').trim().toLowerCase();
          if (state === 'requested') counts.requested++;
          else if (state === 'approvalrequired') counts.approvalRequired++;
          else if (state === 'blocked') counts.blocked++;
          else if (state === 'executed') counts.executed++;
          else if (state === 'failed') counts.failed++;
        }
        return counts;
      }

      function isTimeoutFallbackReason(fallbackReason) {
        return String(fallbackReason || '').trim().toUpperCase() === 'MODEL_TIMEOUT';
      }

      function getFallbackResultText(fallbackReason) {
        if (!fallbackReason) {
          return '';
        }
        return isTimeoutFallbackReason(fallbackReason)
          ? 'Local model timed out; indexed-context fallback was used.'
          : 'Local model request failed; indexed-context fallback was used.';
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

      function buildDerivedSummary(structured, status, reasonCode, fallbackReason, fallbackMode, modelUsed, duration, buildText, embeddingsSummary, embeddingsWarning) {
        const summaryText = fallbackReason ? '' : normalizeOptionalText(structured && structured.summaryText);
        const summary = fallbackReason ? '' : normalizeOptionalText(structured && structured.summary);
        const details = [];
        details.push('Final status: ' + status);
        if (reasonCode) details.push('Reason code: ' + reasonCode);
        if (fallbackReason) details.push('Fallback reason: ' + fallbackReason);
        if (fallbackMode) details.push('Fallback mode: ' + fallbackMode);
        if (fallbackReason) details.push('Fallback result: ' + getFallbackResultText(fallbackReason));
        if (modelUsed && modelUsed !== 'not available') details.push('Model used: ' + modelUsed);
        if (duration && duration !== 'not available') details.push('Duration: ' + duration);
        details.push('Build: ' + (buildText || 'not run'));
        if (embeddingsWarning) details.push('Embeddings: ' + embeddingsSummary);
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
        const approvalRequiredActions = normalizeApprovalRequiredActions(structured);
        const actionLifecycle = normalizeActionLifecycle(structured);
        const actionLifecycleCounts = buildActionLifecycleCounts(actionLifecycle);
        const externalAttempts = Number.isFinite(structured && structured.externalAttempts)
          ? Math.max(0, Math.floor(structured.externalAttempts))
          : approvalRequiredActions.length;
        const deniedActions = Number.isFinite(structured && structured.deniedActions)
          ? Math.max(0, Math.floor(structured.deniedActions))
          : approvalRequiredActions.length;
        const blockedActions = Number.isFinite(structured && structured.blockedActions)
          ? Math.max(0, Math.floor(structured.blockedActions))
          : actionLifecycleCounts.blocked;
        const hostBoundaryPreserved = typeof (structured && structured.hostBoundaryPreserved) === 'boolean'
          ? structured.hostBoundaryPreserved
          : true;
        const changedHints = structured && Array.isArray(structured.changedHints) ? structured.changedHints : [];
        const changedRanges = structured && Array.isArray(structured.changedRanges) ? structured.changedRanges : [];
        const changedKinds = structured && Array.isArray(structured.changedKinds) ? structured.changedKinds : [];
        const task = String(currentRunTask || (taskInput && taskInput.value ? taskInput.value : '')).trim();
        const status = normalizeFinalStatusValue(structured, ok, failed);
        const provider = normalizeText(structured && (structured.provider || structured.modelProvider), 'not available');
        const model = normalizeText(structured && structured.model, 'not available');
        const modelUsed = normalizeModelUsedText(provider, model);
        const duration = normalizeDurationText(structured);
        const fallbackReason = normalizeOptionalText(structured && structured.fallbackReason);
        const fallbackMode = normalizeOptionalText(structured && structured.fallbackMode);
        const reasonCode = normalizeReasonCode(structured);
        const finalStatus = normalizeOptionalText(structured && structured.finalStatus);
        const embeddingsStatus = normalizeOptionalText(structured && (structured.embeddingsStatus || structured.EmbeddingsStatus));
        const degradedFlags = Array.isArray(structured && structured.degradedFlags) ? structured.degradedFlags.map(String) : [];
        const embeddingsSummary = normalizeEmbeddingsSummary(embeddingsStatus, degradedFlags);
        const timeline = normalizeFallbackTimelineConsistency(normalizeTraceEvents(structured), fallbackReason);
        const buildText = normalizeBuildText(structured, buildSucceeded, buildStarted);
        const derivedTimeline = timeline.length === 0
          ? buildDerivedTimeline(status, structured, fallbackReason, fallbackMode, provider, model, duration, buildText)
          : [];
        const summary = buildDerivedSummary(
          structured,
          finalStatus || status,
          reasonCode,
          fallbackReason,
          fallbackMode,
          modelUsed,
          duration,
          buildText,
          embeddingsSummary.text,
          embeddingsSummary.isWarning
        );

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
          modelUsed,
          reasonCode,
          fallbackReason,
          fallbackMode,
          finalStatus,
          embeddingsStatus,
          embeddingsSummary: embeddingsSummary.text,
          embeddingsWarning: embeddingsSummary.isWarning,
          degradedFlags,
          summary,
          messageText: fallbackReason
            ? getFallbackResultText(fallbackReason)
            : normalizeText(structured && structured.message || message.result || message.error, ''),
          buildSucceeded,
          buildStarted,
          buildText,
          changedFiles,
          changedFilesCount: changedFiles.length,
          approvalRequiredActions,
          approvalRequiredCount: approvalRequiredActions.length,
          externalAttempts,
          deniedActions,
          blockedActions,
          hostBoundaryPreserved,
          actionLifecycle,
          actionLifecycleCounts,
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

