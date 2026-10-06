# WORK PACKAGE — WP-KNOWLEDGE-SORTORDER-UX · Taxonomy sıralama UX: "Sıra" alanını gizle + otomatik sona ekle + "Sırala" sürükle-bırak paneli (frontend)

> **CT (SoR).** MOD-0162-FU02 Knowledge/Taxonomy Admin UI. Branch `feature/crm-scmm-studio`. **Kullanıcı E4:** Subject/Topic/Profile oluştururken **"Sıra"** (sortOrder) numarasını elle girmek kötü UX — kullanıcı ne yazacağını bilemez. **Karar:** (1) formda "Sıra" alanını **gizle**, (2) yeni kayıt **otomatik sona eklensin** (max+10), (3) sıra değiştirme **ayrı bir "Sırala" panelinde sürükle-bırakla** yapılsın (arka planda 10/20/30 yeniden numaralandır). **Frontend** (taxonomy.js + Taxonomy.cshtml + resx). Backend contract DEĞİŞMEZ (mevcut create/update SortOrder alanı reuse).

## Kanıt
- **sortOrder tüketimi** (`taxonomy.js:567`): picker/dropdown `.sort((a,b)=>a.sortOrder-b.sortOrder || a.label.localeCompare(b.label))` — küçük sayı önce, eşitlik alfabetik. Grid varsayılan sıralaması `updatedAt desc` (satır 75/83/94 `order:[[…,'desc']]`), sortOrder yalnız görünen sütun. → sortOrder'ın asıl etkisi **seçici listelerde**.
- **Grid client-side** (`taxonomy.js:247-249` `dataTable.ext.search.push`) → tüm satırlar tarayıcıda; frontend max+10 hesaplayabilir + reorder listesi kurabilir.
- **RowReorder eklentisi YOK** (`wwwroot/assets/vendor/libs`: datatables-{buttons,select,colreorder,rowgroup,responsive,fixed*} var, **rowreorder yok**); `sortablejs` var. → DataTable tbody'sine ham Sortable = kırılgan (KP `#stepList` reorder'ı bu yüzden şu an bozuk). **Ayrı düz-liste paneli** = sağlam yol.
- **Form alanı** (`_Form.cshtml`/Taxonomy.cshtml): `taxSortOrder` input (`taxonomy.js:872 setValue('taxSortOrder', row?.sortOrder ?? 0)` · `1004 sortOrder: document.getElementById('taxSortOrder').value`). Create/update payload `sortOrder` taşıyor (`920/1004`).
- **Backend:** `SubjectCommands`/`TopicCommands`/`AudienceProfileCommands` hepsi `int SortOrder = 0` alıyor, olduğu gibi persist (`SubjectCommandHandlers:129/206`) — **auto-append yok**, update SortOrder'ı kabul ediyor (reorder-persist için yeterli).

## NE (frontend; backend contract DEĞİŞMEZ)
1. **"Sıra" alanını formdan GİZLE** (3 sekme: Subject/Topic/Profile) — `taxSortOrder` input'u kaldır/gizle; kullanıcı numara görmez/yazmaz. (Değer hâlâ payload'da taşınır — auto-append'ten.)
2. **Otomatik sona ekle (create):** yeni kayıtta `sortOrder = max(scope'taki aktif kayıtların sortOrder'ı) + 10` (boşsa 10). **Scope:** Subject = tüm subject'ler · Topic = **seçili parent Subject içindeki** topic'ler · Profile = tüm profiller. Grid client-side yüklü olduğundan frontend hesaplar. (Edit'te mevcut sortOrder korunur.)
3. **"Sırala" paneli (reorder):** her sekme toolbar'ına **"Sırala"** butonu → **offcanvas/modal** açar; içinde o sekmenin **aktif** kayıtları mevcut sortOrder sırasında **düz bir sortable liste** (sürükle tutamacı). `sortablejs` ile **düz liste üzerinde** (DataTable tbody'sine DEĞİL) drag-drop. Kaydet → görünür sırayı **10/20/30…** yeniden numaralandır → değişen her kaydı mevcut **update** komutuyla (satırın tüm alanları + yeni sortOrder) persist et → grid + picker'ı yenile. Topic paneli seçili parent Subject'e göre filtreli.
4. **resx (7 dil):** yeni etiketler `Reorder`/"Sırala", `ReorderSubjects`/`ReorderTopics`/`ReorderProfiles`, `ReorderHelp`, `SaveOrder`; `_IndexL10n.cshtml` köprü whitelist'ine ekle (JS okuyorsa). "Sıra" form etiketi kaldırıldığı için ilgili label artık kullanılmıyorsa dokunma.

