const { version } = require('./package.json');

const webviewBody = `
    <div class="agent-shell">
      <header class="agent-topbar">
        <div class="dialog-header-left">
          <button id="backToDialogs" class="icon-button" type="button" title="Назад к диалогам" style="display:none;">←</button>
          <div id="dialogTitle" class="agent-title">Диалоги</div>
        </div>
        <div class="agent-toolbar" aria-label="Действия">
          <button id="helpButton" class="icon-button" type="button" title="Справка">?</button>
        </div>
      </header>

      <section class="sessions-strip">
        <div class="sessions-title">Сессии</div>
        <div class="sessions-tabs">
          <button id="clearOutput" class="icon-button" type="button" title="Новая сессия">Новая</button>
          <button id="sessionsTabActive" class="icon-button" type="button">Активные</button>
          <button id="sessionsTabArchived" class="icon-button" type="button">Архив</button>
        </div>
        <div id="recentRuns" class="recent-runs"></div>
      </section>

      <main class="chat-scroll">
        <div id="status" class="status-line">Ожидание задачи</div>
        <div id="thinkingIndicator" class="thinking-indicator" aria-live="polite" style="display: none;">
          <span class="thinking-dot"></span>
          <span class="thinking-text">Агент думает</span>
        </div>
        <section id="dialogThread" class="dialog-thread"></section>
        <section id="resultSection" class="message agent-message">
          <div class="message-role">Результат <span id="resultBadge" class="result-badge"></span></div>
          <div id="result" class="message-content muted">Ответ агента появится здесь.</div>
        </section>

        <section id="structuredResultSection" class="details-section" style="display: none;">
          <section id="runStatusSection" class="run-section">
            <div class="details-title">Статус</div>
            <div id="runStatusGrid" class="kv-grid"></div>
          </section>
          <div class="details-title">Сводка</div>
          <div id="summary" class="summary-box"></div>
          <section id="failureSection" class="run-section" style="display: none;">
            <div class="details-title">Ошибка</div>
            <div id="failureSummary" class="failure-grid"></div>
          </section>
          <section id="timelineSection" class="run-section" style="display: none;">
            <div class="details-title">Хронология</div>
            <ol id="timelineList" class="timeline-list"></ol>
            <div id="timelineEmpty" class="empty-state">События хронологии отсутствуют</div>
          </section>
          <div id="changedFilesTitle" class="details-title">Измененные файлы</div>
          <div class="details-actions">
            <button id="copyStructuredResult" type="button">Копировать структурированный результат</button>
            <button id="copyChangedFiles" type="button">Копировать измененные файлы</button>
            <button id="exportChangedFiles" type="button">Экспортировать измененные файлы</button>
            <button id="openAllChangedFiles" type="button">Открыть измененные файлы</button>
          </div>
          <div id="runStats" class="summary-box"></div>
          <div id="changedFilesLegend" class="changed-legend">
            <span class="status-badge added">добавлен</span>
            <span class="status-badge updated">обновлен</span>
            <span class="status-badge removed">удален</span>
            <span class="status-badge renamed">переименован</span>
            <span class="status-badge modified">изменен</span>
            <span class="status-badge opened">открыт</span>
          </div>
          <label class="filter-row" for="changedKindFilter">
            <span>Фильтр</span>
            <select id="changedKindFilter">
              <option value="All">Все</option>
              <option value="BuildFix">Исправление сборки</option>
              <option value="BugFix">Исправление ошибки</option>
              <option value="Validation">Проверка</option>
              <option value="FeatureAdd">Добавление функции</option>
              <option value="Refactor">Рефакторинг</option>
              <option value="Update">Обновление</option>
              <option value="Unknown">Неизвестно</option>
            </select>
          </label>
          <ul id="changedFiles"></ul>
          <section id="diagnosticsSection" class="run-section">
            <div class="details-title">Сборка / Диагностика</div>
            <div id="buildStatus" class="build-status na">Сборка: не запускалась</div>
            <div id="diagnosticsSummary" class="diagnostics-summary"></div>
            <ul id="diagnosticsList" class="diagnostics-list"></ul>
            <div id="diagnosticsEmpty" class="empty-state">Нет распознанных диагностик</div>
          </section>
        </section>

        <section class="logs-section" style="display: none;">
          <div class="logs-header-row">
            <h3 id="logsHeader">Логи (агент + приложение)</h3>
          </div>
          <div id="logs"></div>
        </section>
      </main>

      <footer class="composer">
        <textarea id="task" placeholder="Опишите, что нужно создать или изменить"></textarea>
        <div class="composer-actions">
          <button id="copyResult" class="secondary-button" type="button">Копировать результат</button>
          <button id="stop" class="secondary-button" type="button" disabled>Стоп</button>
          <button id="send" class="send-button" type="button" title="Отправить">Отправить</button>
        </div>
        <label class="model-selector-row composer-model-row" for="modelSelector">
          <span>Model</span>
          <select id="modelSelector"></select>
          <span id="modelPing" class="model-selector-ping">Пинг: нет данных</span>
        </label>
        <div id="modelSelectionToast" class="model-selection-toast" aria-live="polite"></div>
        <div id="modelSelectionStatus" class="model-selection-status" aria-live="polite"></div>
        <div class="composer-status">
          <span class="composer-status-item">Версия: ${version}</span>
        </div>
      </footer>
    </div>`;

function getWebviewBody() {
  return webviewBody;
}

module.exports = { getWebviewBody };
