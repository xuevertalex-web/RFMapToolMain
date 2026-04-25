const { app, BrowserWindow, Menu, dialog, ipcMain, shell } = require('electron');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');

let mainWindow = null;
let currentAgentProcess = null;
let currentTerminalProcess = null;
let storageRoot = null;
const workspaceStateCache = new Map();
const workspaceTreeCache = new Map();
let cachedHealthStatus = null;
let cachedHealthTimestamp = 0;
let cachedLiveStatus = null;
let cachedLiveStatusKey = '';

app.setName('Aelivar');
app.setAppUserModelId('Aelivar');

function terminateChildProcessTree(child) {
  if (!child || !child.pid) {
    return Promise.resolve(false);
  }

  return new Promise(resolve => {
    const killer = spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], {
      shell: false,
      windowsHide: true
    });

    killer.on('error', () => {
      try {
        child.kill('SIGKILL');
        resolve(true);
      } catch {
        resolve(false);
      }
    });

    killer.on('close', code => {
      resolve(code === 0);
    });
  });
}

async function stopAgentProcess() {
  if (!currentAgentProcess) {
    return false;
  }

  const child = currentAgentProcess;
  currentAgentProcess = null;
  return await terminateChildProcessTree(child);
}

function ensureStorageRoot() {
  if (!storageRoot) {
    storageRoot = path.join(app.getPath('userData'), 'workspace-state');
    fs.mkdirSync(storageRoot, { recursive: true });
  }

  return storageRoot;
}

function makeWorkspaceStoragePath(workspaceRoot) {
  const normalized = String(workspaceRoot || '').trim().toLowerCase();
  const safeName = Buffer.from(normalized, 'utf8').toString('hex');
  return path.join(ensureStorageRoot(), `${safeName}.json`);
}

function loadWorkspaceState(workspaceRoot) {
  if (!workspaceRoot) {
    return null;
  }

  const filePath = makeWorkspaceStoragePath(workspaceRoot);
  if (!fs.existsSync(filePath)) {
    return null;
  }

  try {
    const raw = fs.readFileSync(filePath, 'utf8');
    workspaceStateCache.set(filePath, raw);
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function saveWorkspaceState(workspaceRoot, state) {
  if (!workspaceRoot) {
    return false;
  }

  const filePath = makeWorkspaceStoragePath(workspaceRoot);
  const serialized = JSON.stringify(state, null, 2);
  if (workspaceStateCache.get(filePath) === serialized) {
    return true;
  }

  fs.writeFileSync(filePath, serialized, 'utf8');
  workspaceStateCache.set(filePath, serialized);
  return true;
}

function getAgentRepoRoot() {
  return path.resolve(__dirname, '..');
}

function getAgentRuntimeRoot() {
  return path.join(getAgentRepoRoot(), '.agent-runtime');
}

function safeReadJson(filePath) {
  if (!fs.existsSync(filePath)) {
    return null;
  }

  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch {
    return null;
  }
}

function readTailLines(filePath, maxLines = 40) {
  if (!fs.existsSync(filePath)) {
    return [];
  }

  try {
    const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/).filter(Boolean);
    return lines.slice(-maxLines);
  } catch {
    return [];
  }
}

function sendMenuAction(action) {
  if (!mainWindow || mainWindow.isDestroyed()) {
    return;
  }

  mainWindow.webContents.send('menu:action', { action });
}

function buildAppMenu() {
  const template = [
    {
      label: 'Файл',
      submenu: [
        { label: 'Новая беседа', accelerator: 'Ctrl+N', click: () => sendMenuAction('new-session') },
        { label: 'Открыть проект...', accelerator: 'Ctrl+O', click: () => sendMenuAction('open-workspace') },
        { label: 'Добавить файлы в проект...', accelerator: 'Ctrl+Shift+O', click: () => sendMenuAction('import-files') },
        { label: 'Открыть файл...', accelerator: 'Ctrl+P', click: () => sendMenuAction('open-preview-file') },
        { type: 'separator' },
        { label: 'Обновить файлы', accelerator: 'F5', click: () => sendMenuAction('refresh-explorer') },
        { type: 'separator' },
        { role: 'close', label: 'Закрыть окно' },
        { role: 'quit', label: 'Выход' }
      ]
    },
    {
      label: 'Правка',
      submenu: [
        { role: 'undo', label: 'Отменить' },
        { role: 'redo', label: 'Повторить' },
        { type: 'separator' },
        { role: 'cut', label: 'Вырезать' },
        { role: 'copy', label: 'Копировать' },
        { role: 'paste', label: 'Вставить' },
        { role: 'delete', label: 'Удалить' },
        { type: 'separator' },
        { role: 'selectAll', label: 'Выделить всё' }
      ]
    },
    {
      label: 'Вид',
      submenu: [
        { role: 'reload', label: 'Перезагрузить' },
        { role: 'forceReload', label: 'Принудительно перезагрузить' },
        { type: 'separator' },
        { role: 'resetZoom', label: 'Сбросить масштаб' },
        { role: 'zoomIn', label: 'Увеличить масштаб' },
        { role: 'zoomOut', label: 'Уменьшить масштаб' },
        { type: 'separator' },
        { role: 'togglefullscreen', label: 'Полноэкранный режим' }
      ]
    },
    {
      label: 'Окно',
      submenu: [
        { role: 'minimize', label: 'Свернуть' },
        { role: 'zoom', label: 'Масштаб окна' },
        { role: 'close', label: 'Закрыть окно' }
      ]
    },
    {
      label: 'Справка',
      submenu: [
        {
          label: 'О программе Aelivar',
          click: async () => {
            await dialog.showMessageBox(mainWindow, {
              type: 'info',
              title: 'О программе Aelivar',
              message: 'Aelivar',
              detail: 'Настольный интерфейс агента для работы с проектом.'
            });
          }
        }
      ]
    }
  ];

  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1440,
    height: 960,
    minWidth: 1100,
    minHeight: 760,
    backgroundColor: '#101418',
    title: 'Aelivar',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false
    }
  });

  mainWindow.on('closed', () => {
    void stopAgentProcess();
    mainWindow = null;
  });

  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));
  wireContextMenu(mainWindow);
}

