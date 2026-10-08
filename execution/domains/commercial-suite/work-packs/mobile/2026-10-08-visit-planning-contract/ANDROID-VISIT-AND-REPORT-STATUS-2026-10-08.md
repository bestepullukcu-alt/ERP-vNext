# Ziyaret ve ziyaret raporu — Android durum raporu

Tarih: 2026-10-08.

Kaynaklar (hepsi salt-okunur incelendi):

- **Android:** yerel `master` `b33b4ef` (push edilmedi), modül `:MyPossibility`.
- **Backend:** ERP-vNext.
  - `main` `b994c813a`.
  - Ziyaret çalışmalarının yapıldığı branch: `test/crm-content-visit-e2e`. Bu branch `main`'in 94 commit önünde ve henüz `main`'de değil.

Kesin olmayan yerler "tahmin" ya da "karar bekliyor" olarak işaretlendi.

---

## 1. Kısa özet

**Android'de olanlar:**

- planlı ziyaret listesi,
- hafta/ay takvimi,
- ziyaret oluşturma ve düzenleme,
- onaylama, iptal (sebepli) ve arşivleme,
- ziyaret raporunu görüntüleme (yalnız okuma).

**Android'de olmayanlar:**

- ziyaret raporu yazma, ziyaret sonucu girme (yapıldı / yapılamadı / ertelendi) ve rapor düzeltme;
- check-in / check-out;
- çevrimdışı çalışma.

**Neden eksik:** Rapor yazmanın yetki anahtarları backend'de tanımlı, ama hiçbir role verilmemiş. Uçlar da hâlâ "yalnız geliştirme ortamı" yedek yetkisiyle korunuyor (F-RBAC açık). Check-in ise anti-fraud backend'ine bağlı.

**Bu inceleme sırasında bulunan önemli sorun (§9.1) — Android'de düzeltildi (2026-10-08):** Mobilde oluşturulan ziyaret
sunucuda **"taslak"** doğuyordu; backend'de taslağı "planlı"ya çeviren bir uç olmadığı ve onay yalnız planlı ziyaret için
yapılabildiği için **mobilde açılan ziyaret onaylanamıyordu.** Android artık oluştururken `planStatus = planned` gönderiyor.
Backend'deki boşluk (taslak → planlı ucu yok) Web ve diğer yollar için hâlâ açık.

**Rapor zamanı:**

| Soru | Cevap |
|---|---|
| En erken ne zaman? | Ziyaret günü (UTC takvim günü). |
| En geç ne zamana kadar? | Sınır yok. |
| Gönderilmiş rapor ne kadar süre değiştirilebilir? | 60 dakika. |
| Sonrasında? | Gerekçeli "düzeltme" ile, süresiz. |

Ayrıntı §6.4'te.

---

## 2. Kavramlar

| Kavram | Ne demek | Backend kaydı |
|---|---|---|
| **Planlı ziyaret** (planned visit) | Kimin, ne zaman, hangi hedefe (doktor / işyeri / eczane), hangi amaçla gideceği. | `PlannedVisit` |
| **Ziyaret raporu** (visit report) | Ziyaretin gerçekte ne olduğu: sonuç, sunulan içerik, numune, geri bildirim. Her planlı ziyarete **en fazla bir** rapor. | `VisitReport` |
| **Kaynak** (resource) | Ziyareti yapacak kişi. Bugün "kullanıcı = kaynak"; oturumdaki kişi (`GET api/crm/resources/me`). | — |
| **Hedef** (target) | `account` (işyeri), `contact` (doktor), `account-contact-link` (işyerindeki doktor), `pharmacy` (eczane). | — |
| **Kaynak türü** (source) | `manual` (elle), `route-plan` (haftalık planlama motoru). Mobil yalnız `manual` oluşturur. | — |

---

## 3. Android'de neler var

### 3.1 Ekranlar

