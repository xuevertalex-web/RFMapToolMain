const { version } = require('./package.json');

const webviewBody = `
    <div class="agent-shell">
      <header class="agent-topbar">
        <div>
          <div class="agent-tab">CHAT</div>
          <div class="agent-title">Local Agent</div>
        </div>
        <div class="agent-toolbar" aria-label="Actions">
          <button id="helpButton" class="icon-button" type="button" title="Help">?</button>
          <button id="clearOutput" class="icon-button" type="button" title="New Session">+</button>
          <button id="rerunLast" class="icon-button" type="button" title="Rerun Last">R</button>
          <button id="exportRunReport" class="icon-button" type="button" title="Export Run Report">E</button>
        </div>
      </header>

      <section class="sessions-strip">
        <div class="sessions-title">SESSIONS</div>
        <div id="recentRuns" class="recent-runs"></div>
      </section>

      <main class="chat-scroll">
        <div id="status" class="status-line">Waiting for task</div>
        <div id="thinkingIndicator" class="thinking-indicator" aria-live="polite" style="display: none;">
          <span class="thinking-dot"></span>
          <span class="thinking-text">Agent is thinking</span>
        </div>
        <section class="message agent-message">
          <div class="message-role">Result <span id="resultBadge" class="result-badge"></span></div>
          <div id="result" class="message-content muted">Agent response will appear here.</div>
        </section>

        <section id="structuredResultSection" class="details-section" style="display: none;">
          <section id="runStatusSection" class="run-section">
            <div class="details-title">Status</div>
            <div id="runStatusGrid" class="kv-grid"></div>
          </section>
          <div class="details-title">Summary</div>
          <div id="summary" class="summary-box"></div>
          <section id="failureSection" class="run-section" style="display: none;">
            <div class="details-title">Failure</div>
            <div id="failureSummary" class="failure-grid"></div>
          </section>
          <section id="timelineSection" class="run-section">
            <div class="details-title">Timeline</div>
            <ol id="timelineList" class="timeline-list"></ol>
            <div id="timelineEmpty" class="empty-state">No timeline events available</div>
          </section>
          <div id="changedFilesTitle" class="details-title">Changed Files</div>
          <div class="details-actions">
            <button id="copyStructuredResult" type="button">Copy Structured Result</button>
            <button id="copyChangedFiles" type="button">Copy Changed Files</button>
            <button id="exportChangedFiles" type="button">Export Changed Files</button>
            <button id="openAllChangedFiles" type="button">Open Changed Files</button>
          </div>
          <div id="runStats" class="summary-box"></div>
          <div id="changedFilesLegend" class="changed-legend">
            <span class="status-badge added">added</span>
            <span class="status-badge updated">updated</span>
            <span class="status-badge removed">removed</span>
            <span class="status-badge renamed">renamed</span>
            <span class="status-badge modified">modified</span>
            <span class="status-badge opened">opened</span>
          </div>
          <label class="filter-row" for="changedKindFilter">
            <span>Filter</span>
            <select id="changedKindFilter">
              <option value="All">All</option>
              <option value="BuildFix">BuildFix</option>
              <option value="BugFix">BugFix</option>
              <option value="Validation">Validation</option>
              <option value="FeatureAdd">FeatureAdd</option>
              <option value="Refactor">Refactor</option>
              <option value="Update">Update</option>
              <option value="Unknown">Unknown</option>
            </select>
          </label>
          <ul id="changedFiles"></ul>
          <section id="diagnosticsSection" class="run-section">
            <div class="details-title">Build / Diagnostics</div>
            <div id="buildStatus" class="build-status na">Build: not started</div>
            <div id="diagnosticsSummary" class="diagnostics-summary"></div>
            <ul id="diagnosticsList" class="diagnostics-list"></ul>
            <div id="diagnosticsEmpty" class="empty-state">No parsed diagnostics available</div>
          </section>
        </section>

        <section class="logs-section">
          <div class="logs-header-row">
            <h3 id="logsHeader">Logs (agent + app)</h3>
            <button id="copyLogs" type="button">Copy</button>
          </div>
          <div id="logs"></div>
        </section>
      </main>

      <footer class="composer">
        <textarea id="task" placeholder="Describe what to create or change"></textarea>
        <div class="composer-actions">
          <button id="copyResult" class="secondary-button" type="button">Copy Result</button>
          <button id="stop" class="secondary-button" type="button" disabled>Stop</button>
          <button id="send" class="send-button" type="button" title="Send">Send</button>
        </div>
        <label class="model-selector-row composer-model-row" for="modelSelector">
          <span>Model</span>
          <select id="modelSelector"></select>
          <span id="modelPing" class="model-selector-ping">Ping: no data</span>
        </label>
        <div id="modelSelectionToast" class="model-selection-toast" aria-live="polite"></div>
        <div id="modelSelectionStatus" class="model-selection-status" aria-live="polite"></div>
        <div class="composer-status">
          <span class="composer-status-item">Version: ${version}</span>
        </div>
      </footer>
    </div>`;

function getWebviewBody() {
  return webviewBody;
}

module.exports = { getWebviewBody };
