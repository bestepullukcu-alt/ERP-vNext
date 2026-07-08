const { loadScript } = require("./load-script");

describe("PersonReferencePicker", () => {
    function load() {
        delete window.PersonReferenceApi;
        delete window.PersonReferencePicker;
        global.fetch = vi.fn();
        loadScript("wwwroot/assets/js/Platform/PersonReferences/person-reference-picker.js");
        return window.PersonReferencePicker._test;
    }

    it("normalizes only safe person reference fields", () => {
        const helper = load();

        const person = helper.normalizePerson({
            personId: "00000000-0000-0000-0000-000000000123",
            displayName: "Ada Lovelace",
            referenceCode: "P-123",
            status: "Active",
            referenceable: true,
            userId: "not-projected",
            governmentIdentifier: "not-projected"
        });

        expect(person).toEqual({
            personId: "00000000-0000-0000-0000-000000000123",
            displayName: "Ada Lovelace",
            referenceCode: "P-123",
            status: "Active",
            referenceable: true,
            profilePointer: ""
        });
        expect("userId" in person).toBe(false);
        expect("governmentIdentifier" in person).toBe(false);
    });

    it("bounds search paging and maps referenceable filter", () => {
        const helper = load();

        const params = helper.buildSearchParams({
            query: " ada ",
            referenceable: true,
            page: -5,
            pageSize: 500
        });

        expect(params.get("query")).toBe("ada");
        expect(params.get("referenceable")).toBe("true");
        expect(params.get("page")).toBe("1");
        expect(params.get("pageSize")).toBe("100");
    });

    it("classifies gateway failures for picker error states", () => {
        const helper = load();

        expect(helper.classifyStatus(403)).toBe("permission_denied");
        expect(helper.classifyStatus(404)).toBe("missing_person");
        expect(helper.classifyStatus(503)).toBe("dependency_unavailable");
    });

    it("validates selected person ids through the same-origin proxy only", async () => {
        load();
        fetch.mockResolvedValue({
            ok: true,
            text: () => Promise.resolve(JSON.stringify({
                data: {
                    results: [
                        {
                            personId: "00000000-0000-0000-0000-000000000123",
                            displayName: "Ada Lovelace",
                            referenceable: true
                        }
                    ]
                }
            }))
        });

        const result = await window.PersonReferenceApi.validate(["00000000-0000-0000-0000-000000000123"]);

        expect(fetch).toHaveBeenCalledTimes(1);
        expect(fetch.mock.calls[0][0]).toBe("/Platform/PersonReferences/api/lookup-validation");
        expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual({
            personIds: ["00000000-0000-0000-0000-000000000123"]
        });
        expect(fetch.mock.calls[0][1].credentials).toBe("same-origin");
        expect(result[0].personId).toBe("00000000-0000-0000-0000-000000000123");
    });

    it("returns an abortable Select2 transport and resolves normalized search results", async () => {
        const helper = load();
        fetch.mockResolvedValue({
            ok: true,
            text: () => Promise.resolve(JSON.stringify({
                data: {
                    items: [
                        {
                            personId: "00000000-0000-0000-0000-000000000123",
                            displayName: "Ada Lovelace",
                            referenceCode: "P-123",
                            status: "Active",
                            referenceable: true
                        }
                    ],
                    page: 1,
                    pageSize: 20
                }
            }))
        });
        const success = vi.fn();
        const failure = vi.fn();

        const transport = helper.createSelect2Transport(
            { referenceable: true },
            { data: { term: "Ada", page: 1 } },
            success,
            failure);
        await vi.waitFor(() => expect(success).toHaveBeenCalledTimes(1));

        expect(transport).toEqual({ abort: expect.any(Function) });
        expect(failure).not.toHaveBeenCalled();
        expect(fetch.mock.calls[0][0]).toBe("/Platform/PersonReferences/api?query=Ada&referenceable=true&page=1&pageSize=20");
        expect(success.mock.calls[0][0].items[0].personId).toBe("00000000-0000-0000-0000-000000000123");
    });

    it("resolves exact guid searches through lookup validation without broad person search", async () => {
        const helper = load();
        fetch.mockResolvedValue({
            ok: true,
            text: () => Promise.resolve(JSON.stringify({
                data: {
                    results: [
                        {
                            personId: "02510000-0000-0000-0000-000000000001",
                            displayName: "MOD0251 Smoke Person",
                            referenceCode: "MOD0251-SMOKE-PERSON",
                            status: "Active",
                            referenceable: true
                        }
                    ]
                }
            }))
        });
        const success = vi.fn();

        helper.createSelect2Transport(
            { referenceable: true },
            { data: { term: "02510000-0000-0000-0000-000000000001", page: 1 } },
            success,
            vi.fn());
        await vi.waitFor(() => expect(success).toHaveBeenCalledTimes(1));

        expect(fetch.mock.calls[0][0]).toBe("/Platform/PersonReferences/api/lookup-validation");
        expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual({
            personIds: ["02510000-0000-0000-0000-000000000001"]
        });
        expect(success.mock.calls[0][0].items[0]).toMatchObject({
            personId: "02510000-0000-0000-0000-000000000001",
            displayName: "MOD0251 Smoke Person",
            referenceable: true
        });
    });

    it("processes undefined Select2 data as an empty result set", () => {
        const helper = load();

        const result = helper.processSelect2Results(undefined, { page: 1 });

        expect(result).toEqual({
            results: [],
            pagination: { more: false }
        });
    });
});
