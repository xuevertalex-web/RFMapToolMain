const { getWebviewClientState } = require('./webviewClientState');
const { getWebviewClientRunNormalization } = require('./webviewClientRunNormalization');
const { getWebviewClientActions } = require('./webviewClientActions');
const { getWebviewClientRendering } = require('./webviewClientRendering');
const { getWebviewClientEvents } = require('./webviewClientEvents');

function getWebviewClient() {
  return [
    getWebviewClientRunNormalization(),
    getWebviewClientState(),
    getWebviewClientActions(),
    getWebviewClientRendering(),
    getWebviewClientEvents()
  ].join('\n\n');
}

module.exports = { getWebviewClient };
