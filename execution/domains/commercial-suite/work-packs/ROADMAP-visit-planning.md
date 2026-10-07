# Ziyaret Planlama — yol haritası ve durum

> **Takip dosyası.** Her adımda güncellenir. Son güncelleme: **2026-10-07** (CT).
> Dal: `test/crm-content-visit-e2e` (main ile senkron, `fe370c9f`).
> Ayrıntılar: [durum analizi](VISIT-PLANNING-current-state-analysis.md) · [mockup brief](mockups/visit-planning/BRIEF-visit-planning-rep-week.md) · [mockup analizi](mockups/visit-planning/VISIT-PLANNING-mockup-analysis.md) · [mobil not](mobile/2026-10-06-visit-planning/MOBILE-NOTE-2026-10-06-visit-planning.md) · [mobil talepler](MOBILE-REQUESTS-2026-10-05-analysis.md)

## Neredeyiz
**Faz 0 bitmek üzere** (yalnız 0.1 bölge ataması kullanıcıda). ☑ **Faz 1 — VP-FIX-1** bitti (E2 + E4). ☑ **Faz 2** E2 + **E4 kabul** (`fecf231e`, 2026-10-07). ☑ D9 / F-1 (VP-FIX-2). ☑ **Faz 2b** E2 kabul (`529a6761d`). ☑ **Faz E2E** (E0–E11). ◐ **E2E-FIX-1/2/3** paketlendi (2026-10-07, paralel). K-7 kabul; **mockup v2 analiz edildi ve S-1..S-4 onaylandı** (Faz 4 girdisi hazır).

## Sıradaki işler (sıralı — 2026-10-07)
| # | İş | Kim | Durum |
|---|---|---|---|
| 1 | **E2E-FIX-1 / 2 / 3** — ajanlara gönder (paralel olabilir); dönüşte CT K13 + §37 | kullanıcı → ajan → CT | ☑ üçü de E2 kabul (`4e3150f1a`, `aaa174ba0`, `dd7cad82a`); E4 → #2 |
| 2 | Bekleyen canlı kontroller (E4): VP-FIX-2 (düzenle korur, Türkçe arama) + VP-2B (aktif kişi sayısı / filtre / seçenekler) + E2E-FIX'ler | fleet yeniden başlat + giriş (kullanıcı), CT kontrol | ☑ **hepsi E4 kabul (2026-10-07)**; test kayıtları: plan `a238bdc5` (2 ziyaret), rapor `939766be` |
| 3 | **Faz 3** — planlama motoru: K-7 ürün listesi veri modeli (S-1..S-4), B-4 dönem planı / hafta durumu, B-5 gün dengeleme, B-6, B-7, B-9, D3, D5, E7-B1 / B2 kararları | CT paketledi (4 paket: 3A ∥ 3D → 3B → 3C) | ◐ 3A ☑ E2 (`e6c383a5b`), 3D ☑ E2 (`ccd93de04`), 3B ☑ E2 (`db877db4d`), 3C ☑ E2 (`e5d3a6b7a`); ☑ E4 (2026-10-07) |
| 4 | **Faz 4** — Web arayüzü mockup v2'ye göre (VP-UI-1..3) | CT paketledi (4A ∥ 4B → 4C → 4D) | ◐ 4A ☑, 4B ☑ (E2); 4C gönderilecek → 4D |
| 5 | **Faz 5** — mobil sözleşme notu (Faz 2 + 2b + E2E-FIX-1 alanları + K-7) | CT yazar, kullanıcı iletir | ☐ |
| 6 | **Faz 6** — Planlanan Ziyaretler sayfası (+ ek konular: numune, amaç, ortak ziyaret…) | canlı analiz → mockup? | ☐ |
| 7 | **SB-3c** — ziyaret başlat / tamamla, gerçekte sunulanlar (`ContentActuals`), yolculuk ilerlemesi yazılır (E9-B1); "son ziyaret" ürün kaynağının ön koşulu | CT paketler | ☐ **ertelendi (kullanıcı kararı 2026-10-07)** — o zamana kadar K-7'deki "son ziyaret" kaynağı boş kalır, yolculuk aşaması yalnız plandaki sıradan ilerler |
| 8 | **Faz 8** — AUD-CRM-1 merkezi denetim → mimari test yeşil → main'e PR | CT paketler, 2 karar sorulur | ☐ en son |
| — | Yan işler: 0.5 TR referans etiketleri (veri) · read-all grant script (rol seçimi) · CYC-UI-FIX-2 E4 · backlog kararları (E2-B2, E4-B4, E9-B4b) | kullanıcı / sonra | ☐ |

