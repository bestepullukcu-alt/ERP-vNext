const { loadScript } = require("./load-script");

describe("Working Calendar Overrides details read-only parity", () => {
    afterEach(() => {
        delete window.fetch;
        delete window.L10n;
        document.body.innerHTML = "";
    });

    it("hides every mutation action for inherited country details and keeps own overrides editable", async () => {
        document.body.innerHTML = `
            <div class="working-calendar-override-details" data-calendar-id="30bc3ece-0000-0000-0000-000000000000">
                <span id="wc-title"></span><span id="wc-subtitle"></span><span id="wc-code"></span>
                <span id="wc-name"></span><span id="wc-description"></span><span id="wc-country"></span>
                <span id="wc-year"></span><span id="wc-scope"></span><span id="wc-org-unit"></span>
                <span id="wc-notes"></span><span id="wc-status"></span><span id="wc-weekend"></span>
                <div id="wc-weekend-inherited" class="d-none"><span id="wc-weekend-inherited-text"></span></div>
                <table><tbody id="wc-days-body"></tbody></table>
                <a id="wc-btn-edit" class="d-none"></a>
                <button id="wc-btn-activate" class="d-none"></button>
                <button id="wc-btn-archive" class="d-none"></button>
                <button id="wc-btn-add-day" class="d-none"></button>
                <input id="wc-probe-date" />
            </div>`;
        window.L10n = {};

        let dto = {
            id: "30bc3ece-0000-0000-0000-000000000000",
            calendarCode: "TR-2026",
            calendarName: "Türkiye 2026",
            countryCode: "TR",
            calendarYear: 2026,
            scopeType: "country",
            calendarStatus: "active",
            effectiveWeekendDays: ["saturday", "sunday"],
            days: [],
            isCountryLayer: true,
            isReadOnly: true
        };
        window.fetch = vi.fn(async () => ({ ok: true, json: async () => ({ data: dto }) }));
        loadScript("wwwroot/assets/js/WorkingCalendar/Overrides/details.js");

        document.dispatchEvent(new Event("DOMContentLoaded"));
        await new Promise((resolve) => setTimeout(resolve, 0));

        expect(document.getElementById("wc-btn-edit").classList.contains("d-none")).toBe(true);
        expect(document.getElementById("wc-btn-activate").classList.contains("d-none")).toBe(true);
        expect(document.getElementById("wc-btn-archive").classList.contains("d-none")).toBe(true);
        expect(document.getElementById("wc-btn-add-day").classList.contains("d-none")).toBe(true);

        dto = {
            ...dto,
            id: "31bc3ece-0000-0000-0000-000000000000",
            calendarCode: "ACME-TR-2026",
            scopeType: "tenant",
            isCountryLayer: false,
            isReadOnly: false
        };
        document.dispatchEvent(new Event("DOMContentLoaded"));
        await new Promise((resolve) => setTimeout(resolve, 0));

        expect(document.getElementById("wc-btn-edit").classList.contains("d-none")).toBe(false);
        expect(document.getElementById("wc-btn-archive").classList.contains("d-none")).toBe(false);
        expect(document.getElementById("wc-btn-add-day").classList.contains("d-none")).toBe(false);
    });
});
