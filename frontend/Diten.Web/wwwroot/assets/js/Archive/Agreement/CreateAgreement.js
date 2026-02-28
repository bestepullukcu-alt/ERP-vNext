/*==================== CONFIG ====================*/
const port2 = protocol === 'https:' ? '5060' : '5053';
const BASE_URL = `${window.ApiBaseUrl}`;

// BE endpoint’leri
const AGREEMENT_CREATE_URL = `${BASE_URL}/services/PvOrganization/Agreement/CreateAgreement`;
const AGREEMENT_ATTACH_URL = `${BASE_URL}/services/PvOrganization/Agreement/UploadAgreementAttachments`;

/*==================== HELPERS ====================*/

async function apiFetch(url, method = "GET", body = null) {
    const opts = { method };
    if (body != null) { opts.headers = { "Content-Type": "application/json" }; opts.body = JSON.stringify(body); }
    const res = await fetch(url, opts);
    if (!res.ok) { const t = await res.text().catch(() => ""); showToast?.(`HTTP ${res.status}`, "error"); throw new Error(t || `Request failed: ${url}`); }
    const ct = res.headers.get("content-type") || "";
    return ct.includes("application/json") ? res.json() : res.text();
}

const toast = (m, t = "info") => (window.showToast ? showToast(m, t) : console.log(t.toUpperCase(), m));
const resetSelect = (el, ph) => el && (el.innerHTML = `<option value="">${ph || "Select"}</option>`);
const setDisabled = (el, on = true) => el && (el.disabled = !!on);
const bytesHuman = (b) => !b ? "0 Bytes" : ((u = ["Bytes", "KB", "MB", "GB", "TB"], i = Math.floor(Math.log(b) / Math.log(1024))), `${(b / Math.pow(1024, i)).toFixed(i ? 1 : 0)} ${u[i]}`);
const AGR_NUM_RE = /^AGR\*[0-9]{4}-[0-9]{3}$/;
const isAgrNumber = (v) => AGR_NUM_RE.test((v || "").trim());
const fireChange = (el) =>
    el && (window.jQuery && $(el).data("select2") ? $(el).trigger("change") : el.dispatchEvent(new Event("change", { bubbles: true })));
function fillSelect(el, items, mapLabel = x => x.name || x.label || x.id, mapVal = x => x.id || x.value) {
    if (!el) return;
    for (const x of items) el.appendChild(new Option(mapLabel(x), mapVal(x)));
    if (items.length === 1) el.value = mapVal(items[0]);
    fireChange(el);
}
function setPageAlert(msg) {
    const box = document.getElementById("pageAlert");
    const txt = document.getElementById("pageAlertText");
    if (!box || !txt) return;
    txt.textContent = msg || "Please fill in the required fields.";
    box.classList.add("show");
}
function clearPageAlert() { document.getElementById("pageAlert")?.classList.remove("show"); }
function extractIdFromResponse(res) { return res?.data ?? res?.id ?? (typeof res === "string" ? res : null); }

/* ---- INVALID HELPERS (Select2-aware) ---- */
function addInvalid(el) {
    if (!el) return;
    el.classList.add("is-invalid");
    if (window.jQuery && $.fn.select2 && $(el).hasClass("select2-hidden-accessible")) {
        $(el).next(".select2").find(".select2-selection").addClass("is-invalid");
    }
}
function clearInvalid(el) {
    if (!el) return;
    el.classList.remove("is-invalid");
    if (window.jQuery && $.fn.select2 && $(el).hasClass("select2-hidden-accessible")) {
        $(el).next(".select2").find(".select2-selection").removeClass("is-invalid");
    }
}
function wireClearOnChange(el) {
    const maybeClear = () => {
        const val = (window.jQuery && $.fn.select2 && $(el).hasClass("select2-hidden-accessible"))
            ? ($(el).val() ?? "")
            : (el?.value ?? "");
        if ((val + "").trim() && !/^Select /i.test(val)) clearInvalid(el);
    };
    if (window.jQuery && $.fn.select2 && $(el).hasClass("select2-hidden-accessible")) {
        $(el).off(".__clearInv")
            .on("select2:select.__clearInv select2:clear.__clearInv change.__clearInv", maybeClear);
    } else {
        el.removeEventListener("input", maybeClear);
        el.removeEventListener("change", maybeClear);
        el.addEventListener("input", maybeClear);
        el.addEventListener("change", maybeClear);
    }
}