| Ekran | Ne yapıyor |
|---|---|
| **Ziyaret listesi** | Kullanıcı tarih aralığı seçer. Liste 7 günlük sayfalarla okunur, "Daha fazla" ile devam eder. Satırda tarih, saat, hedef adı ("Doktor: Dr. … · Hastane"), ziyaret tipi, amaç, durum ve kod var. Filtreler: durum, amaç, hedef, arşivlenenler. Yenile, Tekrar dene ve Takvim butonları; "Yeni ziyaret" butonu. |
| **Takvim** | Hafta / ay görünümü, önceki / sonraki, Bugün. Haftanın ilk günü cihaz diline göre. 7 gün gösterilir, hafta sonu dahil. Günde kaç ziyaret olduğu gösterilir. Seçilen günün ziyaretleri altta listelenir; satırda rapor durumu da var ("Rapor: none / draft / submitted / amended"). |
| **Yeni ziyaret** | Hedef (CRM seçici: işyeri → doktor), tarih, başlangıç–bitiş saati, ziyaret tipi, amaç, açıklama, not. Temsilci, strateji, kampanya ve segment alanı yok. Kod uygulama tarafından üretilir (`PV-` + UUID). |
| **Ziyaret düzenleme** | Aynı form. Planın durumuna göre üç işlem: **Onayla**, **İptal et** (sebep sorulur, en fazla 500 karakter), **Arşivle** (onay diyaloğu; geri alınamaz). Arşivlenmiş ziyaret salt okunur. Çakışmada "Yeniden yükle". |
| **Ziyaret raporu bölümü** | Düzenleme ekranının içinde, **salt okunur**. Ayrıntı §6.1'de. |
| **Kaynak seçimi** | `resources/me` birden fazla kaynak dönerse açılan seçim diyaloğu. Backend bugün her zaman tek kaynak döndüğü için **pratikte hiç açılmıyor**. |

### 3.2 Kullanılan uçlar

| Uç | Nerede |
|---|---|
| `GET api/crm/planned-visits` (tarih aralığı, kaynak, hedef, durum, amaç, arşiv filtreleri) | Liste |
| `GET api/crm/planned-visits/{id}` | Düzenleme, takvim satırı adı |
| `POST api/crm/planned-visits` | Yeni ziyaret |
| `PUT api/crm/planned-visits/{id}` (`expectedVersion` ile) | Düzenleme kaydı |
| `POST …/{id}/confirm`, `…/{id}/cancel`, `…/{id}/archive` | Onay, iptal, arşiv |
| `GET api/crm/planned-visits/contract` | Tip / amaç / durum listeleri, desteklenen filtreler |
| `GET api/crm/visit-report/calendar?from&to&resourceId` | Takvim |
| `GET api/crm/visit-report?plannedVisitId=` | Rapor görüntüleme |
| `GET api/crm/resources/me` | Kaynak (temsilci) |
| `GET api/crm/accounts/{id}`, `GET api/crm/contacts/{id}` | Sunucu ad göndermediğinde hedef adı (ara çözüm) |

- Rapor yazan hiçbir uç çağrılmıyor.
- Silme (DELETE) yok.
- Bu liste bir test tarafından sabitleniyor (`ErpPermissionBoundaryTest`).

### 3.3 Hedef adları

- **Sunucu adı gönderirse** (WP-VP-2: `targetDisplayName`, `accountDisplayName`, `contactDisplayName`, `targetInactive`): ad doğrudan gösterilir, pasif hedef "(pasif)" yazılır.
- **Göndermezse:**
  - Ad işyeri / kişi uçlarından okunur.
  - Yalnız ekrandaki satırlar için okunur.
  - Her kayıt oturum başına bir kez okunur, aynı anda en fazla 4 istek gider.
- **Hiçbir durumda** kimlik (GUID) ya da hedef kodu gösterilmez.

### 3.4 Çevrimdışı

**Yok.**

- Ziyaretler cihazda saklanmıyor; Room, DataStore veya dosya kullanılmıyor.
- Ekran yenilenince her şey sunucudan yeniden okunuyor.
- Ağ hatasında yalnız "Tekrar dene" var.
- Dönme (rotation) gibi durumlarda yalnız form, filtre ve takvim seçimi bellekte korunuyor.

