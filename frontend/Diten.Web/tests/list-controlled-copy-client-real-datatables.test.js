const vm = require("vm");
const { read, until, razor, bridgeL10n, bootBrowser } = require("./controlled-copy-harness");

/*
 * BL-452 package 2 — THE CONTROLLED COPY on the CLIENT-MODE reference (Golden Slim). A client-mode list already holds
 * every row, so the rows stay where they always came from — the table, through the same exportOptions (visible
 * columns, applied filter) — and NO request is made. What changes is only the header block and the footer, which
 * must be the same block the server-mode copy carries.
 */
const TR_WORDS = { Status: "Durum", Active: "Aktif", Passive: "Pasif", ReferenceType: "Referans Türü", Priority: "Öncelik", ShowAll: "Tümünü Göster", Apply: "Uygula", Reset: "Sıfırla" };
const HEADERS = ["", "", "Kod", "Ad", "Referans Türü", "Öncelik", "Durum", "İşlemler"];
const ITEMS = [
  { id: "s1", code: "GS-001", name: "Beta", referenceType: "Standard", priority: 1, isActive: true },
  { id: "s2", code: "GS-002", name: "Alpha", referenceType: "Custom", priority: 2, isActive: false },
  { id: "s3", code: "GS-003", name: "Gamma", referenceType: "Pro", priority: 3, isActive: true },
  { id: "s4", code: "GS-004", name: "Delta", referenceType: "Standard", priority: 4, isActive: false },
  { id: "s5", code: "GS-005", name: "Epsilon", referenceType: "Custom", priority: 5, isActive: true }
];

describe("controlled copy — client-mode list (Golden Slim): rows from the table, no request, the same header block", () => {
  let b;
  const list = () => b.ctx.SlimHandle;
  const button = (format) => list().dt.button(`.dt-controlled-copy[data-export-format='${format}']`);

  beforeAll(async () => {
    document.title = "Altın Slim - Di10";
    document.body.innerHTML =
      '<a class="app-brand-link tenant-brand-link" data-tenant-name="Diten Pharma A.Ş."></a>'
      + razor(read("Views", "DevEnablement", "GoldenReferenceSlim", "_Filter.cshtml"), TR_WORDS)
      + '<div class="card"><div class="card-datatable"><table id="dt-goldenreferenceslim" data-dt-standard="v2" data-dt-data-mode="client" class="datatables-goldenreferenceslim table border-top">'
      + `<thead><tr>${HEADERS.map((h) => `<th>${h}</th>`).join("")}</tr></thead></table></div></div>`;

    b = bootBrowser({
      network: {
        list: () => ({ success: true, data: ITEMS }),
        fetch: async () => ({ ok: true, status: 200, json: async () => ({ referenceTypes: [], priorities: [] }) })
      }
    });

    window.API = { deven: "http://gw" };
    window.CurrentLanguage = "tr";
    window.CurrentUser = { tenantId: "t-1", email: "ayse@diten.test" };
    window.L10n = Object.assign(bridgeL10n("tr"), { AddNew: "Yeni", Active: "Aktif", Passive: "Pasif", Actions: "İşlemler" });
    window.Permissions = { has: () => true };
    window.personalizationClient = { getViews: async () => [] };

    const createList = window.DitenDataTable.createList;
    window.DitenDataTable.createList = async (options) => { const h = await createList(options); b.ctx.SlimHandle = h; return h; };
    b.run("wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js");
    vm.runInContext("GoldenReferenceSlimList.init()", b.ctx);
    window.DitenDataTable.createList = createList;
    await until(() => b.ctx.SlimHandle && list().dt.rows().count() === 5);

    // Status = Aktif applied (3 of 5 rows), sorted by Ad ascending.
    window.jQuery("#filterStatus").val(["Active"]);
    document.getElementById("btnFilterApply").click();
    list().dt.order([3, "asc"]).draw();
    await until(() => list().dt.rows({ search: "applied" }).count() === 3);
  });

  test("(3) PDF makes NO request: the rows are the table's filtered rows, with the same header block and footer", async () => {
    expect(list().exportMode).toBe("client");
    const requests = b.fetches.length + b.listRequests.length;
    button("pdf").trigger();
    await until(() => b.pdfs.length === 1);
    expect(b.fetches.length + b.listRequests.length, "no network at all").toBe(requests);

    const doc = b.pdfs[0].docDefinition;
    const table = doc.content.find((c) => c.table && c.table.headerRows === 1).table;
    expect(table.body[0].map((c) => c.text)).toEqual(["Kod", "Ad", "Referans Türü", "Öncelik", "Durum"]);
    expect(table.body.slice(1).map((r) => r[1]), "the applied filter and the sort, from the table").toEqual(["Beta", "Epsilon", "Gamma"]);

    expect(doc.content.map((c) => c.text).filter(Boolean)).toEqual(["Diten Pharma A.Ş.", "Altın Slim"]);
    const lines = Object.fromEntries(doc.content.find((c) => c.layout === "noBorders").table.body.map(([l, v]) => [l.text, v.text]));
    expect(Object.keys(lines), "the same seven lines as the server-mode copy")
      .toEqual(["Şirket", "Filtreler", "Arama", "Sıralama", "Satır sayısı", "Oluşturan", "Oluşturma zamanı"]);
    expect(lines).toMatchObject({ "Filtreler": "Durum: Aktif", "Arama": "Yok", "Sıralama": "Ad (artan)", "Satır sayısı": "3", "Oluşturan": "ayse@diten.test" });
    expect(doc.footer(1, 1).columns[1].text).toBe("Sayfa 1 / 1");
    expect(doc.footer(1, 1).columns[0].text).toMatch(/^Bu çıktı kontrolsüz kopyadır — /);
  });

  test("(4) print on a client list: the same block in the window's DOM, the table's rows, no 'Generated'", async () => {
    const requests = b.fetches.length + b.listRequests.length;
    button("print").trigger();
    await until(() => b.opened.length === 1 && b.opened[0].document.querySelector(".print-shell"));
    const doc = b.opened[0].document;
    expect(b.fetches.length + b.listRequests.length).toBe(requests);
    expect(doc.querySelector('.print-meta dd[data-line="filters"]').textContent).toBe("Durum: Aktif");
    expect(doc.querySelectorAll(".print-table tbody tr").length).toBe(3);
    expect(doc.body.textContent).not.toMatch(/Generated/);
  });

  test("the Action menu reads its words from the bridge: İşlem · Yazdır · PDF · Kopyala", () => {
    const container = list().dt.table().container();
    expect(container.querySelector(".dt-export-collection-btn").textContent.trim()).toBe("İşlem");
  });
});
