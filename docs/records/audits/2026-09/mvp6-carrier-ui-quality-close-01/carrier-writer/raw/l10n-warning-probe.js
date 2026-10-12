const fs = require('fs');
const vm = require('vm');

const sourcePath = process.argv[2];
const source = fs.readFileSync(sourcePath, 'utf8');
const oldSource = source.replace(
  "    const hasLocalizedValue = (dictionary, key) =>\n        Object.prototype.hasOwnProperty.call(dictionary, key)\n        && typeof dictionary[key] === 'string'\n        && dictionary[key].trim().length > 0;\n    const warnMissing = (dictionary) => requiredKeys.forEach((key) => {\n        if (!hasLocalizedValue(dictionary, key))",
  "    const warnMissing = (dictionary) => requiredKeys.forEach((key) => {\n        if (!dictionary[key] || dictionary[key] === key)"
);

const requiredMatch = source.match(/const requiredKeys = \[([\s\S]*?)\];/);
if (!requiredMatch) throw new Error('requiredKeys not found');
const requiredKeys = [...requiredMatch[1].matchAll(/'([^']+)'/g)].map(match => match[1]);
const falsePositiveKeys = ['Active', 'Actions', 'Apply', 'Cancel', 'Filter', 'Passive', 'Reset', 'Save', 'Status', 'Unknown'];
const basePayload = Object.fromEntries(requiredKeys.map(key => [key, `localized-${key}`]));
falsePositiveKeys.forEach(key => { basePayload[key] = key; });

function run(label, script, payload) {
  const warnings = [];
  const sandbox = {
    document: { getElementById: () => ({ textContent: JSON.stringify(payload) }) },
    window: {},
    console: { warn: message => warnings.push(message), error: message => warnings.push(`ERROR:${message}`) }
  };
  vm.runInNewContext(script, sandbox, { filename: `${label}.index.l10n.js` });
  console.log(`${label}\twarning_count=${warnings.length}\t${warnings.join(' | ') || 'none'}`);
  return warnings;
}

const red = run('RED-old-equal-value-check', oldSource, basePayload);
const green = run('GREEN-presence-check', source, basePayload);
const mutation = { ...basePayload };
delete mutation.Save;
const mutant = run('NEGATIVE-missing-Save', source, mutation);

if (red.length !== falsePositiveKeys.length || green.length !== 0 || mutant.length !== 1 || !mutant[0].endsWith('Save')) {
  process.exitCode = 1;
}
