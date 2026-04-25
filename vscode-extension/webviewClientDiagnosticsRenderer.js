const webviewClientDiagnosticsRenderer = `function renderBuildDiagnostics(run) {
        if (!diagnosticsSection || !diagnosticsList || !diagnosticsEmpty) return;
        buildStatus.textContent = 'Build: ' + run.buildText;
        buildStatus.className = run.buildSucceeded === true
          ? 'build-status ok'
          : run.buildSucceeded === false
            ? 'build-status fail'
            : 'build-status na';
        diagnosticsList.replaceChildren();
        diagnosticsSummary.textContent = Array.isArray(run.diagnostics) && run.diagnostics.length
          ? 'Diagnostics: ' + run.diagnostics.length
          : 'Diagnostics: not available';
        if (!Array.isArray(run.diagnostics) || run.diagnostics.length === 0) {
          diagnosticsEmpty.style.display = 'block';
          return;
        }
        diagnosticsEmpty.style.display = 'none';
        for (const diagnostic of run.diagnostics) {
          const item = document.createElement('li');
          item.className = 'diagnostic-item ' + String(diagnostic.severity || '').toLowerCase();
          item.textContent = [
            diagnostic.severity,
            diagnostic.code,
            diagnostic.message,
            diagnostic.file ? diagnostic.file + (diagnostic.line ? ':' + diagnostic.line : '') : ''
          ].filter(Boolean).join(' · ');
          if (diagnostic.file) {
            item.style.cursor = 'pointer';
            item.addEventListener('click', () => {
              vscode.postMessage({
                type: 'openFile',
                path: diagnostic.file,
                startLine: diagnostic.line || undefined,
                endLine: diagnostic.line || undefined
              });
            });
          }
          diagnosticsList.appendChild(item);
        }
      }`;

function getWebviewClientDiagnosticsRenderer() {
  return webviewClientDiagnosticsRenderer;
}

module.exports = { getWebviewClientDiagnosticsRenderer };