/*==================== LOOKUPS ====================*/
async function loadCountries() {
    const sel = document.getElementById("country"); if (!sel) return;
    resetSelect(sel, "Select Country"); setDisabled(sel, true);
    try {
        const res = await apiFetch(`${BASE_URL}/services/PvTenant/Tenant/GetCountriesByTenantId`);
        const list = Array.isArray(res) ? res : res?.data || [];
        const opts = list.map(c => ({ id: c.id, name: c.name || c.countryName || c.id, iso2: String(c.iso2 || "").toUpperCase() }));
        for (const c of opts) { const o = new Option(c.name, c.id); if (c.iso2) o.setAttribute("data-iso2", c.iso2); sel.appendChild(o); }
        fireChange(sel);
    } catch { toast("Countries could not be loaded.", "error"); }
    finally { setDisabled(sel, false); }
}
async function loadAgreementTypes() {
    const sel = document.getElementById("agrType"); if (!sel) return;
    resetSelect(sel, "Select Agreement Type"); setDisabled(sel, true);
    try { const r = await apiFetch(`${BASE_URL}/services/PvOrganization/Agreement/GetAgreementTypes`); fillSelect(sel, Array.isArray(r) ? r : r?.data || []); }
    catch { toast("Agreement types could not be loaded.", "error"); }
    finally { setDisabled(sel, false); }
}
async function loadAgreementSubTypes(typeId) {
    const sel = document.getElementById("agrSubType"); if (!sel) return;
    resetSelect(sel, "Select Sub Type"); if (!typeId) return setDisabled(sel, true);
    setDisabled(sel, true);
    try { const r = await apiFetch(`${BASE_URL}/services/PvOrganization/Agreement/GetAgreementSubTypes/${encodeURIComponent(typeId)}`); fillSelect(sel, Array.isArray(r) ? r : r?.data || []); }
    catch { toast("Sub types could not be loaded.", "error"); }
    finally { setDisabled(sel, false); }
}
async function loadAgreementStatus() {
    const sel = document.getElementById("agrStatus"); if (!sel) return;
    resetSelect(sel, "Select Status"); setDisabled(sel, true);
    try { const r = await apiFetch(`${BASE_URL}/services/PvOrganization/Agreement/GetAgreementStatus`); fillSelect(sel, Array.isArray(r) ? r : r?.data || []); }
    catch { toast("Statuses could not be loaded.", "error"); }
    finally { setDisabled(sel, false); }
}
async function loadPaymentTerms() {
    const sel = document.getElementById("paymentTerms"); if (!sel) return;
    resetSelect(sel, "Select Payment Terms"); setDisabled(sel, true);
    try { const r = await apiFetch(`${BASE_URL}/services/PvOrganization/Agreement/GetPaymentTerms`); fillSelect(sel, Array.isArray(r) ? r : r?.data || []); }
    catch { toast("Payment terms could not be loaded.", "error"); }
    finally { setDisabled(sel, false); }
}
let _companiesCache = null;
async function loadCompanies(ids = []) {
    if (!_companiesCache) {
        const r = await apiFetch(`${BASE_URL}/services/PvOrganization/OrganizationControlller/GetOrganizationsByTenantId`);
        const list = Array.isArray(r) ? r : r?.data || [];
        _companiesCache = list.map(c => ({ id: c.id, name: c.companyName || c.name || c.id }));
    }
    ids.forEach(id => { const el = document.getElementById(id); if (!el) return; resetSelect(el, "Select Company"); setDisabled(el, true); fillSelect(el, _companiesCache); setDisabled(el, false); });
}
let _rolesCache = null;
async function loadRoles(ids = []) {
    if (!_rolesCache) {
        const r = await apiFetch(`${BASE_URL}/services/PvTenant/Role/GetActiveRolesByTenantId`);
        const list = Array.isArray(r) ? r : r?.data || [];
        _rolesCache = list.map(x => ({ id: x.id, name: x.name || x.id }));
    }
    ids.forEach(id => { const el = document.getElementById(id); if (!el) return; resetSelect(el, "Select Role"); setDisabled(el, true); fillSelect(el, _rolesCache); setDisabled(el, false); });
}
async function loadGoverningLaw() {
    const s = document.getElementById("governingLaw"); if (!s) return; resetSelect(s, "Select Governing Law"); setDisabled(s, true);
    try { const r = await apiFetch(`${BASE_URL}/services/PvOrganization/Agreement/GetGoverningLaw`); fillSelect(s, Array.isArray(r) ? r : r?.data || []); }
    catch { toast("Governing law could not be loaded.", "error"); }
    finally { setDisabled(s, false); }
}
async function loadConfidentialityClause() {
    const s = document.getElementById("confidentiality"); if (!s) return; resetSelect(s, "Select Confidentiality Clause"); setDisabled(s, true);
    try { const r = await apiFetch(`${BASE_URL}/services/PvOrganization/Agreement/GetConfidentialityClause`); fillSelect(s, Array.isArray(r) ? r : r?.data || []); }
    catch { toast("Confidentiality clause could not be loaded.", "error"); }
    finally { setDisabled(s, false); }
}
async function loadRenewalTerms() {
    const s = document.getElementById("renewalTerms"); if (!s) return; resetSelect(s, "Select Renewal Terms"); setDisabled(s, true);
    try { const r = await apiFetch(`${BASE_URL}/services/PvOrganization/Agreement/GetRenewalTerms`); fillSelect(s, Array.isArray(r) ? r : r?.data || []); }
    catch { toast("Renewal terms could not be loaded.", "error"); }
    finally { setDisabled(s, false); }
}
async function loadCorrespondentBankRequirement() {
    const s = document.getElementById("correspondentBankSel"); if (!s) return; resetSelect(s, "Select Correspondent Bank"); setDisabled(s, true);
    try { const r = await apiFetch(`${BASE_URL}/services/PvOrganization/Agreement/GetCorrespondentBankRequirement`); fillSelect(s, Array.isArray(r) ? r : r?.data || []); }
    catch { toast("Correspondent bank requirement could not be loaded.", "error"); }
    finally { setDisabled(s, false); }
}
async function loadCurrenciesByIso2(iso2) {
    const sel = document.getElementById("currency"); if (!sel) return;
    resetSelect(sel, "Select Currency"); if (!iso2) return setDisabled(sel, true);
    setDisabled(sel, true);
    try {
        const data = await apiFetch(`https://restcountries.com/v3.1/alpha/${encodeURIComponent(iso2.toUpperCase())}?fields=currencies`);
        const obj = (Array.isArray(data) ? data[0] : data)?.currencies || {};
        const items = Object.entries(obj).map(([code, info]) => ({ id: code, name: `${info?.name || code} (${code})` }));
        fillSelect(sel, items);
    } catch { toast("Currency could not be loaded.", "error"); }
    finally { setDisabled(sel, false); }
}

