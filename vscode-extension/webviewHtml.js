const { getWebviewStyles } = require('./webviewStyles');
const { getWebviewBody } = require('./webviewBody');
const { getWebviewClient } = require('./webviewClient');

function getHtml() {
  return `<!DOCTYPE html>
  <html lang="en">
  <head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <style>${getWebviewStyles()}</style>
  </head>
  <body>
    ${getWebviewBody()}
    <script>
${getWebviewClient()}
    </script>
  </body>
  </html>`;
}

module.exports = { getHtml };
