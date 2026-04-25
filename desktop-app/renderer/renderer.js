class Run {
  constructor(task) {
    this.id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    this.task = task;
    this.status = 'pending';
    this.startedAt = new Date().toISOString();
    this.completedAt = null;
    this.logs = [];
    this.summary = '';
    this.changedFiles = [];
  }
}

class Message {
  constructor(text, role = 'user', attachments = []) {
    this.id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    this.text = text;
    this.role = role;
    this.attachments = attachments;
    this.timestamp = new Date().toISOString();
  }
}

class Session {
  constructor(title) {
    this.id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    this.title = title;
    this.createdAt = new Date().toISOString();
    this.messages = [];
    this.runs = [];
  }
}

const appState = {
  workspace: null,
  sessions: [],
  currentSessionId: null,
  archivedSessions: [],
  isRunning: false,
  currentRunId: null,
  previewFilePath: '',
  previewContent: '',
  theme: 'light',
  pendingAttachments: [],
  workspaceTreeExpanded: true,
  activityExpanded: false,
  liveActivity: null
};

const explorerState = { tree: [], expanded: new Set() };
let statusResetTimer = null;
let healthPollTimer = null;
let editingSessionId = null;
let uiScale = 0.75;
let liveStatusPollTimer = null;
let persistWorkspaceTimer = null;
let messagesRenderQueued = false;

const openWorkspaceBtn = document.getElementById('openWorkspaceBtn');
const workspaceInfo = document.getElementById('workspaceInfo');
const workspaceClosed = document.getElementById('workspaceClosed');
const workspaceName = document.getElementById('workspaceName');
const workspaceToggleBtn = document.getElementById('workspaceToggleBtn');
const workspaceToggleIcon = document.getElementById('workspaceToggleIcon');
const projectExplorerSection = document.getElementById('projectExplorerSection');
const fileTree = document.getElementById('fileTree');
const newSessionBtn = document.getElementById('newSessionBtn');
const sessionsList = document.getElementById('sessionsList');
const settingsSection = document.getElementById('settingsSection');
const closeSettingsBtn = document.getElementById('closeSettingsBtn');
const closeOverlayBtn = document.getElementById('closeOverlayBtn');
const sidebarOverlay = document.getElementById('sidebarOverlay');
const overlayTitle = document.getElementById('overlayTitle');
const overlayBody = document.getElementById('overlayBody');
const modelSelect = document.getElementById('modelSelect');
const themeSelect = document.getElementById('themeSelect');
const accessModeSelect = document.getElementById('accessModeSelect');
const composerModelSelect = document.getElementById('composerModelSelect');
const attachButton = document.getElementById('attachButton');
const attachmentBar = document.getElementById('attachmentBar');
const settingsButton = document.querySelector('[data-activity="settings"]');
const backButton = document.getElementById('backButton');
const forwardButton = document.getElementById('forwardButton');
const chatTitle = document.getElementById('chatTitle');
const statusInfo = document.getElementById('statusInfo');
const chatStatus = document.getElementById('chatStatus');
const liveActivityBar = document.getElementById('liveActivityBar');
const activityToggleBtn = document.getElementById('activityToggleBtn');
const activitySummary = document.getElementById('activitySummary');
const activityDetails = document.getElementById('activityDetails');
const activityTitle = document.getElementById('activityTitle');
const activityPath = document.getElementById('activityPath');
const activityMeta = document.getElementById('activityMeta');
const activityCloseBtn = document.getElementById('activityCloseBtn');
const messagesArea = document.getElementById('messagesArea');
const taskInput = document.getElementById('taskInput');
const sendBtn = document.getElementById('sendBtn');
const stopBtn = document.getElementById('stopBtn');
const chatPanel = document.getElementById('chatPanel');
const inlinePreviewPanel = document.getElementById('inlinePreviewPanel');
const previewTitle = document.getElementById('previewTitle');
const previewPath = document.getElementById('previewPath');
const filePreview = document.getElementById('filePreview');
const previewCloseBtn = document.getElementById('previewCloseBtn');

document.addEventListener('DOMContentLoaded', async () => {
  restoreShellState();
  applyTheme(appState.theme);
  setupEventListeners();
  if (appState.workspace) {
    await loadWorkspaceState(appState.workspace);
    await refreshWorkspaceState();
  }
  render();
  startHealthPolling();
  startLiveStatusPolling();
});

