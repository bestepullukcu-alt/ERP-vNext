import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);
const languages = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
const required = ['InitiativesTitle', 'InitiativeType', 'Priority', 'PlannedStartDate', 'PlannedEndDate', 'ClassificationUnavailable', 'LifecycleUnavailable', 'ValidationError', 'Unauthorized', 'Forbidden', 'NotFound', 'Conflict', 'DependencyUnavailable', 'CreateSuccessor', 'CompletionSummary', 'ClosureReason', 'BenefitDisposition'];
for (const language of languages) {
  const xml = await readFile(new URL(`Resources/Views/PPM/Initiatives/InitiativesIndex.${language}.resx`, root), 'utf8');
  for (const key of required) assert.match(xml, new RegExp(`name="${key}"`), `${language}: ${key}`);
}
const bridge = await readFile(new URL('Views/PPM/Initiatives/_IndexL10n.cshtml', root), 'utf8');
assert.match(bridge, /window\.L10n|ppm-initiative-l10n/);
console.log('ppm initiative localization contract: PASS');
