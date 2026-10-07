# Ziyaret Planlama — yol haritası ve durum

> **Takip dosyası.** Her adımda güncellenir. Son güncelleme: **2026-10-06** (CT).
> Dal: `test/crm-content-visit-e2e` (main ile senkron, `fe370c9f`).
> Ayrıntılar: [durum analizi](VISIT-PLANNING-current-state-analysis.md) · [mockup brief](mockups/visit-planning/BRIEF-visit-planning-rep-week.md) · [mockup analizi](mockups/visit-planning/VISIT-PLANNING-mockup-analysis.md) · [mobil not](mobile/2026-10-06-visit-planning/MOBILE-NOTE-2026-10-06-visit-planning.md) · [mobil talepler](MOBILE-REQUESTS-2026-10-05-analysis.md)

## Neredeyiz
**Faz 0 bitmek üzere** (yalnız 0.1 bölge ataması kullanıcıda). ☑ **Faz 1 — VP-FIX-1** bitti (E2 + E4). ☑ **Faz 2** E2 + **E4 kabul** (`fecf231e`, 2026-10-07). ⚠ **D9** düzenleme formu hedefleri siliyor — öncelikli küçük düzeltme. Sonra **Faz 2b** (mobil iş yeri listesi).

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
| — | Tarih biçimi ("5 Oct, 26" tarzı) ve Rota sekmesinin tasarımı değişmez |

## İş listesi
Durum: ☐ bekliyor · ◐ sürüyor · ☑ bitti

### Faz 0 — veri ve kararlar
- ◐ **0.1** Beste'yi (`bestepullukcu@gmail.com`, sistemde "Admin User") **İstanbul ilçelerine** ata — kullanıcı yapıyor. ⚠ İl (area) düzeyine atama reddedildi: kural `medical-representative` → yalnız **zone / microzone** (`TerritoryPositionPolicy`; area-manager → area, regional-manager → region). Kural doğru, değişmez. Test için 5 ilçe: Şişli (birincil), Kağıthane, Beyoğlu, Beşiktaş, Fatih. Durum (2026-10-06): 4 atama var (Şişli, Kağıthane, Beyoğlu, Fatih) — **Beşiktaş eksik**, **dördü de birincil** (yalnız Şişli olmalı). Beste'nin ilçelerindeki hesaplar (0.4 sonrası): Şişli 734 · Fatih 434 · Kağıthane 184 · Beyoğlu 183 = **1.535**.
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

- ☐ Takip: aynı arama sorunu `TerritoryModelRepository` (ad / kod; kaçışsız) + `SegmentCandidateSource` (i katlaması yok) — küçük.

### Faz E2E — TUTUKON uçtan uca test (kullanıcı giriş yapar, CT yürütür) — VP-FIX-2 kabulünden sonra; Faz 2b ajan işiyle paralel
Plan: [E2E-TUTUKON-content-to-visit-plan.md](E2E-TUTUKON-content-to-visit-plan.md).
- ☑ E0 envanter (salt okuma; 2026-10-07 — içerik / yol / yolculuk / uygun segment yok, oyun yeniden kurulmalı)
- ☑ E1 aktif segment `E2E-TUT-SINDIRIM` — **yalnız gastroenteroloji** (910 üye); bulgular E1-B1 segment düzenleyici referans değerleri boş (hata), E1-B2 10K aday sınırı (aile / dahiliye kurulamıyor), E1-B3 global-products 400
- ☐ E2 içerik
- ☐ E3 yol
- ☐ E4 yolculuk
- ☐ E5 oyun yeni sürüm (yeni segment + yolculuk)
- ☐ E6 sıklık
- ☐ E7 plan + önizleme (`contentStatus = resolved`)
- ☐ E8 uygula
- ☐ E9 ziyaret raporu + yolculuk ilerlemesi
- ☐ E10 sonraki aşama
- ☐ E11 mobil örnek yanıtlar
- ☐ **E2E-FIX (toplu paket, test bitince — kullanıcı kararı 2026-10-07):** E1-B1 segment referans değerleri, E1-B2 10K aday sınırı (ek blokları aday sorgusuna indirme), E1-B3 global-products 400, Türkçe arama takibi (TerritoryModel + SegmentCandidateSource) + E2–E11'de çıkacak bulgular

Kurallar:
- Yazmalar test kapsamında onaylı; CT her yazmayı önceden söyler.
- Kayıtlar `E2E-TUT-` önekiyle açılır.
- Onaylarda sema girişi (kullanıcı) gerekir.

### Faz 2b — mobil iş yeri listesi talebi (2026-10-06; [talep](mobile/2026-10-06-account-list/BACKEND-CRM-ACCOUNT-LIST-ACTIVE-CONTACTS-REQUIREMENTS.md) · [CT yanıtı](mobile/2026-10-06-account-list/MOBILE-ANSWERS-2026-10-06-account-list.md)) — ◐ **paketlendi 2026-10-07: [WP-VP-2B](WP-VP-2B-account-list-active-contacts-filter-options.md)**, worktree `C:\tmp\vp-2b` (M-ACC-1 + M-ACC-2 tek pakette; yalnız CRM)
- ☐ **M-ACC-1** (R1 + R2): `activeContactCount` (sayfa başına tek toplama, `/contacts` aktif kuralıyla birebir) + `hasActiveContacts=true|false` filtresi (VE; `total` filtreli; geçersiz → 400). Hem `GET /api/crm/accounts` hem B-2'nin `visit-plan/my-accounts` ucu. Sayım `crm.account.read` altında.
- ☐ **M-ACC-2** (R3-a): `GET /api/crm/accounts/filter-options` (`crm.account.read`): hesaplarda bulunan bölge düğümleri + iş yeri türleri; bölgesi atanmış temsilcide yalnız kendi bölgesi.
- ☐ R4 → **0.5** (uç açık, eksik TR etiket verisi). Mobil bilgilendirildi.
- ☐ Faz 2 + 2b bitince mobil **sözleşme notu** (adlar, sahiplik, `resources/me` adı, `my-accounts`, sayım / filtre / seçenekler).

### Faz 3 — planlama motoru
- ☐ B-4 dönem planı + hafta durumu / onay / yeniden aç / otomatik sonraki hafta; sıklık yok = dönemde 1; eşit dağılım; eczane sıklığı (B1–B3, MK-3/4/7)
- ☐ B-5 gün dengeleme, hafta sonu / tatil / yarım gün, kaydırma + nedenler (C1–C3, MK-6/8/9)
- ☐ B-6 "bu hafta görülmesi gerekenler" + doktor başına hedef / yapılan / kalan / son ziyaret (B4)
- ☐ B-7 haftalık + dönem kapasitesi; tipik ziyaret süresi; tahmini saat (C4, C5)
- ☐ B-9 segment rozeti alanı
- ☐ D3 dönem başına tek plan + boş taslak silme · D5 toplu okuma ucu

### Faz 4 — Web arayüzü (mockup'a göre)
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
