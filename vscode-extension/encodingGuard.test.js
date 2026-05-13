const fs = require('fs');
const path = require('path');
const assert = require('assert');

const ROOT = path.resolve(__dirname, '..');
const ALLOWED = new Set(['.cs','.js', '.json', '.md', '.ts', '.css', '.html', '.txt', '.cmd', '.ps1', '.yml', '.yaml']);
const SKIP_DIR = new Set(['node_modules', '.git', '.vscode', 'assets', 'resources']);
const SKIP_FILE = new Set(['local-cursor-agent-0.1.105.vsix', 'encodingGuard.test.js']);

function walk(dir, out) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (SKIP_DIR.has(entry.name)) continue;
      walk(path.join(dir, entry.name), out);
      continue;
    }
    const abs = path.join(dir, entry.name);
    if (SKIP_FILE.has(entry.name)) continue;
    if (!ALLOWED.has(path.extname(entry.name).toLowerCase())) continue;
    out.push(abs);
  }
}

function hasUtf8Bom(buf) {
  return buf.length >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf;
}

function hasMojibake(text) {
  const markers = ['Р С•', 'Р С‘', 'Р В°', 'Р Вµ', 'РЎвЂљ', 'РЎРЉ', 'РЎРЏ', 'РЎС“', 'РЎв‚¬', 'РЎвЂЎ', 'РЎвЂ№'];
  let hits = 0;
  for (const marker of markers) {
    if (text.includes(marker)) hits += 1;
    if (hits >= 2) return true;
  }
  return false;
}

function run() {
  const files = [];
  walk(ROOT, files);
  const failures = [];

  for (const file of files) {
    const rel = path.relative(ROOT, file);
    const buf = fs.readFileSync(file);
    if (hasUtf8Bom(buf)) {
      failures.push(`${rel}: UTF-8 BOM is not allowed`);
      continue;
    }
    const text = buf.toString('utf8');
    if (text.includes('\uFFFD')) {
      failures.push(`${rel}: contains replacement character U+FFFD`);
    }
    if (hasMojibake(text)) {
      failures.push(`${rel}: contains mojibake-like Cyrillic sequence`);
    }
  }

  assert.strictEqual(failures.length, 0, `Encoding guard failed:\n${failures.join('\n')}`);
  console.log('encoding guard tests passed');
}

run();