function setupEventListeners() {
  openWorkspaceBtn.addEventListener('click', selectWorkspace);
  workspaceToggleBtn?.addEventListener('click', toggleWorkspaceTree);
  newSessionBtn.addEventListener('click', createNewSession);
  sendBtn.addEventListener('click', sendTask);
  stopBtn.addEventListener('click', stopRun);
  attachButton.addEventListener('click', attachFilesToComposer);
  settingsButton?.addEventListener('click', toggleSettings);
  closeSettingsBtn?.addEventListener('click', () => settingsSection.classList.add('hidden'));
  closeOverlayBtn?.addEventListener('click', closeSidebarOverlay);
  backButton.addEventListener('click', () => selectAdjacentSession(-1));
  forwardButton.addEventListener('click', () => selectAdjacentSession(1));
  activityToggleBtn?.addEventListener('click', toggleActivityDetails);
  activityCloseBtn?.addEventListener('click', () => {
    appState.activityExpanded = false;
    renderActivityPanel();
  });
  previewCloseBtn?.addEventListener('click', closeFilePreview);

  document.addEventListener('click', event => {
    if (settingsSection.classList.contains('hidden')) {
      return;
    }

    if (settingsSection.contains(event.target) || settingsButton?.contains(event.target)) {
      return;
    }

    settingsSection.classList.add('hidden');
  });

  taskInput.addEventListener('keydown', event => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      sendTask();
    }
  });
  taskInput.addEventListener('input', autoResizeTaskInput);

  modelSelect.addEventListener('change', syncModelSelectsFromSettings);
  composerModelSelect.addEventListener('change', syncModelSelectsFromComposer);
  themeSelect.addEventListener('change', async () => {
    appState.theme = themeSelect.value;
    applyTheme(appState.theme);
    persistShellState();
    schedulePersistWorkspaceState();
  });

  window.desktopApi.onAgentLog?.(({ text }) => {
    const run = getCurrentRun();
    if (!run || !appState.isRunning) {
      return;
    }

    run.logs.push(text);
    updateLiveActivityFromLogText(text);
    scheduleRenderMessages();
    schedulePersistWorkspaceState(400);
  });

  window.desktopApi.onMenuAction?.(async ({ action }) => {
    switch (action) {
      case 'new-session':
        createNewSession();
        break;
      case 'open-workspace':
        await selectWorkspace();
        break;
      case 'import-files':
        await importFilesIntoWorkspace();
        break;
      case 'open-preview-file':
        await openPreviewFile();
        break;
      case 'refresh-explorer':
        await refreshWorkspaceState();
        break;
      default:
        break;
    }
  });
}

async function selectWorkspace() {
  const workspace = await window.desktopApi.openFolder();
  if (!workspace) {
    return;
  }

  appState.workspace = workspace;
  persistShellState();
  await loadWorkspaceState(workspace);
  await refreshWorkspaceState();
  render();
  setStatus('Проект открыт');
}

async function loadWorkspaceState(workspace) {
  const saved = await window.desktopApi.loadWorkspaceState(workspace);
  appState.sessions = Array.isArray(saved?.sessions) ? saved.sessions : [];
  appState.archivedSessions = Array.isArray(saved?.archivedSessions) ? saved.archivedSessions : [];
  appState.currentSessionId = saved?.currentSessionId || appState.sessions[0]?.id || null;
  appState.currentRunId = saved?.currentRunId || null;
  appState.previewFilePath = saved?.previewFilePath || '';
  appState.previewContent = '';
  appState.workspaceTreeExpanded = saved?.workspaceTreeExpanded !== false;
  appState.activityExpanded = saved?.activityExpanded === true;
  explorerState.expanded = new Set(Array.isArray(saved?.expandedPaths) ? saved.expandedPaths : []);
  appState.pendingAttachments = [];
  ensureActiveSession();
}

async function persistWorkspaceState() {
  if (!appState.workspace) {
    return;
  }

  await window.desktopApi.saveWorkspaceState({
    workspaceRoot: appState.workspace,
    state: {
      workspaceRoot: appState.workspace,
      currentSessionId: appState.currentSessionId,
      currentRunId: appState.currentRunId,
      previewFilePath: appState.previewFilePath,
      activityExpanded: appState.activityExpanded,
      sessions: appState.sessions,
      archivedSessions: appState.archivedSessions,
      workspaceTreeExpanded: appState.workspaceTreeExpanded,
      expandedPaths: Array.from(explorerState.expanded)
    }
  });
}

function schedulePersistWorkspaceState(delay = 180) {
  clearTimeout(persistWorkspaceTimer);
  persistWorkspaceTimer = setTimeout(() => {
    void persistWorkspaceState();
  }, delay);
}

function scheduleRenderMessages() {
  if (messagesRenderQueued) {
    return;
  }

  messagesRenderQueued = true;
  requestAnimationFrame(() => {
    messagesRenderQueued = false;
    renderMessages();
  });
}

function persistShellState() {
  localStorage.setItem('aelivar-shell-state', JSON.stringify({
    workspace: appState.workspace,
    theme: appState.theme,
    uiScale
  }));
}

function restoreShellState() {
  const raw = localStorage.getItem('aelivar-shell-state');
  if (!raw) {
    return;
  }

  try {
    const parsed = JSON.parse(raw);
    appState.workspace = parsed.workspace || null;
    appState.theme = parsed.theme || 'light';
    setUiScale(typeof parsed.uiScale === 'number' ? parsed.uiScale : 0.75);
  } catch {
    appState.workspace = null;
    appState.theme = 'light';
  }
}