## KORU / YAPMA
- **DataTable tbody'sine Sortable UYGULAMA** (grid'in paging/sort/redraw'ını bozar — KP kırılganlığını tekrarlama); reorder yalnız **ayrı offcanvas düz-liste**. Grid varsayılan sıralaması + sütunları + picker sort (`taxonomy.js:567`) DEĞİŞMEZ. Backend create/update **contract DEĞİŞMEZ** (mevcut SortOrder alanı reuse; yeni endpoint gerekmez — değişen kayıtlar update ile). Archive/no-delete kuralı korunur (yalnız aktif kayıtlar sıralanır). Tema/L10n köprüsü ([[l10n-bridge-pascalcase-loader]]). Diğer sekmeler/alanlar DOKUNMA.
- **DUR:** offcanvas düz-liste drag-drop `sortablejs` ile çalışmıyorsa (KP'deki aynı kök neden — ör. Sortable global yüklenmiyor/handle) → kök nedeni bul, additive düzelt; düzeltilemiyorsa **yukarı/aşağı ok** fallback'ine geç + raporla (numara yazdırmama hedefi korunur). Reorder-persist bulk update atomik değilse (kısmi başarı) → değişen-only + hata toast + reload (KP persistOrder deseni).

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → **201/0**. git diff: `taxonomy.js` + `Taxonomy.cshtml` + `_IndexL10n.cshtml` + `KnowledgeIndex.*.resx` (7 dil, yeni Reorder anahtarları). Backend/diğer resx/picker-sort diff YOK.
- **E4 (FLEET RESTART — resx + view):** (1) Subject **Oluştur** → "Sıra" alanı **YOK**; kaydet → yeni kayıt listenin/picker'ın **sonunda** (max+10). (2) Toolbar **"Sırala"** → offcanvas düz-liste → **sürükle-bırakla** sıra değiştir → Kaydet → picker/dropdown yeni sırayı yansıtır (numara girilmeden). (3) Topic "Sırala" seçili parent Subject'e göre. (4) Sürükle-bırak çalışmıyorsa ok-fallback devrede.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-KNOWLEDGE-SORTORDER-UX · Taxonomy sıralama UX: "Sıra" gizle + otomatik sona ekle + "Sırala" sürükle-bırak paneli (MOD-0162-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Worktree: ana checkout

Kullanıcı E4: Subject/Topic/Profile oluştururken "Sıra" (sortOrder) numarasını elle girmek kötü UX. Karar: (1) formda "Sıra" alanını gizle, (2) yeni kayıt otomatik sona (max+10), (3) sıra değişimi ayrı "Sırala" offcanvas panelinde sürükle-bırak (düz liste, DataTable'a DEĞİL). Frontend; backend contract DEĞİŞMEZ (mevcut create/update SortOrder reuse).

Önce oku: execution/domains/commercial-suite/work-packs/WP-KNOWLEDGE-SORTORDER-UX-taxonomy-reorder.md · frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/taxonomy.js (sortOrder: 567 picker-sort, 872/920/1004 form payload, 247 client-side, 361/374/387 kolonlar) · frontend/Diten.Web/Views/CRM/Knowledge/Taxonomy.cshtml (taxSortOrder input + toolbar .add-new) · _IndexL10n.cshtml (köprü) · KnowledgeIndex.*.resx (7 dil) · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgePaths/form.js (Sortable deseni — ama tbody'ye değil düz-listeye uygula) · memory l10n-bridge-pascalcase-loader.

NE (frontend; backend contract DEĞİŞMEZ):
 1) "Sıra" (taxSortOrder) input'unu 3 sekme formundan gizle/kaldır — kullanıcı numara görmez.
 2) Create'te sortOrder = max(scope aktif sortOrder)+10 (boşsa 10). Scope: Subject=tüm subject; Topic=seçili parent Subject içi; Profile=tüm profil. Grid client-side yüklü → frontend hesaplar. Edit'te mevcut korunur.
 3) Her sekmeye "Sırala" butonu → offcanvas/modal düz sortable liste (o sekmenin aktif kayıtları, mevcut sıra) → sortablejs DÜZ LİSTE üzerinde (DataTable tbody'sine DEĞİL) → Kaydet: görünür sırayı 10/20/30 yeniden numaralandır → değişen her kaydı mevcut update komutuyla (tüm alanlar + yeni sortOrder) persist → grid+picker reload. Topic paneli parent Subject'e göre filtreli.
 4) resx 7 dil: Reorder/Sırala, ReorderSubjects/Topics/Profiles, ReorderHelp, SaveOrder; _IndexL10n köprü whitelist'e ekle.
KORU/YAPMA: DataTable tbody'sine Sortable UYGULAMA (grid paging/sort/redraw bozulur); reorder yalnız ayrı offcanvas düz-liste; grid default-sort+kolonlar+picker-sort(567) DEĞİŞMEZ; backend create/update contract DEĞİŞMEZ (SortOrder reuse, yeni endpoint YOK); yalnız aktif kayıt sıralanır (archive kuralı); tema/L10n köprüsü; diğer sekme/alan DOKUNMA.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff yalnız taxonomy.js+Taxonomy.cshtml+_IndexL10n.cshtml+KnowledgeIndex.*.resx; backend/picker-sort/diğer resx diff yok. Ayrı commit ("feat(knowledge): WP-KNOWLEDGE-SORTORDER-UX — hide sort field, auto-append, drag-drop reorder panel (MOD-0162-FU02)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: offcanvas düz-liste drag-drop sortablejs ile çalışmıyorsa → kök nedeni bul+additive düzelt; olmuyorsa yukarı/aşağı ok fallback + raporla. Bulk reorder-persist atomik değilse → değişen-only+hata toast+reload (KP persistOrder deseni).
```

## §37 CT bağımsız doğrulama (2026-09-23) → **ACCEPTED (E2)**
```
Commit: 6db7d751 · CT: ACCEPTED E2 · izole worktree @6db7d751 → Diten.Web.Tests 201/0
```
- ✅ **Kapsam (10 dosya, +187/−4):** taxonomy.js (+115) · Taxonomy.cshtml (+30) · _IndexL10n.cshtml (+4) · KnowledgeIndex.*.resx ×7 (+6 anahtar/dil). **Backend/dt-defaults/picker-sort(567)/grid default-sort diff = 0.**
- ✅ **KRİTİK KORU doğrulandı:** `Sortable.create` **`taxReorderList`** (offcanvas düz-liste host) üzerine uygulanmış — **DataTable tbody'sine DEĞİL** (grid paging/sort/redraw korunur; KP kırılganlığı tekrarlanmadı).
- ✅ **Auto-append:** `nextSortOrder(kind,src)` = `max(scope aktif sortOrder)+10` (boş→10); scope Subject=tüm · Topic=seçili parent Subject içi (`subjectId` filtre) · Profile=tüm; submit anında hesaplanır; edit'te mevcut korunur.
- ✅ **"Sıra" alanı gizlendi:** `taxSortOrder` input `d-none` sarıldı (payload için korundu) — kullanıcı numara görmez/yazmaz.
- ✅ **Reorder persist changed-only:** görünür sıra 10/20/30 yeniden numaralandırılır, yalnız `sortOrder != desired` olan kayıt mevcut update (full-replace) ile persist + toast + reload; ▲/▼ fallback yerinde.
- ✅ **resx (7 dil):** 6 yeni anahtar (Reorder/ReorderSubjects/ReorderTopics/ReorderProfiles/ReorderHelp/SaveOrder), L10N terimleriyle tutarlı (tr: Konuları/Başlıkları/Hedef Kitle Profillerini Sırala); köprü whitelist güncellendi; CRLF korundu; key=151/dil.
- ✅ **E2:** Diten.Web.Tests **201/0**; `node --check` temiz.

**WP-KNOWLEDGE-SORTORDER-UX KOMPLE (E2).** E4 = fleet restart (resx+view): Oluştur'da Sıra yok + otomatik son + "Sırala" sürükle-bırak. Reusable: [[knowledge-taxonomy-sortorder-ux]] (no-Sortable-on-tbody gotcha).