app.whenReady().then(() => {
  buildAppMenu();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  void stopAgentProcess();
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('before-quit', () => {
  void stopAgentProcess();
});

ipcMain.handle('dialog:openFolder', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openDirectory']
  });

  if (result.canceled || !result.filePaths || result.filePaths.length === 0) {
    return null;
  }

  return result.filePaths[0];
});

ipcMain.handle('dialog:openFiles', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile', 'multiSelections']
  });

  if (result.canceled || !result.filePaths || result.filePaths.length === 0) {
    return [];
  }

  return result.filePaths;
});

ipcMain.handle('storage:loadWorkspaceState', async (_event, workspaceRoot) => {
  return loadWorkspaceState(String(workspaceRoot || '').trim());
});

ipcMain.handle('storage:saveWorkspaceState', async (_event, payload) => {
  const workspaceRoot = String(payload?.workspaceRoot || '').trim();
  const state = payload?.state ?? null;
  return saveWorkspaceState(workspaceRoot, state);
});

ipcMain.handle('attachments:prepare', async (_event, payload) => {
  const files = Array.isArray(payload?.files) ? payload.files : [];
  const maxInlineBytes = 64 * 1024;
  const prepared = [];

  for (const filePath of files) {
    if (!filePath || !fs.existsSync(filePath)) {
      continue;
    }

    const stats = fs.statSync(filePath);
    if (!stats.isFile()) {
      continue;
    }

    const ext = path.extname(filePath).toLowerCase();
    const isTextLike = [
      '.txt', '.md', '.json', '.js', '.ts', '.tsx', '.jsx', '.css', '.html', '.xml', '.yml', '.yaml',
      '.cs', '.csproj', '.sln', '.py', '.java', '.cpp', '.c', '.h', '.hpp', '.rs', '.go', '.php', '.rb',
      '.sql', '.sh', '.ps1', '.bat', '.cmd', '.ini', '.toml', '.log'
    ].includes(ext);

    let inlineContent = null;
    let truncated = false;

    if (isTextLike) {
      const bytesToRead = Math.min(stats.size, maxInlineBytes);
      inlineContent = fs.readFileSync(filePath, 'utf8');
      if (Buffer.byteLength(inlineContent, 'utf8') > bytesToRead) {
        inlineContent = inlineContent.slice(0, bytesToRead);
        truncated = true;
      } else {
        truncated = stats.size > maxInlineBytes;
      }
    }

    prepared.push({
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      name: path.basename(filePath),
      path: filePath,
      size: stats.size,
      isTextLike,
      inlineContent,
      truncated
    });
  }

  return prepared;
});