/*==================== ATTACHMENTS ====================*/
const Attachments = (() => {
    let files = [];
    const MAX = 20 * 1024 * 1024;
    const els = { drop: null, input: null, list: null, btn: null, cnt: null, total: null };
    const keyOf = f => `${f.name}__${f.size}__${f.lastModified}`;

    function render() {
        if (els.list) {
            els.list.innerHTML = files.map(f => `
        <div class="att-item" data-key="${f.key}">
          <div class="meta"><i class="bx bx-paperclip"></i><span class="name">${f.name}</span>
            <small class="text-muted ms-2">${bytesHuman(f.size)}</small></div>
          <button type="button" class="btn btn-outline-danger btn-sm rm">Remove</button>
        </div>`).join("");
            els.list.querySelectorAll(".rm").forEach(b => b.onclick = () => remove(b.closest(".att-item").dataset.key));
        }
        els.cnt && (els.cnt.textContent = String(files.length));
        els.total && (els.total.textContent = bytesHuman(files.reduce((s, x) => s + x.size, 0)));
    }
    function add(fileList) {
        const arr = Array.from(fileList || []); let added = 0;
        for (const f of arr) {
            if (f.size > MAX) { toast(`${f.name}: exceeds 20MB, skipped.`, "warning"); continue; }
            const key = keyOf(f); if (files.some(x => x.key === key)) continue;
            files.push({ key, name: f.name, size: f.size, file: f }); added++;
        }
        added && render();
    }
    function remove(key) { files = files.filter(x => x.key !== key); render(); }
    function bindDnD() {
        const d = els.drop; if (!d) return;
        const prevent = e => { e.preventDefault(); e.stopPropagation(); };
        ["dragenter", "dragover", "dragleave", "drop"].forEach(ev => d.addEventListener(ev, prevent));
        d.addEventListener("dragover", () => d.classList.add("bg-light"));
        d.addEventListener("dragleave", () => d.classList.remove("bg-light"));
        d.addEventListener("drop", e => { d.classList.remove("bg-light"); add(e.dataTransfer.files); });
    }
    function init() {
        els.drop = document.getElementById("attDropArea");
        els.input = document.getElementById("fileUpload");
        els.btn = document.getElementById("chooseFileBtn");
        els.list = document.getElementById("attList");
        els.cnt = document.getElementById("attFileCount");
        els.total = document.getElementById("attTotalSize");
        els.btn && (els.btn.onclick = () => els.input?.click());
        els.input && (els.input.onchange = e => { add(e.target.files); e.target.value = ""; });
        bindDnD(); render();
    }
    const getFiles = () => files.map(f => f.file);
    return { init, getFiles };
})();