## Yapılanlar (2026-10-06)
| Ne | Kanıt |
|---|---|
| `test/crm-content-visit-e2e` main ile senkron; iki çakışma düzeltmesi + AUD-001 defterinden 19 eski komut çıkarıldı | `fe370c9f` (yedek `backup/crm-content-visit-e2e-presync-20261006`) |
| Mobil talepleri analizi | `6232d0b6` |
| Ziyaret Planlama canlı gezildi, durum analizi (A1–A6, B1–B4, C1–C6, D1–D8) | `b38ce3bc`, A6 `1627fd54` |
| Mockup brief + K-4 kararı | `85ba3d05`, `422dc5c9` |
| Mobil ekiplere not (kararlar, ekran görüntüleri, M1–M12, sorular) | `1627fd54` — **kullanıcı iletecek** |
| Mockup geldi, analiz + sıralı iş listesi | `38edaf07` |
| D8: `asdasdasd` test ziyareti arşivlendi (uygulama içinden, kullanıcı onayıyla) | planlanan ziyaret `b41d4a32…` → `archived` |

## Kararlar
| # | Karar |
|---|---|
| K-1 | Temsilci kendi haftasını planlar; ileride ayrı yönetici görünümü |
| K-2 | Bir hafta onaylanınca sonraki haftalar sıklığa göre otomatik taslak |
| K-3 | Temsilci strateji şablonu / kampanya görmez, seçmez |
| K-4 | Temsilci segment seçmez; "Bu hafta görülmesi gerekenler" önerisi; segment yalnız bilgi rozeti |
| K-5 | Bölgesi atanmamış temsilci geçişte tüm hesapları görür + "bölge atanmamış" uyarısı |
| K-6 | Atama şimdilik kullanıcı ↔ bölge (MOD-0151 kaynak ataması) |
| MK-1 | Yeni plan **sağdan açılan panel** (mockup'taki gibi) |
| MK-3 | Plan = dönem planı; içinde haftalar, hafta bazında durum ve onay |
| MK-4 | Yeniden açma gerekçesi haftanın geçmişine + denetim kaydına; bildirim yönetici görünümüyle |
| MK-5 | "Benim planlarım / Ekip" anahtarı mockup'taki gibi görünür; Ekip yönetici görünümü gelene kadar etkin değil |
| MK-6 | Kapasite aşımında sığmayan hedefler sonraki haftaya kayar, nedeni listelenir |
| MK-7 | Eczane sıklığı hesap politikasından; yoksa "dönemde 1" |
| MK-8 | Günlük üst sınır dönem kapasitesinden türetilir |
| MK-9 | Yarım gün kapasiteyi yarıya indirir (çalışma takvimi destekliyor) |
| K-7 | ☑ **Kabul (2026-10-07):** oyunsuz planlama + ziyaretin ürün listesi (kaynak sırası oyun → temsilci seçimi → son ziyaret → portföy), karışık sıra, ekleme / çıkarma kuralları → [karar belgesi](VISIT-PRODUCTS-without-play-decision.md). Ekran: Ziyaret Planlama Hedefler + Haftalar (Faz 4, mockup eki [BRIEF-ADDENDUM-K7](mockups/visit-planning/BRIEF-ADDENDUM-K7-visit-products.md)), Planlanan Ziyaret (Faz 6), rapor (SB-3c). Veri modeli Faz 3. Ek konular (numune, amaç, ortak ziyaret, potansiyel, ürün sıklığı) Faz 6'da konuşulacak |
| — | Tarih biçimi ("5 Oct, 26" tarzı) ve Rota sekmesinin tasarımı değişmez |

## İş listesi
Durum: ☐ bekliyor · ◐ sürüyor · ☑ bitti

### Faz 0 — veri ve kararlar
- ☑ **0.1** Beste'yi (`bestepullukcu@gmail.com`, sistemde "Admin User") **İstanbul ilçelerine** ata — kullanıcı yapıyor. ⚠ İl (area) düzeyine atama reddedildi: kural `medical-representative` → yalnız **zone / microzone** (`TerritoryPositionPolicy`; area-manager → area, regional-manager → region). Kural doğru, değişmez. Test için 5 ilçe: Şişli (birincil), Kağıthane, Beyoğlu, Beşiktaş, Fatih. Durum (2026-10-06): 4 atama var (Şişli, Kağıthane, Beyoğlu, Fatih) — **Beşiktaş eksik**, **dördü de birincil** (yalnız Şişli olmalı). Beste'nin ilçelerindeki hesaplar (0.4 sonrası): Şişli 734 · Fatih 434 · Kağıthane 184 · Beyoğlu 183 = **1.535**.
- ☑ **0.4** (İstanbul + Kocaeli uygulandı 2026-10-06, CT doğruladı: 9.667 hesap ilçeye taşındı, 9.667 il satırı "ended", hesap başına tek aktif atama, 43.374 hesabın hepsinde aktif atama; İstanbul ilde kalan 21, Kocaeli 0; CorrelationId `relink-zones-3908b82f…`. Diğer iller ilde — gerekirse `--cities` olmadan yeniden çalıştırılır.) Hesapları ilçeye bağla (veri): bugün 43.374 hesabın hepsi **il** düzeyinde bağlı → ilçeye atanan temsilcinin hesap listesi boş kalır. Hesaptaki ilçe adı (`AddressLine`) ile ilçe düğümü eşleşmesi: **Türkiye %77, İstanbul 8.548 / 8.864, Kocaeli 513 / 824**; eşleşmeyen il düzeyinde kalır. Script: `scripts/data-load/relink_tr_accounts_to_zones.py` (varsayılan deneme; `--apply` kullanıcıda; geçmiş korunur, eski il satırı "ended"). Eski ilçe / semt adları için eşleme eklendi → deneme: **İstanbul 8.843 / 8.864, Kocaeli 824 / 824**, TR geneli ~%78. ◐ Kullanıcı çalıştıracak. **B-2'den önce gerekli.** ⚠ Bugün okuma tam düğüm eşleşmesi: "İstanbul" il filtresi taşınan hesapları göstermez (alt ağaç okuması B-2'de).
- ☑ **0.2** AUD-001 kararı: **A — CRM merkezi denetime bağlanır, ama en sonda** (Faz 8). O zamana kadar mimari test kırmızı kalır; dal `main`'e PR olmaz.
- ☑ **0.3** D8 test kaydı arşivlendi.