async function refreshWorkspaceState() {
  explorerState.tree = appState.workspace
    ? await window.desktopApi.listFiles(appState.workspace)
    : [];
  if (appState.previewFilePath) {
    try {
      appState.previewContent = await window.desktopApi.readFile(appState.previewFilePath);
      previewTitle.textContent = appState.previewFilePath.split(/[\\/]/).pop();
      previewPath.textContent = appState.previewFilePath;
      filePreview.textContent = appState.previewContent || '';
    } catch {
      appState.previewFilePath = '';
      appState.previewContent = '';
    }
  }
  renderWorkspace();
  renderFileTree();
}

function toggleWorkspaceTree() {
  appState.workspaceTreeExpanded = !appState.workspaceTreeExpanded;
  renderWorkspace();
  schedulePersistWorkspaceState();
}

async function importFilesIntoWorkspace() {
  if (!appState.workspace) {
    setStatus('Сначала откройте проект', 'error');
    return;
  }

  const files = await window.desktopApi.openFiles();
  if (!Array.isArray(files) || files.length === 0) {
    return;
  }

  await window.desktopApi.importFiles({
    workspaceRoot: appState.workspace,
    files
  });

  await refreshWorkspaceState();
  setStatus(`Добавлено файлов: ${files.length}`);
}

async function openPreviewFile() {
  const files = await window.desktopApi.openFiles();
  if (!Array.isArray(files) || files.length === 0) {
    return;
  }

  await openFilePreview(files[0]);
}

async function openFilePreview(filePath) {
  if (appState.previewFilePath === filePath) {
    closeFilePreview();
    return;
  }

  appState.previewFilePath = filePath;
  appState.previewContent = await window.desktopApi.readFile(filePath);
  previewTitle.textContent = filePath.split(/[\\/]/).pop();
  previewPath.textContent = filePath;
  filePreview.textContent = appState.previewContent || '';
  inlinePreviewPanel.classList.remove('hidden');
  renderFileTree();
  await persistWorkspaceState();
}

function closeFilePreview() {
  appState.previewFilePath = '';
  appState.previewContent = '';
  inlinePreviewPanel.classList.add('hidden');
  renderFileTree();
  void persistWorkspaceState();
}

function renderActivePanel() {
  chatPanel.classList.add('active');
  if (!appState.previewFilePath) {
    inlinePreviewPanel.classList.add('hidden');
    return;
  }

  inlinePreviewPanel.classList.remove('hidden');
  previewTitle.textContent = appState.previewFilePath.split(/[\\/]/).pop();
  previewPath.textContent = appState.previewFilePath;
  filePreview.textContent = appState.previewContent || '';
}

function createNewSession() {
  if (!appState.workspace) {
    setStatus('Сначала откройте проект', 'error');
    return;
  }

  const session = new Session('Новая беседа');
  appState.sessions.unshift(session);
  appState.currentSessionId = session.id;
  schedulePersistWorkspaceState();
  render();
}

function ensureActiveSession() {
  const existing = getCurrentSession();
  if (existing) {
    return existing;
  }

  const session = new Session('Новая беседа');
  appState.sessions.unshift(session);
  appState.currentSessionId = session.id;
  return session;
}

function getCurrentSession() {
  return appState.sessions.find(session => session.id === appState.currentSessionId) || null;
}

function getCurrentRun() {
  const session = getCurrentSession();
  if (!session || !appState.currentRunId) {
    return null;
  }

  return session.runs.find(run => run.id === appState.currentRunId) || null;
}

function getLastRun() {
  const session = getCurrentSession();
  if (!session || session.runs.length === 0) {
    return null;
  }

  return session.runs[session.runs.length - 1];
}

function selectSession(sessionId) {
  appState.currentSessionId = sessionId;
  schedulePersistWorkspaceState();
  render();
}

function deleteSession(sessionId) {
  appState.sessions = appState.sessions.filter(item => item.id !== sessionId);
  if (appState.currentSessionId === sessionId) {
    appState.currentSessionId = appState.sessions[0]?.id || null;
  }
  ensureActiveSession();
  schedulePersistWorkspaceState();
  render();
}

function archiveSession(sessionId) {
  const session = appState.sessions.find(item => item.id === sessionId);
  if (!session) {
    return;
  }

  appState.archivedSessions.unshift(session);
  appState.sessions = appState.sessions.filter(item => item.id !== sessionId);
  if (appState.currentSessionId === sessionId) {
    appState.currentSessionId = appState.sessions[0]?.id || null;
  }
  ensureActiveSession();
  schedulePersistWorkspaceState();
  render();
}