/*==================== PARTIES ====================*/
const Parties = (() => {
    const state = { init: [], rec: [] };
    const rows = {
        init: { c: "company", r: "initiatingRole", btn: "btnAddPartyInit", list: "selectedPartiesInit" },
        rec: { c: "company2", r: "receivingRole", btn: "btnAddPartyRec", list: "selectedPartiesRec" }
    };

    const getVal = el => {
        if (!el) return "";
        if (window.jQuery && $.fn.select2 && $(el).hasClass("select2-hidden-accessible")) {
            const v = $(el).val();
            // select2 çokluysa dizi gelebilir; ilkini al
            const s = Array.isArray(v) ? (v[0] ?? "") : (v ?? "");
            return (s + "").trim();
        }
        return (el.value ?? "").trim();
    };

    const getText = el => el?.options?.[el.selectedIndex]?.text?.trim?.() || "";

    const toNumIfNumeric = v => (/^\d+$/.test(v) ? Number(v) : v); // backend int bekliyorsa yardımcı

    function draw(k) {
        const box = document.getElementById(rows[k].list);
        if (!box) return;
        box.innerHTML = state[k].map(p => `
      <span class="party-chip" data-k="${k}" data-id="${p.id}" data-company-id="${p.companyId}" data-role-id="${p.roleId}">
        ${p.label}<button class="x" aria-label="Remove">&times;</button>
      </span>`).join("");
    }

    function syncBtn(k) {
        const { c, r, btn } = rows[k];
        const ce = document.getElementById(c), re = document.getElementById(r);
        const b = document.getElementById(btn); if (!b) return;
        const ok = !!getVal(ce) && !!getVal(re);
        b.disabled = !ok;
        b.classList.toggle("disabled", !ok);
    }

    function add(k) {
        const { c, r } = rows[k];
        const ce = document.getElementById(c), re = document.getElementById(r);
        const companyIdRaw = getVal(ce), roleIdRaw = getVal(re);
        if (!(companyIdRaw && roleIdRaw)) return;

        const companyId = toNumIfNumeric(companyIdRaw);
        const roleId = toNumIfNumeric(roleIdRaw);

        const label = `${getText(ce)} (${getText(re)})`;
        if (state[k].some(x => x.companyId == companyId && x.roleId == roleId)) return;

        state[k].push({ id: Math.random().toString(36).slice(2, 9), label, companyId, roleId });
        draw(k); syncBtn(k);
    }

    function removeChip(e) {
        // DÜZELTİLDİ: önce buton .x mi bak, sonra chip’i bul
        const btn = e.target;
        if (!btn.classList || !btn.classList.contains("x")) return;
        const chip = btn.closest(".party-chip"); if (!chip) return;
        const k = chip.dataset.k, id = chip.dataset.id;
        state[k] = state[k].filter(p => p.id !== id);
        draw(k); syncBtn(k);
    }

    function bind() {
        Object.keys(rows).forEach(k => {
            const { c, r, btn } = rows[k];
            const ce = document.getElementById(c), re = document.getElementById(r);
            ce && ce.addEventListener("change", () => syncBtn(k));
            re && re.addEventListener("change", () => syncBtn(k));
            document.getElementById(btn)?.addEventListener("click", () => add(k));
            if (window.jQuery) $(`#${c},#${r}`).on("select2:select select2:clear change", () => syncBtn(k));
            syncBtn(k); draw(k);
        });
        document.addEventListener("click", removeChip);
    }

    const list = (k) => state[k].map((p, i) => ({
        CompanyId: p.companyId,
        RoleId: p.roleId,
        SortOrder: i
    }));

    // İsteyen dışarıdan otomatik eklesin diye expose et
    return { bind, list, _state: state, _add: add };
})();

