const webviewClientBootstrap = `const vscode = acquireVsCodeApi();
      const taskInput = document.getElementById('task');
      const logs = document.getElementById('logs');
      const result = document.getElementById('result');
      const resultBadge = document.getElementById('resultBadge');
      const copyResultButton = document.getElementById('copyResult');
      const clearOutputButton = document.getElementById('clearOutput');
      const rerunLastButton = document.getElementById('rerunLast');
      const exportRunReportButton = document.getElementById('exportRunReport');
      const structuredResultSection = document.getElementById('structuredResultSection');
      const runStatusGrid = document.getElementById('runStatusGrid');
      const failureSection = document.getElementById('failureSection');
      const failureSummary = document.getElementById('failureSummary');
      const timelineList = document.getElementById('timelineList');
      const timelineEmpty = document.getElementById('timelineEmpty');
      const diagnosticsSection = document.getElementById('diagnosticsSection');
      const diagnosticsSummary = document.getElementById('diagnosticsSummary');
      const diagnosticsList = document.getElementById('diagnosticsList');
      const diagnosticsEmpty = document.getElementById('diagnosticsEmpty');
      const summary = document.getElementById('summary');
      const changedFilesTitle = document.getElementById('changedFilesTitle');
      const copyStructuredResultButton = document.getElementById('copyStructuredResult');
      const copyChangedFilesButton = document.getElementById('copyChangedFiles');
      const exportChangedFilesButton = document.getElementById('exportChangedFiles');
      const openAllChangedFilesButton = document.getElementById('openAllChangedFiles');
      const runStats = document.getElementById('runStats');
      const changedFiles = document.getElementById('changedFiles');
      const changedKindFilter = document.getElementById('changedKindFilter');
      const buildStatus = document.getElementById('buildStatus');
      const logsHeader = document.getElementById('logsHeader');
      const logsToggle = document.getElementById('logsToggle');
      const copyLogsButton = document.getElementById('copyLogs');
      const exportLogsButton = document.getElementById('exportLogs');
      const helpButton = document.getElementById('helpButton');
      const modelName = document.getElementById('modelName');
      const modelPing = document.getElementById('modelPing');
      const recentRunsContainer = document.getElementById('recentRuns');
      const status = document.getElementById('status');
      const thinkingIndicator = document.getElementById('thinkingIndicator');
      const sendButton = document.getElementById('send');
      const stopButton = document.getElementById('stop');
      let uiRunning = false;
      let logsCollapsed = false;
      let currentChangedKindFilter = 'All';
      let currentChangedFiles = [];
      let currentChangedHints = [];
      let currentChangedRanges = [];
      let currentKindMap = new Map();
      let currentChangedRangeMap = new Map();
      let currentChangedHintMap = new Map();
      let recentRuns = [];
      let suppressPlainResultLog = false;
      let currentRunTask = '';
      let lastResultPayload = {
        resultText: '',
        summaryText: '',
        buildText: '',
        changedFiles: []
      };
      const webviewStateVersion = 3;
      const webviewState = vscode.getState() || {};
      if (webviewState && typeof webviewState.taskInputValue === 'string') {
        taskInput.value = webviewState.taskInputValue;
      }
      if (webviewState && typeof webviewState.changedKindFilterValue === 'string') {
        currentChangedKindFilter = webviewState.changedKindFilterValue;
        changedKindFilter.value = currentChangedKindFilter;
      }
      if (webviewState && typeof webviewState.logsCollapsed === 'boolean') {
        logsCollapsed = !!webviewState.logsCollapsed;
      }
      // Do not restore prior run output from VS Code webview state. Old results can
      // survive extension reinstalls and look like a fresh agent response.
      recentRuns = [];`;

function getWebviewClientBootstrap() {
  return webviewClientBootstrap;
}

module.exports = { getWebviewClientBootstrap };