ipcMain.handle('workspace:listFiles', async (_event, workspaceRoot) => {
  if (!workspaceRoot || !fs.existsSync(workspaceRoot)) {
    return [];
  }

  const cachedTree = workspaceTreeCache.get(workspaceRoot);
  if (cachedTree && Date.now() - cachedTree.timestamp < 500) {
    return cachedTree.value;
  }

  function walk(dir, root) {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    const items = [];
    for (const entry of entries) {
      if (entry.name === '.git' || entry.name === 'bin' || entry.name === 'obj' || entry.name === 'node_modules') {
        continue;
      }

      const fullPath = path.join(dir, entry.name);
      const relativePath = path.relative(root, fullPath);
      if (entry.isDirectory()) {
        items.push({
          name: entry.name,
          path: fullPath,
          relativePath,
          kind: 'directory',
          children: walk(fullPath, root)
        });
      } else {
        items.push({
          name: entry.name,
          path: fullPath,
          relativePath,
          kind: 'file'
        });
      }
    }

    return items.sort((a, b) => {
      if (a.kind !== b.kind) {
        return a.kind === 'directory' ? -1 : 1;
      }
      return a.name.localeCompare(b.name);
    });
  }

  const value = walk(workspaceRoot, workspaceRoot);
  workspaceTreeCache.set(workspaceRoot, {
    timestamp: Date.now(),
    value
  });
  return value;
});

ipcMain.handle('workspace:readFile', async (_event, filePath) => {
  if (!filePath || !fs.existsSync(filePath)) {
    return '';
  }

  const stats = fs.statSync(filePath);
  const maxPreviewBytes = 256 * 1024;
  const bytesToRead = Math.min(stats.size, maxPreviewBytes);
  const buffer = Buffer.alloc(bytesToRead);
  const fd = fs.openSync(filePath, 'r');

  try {
    fs.readSync(fd, buffer, 0, bytesToRead, 0);
  } finally {
    fs.closeSync(fd);
  }

  const content = buffer.toString('utf8');
  if (stats.size <= maxPreviewBytes) {
    return content;
  }

  return `${content}\n\n[preview truncated: ${stats.size} bytes total]`;
});

ipcMain.handle('workspace:importFiles', async (_event, payload) => {
  const workspaceRoot = String(payload.workspaceRoot || '').trim();
  const files = Array.isArray(payload.files) ? payload.files : [];

  if (!workspaceRoot || !fs.existsSync(workspaceRoot)) {
    throw new Error('Workspace is not selected.');
  }

  const imported = [];
  for (const filePath of files) {
    if (!filePath || !fs.existsSync(filePath)) {
      continue;
    }

    const parsed = path.parse(filePath);
    let targetPath = path.join(workspaceRoot, parsed.base);
    let counter = 1;

    while (fs.existsSync(targetPath)) {
      targetPath = path.join(workspaceRoot, `${parsed.name}-${counter}${parsed.ext}`);
      counter += 1;
    }

    fs.copyFileSync(filePath, targetPath);
    imported.push(targetPath);
  }

  workspaceTreeCache.delete(workspaceRoot);
  return imported;
});

ipcMain.handle('shell:openPath', async (_event, targetPath) => {
  if (!targetPath) {
    return false;
  }

  await shell.openPath(String(targetPath));
  return true;
});

ipcMain.handle('app:getZoom', async () => {
  if (!mainWindow || mainWindow.isDestroyed()) {
    return 1;
  }

  return mainWindow.webContents.getZoomFactor();
});

ipcMain.handle('app:setZoom', async (_event, factor) => {
  if (!mainWindow || mainWindow.isDestroyed()) {
    return 1;
  }

  const nextFactor = Math.min(Math.max(Number(factor) || 1, 0.75), 1.75);
  mainWindow.webContents.setZoomFactor(nextFactor);
  return nextFactor;
});

