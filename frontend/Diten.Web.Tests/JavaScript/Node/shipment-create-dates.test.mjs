// The two schedule fields of the Shipment form, run by Node: the SHIPPED create.js and diten-datefield.js against fields whose
// classes are read from the SHIPPED _Form.cshtml, so a class that leaves `.flatpickr-date` goes red here.
//
//   node --test frontend/Diten.Web.Tests/JavaScript/Node/shipment-create-dates.test.mjs
// ShipmentDetailsDateNodeTests.cs runs it from `dotnet test`. flatpickr itself is a double; its own calendar is not drawn here.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const read = (file) => readFileSync(path.join(repo, file), 'utf8');
const form = read('frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml');

const fieldFor = (id) => {
    const match = form.match(new RegExp(`<i class="bx [^"]*diten-field-icon"[^>]*></i><input id="${id}" class="([^"]*)"`));
    assert.ok(match, `${id} is not in _Form.cshtml with its icon`);
    const icon = { handlers: [], addEventListener(name, handler) { this.handlers.push([name, handler]); } };
    const node = {
        id, classes: match[1].split(' '), value: '', opened: 0,
        flatpickr(config) { this._flatpickr = { config, open: () => { node.opened += 1; } }; },
        closest: () => ({ querySelector: () => icon })
    };
    return { node, icon };
};
const fields = [fieldFor('plannedShipAt'), fieldFor('plannedDeliverAt')];
const elements = new Map(fields.map(({ node }) => [node.id, node]));
const document = {
    getElementById: (id) => elements.get(id) ?? null,
    addEventListener: () => {},
    querySelector: () => null,
    querySelectorAll: (selector) => (selector === '.flatpickr-date' ? fields.map(({ node }) => node).filter((node) => node.classes.includes('flatpickr-date')) : [])
};
const context = vm.createContext({ document, window: { L10n: {} }, crypto, console });
context.globalThis = context;
vm.runInContext(read('frontend/Diten.Web/wwwroot/assets/js/shared/diten-datefield.js').replace("typeof window !== 'undefined' ? window : globalThis", 'window'), context);
const create = vm.runInContext(`${read('frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js')}\n;ShipmentCreate`, context);

test('both fields get a date and time flatpickr', () => {
    assert.equal(create.enhanceDates(), 2);
    fields.forEach(({ node }) => {
        assert.equal(node._flatpickr.config.enableTime, true);
        assert.equal(node._flatpickr.config.dateFormat, 'Y-m-d H:i');
    });
});

test('each icon opens its own calendar', () => {
    fields.forEach(({ node, icon }) => {
        const click = icon.handlers.find(([name]) => name === 'click')?.[1];
        assert.ok(click, `${node.id}: the icon is not bound`);
        click();
        assert.equal(node.opened, 1);
    });
});

test('the value the picker writes reaches the payload as the same instant in UTC', () => {
    elements.get('plannedShipAt').value = '2026-11-02 10:30';
    elements.get('plannedDeliverAt').value = '';
    const body = create.payload();
    assert.equal(body.plannedShipAt, new Date(2026, 10, 2, 10, 30).toISOString());
    assert.equal(body.plannedDeliverAt, null);
});
