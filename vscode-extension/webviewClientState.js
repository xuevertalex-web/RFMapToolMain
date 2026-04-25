const { getWebviewClientBootstrap } = require('./webviewClientBootstrap');
const { getWebviewClientPersistence } = require('./webviewClientPersistence');
const { getWebviewClientRecentRunsState } = require('./webviewClientRecentRunsState');
const { getWebviewClientRunState } = require('./webviewClientRunState');

function getWebviewClientState() {
  return [
    getWebviewClientBootstrap(),
    getWebviewClientPersistence(),
    getWebviewClientRecentRunsState(),
    getWebviewClientRunState()
  ].join('\n\n');
}

module.exports = { getWebviewClientState };
