# Ziyaret Planlama — yol haritası ve durum

> **Takip dosyası.** Her adımda güncellenir. Son güncelleme: **2026-10-06** (CT).
> Dal: `test/crm-content-visit-e2e` (main ile senkron, `fe370c9f`).
> Ayrıntılar: [durum analizi](VISIT-PLANNING-current-state-analysis.md) · [mockup brief](mockups/visit-planning/BRIEF-visit-planning-rep-week.md) · [mockup analizi](mockups/visit-planning/VISIT-PLANNING-mockup-analysis.md) · [mobil not](mobile/2026-10-06-visit-planning/MOBILE-NOTE-2026-10-06-visit-planning.md) · [mobil talepler](MOBILE-REQUESTS-2026-10-05-analysis.md)

## Neredeyiz
**Faz 0 bitmek üzere** (yalnız 0.1 bölge ataması kullanıcıda). Sıradaki: **Faz 1 — VP-FIX-1**.

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
- ◐ **0.1** Beste'yi (`bestepullukcu@gmail.com`, sistemde "Admin User") İstanbul + Kocaeli'ye ata — **kullanıcı yapıyor** (adımlar aşağıda).
- ☑ **0.2** AUD-001 kararı: **A — CRM merkezi denetime bağlanır, ama en sonda** (Faz 8). O zamana kadar mimari test kırmızı kalır; dal `main`'e PR olmaz.
- ☑ **0.3** D8 test kaydı arşivlendi.

### Faz 1 — VP-FIX-1 (mockup'tan bağımsız)
- ☐ D1 liste hedef sayısı (hep 0)
- ☐ D2 onaylı plan kilidi (ekran + sunucu)
- ☐ C2 hafta sonuna ziyaret düşmesin
- ☐ C3 çalışma takvimi 400 (tatiller)
- ☐ A1 strateji şablonu seçicisini kaldır
- ☐ D7 menü: Ziyaret Planlama + Ziyaret Yürütme
- ☐ D6 / A5 bugünkü etiketlerin 7 dili

### Faz 2 — backend temel (mobil de bekliyor)
- ☐ B-8 görünen adlar (D4; mobil D1)
- ☐ B-1 temsilci = oturum + sahiplik (A2; mobil B01) + `resources/me` `displayName` (mobil R-M4)
- ☐ B-2 hedef evreni = bölge ataması; ataması yoksa tümü + uyarı (K-5); bölge dışı işareti (A4)
- ☐ B-3 strateji / kampanya / segment sunucuda türetilir; segment dışının sessizce düşmesi kalkar (A3, A6)

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
