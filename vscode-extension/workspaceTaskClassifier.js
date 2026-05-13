function normalizeTaskText(task) {
  const normalized = String(task || '')
    .trim()
    .toLowerCase()
    .replace(/[\u2018\u2019]/g, "'")
    .replace(/[\u201c\u201d]/g, '"');
  return normalized.replace(/^['"`]+|['"`]+$/g, '').trim();
}

function hasExplicitMutationTarget(value) {
  if (!value) {
    return false;
  }

  const fileLikePattern = /(^|[\s"'`])(?:[\w.-]+[\\/])*[\w.-]+\.(?:cs|js|ts|json|md|cmd|ps1|csproj|sln|html|css|yml|yaml|xml|txt|config)(?=$|[\s"'`.,;:!?])/i;
  if (fileLikePattern.test(value)) {
    return true;
  }

  const explicitTargetTerms = [
    ' file ', ' files ', ' folder ', ' directory ', ' package ', ' package.json ',
    ' script ', ' test ', ' class ', ' method ', ' function ', ' module ',
    ' файл', ' файла', ' файлы', ' папк', ' package.json', ' скрипт',
    ' тест', ' класс', ' метод', ' функцию', ' модуль'
  ];

  const padded = ` ${value} `;
  return explicitTargetTerms.some(term => padded.includes(term));
}

function isLowSignalTask(value) {
  if (!value) {
    return true;
  }

  const compact = value.replace(/[\s?!.,;:]+/g, '');
  const lowSignalValues = new Set([
    'here', 'there', 'this', 'ok', 'okay', 'hi', 'hello',
    'тут', 'здесь', 'это', 'сюда', 'ок', 'привет', 'алло', 'ало', 'агент'
  ]);

  if (lowSignalValues.has(compact)) {
    return true;
  }

  if (/^(what can you do|explain this project|where is|what is|how does)\b/.test(value)) {
    return true;
  }

  if (/^(что ты умеешь|объясни проект|что тут|где находится|что это|как работает)\b/.test(value)) {
    return true;
  }

  return false;
}

function isAnalysisOnlyTask(task) {
  const value = normalizeTaskText(task);
  if (isLowSignalTask(value)) {
    return true;
  }

  const alwaysMutationPatterns = [
    /\b(create|delete|remove|rename)\b/
  ];
  const alwaysMutationTerms = ['создай', 'удали', 'переименуй'];
  if (alwaysMutationPatterns.some(pattern => pattern.test(value)) || alwaysMutationTerms.some(term => value.includes(term))) {
    return false;
  }

  const conditionalMutationPatterns = [
    /\b(fix|edit|update|modify|change)\b/
  ];
  const conditionalMutationTerms = ['исправь', 'обнови', 'измени', 'поменяй', 'отредактируй'];
  if (conditionalMutationPatterns.some(pattern => pattern.test(value)) || conditionalMutationTerms.some(term => value.includes(term))) {
    return !hasExplicitMutationTarget(value);
  }

  return true;
}

module.exports = {
  isAnalysisOnlyTask,
  normalizeTaskText,
  hasExplicitMutationTarget
};