ipcMain.handle('health:check', async (_event, payload) => {
  const endpoint = String(payload?.endpoint || process.env.OLLAMA_ENDPOINT || 'http://localhost:11434').trim();
  const llmModel = String(payload?.llmModel || process.env.OLLAMA_MODEL || '').trim();
  const embeddingModel = String(payload?.embeddingModel || 'nomic-embed-text').trim();
  const cacheKey = `${endpoint}|${llmModel}|${embeddingModel}`;

  if (cachedHealthStatus && cachedHealthStatus.cacheKey === cacheKey && Date.now() - cachedHealthTimestamp < 5000) {
    return cachedHealthStatus.value;
  }

  const startedAt = new Date().toISOString();

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 4000);
    const response = await fetch(`${endpoint}/api/tags`, { signal: controller.signal });
    clearTimeout(timeout);

    if (!response.ok) {
      const value = {
        ok: false,
        endpoint,
        startedAt,
        llm: {
          status: 'failed',
          message: `HTTP ${response.status}`,
          model: llmModel || null
        },
        embeddings: {
          status: 'failed',
          message: `HTTP ${response.status}`,
          model: embeddingModel || null
        }
      };
      cachedHealthStatus = { cacheKey, value };
      cachedHealthTimestamp = Date.now();
      return value;
    }

    const data = await response.json();
    const models = Array.isArray(data?.models) ? data.models : [];
    const modelNames = models.map(model => String(model?.name || '')).filter(Boolean);

    const llmStatus = !llmModel
      ? { status: 'ready', message: 'Сервис доступен', model: null }
      : modelNames.includes(llmModel)
        ? { status: 'ready', message: 'Модель доступна', model: llmModel }
        : { status: 'warning', message: 'Модель не найдена в Ollama', model: llmModel };

    const embeddingStatus = !embeddingModel
      ? { status: 'ready', message: 'Сервис доступен', model: null }
      : modelNames.includes(embeddingModel)
        ? { status: 'ready', message: 'Embedding-модель доступна', model: embeddingModel }
        : { status: 'warning', message: 'Embedding-модель не найдена', model: embeddingModel };

    const value = {
      ok: true,
      endpoint,
      startedAt,
      llm: !llmModel
        ? { status: 'warning', message: 'LLM-модель не выбрана', model: null }
        : llmStatus,
      embeddings: embeddingStatus,
      models: modelNames.slice(0, 64)
    };
    cachedHealthStatus = { cacheKey, value };
    cachedHealthTimestamp = Date.now();
    return value;
  } catch (error) {
    const value = {
      ok: false,
      endpoint,
      startedAt,
      llm: {
        status: 'failed',
        message: String(error?.message || error),
        model: llmModel || null
      },
      embeddings: {
        status: 'failed',
        message: String(error?.message || error),
        model: embeddingModel || null
      }
    };
    cachedHealthStatus = { cacheKey, value };
    cachedHealthTimestamp = Date.now();
    return value;
  }
});

ipcMain.handle('ollama:listModels', async (_event, payload) => {
  const endpoint = String(payload?.endpoint || process.env.OLLAMA_ENDPOINT || 'http://localhost:11434').trim();

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 4000);
    const response = await fetch(`${endpoint}/api/tags`, { signal: controller.signal });
    clearTimeout(timeout);

    if (!response.ok) {
      return [];
    }

    const data = await response.json();
    const models = Array.isArray(data?.models) ? data.models : [];
    return models.map(model => String(model?.name || '')).filter(Boolean);
  } catch {
    return [];
  }
});

