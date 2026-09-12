# WorkCenterNext — mock ↔ gerçek dikiş denetimi

> **Statü:** ENVANTER. Bu dilimde hiçbir davranış değiştirilmedi — kod, resx, test yok.
> **Tarih:** 2026-07-26 · **Branch:** `feature/pss/candcap0006-wc1-work-item-projection`
> **Kapsam:** `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/` altındaki 8 dosya (4.407 satır).

## Neden

WorkCenterNext frontend-first kuruldu, sonra backend bağlandı. Aynı sınıftan üç hata reaktif olarak
tek tek bulundu (`catalogVisible` allowlist'i, `item.group` uydurma kuyruk adı, `CURRENT_USER` unvanı).
Ortak kök: **mock modülünde tanımlı bir sabit, provenance kontrolü olmadan gerçek kalemlere uygulanıyor.**
Bu denetim o sınıfı sistematik tarar.

## Yöntem

Üç mekanik geçiş. Hiçbiri "şüpheli görünen yere bak" değil — hepsi tam sayım.

- **P1 — Uydurma sabitlerin erişilebilirliği.** `mock-data.js`'teki her modül-seviyesi sabiti listele;
  her biri için `provenance === 'api'` olan bir kaleme ulaşıp ulaşmadığını izle.
- **P2 — İhraç yüzeyi.** `WorkCenterNextData`'nın ihraç ettiği her üye × `app.js`'teki her `data.*`
  erişimi (89 erişim, 15 farklı üye); her biri için guard var mı.
- **P3 — `toPresentation` alan-alan.** 60+ `item.X = …` atamasının her birinde sağ taraf
  **payload-türevli** mi yoksa **mock-türevli** mi.

Sınıflandırma: 🔴 gerçek kalemi bozuyor · 🟠 yanlış bilgi gösteriyor · 🟢 gerçek yola ulaşamıyor.

---

## 1. P1 — `mock-data.js` sabitleri

| Sabit | Satır | Gerçek yola ulaşıyor mu | Guard | Sınıf | Sonuç |
|---|---|---|---|---|---|
| `TODAY_ISO` / `TODAY` | 9-10 | **Evet** — `computeSla` (98-104), activity `ago` (258-263), ihraç `todayIso` (13 erişim) | **yok** | 🟠 | Tarih **2026-07-24'e donmuş**; gerçek kalemlerin SLA durumu ve takvim "bugün"ü sabit bir güne göre hesaplanıyor. |
| `CURRENT_USER` | 11 | **Evet** — `data.currentUser`, 20 erişim; `app.js:384` koşulsuz render | **yok** | 🟠 | *(bilinen açık 3)* Gerçek kullanıcı, kapsam seçicisinde kendi unvanı yerine "Operasyon PMO Lideri" görüyor. |
| `MODULE_LABELS` | 27-35 | **Evet** — `moduleLabel()` → `toPresentation:179-180` | **yok** | 🟠 | *(bilinen açık 4)* Gerçek kalemlerin modül adı sabit Türkçe haritadan; 7 dil yok. |
| `DELEGATORS` | 13-16 | **Evet** — `app.js:367` koşulsuz render, `134`, `352` | **yok** | 🟠 | Gerçek kullanıcıya, kendisi için var olmayan "Deniz Koç / Aylin Ersoy" vekâlet kapsamları listeleniyor. |
| `TYPE_ICON` | 23 | Evet — `toPresentation:186` | yok (gereksiz) | 🟢 | Saf ikon eşlemesi, bilinmeyen tip `bx-circle`'a düşüyor; uydurma **bilgi** değil. |
| `MEETINGS` | 17-19 | Hayır | `buildMeetings` → `showcaseFixturesEnabled()` (317) | 🟢 | Showcase dışında `[]`. |
| `NOTES` | 20-22 | Hayır | `buildNotes` → `showcaseFixturesEnabled()` (318) | 🟢 | Showcase dışında `[]`. |
| `VISIBLE_CATALOG_IDS` | 43-51 | Hayır | `provenance === 'fixture'` (223) | 🟢 | *(eski hata 1, düzeltildi)* |
| `ON_BEHALF_OF` | 12 | — | — | 🟢 | **Ölü ihraç:** `data.onBehalfOf` app.js'te 0 erişim. |

## 2. P2 — İhraç yüzeyi × `data.*` erişimleri

| Üye | Erişim | Gerçek yolda | Guard | Sınıf |
|---|---|---|---|---|
| `currentUser` | 20 | **Evet** (384 koşulsuz; 1342, 1366, 1943 fallback) | yok | 🟠 |
| `todayIso` | 13 | **Evet** (725, 1450, 1457, 2638 snooze; 1762, 1786 takvim; 2663-2668 snooze min-date) | yok | 🟠 |
| `delegators` | 3 | **Evet** (367 koşulsuz) | yok | 🟠 |
| `resolveLabel` | 16 | Evet | — | 🟢 Payload etiketlerini resx'e çözen ortak mapper; uydurma yok. |
| `tabFor` / `segmentFor` | 5 | Evet | — | 🟢 Payload alanlarının saf fonksiyonu. |
| `toPresentation` | 2 | Evet (2582, 2778) | **kısmi** | 🟠 → §4 |
| `getActions` | 1 | Evet (212) | — | 🟢 `item.actions` klonu. |
| `buildItems` · `buildTriggers` · `buildMeetings` · `buildNotes` · `showcaseFixturesEnabled` | 10 | Hayır | `loadWorkItems:3504` showcase dalı | 🟢 |
| `status` · `computeSla` · `computeBlocked` · `onBehalfOf` | 0 | — | — | 🟢 Ölü ihraçlar. |
| `parentTask.data.priority` / `.assigneeUserId` | 2464-2465 | — | — | 🟢 **Yanlış pozitif:** bu `data` mock modülü değil, `TasksApi` yanıt zarfı. |

## 3. P3 — `toPresentation` alanları: payload mı, mock mu

Payload-türevli olanlar (çoğunluk) sorunsuz. **Mock-türevli olup gerçek kaleme ulaşanlar:**

| Alan | Satır | Kaynak | Sınıf | Sonuç |
|---|---|---|---|---|
| `sourceModule`, `sourceModuleName` | 179-180 | `MODULE_LABELS` | 🟠 | Bkz. §1. |
| `slaState`, `slaDiffDays` | 210-211 | `computeSla` ← donmuş `TODAY` | 🟠 | Projeksiyon `SlaState` **taşımıyor** (`WorkAggregationModels.cs:187-232` alan listesinde yok), dolayısıyla `item.slaState \|\| sla.state` daima mock hesabına düşüyor. |
| `activity[].ago` | 258-263 | donmuş `TODAY` | 🟠 | "N gün önce" sabit güne göre. |
| `timesheet.startedAt` | 265-273 | `Date.now() - (37 * 60000)` | 🟢 | **Bugün ulaşılamaz:** `TaskWorkItemProvider.ResolveCapabilities` (269-294) `timeTracking` **bildirmiyor** → `timesheet = null`. **Gizli tuzak:** bir sağlayıcı `timeTracking` bildirdiği an gerçek kalemler uydurma 37 dakikalık geçmiş süre gösterir. |
| `typeIcon` | 186 | `TYPE_ICON` | 🟢 | Bkz. §1. |

**Projeksiyonda olmayıp UI'ın okuduğu alan:**

| Alan | Nerede render ediliyor | Sınıf | Sonuç |
|---|---|---|---|
| `priority` | `app.js:1662` (Öncelik sütunu), `priorityLabel:189`, `PRIORITY_KIND:39` | 🟠 | `WorkAggregation` özelliğinin tamamında "priority" **hiç geçmiyor**. Gerçek kalemde `undefined` → `PRIORITY_KIND[undefined]` = `undefined` → çip sınıfı `wcn-chip-undefined`, etiket `t(undefined)`. Öncelik sütunu gerçek kalemler için anlamsız. |

## 4. Provenance damgasının sessizce değişmesi

`toPresentation(fixture, options)` — `options` verilmezse provenance **`'api'`'ye düşüyor** (168. satır, bilinçli varsayılan).
İki çağrı `options` vermiyor:

- `app.js:2582` — `applyReviewMeeting` içinde kalemi yeniden projekte ediyor.
- `app.js:2778` — showcase self-task oluşturma (2754'te showcase guard'ı var).

Sonuç: fixture kökenli bir kalem yeniden projekte edildiğinde **provenance `'fixture'` → `'api'` oluyor**;
`catalogVisible` (223) böylece `true`'ya dönüyor.

**Sınıf: 🟠, 🔴 değil** — çünkü gerçek transition kapısı `isRealTaskItem` (2319-2320) provenance'a **ek olarak**
`source.providerCode === 'tasks'` istiyor ve **hiçbir fixture `providerCode: 'tasks'` kullanmıyor** (fixtures/ taraması: 0 eşleşme).
Yani damga bozulsa da fixture gerçek sunucu çağrısına dönüşemiyor. Ama koruma, provenance'ın kendisine değil
**ilgisiz ikinci bir koşula** yaslanmış durumda: MOD-0024 fixture'ı eklenip `providerCode: 'tasks'` verildiği gün
bu 🔴 olur.

## 5. Diğer dosyalar

| Dosya | Bulgu | Sınıf |
|---|---|---|
| `work-items-api.js` | Provenance'ı **açıkça** `'api'` veriyor (74). Sözleşme doğrulaması var, geçersiz kalem düşürülüp raporlanıyor (45-64). Uydurma sabit yok. | 🟢 |
| `l10n.js` | Saf resx köprüsü; `t`/`tf`/`tn` eksik anahtarda anahtarı döndürüyor (görünür bozukluk, sessiz değil). Uydurma veri yok. | 🟢 |
| `migration-fixture-adapter.js` | Yalnız `fixtureKind === 'migration'` girdisini işliyor (5); gerçek projeksiyon kalemi bu değeri taşımıyor. İçindeki sabit tarih `'2026-07-24T09:00:00+03:00'` (43) yalnız fixture'a yazılıyor. | 🟢 |
| `trigger-response-resolver.js` | Yalnız trigger fixture'ları; gerçek yolda `state.triggers = []` (3521, "no provider yet"). | 🟢 |
| `task-detail-resolver.js` | Girdi alanlarının hepsi payload-türevli (`systemState`, `blockedState`, `slaState`, `actionDepth`). Kendi uydurma sabiti yok — ama `slaState` üzerinden §3'teki donmuş-tarih hatasını **devralıyor** (`overdue` banner'ı yanlış tetiklenebilir). | 🟢 (türev risk) |
| `quick-create.js` | `TasksL10n` üzerinden çeviriyor, `TasksApi.create` ile gerçek yazma yapıyor. Mock sabiti yok. | 🟢 |