- ☐ **0.5** Referans setlerine TR etiketi (veri): `account-type` (9 değer) ve `medical-specialty` (22 değer) yalnız İngilizce → ekranda "Hospital", "Urology". MOD-0048 yayın akışıyla (yap-onayla) TR etiket sürümü; CT script / adım listesi hazırlar.

### Faz 1 — VP-FIX-1 (mockup'tan bağımsız) — ☑ E2 kabul `b0fe13aa` · ☑ **E4 kabul** (2026-10-06) · paketlendi 2026-10-06, [WP](WP-VP-FIX-1-visit-planning-quick-fixes.md), worktree `C:\tmp\vp-fix-1`
- ☑ D1 liste hedef sayısı (hep 0)
- ☑ D2 onaylı plan kilidi (ekran + sunucu)
- ☑ C2 hafta sonuna ziyaret düşmesin
- ☑ C3 çalışma takvimi 400 (tatiller)
- ☑ A1 strateji şablonu seçicisini kaldır
- ☑ D7 menü: Ziyaret Planlama + Ziyaret Yürütme
- ☑ D6 / A5 bugünkü etiketlerin 7 dili

### Faz 2 — backend temel (mobil de bekliyor) — ◐ paketlendi 2026-10-06 tek pakette: [WP-VP-2](WP-VP-2-rep-scope-names-territory-derivation.md), worktree `C:\tmp\vp-2`. Yeni izin anahtarları `crm.planned-visit.read-all` + `crm.visit-plan.read-all` (yalnız açık grant; grant script'i kullanıcıda)
- ☑ B-8 görünen adlar (D4; mobil D1)
- ☑ B-1 temsilci = oturum + sahiplik (A2; mobil B01) + `resources/me` `displayName` (mobil R-M4)
- ☑ B-2 hedef evreni = bölge ataması; ataması yoksa tümü + uyarı (K-5); bölge dışı işareti (A4)
- ☑ B-3 strateji / kampanya / segment sunucuda türetilir; segment dışının sessizce düşmesi kalkar (A3, A6)

