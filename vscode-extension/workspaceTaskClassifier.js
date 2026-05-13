function normalizeTaskText(task) {
  const normalized = String(task || '')
    .trim()
    .toLowerCase()
    .replace(/[\u2018\u2019]/g, "'")
    .replace(/[\u201c\u201d]/g, '"');
  return normalized.replace(/^['"`]+|['"`]+$/g, '').trim();
}

function hasExplicitMutationTarget(value) {
  if (!value) return false;
  const fileLikePattern = /(^|[\s"'`])(?:[\w.-]+[\\/])*[\w.-]+\.(?:cs|js|ts|json|md|cmd|ps1|csproj|sln|html|css|yml|yaml|xml|txt|config)(?=$|[\s"'`.,;:!?])/i;
  if (fileLikePattern.test(value)) return true;
  const explicitTargetTerms = [
    ' file ', ' files ', ' folder ', ' directory ', ' package ', ' package.json ',
    ' script ', ' test ', ' class ', ' method ', ' function ', ' module ',
    ' \u0444\u0430\u0439\u043b', ' \u0444\u0430\u0439\u043b\u0430', ' \u0444\u0430\u0439\u043b\u044b', ' \u043f\u0430\u043f\u043a', ' \u0441\u043a\u0440\u0438\u043f\u0442', ' \u0442\u0435\u0441\u0442', ' \u043a\u043b\u0430\u0441\u0441', ' \u043c\u0435\u0442\u043e\u0434', ' \u0444\u0443\u043d\u043a\u0446\u0438\u044e', ' \u043c\u043e\u0434\u0443\u043b\u044c'
  ];
  const padded = ` ${value} `;
  return explicitTargetTerms.some(term => padded.includes(term));
}

function isLowSignalTask(value) {
  if (!value) return true;
  const compact = value.replace(/[\s?!.,;:]+/g, '');
  const lowSignalValues = new Set([
    'here', 'there', 'this', 'ok', 'okay', 'hi', 'hello', 'yo', 'sup',
    '\u0442\u0443\u0442', '\u0437\u0434\u0435\u0441\u044c', '\u044d\u0442\u043e', '\u0441\u044e\u0434\u0430', '\u043e\u043a', '\u043f\u0440\u0438\u0432\u0435\u0442', '\u0430\u043b\u043b\u043e', '\u0430\u043b\u043e', '\u0430\u0433\u0435\u043d\u0442'
  ]);
  if (lowSignalValues.has(compact)) return true;

  if (/^(what can you do|explain this project|describe this project|where is|what is|how does|help me|can you help)\b/.test(value)) return true;
  if (/^(\u0447\u0442\u043e \u0442\u044b \u0443\u043c\u0435\u0435\u0448\u044c|\u043e\u0431\u044a\u044f\u0441\u043d\u0438 \u043f\u0440\u043e\u0435\u043a\u0442|\u043e\u043f\u0438\u0448\u0438 \u043f\u0440\u043e\u0435\u043a\u0442|\u0447\u0442\u043e \u0442\u0443\u0442|\u0433\u0434\u0435 \u043d\u0430\u0445\u043e\u0434\u0438\u0442\u0441\u044f|\u0447\u0442\u043e \u044d\u0442\u043e|\u043a\u0430\u043a \u0440\u0430\u0431\u043e\u0442\u0430\u0435\u0442|\u043f\u043e\u043c\u043e\u0433\u0438|\u043c\u043e\u0436\u0435\u0448\u044c \u043f\u043e\u043c\u043e\u0447\u044c)\b/.test(value)) return true;

  const signalChars = compact.replace(/[`'"-]/g, '');
  if (signalChars.length > 0 && signalChars.length <= 10 && !/\b(create|delete|remove|rename|fix|edit|update|modify|change|\u0441\u043e\u0437\u0434\u0430\u0439|\u0443\u0434\u0430\u043b\u0438|\u043f\u0435\u0440\u0435\u0438\u043c\u0435\u043d\u0443\u0439|\u0438\u0441\u043f\u0440\u0430\u0432\u044c|\u043e\u0431\u043d\u043e\u0432\u0438|\u0438\u0437\u043c\u0435\u043d\u0438|\u043f\u043e\u043c\u0435\u043d\u044f\u0439|\u043e\u0442\u0440\u0435\u0434\u0430\u043a\u0442\u0438\u0440\u0443\u0439)\b/.test(value)) {
    return true;
  }
  return false;
}

function isAnalysisOnlyTask(task) {
  const value = normalizeTaskText(task);
  if (isLowSignalTask(value)) return true;

  const alwaysMutationPatterns = [/\b(create|delete|remove|rename)\b/];
  const alwaysMutationTerms = ['\u0441\u043e\u0437\u0434\u0430\u0439', '\u0443\u0434\u0430\u043b\u0438', '\u043f\u0435\u0440\u0435\u0438\u043c\u0435\u043d\u0443\u0439'];
  if (alwaysMutationPatterns.some(p => p.test(value)) || alwaysMutationTerms.some(term => value.includes(term))) return false;

  const conditionalMutationPatterns = [/\b(fix|edit|update|modify|change)\b/];
  const conditionalMutationTerms = ['\u0438\u0441\u043f\u0440\u0430\u0432\u044c', '\u043e\u0431\u043d\u043e\u0432\u0438', '\u0438\u0437\u043c\u0435\u043d\u0438', '\u043f\u043e\u043c\u0435\u043d\u044f\u0439', '\u043e\u0442\u0440\u0435\u0434\u0430\u043a\u0442\u0438\u0440\u0443\u0439'];
  if (conditionalMutationPatterns.some(p => p.test(value)) || conditionalMutationTerms.some(term => value.includes(term))) {
    return !hasExplicitMutationTarget(value);
  }
  return true;
}

module.exports = { isAnalysisOnlyTask, normalizeTaskText, hasExplicitMutationTarget };