---

## 6. `runBulk` sorusunun kesin cevabı

**Soru:** `runBulk` → `applyTransition(item, action.key)` çağrısında `isRealTaskItem` yok;
tek-aksiyon yolunda var. Bu asimetri UI'dan erişilebilir mi?

*(Doğrulanan satırlar: guard'sız çağrı `app.js:2973`, guard'lı karşılığı `app.js:2486` —
`applyAction` gövdesinin ilk satırı. Asimetri kodda gerçek.)*

**Cevap: HAYIR — toplu aksiyon yolu bugün UI'dan tamamen erişilemez. Ölü kod.**

Render yolunu okuyarak bulunan **üç bağımsız kopukluk**, herhangi biri tek başına yeterli:

1. **Tabloda seçim kutusu yok.** `mountWorkCenterDataTable` sütun tanımı (`app.js:1656-1665`) şu 9 sütun:
   `control · type · title · module · status · priority · sla · requester · action`. 0. sütun
   `columnDefs`'te açıkça `render: () => ''` (1669-1671) — Responsive'in `+` katlama kontrolü, checkbox değil.
   `<thead>` (1600-1609) da checkbox `<th>`'i içermiyor.
2. **`state.tableSelected`'i dolduracak markup hiç üretilmiyor.** Onu yazan tek yer olay dinleyicileri
   `app.js:3456` (`[data-wcn-check-all]`) ve `3463` (`[data-wcn-check]`); bu iki attribute'u **emit eden hiçbir
   şablon yok**. Tek benzer isim `data-wcn-check-item` (1048) ve o **checklist** öğesi toggle'ı — başka özellik.
