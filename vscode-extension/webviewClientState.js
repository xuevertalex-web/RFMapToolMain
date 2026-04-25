const { getWebviewClientBootstrap } = require('./webviewClientBootstrap');
const { getWebviewClientPersistence } = require('./webviewClientPersistence');
const { getWebviewClientRecentRunsState } = require('./webviewClientRecentRunsState');
const { getWebviewClientRunState } = require('./webviewClientRunState');
const { getWebviewClientModelSelector } = require('./webviewClientModelSelector');

function getWebviewClientState() {
  return [
    getWebviewClientBootstrap(),
    getWebviewClientModelSelector(),
    getWebviewClientPersistence(),
    getWebviewClientRecentRunsState(),
    getWebviewClientRunState()
  ].join('\n\n');
}

module.exports = { getWebviewClientState };
