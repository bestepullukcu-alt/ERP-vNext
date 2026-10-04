// BL-454 FIX4 — the template editor's "changed after you opened it" refusal, run in the page's own script.
// Response<T> serialises its reason as `reason_code`; round 3 read `reasonCode`, so the sentence never appeared.
const fs = require('fs');
const path = require('path');

const source = fs.readFileSync(
  path.resolve(__dirname, '..', 'wwwroot/assets/js/Platform/NotificationTemplates/form.js'), 'utf8');

const flush = () => new Promise(resolve => setTimeout(resolve, 0));

function page() {
  document.body.innerHTML = `
    <form id="notificationTemplateForm" data-mode="edit" data-template-id="t-1">
      <div id="formErrorSummary" class="d-none"></div>
      <div id="scopeBanner" class="d-none"></div>
      <input id="templateKey"><div class="invalid-feedback" data-valmsg-for="TemplateKey"></div>
      <select id="templateLocale"></select><select id="templateChannel"></select><select id="templateStatus"></select>
      <input id="semanticVersion"><input id="subjectTemplate">
      <textarea id="bodyHtmlTemplate"></textarea><textarea id="bodyTextTemplate"></textarea>
      <div id="variablesEditor"></div><div id="variablesEmptyState"></div>
      <template id="variableRowTemplate"><div class="variable-row"></div></template>
      <button id="btnSaveTemplate" type="submit">Save</button>
    </form>`;
  window.L10n = { TemplateChangedReload: 'Reload and save again.', ErrorOccurred: 'Error.' };
}

function server(saveBody) {
  const calls = [];
  global.fetch = vi.fn(async (url, options = {}) => {
    calls.push({ url, options });
    const json = body => ({ ok: true, status: 200, json: async () => body });
    if (url.includes('/lookups/')) return json({ data: [] });
    if ((options.method || 'GET') === 'GET') {
      return json({ data: { templateKey: 'platform.tasks.assigned', locale: 'en', channel: 'Email', status: 'Active',
        subjectTemplate: 's', bodyHtmlTemplate: '<p>b</p>', bodyTextTemplate: 'b', variables: [], rowVersion: 'AAAAAw==' } });
    }
    return { ok: false, status: 409, json: async () => saveBody };
  });
  return calls;
}

async function openAndSave() {
  new Function(source)();
  document.dispatchEvent(new Event('DOMContentLoaded'));
  await flush(); await flush();
  document.getElementById('notificationTemplateForm')
    .dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
  await flush(); await flush();
}

describe('notification template editor — a save made from an older read', () => {
  it('sends back the version it read and shows the reload sentence for TEMPLATE_CHANGED', async () => {
    page();
    const calls = server({ reason_code: 'TEMPLATE_CHANGED', errors: ['The template was changed after it was opened.'] });

    await openAndSave();

    const save = calls.find(call => call.options.method === 'PUT');
    expect(JSON.parse(save.options.body).rowVersion).toBe('AAAAAw==');
    expect(document.getElementById('formErrorSummary').textContent).toContain('Reload and save again.');
    expect(document.querySelector('[data-valmsg-for="TemplateKey"]').textContent).toBe('');
  });

  it('keeps showing a duplicate-key 409 under the key field', async () => {
    page();
    server({ reason_code: null, errors: ['An active notification template already exists.'] });

    await openAndSave();

    expect(document.querySelector('[data-valmsg-for="TemplateKey"]').textContent).toContain('already exists');
    expect(document.getElementById('formErrorSummary').textContent).not.toContain('Reload and save again.');
  });
});
