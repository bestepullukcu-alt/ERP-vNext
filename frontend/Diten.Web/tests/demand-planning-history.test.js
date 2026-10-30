const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const flush = () => new Promise(resolve => setTimeout(resolve, 0));

function mount(fetchMock) {
    document.body.innerHTML = `
        <main id="demandHistory">
            <select id="demandHistoryCompany" disabled></select>
            <p id="demandHistoryState"></p>
            <div id="demandHistoryRows"></div>
            <button id="demandHistoryPrevious" disabled></button>
            <button id="demandHistoryNext" disabled></button>
        </main>
        <script id="demandHistoryLabels" type="application/json">
            {"HistoryLoading":"Loading","HistoryEmpty":"Empty","HistoryUnavailable":"Unavailable",
             "HistorySelectCompany":"Select company","HistorySku":"SKU","HistoryWarehouse":"Warehouse",
             "HistoryDate":"Date","HistoryQuantity":"Quantity","HistoryUnit":"Unit",
             "HistoryBatch":"Batch","HistoryDecision":"Decision","HistoryActor":"Actor",
             "HistoryAt":"At","HistoryUnitPending":"Unit not verified by MOD-0290",
             "HistoryUnitMismatch":"Accepted unit differs from the current base unit"}
        </script>`;
    vi.stubGlobal('fetch', fetchMock);
    new Function(read('wwwroot/assets/js/SupplyChain/demand-planning-history.js'))();
}

describe('MOD-0188 accepted history view', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
        document.body.innerHTML = '';
    });

    it('shows a base-unit mismatch without changing the accepted quantity or audit', async () => {
        const fetchMock = vi.fn()
            .mockResolvedValueOnce({ ok: true, json: async () => ({ data: [
                { legalEntityId: 'company-a', displayName: 'Company A' }
            ] }) })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ data: [{
                row: { sourceRecordKey: 'SHIP-1', skuId: 'sku-a', warehouseId: 'wh-a',
                    acceptedQuantity: 3, acceptedUomId: 'BOX', unitVerificationPending: true },
                effectiveUnitVerificationPending: true,
                unitWarning: 'DifferentFromCurrentBase',
                audit: { batchId: 'batch-a', action: 'RowAccepted', actorId: 'actor-a',
                    occurredAt: '2026-10-13T10:00:00Z' }
            }] }) });
        mount(fetchMock);
        await flush();
        await flush();

        const text = document.getElementById('demandHistoryRows').textContent;
        expect(text).toContain('Accepted unit differs from the current base unit');
        expect(text).toContain('BOX');
        expect(text).toContain('3');
        expect(text).toContain('actor-a');
    });

    it('clears the effective warning when the current base matches the immutable accepted unit', async () => {
        const fetchMock = vi.fn()
            .mockResolvedValueOnce({ ok: true, json: async () => ({ data: [
                { legalEntityId: 'company-a', displayName: 'Company A' }
            ] }) })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ data: [{
                row: { sourceRecordKey: 'SHIP-1', skuId: 'sku-a', warehouseId: 'wh-a',
                    acceptedQuantity: 3, acceptedUomId: 'BOX', unitVerificationPending: true },
                effectiveUnitVerificationPending: false,
                audit: { batchId: 'batch-a', action: 'RowAccepted', actorId: 'actor-a',
                    occurredAt: '2026-10-13T10:00:00Z' }
            }] }) });
        mount(fetchMock);
        await flush();
        await flush();

        expect(document.querySelector('.dp-history-unit-pending')).toBeNull();
        expect(document.getElementById('demandHistoryRows').textContent).toContain('BOX');
    });

    it('reads live scoped rows and shows the accepted unit, pending flag and audit', async () => {
        const fetchMock = vi.fn()
            .mockResolvedValueOnce({ ok: true, json: async () => ({ data: [
                { legalEntityId: 'company-a', displayName: 'Company A' }
            ] }) })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ data: [{
                row: { sourceRecordKey: '<source>', skuId: 'sku-a', warehouseId: 'wh-a',
                    acceptedQuantity: 3, acceptedUomId: 'BOX', unitVerificationPending: true },
                audit: { batchId: 'batch-a', action: 'RowAccepted', actorId: 'actor-a',
                    occurredAt: '2026-10-13T10:00:00Z' }
            }] }) });
        mount(fetchMock);
        await flush();
        await flush();

        expect(fetchMock).toHaveBeenCalledTimes(2);
        const scopedUrl = new URL(fetchMock.mock.calls[1][0]);
        expect(scopedUrl.searchParams.get('legalEntityId')).toBe('company-a');
        expect(document.getElementById('demandHistoryRows').textContent)
            .toContain('Unit not verified by MOD-0290');
        expect(document.getElementById('demandHistoryRows').textContent).toContain('BOX');
        expect(document.getElementById('demandHistoryRows').textContent).toContain('actor-a');
        expect(document.querySelector('#demandHistoryRows h2').textContent).toBe('<source>');
        expect(document.querySelector('#demandHistoryRows h2').children).toHaveLength(0);
    });

    it('keeps the history empty and closed when company lookup fails', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 503 });
        mount(fetchMock);
        await flush();
        expect(fetchMock).toHaveBeenCalledTimes(1);
        expect(document.getElementById('demandHistoryRows').children).toHaveLength(0);
        expect(document.getElementById('demandHistoryState').textContent).toBe('Unavailable');
    });

    it('does not render a history shell or load data without read permission', () => {
        const view = read('Views/SupplyChain/DemandPlanning/History.cshtml');
        const controller = read('Controllers/DemandPlanningController.cs');
        expect(view).toContain('<partial name="_AccessDenied"');
        expect(view).toMatch(/else\s*\{\s*<link rel="stylesheet"/);
        expect(view).toContain('@if (canRead)');
        expect(controller).toContain('PermissionClaims.HasPermission(User, "demand.history.read")');
        expect(controller).toContain('X-Legal-Entity-Id');
        expect(controller).not.toContain('5068');
    });
});