function restoreArchivedSession(sessionId) {
  const session = appState.archivedSessions.find(item => item.id === sessionId);
  if (!session) {
    return;
  }

  appState.archivedSessions = appState.archivedSessions.filter(item => item.id !== sessionId);
  appState.sessions.unshift(session);
  appState.currentSessionId = session.id;
  schedulePersistWorkspaceState();
  render();
}

function deleteArchivedSession(sessionId) {
  appState.archivedSessions = appState.archivedSessions.filter(item => item.id !== sessionId);
  schedulePersistWorkspaceState();
  renderArchiveOverlay();
}

function renameSession(sessionId) {
  editingSessionId = sessionId;
  renderSessions();
}

function commitSessionRename(sessionId, nextTitle) {
  const session = appState.sessions.find(item => item.id === sessionId);
  editingSessionId = null;
  if (session && nextTitle && nextTitle.trim()) {
    session.title = nextTitle.trim();
    schedulePersistWorkspaceState();
  }
  renderSessions();
}

function selectAdjacentSession(direction) {
  if (appState.sessions.length === 0) {
    return;
  }

  const currentIndex = appState.sessions.findIndex(session => session.id === appState.currentSessionId);
  if (currentIndex < 0) {
    return;
  }

  const nextIndex = currentIndex + direction;
  if (nextIndex < 0 || nextIndex >= appState.sessions.length) {
    return;
  }

  selectSession(appState.sessions[nextIndex].id);
}

async function attachFilesToComposer() {
  const files = await window.desktopApi.openFiles();
  if (!Array.isArray(files) || files.length === 0) {
    return;
  }

  const prepared = await window.desktopApi.prepareAttachments({ files });
  appState.pendingAttachments.push(...prepared);
  renderAttachments();
}

function removePendingAttachment(attachmentId) {
  appState.pendingAttachments = appState.pendingAttachments.filter(item => item.id !== attachmentId);
  renderAttachments();
}

function buildTaskPayload(task, attachments) {
  if (!attachments.length) {
    return task;
  }

  const blocks = attachments.map(file => {
    const header = `Attached file: ${file.path} (${file.size} bytes)`;
    if (!file.isTextLike || !file.inlineContent) {
      return `${header}\nBinary or unsupported preview. Use it as context metadata only.`;
    }

    const suffix = file.truncated ? '\n[content truncated]' : '';
    return `${header}\n---\n${file.inlineContent}${suffix}\n---`;
  });

  return `${task}\n\nUse these attached files as additional context:\n\n${blocks.join('\n\n')}`;
}

async function sendTask() {
  const task = taskInput.value.trim();
  if (!task || appState.isRunning || !appState.workspace) {
    return;
  }

  const session = ensureActiveSession();
  const attachments = appState.pendingAttachments.map(item => ({ ...item }));
  session.messages.push(new Message(task, 'user', attachments));
  taskInput.value = '';
  autoResizeTaskInput();

  const run = new Run(task);
  run.status = 'running';
  session.runs.push(run);
  appState.currentRunId = run.id;
  appState.isRunning = true;
  appState.liveActivity = {
    eventType: 'ModelRequest',
    component: 'Agent',
    outcome: 'running',
    reasonCode: '',
    path: '',
    meta: 'Агент начал обрабатывать задачу.',
    timelineLines: []
  };
  const taskPayload = buildTaskPayload(task, attachments);
  appState.pendingAttachments = [];
  await persistWorkspaceState();
  render();

  try {
    const result = await window.desktopApi.runAgent({
      workspaceRoot: appState.workspace,
      task: taskPayload,
      model: composerModelSelect.value || 'qwen2.5-coder:7b',
      accessMode: accessModeSelect.value
    });

    run.status = result.exitCode === 0 && result.structuredResult?.ok !== false ? 'success' : 'failed';
    run.logs = Array.isArray(result.logs) ? result.logs : [];
    run.summary = result.structuredResult?.summary || 'Задача завершена';
    run.changedFiles = Array.isArray(result.structuredResult?.changedFiles) ? result.structuredResult.changedFiles : [];
    run.completedAt = new Date().toISOString();
    session.messages.push(new Message(run.summary, 'assistant'));
    setStatus(run.status === 'success' ? 'Готово' : 'Ошибка', run.status === 'success' ? 'info' : 'error');
  } catch (error) {
    run.status = 'failed';
    run.summary = String(error.message || error);
    run.completedAt = new Date().toISOString();
    session.messages.push(new Message(run.summary, 'assistant'));
    setStatus(run.summary, 'error');
  }

  appState.isRunning = false;
  appState.currentRunId = null;
  appState.liveActivity = {
    eventType: run.status === 'success' ? 'RunCompleted' : 'RunFailed',
    component: 'Agent',
    outcome: run.status,
    reasonCode: '',
    path: run.changedFiles?.[0] || '',
    meta: run.summary || 'Задача завершена.',
    timelineLines: []
  };
  await persistWorkspaceState();
  render();
}

