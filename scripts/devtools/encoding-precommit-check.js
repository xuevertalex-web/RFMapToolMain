const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = path.resolve(__dirname, '..', '..');
const exts = new Set(['.cs','.js','.json','.md','.ps1','.cmd']);
const bad = /(Р[А-Яа-яA-Za-z]|С[А-Яа-яA-Za-z]|Ð.|Ñ.)/u;

function getStaged(){
  const out = cp.execSync('git diff --cached --name-only --diff-filter=ACM', {cwd: root, encoding:'utf8'});
  return out.split(/\r?\n/).filter(Boolean).map(f => path.join(root,f));
}

const offenders=[];
for(const file of getStaged()){
  if(!exts.has(path.extname(file).toLowerCase())) continue;
  if(!fs.existsSync(file)) continue;
  const txt = fs.readFileSync(file,'utf8');
  if (bad.test(txt)) offenders.push(path.relative(root,file));
}
if(offenders.length){
  console.error('Mojibake detected in staged files:\n' + offenders.join('\n'));
  process.exit(1);
}
console.log('pre-commit encoding check passed');
