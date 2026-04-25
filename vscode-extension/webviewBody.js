const { version } = require('./package.json');

const webviewBody = `
    <div class="agent-shell">
      <header class="agent-topbar">
        <div>
          <div class="agent-tab">ЧАТ</div>
          <div class="agent-title">Local Agent</div>
        </div>
        <div class="agent-toolbar" aria-label="Действия">
          <button id="helpButton" class="icon-button" type="button" title="Справка">?</button>
          <button id="clearOutput" class="icon-button" type="button" title="Новая сессия">+</button>
          <button id="rerunLast" class="icon-button" type="button" title="Повторить последний запуск">↻</button>
          <button id="exportRunReport" class="icon-button" type="button" title="Экспорт отчета">⇩</button>
        </div>
      </header>

      <section class="sessions-strip">
        <div class="sessions-title">СЕАНСЫ</div>
        <div id="recentRuns" class="recent-runs"></div>
      </section>

      <main class="chat-scroll">
        <div id="status" class="status-line">Ожидает задачу</div>
        <div id="thinkingIndicator" class="thinking-indicator" aria-live="polite" style="display: none;">
          <span class="thinking-dot"></span>
          <span class="thinking-text">Агент думает</span>
        </div>
        <section class="message agent-message">
          <div class="message-role">Ответ <span id="resultBadge" class="result-badge"></span></div>
          <div id="result" class="message-content muted">Здесь появится ответ агента.</div>
        </section>

        <section id="structuredResultSection" class="details-section" style="display: none;">
          <section id="runStatusSection" class="run-section">
            <div class="details-title">Статус</div>
            <div id="runStatusGrid" class="kv-grid"></div>
          </section>
          <div class="details-title">Сводка</div>
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
          <div id="changedFilesTitle" class="details-title">Измененные файлы</div>
          <div class="details-actions">
            <button id="copyStructuredResult" type="button">Копировать результат</button>
            <button id="copyChangedFiles" type="button">Копировать файлы</button>
            <button id="exportChangedFiles" type="button">Экспорт файлов</button>
            <button id="openAllChangedFiles" type="button">Открыть файлы</button>
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
            <span>Фильтр</span>
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
            <div id="buildStatus" class="build-status na">Сборка: не запускалась</div>
            <div id="diagnosticsSummary" class="diagnostics-summary"></div>
            <ul id="diagnosticsList" class="diagnostics-list"></ul>
            <div id="diagnosticsEmpty" class="empty-state">No parsed diagnostics available</div>
          </section>
        </section>

        <section class="logs-section">
          <div class="logs-header-row">
            <h3 id="logsHeader">Логи</h3>
            <button id="copyLogs" type="button">Копировать</button>
          </div>
          <div id="logs"></div>
        </section>
      </main>

      <footer class="composer">
        <textarea id="task" placeholder="Опишите, что нужно создать или изменить"></textarea>
        <div class="composer-actions">
          <button id="copyResult" class="secondary-button" type="button">Копировать ответ</button>
          <button id="stop" class="secondary-button" type="button" disabled>Остановить</button>
          <button id="send" class="send-button" type="button" title="Отправить">↑</button>
        </div>
        <div class="composer-status">
          <span id="modelName" class="composer-status-item">Модель: не определена</span>
          <span id="modelPing" class="composer-status-item">Пинг: нет данных</span>
          <span class="composer-status-item">Версия: ${version}</span>
        </div>
      </footer>
    </div>`;

function getWebviewBody() {
  return webviewBody;
}

module.exports = { getWebviewBody };