async function stopRun() {
  await window.desktopApi.stopAgent();
  const run = getCurrentRun();
  if (run) {
    run.status = 'cancelled';
    run.completedAt = new Date().toISOString();
  }
  appState.isRunning = false;
  appState.currentRunId = null;
  appState.liveActivity = {
    eventType: 'RunCancelled',
    component: 'Agent',
    outcome: 'cancelled',
    reasonCode: 'USER_CANCELLED',
    path: '',
    meta: 'Выполнение остановлено пользователем.',
    timelineLines: []
  };
  await persistWorkspaceState();
  render();
  setStatus('Остановлено');
}

function toggleActivityDetails() {
  if (liveActivityBar.classList.contains('hidden')) {
    return;
  }

  appState.activityExpanded = !appState.activityExpanded;
  renderActivityPanel();
  void persistWorkspaceState();
}

function startLiveStatusPolling() {
  clearInterval(liveStatusPollTimer);
  void refreshLiveActivity();
  liveStatusPollTimer = setInterval(() => {
    void refreshLiveActivity();
  }, 1800);
}

async function refreshLiveActivity() {
  if (!appState.isRunning && !appState.liveActivity) {
    renderActivityPanel();
    return;
  }

  try {
    const payload = await window.desktopApi.getAgentLiveStatus();
    const nextActivity = buildLiveActivity(payload);
    if (nextActivity) {
      appState.liveActivity = nextActivity;
    } else if (!appState.isRunning) {
      appState.liveActivity = null;
    }
  } catch {
    if (!appState.isRunning) {
      appState.liveActivity = null;
    }
  }

  renderActivityPanel();
}

function buildLiveActivity(payload) {
  const events = Array.isArray(payload?.events) ? payload.events : [];
  const lastEvent = events[events.length - 1] || null;
  const timelineLines = Array.isArray(payload?.timelineLines) ? payload.timelineLines : [];
  const manifest = payload?.manifest || null;

  if (!lastEvent && !timelineLines.length && !manifest) {
    return null;
  }

  const metadata = lastEvent?.Metadata || lastEvent?.metadata || {};
  const pathCandidate = metadata.path
    || metadata.file_path
    || metadata.normalized_path
    || metadata.requested_path
    || metadata.workspace_root
    || metadata.working_directory
    || metadata.target_path
    || '';

  return {
    eventType: String(lastEvent?.EventType || lastEvent?.event_type || manifest?.FinalStatus || 'Running'),
    component: String(lastEvent?.Component || lastEvent?.component || 'Agent'),
    outcome: String(lastEvent?.Outcome || lastEvent?.outcome || manifest?.FinalStatus || 'running'),
    reasonCode: String(lastEvent?.ReasonCode || lastEvent?.reason_code || manifest?.ReasonCode || ''),
    path: String(pathCandidate || ''),
    meta: compactActivityMetadata(metadata, timelineLines),
    timelineLines
  };
}

function compactActivityMetadata(metadata, timelineLines) {
  const parts = [];
  const command = metadata.command || metadata.tool || metadata.action || metadata.operation_kind || '';
  const target = metadata.normalized_path || metadata.requested_path || metadata.path || metadata.file_path || '';
  const reason = metadata.reason_code || metadata.reason || '';

  if (command) {
    parts.push(`действие: ${command}`);
  }
  if (target) {
    parts.push(`цель: ${target}`);
  }
  if (reason) {
    parts.push(`причина: ${reason}`);
  }
  if (Array.isArray(metadata.target_set) && metadata.target_set.length > 0) {
    parts.push(`файлы: ${metadata.target_set.slice(0, 4).join(', ')}`);
  }

  const tail = timelineLines.slice(-3);
  if (tail.length > 0) {
    parts.push('', ...tail);
  }

  return parts.length > 0 ? parts.join('\n') : 'Агент выполняет шаги. Подробности появятся по мере работы.';
}

function updateLiveActivityFromLogText(text) {
  const trimmed = String(text || '').trim();
  if (!trimmed) {
    return;
  }

  const lines = trimmed.split(/\r?\n/).filter(Boolean);
  const lastLine = lines[lines.length - 1] || trimmed;
  const match = lastLine.match(/([A-Za-z]:\\[^:\r\n]+|\/[^:\r\n]+)/);

  appState.liveActivity = {
    eventType: 'LiveLog',
    component: 'Agent',
    outcome: 'running',
    reasonCode: '',
    path: match ? match[1] : '',
    meta: lastLine,
    timelineLines: lines.slice(-3)
  };
  renderActivityPanel();
}

function renderActivityPanel() {
  const currentRun = getCurrentRun() || getLastRun();
  const live = appState.liveActivity;
  const shouldShow = appState.isRunning || Boolean(live);

  liveActivityBar.classList.toggle('hidden', !shouldShow);
  if (!shouldShow) {
    activityDetails.classList.add('hidden');
    return;
  }

  activitySummary.textContent = live ? formatActivitySummary(live) : 'Агент в работе';
  activityTitle.textContent = live ? formatActivityTitle(live) : 'Агент выполняет задачу';
  activityPath.textContent = live?.path || currentRun?.changedFiles?.[0] || 'Путь пока не определён';
  activityMeta.textContent = live?.meta || 'Детали текущего шага появятся во время работы агента.';
  activityDetails.classList.toggle('hidden', !appState.activityExpanded);
  liveActivityBar.classList.toggle('expanded', appState.activityExpanded);
}