/*==================== VALIDATION ====================*/
function validateRequired(ids, labels, prefix) {
    const missing = []; let first = null;
    ids.forEach((id, i) => {
        const el = document.getElementById(id);
        const v = (el?.value || "").trim();
        if (!v || /^Select /i.test(v)) {
            missing.push(labels[i]);
            addInvalid(el);
            wireClearOnChange(el);
            if (!first) first = el;
        }
    });
    if (missing.length) {
        setPageAlert(`${prefix || "Please fill in the required fields"}: ${missing.join(", ")}`);
        try { first?.scrollIntoView({ behavior: "smooth", block: "center" }); } catch { }
        return false;
    }
    clearPageAlert();
    return true;
}
function validateStep1() {
    const ok = validateRequired(
        ["agrType", "agrSubType", "agrNumber", "agrStatus", "startDate", "country", "currency"],
        ["Agreement Type", "Sub Type", "Agreement Number", "Agreement Status", "Start Date", "Country / Region", "Currency"]
    );
    if (!ok) return false;

    const numEl = document.getElementById("agrNumber");
    const val = (numEl?.value || "").trim();
    if (!isAgrNumber(val)) {
        addInvalid(numEl);
        wireClearOnChange(numEl);
        setPageAlert("Agreement Number must match: AGR*YYYY-NNN (e.g., AGR*2025-001)");
        return false;
    }
    return true;
}
function validateStep2() {
    const initCnt = document.querySelectorAll("#selectedPartiesInit .party-chip").length;
    const recCnt = document.querySelectorAll("#selectedPartiesRec .party-chip").length;
    if (!initCnt || !recCnt) { setPageAlert("Please add at least one Initiating and one Receiving party."); return false; }
    clearPageAlert(); return true;
}
function validateAll() {
    if (!validateStep1()) return { ok: false, step: 1 };
    if (!validateStep2()) return { ok: false, step: 2 };
    return { ok: true };
}

/*==================== CREATE + UPLOAD ====================*/
// tüm alanları null/boş ise objeyi null yapan helper
function nullIfEmpty(obj) {
    if (!obj || Array.isArray(obj)) return obj; // sadece düz objeler için
    const vals = Object.values(obj);
    const allNullish = vals.every(v =>
        v === null ||
        v === undefined ||
        (typeof v === "string" && v.trim() === "")
    );
    return allNullish ? null : obj;
}