ipcMain.handle('agent:getLiveStatus', async () => {
  const runtimeRoot = getAgentRuntimeRoot();
  const machineRunsRoot = path.join(runtimeRoot, 'logs', 'machine', 'runs');
  const humanRoot = path.join(runtimeRoot, 'logs', 'human');
  const manifestPath = path.join(machineRunsRoot, 'latest_manifest.json');
  const manifestStat = fs.existsSync(manifestPath) ? fs.statSync(manifestPath) : null;
  const manifest = safeReadJson(manifestPath);
  const eventStreamPath = manifest?.EventStreamFile
    ? path.join(machineRunsRoot, String(manifest.EventStreamFile))
    : null;
  const eventStat = eventStreamPath && fs.existsSync(eventStreamPath) ? fs.statSync(eventStreamPath) : null;
  const timelinePath = path.join(humanRoot, 'latest_timeline.log');
  const timelineStat = fs.existsSync(timelinePath) ? fs.statSync(timelinePath) : null;
  const cacheKey = [
    manifestPath,
    manifestStat?.mtimeMs ?? 0,
    eventStreamPath ?? '',
    eventStat?.mtimeMs ?? 0,
    timelineStat?.mtimeMs ?? 0
  ].join('|');

  if (cachedLiveStatus && cachedLiveStatusKey === cacheKey) {
    return cachedLiveStatus;
  }

  const eventLines = eventStreamPath ? readTailLines(eventStreamPath, 24) : [];
  const events = eventLines
    .map(line => {
      try {
        return JSON.parse(line);
      } catch {
        return null;
      }
    })
    .filter(Boolean);

  const timelineLines = readTailLines(timelinePath, 20);
  const value = {
    runtimeRoot,
    manifest,
    events,
    timelineLines
  };
  cachedLiveStatus = value;
  cachedLiveStatusKey = cacheKey;
  return value;
});

ipcMain.handle('agent:run', async (_event, payload) => {
  if (currentAgentProcess) {
    throw new Error('Agent is already running.');
  }

  const workspaceRoot = String(payload.workspaceRoot || '').trim();
  const task = String(payload.task || '').trim();
  const accessMode = String(payload.accessMode || 'WorkspaceWrite').trim();
  const model = String(payload.model || 'qwen2.5-coder:7b').trim();

  if (!workspaceRoot) {
    throw new Error('Workspace is not selected.');
  }

  if (!task) {
    throw new Error('Task is empty.');
  }

  const dllPath = path.join(__dirname, '..', 'bin', 'Debug', 'net8.0', 'LocalCursorAgent.dll');
  if (!fs.existsSync(dllPath)) {
    throw new Error(`Aelivar engine binary not found: ${dllPath}`);
  }

  const args = [dllPath, '--workspace', workspaceRoot, '--access', accessMode, '--parent-pid', String(process.pid), '--ollama-model', model || 'qwen2.5-coder:7b', '--task', task];
  
  // Add model configuration if specified
  return await new Promise((resolve, reject) => {
    const logs = [];
    let stdout = '';
    let stderr = '';

    const child = spawn('dotnet', args, {
      cwd: path.dirname(dllPath),
      shell: false,
      windowsHide: true,
      env: {
        ...process.env,
        LOCALCURSOR_LLM_PROVIDER: 'local',
        OLLAMA_MODEL: model || 'qwen2.5-coder:7b'
      }
    });

    currentAgentProcess = child;

    child.stdout.on('data', chunk => {
      const text = chunk.toString();
      stdout += text;
      logs.push(...text.split(/\r?\n/).filter(Boolean));
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('agent:log', { stream: 'stdout', text });
      }
    });

    child.stderr.on('data', chunk => {
      const text = chunk.toString();
      stderr += text;
      logs.push(...text.split(/\r?\n/).filter(Boolean));
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('agent:log', { stream: 'stderr', text });
      }
    });

    child.on('error', err => {
      currentAgentProcess = null;
      reject(err);
    });

    child.on('close', code => {
      currentAgentProcess = null;
      const structuredResult = extractStructuredResult(logs);
      const result = {
        exitCode: code,
        logs,
        stdout,
        stderr,
        structuredResult
      };

      if (code !== 0) {
        const error = new Error(
          structuredResult && structuredResult.message
            ? structuredResult.message
            : `Aelivar failed with exit code ${code}.`
        );
        error.result = result;
        reject(error);
        return;
      }

      resolve(result);
    });
  });
});

ipcMain.handle('agent:stop', async () => {
  return await stopAgentProcess();
});