- ☑ **WP-VP-FIX-2** E2 kabul `23e0a9b98` (2026-10-07; ◐ E4 bekliyor) · paketlendi 2026-10-07 ([WP](WP-VP-FIX-2-edit-keeps-targets-turkish-search.md), worktree `C:\tmp\vp-fix-2`): D9 + F-1.
- ☑ **F-1** (VP-2 E4): hesap araması Türkçe harfe duyarlı ("Hamidiye" 0, "HAMİDİYE" 4) — `my-accounts` + genel hesap araması; D9 ile aynı küçük pakete.
- → **Veri:** TUTUKON oyunu arşivli `SEG-URO-DOCTORS` segmentine bağlı → **Faz E2E** adım E1 + E5'te çözülür.
- ☑ **D9** (VP-2 kabulünde bulundu): Ziyaret Planlama Düzenle formu hedef dizilerini boş gönderiyor → taslakta hafta / dönem değiştirmek tüm hedefleri siler. Düzeltme: form mevcut seçimi göndersin **ya da** sunucu `null` dizi = "dokunma" kabul etsin (tercih: ikisi birden). Küçük paket, Faz 2b'den önce.
- ☐ Grant: `scripts/rbac/grant_visit_planning_read_all_97c5.py --role <onaylayan yönetici rolü> --apply` (Auth yeniden başlatıldıktan sonra; kullanıcı rolü seçer).

- → Takip **WP-E2E-FIX-3**'e taşındı: aynı arama sorunu `TerritoryModelRepository` (ad / kod; kaçışsız) + `SegmentCandidateSource` (i katlaması yok) — küçük.

