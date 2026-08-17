(() => {
    const root = document.getElementById("demand-capture-root");
    const form = document.getElementById("demand-capture-form");
    if (!root || !form) {
        // eslint-disable-next-line no-console
        console.error("demandIdeaCapture: missing #demand-capture-root or #demand-capture-form — buttons will not work.");
        return;
    }

    const apiBase = (root.dataset.apiBase || "").replace(/\/$/, "");
    /** When apiBase is empty, requests use same-origin /api/v1 (API hosted in WebUI). */
    const apiServiceHint = "Demand API unavailable — ensure MongoDB is running, ConnectionStrings:MongoDb and DatabaseName are correct, then restart Diten.WebUI (see README).";
    let recordId = null;
    let recordStatus = "Draft";
    let meta = null;
    /** Linked demand idea IDs (persisted as relatedIdeaIds). */
    let relatedIdeaIds = [];

    const guidRe = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    const normalizeGuid = (raw) => {
        const s = String(raw || "").trim();
        if (!guidRe.test(s)) return null;
        return s.toLowerCase();
    };

    /** Mirrors backend metadata defaults — used when API is offline or returns empty/mismatched JSON */
    const DEFAULT_DEMAND_META = {
        requestTypes: ["Process Improvement", "Program", "Enhancement", "Compliance", "New Capability", "Platform", "Infrastructure", "Regulatory"],
        strategicAlignments: ["Growth & Revenue", "Operational Excellence", "Digital Transformation", "Cost Reduction", "Customer Experience", "Risk & Compliance", "Innovation & R&D"],
        businessUnits: ["IT", "HR", "Risk", "Operations", "Engineering", "Security", "Sales", "Finance", "Corporate", "Legal", "Product"],
        categories: [
            "Business application",
            "Technology",
            "Infrastructure & platform",
            "Data & analytics",
            "Security & privacy",
            "Customer experience",
            "Regulatory / compliance",
            "Operations",
            "Product innovation",
            "Program / initiative",
            "Other"
        ],
        demandSources: [
            "Portfolio intake",
            "Business unit request",
            "Internal business request",
            "Executive / steering committee",
            "Customer / partner",
            "Regulatory / audit finding",
            "Incident / problem management",
            "Innovation lab / hackathon",
            "Vendor / contract",
            "Other"
        ],
        priorities: ["Low", "Medium", "High", "Critical"],
        complianceImpacts: ["None", "Low", "Medium", "High", "Critical"],
        estimatedComplexities: ["Trivial", "Low", "Medium", "High", "Very high"],
        riskSensitivities: ["Very low", "Low", "Medium", "High", "Critical"]
    };

    const cloneDefaultMeta = () => {
        const o = {};
        Object.keys(DEFAULT_DEMAND_META).forEach((k) => {
            o[k] = [...DEFAULT_DEMAND_META[k]];
        });
        return o;
    };

    const normalizeMetaFromApi = (raw) => {
        if (!raw || typeof raw !== "object") return {};
        const pick = (camel, pascal) => {
            const a = raw[camel];
            const b = raw[pascal];
            if (Array.isArray(a)) return a.filter((x) => typeof x === "string" && x.length);
            if (Array.isArray(b)) return b.filter((x) => typeof x === "string" && x.length);
            return [];
        };
        return {
            requestTypes: pick("requestTypes", "RequestTypes"),
            strategicAlignments: pick("strategicAlignments", "StrategicAlignments"),
            businessUnits: pick("businessUnits", "BusinessUnits"),
            categories: pick("categories", "Categories"),
            demandSources: pick("demandSources", "DemandSources"),
            priorities: pick("priorities", "Priorities"),
            complianceImpacts: pick("complianceImpacts", "ComplianceImpacts"),
            estimatedComplexities: pick("estimatedComplexities", "EstimatedComplexities"),
            riskSensitivities: pick("riskSensitivities", "RiskSensitivities")
        };
    };

    const mergeMetaWithDefaults = (normalized) => {
        const out = {};
        Object.keys(DEFAULT_DEMAND_META).forEach((k) => {
            const arr = normalized[k];
            out[k] = Array.isArray(arr) && arr.length ? arr : [...DEFAULT_DEMAND_META[k]];
        });
        return out;
    };

    const applyMetaToSelects = () => {
        if (!meta) return;
        fillSelect("cap-reqtype", meta.requestTypes);
        fillSelect("cap-strategic", meta.strategicAlignments);
        fillSelect("cap-bu", meta.businessUnits);
        fillSelect("cap-category", meta.categories);
        fillSelect("cap-demand-source", meta.demandSources);
        fillSelect("cap-priority", meta.priorities);
        fillSelect("cap-compliance", meta.complianceImpacts);
        fillSelect("cap-complexity", meta.estimatedComplexities);
        fillSelect("cap-risk", meta.riskSensitivities);
    };

    const el = {
        validationSummary: document.getElementById("dc-validation-summary"),
        alert: document.getElementById("dc-api-alert"),
        loading: document.getElementById("dc-loading-indicator"),
        recordDisplay: document.getElementById("dc-record-display"),
        statusBadge: document.getElementById("dc-status-badge"),
        btnDraft: document.getElementById("demand-cap-draft"),
        btnSubmit: document.getElementById("demand-cap-submit"),
        relatedList: document.getElementById("related-ideas-list"),
        relatedEmpty: document.getElementById("related-ideas-empty"),
        attachments: document.getElementById("cap-attachments-list"),
        dropzone: document.getElementById("cap-dropzone"),
        fileInput: document.getElementById("cap-file-input"),
        linksList: document.getElementById("cap-links-list"),
        tagInput: document.getElementById("cap-tag-input"),
        tagsVisual: document.getElementById("demand-tags-visual")
    };

    const fieldIds = {
        title: "cap-title",
        problemStatement: "cap-problem",
        expectedOutcome: "cap-outcome",
        requestType: "cap-reqtype",
        businessUnit: "cap-bu",
        requestor: "cap-requestor",
        category: "cap-category",
        demandSource: "cap-demand-source",
        priority: "cap-priority",
        complianceImpact: "cap-compliance",
        estimatedComplexity: "cap-complexity",
        riskSensitivity: "cap-risk"
    };

    const fieldToSection = {
        title: "core",
        problemStatement: "core",
        expectedOutcome: "core",
        requestType: "core",
        businessUnit: "core",
        requestor: "core",
        category: "class",
        demandSource: "class",
        priority: "class",
        complianceImpact: "class",
        estimatedComplexity: "class",
        riskSensitivity: "class"
    };

    const setSectionErrorState = (fieldKeys, on) => {
        const sections = new Set();
        (fieldKeys || []).forEach((k) => {
            const camel = k.charAt(0).toLowerCase() + k.slice(1);
            const sec = fieldToSection[camel];
            if (sec) sections.add(sec);
        });
        document.querySelectorAll(".dc-section[data-section]").forEach((s) => {
            const id = s.getAttribute("data-section");
            s.classList.toggle("dc-section--has-error", on && id && sections.has(id));
        });
    };

    const notify = (message, kind = "success") => {
        if (window.Notiflix?.Notify) {
            if (kind === "error") Notiflix.Notify.failure(message);
            else if (kind === "warning") Notiflix.Notify.warning(message);
            else Notiflix.Notify.success(message);
            return;
        }
        if (window.Swal) {
            Swal.fire({ title: message, icon: kind === "error" ? "error" : kind === "warning" ? "warning" : "success", timer: 2600, showConfirmButton: false, toast: true, position: "top-end" });
            return;
        }
        // eslint-disable-next-line no-alert
        alert(message);
    };

    const showAlert = (kind, html) => {
        if (!el.alert) return;
        el.alert.className = `alert border-0 shadow-sm mb-3 alert-${kind === "danger" ? "danger" : kind === "warning" ? "warning" : "info"}`;
        el.alert.innerHTML = html;
        el.alert.classList.remove("d-none");
    };
    const hideAlert = () => {
        if (el.alert) {
            el.alert.classList.add("d-none");
            el.alert.innerHTML = "";
        }
    };

    const debounce = (fn, ms) => {
        let t;
        return (...args) => {
            clearTimeout(t);
            t = setTimeout(() => fn(...args), ms);
        };
    };

    const val = (id) => {
        const n = document.getElementById(id);
        return n ? String(n.value || "").trim() : "";
    };
    const setVal = (id, v) => {
        const n = document.getElementById(id);
        if (n) n.value = v ?? "";
    };

    const formatLocalYmd = (d) => {
        if (!d || Number.isNaN(d.getTime())) return null;
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    };

    const getReviewDueValue = () => {
        const el = document.getElementById("cap-review-due");
        if (!el) return null;
        const fp = el._flatpickr;
        if (fp && fp.selectedDates && fp.selectedDates.length) {
            return formatLocalYmd(fp.selectedDates[0]);
        }
        const v = String(el.value || "").trim();
        if (!v) return null;
        if (/^\d{4}-\d{2}-\d{2}$/.test(v)) return v;
        return null;
    };

    const initReviewDuePicker = () => {
        const el = document.getElementById("cap-review-due");
        if (!el || el._flatpickr || typeof flatpickr === "undefined") return;
        let samplePlaceholder = "Select date";
        try {
            samplePlaceholder = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(2026, 5, 15));
        } catch {
            /* ignore */
        }
        flatpickr(el, {
            dateFormat: "Y-m-d",
            altInput: true,
            altFormat: "F j, Y",
            allowInput: true,
            altInputClass: "form-control dc-input dc-date-input dc-fp-alt",
            locale: { firstDayOfWeek: 1 },
            disableMobile: false,
            onChange: () => onFormChange(),
            onReady(_d, _s, inst) {
                if (inst.altInput) {
                    inst.altInput.placeholder = samplePlaceholder;
                    inst.altInput.setAttribute("aria-label", "Review due date");
                }
            }
        });
    };

    const fillSelect = (id, options, selected) => {
        const sel = document.getElementById(id);
        if (!sel) return;
        const keep = sel.querySelector("option[value='']");
        sel.innerHTML = "";
        if (keep) sel.appendChild(keep);
        else {
            const o = document.createElement("option");
            o.value = "";
            o.textContent = "Select...";
            sel.appendChild(o);
        }
        (options || []).forEach((x) => {
            const o = document.createElement("option");
            o.value = x;
            o.textContent = x;
            if (selected && selected === x) o.selected = true;
            sel.appendChild(o);
        });
    };

    const getTags = () =>
        [...document.querySelectorAll("#demand-tags-visual .demand-tag-pill")].map((x) => x.getAttribute("data-tag") || "").filter(Boolean);

    const renderTags = (tags) => {
        if (!el.tagsVisual) return;
        el.tagsVisual.innerHTML = (tags || []).map((t) =>
            `<span class="badge bg-label-primary text-primary rounded-pill demand-tag-pill dc-tag-chip" data-tag="${escapeAttr(t)}">${escapeHtml(t)}<button type="button" class="btn btn-sm btn-link text-primary p-0 ms-1 demand-tag-remove" aria-label="Remove">&times;</button></span>`
        ).join("");
        el.tagsVisual.querySelectorAll(".demand-tag-remove").forEach((b) => {
            b.addEventListener("click", () => {
                b.closest(".demand-tag-pill")?.remove();
                onFormChange();
            });
        });
    };

    const escapeHtml = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/"/g, "&quot;");
    const escapeAttr = (s) => escapeHtml(s).replace(/'/g, "&#39;");

    const renderConnectedRelated = () => {
        const host = document.getElementById("related-connected-host");
        const empty = document.getElementById("related-connected-empty");
        if (!host) return;
        if (!relatedIdeaIds.length) {
            host.innerHTML = "";
            if (empty) empty.classList.remove("d-none");
            return;
        }
        if (empty) empty.classList.add("d-none");
        host.innerHTML = relatedIdeaIds.map((id) => {
            const short = `${id.slice(0, 8)}…`;
            return `<span class="badge bg-label-primary text-primary rounded-pill d-inline-flex align-items-center gap-1 py-1 ps-2 pe-1 dc-related-chip">
                <span class="font-monospace small" title="${escapeAttr(id)}">${escapeHtml(short)}</span>
                <button type="button" class="btn btn-sm btn-link text-danger p-0 lh-1 dc-related-remove" data-related-id="${escapeAttr(id)}" aria-label="Remove link">&times;</button>
            </span>`;
        }).join("");
        host.querySelectorAll(".dc-related-remove").forEach((b) => {
            b.addEventListener("click", async () => {
                const rid = b.getAttribute("data-related-id");
                relatedIdeaIds = relatedIdeaIds.filter((x) => x !== rid);
                renderConnectedRelated();
                onFormChange();
                if (recordId) await saveDraft({ silent: true });
            });
        });
    };

    const isRelatedConnected = (id) => relatedIdeaIds.includes(String(id || "").toLowerCase());

    const tryAddRelatedFromRaw = async (raw, opts = {}) => {
        const id = normalizeGuid(raw);
        if (!id) {
            notify("Enter a valid record GUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).", "warning");
            return false;
        }
        if (recordId && id === String(recordId).toLowerCase()) {
            notify("You cannot link a record to itself.", "warning");
            return false;
        }
        if (relatedIdeaIds.includes(id)) {
            if (!opts.silent) notify("That idea is already linked.", "warning");
            return false;
        }
        relatedIdeaIds.push(id);
        renderConnectedRelated();
        onFormChange();
        if (!opts.silent) notify("Idea linked.");
        if (recordId) await saveDraft({ silent: true });
        fetchRelated();
        return true;
    };

    const getLinks = () =>
        [...document.querySelectorAll("#cap-links-list input.cap-link-input")].map((i) => String(i.value || "").trim()).filter(Boolean);

    const renderLinks = (links) => {
        if (!el.linksList) return;
        const arr = links && links.length ? links : [""];
        el.linksList.innerHTML = arr.map((url, idx) =>
            `<div class="input-group input-group-sm cap-link-row dc-link-row">
                <span class="input-group-text dc-input-group-icon border-end-0"><i class="bx bx-link-alt" aria-hidden="true"></i></span>
                <input type="url" class="form-control cap-link-input border-start-0" placeholder="https://…" value="${escapeAttr(url)}" data-idx="${idx}" aria-label="Supporting link URL ${idx + 1}" />
                <button type="button" class="btn btn-outline-secondary cap-link-remove" title="Remove link">&times;</button>
            </div>`
        ).join("");
        el.linksList.querySelectorAll(".cap-link-remove").forEach((b) => {
            b.addEventListener("click", () => {
                b.closest(".cap-link-row")?.remove();
                if (!el.linksList.querySelector(".cap-link-row")) renderLinks([""]);
                onFormChange();
            });
        });
        el.linksList.querySelectorAll(".cap-link-input").forEach((i) => i.addEventListener("input", onFormChange));
    };

    const buildPayload = () => {
        const rdVal = getReviewDueValue();
        return {
            title: val("cap-title") || null,
            problemStatement: val("cap-problem") || null,
            expectedOutcome: val("cap-outcome") || null,
            requestType: val("cap-reqtype") || null,
            strategicAlignment: val("cap-strategic") || null,
            businessUnit: val("cap-bu") || null,
            requestor: val("cap-requestor") || null,
            sponsor: val("cap-sponsor") || null,
            ownerName: val("cap-requestor") || null,
            proposedScope: val("cap-scope") || null,
            outOfScope: val("cap-outofscope") || null,
            assumptions: val("cap-assumptions") || null,
            constraints: val("cap-constraints") || null,
            category: val("cap-category") || null,
            demandSource: val("cap-demand-source") || null,
            priority: val("cap-priority") || null,
            complianceImpact: val("cap-compliance") || null,
            estimatedComplexity: val("cap-complexity") || null,
            riskSensitivity: val("cap-risk") || null,
            supportingLinks: getLinks(),
            notes: val("cap-notes") || null,
            tags: getTags(),
            strategicThemeKeys: [],
            reviewDueDate: rdVal,
            relatedIdeaIds: [...relatedIdeaIds]
        };
    };

    const applyDtoToForm = (dto) => {
        if (!dto) return;
        recordStatus = dto.status || "Draft";
        relatedIdeaIds = Array.isArray(dto.relatedIdeaIds)
            ? [...new Set(dto.relatedIdeaIds.map((x) => normalizeGuid(x)).filter(Boolean))]
            : [];
        setVal("cap-title", dto.title);
        setVal("cap-problem", dto.problemStatement);
        setVal("cap-outcome", dto.expectedOutcome);
        setVal("cap-reqtype", dto.requestType);
        setVal("cap-strategic", dto.strategicAlignment);
        setVal("cap-bu", dto.businessUnit);
        setVal("cap-requestor", dto.requestor);
        setVal("cap-sponsor", dto.sponsor);
        setVal("cap-scope", dto.proposedScope);
        setVal("cap-outofscope", dto.outOfScope);
        setVal("cap-assumptions", dto.assumptions);
        setVal("cap-constraints", dto.constraints);
        setVal("cap-category", dto.category);
        setVal("cap-demand-source", dto.demandSource);
        setVal("cap-priority", dto.priority);
        setVal("cap-compliance", dto.complianceImpact);
        setVal("cap-complexity", dto.estimatedComplexity);
        setVal("cap-risk", dto.riskSensitivity);
        setVal("cap-notes", dto.notes);
        const rdEl = document.getElementById("cap-review-due");
        if (dto.reviewDueDate) {
            const d = new Date(dto.reviewDueDate);
            if (!Number.isNaN(d.getTime())) {
                if (rdEl?._flatpickr) rdEl._flatpickr.setDate(d, false);
                else setVal("cap-review-due", formatLocalYmd(d) || "");
            }
        } else if (rdEl?._flatpickr) {
            rdEl._flatpickr.clear();
        } else {
            setVal("cap-review-due", "");
        }
        renderTags([...(dto.tags || [])]);
        renderLinks([...(dto.supportingLinks || [])]);
        renderAttachments(dto.attachments || []);
        renderConnectedRelated();
        updateSummary(dto);
        updateReadOnlyState();
        onFormChange();
    };

    const statusBadgeClass = (s) => {
        const x = String(s || "");
        if (x === "Submitted") return "bg-label-info text-info";
        if (x === "Under Review") return "bg-label-warning text-warning";
        if (x === "Approved") return "bg-label-success text-success";
        if (x === "Rejected") return "bg-label-danger text-danger";
        if (x === "Transferred") return "bg-label-primary text-primary";
        return "bg-label-secondary text-secondary";
    };

    const updateSummary = (dto) => {
        if (el.recordDisplay) {
            const rn = dto.recordNumber || dto.id;
            if (rn) {
                el.recordDisplay.textContent = dto.recordNumber || dto.id;
                el.recordDisplay.classList.remove("text-muted");
            } else {
                el.recordDisplay.textContent = "Not yet assigned";
                el.recordDisplay.classList.add("text-muted");
            }
        }
        if (el.statusBadge) {
            el.statusBadge.textContent = dto.status || "Draft";
            el.statusBadge.className = `badge rounded-pill px-3 py-2 dc-status-pill ${statusBadgeClass(dto.status)}`;
        }
    };

    const updateReadOnlyState = () => {
        const ro = recordStatus !== "Draft";
        form.querySelectorAll("input, select, textarea, button").forEach((n) => {
            if (n.closest("#dc-header-actions")) return;
            if (ro) {
                if (n.tagName === "SELECT" || n.type === "checkbox" || n.type === "file") n.disabled = true;
                else if (n.tagName === "BUTTON") n.disabled = true;
                else n.readOnly = true;
            } else {
                n.readOnly = false;
                n.disabled = false;
            }
        });
        if (el.btnDraft) el.btnDraft.disabled = ro;
        /* Submit runs save-then-submit when no recordId; only lock when not Draft */
        if (el.btnSubmit) el.btnSubmit.disabled = ro;
        if (el.fileInput) el.fileInput.disabled = ro || !recordId;
        if (el.dropzone) el.dropzone.classList.toggle("opacity-50", ro || !recordId);
        const rd = document.getElementById("cap-review-due");
        const fp = rd?._flatpickr;
        if (fp) {
            fp._input.disabled = ro;
            if (fp.altInput) fp.altInput.disabled = ro;
            fp.set("clickOpens", !ro);
        }
    };

    const hideValidationSummary = () => {
        if (el.validationSummary) {
            el.validationSummary.classList.add("d-none");
            el.validationSummary.innerHTML = "";
        }
        setSectionErrorState([], false);
    };

    const showValidationSummary = (html) => {
        if (!el.validationSummary) return;
        el.validationSummary.innerHTML = html;
        el.validationSummary.classList.remove("d-none");
    };

    const clearFieldErrors = () => {
        form.querySelectorAll(".is-invalid").forEach((x) => x.classList.remove("is-invalid"));
        form.querySelectorAll("[data-field-error]").forEach((x) => { x.textContent = ""; });
        document.getElementById("cap-priority")?.setAttribute("aria-invalid", "false");
        hideValidationSummary();
    };

    const applyServerErrors = (errors) => {
        if (!errors) return;
        clearFieldErrors();
        const keys = [];
        const lines = [];
        Object.keys(errors).forEach((k) => {
            if (k.toLowerCase() === "status") {
                notify((errors[k] && errors[k][0]) || "Invalid status.", "error");
                return;
            }
            const camel = k.charAt(0).toLowerCase() + k.slice(1);
            keys.push(k);
            const fid = fieldIds[camel];
            const input = fid ? document.getElementById(fid) : null;
            const msg = (errors[k] && errors[k][0]) || "Invalid.";
            if (input) {
                input.classList.add("is-invalid");
                const fb = form.querySelector(`[data-field-error="${camel}"]`);
                if (fb) fb.textContent = msg;
                lines.push(`<li><strong>${escapeHtml(camel)}</strong>: ${escapeHtml(msg)}</li>`);
            } else showAlert("danger", msg);
        });
        setSectionErrorState(keys, true);
        if (lines.length) {
            showValidationSummary(`<div class="d-flex align-items-start gap-2"><i class="bx bx-error-circle text-danger fs-4 flex-shrink-0" aria-hidden="true"></i><div><strong>Fix the following before continuing</strong><ul class="mb-0 ps-3 mt-2 small">${lines.join("")}</ul></div></div>`);
        }
        scrollToFirstInvalid();
    };

    const scrollToFirstInvalid = () => {
        const first = form.querySelector(".is-invalid");
        if (first) {
            first.scrollIntoView({ behavior: "smooth", block: "center" });
            try { first.focus(); } catch { /* ignore */ }
        }
    };

    const validateClientSubmit = () => {
        const errs = [];
        const push = (name, id, label) => {
            const input = document.getElementById(id);
            if (!input || input.disabled) return;
            if (!String(input.value || "").trim()) errs.push({ name, id, label });
        };
        push("title", "cap-title", "Idea title");
        push("problemStatement", "cap-problem", "Business need / problem statement");
        push("expectedOutcome", "cap-outcome", "Expected outcome");
        push("requestType", "cap-reqtype", "Request type");
        push("businessUnit", "cap-bu", "Business unit");
        push("requestor", "cap-requestor", "Requestor");
        push("priority", "cap-priority", "Priority");
        return errs;
    };

    const applyClientValidation = (errs) => {
        if (!errs.length) return;
        const byId = new Map();
        errs.forEach((e) => { if (!byId.has(e.id)) byId.set(e.id, e); });
        byId.forEach((e) => {
            const input = document.getElementById(e.id);
            if (input) {
                input.classList.add("is-invalid");
                if (e.id === "cap-priority") input.setAttribute("aria-invalid", "true");
            }
        });
        setSectionErrorState([...byId.values()].map((e) => e.name), true);
        const lines = [...byId.values()].map((e) => `<li><strong>${escapeHtml(e.label)}</strong> is required.</li>`);
        showValidationSummary(`<div class="d-flex align-items-start gap-2"><i class="bx bx-error-circle text-danger fs-4 flex-shrink-0" aria-hidden="true"></i><div><strong>Cannot submit yet</strong><ul class="mb-0 ps-3 mt-2 small">${lines.join("")}</ul></div></div>`);
        scrollToFirstInvalid();
    };

    const capturePageUrl = (id) => `/DemandIdeas/Capture?id=${encodeURIComponent(id)}`;

    const setBtnLoading = (btn, loading) => {
        if (!btn) return;
        btn.classList.toggle("is-loading", loading);
        if (loading) btn.disabled = true;
        else updateReadOnlyState();
    };

    const apiUrl = (path) => {
        const p = path.startsWith("/") ? path : `/${path}`;
        if (apiBase) return `${apiBase}/api/v1${p}`;
        return `/api/v1${p}`;
    };

    const fetchJson = async (url, options) => {
        const res = await fetch(url, {
            ...options,
            headers: { "Content-Type": "application/json", ...(options && options.headers) }
        });
        const text = await res.text();
        let data = null;
        try {
            data = text ? JSON.parse(text) : null;
        } catch {
            data = text;
        }
        return { res, data };
    };

    const getApiFailure = (res, data, fallbackMessage) => {
        const errors = data && typeof data === "object" && data.errors && typeof data.errors === "object"
            ? data.errors
            : null;
        const errorCode = data && typeof data === "object" && typeof data.errorCode === "string"
            ? data.errorCode
            : null;
        const message = data && typeof data === "object" && typeof data.message === "string" && data.message
            ? data.message
            : fallbackMessage || `Request failed (${res.status}).`;
        return { errors, errorCode, message };
    };

    const applyApiFailure = (res, data, fallbackMessage) => {
        const failure = getApiFailure(res, data, fallbackMessage);
        if (failure.errors) applyServerErrors(failure.errors);
        notify(failure.message, failure.errorCode === "NotFound" ? "warning" : "error");
        return failure;
    };

    const setLoading = (on) => {
        if (!el.loading) return;
        el.loading.classList.toggle("d-none", !on);
        el.loading.classList.toggle("d-flex", on);
    };

    const loadMeta = async () => {
        setLoading(true);
        try {
            let mRes;
            let mData;
            try {
                ({ res: mRes, data: mData } = await fetchJson(apiUrl("/demand-ideas/meta")));
            } catch {
                hideAlert();
                meta = cloneDefaultMeta();
                applyMetaToSelects();
                return;
            }
            const rawMeta = mRes.ok && mData && typeof mData === "object" ? mData : {};
            hideAlert();
            meta = mergeMetaWithDefaults(normalizeMetaFromApi(rawMeta));
            applyMetaToSelects();
        } finally {
            setLoading(false);
            updateReadOnlyState();
        }
    };

    const loadRecord = async (id) => {
        setLoading(true);
        try {
            const { res, data } = await fetchJson(apiUrl(`/demand-ideas/${encodeURIComponent(id)}`));
            if (!res.ok) {
                const failure = getApiFailure(res, data, "Record not found.");
                showAlert(failure.errorCode === "NotFound" ? "warning" : "danger", failure.message);
                return;
            }
            recordId = data.id;
            applyDtoToForm(data);
        } finally {
            setLoading(false);
        }
    };

    const saveDraft = async (opts = {}) => {
        hideAlert();
        clearFieldErrors();
        if (!opts.silent) setBtnLoading(el.btnDraft, true);
        try {
            const body = buildPayload();
            if (!recordId) {
                let res;
                let data;
                try {
                    ({ res, data } = await fetchJson(apiUrl("/demand-ideas"), { method: "POST", body: JSON.stringify(body) }));
                } catch {
                    notify(apiServiceHint, "error");
                    return false;
                }
                if (!res.ok) {
                    applyApiFailure(res, data, "Could not create draft.");
                    return false;
                }
                recordId = data.id;
                applyDtoToForm(data);
                const url = new URL(window.location.href);
                url.searchParams.set("id", recordId);
                window.history.replaceState({}, "", url.toString());
                if (!opts.silent) notify("Draft saved.");
                return true;
            }
            let res;
            let data;
            try {
                ({ res, data } = await fetchJson(apiUrl(`/demand-ideas/${encodeURIComponent(recordId)}`), { method: "PUT", body: JSON.stringify(body) }));
            } catch {
                notify(apiServiceHint, "error");
                return false;
            }
            if (res.status === 409) {
                applyApiFailure(res, data, "Cannot update record.");
                return false;
            }
            if (!res.ok) {
                applyApiFailure(res, data, "Save failed.");
                return false;
            }
            applyDtoToForm(data);
            if (!opts.silent) notify("Draft saved.");
            return true;
        } finally {
            if (!opts.silent) {
                setBtnLoading(el.btnDraft, false);
                updateReadOnlyState();
            }
        }
    };

    const submitRecord = async () => {
        hideAlert();
        clearFieldErrors();
        const cErrs = validateClientSubmit();
        if (cErrs.length) {
            applyClientValidation(cErrs);
            return;
        }
        setBtnLoading(el.btnSubmit, true);
        try {
            if (!recordId) {
                const ok = await saveDraft({ silent: true });
                if (!ok || !recordId) return;
            } else {
                let res;
                let data;
                try {
                    ({ res, data } = await fetchJson(apiUrl(`/demand-ideas/${encodeURIComponent(recordId)}`), { method: "PUT", body: JSON.stringify(buildPayload()) }));
                } catch {
                    notify(apiServiceHint, "error");
                    return;
                }
                if (res.status === 409) {
                    applyApiFailure(res, data, "Cannot update record.");
                    return;
                }
                if (!res.ok) {
                    applyApiFailure(res, data, "Save latest changes before submit.");
                    return;
                }
                applyDtoToForm(data);
            }
            let res;
            let data;
            try {
                ({ res, data } = await fetchJson(apiUrl(`/demand-ideas/${encodeURIComponent(recordId)}/submit`), { method: "POST", body: "{}" }));
            } catch {
                notify(apiServiceHint, "error");
                return;
            }
            if (!res.ok) {
                applyApiFailure(res, data, "Submit validation failed.");
                return;
            }
            applyDtoToForm(data);
            notify("Submitted successfully.");
        } finally {
            setBtnLoading(el.btnSubmit, false);
            updateReadOnlyState();
        }
    };

    const renderAttachments = (atts) => {
        if (!el.attachments) return;
        el.attachments.innerHTML = (atts || []).map((a) => {
            const href = a.downloadUrl && (a.downloadUrl.startsWith("http") ? a.downloadUrl : `${apiBase || ""}${a.downloadUrl}`);
            return `<li class="dc-attach-chip">
                <i class="bx bx-file" aria-hidden="true"></i>
                <a href="${escapeAttr(href)}" target="_blank" rel="noopener" class="text-truncate flex-grow-1 min-w-0">${escapeHtml(a.fileName)}</a>
                <span class="text-muted small flex-shrink-0">${formatBytes(a.sizeBytes)}</span>
            </li>`;
        }).join("");
    };

    const formatBytes = (n) => {
        if (!n && n !== 0) return "";
        const u = ["B", "KB", "MB", "GB"];
        let i = 0;
        let v = n;
        while (v >= 1024 && i < u.length - 1) {
            v /= 1024;
            i++;
        }
        return `${v.toFixed(i ? 1 : 0)} ${u[i]}`;
    };

    const uploadFiles = async (files) => {
        if (!recordId) {
            notify("Save a draft first to attach files.", "warning");
            return;
        }
        const overlay = document.getElementById("cap-dropzone-overlay");
        if (overlay) {
            overlay.classList.remove("d-none");
            overlay.classList.add("d-flex");
        }
        if (el.dropzone) el.dropzone.classList.add("dc-dropzone--busy");
        try {
            for (const file of files) {
                const fd = new FormData();
                fd.append("file", file);
                const res = await fetch(apiUrl(`/uploads/${encodeURIComponent(recordId)}`), { method: "POST", body: fd });
                const text = await res.text();
                let data = null;
                try {
                    data = text ? JSON.parse(text) : null;
                } catch { /* ignore */ }
                if (!res.ok) {
                    notify(data?.message || `Upload failed (${res.status})`, "error");
                    continue;
                }
            }
            await loadRecord(recordId);
            notify("File(s) uploaded.");
        } finally {
            if (overlay) {
                overlay.classList.add("d-none");
                overlay.classList.remove("d-flex");
            }
            if (el.dropzone) el.dropzone.classList.remove("dc-dropzone--busy");
        }
    };

    const fetchRelated = async () => {
        try {
            const u = new URL(apiUrl("/demand-ideas/related"), window.location.origin);
            const t = val("cap-title");
            if (t) u.searchParams.append("title", t);
            const rt = val("cap-reqtype");
            if (rt) u.searchParams.append("requestType", rt);
            const bu = val("cap-bu");
            if (bu) u.searchParams.append("businessUnit", bu);
            const sa = val("cap-strategic");
            if (sa) u.searchParams.append("strategicAlignment", sa);
            getTags().forEach((tag) => u.searchParams.append("tags", tag));
            if (recordId) u.searchParams.append("excludeId", recordId);
            u.searchParams.append("take", "8");
            const res = await fetch(u.toString());
            if (!res.ok) return;
            const raw = await res.json();
            const list = Array.isArray(raw) ? raw : [];
            if (el.relatedList) {
                el.relatedList.innerHTML = list.map((r) => {
                    const connected = isRelatedConnected(r.id);
                    const actions = connected
                        ? `<span class="badge bg-label-success flex-shrink-0 align-self-center">Linked</span>`
                        : `<button type="button" class="btn btn-sm btn-outline-primary flex-shrink-0 dc-related-connect" data-related-id="${escapeAttr(r.id)}">Connect</button>`;
                    return `<li class="p-0 mb-2">
                        <div class="d-flex align-items-center gap-2 flex-wrap">
                            <a href="${capturePageUrl(r.id)}" class="dc-related-link d-flex justify-content-between align-items-start gap-2 text-decoration-none rounded-2 flex-grow-1 min-w-0 border p-2">
                                <div class="min-w-0 flex-grow-1">
                                    <div class="dc-related-title text-truncate">${escapeHtml(r.title)}</div>
                                    <div class="dc-related-meta font-monospace">${escapeHtml(r.recordNumber || r.id)}</div>
                                </div>
                                <span class="d-flex align-items-center gap-1 flex-shrink-0">
                                    <span class="badge bg-label-primary rounded-pill">${r.matchScore}% match</span>
                                    <i class="bx bx-chevron-right dc-related-chev" aria-hidden="true"></i>
                                </span>
                            </a>
                            ${actions}
                        </div>
                    </li>`;
                }).join("");
            }
            if (el.relatedEmpty) {
                el.relatedEmpty.classList.toggle("d-none", list.length > 0);
                if (el.relatedList) el.relatedList.classList.toggle("d-none", list.length === 0);
            }
        } catch {
            /* ignore network errors */
        }
    };

    const debouncedRelated = debounce(() => { fetchRelated(); }, 550);

    const onFormChange = () => {
        const pr = document.getElementById("cap-priority");
        if (pr && val("cap-priority") && pr.classList.contains("is-invalid")) {
            pr.classList.remove("is-invalid");
            pr.setAttribute("aria-invalid", "false");
            const fb = form.querySelector('[data-field-error="priority"]');
            if (fb) fb.textContent = "";
        }
        debouncedRelated();
    };

    document.getElementById("cap-add-link")?.addEventListener("click", () => {
        const row = document.createElement("div");
        row.className = "input-group input-group-sm cap-link-row dc-link-row";
        row.innerHTML = `<span class="input-group-text dc-input-group-icon border-end-0"><i class="bx bx-link-alt" aria-hidden="true"></i></span><input type="url" class="form-control cap-link-input border-start-0" placeholder="https://…" aria-label="Supporting link URL" /><button type="button" class="btn btn-outline-secondary cap-link-remove" title="Remove link">&times;</button>`;
        el.linksList?.appendChild(row);
        row.querySelector(".cap-link-remove")?.addEventListener("click", () => {
            row.remove();
            if (!el.linksList?.querySelector(".cap-link-row")) renderLinks([""]);
            onFormChange();
        });
        row.querySelector(".cap-link-input")?.addEventListener("input", onFormChange);
    });

    const addTag = () => {
        const t = (el.tagInput?.value || "").trim();
        if (!t) return;
        const cur = getTags();
        if (cur.includes(t)) return;
        cur.push(t);
        renderTags(cur);
        if (el.tagInput) el.tagInput.value = "";
        onFormChange();
    };
    document.getElementById("cap-tag-add")?.addEventListener("click", addTag);
    el.tagInput?.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            e.preventDefault();
            addTag();
        }
    });

    const getCaptureShareUrl = () => {
        const u = new URL(window.location.href);
        if (recordId) u.searchParams.set("id", recordId);
        else u.searchParams.delete("id");
        return u.toString();
    };

    document.getElementById("dc-open-related-btn")?.addEventListener("click", () => {
        let raw = document.getElementById("dc-open-related-id")?.value?.trim() || "";
        if (!raw) {
            notify("Paste a record ID or a Capture page URL.", "warning");
            return;
        }
        if (raw.includes("id=")) {
            try {
                const u = new URL(raw.startsWith("http") ? raw : raw, window.location.origin);
                const q = u.searchParams.get("id");
                if (q) raw = q;
            } catch {
                /* ignore */
            }
        }
        if (!guidRe.test(raw)) {
            notify("Enter a valid record GUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx) or paste a Capture URL with ?id=…", "warning");
            return;
        }
        window.open(`/DemandIdeas/Capture?id=${encodeURIComponent(raw)}`, "_blank", "noopener,noreferrer");
    });

    document.getElementById("dc-connect-related-btn")?.addEventListener("click", () => {
        let raw = document.getElementById("dc-open-related-id")?.value?.trim() || "";
        if (raw.includes("id=")) {
            try {
                const u = new URL(raw.startsWith("http") ? raw : raw, window.location.origin);
                const q = u.searchParams.get("id");
                if (q) raw = q;
            } catch {
                /* ignore */
            }
        }
        tryAddRelatedFromRaw(raw, { silent: false });
    });

    document.getElementById("related-ideas-panel")?.addEventListener("click", (e) => {
        const btn = e.target.closest(".dc-related-connect");
        if (!btn) return;
        e.preventDefault();
        const id = btn.getAttribute("data-related-id");
        if (id) tryAddRelatedFromRaw(id, { silent: false });
    });

    document.getElementById("dc-copy-page-link")?.addEventListener("click", async () => {
        const text = getCaptureShareUrl();
        try {
            await navigator.clipboard.writeText(text);
            notify("Page link copied to clipboard.");
        } catch {
            notify("Could not copy link. Copy from the address bar.", "warning");
        }
    });

    el.btnDraft?.addEventListener("click", (e) => {
        e.preventDefault();
        e.stopPropagation();
        saveDraft();
    });
    el.btnSubmit?.addEventListener("click", (e) => {
        e.preventDefault();
        e.stopPropagation();
        submitRecord();
    });

    [
        "cap-title",
        "cap-problem",
        "cap-outcome",
        "cap-reqtype",
        "cap-bu",
        "cap-requestor",
        "cap-category",
        "cap-demand-source",
        "cap-priority",
        "cap-compliance",
        "cap-complexity",
        "cap-risk",
        "cap-sponsor",
        "cap-strategic",
        "cap-scope",
        "cap-outofscope",
        "cap-assumptions",
        "cap-constraints",
        "cap-notes",
        "cap-review-due"
    ].forEach((id) => {
        document.getElementById(id)?.addEventListener("input", onFormChange);
        document.getElementById(id)?.addEventListener("change", onFormChange);
    });

    el.fileInput?.addEventListener("change", (e) => {
        const f = e.target.files;
        if (f && f.length) uploadFiles(f);
        e.target.value = "";
    });
    el.dropzone?.addEventListener("dragover", (e) => {
        e.preventDefault();
        el.dropzone.classList.add("demand-dropzone-active");
    });
    el.dropzone?.addEventListener("dragleave", () => el.dropzone.classList.remove("demand-dropzone-active"));
    el.dropzone?.addEventListener("drop", (e) => {
        e.preventDefault();
        el.dropzone.classList.remove("demand-dropzone-active");
        if (e.dataTransfer?.files?.length) uploadFiles(e.dataTransfer.files);
    });

    (async () => {
        initReviewDuePicker();
        renderLinks([""]);
        renderTags([]);
        await loadMeta();
        const initial = root.dataset.initialId;
        if (initial) {
            await loadRecord(initial);
        } else {
            recordId = null;
            recordStatus = "Draft";
            if (el.recordDisplay) {
                el.recordDisplay.textContent = "Not yet assigned";
                el.recordDisplay.classList.add("text-muted");
            }
            updateSummary({ status: "Draft", requestType: "", priority: "", ownerName: "", sponsor: "", reviewDueDate: null });
        }
        renderConnectedRelated();
        updateReadOnlyState();
        fetchRelated();
    })();
})();