---

## 4. Ziyaret ne zaman açılabiliyor? Kurallar

İki tarafın kuralları:

| Kural | Android (gönderilmeden önce) | Backend |
|---|---|---|
| **Zorunlu alanlar** | hedef, tarih, saat aralığı, ziyaret tipi, amaç | hedef, tarih, tip, amaç, kaynak, kod. **Saat aralığı backend'de isteğe bağlı**, Android'de zorunlu. |
| **Geçmiş tarih** | Oluştururken yasak. Düzenlemede yalnız **taslak** ziyarette serbest. Bugün serbest. | Aynı: `planned_visit_date_in_past`. Güncellemede yalnız taslakta geçmiş tarih kabul. |
| **"Bugün" nedir** | UTC takvim günü | UTC takvim günü. CRM'de kiracı saat dilimi yok. |
| **Gelecek tarih sınırı** | Yok | Yok |
| **Saat** | Başlangıç ve bitiş birlikte; bitiş başlangıçtan sonra. | Aynı (`planned_visit_time_window_invalid`). |
| **Süre** | Gönderilmiyor (sunucunun değeri korunuyor). | İsteğe bağlı, 1–1440 dk, saat aralığından uzun olamaz. |
| **Metin sınırları** | Açıklama 1000, not 2000, iptal sebebi 500 karakter. | Aynı |
| **Çakışma** | — | Aynı temsilci, aynı gün, saatleri kesişen **aktif** (planlı / onaylı) iki ziyaret olamaz (`planned_visit_overlap`). |
| **Aynı gün tekrarı** | — | Aynı hedef, aynı gün, aynı ziyaret tipiyle ikinci aktif ziyaret olamaz (`planned_visit_duplicate_same_day_type`). |
| **Kod** | `PV-` + UUID. Çakışırsa 3 deneme. | Benzersiz, en fazla 64 karakter. |
| **Kaynak (temsilci)** | Yalnız `resources/me` | WP-VP-2 ile kaynak her zaman **oturumdaki kişi**. Başkası adına → 403 `resource_not_caller`. Yalnız `crm.planned-visit.read-all` sahibi başkası adına açabilir. |
| **Kaynak türü** | — | API yalnız `manual` kabul eder. `route-plan` yalnız haftalık planlama motorundan gelir. |

Saat ve süre hataları ile çakışma ve tekrar kuralları Android'de kendi mesajlarıyla gösteriliyor.

### 4.1 Durumlar ve geçişler

```
taslak ──(uç yok!)──► planlı ──onay──► onaylı
  │                    │                 │
  └──── iptal ◄────────┴─────────────────┘
arşiv: arşivlenmemiş her durumdan; geri alınamaz
```

| Durum | Android'de sunulan işlemler | Not |
|---|---|---|
| Taslak (`draft`) | İptal, Arşiv | **Onay yok.** Backend onayı yalnız planlı ziyarette kabul ediyor (§9.1). Android 2026-10-08'den beri taslak oluşturmuyor; taslaklar yalnız başka yollardan gelir. |
| Planlı (`planned`) | Onay, İptal, Arşiv | Haftalık planlama motorunun ürettiği ziyaretler bu durumda gelir. |
| Onaylı (`confirmed`) | İptal, Arşiv | Planlıya geri dönülemez. |
| İptal (`cancelled`) | Arşiv | — |
| Arşiv (`archived`) | Hiçbiri, salt okunur | Listede varsayılan olarak gizli, takvimde hiç yok. |

**Düzenleme:**

- Backend arşiv dışındaki **her durumda** (iptal ve onaylı dahil) düzenlemeyi kabul ediyor.
- Android de yalnız arşivde formu kapatıyor.

**Onay:**

- Ayrı bir yetkiyle yapılıyor (`crm.planned-visit.confirm`); planı yazan ile onaylayan farklı kişiler olabilir.
- Onay anında **izin (consent) kontrolü** yeniden yapılıyor. Reddedilirse (`plan_blocked_by_consent`, `plan_consent_unknown`, `consent_filter_not_applied`) ziyaret planlı kalıyor; Android "Yeniden yükle" sunuyor.

