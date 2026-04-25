const webviewClientRecentRuns = `function renderRecentRuns() {
        if (!recentRunsContainer) {
          return;
        }

        recentRunsContainer.replaceChildren();

        if (!Array.isArray(recentRuns) || recentRuns.length === 0) {
          const empty = document.createElement('div');
          empty.style.opacity = '0.75';
          empty.textContent = 'No recent runs';
          recentRunsContainer.appendChild(empty);
          return;
        }

        for (const run of recentRuns) {
          const item = document.createElement('div');
          item.style.padding = '6px 8px';
          item.style.marginBottom = '6px';
          item.style.border = '1px solid var(--vscode-input-border, transparent)';
          item.style.borderRadius = '6px';
          item.style.cursor = 'pointer';
          item.style.background = 'var(--vscode-input-background)';

          const topRow = document.createElement('div');
          topRow.style.display = 'flex';
          topRow.style.alignItems = 'center';
          topRow.style.gap = '6px';
          topRow.style.flexWrap = 'wrap';
          topRow.style.fontSize = '0.9em';
          topRow.style.justifyContent = 'space-between';

          const leftRow = document.createElement('div');
          leftRow.style.display = 'flex';
          leftRow.style.alignItems = 'center';
          leftRow.style.gap = '6px';
          leftRow.style.flexWrap = 'wrap';

          const time = document.createElement('span');
          time.style.opacity = '0.75';
          time.textContent = run.timestamp;
          leftRow.appendChild(time);

          const badge = document.createElement('span');
          badge.className = 'result-badge ' + getRecentRunBadgeClass(run.ok);
          badge.textContent = run.ok ? 'OK' : 'Error';
          leftRow.appendChild(badge);

          topRow.appendChild(leftRow);

          const rerunButton = document.createElement('button');
          rerunButton.type = 'button';
          rerunButton.style.width = 'auto';
          rerunButton.style.padding = '2px 6px';
          rerunButton.style.fontSize = '0.8em';
          rerunButton.textContent = 'Rerun';
          const canRerun = canRerunRecentTask(run);
          rerunButton.disabled = !canRerun;
          rerunButton.style.opacity = canRerun ? '1' : '0.55';
          rerunButton.style.cursor = canRerun ? 'pointer' : 'not-allowed';
          rerunButton.addEventListener('click', event => {
            event.stopPropagation();
            const rerunTask = String(run.task || '').trim();
            if (!canRerunRecentTask(run) || !rerunTask || rerunTask === '(no task)') {
              return;
            }

            taskInput.value = rerunTask;
            autoResizeTaskInput();
            saveWebviewState();
            taskInput.focus();
            startAgentRunFromInput();
          });
          topRow.appendChild(rerunButton);

          item.appendChild(topRow);

          const task = document.createElement('div');
          task.style.marginTop = '3px';
          task.style.fontWeight = '600';
          task.style.fontSize = '0.92em';
          task.style.lineHeight = '1.3';
          task.textContent = truncateRecentRunTask(run.task);
          item.appendChild(task);

          const metaLine = document.createElement('div');
          metaLine.style.marginTop = '2px';
          metaLine.style.fontSize = '0.82em';
          metaLine.style.opacity = '0.75';
          metaLine.textContent = 'changed: ' + (Number.isFinite(run.changedCount) ? run.changedCount : 0);
          item.appendChild(metaLine);

          item.addEventListener('click', () => {
            taskInput.value = run.task;
            autoResizeTaskInput();
            saveWebviewState();
            taskInput.focus();
          });

          recentRunsContainer.appendChild(item);
        }
      }

      renderRecentRuns();`;

function getWebviewClientRecentRuns() {
  return webviewClientRecentRuns;
}

module.exports = { getWebviewClientRecentRuns };
