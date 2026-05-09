const webviewClientStatusRenderer = `function renderRunStatus(run) {
        if (!runStatusGrid) return;
        runStatusGrid.replaceChildren();

        const fallbackActive = run.status === 'fallback-success' || !!run.fallbackReason || !!run.fallbackMode;
        const modelUsed = normalizeStatusCell(run.modelUsed, '') || normalizeStatusCell([run.provider, run.model].filter(Boolean).join(' / '), 'not available');
        const finalStatus = normalizeStatusCell(run.finalStatus, '');
        const normalizedFinalStatus = finalStatus ? finalStatus.toLowerCase() : '';
        const changedFilesCount = Number.isFinite(run.changedFilesCount)
          ? run.changedFilesCount
          : Array.isArray(run.changedFiles)
            ? run.changedFiles.length
            : 0;
        const embeddingsSummary = normalizeStatusCell(run.embeddingsSummary, '') || normalizeStatusCell(run.embeddingsStatus, 'not available');

        const rows = [
          ['status', run.status || 'not available'],
          ['reason code', normalizeStatusCell(run.reasonCode, 'not available')],
          ['continuation hint', normalizeStatusCell(run.continuationHint, 'not available')],
          ['next actions', Array.isArray(run.nextActionCandidates) && run.nextActionCandidates.length ? run.nextActionCandidates.join(' | ') : 'not available']
        ];

        if (finalStatus && normalizedFinalStatus !== String(run.status || '').toLowerCase()) {
          rows.push(['final status', finalStatus]);
        }

        if (fallbackActive) {
          rows.push(['fallback reason', normalizeStatusCell(run.fallbackReason, 'not available')]);
          rows.push(['fallback mode', normalizeStatusCell(run.fallbackMode, 'not available')]);
        }

        rows.push(['build', normalizeStatusCell(run.buildText, 'not run')]);
        rows.push(['changed files', String(Math.max(0, changedFilesCount))]);
        rows.push(['approval required', String(Math.max(0, Number.isFinite(run.approvalRequiredCount) ? run.approvalRequiredCount : 0))]);
        rows.push(['external attempts', String(Math.max(0, Number.isFinite(run.externalAttempts) ? run.externalAttempts : 0))]);
        rows.push(['outside boundary attempts', String(Math.max(0, Number.isFinite(run.outsideBoundaryAttempts) ? run.outsideBoundaryAttempts : 0))]);
        rows.push(['high-risk approval required', String(Math.max(0, Number.isFinite(run.highRiskApprovalRequiredActions) ? run.highRiskApprovalRequiredActions : 0))]);
        rows.push(['denied actions', String(Math.max(0, Number.isFinite(run.deniedActions) ? run.deniedActions : 0))]);
        rows.push(['blocked actions', String(Math.max(0, Number.isFinite(run.blockedActions) ? run.blockedActions : 0))]);
        rows.push(['requested actions', String(Math.max(0, Number.isFinite(run.requestedActions) ? run.requestedActions : 0))]);
        rows.push(['executed actions', String(Math.max(0, Number.isFinite(run.executedActions) ? run.executedActions : 0))]);
        rows.push(['failed actions', String(Math.max(0, Number.isFinite(run.failedActions) ? run.failedActions : 0))]);
        rows.push(['plan required', String(run.planRequired === true)]);
        rows.push(['host boundary preserved', String(run.hostBoundaryPreserved !== false)]);
        rows.push(['lifecycle requested', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.requested) || 0))]);
        rows.push(['lifecycle approval_required', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.approvalRequired) || 0))]);
        rows.push(['lifecycle blocked', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.blocked) || 0))]);
        rows.push(['lifecycle executed', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.executed) || 0))]);
        rows.push(['lifecycle failed', String(Math.max(0, Number(run.actionLifecycleCounts && run.actionLifecycleCounts.failed) || 0))]);
        rows.push(['approval status allowed', String(Math.max(0, Number(run.approvalStatusSummary && run.approvalStatusSummary.allowed) || 0))]);
        rows.push(['approval status required', String(Math.max(0, Number(run.approvalStatusSummary && run.approvalStatusSummary.approvalRequired) || 0))]);
        rows.push(['approval status denied', String(Math.max(0, Number(run.approvalStatusSummary && run.approvalStatusSummary.denied) || 0))]);
        rows.push(['approval status n/a', String(Math.max(0, Number(run.approvalStatusSummary && run.approvalStatusSummary.notApplicable) || 0))]);
        const contextDiagnostics = run.contextDiagnostics && typeof run.contextDiagnostics === 'object' ? run.contextDiagnostics : null;
        if (contextDiagnostics) {
          rows.push(['context files', String(Math.max(0, Number(contextDiagnostics.totalFiles) || 0))]);
          rows.push(['context chars', String(Math.max(0, Number(contextDiagnostics.totalChars) || 0))]);
          rows.push(['context budget', String(Math.max(0, Number(contextDiagnostics.budgetUsed) || 0)) + ' / ' + String(Math.max(0, Number(contextDiagnostics.budgetLimit) || 0))]);
          const preview = Array.isArray(contextDiagnostics.items) ? contextDiagnostics.items.slice(0, 3) : [];
          for (const item of preview) {
            const itemText = [normalizeStatusCell(item.path, ''), normalizeStatusCell(item.reason, ''), String(Math.max(0, Number(item.charCount) || 0))].filter(Boolean).join(' | ');
            rows.push(['context file', normalizeStatusCell(itemText, 'not available')]);
          }
        }
        const retryDiagnostics = run.retryDiagnostics && typeof run.retryDiagnostics === 'object' ? run.retryDiagnostics : null;
        const retryAttempts = retryDiagnostics && Array.isArray(retryDiagnostics.attempts) ? retryDiagnostics.attempts : [];
        rows.push(['retries', String(Math.max(0, Number.isFinite(run.retryCount) ? run.retryCount : retryAttempts.length))]);
        if (retryAttempts.length > 0) {
          for (const item of retryAttempts.slice(0, 3)) {
            const retryText = [
              'attempt ' + String(Math.max(0, Number(item.attempt) || 0)),
              normalizeStatusCell(item.reason, 'not available'),
              String(Math.max(0, Number(item.delayMs) || 0)) + ' ms'
            ].join(' | ');
            rows.push(['retry attempt', retryText]);
          }
        }
        const indexingDiagnostics = run.indexingDiagnostics && typeof run.indexingDiagnostics === 'object' ? run.indexingDiagnostics : null;
        if (indexingDiagnostics) {
          rows.push(['indexing', String(Math.max(0, Number(indexingDiagnostics.indexedFiles) || 0)) + ' files']);
          rows.push(['cache', String(Math.max(0, Number(indexingDiagnostics.cacheHits) || 0)) + ' hits / ' + String(Math.max(0, Number(indexingDiagnostics.cacheMisses) || 0)) + ' misses']);
          const indexingMode = indexingDiagnostics.fullRebuild ? 'full rebuild' : indexingDiagnostics.partialRefresh ? 'partial refresh' : 'cached';
          rows.push(['indexing mode', indexingMode]);
        }
        const firstApproval = Array.isArray(run.approvalRequiredActions) && run.approvalRequiredActions.length > 0 ? run.approvalRequiredActions[0] : null;
        if (firstApproval) {
          rows.push(['approval proposal id', normalizeStatusCell(firstApproval.proposalId, 'not available')]);
          rows.push(['approval token format', normalizeStatusCell(firstApproval.approvalTokenFormat, 'not available')]);
          const copyToken = typeof buildApprovalCopyToken === 'function'
            ? buildApprovalCopyToken(firstApproval)
            : normalizeStatusCell(firstApproval.approvalTokenFormat, '');
          if (copyToken) {
            rows.push(['copy token', copyToken]);
          }
        }
        rows.push(['model used', modelUsed]);
        rows.push(['execution mode', normalizeStatusCell(run.executionMode, 'active-workspace')]);
        rows.push(['execution workspace', normalizeStatusCell(run.executionWorkspaceKind, 'active workspace')]);
        rows.push(['active workspace used', String(run.activeWorkspaceUsed !== false)]);
        if (normalizeStatusCell(run.sandboxRoot, '')) rows.push(['sandbox root', normalizeStatusCell(run.sandboxRoot, '')]);
        if (normalizeStatusCell(run.worktreeRoot, '')) rows.push(['worktree root', normalizeStatusCell(run.worktreeRoot, '')]);
        rows.push(['runtime profile', normalizeStatusCell(run.runtimeProfile, 'not available')]);
        rows.push(['runtime endpoint', normalizeStatusCell(run.runtimeEndpoint, 'not available')]);
        rows.push(['configured context window', normalizeStatusCell(run.configuredContextWindow, 'not available')]);
        rows.push(['configured gpu offload', normalizeStatusCell(run.configuredGpuOffloadOptions, 'not available')]);
        rows.push(['runtime tuning profile', normalizeStatusCell(run.runtimeTuningProfile, 'not available')]);
        rows.push(['runtime tuning options', normalizeStatusCell(run.runtimeTuningOptions, 'not available')]);
        rows.push(['runtime tuning source', normalizeStatusCell(run.runtimeTuningSource, 'not available')]);
        rows.push(['runtime tuning applied', String(run.runtimeTuningApplied === true)]);
        rows.push(['runtime tuning warnings', Array.isArray(run.runtimeTuningWarnings) && run.runtimeTuningWarnings.length ? run.runtimeTuningWarnings.join(' | ') : 'none']);
        rows.push(['gpu usage measured', String(run.gpuUsageMeasured === true)]);
        rows.push(['embeddings', embeddingsSummary]);
        rows.push(['duration', normalizeStatusCell(run.duration, 'not available')]);
        rows.push(['workspace', normalizeStatusCell(run.workspace, 'not available')]);
        rows.push(['task', normalizeStatusCell(run.taskPreview, 'not available')]);

        for (const row of rows) {
          const key = document.createElement('div');
          key.className = 'kv-key';
          key.textContent = row[0];
          const value = document.createElement('div');
          value.className = 'kv-value';
          value.textContent = row[1] || 'not available';
          if (row[0] === 'task') {
            value.classList.add('task-preview');
            value.title = String(row[1] || '').trim();
          }
          runStatusGrid.appendChild(key);
          runStatusGrid.appendChild(value);
        }
      }

      function normalizeStatusCell(value, fallback) {
        const text = String(value === undefined || value === null ? '' : value).trim();
        if (!text || text === 'not available') {
          return fallback || 'not available';
        }
        return text;
      }`;

function getWebviewClientStatusRenderer() {
  return webviewClientStatusRenderer;
}

module.exports = { getWebviewClientStatusRenderer };
