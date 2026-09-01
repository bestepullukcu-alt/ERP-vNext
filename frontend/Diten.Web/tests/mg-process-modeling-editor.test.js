const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

describe('MOD-0355 model/version editor', () => {
  const models = read('Models/ManagementGovernance/ProcessModeling/ProcessModelingFrontendModels.cs');
  const editor = read('Views/ManagementGovernance/ProcessModeling/Editor.cshtml');
  const script = read('wwwroot/assets/js/pages/management-governance/process-modeling/editor.js');
  const form = read('Views/ManagementGovernance/ProcessModeling/_CreateEditOffcanvas.cshtml');

  it('keeps the four-field identity form exact and enables it only through ready/permission state', () => {
    for (const field of ['ProcessDefinitionId', 'ModelCode', 'Name', 'Description']) {
      const type = field === 'ProcessDefinitionId' ? 'Guid?' : field === 'Description' ? 'string?' : 'string';
      expect(models).toContain(`public ${type} ${field}`);
      expect(form).toContain(`asp-for="${field}"`);
    }
    expect(form.match(/asp-for=/g)).toHaveLength(8); // label + input/textarea for four fields
    expect(form).toContain('formProcessModelIdentity');
    expect(form).toContain('ProcessModelExpectedVersion');
    expect(form).toContain('@Html.AntiForgeryToken()');
  });

  it('maps lifecycle controls to exact permission constants without granting authority', () => {
    const mappings = {
      RequestReview: 'management-governance.process-modeling.models.request-review',
      ReturnToDraft: 'management-governance.process-modeling.models.return-to-draft',
      Publish: 'management-governance.process-modeling.models.publish',
      Retire: 'management-governance.process-modeling.models.retire',
      CreateRevision: 'management-governance.process-modeling.models.create-revision'
    };
    for (const [name, permission] of Object.entries(mappings)) {
      expect(models).toContain(`public const string ${name} = "${permission}"`);
      expect(editor).toContain(`Permissions.Contains(ProcessModelingFrontendPermissions.${name})`);
    }
    expect(editor).toContain('data-read-only=');
    expect(editor).not.toMatch(/approve|assign|escalat|start-process|complete-task/i);
  });

  it('renders separate keyboard-addressable graph, activity and control-point workspaces', () => {
    expect(editor).toContain('id="modelGraph"');
    expect(editor).toContain('id="modelActivities"');
    expect(editor).toContain('id="modelControls"');
    expect(editor.match(/tabindex="0"/g)).toHaveLength(3);
    expect(editor).toContain('nav nav-pills d-inline-flex gap-2 flex-wrap');
    expect(editor).toContain('wc-tab-compact');
  });

  it('uses same-origin read/write fetch and distinguishes all closure states', () => {
    expect(script).toContain('/management-governance/process-modeling/api/${path}');
    expect(script).toContain("credentials: 'same-origin'");
    expect(script).not.toMatch(/localhost|:5017|document\.cookie|Authorization|X-Tenant-Id/i);
    expect(script).toContain('RequestVerificationToken: token');
    expect(script).toContain("'Idempotency-Key': crypto.randomUUID()");
    expect(script).toContain('expectedVersion: current.expectedVersion');
    for (const status of [400, 401, 403, 404, 409]) expect(script).toContain(`status === ${status}`);
    expect(script).toContain('L.Error503');
    expect(script).toContain("error.offline ? 'offline'");
  });

  it('maps the exact lifecycle paths and gates them by current state', () => {
    for (const action of ['request-review', 'return-to-draft', 'publish', 'retire', 'create-revision']) {
      expect(editor).toContain(`data-lifecycle-action="${action}"`);
      expect(script).toContain(`'${action}'`);
    }
    expect(script).toContain("current.lifecycle === 'Draft'");
    expect(script).toContain("current.lifecycle === 'Review'");
    expect(script).toContain("current.lifecycle === 'Published'");
    expect(script).toContain("['Published', 'Retired'].includes(current.lifecycle)");
    expect(script).toContain('window.showConfirm?.');
  });

  it('provides keyboard, focus, live-region and RTL-ready markers', () => {
    expect(editor).toContain('aria-live="polite"');
    expect(editor).toContain('tabindex="-1"');
    expect(script).toContain("event.key.toLowerCase() === 's'");
    expect(script).toContain('button.focus()');
    expect(script).toContain('statusHost.focus({ preventScroll: true })');
    expect(editor).not.toMatch(/dir="ltr"/i);
  });

  it('keeps all fourteen localization files key-aligned', () => {
    const languages = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
    const keys = (file) => [...read(file).matchAll(/<data name="([^"]+)"/g)].map(match => match[1]).sort();
    const indexBaseline = keys('Resources/Views/ManagementGovernance/ProcessModeling/ProcessModelingIndex.en.resx');
    const editorBaseline = keys('Resources/Views/ManagementGovernance/ProcessModeling/ProcessModelingEditor.en.resx');
    for (const language of languages) {
      expect(keys(`Resources/Views/ManagementGovernance/ProcessModeling/ProcessModelingIndex.${language}.resx`)).toEqual(indexBaseline);
      expect(keys(`Resources/Views/ManagementGovernance/ProcessModeling/ProcessModelingEditor.${language}.resx`)).toEqual(editorBaseline);
    }
  });
});
