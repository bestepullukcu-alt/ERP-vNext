# Mobil ekiplere sözleşme notu — Ziyaret planlama (Faz 2 → Faz 4)

> **Kime:** iOS ve Android ekipleri · **Kimden:** backend / Control Tower · **Tarih:** 2026-10-08
> **Önceki notlar:** [2026-10-06 not](../2026-10-06-visit-planning/MOBILE-NOTE-2026-10-06-visit-planning.md) · [2026-10-06 yanıtlar](../2026-10-06-visit-planning/MOBILE-ANSWERS-2026-10-06.md) · [iş yeri listesi yanıtı](../2026-10-06-account-list/MOBILE-ANSWERS-2026-10-06-account-list.md) · [örnek yanıtlar](../2026-10-06-visit-planning/E2E-SAMPLE-RESPONSES-2026-10-07.md)
> **Kaynak:** CRM kodu (test dalı `test/crm-content-visit-e2e`, 2026-10-08) — alan adları DTO'lardan birebir. Web'de canlı denendi (kiracı 97c5).
> **Durum:** bu not, 06 Ekim notunun §5'indeki "taslak" alanların **kesin** halidir. Bu değişiklikler main'e PR ile gelecek (tarih ayrıca bildirilecek). Siz düzeltmelerinizi main üzerinden yaparsınız.

## 1. Kısa özet
- **Yanıt şekli değişmedi.** Yeni alanların hepsi **ek alan**; mevcut alan adları, zarf (`Response<T>`, veri `data` altında) ve `X-Tenant-Id` zorunluluğu aynı.
- **Ama davranış değişti.** §2'deki 9 madde uygulamanızı etkiler. Özellikle:
  - B1: oyun / kampanya / segment artık hiç gönderilmez;
  - B4: hafta hafta onay;
  - B5: `weekNumber` anlamı değişti;
  - B8: taslak haftalar planlanan ziyaret değildir.
- Sizden istenen: §7'deki uyum listesini uygulamalarınızla karşılaştırın, §9'daki sorulara yanıt verin.