function formatActivitySummary(live) {
  const label = formatActivityTitle(live);
  return live?.path ? `${label}: ${basename(live.path)}` : label;
}

function formatActivityTitle(live) {
  const type = String(live?.eventType || '').toLowerCase();
  if (type.includes('fileaction')) {
    return 'Правит файл';
  }
  if (type.includes('buildverification') || type.includes('build')) {
    return 'Проверяет сборку';
  }
  if (type.includes('modelrequest')) {
    return 'Думает';
  }
  if (type.includes('targetresolution')) {
    return 'Ищет цель';
  }
  if (type.includes('indexing')) {
    return 'Индексирует проект';
  }
  if (type.includes('processspawn')) {
    return 'Запускает процесс';
  }
  if (type.includes('toollifecycle') || type.includes('toolresult')) {
    return 'Использует инструмент';
  }
  if (live?.outcome === 'failed') {
    return 'Ошибка';
  }
  return 'В работе';
}

function basename(targetPath) {
  return String(targetPath || '').split(/[\\/]/).pop() || targetPath;
}

function render() {
  renderWorkspace();
  renderFileTree();
  renderSessions();
  renderMessages();
  renderAttachments();
  renderStatus();
  renderActivityPanel();
  renderActivePanel();
}

function renderWorkspace() {
  const hasWorkspace = Boolean(appState.workspace);
  workspaceInfo.style.display = hasWorkspace ? 'block' : 'none';
  workspaceClosed.style.display = hasWorkspace ? 'none' : 'block';
  workspaceName.textContent = hasWorkspace ? appState.workspace.split(/[\\/]/).pop() : 'Нет проекта';
  workspaceToggleIcon.textContent = appState.workspaceTreeExpanded ? '▾' : '▸';
  projectExplorerSection.classList.toggle('collapsed', !appState.workspaceTreeExpanded);
  taskInput.disabled = !hasWorkspace;
  sendBtn.disabled = !hasWorkspace || appState.isRunning;
  themeSelect.value = appState.theme || 'light';
  modelSelect.value = composerModelSelect.value || 'qwen2.5-coder:7b';
  autoResizeTaskInput();
}

function renderSessions() {
  sessionsList.innerHTML = '';

  if (appState.sessions.length === 0) {
    sessionsList.innerHTML = '<div class="workspace-closed">Бесед пока нет</div>';
    return;
  }

  appState.sessions.forEach(session => {
    const button = document.createElement('div');
    button.className = `session-item${session.id === appState.currentSessionId ? ' active' : ''}`;
    button.innerHTML = `
      <div class="session-main">
        ${editingSessionId === session.id
          ? `<input class="session-title-input" type="text" value="${escapeHtml(session.title)}" />`
          : `<div class="session-title">${escapeHtml(session.title)}</div>`}
        <span class="session-item-time">${formatRelative(session.createdAt)}</span>
      </div>
      <div class="session-actions">
        <button class="session-action" type="button" data-action="rename" title="Переименовать">✎</button>
        <button class="session-action" type="button" data-action="archive" title="Архивировать">⌄</button>
        <button class="session-action" type="button" data-action="delete" title="Удалить">×</button>
      </div>
    `;

    if (editingSessionId === session.id) {
      const input = button.querySelector('.session-title-input');
      input.addEventListener('click', event => event.stopPropagation());
      input.addEventListener('keydown', event => {
        if (event.key === 'Enter') {
          event.preventDefault();
          commitSessionRename(session.id, input.value);
        } else if (event.key === 'Escape') {
          editingSessionId = null;
          renderSessions();
        }
      });
      input.addEventListener('blur', () => commitSessionRename(session.id, input.value));
      queueMicrotask(() => input.focus());
    } else {
      button.addEventListener('click', () => selectSession(session.id));
    }

    button.querySelector('[data-action="rename"]').addEventListener('click', event => {
      event.stopPropagation();
      renameSession(session.id);
    });
    button.querySelector('[data-action="archive"]').addEventListener('click', event => {
      event.stopPropagation();
      archiveSession(session.id);
    });
    button.querySelector('[data-action="delete"]').addEventListener('click', event => {
      event.stopPropagation();
      deleteSession(session.id);
    });

    sessionsList.appendChild(button);
  });
}

