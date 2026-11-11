# MVP-1 FIX-QUI1 — kanıt (WP-MVP1-FIX-QUI1, MOD-0175 Bloke Stok Web düzeltmeleri) — 2026-10-14

> **Durum: ajan sonucu, CT onayı bekliyor.** Taban ölçüldü: `1f904aa83` (prompt'un beklediği). Dal `fix/mvp1-qui1`, yalıtılmış worktree
> `C:\Users\Ali\erp-vnext-wt-qui1`. Push yok, PR yok; main'e, MVP-1 dalına ve ana checkout'a yazılmadı. Sahibin filosuna (5000–5070, 4010)
> dokunulmadı; `.claude/launch.json` "web" (5001) başlatılmadı. Servis kodu ve sözleşmeler değişmedi; controller olarak yalnız `QuarantineController`.
> Yer (docs-organization, beş soru): tek bir WP'nin tarihli, bitmiş ölçüm kaydı → 2. soru → `docs/records/audits/2026-10/`.

## 1. Commit'ler

| # | Commit | Adım |
|---|---|---|
| 1 | `6ff3c638b` | `fix(web)` — blocked.js, details.js, retest.js, settings.js, `QuarantineController.ListBlocked`, Blocked / _BlockStockModal görünümleri, 7 resx + `Quarantine.Keys`, vitest çağıran listesine 1 satır |
| 2 | `4a08ad452` | `test(web)` — `Diten.Web.Tests/JavaScript/Node/quarantine-blocked.test.mjs` (10) + `Diten.Web.Tests/Quarantine/QuarantineBlockedFixTests.cs` (8) |
| 3 | (bu commit) | bu kanıt + MOD-0175 pack §19 notu |

## 2. Üç bulgu — önce / sonra

| # | Bulgu | Önce (`1f904aa83`) | Sonra |
|---|---|---|---|
| 1 | "Bloke kaldırılsın mı?" gövdesi | `blocked.js:76` `subtext: body.outerHTML` → pencere `subtext`'i METİN olarak kaçışlar (`DitenText`), okuyucu `<div class="text-start small"><div class="fw-medium mb-1" data-ub-subject>…` görür | `subtextHtml: body.outerHTML` — pencerenin işaretleme kapısı (`_GlobalConfirmation.cshtml:259-260`: `const body = hasFormBody ? String(options.subtextHtml) : (subtext ? text(subtext) : '');`). Gövde görünümün SABİT şablonu (`_UnblockModal.cshtml`), konu satırı `textContent` ile yazılır, serileştirmede kaçışlı kalır; kullanıcı verisini innerHTML ile yazan yol yok. Okuyucu: "Parasetamol 500 mg · L-2026-0001 · 50 EA" + açıklama cümlesi |
| 2 | "Stoğu bloke et" miktarı | `blocked.js:155` `if (row) …qty.value = row.onHand` — yazılan 50, satır seçilince 200 | Yazılan miktar KORUNUR; alan boşsa satır miktarı öneri olarak yazılır; miktarın altında "Kullanılabilir: 200 EA" (`Quarantine.BlockStock.Available`); satır miktarını aşan miktar hemen alan hatası (`QtyInvalid`) gösterir ve kaydetme isteği GİTMEZ (mevcut alan hatası deseni). Boş satır seçeneği artık 0. satır sayılmaz |
| 3 | Kapanmış kayıt "0 EA", etiketsiz | Liste `status`'suz istenir (servis hepsini döner); durum gösterilmez; rozet sayfanın toplamı | Kapı varsayılan **`status=Open`** ister; filtre "Durum": **Açık** (boş = varsayılan) / **Kapandı** / **Numune saklandı** / **Tümü** (`All` → status yok), mevcut filtre panelinde; kapı gösterdiği durumu cevaba yazar (`"status"`); Open dışındaki kayıt "Bekleyen" sütununda durum etiketi taşır (`data-qr-blocked-status`), işlem menüsü boş (Open değilken hiçbir işlem görünmez); sekme rozeti yalnız açık kayıtları sayar |

**Rozet (ölçüldü):** diğer sekmelerden `ui.loadCounts` → `/Quarantine/list/blocked?start=0&length=1` (status yok) → kapının varsayılanı Open → açık sayısı.
Bloke Stok sayfasında `onResponse`: cevap `status: "Open"` ise rozet = bu sayfanın toplamı; başka durum seçiliyse (Kapandı / Tümü …) rozet
o toplamla yazılmaz, açık sayısı ayrı bir varsayılan istekle okunur (Node testi: Closed 7 kayıt → rozet 2 = açık sayısı).

## 3. Taranan yerler — `outerHTML` / `subtext`

| Yer | Durum | Ne yapıldı |
|---|---|---|
| `Quarantine/blocked.js:76` `subtext: body.outerHTML` (Blokeyi kaldır) | **ham HTML** (bulgu 1) | `subtextHtml` |
| `Quarantine/blocked.js:99` `subtext: esc([...])` (İncelemeyi tamamla) | **çift kaçış** — pencere metni kendisi kaçışlar; `esc` önceden uygulanınca okuyucu `&amp;` / `&quot;` / `&#39;` görür | `esc` kaldırıldı (metin olarak verilir) |
| `Quarantine/details.js:209` `subtext: esc(t('Quarantine.Workflow.ReapplyText'))` | çift kaçış | `esc` kaldırıldı |
| `Quarantine/details.js:220` `subtext: esc(t('Quarantine.Workflow.WithdrawText'))` | çift kaçış | `esc` kaldırıldı |
| `Quarantine/details.js:239` `subtext: esc(t('Quarantine.Details.StartReviewText'))` | çift kaçış — **görünür**: tr metni `Kayıt "İncelemede" durumuna geçer…` → `&quot;İncelemede&quot;` | `esc` kaldırıldı |
| `Quarantine/retest.js:44` `subtext: esc(t('Quarantine.Retest.Confirm.Text', …))` | çift kaçış — **görünür**: tr `… Karantina Kuyruğu'na eklenir` → `Kuyruğu&#39;na` | `esc` kaldırıldı |
| `Quarantine/settings.js:168` `subtext: esc(t('Quarantine.Settings.Template.Text'))` | çift kaçış | `esc` kaldırıldı |
| `Quarantine/decide.js`, `disposition.js`, `submit.js`, `queue.js`, `details.js` diğer `innerHTML` satırları | pencere değil; değerler `esc(…)` ile yazılıyor | dokunulmadı |
| **Quarantine dışı:** `Traceability/Lots/status.js:86` `subtext: body.outerHTML` | **aynı ham HTML hatası** | kapsam dışı (yalnız Quarantine dosyaları) → **FU-QUI1-LOTSTATUS** önerisi |

Dispose / Decide / Submit pencereleri `showConfirm` kullanmıyor (kendi sayfaları); `subtext` geçen yerlerin tamamı yukarıda.

## 4. Testler

| Dosya | Test | Ne ölçer |
|---|---|---|
| `JavaScript/Node/quarantine-blocked.test.mjs` (Node, **gönderilen** blocked.js + `_UnblockModal.cshtml` şablonu + `_GlobalConfirmation` gövde kuralı + tr resx; küçük kaydeden sahte DOM) | (1) ×4 | gövde kuralı hâlâ aynı; pencere metninde `<div`, `class=`, `&lt;` yok, konu + cümle var; kötü niyetli ad (`<img …>`) işaretlemede etiket olmaz, metin olarak görünür; "İncelemeyi tamamla" konusu `&amp;`/`&quot;` göstermez |
| | (2) ×3 | yazılan 50 satır seçiminden sonra 50, ipucu "Kullanılabilir: 200 EA"; boşken 200 önerilir; 250 hemen alan hatası, kaydetme isteği yok, 50 ile tek istek `quantity: 50` |
| | (3) ×3 | filtre alanı `{ id: 'filterStatus', key: 'status' }`; Kapandı kayıt etiketli ve işlemsiz; rozet yalnız açık |
| `Quarantine/QuarantineBlockedFixTests.cs` | `The_Bloke_Stok_page_holds_against_the_shipped_script` | Node dosyasını `dotnet test`'ten koşar (`# pass 10`, `# fail 0`; ExpiryF5NodeTests deseni) |
| | `The_blocked_list_asks_for_Open_by_default_and_forwards_the_status_filter` ×5 | gateway'e giden istek: (yok / boş) → `status=Open`; Closed / SampleRetained iletilir; `All` → status yok; cevapta `status` |
| | `The_page_offers_the_status_filter_with_Open_first_and_the_available_hint_under_the_quantity` | render: seçenekler `""=Open, Closed, SampleRetained, All`; `qr-block-available` miktar ile alan hatası arasında |
| | `The_new_keys_are_delivered_and_real_in_all_seven_languages` | 6 yeni anahtar `Quarantine.Keys`'te, 7 dilde dolu, İngilizce kopyası değil; `Available` `{0}` `{1}` taşır |

Mevcut `QuarantineWebTests.Seven_languages_carry_exactly_the_Quarantine_keys_with_real_text` ve `Every_key_a_page_or_script_asks_for_is_delivered` de yeni anahtarları kapsar.

## 5. Önce / sonra

| | Taban `1f904aa83` | Sonra |
|---|---|---|
| `Diten.Web.Tests` | **2272 / 2272** | **2280 / 2280** (+8) |
| `dotnet build Diten.Web.Tests.csproj --no-incremental` | 0 hata, 24 uyarı | 0 hata, **24** uyarı — 0 yeni (Quarantine dosyalarında 0) |
| Quarantine filtresi | 29 | 37 |

## 6. Sabotajlar (filtre `FullyQualifiedName~Diten.Web.Tests.Quarantine`, 37)

Uygula → build → filtre → **özgün baytları geri yaz** (mtime damgalı) → build → filtre. Döngü sonunda dört dosyanın SHA-256'sı aynı, `git diff`
döngü öncesiyle aynı, `git status --porcelain` boş.

| # | Sabotaj | Kırmızı | Geri |
|---|---|---|---|
| s1 | `subtext: body.outerHTML` geri (blocked.js) | 1 C# (`The_Bloke_Stok_page_holds_against_the_shipped_script`) ← Node: (1) "Bloke kaldırılsın mı?" … no raw `<div class=…>`; (1) the subject stays TEXT … | 37 / 37 |
| s2 | satır seçiminde koşulsuz `field.value = row.onHand` (blocked.js) | 1 C# ← Node: (2) typed 50 stays 50 …; (2) empty quantity …; (2) above the row amount … | 37 / 37 |
| s3 | varsayılan `status=Open` kaldırıldı (`ListBlocked`) | 2 — `The_blocked_list_asks_for_Open_by_default_…(status: null …)` ve `(status: "" …)` | 37 / 37 |
| s4 | tr `Quarantine.Blocked.Status.Closed` silindi | 3 — `The_new_keys_are_delivered_…`, `QuarantineWebTests.Seven_languages_carry_exactly_…`, Node köprüsü ← (3) a Closed record shows its status label … | 37 / 37 |

## 7. Önizleme (kendi portu)

- **Kurulum:**
  - **Web, 127.0.0.1:5011:** bu worktree'nin üretim `Program`'ı; `WebApplicationFactory` + ikinci host olarak **gerçek Kestrel**.
  - **Kimlik:** test host ara katmanı her isteğe tenant_user + Quarantine anahtarlarını içeren bir principal koyar. Token **imzalanmadı**; çerez opak bir değer.
  - **Gateway:** sahte gateway, **127.0.0.1:5112**. Kanonik veri: BLK-2026-0002 Open 50 EA, BLK-2026-0001 Closed 0 EA, lot stoğu 200 EA.
  - **Sahibin filosu:** hiçbir isteğe girmedi.
- **HTTP kanıtı (kendi host'umdan):**
  - `GET /Quarantine/Blocked` sayfasında `filterStatus` seçenekleri sırasıyla `""` Open, `Closed`, `SampleRetained`, `All`. Ayrıca `qr-block-available` alanı ve `qr-unblock-template` şablonu var.
  - `list/blocked` varsayılanı → `status=Open`, yalnız BLK-2026-0002.
  - `&status=Closed` → BLK-2026-0001 Closed.
  - `&status=All` → ikisi birden.
  - Sahte gateway'in log'u: `…/blocked?page=1&pageSize=10&status=Open`, `…&status=Closed`, `…/blocked?page=1&pageSize=10`.
- **Ekran görüntüsü: alınamadı.**
  - İlk açılış sayfayı gösterdi, ancak tarayıcı bölmesi `localhost` çerezlerini portlar arasında paylaşıyor. Sahibin Web'inden (5001) kalma oturum çerezi 5011'e de gitti ve önizleme kimliğini ezdi ("Şirket listesi yüklenemedi").
  - Host bu çerezleri atacak şekilde düzeltildi (`access_token`, `refresh_token`, `.AspNetCore.*` düşürülür).
  - Sonraki iki gezinme ise "denied or failed" döndü; bölme gizliydi, büyük olasılıkla bir izin onayı bekliyordu. Zorlanmadı.
  - Davranış kanıtı Node testi ve HTTP çıktısıdır. CT ekran görüntüsü isterse aynı düzenek tek komutla yeniden kurulabilir.
- **Kapanış:** süreçler durduruldu; 5011 ve 5112 dinlemiyor.
- **Not — sonraki "web-verify" oturumları için:** tarayıcıda `localhost` çerezleri portlar arası paylaşıldığından, sahibin oturumu kendi portumuzdaki Web'e de taşınır. Ya `127.0.0.1` kullanılmalı (bölme bu oturumda reddetti) ya da çerezler düşürülmeli.

## 8. Karar noktaları / sapmalar

- **D-1 — paylaşılan vitest dosyasına 1 satır.** `frontend/Diten.Web/tests/global-confirm-text-is-text.test.js` `subtextHtml` kullanan çağıranları
  adıyla sabitliyor; listeye `wwwroot/assets/js/Quarantine/blocked.js` eklendi (yalnız ekleme). Bu vitest süiti bu ortamda koşulamadı
  (`node_modules` yok; ne worktree'de ne ana checkout'ta). Aynı tarama Node ile taklit edildi: Quarantine'de önce-kaçışlayan `subtext` kalmadı.
- **D-2 — kapsamın biraz ötesi (aynı aile).** Bulgu 1'in yanında 6 çift kaçış yeri düzeltildi (§3); hepsi Quarantine dosyaları, metinler artık
  pencere kapısında bir kez kaçışlanıyor.
- **D-3 — `Traceability/Lots/status.js:86` aynı ham HTML hatası** — Quarantine dışı, dokunulmadı → **FU-QUI1-LOTSTATUS**.
- **D-4 — sanal LotBlocked satırları** (`blockedId` null, CT-D4): varsayılan `status=Open` altında listede kalıp kalmadıkları servisin kararıdır;
  gerçek servise karşı ölçülmedi (filo yasak). Gerekirse "Tümü" hepsini gösterir.
- **D-5 — durum etiketi** ayrı bir sütun yerine "Bekleyen" sütununda (Open dışı kayıtta bekleyen bilgisi anlamsız); kayıtlı görünümlerin sütun
  indeksleri değişmesin diye. İşlem menüsü Open dışı kayıtta boş; "Detay" sayfası Bloke Stok için yok.

## 9. Diff-stat `1f904aa83..HEAD` (bu commit öncesi)

```text
 .../JavaScript/Node/quarantine-blocked.test.mjs    | 228 +++++++++++++++++++++
 .../Quarantine/QuarantineBlockedFixTests.cs        | 185 +++++++++++++++++
 .../Diten.Web/Controllers/QuarantineController.cs  |   8 +-
 .../Resources/Views/Quarantine/Quarantine.ar.resx  |   6 +
 .../Resources/Views/Quarantine/Quarantine.en.resx  |   6 +
 .../Resources/Views/Quarantine/Quarantine.es.resx  |   6 +
 .../Resources/Views/Quarantine/Quarantine.fr.resx  |   6 +
 .../Resources/Views/Quarantine/Quarantine.ru.resx  |   6 +
 .../Resources/Views/Quarantine/Quarantine.tr.resx  |   6 +
 .../Resources/Views/Quarantine/Quarantine.zh.resx  |   6 +
 frontend/Diten.Web/Views/Quarantine/Blocked.cshtml |   8 +
 frontend/Diten.Web/Views/Quarantine/Quarantine.cs  |   6 +
 .../Views/Quarantine/_BlockStockModal.cshtml       |   1 +
 .../tests/global-confirm-text-is-text.test.js      |   1 +
 .../wwwroot/assets/js/Quarantine/blocked.js        |  59 +++++-
 .../wwwroot/assets/js/Quarantine/details.js        |   6 +-
 .../wwwroot/assets/js/Quarantine/retest.js         |   2 +-
 .../wwwroot/assets/js/Quarantine/settings.js       |   2 +-
 18 files changed, 533 insertions(+), 15 deletions(-)
```

## 10. Yeni anahtarlar (7 dil)

| Anahtar | tr | en |
|---|---|---|
| `Quarantine.BlockStock.Available` | Kullanılabilir: {0} {1} | Available: {0} {1} |
| `Quarantine.Blocked.Filter.Status` | Durum | Status |
| `Quarantine.Blocked.Status.Open` | Açık | Open |
| `Quarantine.Blocked.Status.Closed` | Kapandı | Closed |
| `Quarantine.Blocked.Status.SampleRetained` | Numune saklandı | Sample retained |
| `Quarantine.Blocked.Status.All` | Tümü | All |

fr / es / zh / ar / ru gerçek çevirilerle (ör. Clôturé / Cerrado / 已关闭 / مغلق / Закрыт).

## 11. Araçlar

- Sabotaj aracı `sabotage-qui1.js` (worktree kökünden `node sabotage-qui1.js`): sabotaj başına çapa tam bir kez bulunur, değiştirilir, build + filtre, özgün
  baytlar geri yazılır, build + filtre; sonunda SHA-256 / `git diff` / porcelain kontrolü.
- Önizleme: `qui1-preview/gateway.js` (sahte gateway 5112) + `qui1-preview/Host` (`WebApplicationFactory<Program>` + Kestrel 5011, kimlik ara katmanı,
  sahibin çerezlerini düşürür). Oturum karalama alanında; ürün kodu değil.
