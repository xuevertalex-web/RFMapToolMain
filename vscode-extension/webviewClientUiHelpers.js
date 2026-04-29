const webviewClientUiHelpers = `function normalizeFileKey(value) {
        return String(value || '').trim().replace(/\\\\/g, '/').toLowerCase();
      }

      function normalizeLineNumber(value) {
        const parsed = Number(value);
        if (!Number.isFinite(parsed) || parsed <= 0) {
          return null;
        }
        return Math.floor(parsed);
      }

      function resolveRevealRange(document, requestedStartLine, requestedEndLine) {
        const maxLine = Math.max(0, document.lineCount - 1);

        if (requestedStartLine !== null && requestedStartLine !== undefined) {
          const startLine = clampLineIndex(requestedStartLine - 1, maxLine);
          const endLine = requestedEndLine !== null && requestedEndLine !== undefined
            ? clampLineIndex(requestedEndLine - 1, maxLine)
            : startLine;
          return {
            startLine,
            endLine: Math.max(startLine, endLine)
          };
        }

        let targetLine = 0;
        for (let i = 0; i < document.lineCount; i++) {
          if (document.lineAt(i).text.trim().length > 0) {
            targetLine = i;
            break;
          }
        }

        return {
          startLine: targetLine,
          endLine: targetLine
        };
      }

      function clampLineIndex(line, maxLine) {
        if (!Number.isFinite(line)) {
          return 0;
        }
        return Math.max(0, Math.min(maxLine, Math.floor(line)));
      }

      function appendLogLine(kind, text) {
        const value = String(text || '').trimEnd();
        if (!value) {
          return;
        }

        const line = document.createElement('div');
        line.className = 'log-line ' + kind + ' ' + getLogLineClass(value);
        line.textContent = value;
        logs.appendChild(line);
      }

      function getLogLineClass(text) {
        const value = String(text || '').trim();
        if (value.startsWith('Warning:') || value.includes(' error:') || value.includes(' failed')) {
          return 'warn';
        }

        if (value.startsWith('[embedding]')) {
          return value.includes('error') || value.includes('degraded') ? 'embedding warn' : 'embedding';
        }

        if (/^Extracted \\d+ symbols from /.test(value) || /^Found \\d+ C# files/.test(value)) {
          return 'index';
        }

        if (
          value.startsWith('RuntimeRoot:') ||
          value.startsWith('WorkspaceRoot:') ||
          value.startsWith('AccessMode:') ||
          value.startsWith('AccessModeDescription:') ||
          value.startsWith('ProtectedRootsCount:') ||
          value.startsWith('WorkspacePolicy:') ||
          value.startsWith('Using default exclusions:')
        ) {
          return 'meta';
        }

        if (value.startsWith('[structured result received]') || value.startsWith('Latest manifest:')) {
          return 'result';
        }

        return '';
      }

      function appendLogChunk(kind, text) {
        const segments = splitStructuredLogSegments(String(text || ''));
        for (const segment of segments) {
          if (segment.kind === 'structured') {
            appendLogLine('system', '[structured result received]');
            const summaryLines = buildStructuredResultSummaryLines(segment.payload);
            for (const summaryLine of summaryLines) {
              appendLogLine('system', summaryLine);
            }
            suppressPlainResultLog = true;
            continue;
          }

          appendPlainLogChunk(kind, segment.text);
        }
      }

      function appendPlainLogChunk(kind, text) {
        const lines = normalizeLogSeparators(text).split(/\\r?\\n/);
        for (const line of lines) {
          const trimmed = line.trim();
          if (!trimmed) {
            continue;
          }

          if (
            trimmed === '__LOCAL_CURSOR_AGENT_RESULT_START__' ||
            trimmed === '__LOCAL_CURSOR_AGENT_RESULT_END__'
          ) {
            continue;
          }

          if (suppressPlainResultLog && !trimmed.startsWith('Latest manifest:')) {
            continue;
          }

          if (trimmed.startsWith('Latest manifest:')) {
            suppressPlainResultLog = false;
          }

          appendLogLine(kind, line);
        }
      }

      function normalizeLogSeparators(text) {
        return String(text || '')
          .replace(/(WorkspaceRoot:)/g, '\\n$1')
          .replace(/(AccessMode:)/g, '\\n$1')
          .replace(/(AccessModeDescription:)/g, '\\n$1')
          .replace(/(ProtectedRootsCount:)/g, '\\n$1')
          .replace(/(WorkspacePolicy:)/g, '\\n$1')
          .replace(/(Warning:)/g, '\\n$1')
          .replace(/(Using default exclusions:)/g, '\\n$1')
          .replace(/(EmbeddingsStatus:)/g, '\\n$1')
          .replace(/(Found \\d+ C# files)/g, '\\n$1')
          .replace(/(\\s+Extracted \\d+ symbols from )/g, '\\n$1')
          .replace(/(\\[embedding\\])/g, '\\n$1')
          .replace(/(Latest manifest:)/g, '\\n$1')
          .replace(/^\\n+/, '');
      }

      function splitStructuredLogSegments(text) {
        const result = [];
        let remaining = String(text || '');
        while (remaining.length > 0) {
          const jsonStart = remaining.indexOf('{"ok":');
          if (jsonStart < 0) {
            result.push({ kind: 'text', text: remaining });
            break;
          }

          if (jsonStart > 0) {
            result.push({ kind: 'text', text: remaining.slice(0, jsonStart) });
          }

          const jsonEnd = findJsonObjectEnd(remaining, jsonStart);
          if (jsonEnd < 0) {
            result.push({ kind: 'text', text: remaining.slice(jsonStart) });
            break;
          }

          const payloadText = remaining.slice(jsonStart, jsonEnd + 1);
          let payload = null;
          try {
            const parsed = JSON.parse(payloadText);
            if (parsed && typeof parsed === 'object' && typeof parsed.ok === 'boolean') {
              payload = parsed;
            }
          } catch {
            payload = null;
          }

          if (payload) {
            result.push({ kind: 'structured', payload });
          } else {
            result.push({ kind: 'text', text: payloadText });
          }
          remaining = remaining.slice(jsonEnd + 1);
        }

        return result;
      }

      function findJsonObjectEnd(text, startIndex) {
        let depth = 0;
        let inString = false;
        let escaped = false;
        for (let i = startIndex; i < text.length; i++) {
          const ch = text[i];
          if (inString) {
            if (escaped) {
              escaped = false;
            } else if (ch === '\\\\') {
              escaped = true;
            } else if (ch === '"') {
              inString = false;
            }
            continue;
          }

          if (ch === '"') {
            inString = true;
          } else if (ch === '{') {
            depth++;
          } else if (ch === '}') {
            depth--;
            if (depth === 0) {
              return i;
            }
          }
        }

        return -1;
      }

      function buildStructuredResultSummaryLines(payload) {
        if (!payload || typeof payload !== 'object') {
          return [];
        }

        const fallbackReason = normalizeOptionalLogText(payload.fallbackReason);
        const fallbackMode = normalizeOptionalLogText(payload.fallbackMode);
        const finalStatus = normalizeOptionalLogText(payload.finalStatus);
        const status = finalStatus || (payload.ok ? (fallbackReason || fallbackMode ? 'fallback-success' : 'success') : 'error');
        const reasonCode = normalizeOptionalLogText(payload.reasonCode || payload.rootCauseCode || payload.failureCode);
        const summary = normalizeOptionalLogText(payload.summary || payload.summaryText || payload.message);
        const buildText = normalizeStructuredBuildText(payload);
        const changedFiles = Array.isArray(payload.changedFiles) ? payload.changedFiles.filter(Boolean) : [];
        const approvalRequiredActions = Array.isArray(payload.approvalRequiredActions)
          ? payload.approvalRequiredActions.filter(item => item && typeof item === 'object')
          : [];
        const externalAttempts = Number.isFinite(payload.externalAttempts) ? Math.max(0, Math.floor(payload.externalAttempts)) : approvalRequiredActions.length;
        const deniedActions = Number.isFinite(payload.deniedActions) ? Math.max(0, Math.floor(payload.deniedActions)) : 0;
        const blockedActions = Number.isFinite(payload.blockedActions) ? Math.max(0, Math.floor(payload.blockedActions)) : 0;
        const requestedActions = Number.isFinite(payload.requestedActions) ? Math.max(0, Math.floor(payload.requestedActions)) : 0;
        const executedActions = Number.isFinite(payload.executedActions) ? Math.max(0, Math.floor(payload.executedActions)) : 0;
        const failedActions = Number.isFinite(payload.failedActions) ? Math.max(0, Math.floor(payload.failedActions)) : 0;
        const outsideBoundaryAttempts = Number.isFinite(payload.outsideBoundaryAttempts) ? Math.max(0, Math.floor(payload.outsideBoundaryAttempts)) : 0;
        const highRiskApprovalRequiredActions = Number.isFinite(payload.highRiskApprovalRequiredActions) ? Math.max(0, Math.floor(payload.highRiskApprovalRequiredActions)) : 0;
        const approvalStatusSummary = payload.approvalStatusSummary && typeof payload.approvalStatusSummary === 'object'
          ? payload.approvalStatusSummary
          : {};
        const approvalStatusAllowed = Number.isFinite(approvalStatusSummary.allowed) ? Math.max(0, Math.floor(approvalStatusSummary.allowed)) : 0;
        const approvalStatusRequired = Number.isFinite(approvalStatusSummary.approvalRequired) ? Math.max(0, Math.floor(approvalStatusSummary.approvalRequired)) : 0;
        const approvalStatusDenied = Number.isFinite(approvalStatusSummary.denied) ? Math.max(0, Math.floor(approvalStatusSummary.denied)) : 0;
        const approvalStatusNotApplicable = Number.isFinite(approvalStatusSummary.notApplicable) ? Math.max(0, Math.floor(approvalStatusSummary.notApplicable)) : 0;
        const hostBoundaryPreserved = typeof payload.hostBoundaryPreserved === 'boolean' ? payload.hostBoundaryPreserved : true;
        const modelProvider = normalizeOptionalLogText(payload.provider || payload.modelProvider);
        const model = normalizeOptionalLogText(payload.model);
        const modelText = [modelProvider, model].filter(Boolean).join(' / ');
        const embeddingsStatus = normalizeOptionalLogText(payload.embeddingsStatus || payload.EmbeddingsStatus);
        const degradedFlags = Array.isArray(payload.degradedFlags)
          ? payload.degradedFlags.map(item => normalizeOptionalLogText(item)).filter(Boolean)
          : [];
        const embeddingsInfo = formatEmbeddingsLogStatus(embeddingsStatus, degradedFlags);

        const lines = [];
        lines.push('Status: ' + (status || 'not available'));
        if (finalStatus) lines.push('FinalStatus: ' + finalStatus);
        if (reasonCode) lines.push('ReasonCode: ' + reasonCode);
        if (summary) lines.push('Summary: ' + summary);
        lines.push('Build: ' + buildText);
        lines.push('ChangedFiles: ' + String(changedFiles.length));
        lines.push('ApprovalRequired: ' + String(approvalRequiredActions.length));
        lines.push('ExternalAttempts: ' + String(externalAttempts));
        lines.push('DeniedActions: ' + String(deniedActions));
        lines.push('BlockedActions: ' + String(blockedActions));
        lines.push('RequestedActions: ' + String(requestedActions));
        lines.push('ExecutedActions: ' + String(executedActions));
        lines.push('FailedActions: ' + String(failedActions));
        lines.push('OutsideBoundaryAttempts: ' + String(outsideBoundaryAttempts));
        lines.push('HighRiskApprovalRequired: ' + String(highRiskApprovalRequiredActions));
        lines.push('ApprovalStatusAllowed: ' + String(approvalStatusAllowed));
        lines.push('ApprovalStatusRequired: ' + String(approvalStatusRequired));
        lines.push('ApprovalStatusDenied: ' + String(approvalStatusDenied));
        lines.push('ApprovalStatusNotApplicable: ' + String(approvalStatusNotApplicable));
        lines.push('HostBoundaryPreserved: ' + String(hostBoundaryPreserved));
        for (const item of approvalRequiredActions.slice(0, 3)) {
          const actionType = normalizeOptionalLogText(item.actionType) || 'UnknownAction';
          const target = normalizeOptionalLogText(item.normalizedTarget || item.path || item.command);
          const sandboxRoot = normalizeOptionalLogText(item.sandboxRoot);
          const projectRoot = normalizeOptionalLogText(item.projectRoot);
          const worktreeRoot = normalizeOptionalLogText(item.worktreeRoot);
          const riskLevel = normalizeOptionalLogText(item.riskLevel);
          const reasonCode = normalizeOptionalLogText(item.reasonCode);
          const expectedEffect = normalizeOptionalLogText(item.expectedEffect);
          const approvalStatus = normalizeOptionalLogText(item.approvalStatus);
          const reason = normalizeOptionalLogText(item.reason);
          lines.push('ApprovalProposal: ' + [actionType, target, sandboxRoot, projectRoot, worktreeRoot, riskLevel, reasonCode, expectedEffect, approvalStatus, reason].filter(Boolean).join(' | '));
        }
        if (fallbackReason || fallbackMode) lines.push('Fallback: ' + [fallbackReason, fallbackMode].filter(Boolean).join(' / '));
        if (modelText) lines.push('Model: ' + modelText);
        if (embeddingsInfo.text) lines.push('Embeddings: ' + embeddingsInfo.text);
        return lines;
      }

      function formatEmbeddingsLogStatus(status, degradedFlags) {
        const normalizedStatus = normalizeOptionalLogText(status).toLowerCase();
        const flags = Array.isArray(degradedFlags)
          ? degradedFlags.map(item => normalizeOptionalLogText(item).toLowerCase()).filter(Boolean)
          : [];
        const embeddingsFlagged = flags.some(flag => flag.includes('embed'));

        if (normalizedStatus === 'disabled' || normalizedStatus === 'unavailable') {
          return { text: normalizedStatus };
        }

        if (normalizedStatus === 'notfound') {
          return { text: 'disabled (model not found)' };
        }

        if (normalizedStatus === 'degraded' || embeddingsFlagged) {
          return { text: 'degraded (semantic retrieval limited)' };
        }

        if (normalizedStatus === 'ready' || normalizedStatus === 'ok' || normalizedStatus === 'active' || normalizedStatus === 'enabled') {
          return { text: 'active' };
        }

        if (normalizedStatus) {
          return { text: status };
        }

        if (flags.length) {
          return { text: 'degraded (semantic retrieval limited)' };
        }

        return { text: '' };
      }

      function normalizeStructuredBuildText(payload) {
        const explicit = normalizeOptionalLogText(payload && payload.buildText);
        if (explicit) {
          return explicit;
        }

        if (payload && typeof payload.buildStarted === 'boolean') {
          if (!payload.buildStarted) return 'not started';
          if (payload.buildSucceeded === true) return 'succeeded';
          if (payload.buildSucceeded === false) return 'failed';
        }

        return 'not run';
      }

      function normalizeOptionalLogText(value) {
        return String(value === undefined || value === null ? '' : value).trim();
      }

      function clearChangedRangeDecoration(editor) {
        try {
          if (editor) {
            editor.setDecorations(changedRangeDecoration, []);
          }
        } catch {
          // ignore stale/closed editors
        }
      }

      function clearChangedRangeDecorationTimer() {
        if (changedRangeDecorationTimer !== null) {
          clearTimeout(changedRangeDecorationTimer);
          changedRangeDecorationTimer = null;
        }
      }

      function scheduleChangedRangeDecorationClear(editor) {
        clearChangedRangeDecorationTimer();
        changedRangeDecorationTimer = setTimeout(() => {
          clearChangedRangeDecoration(editor);
          if (lastDecoratedEditor === editor) {
            lastDecoratedEditor = null;
          }
          changedRangeDecorationTimer = null;
        }, CHANGED_RANGE_DECORATION_TIMEOUT_MS);
      }

      function normalizeStatus(status) {
        const value = String(status || '').toLowerCase();
        if (value === 'added' || value === 'create' || value === 'created' || value === 'new') return 'added';
        if (value === 'removed' || value === 'deleted' || value === 'delete') return 'removed';
        if (value === 'renamed' || value === 'moved') return 'renamed';
        if (value === 'updated' || value === 'modified' || value === 'changed') return 'updated';
        return 'modified';
      }

      function normalizeKind(kind) {
        const value = String(kind || '').trim();
        if (!value) {
          return '';
        }

        const allowed = new Set(['BugFix', 'Validation', 'Refactor', 'BuildFix', 'FeatureAdd', 'Update', 'Unknown']);
        if (allowed.has(value)) {
          return value;
        }

        const normalized = value.toLowerCase();
        if (normalized === 'bugfix' || normalized === 'bug_fix' || normalized === 'fix') return 'BugFix';
        if (normalized === 'validation') return 'Validation';
        if (normalized === 'refactor') return 'Refactor';
        if (normalized === 'buildfix' || normalized === 'build_fix' || normalized === 'build') return 'BuildFix';
        if (normalized === 'featureadd' || normalized === 'feature_add' || normalized === 'feature') return 'FeatureAdd';
        if (normalized === 'update' || normalized === 'updated') return 'Update';
        return 'Unknown';
      }

      function getChangedKindPriority(kind) {
        const normalized = normalizeKind(kind);
        switch (normalized) {
          case 'BuildFix': return 1;
          case 'BugFix': return 2;
          case 'Validation': return 3;
          case 'FeatureAdd': return 4;
          case 'Refactor': return 5;
          case 'Update': return 6;
          case 'Unknown':
          default:
            return 7;
        }
      }

      function getChangedKindBadgeStyle(kind) {
        const normalized = normalizeKind(kind);
        switch (normalized) {
          case 'BugFix':
            return { className: 'bugfix', background: 'rgba(232, 116, 80, 0.16)', borderColor: 'rgba(232, 116, 80, 0.34)', color: 'inherit' };
          case 'Validation':
            return { className: 'validation', background: 'rgba(70, 130, 180, 0.16)', borderColor: 'rgba(70, 130, 180, 0.34)', color: 'inherit' };
          case 'Refactor':
            return { className: 'refactor', background: 'rgba(194, 156, 59, 0.16)', borderColor: 'rgba(194, 156, 59, 0.34)', color: 'inherit' };
          case 'BuildFix':
            return { className: 'buildfix', background: 'rgba(64, 160, 96, 0.16)', borderColor: 'rgba(64, 160, 96, 0.34)', color: 'inherit' };
          case 'FeatureAdd':
            return { className: 'featureadd', background: 'rgba(136, 99, 255, 0.16)', borderColor: 'rgba(136, 99, 255, 0.34)', color: 'inherit' };
          case 'Update':
            return { className: 'update', background: 'rgba(104, 128, 160, 0.16)', borderColor: 'rgba(104, 128, 160, 0.34)', color: 'inherit' };
          default:
            return { className: 'unknown', background: 'rgba(112, 112, 112, 0.14)', borderColor: 'rgba(112, 112, 112, 0.28)', color: 'inherit' };
        }
      }

      function inferStatusFromPath(value) {
        const text = String(value || '').trim();
        if (!text) return 'modified';
        if (text.startsWith('+')) return 'added';
        if (text.startsWith('-')) return 'removed';
        if (text.startsWith('~')) return 'updated';
        return 'modified';
      }

      function highlightOpenedChangedFile(pathValue) {
        const items = changedFiles.querySelectorAll('li[data-file-path]');
        for (const item of items) {
          if (item.dataset.filePath === pathValue) {
            item.dataset.status = 'opened';
            const badge = item.querySelector('.status-badge');
            if (badge) {
              badge.textContent = 'opened';
              badge.className = 'status-badge opened';
            }
            item.scrollIntoView({ block: 'center', behavior: 'smooth' });
            break;
          }
        }
      }

      function escapeHtml(text) {
        return text
          .replace(/&/g, '&amp;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;')
          .replace(/"/g, '&quot;')
          .replace(/'/g, '&#39;');
      }`;

function getWebviewClientUiHelpers() {
  return webviewClientUiHelpers;
}

module.exports = { getWebviewClientUiHelpers };
