const webviewClientRecentRuns = `function openDialogsListView() {
        dialogViewMode = 'list';
        selectedDialogId = '';
        if (sessionsStrip) sessionsStrip.style.display = 'block';
        if (chatScroll) chatScroll.style.display = 'none';
        if (composer) composer.style.display = 'none';
        if (backToDialogsButton) backToDialogsButton.style.display = 'none';
        if (dialogTitle) dialogTitle.textContent = 'Dialogs';
        saveWebviewState();
      }

      function openDialogView(run) {
        if (!run) return;
        dialogViewMode = 'detail';
        selectedDialogId = String(run.id || '');
        restoreRecentRunSession(run);
        if (sessionsStrip) sessionsStrip.style.display = 'none';
        if (chatScroll) chatScroll.style.display = 'block';
        if (composer) composer.style.display = 'block';
        if (backToDialogsButton) backToDialogsButton.style.display = 'inline-block';
        if (dialogTitle) dialogTitle.textContent = truncateRecentRunTask(run.task);
        saveWebviewState();
      }

      function openEmptyDialogView() {
        const run = createDialogSession('');
        openDialogView(run);
      }

      function syncDialogViewFromState() {
        if (dialogViewMode !== 'detail') {
          openDialogsListView();
          return;
        }

        const allRuns = ([]).concat(Array.isArray(recentRuns) ? recentRuns : [], Array.isArray(archivedRuns) ? archivedRuns : []);
        const target = allRuns.find(run => String(run.id || '') === String(selectedDialogId || ''));
        if (!target) {
          openDialogsListView();
          return;
        }
        openDialogView(target);
      }

      function getRunPreviewText(run) {
        const messages = Array.isArray(run && run.messages) ? run.messages : [];
        for (let i = messages.length - 1; i >= 0; i--) {
          const message = messages[i];
          if (message && message.role === 'assistant') {
            const text = String(message.text || '').trim();
            if (!text) {
              continue;
            }
            if (text.length <= 72) {
              return text;
            }
            return text.slice(0, 69).trimEnd() + '...';
          }
        }
        return 'No agent response yet';
      }

      function renderRecentRuns() {
        if (!recentRunsContainer) return;
        if (sessionsTabActive) sessionsTabActive.classList.toggle('active', sessionsViewMode !== 'archived');
        if (sessionsTabArchived) sessionsTabArchived.classList.toggle('active', sessionsViewMode === 'archived');

        recentRunsContainer.replaceChildren();
        const source = sessionsViewMode === 'archived' ? archivedRuns : recentRuns;
        if (!Array.isArray(source) || source.length === 0) {
          const empty = document.createElement('div');
          empty.style.opacity = '0.75';
          empty.textContent = sessionsViewMode === 'archived' ? 'No archived dialogs' : 'No dialogs yet';
          recentRunsContainer.appendChild(empty);
          return;
        }

        for (const run of source) {
          const item = document.createElement('div');
          item.className = 'recent-run-card';

          const topRow = document.createElement('div');
          topRow.className = 'recent-run-top';

          const leftRow = document.createElement('div');
          leftRow.className = 'recent-run-left';

          const time = document.createElement('span');
          time.className = 'recent-run-time';
          time.textContent = run.timestamp;
          leftRow.appendChild(time);

          const badge = document.createElement('span');
          badge.className = 'result-badge ' + getRecentRunBadgeClass(run.ok);
          badge.textContent = run.ok ? 'OK' : 'Error';
          leftRow.appendChild(badge);
          topRow.appendChild(leftRow);

          const actions = document.createElement('div');
          actions.className = 'recent-run-actions';

          if (sessionsViewMode !== 'archived') {
            const archiveButton = document.createElement('button');
            archiveButton.type = 'button';
            archiveButton.textContent = 'Archive';
            archiveButton.className = 'recent-run-action';
            archiveButton.onclick = event => {
              event.stopPropagation();
              archiveRecentRunById(run.id, run.task, run.timestamp);
            };
            actions.appendChild(archiveButton);
          } else {
            const restoreButton = document.createElement('button');
            restoreButton.type = 'button';
            restoreButton.textContent = 'Restore';
            restoreButton.className = 'recent-run-action';
            restoreButton.onclick = event => {
              event.stopPropagation();
              restoreArchivedRunById(run.id, run.task, run.timestamp);
            };
            actions.appendChild(restoreButton);
          }

          const deleteButton = document.createElement('button');
          deleteButton.type = 'button';
          deleteButton.textContent = 'Delete';
          deleteButton.className = 'recent-run-action';
          deleteButton.onclick = event => {
            event.stopPropagation();
            deleteRunById(run.id, run.task, run.timestamp);
          };
          actions.appendChild(deleteButton);

          topRow.appendChild(actions);
          item.appendChild(topRow);

          const task = document.createElement('div');
          task.className = 'recent-run-task';
          task.textContent = truncateRecentRunTask(run.task);
          item.appendChild(task);

          const preview = document.createElement('div');
          preview.className = 'recent-run-preview';
          preview.textContent = getRunPreviewText(run);
          item.appendChild(preview);

          item.addEventListener('click', () => openDialogView(run));
          recentRunsContainer.appendChild(item);
        }
      }

      renderRecentRuns();
      syncDialogViewFromState();`;

function getWebviewClientRecentRuns() {
  return webviewClientRecentRuns;
}

module.exports = { getWebviewClientRecentRuns };