3. **`bulkBar` hiç çağrılmıyor.** `app.js:1695`'te tanımlı, çağrı sitesi **sıfır**. `data-wcn-bulk` butonlarını
   üreten tek yer orası (1704); `performBulk`'u tetikleyen tek yol da o attribute (3390-3391).

Zincir: checkbox yok → `tableSelected` hep boş → `bulkBar` zaten çağrılmıyor → `data-wcn-bulk` butonu yok →
`performBulk` tetiklenemez → `runBulk` çalışmaz.

**Dördüncü, bağımsız kopukluk (kod canlansa bile):** `runBulk` `applyTransition`'a ulaşmadan önce
`if (!action || !action.bulk || action.disabled) { failed.push(item); return; }` (2971) süzgecinden geçiyor.
`action.bulk` ← `supportsBulk` (mock-data.js:139).
- `TaskWorkItemProvider` (`providerCode: "tasks"`) **`SupportsBulk: false`** yazıyor — 467 ve 483, iki çağrının ikisi de.
- `supportsBulk: true` gönderen tek yer `WorkItemProjectionService` (171, 196, 214) ve onu kullanan
  `WorkflowApprovalWorkItemProvider` — yani **`providerCode` ≠ `"tasks"`**.

`isRealTaskItem` **yalnız** `providerCode === 'tasks'` eşleştiği için, "toplu işleme girebilen kalemler" kümesi ile
"`isRealTaskItem` true olan kalemler" kümesi **bugün ayrık**. Eksik guard'ın tetiklenmesi için bir sağlayıcının aynı anda
`providerCode: "tasks"` **ve** `SupportsBulk: true` göndermesi gerekir; öyle bir sağlayıcı yok.