## 2. Davranış değişiklikleri — önce bunları okuyun
| # | Değişiklik | Mobil için anlamı |
|---|---|---|
| **B1** | **Oyun / kampanya / segment sunucuda türetilir.** Planlanan ziyaret oluştur / güncelle ve plan (oturum) oluştur / güncelle isteklerinde istemcinin `strategyTemplateId`, `campaignId`, `segmentId` değeri **yok sayılır**. Sunucu doktorun aktif segmentlerinden oyunu, hedef listesinden kampanyayı türetir. Güncellemede gönderilmeseler de köken silinmez. | **06 Ekim yanıtı §1 ("düzenlerken okuduğunuz değeri aynen geri gönderin") artık geçersiz.** Bu üç alanı hiçbir istekte göndermeyin; ekranda göstermeyin. |
| **B2** | **Temsilci = oturumdaki kişi; sahiplik sunucuda.** Kaynak yalnız `GET /api/crm/resources/me`. | Başka bir kaynak için oluşturma `403`; başkasının planı / ziyareti `404`. Listeler yalnız kendi kayıtlarınızı döner. `read-all` yetkisi yalnız yöneticilere açıkça verilir. |
| **B3** | **Temsilci + dönem başına tek plan.** | İkinci planı oluşturmak `409 planning_session_exists` döner. Mevcut planı açın. |
| **B4** | **Onay hafta hafta.** `POST /api/crm/visit-plan/apply` gövdesinde `weekStart` (o haftanın Pazartesi'si, `yyyy-MM-dd`) verilir; yalnız o haftanın ziyaretleri planlanan ziyaret olarak yazılır. | `weekStart` olmadan "bütün dönemi onayla" eski yoldur (**kullanımdan kalkıyor**). Geçmiş hafta `409 week_in_past`, onaylı hafta `409 week_already_approved`. |
| **B5** | **`weekNumber` anlamı değişti.** Önizleme slotundaki `weekNumber` artık **dönemin hafta dizini**: önizlemedeki `weeks[]` listesinde sıra numarası. Ayrıca `weekStart` (Pazartesi) eklendi. | Haftayı göstermek / gruplamak için `weekStart` kullanın. Takvim haftası numarası gerekiyorsa `weeks[i].isoWeek`. |
| **B6** | **Haftayı yeniden açma (yeni uç).** `POST /api/crm/visit-plan/sessions/{id}/weeks/{weekStart}/reopen`, gövde `{ reason, expectedVersion }`; gerekçe ≥ 10 karakter. | Raporu olmayan ziyaretler **iptal edilir** (`planStatus = cancelled`, iptal nedeni `week_reopened`); raporlu olanlar kalır. Takvimde bu ziyaretleri iptal olarak gösterin ya da gizleyin. |
| **B7** | **Arşivlenmiş planlar listede varsayılan olarak gelmez.** | Gerekirse `GET …/sessions?includeArchived=true`. |
| **B8** | **Taslak hafta planlanan ziyaret değildir.** Bir hafta onaylanınca yalnız o haftanın ziyaretleri yazılır. Sonraki haftalar sıklığa göre **önizlemede** taslak olarak hesaplanır, ama onaylanana kadar planlanan ziyaret kaydı **yoktur**. | **06 Ekim notundaki K-2 / M5 satırını düzeltir:** sonraki haftalar kendiliğinden planlanan ziyaret olarak gelmez. Taslak haftaları göstermek için önizlemeyi okuyun (§4.3). |
| **B9** | **Gün ataması sunucuda.** Günlük süre bütçesi dönem kapasitesinden gelir. Hafta sonu ve tatile ziyaret düşmez; yarım gün yarım bütçedir. Kurumlar coğrafi olarak günlere dağıtılır. Sığmayan sonraki haftaya kayar ve kayma nedeni yazılır. | Mobil gün hesaplamaz. Temsilci bir ziyareti başka güne taşımak isterse **gün sabiti** gönderir (§4.5). |

## 3. Uçlar (`/api/crm/visit-plan/…`)
| Uç | Yöntem | Yetki | Not |
|---|---|---|---|
| `sessions` | GET | `crm.visit-plan.read` | `cyclePeriodId`, `status`, `includeArchived` (B7) |
| `sessions` | POST | `crm.visit-plan.generate` | B1, B2, B3 |
| `sessions/{id}` | GET | `crm.visit-plan.read` | §4.1 |
| `sessions/{id}` | PUT | `crm.visit-plan.generate` | hedefler, **ürün seçimi**, **gün sabitleri** (§4.5) |
| `sessions/{id}/targets` | GET | `crm.visit-plan.read` | 06 Ekim notu §7.3 (değişmedi) |
| `preview` | POST | `crm.visit-plan.generate` | `{ planningSessionId }` → §4.3 (hiçbir şey yazmaz) |
| `apply` | POST | `crm.visit-plan.apply` + `crm.planned-visit.manage` | `{ planningSessionId, weekStart, expectedVersion }` (B4) |
| `sessions/{id}/weeks/{weekStart}/reopen` | POST | `crm.visit-plan.apply` + `crm.planned-visit.manage` | B6 |
| `my-accounts` | GET | `crm.visit-plan.read` | bölge evreni; `search` (Türkçe duyarsız), `type`, `page`, `pageSize`, `hasActiveContacts` (2B yanıtındaki gibi) |
| `my-accounts/{accountId}/doctors` | GET | `crm.visit-plan.read` | 06 Ekim notu §7.2 (değişmedi) |
| `GET /api/crm/resources/me` | GET | — | §4.7 |

## 4. Alanlar
Hepsi ek alan. Tarihler `yyyy-MM-dd` metni, saatler `HH:mm` metni.

### 4.1 Plan (oturum) okuma — `GET sessions/{id}`
| Alan | Tür | Anlamı |
|---|---|---|
| `resourceDisplayName` | string? | temsilcinin adı soyadı (e-posta yalnız son çare) |
| `selectedAccounts[]`, `selectedPharmacies[]` | `{ id, displayName }` | seçili kurum / eczane adları (kimlik dizileri aynen duruyor) |
| `selectedContacts[].contactDisplayName`, `.accountDisplayName` | string? | doktor ve kurum adı |
| `selectedContacts[].products[]` | `{ productId, productCode, productName, role }` | temsilcinin doktor için seçtiği ürünler. `role`: `promo` (tanıtım) · `non-promo` (hatırlatma) |
| `weeks[]` | §4.4 | dönemin bütün haftaları |
| `currentWeekStart` | string? | bugünün haftası (bugün dönem içindeyse) |
| `nextDraftWeekStart` | string? | bugünden sonraki ilk onaysız, geçmemiş hafta |
| `visitModel` | `{ maxPromo, maxNonPromo, promoMinutes, nonPromoMinutes, reportMinutes, source }` | ziyaret başına ürün sınırları ve dakikalar. `source`: `cycle_capacity` · `none` (kapasite yoksa hepsi null) |

**Liste öğesi (`GET sessions`):** `doctorCount`, `pharmacyCount`, `approvedWeekCount`, `draftWeekCount`, `isEmpty` (hedefi olmayan taslak).

### 4.2 Planlanan ziyaret (`/api/crm/planned-visits`, liste ve ayrıntı)
- `targetDisplayName`, `accountDisplayName`, `contactDisplayName` (string?) ve `targetInactive` (bool): **GUID göstermeyin** (D1 kapandı).
- `contentItems[]`: onayda dondurulan ürün listesi.
  - Alanlar: `productId`, `productCode`, **`productName`**, `role`, `source`, `order`, `journeyId` / `journeyCode`, `stageId` / `stageIndex` / `stageCode` / `stageName`, `pathId` / `pathCode` / `pathVersion`, `steps[]`, `claims[]`, `warnings[]`.
  - `source`: `play` (önerilen) · `rep-pick` (temsilcinin seçimi) · `last-visit` · `portfolio`.
  - `order`: listedeki 1 tabanlı sıra.
  - Eski kayıtlarda boş liste. Tekil `content` alanı ilk tanıtım ürününden dolmaya devam ediyor.
- `productName` onay anında kaydedilir. Eski kayıtlarda okuma anında MDM'den doldurulur. **Gelmezse kodu gösterin** (§8).

### 4.3 Önizleme — `POST preview`
**Yanıt düzeyi:**
| Alan | Anlamı |
|---|---|
| `weeks[]` | dönemin bütün haftaları, durumlarıyla (§4.4) |
| `calendarStatus` | `{ status: resolved \| unresolved, reasonCode, reason }` — çalışma takvimi cevap verdi mi |
| `nonWorkingDates[]`, `halfDayDates[]` | hafta sonu + tatil; yarım günler |
| `days[]` | taslak haftaların çalışma günleri: `{ date, weekStart, kind, budgetMinutes, plannedMinutes, idleMinutes, overCapacity }`. `kind`: `working` · `half` · `holiday` · `weekend` |
| `shifted[]` | sonraki haftaya kayanlar: `{ targetType, targetId, contactId, displayName, fromWeek, toWeek, reason }` (§5) |
| `weekCapacity[]` | hafta başına `{ weekStart, workingDays, halfDays, holidays, capacityMinutes, plannedMinutes, visitCount, dailyCap, productVisitCounts[] }` |
| `periodCapacity` | `{ capacityMinutes, plannedMinutes, dailyBudgetMinutes, dailyCap, halfDayCap, budgetSource }` |
| `productDistribution[]` | `{ productId, productCode, productName, doctorCount }` |
| `doctorsWithoutProducts` | ürünsüz doktor sayısı |
| `portfolioStatus` | şimdilik hep `undefined` |
| `pinOverflow[]` | gününe sığmayan sabit: `{ targetType, targetId, contactId, displayName, fromDate, toDate, reason }` |
| `pinWarnings[]` | uygulanamayan sabit: `{ weekStart, targetType, targetId, date, code }` |
| `visitModel` | §4.1 |

**Slot (`scheduled[]`), yeni alanlar:**
| Alan | Anlamı |
|---|---|
| `weekStart` | slotun haftası (Pazartesi). Gruplamayı bununla yapın (B5) |
| `isFixed` | onaylanmış ve yazılmış ziyaret; yeniden hesaplanmaz |
| `frequencyStatus`, `requiredVisitCount` | `resolved` · `conflict` · `unknown`. `unknown` hata değildir: "dönemde 1 (varsayılan)" sayılır |
| `contentItems[]` | bu ziyaretin ürünleri: §4.2 alanları, `productName` dahil |
| `overflowProducts[]` | rol sınırı yüzünden bu ziyarete girmeyenler: `{ productId, productCode, productName, role, reason: max_promo \| max_non_promo }`. Sonraki ziyarette başa geçer |
| `productWarnings[]` | `no_products` · `no_approved_content` · `ambiguous_journey` … |
| `isPinned`, `autoPinned` | temsilcinin sabitlediği gün / sabit gününe sığmadığı için otomatik ertesi güne |
| `groupKey` | kurum grubu (bir kurumun doktorları + bağlı eczaneleri); "kurumu taşı" için |
| `reportStatus` | `none` · `reported` · `cancelled` — "yapıldı" rozeti |

**Doktor içerik özeti (`content[]`):** `products[]` (`{ productId, productCode, productName, role, source }`) ve `durationMinutes` (ziyaret süresi, ürün listesinden).

### 4.4 Hafta (`weeks[]`)
| Alan | Anlamı |
|---|---|
| `weekStart`, `isoWeek`, `from`, `to` | Pazartesi, takvim haftası, dönem içi ilk / son gün |
| `status` | **türetilir:** `past` (geçmiş) · `approved` (onaylı) · `draft` (taslak) · `empty` (boş) |
| `visitCount` | onaylı haftada yazılan ziyaret sayısı; önizlemede hesaplanan |
| `storedStatus` | `approved` · `reopened` · `legacy` (eski tek seferlik planlar, sabit hafta) · null |
| `approvedAt`, `approvedBy`, `history[]` | onay / yeniden açma geçmişi: `{ at, by, action, reason }` |
| `dayPins[]` | o haftanın gün sabitleri (§4.5) |
| `reportedVisitCount` | önizlemede: raporlu ziyaret sayısı |

### 4.5 İstekler
**Ürün seçimi** (`PUT sessions/{id}`): `selectedContacts[]` öğesinde `products`.
- **Gönderilmez / null:** mevcut seçim korunur.
- **`[]`:** seçim temizlenir.
- **Liste:** seçim liste ile değiştirilir.
- Ürün öğesi: `{ productId, productCode, role }`. `role` null ise `promo`. Ad gönderilmez, sunucu kendisi okur.
```json
{ "selectedContacts": [ { "contactId": "c71d…", "accountId": "4f0c…", "accountContactLinkId": "1b2e…",
                          "products": [ { "productId": "44e5…", "productCode": "GP-000000000001", "role": "promo" } ] } ],
  "expectedVersion": 17 }
```
`selectedContacts` gönderilmezse doktor listesi değişmez. Boş liste gönderilirse **hepsi temizlenir**; dikkat.

**Gün sabiti** (`PUT sessions/{id}`): `dayPins` yalnız **bir taslak hafta** için, o haftanın sabitlerinin **tamamını** taşır.
- **Gönderilmez:** sabitler korunur.
- **`pins: []`:** o haftanın sabitleri temizlenir.
- Öğe: `{ targetType, targetId, contactId, date, scope }`. `targetType` / `targetId` / `contactId` önizleme slotundan alınır.
- `scope`:
  - `visit`: yalnız bu ziyaret;
  - `institution`: kurumun o haftadaki bütün doktorları + bağlı eczaneleri.
- Aynı ziyarette hem `visit` hem `institution` sabiti varsa `visit` kazanır.
- Taşınmak istenen gün geçmişse ya da çalışma günü değilse sabit uygulanmaz → `pinWarnings`.
- **Sabitlenmeyen ziyaretler yerinde kalır.** Yalnız gün bütçesi ya da yol yetmezse yer değiştirirler.
```json
{ "dayPins": { "weekStart": "2026-10-05",
               "pins": [ { "targetType": "contact", "targetId": "3c08…", "contactId": "3c08…", "date": "2026-10-09", "scope": "visit" } ] },
  "expectedVersion": 18 }
```

**Onay:** `POST apply` `{ planningSessionId, weekStart, expectedVersion }` → yanıt `{ planningSessionId, status, committedPlannedVisitIds, scheduledCount, unscheduledCount, weekStart, weekStatus }`.

**Yeniden açma:** `POST sessions/{id}/weeks/{weekStart}/reopen` `{ reason, expectedVersion }` → yanıt `{ planningSessionId, weekStart, status, cancelledPlannedVisitIds, keptPlannedVisitIds }`.

### 4.6 İş yeri listesi
- 2B yanıtında verilen alanlar canlıda (`activeContactCount`, `hasActiveContacts` süzgeci, filtre seçenekleri ucu). Değişiklik yok.
- `my-accounts` yanıtında `territoryStatus`: `assigned` · `unassigned`. Bölgesi atanmamış temsilci bütün hesapları görür ve bir bant gösterilir.

### 4.7 `resources/me`
- `displayName`: ad soyadı.
- **`countryCode`** (yeni): temsilcinin bölgesinin ülkesi, küçük harf (`"tr"`). Bölge yoksa null.
- `items[]` hâlâ tek öğe.

## 5. Sözlükler
| Alan | Değerler |
|---|---|
| `shifted[].reason` | `capacity_full` (haftada gerçekten yer yok) · **`no_near_day`** (yer var ama yakın gün yok; ör. başka şehirdeki kurum) · `holiday` · `half_day` · `pin_overflow` |
| `pinOverflow[].reason` | `pin_day_full` · `pin_outside_availability` · `pin_overflow` |
| `pinWarnings[].code` | `pin_target_not_in_week` · `pin_not_working_day` |
| Doktor uyarıları | `consent_blocked` (izin yok → **planlanmaz**) · `consent_unknown` (planlanır, uyarı) |
| `role` | `promo` (tanıtım) · `non-promo` (hatırlatma) |
| `source` (ürün) | `play` · `rep-pick` · `last-visit` · `portfolio` |
| `reportStatus` | `none` · `reported` · `cancelled` |
| `periodCapacity.budgetSource` | `cycle_capacity` · `default_hours` |
| `calendarStatus.status` | `resolved` · `unresolved` (hafta sonu yedeği kullanıldı) |

Ekranda bu kodları değil yerel etiketleri gösterin. Web'deki Türkçe karşılıklar: "hafta dolu", "yakın gün yok", "tatil", "yarım gün", "sabit gününe sığmadı".

## 6. Hata kodları (zarf `errors: [mesaj, kod]`; kod ikinci sırada)
| Kod | HTTP | Ne zaman |
|---|---|---|
| `planning_session_exists` | 409 | temsilci + dönem için plan zaten var (B3) |
| `invalid_week` | 400 | `weekStart` Pazartesi değil ya da dönem dışında |
| `week_in_past` | 409 | geçmiş hafta onaylanamaz / yeniden açılamaz |
| `week_already_approved` | 409 | hafta zaten onaylı |
| `week_not_approved` | 409 | onaylı olmayan hafta yeniden açılamaz |
| `reopen_reason_required` | 400 | gerekçe < 10 karakter |
| `invalid_day_pin` | 400 | sabit öğesi bozuk (kapsam, tarih, hedef) |
| `invalid_product_role` | 400 | rol `promo` / `non-promo` değil |
| `product_not_found` | 400 | ürün MDM'de yok |
| `too_many_products` | 400 | seçim sınırı aşıldı |
| `product_lookup_unavailable` | 503 | ürün doğrulaması için MDM'e ulaşılamadı (yalnız **yeni** ürün eklerken) |
| `planning_session_not_empty` / `planning_session_already_committed` | 409 | yalnız boş taslak arşivlenebilir / eski tek seferlik plan değiştirilemez |
| `invalid_quick` | 400 | `doctors?quick=` değeri `due` / `never` / `all` değil |
| `visit_not_yet_due` | 409 | ileri tarihli ziyaret kapatılamaz (06 Ekim notu §5) |
| sahiplik | 404 / 403 | başkasının kaydı 404; başka kaynak adına oluşturma 403 (B2) |
| sürüm | 409 | `expectedVersion` eskiyse; yeniden okuyup tekrar deneyin |

## 7. Mobil uyum listesi — kontrol edin, eksikse düzeltin
| # | Kontrol | Beklenen |
|---|---|---|
| M1 | Oyun / kampanya / segment | Hiçbir istekte gönderilmez, hiçbir ekranda görünmez (**B1**; 06 Ekim yanıtı §1 geçersiz). |
| M2 | Temsilci | Yalnız `resources/me`. Başka kaynak adına istek yok (B2). |
| M3 | Görünen ad | GUID gösterilmez: §4.1 / §4.2 adları. Ad için ara istekler kaldırılabilir. |
| M4 | Hafta | Gruplama `weekStart` ile; `weekNumber` dizindir (B5). Durum rozeti `weeks[].status`. |
| M5 | Taslak haftalar | Planlanan ziyaret listesinde değil, önizlemede (B8). Mobil planlama yapmıyorsa yalnız onaylı haftalar görünür; bu doğru. |
| M6 | Onay / yeniden açma | Mobil planlama yapacaksa: `apply` + `weekStart`; yeniden açma ucu ve gerekçe (B4, B6). |
| M7 | İptal edilen ziyaret | `week_reopened` nedenli iptaller takvimde iptal görünür ya da gizlenir; raporlanabilir değildir. |
| M8 | Ürün adı | `productName ?? productCode`. |
| M9 | Ürün seçimi | `products` null / [] / liste kuralı (§4.5). Ad gönderilmez. |
| M10 | Sıklık | `unknown` → "dönemde 1 (varsayılan)". |
| M11 | Kayma nedenleri | §5 sözlüğü yerel etiketle; `no_near_day` yeni. |
| M12 | Arşivli planlar | Varsayılan listede yok (B7). |
| M13 | RTL | Arapçada rakamla başlayan kurum adları ("018 KLİNİK") ters dönmesin. Ad metinleri yön yalıtımıyla gösterilmeli (Android `BidiFormatter`, iOS `⁨…⁩`). |

## 8. Bilinen sınırlar
- **Ürün adı izni:** adlar MDM'den okunur ve bunun için kullanıcının rolünde `mdm.global-products.read` olmalı. Saha temsilcisi rolüne bu izin ürün sahibi tarafından eklenecek. Eklenene kadar ya da MDM'e ulaşılamazsa `productName` null gelir; yanıt yine 200 döner.
- **`countryCode`:** bölgesi olmayan temsilcide null (tüzel kişi kaynağı henüz yok).
- **Kurum türü ve uzmanlık etiketleri:** referans verisinde dil başına etiket niteliklerle (`attributes.label_tr` …) ekleniyor. Siz `consumable-sets` uç noktasından okuyorsanız, arayüz dilindeki `attributes.label_<dil>`'i, yoksa `label`'ı gösterin.
- **İl adı:** `cityRef` ("TR-34-ISTANBUL") bir koddur. İl etiket seti henüz yok; şimdilik son parçayı Türkçe baş harf kuralıyla gösterin ("İstanbul").
- **Ziyaret yap / tamamla uçları (SB-3c):** henüz yok, ayrı not gelecek (SB-3-MOB).
- **Planlanan Ziyaretler sayfası işleri (Faz 6):** T1–T3, mobilden plan dışı ziyarette ürün listesi. Henüz yok.

## 9. Sizden yanıt beklenenler
1. Uygulamanızda B1'e aykırı bir istek kaldı mı? Bugün düzenlemede `strategyTemplateId` / `campaignId` / `segmentId` geri gönderiyorsanız kaldırmanız gerekiyor.
2. `weekNumber`'ı bugün nerede kullanıyorsunuz (B5)?
3. Mobilde haftalık planlama (hedef seçme, ürün seçme, gün sabitleme, haftayı onaylama) yapılacak mı, yoksa ilk aşamada yalnız görüntüleme mi? Yanıta göre §4.3–4.5 sizin için zorunlu ya da isteğe bağlı olur.
4. `week_reopened` ile iptal edilen ziyaretleri takvimde göstermek mi, gizlemek mi istersiniz?
5. Örnek yanıt isterseniz (önizleme, plan, planlanan ziyaret), test kiracısından gerçek bir yanıt çıkarıp ekleyebiliriz.