function renderFileTree() {
  fileTree.innerHTML = '';

  if (!appState.workspaceTreeExpanded) {
    return;
  }

  if (!Array.isArray(explorerState.tree) || explorerState.tree.length === 0) {
    fileTree.innerHTML = `<div class="workspace-closed">${appState.workspace ? 'Файлы не найдены' : 'Структура проекта появится после открытия проекта'}</div>`;
    return;
  }

  renderTreeNodes(explorerState.tree, fileTree);
}

function renderTreeNodes(nodes, container) {
  nodes.forEach(node => {
    const wrapper = document.createElement('div');
    wrapper.className = 'tree-node';

    const row = document.createElement('div');
    row.className = `tree-row${appState.previewFilePath === node.path ? ' file-active' : ''}`;

    const toggle = document.createElement('span');
    toggle.className = 'tree-toggle';
    toggle.textContent = node.kind === 'directory'
      ? (explorerState.expanded.has(node.path) ? '▾' : '▸')
      : '•';

    const name = document.createElement('span');
    name.className = 'tree-name';
    name.textContent = node.name;

    row.append(toggle, name);
    wrapper.appendChild(row);

    if (node.kind === 'directory') {
      const children = document.createElement('div');
      children.className = `tree-children${explorerState.expanded.has(node.path) ? ' open' : ''}`;
      renderTreeNodes(node.children || [], children);
      wrapper.appendChild(children);
      row.addEventListener('click', async () => {
        if (explorerState.expanded.has(node.path)) {
          explorerState.expanded.delete(node.path);
        } else {
          explorerState.expanded.add(node.path);
        }
        schedulePersistWorkspaceState();
        renderFileTree();
      });
    } else {
      row.addEventListener('click', async () => {
        await openFilePreview(node.path);
      });
      row.addEventListener('dblclick', async () => {
        if (appState.previewFilePath === node.path) {
          closeFilePreview();
        }
      });
    }

    container.appendChild(wrapper);
  });
}

function renderMessages() {
  const session = getCurrentSession();

  if (!session) {
    messagesArea.innerHTML = `
      <div class="empty-state">
        <div class="empty-title">Aelivar</div>
        <div class="empty-text">Откройте проект и сразу пишите задачу агенту.</div>
      </div>
    `;
    return;
  }

  chatTitle.textContent = session.title;
  const shouldStickToBottom = messagesArea.scrollHeight - messagesArea.scrollTop - messagesArea.clientHeight < 80;
  messagesArea.innerHTML = '';

  const items = [
    ...session.messages.map(message => ({ type: 'message', time: new Date(message.timestamp), value: message })),
    ...session.runs.map(run => ({ type: 'run', time: new Date(run.startedAt), value: run }))
  ].sort((a, b) => a.time - b.time);

  if (items.length === 0) {
    messagesArea.innerHTML = `
      <div class="empty-state">
        <div class="empty-title">Aelivar</div>
        <div class="empty-text">Беседа будет храниться внутри этого проекта и откроется снова при следующем запуске.</div>
      </div>
    `;
    return;
  }

  const fragment = document.createDocumentFragment();
  for (const item of items) {
    fragment.appendChild(item.type === 'message' ? createMessageNode(item.value) : createRunNode(item.value));
  }

  messagesArea.appendChild(fragment);
  if (shouldStickToBottom) {
    messagesArea.scrollTop = messagesArea.scrollHeight;
  }
}

function createMessageNode(message) {
  const wrapper = document.createElement('div');
  wrapper.className = `message ${message.role}`;

  const content = document.createElement('div');
  content.className = 'message-content';
  content.textContent = message.text;
  wrapper.appendChild(content);

  if (Array.isArray(message.attachments) && message.attachments.length > 0) {
    const attachmentList = document.createElement('div');
    attachmentList.className = 'message-attachments';

    message.attachments.forEach(file => {
      const chip = document.createElement('button');
      chip.className = 'attachment-chip';
      chip.type = 'button';
      chip.textContent = file.name;
      chip.addEventListener('click', () => openFilePreview(file.path));
      attachmentList.appendChild(chip);
    });

    wrapper.appendChild(attachmentList);
  }

  return wrapper;
}

function createRunNode(run) {
  if (run.status === 'running') {
    return document.createDocumentFragment();
  }

  const wrapper = document.createElement('div');
  wrapper.className = 'run-block';

  const statusLabel = run.status === 'running'
    ? 'В работе'
    : run.status === 'success'
      ? 'Готово'
      : run.status === 'cancelled'
        ? 'Остановлено'
        : 'Ошибка';

  wrapper.innerHTML = `<div class="run-header"><span class="run-status ${run.status}">${statusLabel}</span></div>`;

  if (run.summary) {
    const summary = document.createElement('div');
    summary.className = 'run-summary';
    summary.textContent = run.summary;
    wrapper.appendChild(summary);
  }

  if (Array.isArray(run.changedFiles) && run.changedFiles.length > 0) {
    const list = document.createElement('div');
    list.className = 'run-changed-files';
    run.changedFiles.forEach(file => {
      const item = document.createElement('div');
      item.className = 'run-changed-file';
      item.textContent = file;
      list.appendChild(item);
    });
    wrapper.appendChild(list);
  }

  return wrapper;
}