**Erişilebilir olsaydı ne olurdu:** MOD-0023 onay kalemleri (`supportsBulk: true`) `applyTransition`'a düşerdi →
ekran değişir, veritabanı değişmez; üstelik tek-aksiyon yolundaki `console.warn` (2487-2493) uyarısı olmadan sessizce.
Tek-aksiyonda bir kez düzeltilen hatanın aynısı.

## 7. Yöntem doğrulaması (non-vacuity)

Bilinen iki açık, denetimin **kendi geçişleriyle bağımsız olarak** yakalandı:

| Bilinen açık | Hangi geçiş yakaladı | Nasıl |
|---|---|---|
| 3 — `CURRENT_USER` unvanı | **P1** ve **P2** | P1: `mock-data.js:11` modül sabiti, ihraç ediliyor (302), `toPresentation`/guard dışı. P2: `data.currentUser` 89 erişimin en yoğunu (20); `app.js:384` `buildHeader` içinde ve `buildHeader` provenance'a bakmadan her render'da çağrılıyor. |
| 4 — `MODULE_LABELS` | **P1** ve **P3** | P1: `mock-data.js:27-35` sabiti, `moduleLabel()` ile dışarı sızıyor. P3: `item.sourceModule` (179) sağ tarafı mock-türevli, provenance kontrolü yok. |

Her ikisi de "🟠 gerçek kaleme ulaşıyor, guard yok" olarak, listeye elle eklenmeden çıktı.
Yöntem ayrıca **önceden bilinmeyen** beş bulgu üretti: donmuş `TODAY` (SLA + takvim + activity),
`DELEGATORS`, projeksiyonda olmayan `priority`, `timesheet` 37-dakika çapası (gizli tuzak),
provenance damgasının sessizce değişmesi.

