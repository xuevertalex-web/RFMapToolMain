const { getWebviewClientRecentRuns } = require('./webviewClientRecentRuns');
const { getWebviewClientChangedFiles } = require('./webviewClientChangedFiles');
const { getWebviewClientUiHelpers } = require('./webviewClientUiHelpers');
const { getWebviewClientStatusRenderer } = require('./webviewClientStatusRenderer');
const { getWebviewClientSummaryRenderer } = require('./webviewClientSummaryRenderer');
const { getWebviewClientFailureRenderer } = require('./webviewClientFailureRenderer');
const { getWebviewClientTimelineRenderer } = require('./webviewClientTimelineRenderer');
const { getWebviewClientDiagnosticsRenderer } = require('./webviewClientDiagnosticsRenderer');
const { getWebviewClientRunActionsRenderer } = require('./webviewClientRunActionsRenderer');
const { getWebviewClientModelStatusRenderer } = require('./webviewClientModelStatusRenderer');

function getWebviewClientRendering() {
  return [
    getWebviewClientRecentRuns(),
    getWebviewClientChangedFiles(),
    getWebviewClientUiHelpers(),
    getWebviewClientStatusRenderer(),
    getWebviewClientSummaryRenderer(),
    getWebviewClientFailureRenderer(),
    getWebviewClientTimelineRenderer(),
    getWebviewClientDiagnosticsRenderer(),
    getWebviewClientModelStatusRenderer(),
    getWebviewClientRunActionsRenderer()
  ].join('\n\n');
}

module.exports = { getWebviewClientRendering };
