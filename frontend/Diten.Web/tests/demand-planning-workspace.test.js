const fs = require('fs');
const path = require('path');
const { fixture, mount } = require('../wwwroot/assets/js/SupplyChain/demand-planning-preview.js');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const resourceDir = 'Resources/Views/SupplyChain/DemandPlanning';
const locales = ['en', 'tr', 'fr', 'es', 'zh', 'ar', 'ru'];
const text = {
    Title: 'Demand Planning', Company: 'Company', Cycle: 'Cycle', Revision: 'Revision',
    Series: 'Series', State: 'State', Week: 'Week', Quantity: 'Quantity', Origin: 'Origin',
    Reason: 'Reason', SavePreview: 'Apply in preview', SubmitReview: 'Send for review',
    Approve: 'Approve', Reject: 'Reject', NextWeeks: 'Next 13 weeks',
    PreviousWeeks: 'Previous 13 weeks', ShowAllWeeks: 'Show all 52 weeks',
    FirstThirteen: 'Show first 13 weeks', KnownZero: 'explicit zero',
    Missing: 'Missing', Unknown: 'Unknown', Manual: 'Manual',
    InvalidatedWarning: 'Invalidated: historical audit only.',
    ReasonRequired: 'Enter a reason', InvalidQuantity: 'Invalid quantity',
    NoScope: 'Select one fixture company', IncompleteWeeks: 'All 52 weeks required',
    SavedLocally: 'Only preview', NoAuditPermission: 'Separate audit permission'
};

function setup(permissions = { canEdit: true, canReview: true, canConsume: true, canAudit: false }, data = fixture()) {
    document.body.innerHTML = '<div id="app"></div>';
    return mount(document.getElementById('app'), text, permissions, data);
}
const app = () => document.getElementById('app');
const rows = () => app().querySelectorAll('tbody tr');

describe('MOD-0188 tenant workspace fixture preview', () => {
    afterEach(() => { document.body.innerHTML = ''; });

    it('keeps company content hidden until an explicit fixture selection and isolates cycles', () => {
        const ui = setup();
        expect(rows()).toHaveLength(0);
        ui.setCompany('unlisted-company');
        expect(rows()).toHaveLength(0);
        ui.setCompany('fixture-le-a');
        expect(rows()).toHaveLength(13);
        expect(ui.state.cycleId).toBe('fixture-cycle-a');
        ui.setCompany('fixture-le-b');
        expect(ui.state.cycleId).toBe('fixture-cycle-b');
        expect(app().querySelector('[data-testid="cycle"]').value).toBe('fixture-cycle-b');
        expect(ui.state.revisionId).toBe('fixture-other-company');
    });

    it('shows 13 first, navigates to week 52 and can reveal all 52 at once', () => {
        const ui = setup();
        ui.setCompany('fixture-le-a');
        expect(rows()).toHaveLength(13);
        expect(rows()[0].textContent).toContain('2026-10-05');
        app().querySelectorAll('.dp-toolbar button')[1].click();
        expect(rows()[0].textContent).toContain('14');
        app().querySelectorAll('.dp-toolbar button')[2].click();
        expect(rows()).toHaveLength(52);
        expect(rows()[51].textContent).toContain('52');
    });

    it('distinguishes explicit zero, Missing and Unknown without treating gaps as demand', () => {
        const ui = setup();
        ui.setCompany('fixture-le-a');
        expect(rows()[2].textContent).toContain('0 · explicit zero');
        expect(rows()[4].textContent).toContain('Missing');
        expect(rows()[5].textContent).toContain('Unknown');
        expect(rows()[4].textContent).not.toContain('0 · explicit zero');
    });

    it('requires a reason and valid quantity, keeps incomplete Draft out of review', () => {
        const ui = setup();
        ui.setCompany('fixture-le-a');
        expect(ui.saveWeek(5, 'Known', '7', '')).toBe(false);
        expect(ui.saveWeek(5, 'Known', '-1', 'Correction')).toBe(false);
        expect(ui.transition('submit', 'Ready')).toBe(false);
        expect(ui.data.revisions[0].state).toBe('Draft');
        expect(ui.saveWeek(5, 'Known', '0', 'Verified zero')).toBe(true);
        expect(ui.transition('submit', 'Ready')).toBe(false);
        expect(ui.saveWeek(6, 'Known', '9', 'Verified demand')).toBe(true);
        expect(ui.transition('submit', 'Ready')).toBe(true);
        expect(ui.data.revisions[0].state).toBe('InReview');
        expect(ui.transition('approve', 'Same session')).toBe(false);
    });

    it('shows review decisions only for review permission and a separately prepared candidate', () => {
        const ui = setup({ canEdit: false, canReview: true, canAudit: false });
        ui.setCompany('fixture-le-a');
        ui.setRevision('fixture-review');
        expect(app().textContent).toContain('Approve');
        expect(ui.transition('approve', 'Independent check')).toBe(true);
        expect(ui.data.revisions[1].state).toBe('Approved');
        const noReview = setup({ canEdit: false, canReview: false, canAudit: false });
        noReview.setCompany('fixture-le-a');
        noReview.setRevision('fixture-review');
        expect(app().textContent).not.toContain('Approve');
        expect(noReview.transition('approve', 'No permission')).toBe(false);
    });

    it('does not display Invalidated rows without separate audit permission', () => {
        const ui = setup();
        ui.setCompany('fixture-le-a');
        ui.setRevision('fixture-invalidated');
        expect(app().textContent).toContain('Invalidated: historical audit only.');
        expect(rows()).toHaveLength(0);
        const auditor = setup({ canEdit: false, canReview: false, canAudit: true });
        auditor.setCompany('fixture-le-a');
        auditor.setRevision('fixture-invalidated');
        expect(rows()).toHaveLength(13);
        expect(app().textContent).toContain('Invalidated: historical audit only.');
    });

    it('keeps Published status permission separate and Superseded content closed', () => {
        const noConsumer = setup({ canEdit: true, canReview: false, canConsume: false, canAudit: false });
        noConsumer.setCompany('fixture-le-a');
        expect(app().textContent).not.toContain('fixture-published');
        expect(app().textContent).not.toContain('fixture-superseded');
        expect(noConsumer.state.revisionId).toBe('fixture-draft');
        const consumer = setup({ canEdit: false, canReview: false, canConsume: true, canAudit: false });
        consumer.setCompany('fixture-le-a');
        consumer.setRevision('fixture-superseded');
        expect(rows()).toHaveLength(0);
        expect(app().textContent).toContain('SupersededReadOnly');
    });

    it('keeps long labels and a narrow layout scrollable without dropping week access', () => {
        const data = fixture();
        data.companies[0].name = 'A very long authorized fixture company name for mobile review';
        data.revisions[0].series[0].label = 'SKU and warehouse names deliberately much longer than a mobile screen';
        const ui = setup(undefined, data);
        ui.setCompany('fixture-le-a');
        expect(app().textContent).toContain(data.companies[0].name);
        expect(app().textContent).toContain(data.revisions[0].series[0].label);
        expect(read('wwwroot/assets/css/SupplyChain/demand-planning.css')).toMatch(/@media \(max-width: 767px\)/);
        expect(app().querySelector('.dp-table-scroll')).not.toBeNull();
    });
});