function renderAttachments() {
  attachmentBar.innerHTML = '';
  attachmentBar.classList.toggle('hidden', appState.pendingAttachments.length === 0);

  appState.pendingAttachments.forEach(file => {
    const chip = document.createElement('div');
    chip.className = 'pending-attachment';
    chip.innerHTML = `
      <span class="pending-attachment-name">${escapeHtml(file.name)}</span>
      <button class="pending-attachment-remove" type="button" title="Убрать">×</button>
    `;
    chip.querySelector('.pending-attachment-remove').addEventListener('click', () => removePendingAttachment(file.id));
    attachmentBar.appendChild(chip);
  });
}

function renderStatus() {
  const currentRun = getCurrentRun() || getLastRun();
  stopBtn.style.display = appState.isRunning ? 'inline-flex' : 'none';
  sendBtn.disabled = !appState.workspace || appState.isRunning;

  if (appState.isRunning) {
    chatStatus.textContent = 'В работе';
    chatStatus.className = 'status-badge running';
    statusInfo.className = 'chat-subtitle working';
    return;
  }

  if (!currentRun) {
    chatStatus.textContent = '';
    chatStatus.className = 'status-badge idle';
    statusInfo.className = 'chat-subtitle';
    return;
  }

  if (currentRun.status === 'success') {
    chatStatus.textContent = 'Готово';
    chatStatus.className = 'status-badge success';
    statusInfo.className = 'chat-subtitle success';
  } else if (currentRun.status === 'cancelled') {
    chatStatus.textContent = 'Остановлено';
    chatStatus.className = 'status-badge cancelled';
    statusInfo.className = 'chat-subtitle cancelled';
  } else if (currentRun.status === 'failed') {
    chatStatus.textContent = 'Ошибка';
    chatStatus.className = 'status-badge error';
    statusInfo.className = 'chat-subtitle error';
  } else {
    chatStatus.textContent = '';
    chatStatus.className = 'status-badge idle';
    statusInfo.className = 'chat-subtitle';
  }
}

function toggleSettings() {
  closeSidebarOverlay();
  settingsSection.classList.toggle('hidden');
}

function closeSidebarOverlay() {
  sidebarOverlay.classList.add('hidden');
}

function syncModelSelectsFromSettings() {
  composerModelSelect.value = modelSelect.value;
  void refreshModelHealth();
}

function syncModelSelectsFromComposer() {
  modelSelect.value = composerModelSelect.value;
  void refreshModelHealth();
}

function autoResizeTaskInput() {
  taskInput.style.height = '42px';
  taskInput.style.height = `${Math.min(taskInput.scrollHeight, 160)}px`;
}

function setUiScale(nextScale) {
  uiScale = Math.max(0.5, Math.min(1.1, Number(nextScale.toFixed(2))));
  document.documentElement.style.setProperty('--ui-scale', String(uiScale));
}

function applyTheme(theme) {
  document.documentElement.setAttribute('data-theme', theme);
}

function startHealthPolling() {
  void refreshModelHealth();
  clearInterval(healthPollTimer);
  healthPollTimer = setInterval(() => {
    void refreshModelHealth();
  }, 15000);
}

async function refreshModelHealth() {
  const qwenSettingsOption = Array.from(modelSelect.options).find(option => option.value === 'qwen2.5-coder:7b');
  const qwenComposerOption = Array.from(composerModelSelect.options).find(option => option.value === 'qwen2.5-coder:7b');
  if (!qwenSettingsOption || !qwenComposerOption) {
    return;
  }

  const health = await window.desktopApi.checkHealth({ llmModel: 'qwen2.5-coder:7b' });
  const label = health.llm?.status === 'ready' ? 'qwen2.5-coder:7b (ready)' : 'qwen2.5-coder:7b (offline)';
  if (qwenSettingsOption.textContent !== label) {
    qwenSettingsOption.textContent = label;
  }
  if (qwenComposerOption.textContent !== label) {
    qwenComposerOption.textContent = label;
  }
}

function formatRelative(value) {
  const date = new Date(value);
  const diff = Date.now() - date.getTime();
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));
  if (days > 0) {
    return `${days}д`;
  }
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function setStatus(message, type = 'info') {
  clearTimeout(statusResetTimer);
  statusInfo.textContent = message;
  statusInfo.style.color = type === 'error' ? 'var(--error)' : 'var(--muted)';
  statusInfo.className = `chat-subtitle ${type === 'error' ? 'error' : type === 'working' ? 'working' : type === 'success' ? 'success' : ''}`.trim();
  if (type === 'error') {
    statusResetTimer = setTimeout(() => {
      statusInfo.textContent = 'Готово';
      statusInfo.style.color = 'var(--muted)';
      statusInfo.className = 'chat-subtitle';
    }, 5000);
  }
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}
