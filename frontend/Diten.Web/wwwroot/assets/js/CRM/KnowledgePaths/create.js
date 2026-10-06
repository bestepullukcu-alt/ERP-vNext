/**
 * WP-KP-UI-1 — new knowledge path: chain (published + branched) + country + language + name / objective / validity.
 * Subject, product, audience and steps are a read-only preview of the chain. PathCode is left empty so CRM issues KP-….
 * Posts to the api/paths proxy and opens the workspace.
 */
(function (window, document) {
    'use strict';
    const S = window.KpStudio;
    const root = document.getElementById('kpCreateRoot');
    if (!S || !root) return;
    const { t, esc, api } = S;

    const form = document.getElementById('kpCreateForm');
    const chainSelect = document.getElementById('kpChain');
    const countrySelect = document.getElementById('kpCountry');
    const languageSelect = document.getElementById('kpLanguage');
    const alert = document.getElementById('kpCreateAlert');
    const submit = document.getElementById('kpCreateSubmit');
    let chainList = [];

    const today = () => new Date().toISOString().slice(0, 10);
    document.getElementById('kpFrom').value = today();

    const preview = () => {
        const chain = chainList.find(c => c.id === chainSelect.value);
        const set = (id, value) => { document.getElementById(id).textContent = value || '—'; };
        set('kpPrevSubject', chain?.subjectName);
        set('kpPrevProduct', chain?.productName);
        set('kpPrevAudience', (chain?.audiences || []).join(', '));
        document.getElementById('kpPrevSteps').innerHTML = chain
            ? chain.branches.map(b => `<div><span class="fw-medium">${esc(b.name)}:</span> ${b.steps.map(s => esc(s.name || '—')).join(' → ')}</div>`).join('')
            : '—';
    };

    const init = async () => {
        try {
            const [chains, countries] = await Promise.all([S.chains(), S.countries()]);
            chainList = chains;
            S.fillOptions(chainSelect, chains.map(c => ({ value: c.id, text: `${c.name} · ${t('VersionShort', c.version)}` })));
            document.getElementById('kpNoChain').classList.toggle('d-none', chains.length > 0);
            S.wireCountryLanguage(countrySelect, languageSelect, countries);
        } catch (error) {
            S.showAlert(alert, error);
        }
    };
    chainSelect.addEventListener('change', preview);

    form.addEventListener('submit', async event => {
        event.preventDefault();
        S.hideAlert(alert);
        const name = document.getElementById('kpName').value.trim();
        const objective = document.getElementById('kpObjective').value.trim();
        const from = document.getElementById('kpFrom').value;
        if (!form.checkValidity() || !name || !objective || !from || !chainSelect.value || !countrySelect.value || !languageSelect.value) {
            form.classList.add('was-validated');
            S.showAlert(alert, new Error(t('RequiredMissing')));
            return;
        }

        const to = document.getElementById('kpTo').value;
        submit.disabled = true;
        try {
            const result = await api.post('/paths', {
                pathCode: '',
                pathName: name,
                subjectId: '00000000-0000-0000-0000-000000000000',
                objective,
                pathVersion: '1.0',
                effectiveFrom: new Date(`${from}T00:00:00`).toISOString(),
                effectiveTo: to ? new Date(`${to}T23:59:59`).toISOString() : null,
                description: document.getElementById('kpDescription').value.trim() || null,
                languageCode: languageSelect.value,
                countryCode: countrySelect.value,
                chainTemplateId: chainSelect.value
            });
            window.location.href = `/CRM/KnowledgePaths/${result.data}`;
        } catch (error) {
            S.showAlert(alert, error);
            submit.disabled = false;
        }
    });

    init();
})(window, document);
