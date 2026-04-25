const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('desktopApi', {
  selectFolder: () => ipcRenderer.invoke('dialog:openFolder'),
  openFolder: () => ipcRenderer.invoke('dialog:openFolder'),
  openFiles: () => ipcRenderer.invoke('dialog:openFiles'),
  loadWorkspaceState: workspaceRoot => ipcRenderer.invoke('storage:loadWorkspaceState', workspaceRoot),
  saveWorkspaceState: payload => ipcRenderer.invoke('storage:saveWorkspaceState', payload),
  prepareAttachments: payload => ipcRenderer.invoke('attachments:prepare', payload),
  getProjectStructure: workspaceRoot => ipcRenderer.invoke('workspace:listFiles', workspaceRoot),
  listFiles: workspaceRoot => ipcRenderer.invoke('workspace:listFiles', workspaceRoot),
  readFile: filePath => ipcRenderer.invoke('workspace:readFile', filePath),
  importFiles: payload => ipcRenderer.invoke('workspace:importFiles', payload),
  openPath: targetPath => ipcRenderer.invoke('shell:openPath', targetPath),
  getZoom: () => ipcRenderer.invoke('app:getZoom'),
  setZoom: factor => ipcRenderer.invoke('app:setZoom', factor),
  checkHealth: payload => ipcRenderer.invoke('health:check', payload),
  listOllamaModels: payload => ipcRenderer.invoke('ollama:listModels', payload),
  getAgentLiveStatus: () => ipcRenderer.invoke('agent:getLiveStatus'),
  runAgent: payload => ipcRenderer.invoke('agent:run', payload),
  stopAgent: () => ipcRenderer.invoke('agent:stop'),
  runTerminal: payload => ipcRenderer.invoke('terminal:run', payload),
  stopTerminal: () => ipcRenderer.invoke('terminal:stop'),
  executeTask: payload => ipcRenderer.invoke('agent:run', payload),
  stopTask: () => ipcRenderer.invoke('agent:stop'),
  onAgentLog: callback => {
    const handler = (_event, payload) => callback(payload);
    ipcRenderer.on('agent:log', handler);
    return () => ipcRenderer.removeListener('agent:log', handler);
  },
  onTerminalLog: callback => {
    const handler = (_event, payload) => callback(payload);
    ipcRenderer.on('terminal:log', handler);
    return () => ipcRenderer.removeListener('terminal:log', handler);
  },
  onMenuAction: callback => {
    const handler = (_event, payload) => callback(payload);
    ipcRenderer.on('menu:action', handler);
    return () => ipcRenderer.removeListener('menu:action', handler);
  }
});