describe('MOD-0188 frontend boundaries', () => {
    it('uses only the tenant shell and development-only fixture; never calls 5068', () => {
        const controller = read('Controllers/DemandPlanningController.cs');
        const view = read('Views/SupplyChain/DemandPlanning/Index.cshtml');
        const preview = read('wwwroot/assets/js/SupplyChain/demand-planning-preview.js');
        expect(controller).toContain('[Authorize]');
        expect(controller).toContain('PermissionClaims.HasPermission(User, "demand.plans.read")');
        expect(controller).toContain('environment.IsDevelopment()');
        expect(controller).toContain('demand.plans.consume');
        expect(view).toContain('Layout = "_LayoutTenantShell"');
        expect(view).toContain('<partial name="_AccessDenied"');
        expect(view).toContain('UnavailableTitle');
        expect(preview).not.toMatch(/fetch\(|XMLHttpRequest|5068|Authorization/);
        expect(view).not.toContain('5068');
    });

    it('has complete, nonempty seven-language RESX keys including errors and Invalidated warning', () => {
        const keys = locale => {
            const source = read(resourceDir + '/DemandPlanningIndex.' + locale + '.resx');
            const xml = new DOMParser().parseFromString(source, 'application/xml');
            expect(xml.querySelector('parsererror')).toBeNull();
            const items = [...xml.querySelectorAll('data')];
            expect(items.every(item => item.querySelector('value')?.textContent.trim())).toBe(true);
            return items.map(item => item.getAttribute('name')).sort();
        };
        const baseline = keys('en');
        locales.slice(1).forEach(locale => expect(keys(locale)).toEqual(baseline));
        ['InvalidatedWarning', 'ReasonRequired', 'UnavailableBody', 'NoAuditPermission',
            'Missing', 'KnownZero'].forEach(key => expect(baseline).toContain(key));
        locales.forEach(locale => expect(
            read('Resources/SharedResource.' + locale + '.resx')
        ).toContain('name="Unknown"'));
    });
});
