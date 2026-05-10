const webviewStyles = `
      html, body {
        height: 100%;
        margin: 0;
        padding: 0;
        overflow-x: hidden;
        color: var(--vscode-editor-foreground);
        background: var(--vscode-sideBar-background, var(--vscode-editor-background));
        font-family: var(--vscode-font-family);
        font-size: var(--vscode-font-size);
      }

      button, textarea, select {
        box-sizing: border-box;
        font: inherit;
      }

      button {
        color: var(--vscode-button-foreground);
        background: var(--vscode-button-background);
        border: 1px solid var(--vscode-button-border, transparent);
        border-radius: 4px;
        padding: 5px 8px;
      }

      button:disabled {
        opacity: 0.55;
        cursor: not-allowed;
      }

      .agent-shell {
        height: 100vh;
        display: flex;
        flex-direction: column;
        min-width: 0;
        max-width: 100vw;
        overflow-x: hidden;
      }

      .agent-topbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
        padding: 10px 12px 8px;
        border-bottom: 1px solid var(--vscode-sideBar-border, var(--vscode-panel-border));
        min-width: 0;
      }

      .agent-tab { display:none; }

      .agent-title {
        margin-top: 0;
        font-weight: 600;
        font-size: 16px;
        text-transform: none;
        color: var(--vscode-sideBarTitle-foreground, var(--vscode-foreground));
      }

      .sessions-title, .details-title {
        margin-top: 8px;
        font-weight: 700;
        font-size: 11px;
        text-transform: uppercase;
        color: var(--vscode-sideBarTitle-foreground, var(--vscode-foreground));
      }

      .app-version {
        margin-left: 6px;
        font-weight: 500;
        color: var(--vscode-descriptionForeground);
        text-transform: none;
      }

      .agent-toolbar, .composer-actions, .details-actions, .logs-header-row {
        display: flex;
        align-items: center;
        gap: 6px;
        flex-wrap: wrap;
        min-width: 0;
      }

      .logs-header-row h3 {
        flex: 1 1 auto;
        min-width: 0;
        margin: 0;
      }

      .logs-header-row button, .details-actions button, .composer-actions button {
        flex: 0 0 auto;
        white-space: nowrap;
      }

      .icon-button, .secondary-button {
        width: auto;
        min-width: 28px;
        background: transparent;
        color: var(--vscode-icon-foreground);
        border-color: transparent;
      }

      .icon-button:hover, .secondary-button:hover {
        background: var(--vscode-toolbar-hoverBackground);
      }

      .sessions-strip {
        display: block;
        padding: 8px 12px;
        border-bottom: 1px solid var(--vscode-sideBar-border, var(--vscode-panel-border));
        min-width: 0;
        overflow-x: hidden;
      }
      .dialog-header-left { display:flex; align-items:center; gap:8px; min-width:0; }

      #helpButton {
        display: none;
      }

      .recent-runs {
        margin-top: 6px;
        min-width: 0;
        overflow-x: hidden;
      }

      .recent-run-card {
        padding: 6px 8px;
        margin-bottom: 6px;
        border: 1px solid var(--vscode-input-border, transparent);
        border-radius: 6px;
        cursor: pointer;
        background: var(--vscode-input-background);
      }

      .recent-run-top {
        display: flex;
        justify-content: space-between;
        gap: 6px;
        align-items: center;
      }

      .recent-run-left {
        display: flex;
        gap: 6px;
        align-items: center;
      }

      .recent-run-time {
        opacity: 0.72;
        font-size: 0.78em;
      }

      .recent-run-actions {
        display: flex;
        gap: 4px;
      }

      .recent-run-action {
        padding: 1px 6px;
        font-size: 0.76em;
      }

      .recent-run-task {
        margin-top: 4px;
        font-weight: 600;
        font-size: 0.9em;
      }

      .recent-run-preview {
        margin-top: 2px;
        opacity: 0.85;
        font-size: 0.82em;
        display: -webkit-box;
        -webkit-box-orient: vertical;
        -webkit-line-clamp: 2;
        overflow: hidden;
      }
      .sessions-tabs {
        display: flex;
        gap: 6px;
        margin-top: 6px;
      }
      .sessions-tabs .icon-button.active {
        background: var(--vscode-toolbar-hoverBackground);
        border-color: var(--vscode-focusBorder);
      }

      .chat-scroll {
        flex: 1;
        min-height: 0;
        overflow-y: scroll;
        overflow-x: hidden;
        padding: 10px 12px;
        scrollbar-gutter: stable;
      }

      .status-line {
        color: var(--vscode-descriptionForeground);
        margin-bottom: 8px;
      }

      .thinking-indicator {
        align-items: center;
        gap: 8px;
        margin: 0 0 8px;
        color: var(--vscode-descriptionForeground);
      }

      .thinking-dot {
        width: 8px;
        height: 8px;
        border-radius: 999px;
        background: var(--vscode-progressBar-background, var(--vscode-focusBorder));
        animation: thinkingPulse 1.2s ease-in-out infinite;
      }

      .thinking-text {
        animation: thinkingTextPulse 1.6s ease-in-out infinite;
      }

      .thinking-text::after {
        content: '';
        animation: thinkingDots 1.2s steps(4, end) infinite;
      }

      @keyframes thinkingPulse {
        0%, 100% { opacity: 0.35; transform: scale(0.85); }
        50% { opacity: 1; transform: scale(1); }
      }

      @keyframes thinkingTextPulse {
        0%, 100% { opacity: 0.55; }
        50% { opacity: 1; }
      }

      @keyframes thinkingDots {
        0% { content: ''; }
        25% { content: '.'; }
        50% { content: '..'; }
        75%, 100% { content: '...'; }
      }

      .message, .run-section, .details-section, .logs-section {
        box-sizing: border-box;
        width: 100%;
        max-width: 100%;
        min-width: 0;
        border: 1px solid var(--vscode-panel-border);
        background: var(--vscode-editor-background);
        border-radius: 6px;
        padding: 10px;
        margin-bottom: 10px;
      }

      .message-role {
        font-weight: 600;
        margin-bottom: 8px;
      }

      .dialog-thread {
        display: block;
        margin-bottom: 10px;
      }

      .thread-empty {
        color: var(--vscode-descriptionForeground);
        font-size: 0.9em;
        padding: 8px 0;
      }

      .thread-message {
        border: 1px solid var(--vscode-panel-border);
        border-radius: 8px;
        padding: 8px 10px;
        margin-bottom: 8px;
        background: var(--vscode-editor-background);
      }

      .thread-message-user {
        border-color: rgba(9, 105, 218, 0.35);
        background: rgba(9, 105, 218, 0.08);
      }

      .thread-message-assistant {
        border-color: rgba(46, 160, 67, 0.35);
        background: rgba(46, 160, 67, 0.08);
      }

      .thread-message-meta {
        opacity: 0.75;
        font-size: 0.78em;
        margin-bottom: 4px;
      }

      .thread-message-body {
        white-space: pre-wrap;
        overflow-wrap: anywhere;
        line-height: 1.4;
      }

      .message-content, #logs, .summary-box {
        white-space: pre-wrap;
        overflow-wrap: anywhere;
        line-height: 1.45;
      }

      .muted, .empty-state {
        color: var(--vscode-descriptionForeground);
      }

      .composer {
        box-sizing: border-box;
        min-width: 0;
        overflow-x: hidden;
        padding: 10px;
        border-top: 1px solid var(--vscode-sideBar-border, var(--vscode-panel-border));
        background: var(--vscode-sideBar-background, var(--vscode-editor-background));
      }

      #task {
        width: 100%;
        min-height: 84px;
        max-height: none;
        resize: none;
        overflow: hidden;
        color: var(--vscode-input-foreground);
        background: var(--vscode-input-background);
        border: 1px solid var(--vscode-input-border, var(--vscode-panel-border));
        border-radius: 8px;
        padding: 10px;
      }

      .composer-actions {
        justify-content: flex-end;
        margin-top: 6px;
      }

      .composer-model-row {
        margin-top: 6px;
        width: 100%;
        justify-content: flex-start;
      }

      .model-selector-row {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        margin-right: auto;
        color: var(--vscode-descriptionForeground);
      }

      .model-selector-ping {
        font-size: 0.82em;
        color: var(--vscode-descriptionForeground);
        opacity: 0.95;
        max-width: 220px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .model-selector-row select {
        min-width: 220px;
        max-width: 100%;
        padding: 4px 8px;
        color: var(--vscode-input-foreground);
        background: var(--vscode-input-background);
        border: 1px solid var(--vscode-input-border, var(--vscode-panel-border));
        border-radius: 6px;
      }

      .model-selection-toast {
        display: none;
        margin-top: 8px;
        padding: 6px 8px;
        border-radius: 6px;
        border: 1px solid rgba(46, 160, 67, 0.30);
        background: rgba(46, 160, 67, 0.16);
        color: var(--vscode-foreground);
      }

      .model-selection-toast.show {
        display: block;
      }

      .model-selection-toast.warn {
        border-color: rgba(143, 112, 0, 0.35);
        background: rgba(143, 112, 0, 0.14);
      }

      .model-selection-status {
        margin-top: 6px;
        min-height: 1.2em;
        color: var(--vscode-descriptionForeground);
      }

      .composer-status {
        display: flex;
        align-items: center;
        gap: 8px;
        flex-wrap: wrap;
        margin-top: 4px;
        color: var(--vscode-descriptionForeground);
        font-size: 0.78em;
        opacity: 0.85;
      }

      .composer-status-item {
        min-width: 0;
        overflow-wrap: anywhere;
      }

      @media (max-width: 760px) {
        .composer-actions {
          justify-content: flex-start;
          gap: 4px;
        }

        .secondary-button {
          font-size: 0.92em;
          padding: 4px 6px;
        }

        .model-selector-row {
          gap: 6px;
        }

        .model-selector-row select {
          min-width: 160px;
          flex: 1 1 180px;
        }

        .model-selector-ping {
          max-width: 100%;
          white-space: normal;
          line-height: 1.2;
        }
      }

      .send-button {
        min-width: 32px;
        height: 28px;
        border-radius: 999px;
      }

      .kv-grid, .failure-grid {
        display: grid;
        grid-template-columns: 92px minmax(0, 1fr);
        gap: 6px 10px;
        min-width: 0;
        align-items: start;
      }

      .kv-key {
        color: var(--vscode-descriptionForeground);
      }

      .kv-value {
        overflow-wrap: anywhere;
      }

      .kv-value.task-preview {
        display: -webkit-box;
        -webkit-box-orient: vertical;
        -webkit-line-clamp: 4;
        overflow: hidden;
      }

      .timeline-list, .diagnostics-list, #changedFiles {
        margin: 8px 0 0;
        padding-left: 18px;
      }

      .timeline-item, .diagnostic-item, #changedFiles li {
        margin: 5px 0;
        padding: 5px 7px;
        border-radius: 4px;
        border: 1px solid transparent;
      }

      .timeline-title {
        font-weight: 600;
      }

      .timeline-detail {
        color: var(--vscode-descriptionForeground);
        margin-top: 2px;
      }

      #logs {
        display: none;
        max-height: 240px;
        overflow-y: auto;
        overflow-x: hidden;
        padding: 8px;
        background: var(--vscode-input-background);
        border: 1px solid var(--vscode-input-border, var(--vscode-panel-border));
        border-radius: 4px;
        font-family: var(--vscode-editor-font-family);
        font-size: var(--vscode-editor-font-size);
      }

      .log-line {
        display: block;
        box-sizing: border-box;
        max-width: 100%;
        margin: 2px 0;
        padding: 2px 6px;
        border-radius: 4px;
        white-space: pre-wrap;
        overflow-wrap: anywhere;
      }

      .log-line.stdout { background: rgba(128, 128, 128, 0.06); }
      .log-line.stderr, .diagnostic-item.error { background: rgba(248, 81, 73, 0.10); }
      .log-line.system { background: rgba(9, 105, 218, 0.08); }
      .log-line.meta { color: var(--vscode-descriptionForeground); background: rgba(128, 128, 128, 0.05); }
      .log-line.index { border-left: 2px solid rgba(9, 105, 218, 0.55); padding-left: 8px; }
      .log-line.embedding { border-left: 2px solid rgba(143, 112, 0, 0.65); padding-left: 8px; }
      .log-line.warn { background: rgba(143, 112, 0, 0.14); border: 1px solid rgba(143, 112, 0, 0.28); }
      .log-line.result { background: rgba(46, 160, 67, 0.12); border: 1px solid rgba(46, 160, 67, 0.28); }
      .diagnostic-item.warning { background: rgba(143, 112, 0, 0.12); }

      #changedFiles li:hover {
        text-decoration: underline;
      }

      #changedFiles li[data-status="added"] { background: rgba(46, 160, 67, 0.12); border-color: rgba(46, 160, 67, 0.35); }
      #changedFiles li[data-status="updated"] { background: rgba(9, 105, 218, 0.12); border-color: rgba(9, 105, 218, 0.35); }
      #changedFiles li[data-status="removed"] { background: rgba(248, 81, 73, 0.12); border-color: rgba(248, 81, 73, 0.35); }
      #changedFiles li[data-status="renamed"] { background: rgba(143, 112, 0, 0.12); border-color: rgba(143, 112, 0, 0.35); }
      #changedFiles li[data-status="modified"] { background: rgba(128, 128, 128, 0.10); border-color: rgba(128, 128, 128, 0.25); }
      #changedFiles li[data-status="opened"] { outline: 2px solid var(--vscode-focusBorder); }

      .status-badge, .result-badge, .kind-badge {
        display: inline-block;
        margin-left: 6px;
        padding: 1px 6px;
        border-radius: 999px;
        font-size: 0.8em;
        vertical-align: middle;
        border: 1px solid transparent;
      }

      .status-badge.added, .result-badge.ok, .build-status.ok, .summary-box.ok { background: rgba(46, 160, 67, 0.16); border-color: rgba(46, 160, 67, 0.30); }
      .status-badge.updated { background: rgba(9, 105, 218, 0.16); border-color: rgba(9, 105, 218, 0.30); }
      .status-badge.removed, .result-badge.error, .build-status.fail, .summary-box.error { background: rgba(248, 81, 73, 0.16); border-color: rgba(248, 81, 73, 0.30); }
      .status-badge.renamed, .result-badge.running, .summary-box.warn { background: rgba(143, 112, 0, 0.16); border-color: rgba(143, 112, 0, 0.30); }
      .status-badge.modified, .build-status.na, .summary-box.na { background: rgba(128, 128, 128, 0.12); border-color: rgba(128, 128, 128, 0.24); }

      .summary-box, .build-status {
        padding: 8px;
        border-radius: 4px;
        border: 1px solid transparent;
      }

      .changed-legend, .filter-row {
        display: flex;
        align-items: center;
        gap: 8px;
        flex-wrap: wrap;
        margin: 8px 0;
      }

      .filter-row select {
        color: var(--vscode-dropdown-foreground);
        background: var(--vscode-dropdown-background);
        border: 1px solid var(--vscode-dropdown-border);
      }`;

function getWebviewStyles() {
  return webviewStyles;
}

module.exports = { getWebviewStyles };
