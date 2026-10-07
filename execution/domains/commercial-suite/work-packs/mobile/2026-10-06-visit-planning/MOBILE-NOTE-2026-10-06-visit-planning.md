# Mobil ekiplere not — Ziyaret planlama kararları ve uyum listesi

> **Kime:** iOS ve Android ekipleri · **Kimden:** backend / Control Tower · **Tarih:** 2026-10-06
> **Kaynak:** Web'deki Ziyaret Planlama ve Planlanan Ziyaretler sayfalarının canlı incelemesi (97c5, yerel ortam) + ürün sahibinin bugünkü kararları.
> Ekran görüntüleri Web'den. Bazıları dar pencerede alındı, bu yüzden düzen mobile benziyor. Görüntülerde demo verisi (doktor adları) ve bir test kullanıcısının e-postası var; ekip dışına paylaşmayın.

## 1. Kısa özet
- Ürün sahibi **temsilcinin ekranı** için dört karar verdi (§2). Bu kararlar **mobil uygulamalar için de geçerli**.
- Bazı backend alanları **henüz yok** ama geliyor (§5). Gelene kadar mobilde yapılması / yapılmaması gerekenler §4'te.
- Sizden istenen: §4'teki listeyi uygulamalarınızla karşılaştırın, eksik ya da aykırı olanları düzeltin, §6'daki sorulara yanıt verin.

## 2. Kararlar (ürün sahibi, 2026-10-06)
| # | Karar | Mobil için anlamı |
|---|---|---|
| K-1 | Temsilci **kendi haftasını** planlar. İleride ayrı bir yönetici görünümü gelecek. | Temsilci seçimi yok: kaynak her zaman oturumdaki kişi (`resources/me`). |
| K-2 | Bir hafta planlanıp onaylanınca **sonraki haftalar doktorun / kurumun ziyaret sıklığına göre otomatik** oluşur. | Sonraki haftaların ziyaretleri backend'den **planlanmış ziyaret** olarak gelir (`source = route-plan`). Mobil bunları kendisi üretmez. |
| K-3 | Temsilci **strateji şablonu ("oyun") ve kampanya** görmez, seçmez. | Hiçbir ekranda bu kelimeler, alanlar ya da seçiciler olmaz. API'ye `strategyTemplateId` / `campaignId` gönderilmez; sunucu kendisi türetir. |
| K-4 | Temsilci **segment seçmez ve segmentle süzmez**. Sistem "**Bu hafta görülmesi gerekenler**" listesini önerir. Segment, doktor satırında yalnız **bilgi rozeti** olur. | iOS raporundaki "Segment — kapsam kararı yok" maddesinin cevabı bu: segment filtresi / kapsamı **yapılmayacak**. Yalnız rozet, alan geldiğinde. |

## 3. Web'de bugün ne var (ekran görüntüleri)
> Web ekranı yeniden tasarlanıyor (mockup aşamasında). Görüntüler bugünkü durumu ve bulunan sorunları göstermek için.

**3.1 Plan listesi** — temsilci e-postayla görünüyor; "Hedefler" sütunu hatalı (hep 0).
![Plan listesi](01-plan-listesi.jpg)

**3.2 Yeni plan formu** — "Temsilci", "Segmentler" ve "Strateji şablonu" alanları **kaldırılacak** (K-1, K-3, K-4).
![Yeni plan formu](02-yeni-plan-formu.png)

**3.3 Hedefler** — temsilci hastaneyi, sonra doktorları ve eczaneleri seçiyor. "Seçilenler" listesinde ve "Bağlantı" sütununda **ad yerine GUID** görünüyor. Bu, mobilin istediği **D1 (görünen ad)** konusunun aynısı.
![Hedefler](03-hedefler.png)

**3.4 Onaylanmış plan** — "Onaylandı" durumunda kaydet butonu hâlâ açık (hata, düzeltilecek). "Arz 6543" dönemin tamamı, "Talep 176" bu plan; birimler farklı.
![Plan özeti](04-plan-ozeti-onayli.jpg)

**3.5 Rota** — gün gün duraklar. Tasarım **değişmeyecek**; mobil takvim / rota görünümü için iyi bir referans.
![Rota](05-rota.jpg)

**3.6 Planlanan Ziyaretler** — hedef "Contact f3b8f9ce" gibi **GUID** ile görünüyor (D1).
![Planlanan ziyaretler](06-planlanan-ziyaretler.jpg)