**Eşzamanlılık:**

- Her yazma isteği okunan sürümle (`expectedVersion`) gidiyor.
- Başkası değiştirmişse 409 dönüyor ve Android "Yeniden yükle" diyor.

**Haftanın yeniden açılması:** Haftalık planlamada bir hafta yeniden açılırsa backend o haftanın ziyaretlerini kendiliğinden iptal ediyor; raporu olanlar hariç.

---

## 5. Hangi bilgiler tutuluyor

### 5.1 Planlı ziyaret (backend kaydı ve Android'deki karşılığı)

| Bilgi | Backend'de | Android gönderiyor mu | Android gösteriyor mu |
|---|---|---|---|
| Kod, hedef türü ve kimliği | var | Oluşturmada (kod yalnız oluşturmada) | Kod (arşiv onayında); hedef adıyla |
| İşyeri / kişi / bağlantı kimlikleri | sunucu türetir | hayır | hayır (ad okumak için kullanılır) |
| Tarih, başlangıç ve bitiş saati | var | evet | evet |
| Süre (dk) | var | düzenlemede aynen geri | hayır |
| Kaynak (temsilci) ve görünen adı | var | evet (`resources/me`'den) | hayır |
| Ziyaret tipi, amaç | var | evet | evet (ham kod) |
| Açıklama, not | var | evet | evet |
| Pozisyon, iş birimi, bölge | var | düzenlemede aynen geri | hayır |
| Kampanya, strateji (oyun), segment | sunucu türetir (WP-VP-2) | oluşturmada hayır, düzenlemede aynen geri | hayır (ürün kararı K-3 / K-4) |
| İçerik yolculuğu / aşama, içerik kaynağı | var | düzenlemede aynen geri | hayır |
| Ürün listesi (`contentItems`: ürün, rol, yolculuk, aşama, adımlar) | var (SB-3b, branch) | hayır | **hayır** |
| Durum (taslak / planlı / onaylı / iptal / arşiv), iptal sebebi, arşiv zamanı / kişisi | var | durum işlemleri | durum ve arşiv bilgisi |
| Sıklık, izin ve uygunluk bilgisi | sunucu hesaplar | hayır | hayır |
| Sürüm, oluşturma / güncelleme zamanı ve kişisi | var | sürüm (`expectedVersion`) | hayır |
| Görünen adlar ve `targetInactive` | var (WP-VP-2, branch) | hayır | evet |

### 5.2 Takvim öğesi

| Bilgi | Android gösteriyor mu |
|---|---|
| Ziyaret, tarih, saat, hedef, kaynak, durum | evet |
| **Rapor durumu** (`none` / `draft` / `submitted` / `amended`) | evet |
| Ziyaret sonucu, planlanan / gerçekleşen aşama, `matchedPlan` | hayır |
| **`plannedContent`** (bu ziyarette ne sunulacak: ürün, yolculuk, aşama, adımlar; WP-E2E-FIX-1, branch) | hayır (Android henüz okumuyor) |

### 5.3 Ziyaret raporu (backend kaydı)

| Bilgi | Not |
|---|---|
| Sonuç (`executionOutcome`) | `completed` (yapıldı), `missed` (yapılamadı), `rescheduled` (ertelendi) |
| Rapor durumu (`reportStatus`) | `draft` → `submitted` → `amended`; geri dönüş yok |
| Sebep kodu | missed / rescheduled için zorunlu: `doctor_unavailable`, `clinic_closed`, `rep_unavailable`, `rescheduled_by_doctor`, `rescheduled_by_rep`, `other` |
| Erteleme tarihi ve notu | Yalnız kayıt. Yeni ziyaret açmıyor, planı değiştirmiyor; tarihin gelecekte olması bile zorunlu değil. |
| Sunulan içerik (`contentActuals`) | yolculuk, aşama, aşama sırası / kodu, plana uydu mu (`matchedPlan`) |
| Numuneler | en fazla 100 kalem; tür, kimlik, adet (1–100000), not |
| Geri bildirim | doktor geri bildirimi (≤4000), sonuç kodu (yapıldı'da zorunlu, serbest metin), takip gerekli mi, takip notu (≤2000) |
| Raporlayan kaynak, yapılma zamanı, gönderim / düzeltme zamanı | var |
| Düzeltmeler (`amendments`) | her biri: zaman, kim, sebep, değişen alanlar |
| **Tutulmayanlar** | konum / GPS, gerçek süre, genel not, imza |

---

## 6. Ziyaret raporu

### 6.1 Android'de bugün

**Rapor yalnız okunuyor.** Ziyaret düzenleme ekranında gösterilenler:

- rapor durumu,
- sonuç,
- sonuç kodu,
- yapılma ve gönderim zamanı,
- "Takip gerekli",
- düzeltme sayısı.

Rapor yoksa "Bu ziyaret için henüz rapor yok." yazıyor. Takvim satırında rapor durumu görünüyor.

**Android'de olmayanlar:**

- sonuç girme (yapıldı / yapılamadı / ertelendi) ve sebep kodu seçme,
- rapor yazma (sunulan içerik, numune, geri bildirim),
- gönderme,
- 60 dakika içinde değiştirme,
- düzeltme (amend),
- raporun planlanan içeriği (`plannedContent`) ile eşleştirilmesi.

### 6.2 Neden yapılmadı

- **Yetki (F-RBAC):**
  - Rapor yetkileri (`crm.visit-report.read`, `.record`, `.amend`) backend'de tanımlı ama **hiçbir role verilmemiş**.
  - Rapor uçları hâlâ "yalnız geliştirme" yedek yetkisiyle korunuyor: okumada `crm.territory.read`, yazmada `crm.territory.model.manage`.
  - Saha temsilcisi rolü yok.
  - Bu hâliyle yazılan mobil ekran, gerçek kullanıcıda 403 alır.
- **Sözleşme:**
  - Sonuç kodu (`outcomeCode`) bugün serbest metin; referans seti yok (backend backlog E9-B4b).
  - Rapor akışını değiştirecek "ziyaret başlat / tamamla" işi (SB-3c) backend'de **ertelendi** (Faz 6 sonrası).

### 6.3 Rapor uçları (backend)

| Uç | Ne yapar |
|---|---|
| `POST api/crm/visit-report/outcome` | Sonucu kaydeder; **taslak** raporu açar ya da günceller. missed / rescheduled burada biter. |
| `POST api/crm/visit-report` | Yapılan ziyaretin raporunu **gönderir** (içerik, numune, geri bildirim); sonuç = yapıldı, durum = gönderildi. |
| `POST api/crm/visit-report/{id}/amend` | Gönderilmiş raporu **gerekçeyle düzeltir** (sebep zorunlu, ≤500); durum = düzeltildi. |
| `GET api/crm/visit-report`, `GET …/{id}`, `GET …/calendar`, `GET …/contract` | Okuma |

Silme yok.

### 6.4 Rapor ne zaman girilebiliyor?

| Soru | Cevap (backend, `test/crm-content-visit-e2e`) |
|---|---|
| **En erken** | Ziyaret günü. `plannedDate` bugünden (UTC) sonraysa "yapıldı", "yapılamadı" ve gönderim 409 `visit_not_yet_due` ile reddediliyor. **"Ertelendi" her zaman girilebiliyor.** |
| **En geç** | **Sınır yok.** İlk sonuç ya da rapor için son tarih, ek süre veya kilit bulunmadı; geçmişteki her ziyaret raporlanabiliyor. |
| **Gönderilmiş raporu değiştirme** | Gönderimden sonra **60 dakika** içinde yeniden göndererek değiştirilebiliyor (`EditWindowMinutes = 60`). Sonrasında 409 `visit_report_edit_window_closed`; değişiklik ancak düzeltmeyle yapılabiliyor. |
| **Düzeltme** | **Süre sınırı yok.** Ziyaretin sahibi (ya da read-all sahibi) her zaman düzeltebiliyor; her düzeltme sebebiyle kayda geçiyor. |
| **Gönderimden sonra sonuç değiştirme** | Yasak (409 `visit_report_invalid_transition`). |
| **Planın durumu** | Kontrol edilmiyor. İptal edilmiş ya da onaylanmamış ziyarete de rapor girilebiliyor; arşivlenmiş ziyaret yalnız takvimde gizli. |
| **Rapor planı değiştirir mi?** | Hayır. Rapor planın durumunu değiştirmiyor; "ertelendi" yeni ziyaret açmıyor. |
| **"Bugün" nedir** | UTC takvim günü. Türkiye'de 00:00–03:00 arası "dün" sayılır. |

Bu kurallar `main`'de henüz yok. `main`'de `visit_not_yet_due` kontrolü bulunmuyor; WP-E2E-FIX-1 branch'te.

---

## 7. Yetkiler

| Yetki | Ne için | Durum |
|---|---|---|
| `crm.planned-visit.read` / `.manage` / `.confirm` | ziyaret okuma / yazma-iptal-arşiv / onay | Tanımlı ve uçlarda zorunlu. 97c5 kiracısında yalnız **Admin** rolüne verilmiş. |
| `crm.planned-visit.read-all` | başkasının ziyaretlerini görmek / yönetmek (yönetici) | Yalnız elle verilir; verme betiği henüz yok. |
| `crm.visit-report.read` / `.record` / `.amend` | rapor | Tanımlı, **verilmemiş**. Uçlar bunları değil yedek `crm.territory.*` yetkisini kullanıyor (F-RBAC açık). |
| `crm.visit-plan.read` | temsilcinin bölge işyerleri (`my-accounts`) | Saha temsilcisine verilip verilmeyeceği belirsiz. |

Android'de **istemci tarafı yetki kontrolü yok**; bu bilinçli bir karar. Ekranlar backend'in 403 cevabını "Bu işlem için yetkiniz yok" olarak gösteriyor.

---

## 8. Testler

| Grup | Test sayısı |
|---|---|
| MyPossibility birim testleri (ziyaret) | 339 |
| MyPossibility cihaz testleri (ziyaret ekranları) | 79 |
| Uygulama içi uçtan uca ziyaret testleri (`ErpVisit*`) | 11 |
| Tüm proje | birim 2268/0, :app cihaz 116/0, MyPossibility cihaz 81/0 (2026-10-07) |

Testlerin hepsi **sahte sunucuya** karşı. Gerçek backend'e karşı uçtan uca ziyaret denemesi henüz yapılmadı.

---

## 9. Bulgular ve riskler

1. **Mobilde açılan ziyaret onaylanamıyordu — Android'de düzeltildi (2026-10-08, seçenek (a)).**
   - Android oluştururken durum göndermiyor, ziyaret **taslak** doğuyor.
   - Backend'de taslak → planlı geçişi yapan bir uç yok. Güncelleme durum almıyor, onay yalnız planlıda çalışıyor.
   - Ayrıca çakışma ve aynı gün kuralları yalnız aktif (planlı / onaylı) ziyaretlerde işliyor; taslaklar bu kurallara takılmıyor.
   - **Seçenekler:**
     - (a) Android oluştururken `planStatus = planned` gönderir. Backend buna izin veriyor; en küçük değişiklik.
     - (b) Backend taslak → planlı için bir uç ekler.
     - (c) Taslak bilinçli olarak "onaysız" kalır.
   - **Öneri:** (a). Ürün kararıyla birlikte yapılmalı.
2. **"Bugün" UTC.** Türkiye'de gece 00:00–03:00 arasında hem ziyaret açma hem rapor girme kuralları bir önceki günü baz alıyor. Backend'in kiracı saat dilimi yok; karar backend'de.
3. **Rapor için son tarih yok.** İstenen bir iş kuralıysa (örneğin "ziyaretten sonra en geç X gün") backend'de tanımlanmalı. Bugün eski bir ziyaret aylar sonra raporlanabilir.
4. **İptal edilmiş ziyarete rapor girilebiliyor.** Backend planın durumuna bakmıyor. İstenmiyorsa backend kuralı gerekiyor.
5. **Raporlayan kaynak doğrulanmıyor.** `reportedByResourceId` oturumdaki kişiyle karşılaştırılmıyor; sahiplik yalnız planın kaynağı üzerinden kontrol ediliyor.
6. **Takvimdeki doktor ziyaretlerinde ad (T-1).** "Çalışma yerindeki doktor" hedefinde sunucunun `targetDisplayName` alanı boş geliyor. Android bu satırlarda adı kendi ara çözümüyle gösteriyor. Rota planından gelen ziyaretler "kişi" türünde olduğu için etkilenmiyor.
7. **Ham kodlar.** Ziyaret tipi, amaç ve durum etiketsiz geliyor ("field-visit", "medical-visit", "planned"); backend etiket yayınlamıyor. Android kodu alan adıyla birlikte gösteriyor.
8. **Takvim hata kodu.** Takvimde eksik tarih 400 `visit_report_reschedule_date_invalid` ile dönüyor; ad yanıltıcı (backend'e not).

---

## 10. Android'de yapılacaklar

| # | İş | Bağımlılık | Tahmini süre |
|---|---|---|---|
| A1 | ~~Oluştururken `planStatus = planned` göndermek~~ **Yapıldı (2026-10-08).** Ziyaret planlı doğuyor ve onaylanabiliyor; çakışma ve aynı-gün kuralları oluşturma anında işliyor. | — | tamam |
| A2 | **Ziyaret sonucu girme**: yapıldı / yapılamadı / ertelendi, sebep kodu, erteleme tarihi. Erken giriş reddi (`visit_not_yet_due`) için açık mesaj. | F-RBAC; sebep / sonuç kodu referans seti | 2–3 gün |
| A3 | **Rapor yazma ve gönderme**: sunulan içerik (planlanan içerikten seçim, `matchedPlan`), numuneler, doktor geri bildirimi, takip; 60 dakikalık değiştirme penceresi. | F-RBAC; mobil sözleşme notu (Faz 5); SB-3c'nin etkisi | 4–6 gün |
| A4 | **Rapor düzeltme** (gerekçeli) | F-RBAC (`.amend`) | 1–2 gün |
| A5 | Takvimde ve düzenleme ekranında **"ne sunacağım"** (`plannedContent` / `contentItems`) | WP-E2E-FIX-1 ve SB-3b'nin `main`'e alınması | 1–2 gün |
| A6 | Hedef seçiciyi **temsilcinin bölgesine** indirmek (`my-accounts`, "bölge dışı ekle" ayrı akış) | Faz 2b sözleşmesi; `crm.visit-plan.read` kararı | 2–3 gün |
| A7 | Ziyaret tipi / amaç / durum için Türkçe etiketler | Backend etiket yayınlarsa | 0.5–1 gün |
| A8 | "Sıklık yok" bilgi rozeti (isteğe bağlı) | — | 0.5 gün |
| A9 | Haftalık planlama (hedef seçip haftayı onaylama) | **Ürün kararı** (Ek1 §6 soru 5); planlama uçları | 2–3 hafta |
| A10 | Check-in / check-out | Anti-fraud backend sözleşmesi (B-AF-01…13) | 1–2 hafta |
| A11 | Çevrimdışı ziyaret ve rapor | Tasarım kararı; backend tekrar-kayıt koruması (idempotency) | 1–2 hafta |
| A12 | Gerçek backend'e karşı uçtan uca deneme | WP-VP-2'nin canlıya alınması; test kullanıcısı | 0.5 gün |

**Toplam:**

- A2–A5 (rapor tarafının tamamı): yaklaşık **9–14 iş günü** (tahmin). Backend maddeleri teslim edildikten sonra başlayabilir.
- A6–A8: yaklaşık **3–5 gün** (tahmin).

---

## 11. Backend'den beklenenler

Öncelik sırasıyla:

| # | Backend'in yapması gereken | Açtığı Android işi | Durum (2026-10-08) |
|---|---|---|---|
| B1 | **WP-VP-2 ve WP-E2E-FIX-1'in `main`'e alınıp canlıya dağıtılması** (adlar, sahiplik, sunucu türetmesi, `visit_not_yet_due`, `plannedContent`) | A5, A12; bugünkü ad ara çözümünün kalkması | Branch'te kabul edildi. Yol haritasında "main senkron + PR" sırada. |
| B2 | **Mobil sözleşme notu (Faz 5)**: Faz 2 / 2b alanları, ziyaret yürütme, K-7 ürünleri | A2, A3, A5, A6 | Henüz başlamadı. |
| B3 | **F-RBAC**: saha temsilcisi rolü; `crm.visit-report.read/record/amend` yetkilerinin rollere verilmesi; `crm.territory.*` yedeğinin rapor uçlarından kaldırılması | A2, A3, A4 | Açık. Rol kararı kullanıcıda. |
| B4 | **Taslak → planlı** için uç (Web ve diğer yollarla açılan taslaklar için; mobil artık planlı oluşturuyor) | — | Bildirilmeli |
| B5 | **Sonuç / sebep kodu referans seti** (`outcomeCode` serbest metin; E9-B4b) | A2, A3 | Backlog'da |
| B6 | **T-1**: "çalışma yerindeki doktor" hedefinde `targetDisplayName` (ya da takvime işyeri / kişi adı) | Ara çözümün kalkması | Bildirildi |
| B7 | **Faz 2b**: `my-accounts` sözleşmesi ve saha temsilcisinin `crm.visit-plan.read` yetkisi | A6 | Faz 2b uçları branch'te |
| B8 | **İş kuralı kararları**: kiracı saat dilimi ("bugün"), rapor son tarihi, iptal edilmiş ziyarete rapor, raporlayanın doğrulanması (§9.2–9.5) | Kurallar netleşir | Karar bekliyor |
| B9 | **SB-3c** (ziyaret başlat / tamamla, sunulan içerik, yolculuk ilerlemesi) | A3'ün son hâli | Ertelendi (Faz 6 sonrası) |
| B10 | **Check-in / anti-fraud** (MOD-0280 burada zaman çizelgesi; saha check-in'i ayrı modül olarak tasarlanmalı) | A10 | Yok |
| B11 | Ziyaret tipi / amaç / durum etiketleri (`{code, label}`) | A7 | İsteğe bağlı |

---

## 12. Kaynaklar

- **Android:** `MyPossibility/src/main/java/com/diten/mypossibility/`. Başlıca dosyalar:
  - `data/remote/VisitApi.kt`, `VisitReportApi.kt`
  - `domain/visit/VisitForm.kt`, `VisitLifecycle.kt`, `CreateVisitUseCase.kt`, `EditVisitUseCase.kt`, `VisitReports.kt`
  - `ui/visit/*`
- **Backend** (`services/Diten.CrmService/src`, branch `test/crm-content-visit-e2e`):
  - `Application/Features/PlannedVisit/*` (`PlannedVisitValidation.cs`, handler'lar)
  - `Application/Features/VisitReport/*` (`VisitReportLimits.EditWindowMinutes`, `ValidateDue`)
  - `Api/Controllers/CRM/PlannedVisitsController.cs`, `VisitReportController.cs`
  - `execution/domains/commercial-suite/work-packs/ROADMAP-visit-planning.md`
- **İlgili Android raporları:**
  - `docs/reports/backend-requirements/ANDROID-REPLY-VISIT-PLANNING-2026-10-06.md` (Ek1 yanıtı)
  - `docs/reports/android/evidence/DitenMultiply-Android-Ek1-Ziyaret-Planlama-Uyum-2026-10-07.pdf` (ekran görüntüleri)
  - `docs/reports/android/ANDROID-DURUM-VE-YOL-HARITASI-2026-10-05.md` (genel yol haritası)