function buildCreateRequest(isDraft) {
    const userName = window.getUserName();
    const userId = window.getUserId();

    const gv = id => (document.getElementById(id)?.value ?? "").trim() || null;

    const parties = [
        ...Parties.list("init").map(p => ({ ...p, Kind: 1 })),
        ...Parties.list("rec").map(p => ({ ...p, Kind: 2 })),
    ];

    const Head = nullIfEmpty({
        AgreementTypeId: gv("agrType"),
        AgreementSubTypeId: gv("agrSubType"),
        AgreementNumber: gv("agrNumber"),
        AgreementStatusId: gv("agrStatus"),
        StartDate: gv("startDate"),
        EndDate: gv("endDate"),
        CountryId: gv("country"),
        CurrencyCode: gv("currency"),
        ValueNet: gv("valueNet") ? Number(gv("valueNet")) : null,
        ValueGross: gv("valueGross") ? Number(gv("valueGross")) : null,
        PaymentTermsId: gv("paymentTerms"),
        DiscountPercent: gv("discount") ? Number(gv("discount")) : null
    });

    const Contact = nullIfEmpty({
        ResponsiblePerson: gv("respPerson"),
        DepartmentId: gv("department"),
        ContactEmail: gv("contactEmail"),
        ContactPhone: gv("contactPhone")
    });

    const Specifics = nullIfEmpty({
        LeadTimeDays: gv("leadTime") ? Number(gv("leadTime")) : null,
        MinimumOrderQty: gv("minOrderQty") ? Number(gv("minOrderQty")) : null,
        DeliveryFrequency: gv("deliveryFreq"),
        SupplierRating: gv("supplierRating") ? Number(gv("supplierRating")) : null
    });

    const Financial = nullIfEmpty({
        BankId: gv("bankName"),
        BankAccountNumber: gv("bankAccount"),
        SwiftCode: gv("swift"),
        CorrespondentBank: gv("correspondentBank"),
        CorrespondentAccountNumber: gv("correspondentAcc")
    });

    const Legal = nullIfEmpty({
        GoverningLawId: gv("governingLaw"),
        ConfidentialityClauseId: gv("confidentiality"),
        RenewalTermsId: gv("renewalTerms"),
        CorrespondentBankRequirementId: gv("correspondentBankSel"),
        AuditClause: gv("auditClause")
    });

    const Delivery = nullIfEmpty({
        Address: gv("deliverAddress"),
        IncotermsId: gv("incoterms"),
        ConsignmentOperator: gv("consignmentOperator"),
        DeliveryContact: gv("deliveryContact"),
        DeliveryPhone: gv("deliveryPhone"),
        DeliveryEmail: gv("deliveryEmail"),
        ExtraId: gv("deliveryExtra")
    });

    return {
        UserId: userId,
        IsDraft: !!isDraft,
        Head,                                         // Head zaten dolu alanlar içeriyor
        Parties: parties.length ? parties : null,     // tamamen boşsa null gönder
        Contact,
        Specifics,
        Financial,
        Legal,
        Delivery,
        Note: gv("attDeliverAddress"),
        CreatedBy: userName
    };
}

async function uploadAttachments(agreementId, files) {

    const userName = window.getUserName();
    const userId = window.getUserId();

    if (!agreementId || !files?.length) return;
    const fd = new FormData();
    fd.append("AgreementId", agreementId);
    fd.append("UserId", userId || "");
    fd.append("CreatedBy", userName || "");
    for (const f of files) fd.append("Files", f, f.name); // List<IFormFile> -> Files
    const res = await fetch(AGREEMENT_ATTACH_URL, { method: "POST", body: fd });
    if (!res.ok) {
        const t = await res.text().catch(() => "");
        throw new Error(t || "Attachment upload failed");
    }
}