## 4. Mobil uyum listesi — kontrol edin, eksikse düzeltin
| # | Kontrol | Beklenen |
|---|---|---|
| M1 | Strateji şablonu / oyun / kampanya | Hiçbir ekranda ad, alan ya da seçici yok. **Oluştururken** `strategyTemplateId`, `campaignId`, `segmentId` gönderilmez. **Düzenlerken** okunan değer **aynen geri gönderilir** (PUT tam değiştirmedir; göndermemek bağı siler) — bkz. [yanıtlar §1](MOBILE-ANSWERS-2026-10-06.md). Sunucu türetmeye geçince (§5) gönderim bırakılır. |
| M2 | Segment | Segment seçimi / filtresi / kapsamı **yok**. Backlog'daki segment işi kapanır. Rozet için alan gelince haber verilecek (§5). |
| M3 | Temsilci / kaynak | `resourceId` yalnız `GET /api/crm/resources/me`'den. Kullanıcı listesinden temsilci seçtiren ekran olmaz. |
| M4 | Hedef seçici (işyeri / kişi) | Bugün olduğu gibi çalışır. **Bölge ataması** uç noktası gelince (§5) yalnız temsilcinin bölgesindeki hedefler listelenir; bölge dışı ekleme uyarılı ayrı akış olur. |
| M5 | Haftalar | Mobil hafta / ziyaret **üretmez**. Sonraki haftalar backend'den planlanmış ziyaret olarak gelir. Mobilde elle oluşturma, plan dışı tekil ziyaret içindir (`source = manual`; T1–T3 sözleşmesi ayrıca gelecek). |
| M6 | Görünen ad (D1) | **GUID gösterilmez.** Alanlar gelene kadar: hedef türü etiketi + mevcut işyeri / kişi uçlarından ad (önbellekli), bulunamazsa "Ad yükleniyor / bilinmiyor". Alanlar gelince doğrudan onları kullanın (§5). |
| M7 | Sıklık | Planlanmış ziyarette `frequencyStatus` var: `resolved` / `unknown`. `unknown` hata değildir; "sıklık yok" bilgi rozeti gösterilebilir. Bugünkü demo verisinde çoğu `unknown`. |
| M8 | Süre | `plannedDurationMinutes` sunucudan geldiği gibi gösterilir; sabit süre varsayılmaz. Eski demo planlarında hepsi **3 dk** (eski hesap); yeni planlarda değişecek. |
| M9 | Hafta sonu / tatil | Takvim hafta sonu ziyaretini de **gösterebilmeli** (gizlemesin). Bugün backend bir Pazar ziyareti üretmiş durumda (`VP-0848afed-0176`, 2026-10-25); düzeltiliyor. |
| M10 | Onaylı hafta | Onaylanmış haftanın ziyaretleri planlama açısından salt okunur olacak. Ziyaret bazında onayla / iptal / rapor akışları aynı kalır. |
| M11 | Referans setleri | Yeni uç: `api/lookups/reference-data/consumable-sets/{setCode}/published-values` (main'de, PR #132). Eski `sets/{setCode}` kullanılmaz. Sunucuya dağıtıldığı ayrıca teyit edilecek. |
| M12 | Zarf / başlık | Yanıtlar `Response<T>` zarfında (`data.items`); `X-Tenant-Id` zorunlu. |

## 5. Backend'de gelecekler (alan adları **taslak**, kesinleşince sözleşme notu gönderilecek)
| İş | Mobil etkisi | Taslak alanlar |
|---|---|---|
| Sunucu türetmesi | strateji / kampanya / segment istemciden alınmaz, doktordan türetilir | istemciden gelen değer reddedilir ya da yok sayılır (karar backend'de) |
| D1 görünen adlar | liste / detay / takvimde ad | `targetDisplayName`, `accountDisplayName`, `contactDisplayName`, hedef pasifse işaret |
| B01 sahiplik | temsilci yalnız kendi ziyaretlerini görür / değiştirir (sunucu tarafında) | istemci değişikliği gerekmez; başkasının kaydı 404 / 403 |
| Sıklık uyumu | doktor başına dönem hedefi / yapılan / kalan, son ziyaret | `frequencyTarget`, `visitsDone`, `visitsRemaining`, `lastVisitDate` |
| "Bu hafta görülmesi gerekenler" | öneri listesi | yeni uç (temsilcinin bölgesi + sıklık + son ziyaret) |
| Bölge evreni | hedef seçicide yalnız kendi bölgesi | yeni uç / filtre (kaynak ↔ bölge modeli kararına bağlı) |
| Segment rozeti | bilgi rozeti | `segments: [{code, label}]` (salt okunur) |
| Takvim düzeltmesi | hafta sonu / tatile ziyaret düşmez, günler dengelenir | istemci değişikliği gerekmez |
| F-RBAC | saha temsilcisi rolü + ziyaret raporu yetkileri | yetki anahtarları değişmez (`crm.planned-visit.*`, `crm.visit-report.*`) |
| **Ziyaret yürütme (WP-E2E-FIX-1, KESİN)** | takvim öğesi "ne sunacağım"ı taşır; ileri tarihli ziyaret kapatılamaz; rapor plandaki yolculuk / aşama kimliğini taşımalı | **ek alan** `GET /api/crm/visit-report/calendar` öğesinde `plannedContent: [{ productId, productCode, role, journeyId, journeyCode, stageId, stageIndex, stageCode, stageName, steps: [{ title, type }] }]` (içerik yoksa `[]`; eski `plannedJourneyId / plannedStageId / plannedStageIndex` aynen) · **yeni hata** `409 visit_not_yet_due` — `POST /api/crm/visit-report/outcome` (`completed`, `missed`) ve `POST /api/crm/visit-report` (gönderim), `plannedDate` > bugün (UTC takvim günü) iken; `rescheduled` serbest · rapor gönderiminde `contentActuals.journeyId / stageId / stageIndex / stageCode / matchedPlan` doldurulmalı (alanlar zaten vardı; ilerleme okuması SB-3c'de bunlara dayanacak) · hata zarfı `errors: [mesaj, kod]` (kod ikinci sırada) |

## 6. Sizden yanıt beklenenler
1. Uygulamanızda bugün **strateji şablonu, kampanya ya da segment** görünen veya seçilen bir yer var mı? Varsa hangi ekran?
2. Planlanan ziyaret oluştur / düzenle isteğinizde `strategyTemplateId`, `campaignId` ya da `segmentId` gönderiyor musunuz?
3. Hedef adını bugün nasıl gösteriyorsunuz: GUID mi, ayrı istek mi?
4. Hafta sonu ve tatil günlerindeki ziyaretleri takvimde nasıl ele alıyorsunuz?
5. Haftalık planlamayı (hedef seçip haftayı onaylama) mobilde de istiyor musunuz, yoksa ilk aşamada yalnız görüntüleme + plan dışı tekil ziyaret yeterli mi?

## 7. Ek (2026-10-07, WP-VP-3D) — hedef durum okumaları **kesinleşti**
§5'teki "Sıklık uyumu", "Bu hafta görülmesi gerekenler" ve "Segment rozeti" satırlarının **kesin** hali. Üç uç da **yalnız okuma**, aynı zarf (`Response<T>`, veri `data` altında), `X-Tenant-Id` zorunlu. Taslak adlar **değişti**: `frequencyTarget` → `requiredVisitCount`, `visitsDone` → `done`, `visitsRemaining` → `remaining`; segment rozeti `segmentBadges: ["ad", …]` (düz ad listesi, en çok 5).

**7.1 Doktor durumu (`status`) — üç uçta da aynı nesne**
| Alan | Tür | Anlamı |
|---|---|---|
| `contactId` | guid | doktor |
| `requiredVisitCount` | int? | dönemde gereken ziyaret; sıklık çözülemezse `null` |
| `frequencyStatus` | string | `resolved` · `unknown` · `conflict` (`unknown` hata değildir) |
| `periodType` | string? | sıklık politikasının dönem türü (`month` …) |
| `done` | int | **temsilcinin** dönemdeki planlı ziyaretlerinden raporu `completed` olanlar (iptal / arşiv sayılmaz; `missed` yapılmış değildir) |
| `planned` | int | bugün → dönem sonu, iptal / arşiv dışı, **raporu olmayan** planlı ziyaretler |
| `remaining` | int? | `max(0, required − done − planned)`; sıklık yoksa `null` |
| `lastVisitDate` | datetime? | temsilcinin son `completed` raporunun `executedAt` değeri (tarih sınırı yok); hiç yoksa `null` |
| `neverVisited` | bool | hiç `completed` rapor yok |
| `dueThisWeek` | bool | "Bu hafta görülmeli": `remaining > 0` **ve** (dönemde henüz `completed` yok **ya da** son ziyaretin haftasından bu haftaya geçen tam hafta ≥ dönem haftası / gereken). Örnek: 4 haftalık dönem, gereken 2 → 1. haftada görüldüyse 3. haftadan itibaren tekrar vadeli. |
| `segmentBadges` | string[] | aktif segment adları — **yalnız bilgi**, süzgeç değildir (K-4) |
| `consentStatus` | string? | ziyaret kanalı izin sonucu (`allowed` · `blocked` · `unknown` · `not_applicable`) |
| `inactive` | bool | doktor kaydı pasif |

**7.2 `GET /api/crm/visit-plan/my-accounts/{accountId}/doctors`** — bir kurumun aktif doktorları + durum. Sorgu: `planningSessionId` (verilirse o planın dönemi; yoksa bugünkü dönem), `quick=due|never|all` (varsayılan `all`; başka değer `400 invalid_quick`), `search` (Türkçe duyarsız: "şirin" → "ŞİRİN"), `specialty`, `page`, `pageSize` (≤ 200). Kurum temsilcinin bölgesinde değilse **gizlenmez**, `outOfTerritory: true` döner. Yetki `crm.visit-plan.read`.
```json
{ "data": {
    "accountId": "4f0c…", "accountName": "MEMORIAL ŞİŞLİ HASTANESİ", "outOfTerritory": false,
    "period": { "cyclePeriodId": "9a1e…", "cycleCode": "2026-10", "startDate": "2026-10-01", "endDate": "2026-10-31", "weekCount": 5 },
    "items": [ { "contactId": "c71d…", "accountContactLinkId": "1b2e…", "displayName": "SADAKAT ÖZDİL",
                 "specialty": "cardiology", "professionalTitle": "Uzm. Dr.", "isPrimary": true,
                 "status": { "contactId": "c71d…", "requiredVisitCount": 2, "frequencyStatus": "resolved", "periodType": "month",
                             "done": 1, "planned": 0, "remaining": 1, "lastVisitDate": "2026-10-05T08:12:00+00:00",
                             "neverVisited": false, "dueThisWeek": false, "segmentBadges": ["Kardiyoloji A"],
                             "consentStatus": "allowed", "inactive": false } } ],
    "totalCount": 1, "page": 1, "pageSize": 50 },
  "statusCode": 200, "isSuccessful": true }
```

**7.3 `GET /api/crm/visit-plan/sessions/{id}/targets`** — planın seçili kurum / eczane / doktorları **tek istekte**: kurum ve eczane için `accountId, found, accountName, accountCode, accountType, cityRef, districtRef, addressLine, latitude, longitude, inactive`; doktor için `contactId, accountId, accountContactLinkId, found, displayName, specialty, status` (7.1). Başka temsilcinin planı `404`. `found: false` = kayıt silinmiş; ad uydurulmaz.
```json
{ "data": { "planningSessionId": "a42373cb…", "resourceId": "…", "period": { "cycleCode": "2026-10", "weekCount": 5, "…": "…" },
    "accounts":   [ { "accountId": "4f0c…", "found": true, "accountName": "MEMORIAL ŞİŞLİ HASTANESİ", "accountType": "hospital",
                      "cityRef": "TR-34-ISTANBUL", "districtRef": "TR-34-SISLI", "addressLine": "…", "latitude": 41.06, "longitude": 28.98, "inactive": false } ],
    "pharmacies": [ { "accountId": "77aa…", "found": true, "accountName": "ŞİFA ECZANESİ", "accountType": "pharmacy", "…": "…" } ],
    "doctors":    [ { "contactId": "c71d…", "accountId": "4f0c…", "found": true, "displayName": "SADAKAT ÖZDİL", "status": { "done": 1, "…": "…" } } ] } }
```

**7.4 `GET /api/crm/accounts/related?accountIds=a,b,c&relationType=pharmacy`** — `/accounts/{id}/related-accounts`'ın toplu hali; satır biçimi aynı (`relatedAccountId, relatedAccountName, relatedAccountType, relationshipType, effectiveLabelCode …`), kurum başına grup. `relationType` ilişki türüyle **ya da** ilişkili hesabın türüyle eşleşir. En çok **100** kimlik; fazlası `400 too_many_ids`, boş `400 account_ids_required`, bozuk kimlik `400 invalid_account_ids`. Yetki `crm.account.read`.
```json
{ "data": { "groups": [ { "accountId": "4f0c…", "items": [ { "relatedAccountId": "77aa…", "relatedAccountName": "ŞİFA ECZANESİ",
                                                         "relatedAccountType": "pharmacy", "relationshipType": "preferred-pharmacy",
                                                         "effectiveLabelCode": "preferred-pharmacy", "displayDirection": "direct", "…": "…" } ] } ] } }
```

**7.5 Planlanan ziyaret önizlemesi** — slot ve doktor içerik özetine `frequencyStatus` + `requiredVisitCount` alanları **eklendi**, ancak motor bunları henüz **doldurmuyor** (`null`); WP-VP-3A ile dolacak.