### Faz E2E — TUTUKON uçtan uca test (kullanıcı giriş yapar, CT yürütür) — VP-FIX-2 kabulünden sonra; Faz 2b ajan işiyle paralel
Plan: [E2E-TUTUKON-content-to-visit-plan.md](E2E-TUTUKON-content-to-visit-plan.md).
- ☑ E0 envanter (salt okuma; 2026-10-07 — içerik / yol / yolculuk / uygun segment yok, oyun yeniden kurulmalı)
- ☑ E1 aktif segment `E2E-TUT-SINDIRIM` — **yalnız gastroenteroloji** (910 üye); bulgular E1-B1 segment düzenleyici referans değerleri boş (hata), E1-B2 10K aday sınırı (aile / dahiliye kurulamıyor), E1-B3 global-products 400
- ☑ E2 içerik — `KC-2026-E70D11` (detaylama) + `KC-2026-2EC045` (kullanım / dozaj) yayımlandı; bulgular E2-B1 ürün seçici 100 sınırı (TUTUKON yok), E2-B2 içerik onaysız yayımlanıyor (karar), E2-B3 ham kodlar
- ☑ E3 yol — `KP-2026-269A07` MLR onaylı (sema) + arşiv PDF + **yayında**
- ☑ E4 yolculuk — `CEJ-2026-8AD806` 2 aşama (Farkındalık → Pekiştirme, ikisi de TUTUKON yolu) **yayında**; bulgular E4-B1..B4
- ☑ E5 oyun — STR-TUTUKON-URO **v2 aktif** (segment E2E-TUT-SINDIRIM, ürün satırı → CEJ-2026-8AD806, sıklık vfp-2026-mi82xi); bulgular E5-B1..B3
- ☑ E6 sıklık — `vfp-2026-mi82xi` aktif (ayda 2); bulgu E6-B1
- ☑ E7 plan `a42373cb` + önizleme: **32 / 32 doktor `resolved`** (TUTUKON · CEJ-2026-8AD806 · Farkındalık → 2. ziyarette Pekiştirme · yol adımları); VP-FIX-2 düzenle-korur ✓; bulgular E7-B1 iki dal düzleşiyor, E7-B2 adım süresi yok, E7-B3 tek gün / sıklık aralığı, E7-B4 düzenle sonrası 404
- ☑ E8 uygula — 33 planlanan ziyaret, adlar + içerik kalemleri + köken (segment / oyun v2) dolu; bulgu E8-B1 onaysız + mesajsız uygula
- ◐ E9 rapor gönderildi ✓; **yolculuk ilerlemesi yazılmıyor** (E9-B1, journeyId gönderilmiyor, journey_progress 0); "ne sunacağım" ekranda yok (E9-B2); aşama serbest metin (E9-B3); sonuç kodu doğrulanmıyor (E9-B4); ileri tarihe rapor (E9-B5)
- ◐ E10 planda öngörü ✓ (2. ziyaret Pekiştirme), gerçekleşenden ilerleme ✗
- ☑ E11 mobil örnek yanıtlar → [E2E-SAMPLE-RESPONSES-2026-10-07.md](mobile/2026-10-06-visit-planning/E2E-SAMPLE-RESPONSES-2026-10-07.md)
- ☑ **E2E-FIX — üç paket E2 + E4 kabul (2026-10-07).** Küçük takipler: onay alt metni tekrarı (Ziyaret Yürütme / Planlama), yolculuk ayrıntısında ham durum / kaynak kodu, içerik listesi ürün adları 100 sınırı, 8 ham `WarningMessage` anahtarı, 10K üyelik çözümü:
  - ☑ **E2 kabul `4e3150f1a`** (CRM 2313+flake, Web 711/0, mimari 26 sabit; ☐ E4) [WP-E2E-FIX-1](WP-E2E-FIX-1-visit-execution-report-and-plan-apply.md) Ziyaret Yürütme: "ne sunacağım" (E9-B2), rapor plandaki yolculuk / aşamayı taşır + aşama seçimi (E9-B1 ön koşul, E9-B3), sonuç kodu hatası (E9-B4), ileri tarih kuralı (E9-B5), onay / etiket / başlık (E9-B6), düzenle 404 (E7-B4), uygula onay + kilit (E8-B1) — worktree `C:\tmp\e2e-fix-1`
  - ☑ **E2 kabul `dd7cad82a`** (CRM 2347/0/5, Web 733/0, mimari 26; ☐ E4) [WP-E2E-FIX-2](WP-E2E-FIX-2-knowledge-chain-authoring-screens.md) İçerik / Bilgi Yolu / Yolculuk: E2-B1, E2-B3, içerik sürümü, E3-B1, E3-B3, E3-B4 (yalnız gönderen geri çeker — CT kuralı), E4-B1..B3 — worktree `C:\tmp\e2e-fix-2`
  - ☑ **E2 kabul `aaa174ba0`** (CRM 2342/0/5, Web 721/0, mimari 26; ☐ E4) [WP-E2E-FIX-3](WP-E2E-FIX-3-segment-play-frequency-screens.md) Segment / Oyun / Sıklık: **⚠ E5-B2 eski oyun sürümü yenisini eziyor (çözücü v1'i seçiyor)**, E5-B1, E5-B3, E1-B1, E1-B3, E1-B2 aday indirme, Türkçe arama takibi, E6-B1 — worktree `C:\tmp\e2e-fix-3`
  - Pakete girmeyenler: E9-B1 yazan uç → **SB-3c** (ertelendi, Faz 6'dan sonra) · E7-B1 / B2 / B3, E8-B2 → Faz 3 · E7-B5 → Faz 4 · E2-B2, E4-B4, E9-B4b → Backlog

Kurallar:
- Yazmalar test kapsamında onaylı; CT her yazmayı önceden söyler.
- Kayıtlar `E2E-TUT-` önekiyle açılır.
- Onaylarda sema girişi (kullanıcı) gerekir.
- **Test kayıtları kalır** (kullanıcı kararı 2026-10-07): `E2E-TUT-` segment / içerik / yol / yolculuk / oyun v2 / sıklık / plan `a42373cb` / 33 planlanan ziyaret / rapor `c229bb38` silinmez; Faz 3–4 ve E2E-FIX kabul testlerinde yeniden kullanılır.

### Faz 2b — mobil iş yeri listesi talebi (2026-10-06; [talep](mobile/2026-10-06-account-list/BACKEND-CRM-ACCOUNT-LIST-ACTIVE-CONTACTS-REQUIREMENTS.md) · [CT yanıtı](mobile/2026-10-06-account-list/MOBILE-ANSWERS-2026-10-06-account-list.md)) — ☑ **E2 kabul `529a6761d` (2026-10-07): [WP-VP-2B](WP-VP-2B-account-list-active-contacts-filter-options.md)** §37 (CRM 2303/0/5, mimari 26 sabit, CT sabotajı 4 kırmızı); ☐ E4 (fleet yeniden başlatma)
- ☐ **M-ACC-1** (R1 + R2): `activeContactCount` (sayfa başına tek toplama, `/contacts` aktif kuralıyla birebir) + `hasActiveContacts=true|false` filtresi (VE; `total` filtreli; geçersiz → 400). Hem `GET /api/crm/accounts` hem B-2'nin `visit-plan/my-accounts` ucu. Sayım `crm.account.read` altında.
- ☐ **M-ACC-2** (R3-a): `GET /api/crm/accounts/filter-options` (`crm.account.read`): hesaplarda bulunan bölge düğümleri + iş yeri türleri; bölgesi atanmış temsilcide yalnız kendi bölgesi.
- ☐ R4 → **0.5** (uç açık, eksik TR etiket verisi). Mobil bilgilendirildi.
- ☐ Faz 2 + 2b bitince mobil **sözleşme notu** (adlar, sahiplik, `resources/me` adı, `my-accounts`, sayım / filtre / seçenekler).

### Faz 3 — planlama motoru — ☑ **dört paket E2 + E4 (2026-10-07)**; Faz 4'e devredilenler: yeniden aç Web vekili + E4, oturum DTO'sunda `products` (E4-3C-B1), eski `committed` planların gösterimi kararı; veri: 28 Eki yarım gün takvimde yok ([tasarım](DESIGN-VP-FAZ3-planning-engine.md)): ☑ E2 `e6c383a5b` [WP-VP-3A](WP-VP-3A-period-plan-week-status-frequency.md) dönem planı + hafta onayla / yeniden aç + tek plan + sıklık (`C:	mpp-3a`) · ☑ E2 `4536fad2c` + CT `ccd93de04` [WP-VP-3D](WP-VP-3D-target-status-reads.md) durum okumaları + D5 (`C:	mpp-3d`, 3A ile paralel) · ☑ E2 `db877db4d` [WP-VP-3B](WP-VP-3B-day-balancing-capacity.md) gün dengeleme + kapasite + yarım gün + taşma (3A sonrası) · ☑ E2 `e5d3a6b7a` [WP-VP-3C](WP-VP-3C-visit-product-list.md) ürün listesi K-7 (3A + 3B sonrası). CT varsayılanları F3-1…F3-6 (tasarım §4): yeniden açma yeni komut (listesiz 26 → 27, Faz 8'de bağlanır), eski planlar olduğu gibi, E7-B1 yalnız ana dal, E7-B2 adım süresi süreye girmez, portföy verisi yok
- ☐ B-4 dönem planı + hafta durumu / onay / yeniden aç / otomatik sonraki hafta; sıklık yok = dönemde 1; eşit dağılım; eczane sıklığı (B1–B3, MK-3/4/7)
- ☐ B-5 gün dengeleme, hafta sonu / tatil / yarım gün, kaydırma + nedenler (C1–C3, MK-6/8/9)
- ☐ B-6 "bu hafta görülmesi gerekenler" + doktor başına hedef / yapılan / kalan / son ziyaret (B4)
- ☐ B-7 haftalık + dönem kapasitesi; tipik ziyaret süresi; tahmini saat (C4, C5)
- ☐ B-9 segment rozeti alanı
- ☐ D3 dönem başına tek plan + boş taslak silme · D5 toplu okuma ucu
- ☐ **K-7 veri modeli:** planlanan ziyaretin ürün listesi (ürün + rol + kaynak + yolculuk / aşama), süre listeden, kaynak önceliği oyun → seçim → son ziyaret → portföy, "karışık" döngü ([karar](VISIT-PRODUCTS-without-play-decision.md))
- ☐ E2E'den gelen çözücü / süre kararları: **E7-B1** yolun iki dalı düzleşiyor (dal seçim kuralı) · **E7-B2** adım süresi süreye girmiyor · **E7-B3 / E8-B2** tek gün + "ayda 2" aralığı (B-4 / B-5 ile çözülür)

### Faz 4 — Web arayüzü (mockup'a göre) — ◐ **paketlendi 2026-10-07:** ☑ E2 `6ae5a0585` [WP-VP-4A](WP-VP-4A-backend-alignment-for-ui.md) backend uyumu (seçili ürünler okuma, yeniden aç vekili, eski planlar sabit hafta, liste sayıları; `C:	mpp-4a`) ∥ ☑ E2 `28a57eae7` [WP-VP-4B](WP-VP-4B-list-new-plan-panel-detail-header.md) liste + yeni plan çekmecesi + detay üstü + durumlar + sayfa iskeleti (`C:	mpp-4b`) → [WP-VP-4C](WP-VP-4C-targets-tab.md) Hedefler + ürün seçici → [WP-VP-4D](WP-VP-4D-weeks-tab-doctor-panel.md) Haftalar + doktor paneli + yeniden aç. **D3 temizliği:** Beste'nin 6 boş taslağı arşivlendi (kullanıcı onayı, 2026-10-07).
- ☑ **Mockup v2 geldi (2026-10-07), K-7 ek brief'i tam karşılıyor** → [v2 analizi](mockups/visit-planning/VISIT-PLANNING-mockup-v2-analysis.md). Rota / liste / yeni plan paneli dokunulmamış. **S-1..S-4 kullanıcı onayladı (2026-10-07):** ürün değişikliği onaylı haftalara dokunmaz (yalnız taslak / öngörülen; onaylı hafta → Planlanan Ziyaret, Faz 6) · toplu uygulamada sınır aşımı → sığmayan sonraki ziyarete + mesaj · önerilen ürünün rolü oyundan, kilitli · seçim oturumda doktor başına, mevcut güncelleme komutuyla (yeni komut YOK); apply planlanan ziyaretlere kopyalar. "Son ziyaret" kaynağı SB-3c'ye bağlı.
- ☐ VP-UI-1 liste + yeni plan paneli + detay üst kısım + durumlar + Ekip anahtarı (etkin değil)
- ☐ VP-UI-2 Hedefler (bölge uyarısı, bölge dışı ekleme)
- ☐ VP-UI-3 Haftalar + doktor paneli + yeniden açma penceresi
- ☐ C6 çok ziyaretli durakta doktor adları (Rota tasarımı değişmeden; kullanıcı onayıyla)

### Faz 5 — mobil
- ☑ Mobil not iletildi (2026-10-06) · ☑ 3 soru yanıtlandı ([yanıtlar](mobile/2026-10-06-visit-planning/MOBILE-ANSWERS-2026-10-06.md)): düzenlemede değerleri geri gönder; `resources/me` tek kaynak + `displayName` gelecek (B-1); ad için ara istek kabul, adlar B-8 ile yanıta girecek
- ☐ Faz 2–3 alanları kesinleşince sözleşme notu (+ SB-3-MOB)

### Faz 6 — Planlanan Ziyaretler sayfası
- ☐ Canlı analiz → (mockup?) → paketler: GUID (D1), tamamen İngilizce sayfa, sahiplik, T1–T3, `target_inactive`

### Faz 8 — AUD-CRM-1: CRM'i merkezi denetim kaydına bağla (kullanıcı kararı: düzeltmelerden sonra, en son)
- ☐ CRM denetim yayıncısını düzelt (kategori / işlem adı), merkezi kayda gönderimi aç, kabul edilmiş iz olarak sabitle
- ☐ 26 işlem (bilgi yolu inceleme + Güvenlik Metni / Ülke Yasal Profili) + Faz 3'ün yeni işlemleri (haftayı onayla / yeniden aç…) bağlanır; yeniden açma gerekçesi (MK-4) denetim kaydına da yazılır
- ☐ O zaman sorulacak iki karar: (1) doğrudan Platform merkezi kaydı mı, CRM kendi izi mi · (2) düzenlemeye tabi işlemde kayıt yazılamazsa işlem dursun mu
- ☐ Mimari test (AUD-001) yeşil → ancak bundan sonra `main`'e PR

### Faz 7 — sonra
- ☐ Yönetici görünümü (Ekip) · sıradaki içerik (SB-3c) · saha temsilcisi rolü (F-RBAC) · pozisyon tabanlı atama · check-in / anti-fraud (MOD-0280)

## Backlog — sonra konuşulacak (kullanıcı kararı 2026-10-07: "bunları sonra konuşuruz")
| # | Konu | Neden karar gerekir |
|---|---|---|
| **E2-B2** | İçerik formdan doğrudan `published` seçilerek yayımlanıyor; inceleme / MLR onayı yok. Yayın kapısı yalnız bağlı iddia varsa çalışıyor. | Tanıtım içeriği için onay akışı zorunlu mu (uyum)? Bilgi yolunda MLR var; içerik tekil yayında yok. |
| **E9-B4b** | Ziyaret sonuç kodu serbest metin; hiçbir serviste sonuç kodu referans kümesi yok (ekran "referans verilerinden gelir" diyor). | Kiracı referans kümesi mi (BRD), sabit sözlük mü? Raporlama için kodlu liste gerekir. |
| **E4-B4** | Yolculuğu oluşturan kişi kendisi yayımlayabiliyor. Bilgi yolunda "yayınlayan ≠ gönderen" var, yolculukta yok. | Görevler ayrılığı (SoD) yolculuğa da uygulansın mı? |

## 0.1 — Bölge ataması adımları (kullanıcı)
Sayfa: `http://localhost:5001/CRM/TerritoryManagement/Models/2e89f9e6-54e0-4fd4-905f-4e5f7f4f31de/ResourceAssignments` (Bölge Yönetimi → TR-Territory → Kaynak Atamaları). İki atama yapılır (İstanbul ve Kocaeli için birer kez):

| Alan | Değer |
|---|---|
| Pozisyon | `medical-representative — Medical Representative` |
| Kapsam | `Territory Subtree` |
| Hedef Düğüm | `TR-34-ISTANBUL — İstanbul (area)` · ikinci atamada `TR-41-KOCAELI — Kocaeli (area)` |
| İş Birimi Kapsamı | boş |
| Kaynak Tipi | `Kullanıcı` |
| Kullanıcı seçin | `Admin User (bestepullukcu@gmail.com)` |
| Başlangıç | 2026-10-01 (dönem başı) · Bitiş boş |
| Atama Kaynağı | `manual` |
| Birincil | İstanbul'da işaretli, Kocaeli'de işaretsiz |
| Değişiklik Nedeni | "Ziyaret planlama testi — temsilci bölge ataması" |

Bittikten sonra CT kontrol eder: `GET /api/crm/resources/{kullanıcı}/territory-responsibilities` iki düğüm dönmeli.