/*==================== WIZARD + INIT ====================*/
document.addEventListener("DOMContentLoaded", async () => {
    const sections = [...document.querySelectorAll(".wizard-section")];
    const steps = [...document.querySelectorAll(".wizard-top .wizard-step")];
    const btnPrev = document.getElementById("btnPrev");
    const btnNext = document.getElementById("btnNext");
    const btnCreate = document.getElementById("btnCreate");
    const btnDraft = document.getElementById("btnSaveDraft");
    const btnAutoNo = document.getElementById("btnAutoNumber");
    const form = document.getElementById("agreementForm");
    if (!sections.length || !form) return;

    const LAST = sections.length; let current = 1;
    function show(step) {
        current = Math.max(1, Math.min(LAST, step));
        sections.forEach(s => s.classList.toggle("d-none", Number(s.dataset.step) !== current));
        steps.forEach(d => { const n = Number(d.dataset.step); d.classList.toggle("active", n === current); d.classList.toggle("done", n < current); });
        btnPrev.disabled = current === 1;
        const isLast = current === LAST;
        btnNext.classList.toggle("d-none", isLast);
        btnCreate.classList.toggle("d-none", !isLast);
        clearPageAlert(); window.scrollTo({ top: 0, behavior: "auto" });
    }

    steps.forEach(d => {
        d.style.cursor = "pointer";
        d.addEventListener("click", () => {
            const target = Number(d.dataset.step);
            if (target > current) {
                if (current === 1 && !validateStep1()) return;
                if (current === 2 && !validateStep2()) return;
            }
            show(target);
        });
    });

    btnPrev?.addEventListener("click", () => show(current - 1));
    btnNext?.addEventListener("click", () => {
        if (current === 1 && !validateStep1()) return;
        if (current === 2 && !validateStep2()) return;
        show(current + 1);
    });

    btnAutoNo?.addEventListener("click", () => {
        const el = document.getElementById("agrNumber");
        if (!el) return;
        el.value = `AGR*${new Date().getFullYear()}-${Math.floor(Math.random() * 900 + 100)}`;
        clearInvalid(el);
        wireClearOnChange(el);
        fireChange(el);
    });

    // Dependent selects
    const typeSel = document.getElementById("agrType");
    typeSel && (typeSel.onchange = function () { loadAgreementSubTypes(this.value?.trim()); });
    const countrySel = document.getElementById("country");
    countrySel && (countrySel.onchange = function () {
        const iso2 = this.selectedOptions?.[0]?.getAttribute("data-iso2");
        if (!iso2) { resetSelect(document.getElementById("currency"), "Select Currency"); setDisabled(document.getElementById("currency"), true); return; }
        loadCurrenciesByIso2(iso2);
    });

    // Lookupları yükle
    setDisabled(document.getElementById("agrSubType"), true);
    setDisabled(document.getElementById("currency"), true);
    await Promise.allSettled([
        loadCountries(), loadAgreementTypes(), loadAgreementStatus(), loadPaymentTerms(),
        loadCompanies(["company", "company2"]), loadRoles(["initiatingRole", "receivingRole"]),
        loadGoverningLaw(), loadRenewalTerms(), loadConfidentialityClause(), loadCorrespondentBankRequirement()
    ]);

    Parties.bind();
    Attachments.init();
    show(1);

    // Taslak kaydet
    btnDraft?.addEventListener("click", async () => {
        try {
            const payload = buildCreateRequest(true);
            const res = await apiFetch(AGREEMENT_CREATE_URL, "POST", payload);

            const id = extractIdFromResponse(res);
            const files = Attachments.getFiles?.() ?? [];
            if (id && files.length) await uploadAttachments(id, files);
            toast("Draft saved.", "success");
            setTimeout(() => {
                window.location.href = `/Agreement/Index`;
            }, 700);
        } catch (e) { toast(e.message || "Draft save failed.", "error"); }
    });

    // Final oluştur
    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const v = validateAll();
        if (!v.ok) { show(v.step); return; }
        try {
            const payload = buildCreateRequest(false);
            const res = await apiFetch(AGREEMENT_CREATE_URL, "POST", payload);
            const id = extractIdFromResponse(res);
            const files = Attachments.getFiles?.() ?? [];
            if (id && files.length) await uploadAttachments(id, files);
            toast("Agreement created.", "success");
            setTimeout(() => {
                window.location.href = `/Agreement/Index`;
            }, 700);

        } catch (err) { toast(err.message || "Create failed.", "error"); }
    });
});

/*==================== TOAST UI ====================*/
function showToast(message, type = "success") {
    const toastEl = document.getElementById("appToast");
    const toastBody = toastEl?.querySelector(".toast-body");
    const toastHeader = toastEl?.querySelector("#appToastHeader");
    if (!toastEl || !toastBody || !toastHeader) return;
    toastBody.innerHTML = message;
    toastEl.classList.remove("bg-success", "bg-danger", "bg-warning", "bg-info");
    switch (type) {
        case "success": toastEl.classList.add("bg-success"); toastHeader.textContent = "Successful"; break;
        case "error": toastEl.classList.add("bg-danger"); toastHeader.textContent = "Error"; break;
        case "warning": toastEl.classList.add("bg-warning"); toastHeader.textContent = "Warning"; break;
        default: toastEl.classList.add("bg-info"); toastHeader.textContent = "Info"; break;
    }
    bootstrap.Toast.getOrCreateInstance(toastEl).show();
}