Yöntemin **bilinen sınırı:** P1-P3 yalnız `mock-data.js` kökenli uydurmayı arar. `app.js`'in kendi içinde ürettiği
sabit metinler (`'Onaylandı'`, `'İmzalandı'`, `'Çözüldü'`, `'İnceleme bekliyor'`, `'Kapandı'` — 2250-2275,
`nativeStatusText` argümanı) bu geçişlerin **kapsamı dışında**; onlar önceki dilimde ayrı bir taramayla bulunmuştu.
Bir sonraki denetim "app.js içi sabit kullanıcı metni" geçişini de eklemeli.

---

## 8. Önerilen düzeltme sırası (BU DİLİMDE YAPILMADI)

Sıra, "kaç gerçek kalemi × ne kadar yanlış" çarpımına göre:

1. **🟠 Donmuş `TODAY` (P1/P3).** Tek sabit, en geniş etki: her gerçek kalemin SLA rozeti, `overdue` banner'ı,
   takvimin "bugün" vurgusu ve snooze min-date doğrulaması. Bugün 2 gün kaymış; her gün büyüyor.
   Gecikmiş bir işi "zamanında" göstermek, denetimdeki en yanıltıcı tek şey.
2. **🟠 `CURRENT_USER` unvanı (bilinen açık 3).** Kullanıcının kendi kimliği hakkında yanlış bilgi; her render'da
   ekranın üstünde. Düzeltme ucuz: unvanı sunucudan (oturum/pozisyon) al ya da provenance yokken alt-etiketi gizle.
3. **🟠 `DELEGATORS`.** Aynı bileşen (kapsam seçici), aynı düzeltme turunda yapılmalı — var olmayan kişileri
   vekâlet kapsamı diye listelemek 2'den daha yanıltıcı, sadece daha az görünür.
4. **🟠 `MODULE_LABELS` (bilinen açık 4).** Modül adı doğru ama tek dilde; 7-dil kapısını ihlal ediyor.
   Kalıcı çözüm sağlayıcının lokalize modül adı göndermesi (WC-3 sözleşme işi) — ara çözüm resx.
5. **🟠 `priority` projeksiyonda yok.** Ya sağlayıcı gönderecek (WC-3) ya da sütun/çip gerçek kalemde gizlenecek.
   Şu an `undefined` render ediliyor.
6. **🟠 Provenance damgasının sessizce değişmesi (§4).** Bugün zararsız; korumayı provenance'ın kendisine
   yaslamak (ya da `toPresentation`'ı options'sız çağırmayı yasaklamak) bunu kalıcı olarak kapatır.
7. **🟢→ölü kod: toplu aksiyon yolu (§6).** Ya seçim UI'ı tamamlanır **ve** `runBulk` `applyTransition`
   çağrısından önce `isRealTaskItem` kontrolü kazanır, ya da `bulkBar`/`runBulk`/`performBulk`/`tableSelected`
   tümden kaldırılır. Yarı-canlı bırakmak, ilk `SupportsBulk: true` gönderen `tasks` sağlayıcısında sessizce 🔴 olur.
8. **🟢 gizli tuzak: `timesheet` 37-dakika çapası.** `timeTracking` bildirilen ilk gün 🟠 olur; o bildirimle
   birlikte düzeltilmeli.
9. **🟢 temizlik.** Ölü ihraçlar: `onBehalfOf`, `status`, `computeSla`, `computeBlocked` (app.js'te 0 erişim).

**Regresyon notu:** 1-5 arası düzeltmelerin hepsi katman-arası sözleşme (projeksiyon ne taşıyor / shell ne uyduruyor)
konusu. Test kapsamı dışında kaldıkları için üç hata da canlıda bulundu; her düzeltme, gerçek payload'a karşı
`mapPayload` seviyesinde bir regresyon testiyle gelmeli — `workcenter-next-pool-group.test.js` bu deseni izliyor.