ipcMain.handle('terminal:run', async (_event, payload) => {
  if (currentTerminalProcess) {
    throw new Error('Terminal is already running.');
  }

  const workspaceRoot = String(payload.workspaceRoot || '').trim();
  const command = String(payload.command || '').trim();

  if (!workspaceRoot) {
    throw new Error('Workspace is not selected.');
  }

  if (!command) {
    throw new Error('Terminal command is empty.');
  }

  return await new Promise((resolve, reject) => {
    const logs = [];
    let stdout = '';
    let stderr = '';

    const child = spawn('powershell', ['-NoLogo', '-NoProfile', '-Command', command], {
      cwd: workspaceRoot,
      shell: false,
      windowsHide: true
    });

    currentTerminalProcess = child;

    child.stdout.on('data', chunk => {
      const text = chunk.toString();
      stdout += text;
      logs.push(...text.split(/\r?\n/).filter(Boolean));
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('terminal:log', { stream: 'stdout', text });
      }
    });

    child.stderr.on('data', chunk => {
      const text = chunk.toString();
      stderr += text;
      logs.push(...text.split(/\r?\n/).filter(Boolean));
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('terminal:log', { stream: 'stderr', text });
      }
    });

    child.on('error', err => {
      currentTerminalProcess = null;
      reject(err);
    });

    child.on('close', code => {
      currentTerminalProcess = null;
      resolve({
        exitCode: code,
        logs,
        stdout,
        stderr
      });
    });
  });
});

ipcMain.handle('terminal:stop', async () => {
  if (!currentTerminalProcess) {
    return false;
  }

  try {
    currentTerminalProcess.kill();
    currentTerminalProcess = null;
    return true;
  } catch {
    return false;
  }
});

ipcMain.handle('file:createFile', async (_event, payload) => {
  const workspaceRoot = String(payload.workspaceRoot || '').trim();
  const fileName = String(payload.fileName || '').trim();

  if (!workspaceRoot || !fs.existsSync(workspaceRoot)) {
    throw new Error('Workspace is not selected.');
  }

  if (!fileName) {
    throw new Error('File name is empty.');
  }

  const filePath = path.join(workspaceRoot, fileName);
  fs.writeFileSync(filePath, '');
  return filePath;
});

ipcMain.handle('file:createFolder', async (_event, payload) => {
  const workspaceRoot = String(payload.workspaceRoot || '').trim();
  const folderName = String(payload.folderName || '').trim();

  if (!workspaceRoot || !fs.existsSync(workspaceRoot)) {
    throw new Error('Workspace is not selected.');
  }

  if (!folderName) {
    throw new Error('Folder name is empty.');
  }

  const folderPath = path.join(workspaceRoot, folderName);
  fs.mkdirSync(folderPath, { recursive: true });
  return folderPath;
});

ipcMain.handle('file:deleteFile', async (_event, filePath) => {
  if (!filePath || !fs.existsSync(filePath)) {
    throw new Error('File not found.');
  }

  const stats = fs.statSync(filePath);
  if (stats.isDirectory()) {
    fs.rmSync(filePath, { recursive: true, force: true });
  } else {
    fs.unlinkSync(filePath);
  }

  return true;
});

function extractStructuredResult(lines) {
  for (let i = lines.length - 1; i >= 0; i--) {
    const raw = String(lines[i] || '').trim();
    if (!raw.startsWith('{') || !raw.endsWith('}')) {
      continue;
    }

    try {
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === 'object' && typeof parsed.ok === 'boolean') {
        return parsed;
      }
    } catch {
      continue;
    }
  }

  return null;
}

function wireContextMenu(window) {
  window.webContents.on('context-menu', (_event, params) => {
    const hasSelection = Boolean(String(params.selectionText || '').trim());
    const isEditable = Boolean(params.isEditable);

    if (!isEditable && !hasSelection) {
      return;
    }

    const template = [];

    if (isEditable) {
      template.push(
        { role: 'undo', label: 'Отменить' },
        { role: 'redo', label: 'Повторить' },
        { type: 'separator' },
        { role: 'cut', label: 'Вырезать' },
        { role: 'copy', label: 'Копировать', enabled: hasSelection },
        { role: 'paste', label: 'Вставить' },
        { role: 'delete', label: 'Удалить' },
        { type: 'separator' },
        { role: 'selectAll', label: 'Выделить всё' }
      );
    } else if (hasSelection) {
      template.push(
        { role: 'copy', label: 'Копировать' },
        { type: 'separator' },
        { role: 'selectAll', label: 'Выделить всё' }
      );
    }

    if (template.length === 0) {
      return;
    }

    Menu.buildFromTemplate(template).popup({ window });
  });
}
