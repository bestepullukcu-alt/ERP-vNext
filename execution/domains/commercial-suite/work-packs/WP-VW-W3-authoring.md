# WORK PACKAGE — WP-VW-W3 · Yazım ekranları: itiraz kütüphanesi, adım sunum notları, ziyaret kanıtı ayarları — 2026-10-12

> **Kaynaklar:**
> - Mockup [`W3 Yazim Ekranlari (standalone).html`](mockups/visit-workspace/W3%20Yazim%20Ekranlari%20(standalone).html) (çözülmüş: `mockups/visit-workspace/decoded-w3/`).
> - Fark belgesi [DESIGN-GAP-W3-authoring](mockups/visit-workspace/DESIGN-GAP-W3-authoring-2026-10-12.md) §2–§6 (kararlar §6).
> - Ana paket [WP-VW-W3](WP-VW-W3-visit-execution.md) §2.2–2.3 + §37.
>
> **Taban:** test dalı (CRM 2608/0/5, Web 898/0, mimari 36).

## 0. Kararlar (kullanıcı, 2026-10-12)
- **Onaylar Workflow + Görev Merkezi ile**, iddialarda ve güvenlik metninde yaptığımız gibi. Kopyalanacak desen: `RegulatoryTextLifecycle.cs` / `RegulatoryTextCore.cs` (KP-5a, canlı E4 2026-10-02).
  - **İtiraz yanıtı** → şablon `CRM-OBJ-RESP`, onaylayıcı **MED-DIR** pozisyonu.
  - **Süreli istisna** → şablon `CRM-VISIT-EVIDENCE-EXC`, onaylayıcı sabit bir **satış operasyon** pozisyonu (yönetici tabanlı onay sonraya; Platform değişikliği ister).
- **CRM tarafı SoD:** Platform yalnız başlatanı engeller. CRM sonuç uygulayıcısı yazar / son düzenleyen / gönderen onayını **kabul etmez** (taslağa döner, neden kodu).
- **Temsilci kapsamı** (tüzel kişilik = Platform birincil pozisyon ataması; iş birimi = CRM bölge ataması) **bu turda YOK.** Önce kiracı katmanı + süreli istisna + geçmiş + okuma izni. Katman çözümleyicisinin yeri (seam) hazırlanır; tüzel kişilik / iş birimi "çözülmedi" döner.
- **Fark belgesi kararları:**
  - K-A2: düzenleyicide tek tık "Onayla" yok;
  - K-A3: boş taslak kaydı;
  - K-A4: adıma yalnız onaylı + aynı ürün yanıtı bağlanır (backend);
  - K-A5: emekli yanıt yayınlı sunumdan da düşer;
  - K-B1: sunum notları **sekme**;
  - K-C3: istisnada geçmiş tarih yok, aynı kural için çakışma yok, erken bitirme = bitiş bugün + "Erken bitti", 30 gün iki uç dahil;
  - K-C4: okuma izni.
- **Mockup eksikleri bizde:** menü, koyu tema, Arapça (7 dil).

## 1. Tur planı
| tur | prompt | çalışma yeri / dal | bağımlılık |
|---|---|---|---|
| 1 | **W3-BE-c** · CRM: itiraz onayı Workflow'a, kütüphane okumaları, kanıt ayarı geçmişi + okuma, süreli istisna + Workflow, çözümleyici yeri, menü sayfaları, gateway | `C:\tmp\vw-w3-be-c` / `wp/vw-w3-be-c` | — |
| 2 | **W3-WEB-c** · itiraz kütüphanesi (A1–A3) + KP Studio adımında "Sunum notları" sekmesi | `C:\tmp\vw-w3-web-c` / `wp/vw-w3-web-c` | BE-c test dalında |
| 2 | **W3-WEB-d** · ziyaret kanıtı ayarları (kiracı + etkin ayar + geçmiş + süreli istisnalar) | `C:\tmp\vw-w3-web-d` / `wp/vw-w3-web-d` | BE-c test dalında |
| 3 | **W3-CFG** · 2 Workflow şablonu (canlı) + yetki betiği | `C:\tmp\vw-w3-cfg` / `wp/vw-w3-cfg` | BE-c + fleet restart |
| sonra | **PLT-ORG-SEAT** · Platform "benim pozisyon atamam" ucu + bildirim | — | katmanlar için |

## 2. Sabit sözleşme (BE-c → WEB-c / WEB-d)

### 2.1 İtiraz yanıtı (`api/crm/objection-responses`)
- **Durum:** `draft` → `in_review` → `approved` → `retired`.
  - `in_review` iken düzenleme kilitli.
  - Reddedilen / iptal edilen / zaman aşımına uğrayan tur → `draft` (karar ve not saklanır).
- **Uçlar:**

| uç | işlev |
|---|---|
| `GET` (liste) | süzgeç `productId`, `categoryCode`, `status`, **`q`** (soruda arama), **`page`**, **`pageSize`** |
| `GET {id}` | kayıt |
| `POST` | oluştur |
| `PUT {id}` | güncelle (taslak; soru / yanıt boş olabilir) |
| `POST {id}/submit` | tamlık: soru + yanıt dolu, iddialar onaylı ve aynı ürün |
| `POST {id}/withdraw` | yalnız gönderen |
| `POST {id}/decision` | `{ decision: "approve" \| "return", comment }`; `return`'de not zorunlu |
| `POST {id}/retire` | emekliye ayır |
| `POST {id}/copy` | yeni taslak, `copiedFromId` |
| `GET {id}/usages` | kullanıldığı yerler |

  Eski `POST {id}/approve` → **409 `approval_via_workflow_only`**.
- **DTO'ya eklenenler:**
  - `productName`, `productForm` (MDM; ulaşılamazsa null, yanıt 200);
  - `usageCount`, `reviewRounds[]`, `decisions[]` (kim, ne zaman, karar, not);
  - `submittedBy` / `submittedAt`, `copiedFromId`;
  - `abilities: { canEdit, canSubmit, canWithdraw, canDecide, canRetire, canCopy }`.
- **`usages[]`:** `{ pathId, pathName, pathVersion, pathStatus, stepId, stepOrder, stepTitle }`.
- **İddia seçici:** iddia listesine `productId` süzgeci (yalnız onaylı, arşivlenmemiş). Yoksa eklenir.
- **Adım bağlantısı** (`KnowledgePathStep.presentation.objectionResponseIds`): yalnız **onaylı** ve yolun **ürünüyle aynı ürün**. Yolun ürünü `KnowledgePathReviewSubmission`'daki türetme ile aynı kaynaktan. Aksi → 400 `path_step_objection_invalid`.
- **Workflow:**

| alan | değer |
|---|---|
| objectType | `crm.objection-response` |
| şablon (ayar) | `Crm:ObjectionResponses:Workflow:TemplateCode` = `CRM-OBJ-RESP` |
| idempotencyKey | `crm.objection-response:{id}:r{n}` |
| DisplayContext | başlık "İtiraz yanıtı onayı · {kategori etiketi}", alt başlık = sorunun ilk satırı, çipler ürün / kategori, derin bağlantı `/CRM/ObjectionResponses/{id}` |

  - Sonuç olayı mevcut tüketiciye (`ClaimWorkflowOutcomeConsumer`) `crm.objection-response` rotası olarak eklenir.
  - Okumada uzlaştırma (120 sn).
  - **SoD sonuç uygulayıcıda:** onaylayan yazar / son düzenleyen / gönderen ise → `draft` + neden `objection_response_sod`.
  - **Onay anında iddialar yeniden denetlenir;** artık onaylı değilse → `draft` + `objection_claim_not_approved`.

### 2.2 Kanıt ayarı (`api/crm/visit-evidence-policy`)
- `GET` artık **`crm.visit-evidence-policy.read`** (yeni) **veya** `.manage` ile. DTO'ya `updatedBy`, `updatedAt`, `isDefault`, `repCount` (aktif birincil bölge ataması olan temsilci sayısı).
- **`PUT`** değişmez; her değişen alan için **geçmiş satırı** yazar.
- **`GET history`:** `[{ at, by, scope: "tenant", field, oldValue, newValue, kind: set|created }]`, yeniden eskiye, sayfalı.
- **`GET effective?resourceId=`** (okuma izni):
  - `fields[{ field, value, source: tenant|exception, exceptionId? }]`;
  - `activeException?`;
  - `scopeChain: { legalEntity: null, businessUnit: null, note: "unresolved" }`.
- **`GET reps?q=`:** aktif birincil bölge ataması olan temsilciler `{ resourceId, displayName }`, en çok 20.
- **Çözümleyici yeri:** `IVisitEvidencePolicyResolver.ResolveAsync(tenantId, resourceId, at)` → etkin ayar + alan kaynakları + aktif istisna.
  - Ziyaret başlatma ve `contract.policy` bunu kullanır.
  - Tüzel kişilik / iş birimi katmanı bu turda hep "çözülmedi" (sonraki paket doldurur).

### 2.3 Süreli istisna (`api/crm/visit-evidence-exceptions`)
- **Alanlar:**

| alan | kural |
|---|---|
| `resourceId`, `repDisplayName` (anlık görüntü) | |
| `rules[]` | ⊂ `location_info`, `photo_optional`, `account_without_location_allow`; ≥ 1 |
| `startDate`, `endDate` | DateOnly, UTC bugün ≤ start ≤ end, (end − start + 1) ≤ 30 |
| `reason` | zorunlu, ≤ 500 |
| `status` | `draft` → `in_review` → `approved` → `cancelled` \| `ended_early` |
| `phase` (türetilir, onaylıda) | `planned` \| `active` \| `expired` |
| zaman damgaları | `createdBy` / `At`, `submittedBy` / `At`, `approvedBy` / `At`, `cancelledBy` / `At` + `cancelReason`, `reviewRounds[]`, `decisions[]` |

- **Çakışma:** aynı temsilci + aynı kural için `in_review` / `approved` (iptal / erken bitmemiş) bir istisnayla tarih kesişirse → 409 `visit_evidence_exception_overlap`.
- **Uçlar:**
  - `GET` liste (süzgeç `status` \| `phase`, durum sayıları);
  - `GET {id}`;
  - `POST` (taslak);
  - `PUT {id}` (taslak);
  - `POST {id}/submit`, `POST {id}/withdraw` (gönderen), `POST {id}/decision` (`approve` \| `return` + not);
  - `POST {id}/cancel` (planlı onaylı; gerekçe);
  - `POST {id}/end-early` (aktif onaylı; gerekçe; `endDate` = bugün, durum `ended_early`).
- **Workflow:** objectType `crm.visit-evidence-exception`, şablon `Crm:VisitEvidenceExceptions:Workflow:TemplateCode` = `CRM-VISIT-EVIDENCE-EXC`. Aynı tüketici, uygulayıcı, uzlaştırma deseni.
  - SoD: oluşturan / gönderen onaylayamaz.
  - Derin bağlantı `/CRM/VisitEvidenceSettings?tab=exceptions&id={id}`.
- **İzinler:** `crm.visit-evidence-exception.read | manage` (karar yetkisi Workflow görevinde).
- **Ziyaret başlatma:** çözümleyici, temsilcinin bugün **aktif onaylı** istisnasını uygular (kuralları gevşetir).
  - Yürütmeye **`started_with_exception`** bayrağı + `exceptionId` + `relaxedRules[]` eklenir.
  - Bayrak `contract.flagCodes`'a eklenir.

### 2.4 Menü ve rotalar
- Platform `CrmManifestProvider`:
  - **"İtiraz kütüphanesi"** `/CRM/ObjectionResponses` (Content Studio grubu; BE-b'nin gizli sayfası görünür yapılır);
  - **"Ziyaret kanıtı"** `/CRM/VisitEvidenceSettings` (CRM ayarları / Ziyaret grubu; `crm.visit-evidence-policy.read`).
- **Gateway rotaları** (W3-BE-b'de unutulmuştu — bu pakette zorunlu ve testli):
  - `api/crm/visit-evidence-exceptions` + `/{everything}`;
  - `api/crm/visit-evidence-policy/{everything}`;
  - mevcut `objection-responses` rotaları yeni yöntemleri (`POST` / `PUT`) kapsıyor mu, kontrol edilir.

---

## W3-BE-c — CRM backend
§2.1–2.4'ün tamamı.

**Ek kurallar:**
- Yeni toplamlar `RegisterClassMaps`'te (GUID tuzağı); bilinmeyen BSON alanı yazma yok.
- Workflow istemcisi mevcut `IClaimWorkflowClient` / `GatewayClaimWorkflowClient` (kullanıcı jetonu iletilir); yeni HTTP istemcisi yazma.
- Workflow erişilemezse gönderim 503, hiçbir şey değişmez. Şablon yoksa 409 `workflow_template_missing`.
- Ret notu Workflow geçmişinden okunur (olayda yok).
- İzinler `CrmManifestProvider`'da (İngilizce); seed / grant yok.

**Acceptance:**
- İtiraz:
  - boş taslak kaydı;
  - gönderimde tamlık / iddia / ürün denetimi;
  - `in_review` kilidi;
  - karar onayla / geri gönder (not zorunlu);
  - SoD uygulayıcıda (yazar, son düzenleyen, gönderen → `draft` + neden);
  - geri çekme yalnız gönderen;
  - eski `/approve` → 409;
  - kullanım okuması;
  - arama + sayfa;
  - kopya;
  - adım bağlantısı onaylı + aynı ürün.
- Kanıt ayarı: okuma izni; geçmiş satırları (değişen alan başına); etkin ayar (kiracı + istisna kaynakları).
- İstisna:
  - tarih kuralları (geçmiş tarih yok, 30 gün iki uç dahil, bitiş zorunlu);
  - çakışma 409;
  - akış + SoD;
  - iptal / erken bitirme;
  - türetilen faz.
- Başlatma: aktif istisna gevşetir (`location_info` ile konum zorunlu kiracıda uzak başlatma kabul) + bayrak; onaysız / süresi geçmiş istisna gevşetmez.
- Gateway rota testi; mimari sayı raporlanır.

**Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
- SoD uygulayıcıdan kaldırılsın → kırmızı.
- İstisna çakışma kontrolü kaldırılsın → kırmızı.
- Onaysız istisna gevşetsin → kırmızı.
- Gateway rotası silinsin → kırmızı.

## W3-WEB-c — itiraz kütüphanesi + adım sunum notları (Web)
> Mockup ile **görsel** karşılaştır: A1, A1 boş, A2 yeni / düzenle, A3 onaylı / onay bekliyor / SoD / emekli, B sekme, B yayınlanmış, AR A1.

1. **`/CRM/ObjectionResponses`:**
   - liste: süzgeçler, arama, sayfa, sütunlar mockup'taki gibi, boş ve "sonuç yok" durumları;
   - düzenle / yeni: iddia seçici yalnız seçili ürünün onaylı iddiaları, en çok 10; sayaçlar; sağda temsilci kumandası önizlemesi;
   - ayrıntı: durum çipleri, "Onay bekliyor · kişi, tarih", "Kullanıldığı yerler", geçmiş zaman çizelgesi;
   - eylemler (`abilities`'e göre): Onaya gönder / Geri çek / Onayla / Taslağa geri gönder (not iletişim kutusu) / Emekliye ayır (iletişim kutusu, "N adımda kullanılıyor") / Kopyasından yeni taslak;
   - **düzenleyicide tek tık "Onayla" YOK** (K-A2);
   - hata durumları: 409 sürüm çakışması, 503, `objection_claim_not_approved`, `objection_response_sod`.
2. **Kategoriler kodla taşınır; etiketler BRD'den** (mockup etiketleri değil). Renkler önyüzde koda göre.
3. **KP Studio adım düzenleyicisi** (mevcut `Views/CRM/KnowledgePaths/**`): **"Sunum notları" sekmesi** (K-B1).
   - anahtar mesaj ≤ 500, konuşma metni ≤ 4000, hedef süre 10–3600 sn (boşsa tahmini **dakika** × 60 ipucu), geçiş ≤ 500;
   - beklenen itirazlar ≤ 10 (yalnız onaylı + aynı ürün);
   - "Kütüphanede yeni oluştur" (kaydedilmemiş değişiklik uyarısıyla);
   - doktor / temsilci önizlemesi;
   - yol düzeyinde tamlık sayacı ("Sunum notları: n / t adım dolu"; dolu = anahtar mesaj + konuşma metni);
   - yayınlanmış sürümde salt okunur;
   - kaydette `presentation` nesnesi **her zaman tam** gönderilir.
   - Mockup'taki "İçerik" sekmesi yer tutucudur; mevcut içerik alanları aynen kalır.
4. **Web vekilleri ve izinler;** UAS-001; 7 dil yankısız, Arapça RTL, koyu tema token'ları.

**Acceptance:**
- Saf kurallar Node'da:
  - eylem uygunluğu (`abilities` × durum);
  - not zorunluluğu;
  - tamlık sayacı;
  - hedef süre ipucu (dakika);
  - itiraz seçici süzgeci (onaylı + aynı ürün + en çok 10).
- Duman testi (sahte DOM):
  - liste;
  - gönder → onay bekliyor;
  - geri gönder notu;
  - emekli iletişim kutusu;
  - adım sekmesi kaydı tam `presentation`;
  - `createError` null.
- 7 dil.

**Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
- `presentation` kısmi gönderilsin → kırmızı.
- Geri gönderde not zorunluluğu kalksın → kırmızı.

## W3-WEB-d — ziyaret kanıtı ayarları (Web)
> Mockup ile **görsel** karşılaştır: C1 + C2 kiracı, C1 etkin ayar, C3, C4 liste / yeni / SoD, C salt okuma, yetkisiz, AR C2.

1. **`/CRM/VisitEvidenceSettings`, iki sekme:** "Kapsamlı ayarlar" · "Süreli istisnalar" (rozet = onay bekleyen + aktif).
2. **Kapsam ağacı bu turda yalnız "Kiracı varsayılanı"** (silinemez, "Tüm alanlar dolu · N temsilci", son değişiklik).
   - "Tüzel kişilik ekle" pasif + ipucu: "Tüzel kişilik ve iş birimi katmanları temsilcinin pozisyon ve bölge ataması bağlandığında açılır."
3. **Ayar formu** (8 alan, mockup gruplarıyla) + önizleme ("Bu kapsamdaki temsilci şunu görecek") + kaydet iletişim kutusu (fark satırları, "N temsilciye uygulanır").
   - Süre tutarlılığı kapalıyken aralık dışı kısa ziyaret değeri gönderilmez.
   - Değer eşlemesi: `required` / `info`, `optional`, `allow_flagged` / `block`.
4. **Etkin ayar araması:** temsilci arama kutusu (`reps?q=`) → alan / değer / kaynak (Kiracı · İstisna), aktif istisna şeridi.
5. **Değişiklik geçmişi** sekmesi.
6. **Süreli istisnalar:**
   - liste (durum / faz süzgeci + sayılar);
   - yeni istisna çekmecesi: temsilci, 3 kural, tarihler + gün sayacı, gerekçe; doğrulamalar; SoD kutusu;
   - ayrıntı çekmecesi: zaman çizelgesi; Onaya gönder / Geri çek / Onayla / Taslağa geri gönder / İptal et / Erken bitir (gerekçeli).
7. **"Ek kanıtlar · Yakında"** kartı (işlevsiz); salt okuma durumu; UAS-001; 7 dil, RTL, koyu tema.

**Acceptance:**
- Saf kurallar Node'da:
  - gün sayacı (iki uç dahil, ≤ 30, bitiş zorunlu, geçmiş başlangıç yok);
  - kural seçimi;
  - önizleme cümleleri (ayar × değer);
  - kaydet farkı;
  - faz / durum çipi.
- Duman testi (sahte DOM):
  - form kaydet iletişim kutusu;
  - istisna oluştur → onaya gönder;
  - SoD düğmesi pasif;
  - salt okumada düğmeler gizli;
  - `createError` null.
- 7 dil.

**Sabotajlar** (her biri kırmızı kanıtlanır, sonra geri alınır):
- 31 günlük aralık kabul edilsin → kırmızı.
- Salt okumada Kaydet görünsün → kırmızı.

## W3-CFG — Workflow şablonları + yetkiler (canlı yapılandırma, uygulama kodu YOK)
WP-KP-5a-CFG deseni.

1. **Şablon `CRM-OBJ-RESP`:**
   - tek aşama, tek adım `medical` ("Tıbbi onay");
   - aday **MED-DIR** pozisyonu (kimliği canlıdan oku);
   - SLA KP-MLR-TR'nin tıbbi adımı gibi;
   - `rejectCommentRequired: true`;
   - oluştur → yayınla (idempotent).
2. **Şablon `CRM-VISIT-EVIDENCE-EXC`:**
   - tek adım `sales_ops` ("Satış operasyon onayı");
   - aday **satış operasyon pozisyonu**. Organizasyonda yoksa **DUR + raporla**; pozisyonu kullanıcı oluşturur ve atar, agent pozisyon / atama YAZMAZ.
3. **Grant betiği** `scripts/rbac/grant_w3_authoring_rbac_97c5.py` (dry-run varsayılan, subtype-4 GUID, yerel belgeden tarih biçimi, marker):
   - **Admin:** yeni CRM anahtarları + `crm.objection-response.submit` (varsa) + `platform.workflow.instances.start` / `view`, `tasks.approve` / `reject` / `cancel`;
   - **onaylayıcı rolü** (sema'nınki Admin değilse): okuma anahtarları.
4. **Duman testi** (salt okuma): şablonlar `Published`; diğer şablonların parmak izi aynı.

---

## §36.1 Agent Prompt — W3-BE-c (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3 · W3-BE-c — itiraz yanıtı onayı Workflow + Görev Merkezi'ne (gönder/geri çek/karar, CRM tarafı SoD, sonuç uygulayıcı + uzlaştırma), kütüphane okumaları (arama/sayfa/kullanım/ürün adı/kopya), adım bağlantısı onaylı+aynı ürün; kanıt ayarı okuma izni + değişiklik geçmişi + etkin ayar + temsilci listesi + çözümleyici yeri; süreli istisna toplamı + Workflow; menü sayfaları + gateway rotaları
Repository: C:\tmp\vw-w3-be-c (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3-be-c · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-authoring.md — önce oku (§0 kararlar, §2 SABİT sözleşme, "W3-BE-c" + Acceptance). Bağlam: mockups/visit-workspace/DESIGN-GAP-W3-authoring-2026-10-12.md (§2, §5.2 onay deseni ve riskleri, §6 kararlar), WP-VW-W3-visit-execution.md §2.2–2.3 + §37 (BE-a/BE-b kabul notları, gateway eksikliği dersi). DESEN (kopyala, yeniden tasarlama): services/Diten.CrmService/src/**/Features/Knowledge/Regulatory/{RegulatoryTextLifecycle.cs (gönder 180-247, geri çek 250-303, karar 310-430), RegulatoryTextCore.cs (uygulayıcı 136-256, uzlaştırma 260-331)}, Infrastructure/Workflow/GatewayClaimWorkflowClient.cs, Infrastructure/Eventing/ClaimWorkflowOutcomeConsumer.cs (ObjectType rotası), ClaimReviewRound. Kod: Domain/Entities/{ObjectionResponse,KnowledgePath,VisitEvidencePolicy,VisitExecution,TerritoryResourceAssignment}.cs, Application/Features/{Knowledge/ObjectionResponses/**,Knowledge/Path/**,VisitExecution/**,ContentComposition/Claims (liste süzgeci)}, VisitPlanning/MyAccounts/RepTerritoryCoverage.cs (aktif birincil atama kuralı), Persistence (RegisterClassMaps), Api/Controllers/CRM/**; Platform CrmManifestProvider; gateway/Diten.ApiGateway/ocelot.json + testleri.
NE: belge §2.1–2.4 ve "W3-BE-c": (1) ObjectionResponse in_review + reviewRounds/decisions + submit/withdraw/decision (return'de not zorunlu) + uygulayıcı (approved/rejected→draft/cancelled→draft/timed-out→draft; SoD yazar/son düzenleyen/gönderen → draft + objection_response_sod; iddialar yeniden denetim → objection_claim_not_approved) + okumada uzlaştırma + tüketici rotası crm.objection-response + eski /approve 409 approval_via_workflow_only; boş taslak kaydı; liste q/page/pageSize; GET {id}/usages + usageCount; productName/productForm (MDM, ulaşılamazsa null); POST {id}/copy (copiedFromId); abilities; iddia listesine productId süzgeci; adım bağlantısı yalnız onaylı + aynı ürün (400 path_step_objection_invalid). (2) kanıt ayarı: GET okuma izni crm.visit-evidence-policy.read veya manage; DTO updatedBy/updatedAt/isDefault/repCount; PUT değişen alan başına geçmiş satırı; GET history; GET effective?resourceId=; GET reps?q=; IVisitEvidencePolicyResolver (kiracı ⊕ aktif onaylı istisna; tüzel kişilik/iş birimi "unresolved") — ziyaret başlatma + contract.policy bunu kullanır. (3) VisitEvidenceException toplamı + uçlar (liste/al/oluştur/güncelle/submit/withdraw/decision/cancel/end-early) + Workflow crm.visit-evidence-exception; tarih kuralları (UTC bugün ≤ start ≤ end, ≤30 gün iki uç dahil), çakışma 409 visit_evidence_exception_overlap, SoD; başlatmada aktif onaylı istisna gevşetir + started_with_exception/exceptionId/relaxedRules + flagCodes. (4) CrmManifestProvider: /CRM/ObjectionResponses (Content Studio, görünür) + /CRM/VisitEvidenceSettings (crm.visit-evidence-policy.read); yeni izinler (crm.visit-evidence-policy.read, crm.visit-evidence-exception.read/manage, gerekiyorsa crm.objection-response.submit) İngilizce. (5) gateway rotaları (visit-evidence-exceptions + /{everything}, visit-evidence-policy/{everything}; objection-responses POST/PUT kapsamı) + rota testi. Alan adları/uçlar/hata kodları §2'de SABİT (WEB-c/WEB-d buna bağlı); değiştirmen gerekirse DUR + raporla.
KORU/YAPMA: ARCH GATE; TenantId; Platform KODUNA DOKUNMA (yalnız CrmManifestProvider satırları); Workflow şablonu/pozisyon/grant/seed/canlı DB yazımı YOK (W3-CFG); yeni Workflow HTTP istemcisi yazma (mevcut istemci); Web'e dokunma; tüzel kişilik/iş birimi katmanı YOK (yalnız yer); mevcut BE-a/BE-b davranışı eklemeli değişir (mobil sözleşmesi bozulmaz).
DOĞRULA (E2): CRM 2608/0/5 tabanı (PiiMasking_… bilinen kararsız) · Platform CrmManifest* yeşil · Gateway testleri (önceden var olan 5063–5065 kırmızısı hariç yeni kırmızı yok) · mimari: yeni komutlar denetimsiz sayıyı artırır (36 → yeni sayı; listeye ekleme YOK, raporla). Testler belge Acceptance; sabotaj 4 (SoD uygulayıcıdan kaldır; istisna çakışma kontrolü kaldır; onaysız istisna gevşetsin; gateway rotası sil → her biri kırmızı; geri al — git checkout -- YOK, .bak + touch). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(crm): WP-VW-W3-BE-c — objection response approval via Workflow/WorkCenter, library reads, evidence policy read/history/effective + time-boxed exceptions via Workflow, menu pages, gateway routes" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, hata kodu tablosu, yeni mimari sayı, W3-CFG için gerekenler (şablon kodları, objectType'lar, gereken izinler), mobil için yeni alanlar (started_with_exception), elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W3-WEB-c (paste-ready · 2. TUR, BE-c test dalına alındıktan SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3 · W3-WEB-c — İtiraz kütüphanesi (/CRM/ObjectionResponses: liste, düzenle, ayrıntı, onaya gönder/geri çek/onayla/geri gönder, emekli, kopya, kullanıldığı yerler) + KP Studio adım düzenleyicisinde "Sunum notları" sekmesi
Repository: C:\tmp\vw-w3-web-c (worktree, test/crm-content-visit-e2e güncel başı — W3-BE-c içerir) · Branch: wp/vw-w3-web-c · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-authoring.md — önce oku (§0, §2.1 + §2.4 sözleşme, "W3-WEB-c" 1–4 + Acceptance) + aynı belgedeki §37 (BE-c kabul notu; gerçek alan adları). MOCKUP: mockups/visit-workspace/"W3 Yazim Ekranlari (standalone).html" (Ekran seçici A1, A1 boş, A2 yeni/düzenle, A3 onaylı/onay bekliyor/SoD/emekli, B sekme, B yayınlanmış, AR A1) + decoded-w3/{Itirazlar.dc.html, KPAdim.dc.html} + fark belgesi DESIGN-GAP-W3-authoring-2026-10-12.md §3 (etiketler BRD'den, dakika birimi, İçerik sekmesi yer tutucu) — GÖRSEL karşılaştırarak yap (dizini py -m http.server ile AYRI sekmede aç; oturum açık kullanıcı sekmesine dokunma). Desen: mevcut CRM liste/form sayfaları (Claims, KnowledgePaths), Golden Slim DataTable, UAS-001, L10n köprüsü, saf çekirdek + Node testi. Kod: frontend/Diten.Web/{Controllers/CRM, Views/CRM/KnowledgePaths/**, Views/CRM/ObjectionResponses/** (yeni), Resources/Views/CRM/**, wwwroot/assets/js/CRM/{KnowledgePaths,ObjectionResponses}/**}; testler Diten.Web.Tests/**.
NE: belge "W3-WEB-c" 1–4: A1 liste (süzgeçler, q arama, sayfa, sütunlar, boş + sonuç yok), A2 düzenle/yeni (iddia seçici yalnız seçili ürünün onaylı iddiaları ≤10, sayaçlar, önizleme; düzenleyicide tek tık "Onayla" YOK), A3 ayrıntı (durum, "Onay bekliyor · kişi, tarih", kullanıldığı yerler, geçmiş; abilities'e göre Onaya gönder/Geri çek/Onayla/Taslağa geri gönder (not iletişim kutusu)/Emekliye ayır (iletişim kutusu)/Kopyasından yeni taslak; 409/503/objection_claim_not_approved/objection_response_sod durumları); kategoriler kodla, etiketler BRD'den, renk koda göre; KP Studio adımına "Sunum notları" SEKMESİ (alanlar + sınırlar, hedef süre ipucu tahmini DAKİKA×60, beklenen itirazlar ≤10 yalnız onaylı + aynı ürün, "Kütüphanede yeni oluştur" kaydedilmemiş uyarısıyla, doktor/temsilci önizlemesi, yol düzeyinde tamlık sayacı, yayınlanmışta salt okunur, kaydette presentation nesnesi HER ZAMAN tam); mevcut İçerik alanları aynen kalır; vekiller + izin sabitleri; UAS-001; 7 DİL yankısız, Arapça RTL, koyu tema token'ları.
KORU/YAPMA: ARCH GATE; CRM/Platform koduna dokunma; mevcut KP Studio davranışı (yayın, sürüm, inceleme) DEĞİŞMEZ; yeni JS kütüphanesi yok; VisitEvidenceSettings dosyalarına dokunma (W3-WEB-d paralel); oturum açık kullanıcı sekmesine mock/harness enjekte etme, canlıda kalıcı yazma YOK.
DOĞRULA (E2): Web 898/0 tabanı · KnowledgePaths testleri yeşil · mimari değişmez. Testler belge Acceptance (saf kurallar Node'da: abilities × durum eylemleri, not zorunluluğu, tamlık sayacı, hedef süre ipucu, itiraz seçici süzgeci; duman testi sahte DOM: liste, gönder → onay bekliyor, geri gönder notu, emekli iletişim kutusu, adım sekmesi kaydı tam presentation, createError null; 7 dil); sabotaj 2 (presentation kısmi gönderilsin → kırmızı; geri gönderde not zorunluluğu kalksın → kırmızı; geri al — git checkout -- YOK). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(web): WP-VW-W3-WEB-c — objection response library (list, edit, detail, workflow actions, retire, copy, usages) + presentation notes tab in the knowledge path step editor" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde madde yapıldı / bilinçli sapma, mockup'la görsel karşılaştırma notu, elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W3-WEB-d (paste-ready · 2. TUR, BE-c test dalına alındıktan SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3 · W3-WEB-d — Ziyaret kanıtı ayarları (/CRM/VisitEvidenceSettings): kiracı ayar formu + önizleme + kaydet onayı, etkin ayar araması, değişiklik geçmişi, süreli istisnalar (liste, yeni, ayrıntı, Workflow eylemleri, iptal/erken bitir)
Repository: C:\tmp\vw-w3-web-d (worktree, test/crm-content-visit-e2e güncel başı — W3-BE-c içerir) · Branch: wp/vw-w3-web-d · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-authoring.md — önce oku (§0, §2.2–2.4 sözleşme, "W3-WEB-d" 1–7 + Acceptance) + aynı belgedeki §37 (BE-c kabul notu). MOCKUP: mockups/visit-workspace/"W3 Yazim Ekranlari (standalone).html" (Ekran seçici C1+C2 kiracı, C1 etkin ayar, C3, C4 liste/yeni/SoD, C salt okuma, yetkisiz, AR C2) + decoded-w3/ZiyaretKanit.dc.html — GÖRSEL karşılaştırarak yap (dizini py -m http.server ile AYRI sekmede aç; oturum açık kullanıcı sekmesine dokunma). Bu turda kapsam ağacı YALNIZ kiracı (tüzel kişilik/iş birimi katmanları sonra; "Tüzel kişilik ekle" pasif + ipucu). Desen: mevcut CRM ayar/liste sayfaları, UAS-001, L10n köprüsü, saf çekirdek + Node testi. Kod: frontend/Diten.Web/{Controllers/CRM/VisitEvidenceSettingsController.cs (yeni), Views/CRM/VisitEvidenceSettings/**, Resources/Views/CRM/VisitEvidenceSettings/**, wwwroot/assets/js/CRM/VisitEvidenceSettings/**}; testler Diten.Web.Tests/**.
NE: belge "W3-WEB-d" 1–7: iki sekme (Kapsamlı ayarlar · Süreli istisnalar, rozet); kapsam ağacında yalnız Kiracı varsayılanı (N temsilci, son değişiklik); 8 alanlı form mockup gruplarıyla + "Bu kapsamdaki temsilci şunu görecek" önizlemesi + kaydet iletişim kutusu (fark satırları, "N temsilciye uygulanır"); değer eşlemesi required/info, optional, allow_flagged/block; süre tutarlılığı kapalıyken aralık dışı değer gönderme; etkin ayar araması (reps?q= → alan/değer/kaynak Kiracı|İstisna + aktif istisna şeridi); değişiklik geçmişi; süreli istisnalar liste (durum/faz süzgeci + sayılar) + yeni çekmece (temsilci, 3 kural, tarihler + gün sayacı iki uç dahil ≤30, geçmiş başlangıç yok, gerekçe ≤500, SoD kutusu) + ayrıntı çekmecesi (zaman çizelgesi; Onaya gönder/Geri çek/Onayla/Taslağa geri gönder/İptal et/Erken bitir gerekçeli); "Ek kanıtlar · Yakında" işlevsiz kart; salt okuma; UAS-001; 7 DİL yankısız, Arapça RTL, koyu tema token'ları; vekiller + izin sabitleri.
KORU/YAPMA: ARCH GATE; CRM/Platform koduna dokunma; tüzel kişilik/iş birimi katmanı ÇİZME (yalnız pasif ekle düğmesi + ipucu); yeni JS kütüphanesi yok; ObjectionResponses/KnowledgePaths dosyalarına dokunma (W3-WEB-c paralel); oturum açık kullanıcı sekmesine mock/harness enjekte etme, canlıda kalıcı yazma YOK.
DOĞRULA (E2): Web 898/0 tabanı · mimari değişmez. Testler belge Acceptance (saf kurallar Node'da: gün sayacı, kural seçimi, önizleme cümleleri, kaydet farkı, faz/durum çipi; duman testi sahte DOM: kaydet iletişim kutusu, istisna oluştur → onaya gönder, SoD düğmesi pasif, salt okumada düğmeler gizli, createError null; 7 dil); sabotaj 2 (31 günlük aralık kabul edilsin → kırmızı; salt okumada Kaydet görünsün → kırmızı; geri al — git checkout -- YOK). dotnet test -o çıktısı REPO İÇİNDE.
Commit: "feat(web): WP-VW-W3-WEB-d — visit evidence settings (tenant policy form with preview and save confirm, effective-setting lookup, change history, time-boxed exceptions with workflow actions)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde madde yapıldı / bilinçli sapma, mockup'la görsel karşılaştırma notu, elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W3-CFG (paste-ready · 3. TUR, BE-c test dalına alınıp fleet yeniden başlatıldıktan SONRA)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3 · W3-CFG — Workflow şablonları CRM-OBJ-RESP (MED-DIR) + CRM-VISIT-EVIDENCE-EXC (satış operasyon pozisyonu) canlı oluştur + yayınla; grant betiği (dry-run varsayılan) — tenant 97c5, uygulama KODU YOK
Repository: C:\tmp\vw-w3-cfg (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3-cfg · commit (rapor + betik) bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3-authoring.md — önce oku ("W3-CFG" 1–4) + §37 BE-c kabul notu (şablon kodları, objectType'lar, izinler). DESEN: execution/domains/commercial-suite/work-packs/WP-KP-5a-CFG-regulatory-templates.md (aynı akış: POST api/v1/workflow/definitions → publish; ekran /Platform/Workflow; idempotent; parmak izi kontrolü) + scripts/rbac/grant_*_97c5.py (dry-run, subtype-4 GUID, tarih biçimi yerel belgeden, marker). Ön koşul: kullanıcı fleet'i yeniden başlatmış ve yerleşik tarayıcıda giriş yapmış olmalı.
VERİ YAZMA YETKİSİ (yalnız bu liste): (1) 2 Workflow şablonunun oluşturulması + yayınlanması; (2) grant betiğinin YAZILMASI (çalıştırmayı kullanıcı yapar, agent RBAC'a YAZMAZ). Pozisyon/atama OLUŞTURMA YOK: satış operasyon pozisyonu Organizasyon'da yoksa DUR + raporla (kullanıcı oluşturup atar).
NE: CRM-OBJ-RESP (tek aşama/tek adım medical "Tıbbi onay", aday MED-DIR pozisyonu — kimliği canlıdan oku, SLA KP-MLR-TR tıbbi adımı gibi, rejectCommentRequired: true, requestedObjectType crm.objection-response); CRM-VISIT-EVIDENCE-EXC (tek adım sales_ops "Satış operasyon onayı", aday satış operasyon pozisyonu, rejectCommentRequired: true, requestedObjectType crm.visit-evidence-exception); oluştur → yayınla (idempotent); betik scripts/rbac/grant_w3_authoring_rbac_97c5.py: Admin'e BE-c'nin yeni CRM anahtarları + platform.workflow.instances.start/view + tasks.approve/reject/cancel (eksik olanlar), onaylayıcı rolü Admin değilse okuma anahtarları; Auth kataloğunda anahtarların varlığını önce doğrula (yoksa DUR).
KORU/YAPMA: kod değişikliği YOK; CLAIM-*/KP-MLR-*/KP-REG-* şablonlarına DOKUNMA (parmak izi önce/sonra aynı); örnek başlatma/kayıt oluşturma YOK (E4'te CT).
DOĞRULA: GET api/v1/workflow/definitions → 2 şablon Published, activePublishedVersionId dolu, tek adım + doğru aday; diğer şablon parmak izleri aynı; betik dry-run çıktısı rapora.
Commit: "chore(crm): WP-VW-W3-CFG — workflow templates CRM-OBJ-RESP + CRM-VISIT-EVIDENCE-EXC (live) + RBAC grant script (dry-run default)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: oluşturulan şablonlar + kimlikleri, pozisyon durumu, betiğin dry-run çıktısı, kullanıcıya --apply + yeniden giriş talimatı. §22 TÜRKÇE. K13.
```

---

## Hazırlık (CT, 2026-10-12; kullanıcı onayı "sen yap")
- **Pozisyon:** Organizasyon ekranından **POS-SALES-OPS "Satış Operasyon"** oluşturuldu (`c1acdf12-40d2-4b2c-a2a4-afe1bdd7681d`).
  - Birim: HQ — Genel Merkez; bağlı olduğu: Head of Commercial (`hoc`).
  - Tür: Kalıcı; durum: **Aktif**.
- **Atama:** **sema pullukcu** → POS-SALES-OPS, **İkincil**, başlangıç 2026-10-12, neden Transfer. sema'nın birincil pozisyonu MED-DIR olarak kalır.
- **Gerekçe:** testlerde istisnayı Admin oluşturur; SoD gereği onaylayıcı başka biri olmalı.
- W3-CFG şablonu `CRM-VISIT-EVIDENCE-EXC` adayı = POS-SALES-OPS.


## §37 CT kabul — W3-BE-c E2 ACCEPTED (2026-10-12)

**Kaynak:** kullanıcının ayrıca başlattığı ajan, tam kapsam `0927fab9e` (`wp/vw-w3-be-c`) → test dalına alındı.
- CT'nin paralel parça 2'si (`8b76a01cf`, `wp/vw-w3-be-c2`) aynı işi tekrar ettiği için **birleştirilmedi** (dal yedek olarak kalır).
- Parça 1 ajanı yazmadan durdu.

**CT ölçümü:**
- **CRM 2640/0/5.**
- **Platform** CrmManifest / CrmVisit **66/0**.
- **Gateway 111/1:** yalnız önceden var olan 5063–5065 kırmızısı; yeni rota testleri yeşil.
- **Mimari:** denetimsiz **36 → 47**.
- **Web 898/0** — CT düzeltmesiyle: `Nav.Page.OBJECTIONRESPONSES` ve `Nav.Page.VISITEVIDENCESETTINGS` 7 dilde eklendi, yeni görünür menü sayfaları için `NavManifestL10nGuardTests` kırmızısı kapandı.

**CT kod okuması:**
- **Ortak onay çekirdeği** `Features/Approvals/WorkflowApprovalCore.cs` (RegulatoryText deseni).
- **Sonuç uygulayıcı:**
  - onaylayan bilinmiyorsa uygulamaz (SoD atlanmaz);
  - yazar / son düzenleyen / gönderen → `draft` + SoD nedeni;
  - itirazda iddialar, istisnada çakışma onay anında yeniden denetlenir.
- **Kimlik biçimi doğrulandı:** Workflow `ActionedBy` = düz kullanıcı kimliği (canlı `approval_tasks`), CRM `ActorName` = `sub` → SoD karşılaştırması gerçekten tutar.

**CT sabotajı:** `WorkflowApprovalRules.IsOwn` hep "değil" yapıldı → **6 kırmızı** (karar ucu, uygulayıcı yazar / editör / gönderen, olay rotası, istisna); `.bak` + touch ile geri alındı.

**Kabul edilen sapmalar:**
- İtiraz listesi yanıtı `{ items, total, page, pageSize }`.
- `productForm` hep null (MDM seçicide yok).
- İstisna yalnız aktif birincil bölge ataması olan temsilciye.
- Erken bitirme iptal alanlarını paylaşır.
- `crm.objection-response.submit` anahtarı eklendi, APPROVE eylemi kaldırıldı.
- Ek hata kodları: `objection_claim_product_mismatch`, `visit_evidence_exception_rep_unknown`.

**Sıradaki:** W3-WEB-c ∥ W3-WEB-d (çalışma yeri test dalı başı) → W3-CFG.

## §37 CT kabul — W3-WEB-c + W3-WEB-d E2 ACCEPTED (2026-10-12)

**Kaynak:** kullanıcının ajanları.
- WEB-c `60dc2a73d` (`wp/vw-w3-web-c`) → test dalında `90edf5341`.
- WEB-d `33d8d2eba` (`wp/vw-w3-web-d`) → test dalında `2d16a835c`.
- Her commit yalnız kendi dosyalarına dokunuyor (WEB-c: ObjectionResponses + KnowledgePaths sunum notları + testler; WEB-d: VisitEvidenceSettings + testler); çakışma yok.

**CT ölçümü (ikisi birlikte, test dalı başında):**
- **Web 947/0** (898 + WEB-c 23 + WEB-d 26).
- **Mimari 38/1:** yalnız bilinen AUD-001; denetimsiz **47** (değişmedi, Web işi CRM komutu eklemiyor).

**CT kod okuması:**
- Vekiller UAS-001'e uyuyor: her uç önce izin denetler (`Has` / `HasAny`), yetkisizde iskelet yok.
- İddia araması `productId`'yi `Guid?` olarak bağlıyor; CRM adresine serbest metin taşınmaz. Liste süzgeçleri `Clean(...)` + `Guid.TryParse` ile.
- Sayfa boyu 1..100 dışında `page_invalid`.
- WEB-c `/decision` vekili notsuz "taslağa geri gönder"i reddediyor (`decision_comment_required`) — şablonun `rejectCommentRequired` kuralıyla aynı yönde.

**CT sabotajları** (`.bak` + touch ile geri alındı; JS kaynaktan okunduğu için `--no-build`):
- `objection-core.js` `usableObjection`'dan "onaylı" koşulu kaldırıldı → **2 kırmızı** (seçici süzgeci + adım sekmesinin kullanılamaz bağlı yanıtı düşürmesi).
- `evidence-core.js` `MAX_DAYS` 30 → 31 → **2 kırmızı** (gün sayacı kuralı + duman testinde 31 günlük istisnanın gönderilmemesi).
- Geri alma sonrası 49/0.

**Kabul edilen sapmalar / açıklar:**
- WEB-d: BE-c ad döndürmediği için kişi alanları "Siz" ya da kısa kimlik gösteriyor → canlı E4'te görsel farka yazılacak; ad çözümü gerekiyorsa küçük BE işi.
- WEB-c ajanı canlı görsel karşılaştırma yapmadı → CT canlı E4'te mockup ile yan yana bakacak.

**Sıradaki:** W3-CFG (Workflow şablonları + grant betiği) → canlı E4 (A1–A3, B, C1–C4, AR) + mockup görsel karşılaştırması.

## §36.2 Uygulama raporu — W3-CFG (2026-10-12, canlı 97c5)

**Ön koşul / DUR kontrolü (salt okuma):**
- **Şablon kodu:** `AuthoringApprovalDefaults` → `CRM-OBJ-RESP`, `CRM-VISIT-EVIDENCE-EXC`; CrmService.Api `appsettings*.json`'da `Crm:ObjectionResponses:Workflow` / `Crm:VisitEvidenceExceptions:Workflow` override'ı yok.
- **Pozisyonlar:**
  - `POS-MED-DIR` "Medikal Direktör" `d0b6ce33-09ab-4789-bfaa-e49d53880fef` aktif; sema birincil.
  - `POS-SALES-OPS` "Satış Operasyon" `c1acdf12-40d2-4b2c-a2a4-afe1bdd7681d` aktif; sema ikincil, 2026-10-12'den, iptal yok.
  - Pozisyon / atama yazılmadı.
- **Auth kataloğu:** `crm.objection-response.{read,manage,submit}`, `crm.visit-evidence-policy.{read,manage}`, `crm.visit-evidence-exception.{read,manage}` ve `platform.workflow.*` mevcut.
- **Oturum:** fleet ayakta (gateway / auth / CRM `/health` 200, Platform 503 = bilinen sağlık sinyali, uçlar çalışıyor). İşlemler yerleşik tarayıcıda ayrı sekmeden, aynı kaynaktaki `/Platform/Workflow/api/…` proxy'si üzerinden yapıldı. Mock / harness yok; örnek başlatılmadı; CRM kaydı oluşturulmadı.
- **Başlangıç parmak izi:** 23 şablonun (şablon + tüm sürüm dokümanlarının SHA-256'sı, Mongo `diten_personalization_dev`) parmak izi alındı.

### 1. Şablonlar — 2/2 Published
İşlem: `POST definitions` (201) → `POST definitions/{id}/publish` (200), ilk denemede; `publishReason` = `WP-VW-W3-CFG`. Idempotent akış: kod varsa oluşturmaz, yayınlıysa yayınlamaz.

| Kod | Ad | Template id | Aktif sürüm (v1) |
|---|---|---|---|
| `CRM-OBJ-RESP` | İtiraz yanıtı — Tıbbi onay | `b6cc495f-2021-499c-a20a-8a834658df2e` | `18562f8f-fa96-4b16-b426-f1d299e133e6` |
| `CRM-VISIT-EVIDENCE-EXC` | Ziyaret kanıtı istisnası — Satış operasyon onayı | `f28ec41d-574b-41fc-882c-a2bde613a7e1` | `b0f5408f-b61a-42c5-8af7-3600470e703e` |

Yayınlı tanım (`GET definitions/{id}/versions/{v}` ile doğrulandı):

| Kod | requestedObjectType | Aşama / adım | Aday | SLA | Seçenek |
|---|---|---|---|---|---|
| `CRM-OBJ-RESP` | `crm.objection-response` | `medical` "Tıbbi Onay" / `medical` "Tıbbi onay" | `position:d0b6ce33…` (MED-DIR) | 4320 dk; 2880'de MED-DIR'e yükselt | `options.rejectCommentRequired: true` |
| `CRM-VISIT-EVIDENCE-EXC` | `crm.visit-evidence-exception` | `sales_ops` "Satış Operasyon" / `sales_ops` "Satış operasyon onayı" | `position:c1acdf12…` (POS-SALES-OPS) | 4320 dk; 2880'de POS-SALES-OPS'a yükselt | `options.rejectCommentRequired: true` |

- **SLA:** KP-MLR-TR v1 `medical` adımından birebir (4320 / 2880). İstisna şablonunda da aynı değer kullanıldı (paket bir değer vermedi).
- **Aday farkı:** KP-MLR-TR'nin tıbbi adımında ikinci aday POS-MED-SPC de var; paket "aday MED-DIR" dediği için yalnız MED-DIR konuldu.
- **`rejectCommentRequired`:** Platform kökte `options.rejectCommentRequired` okur (`WorkflowDefinitionRuntimePlan.RejectCommentRequired`). Adım `requirements.commentRequired: false` kaldı; o alan onayda da yorum ister.
- **Duman:** `CLAIM-*` (7), `KP-MLR-*` (6), `KP-REG-*` (6) ve diğer 4 şablon — **23/23 parmak izi önce / sonra aynı**; yalnız 2 yeni kod eklendi.

### 2. Yetki — betik yazıldı, RBAC'a yazılmadı
97c5 salt okuma kontrolü:
- **Admin** (`6a315467-…`) şu an:
  - var: `crm.objection-response.read / manage / approve`, `crm.visit-evidence-policy.manage`, `platform.workflow.instances.start / view`, `tasks.approve / reject / cancel`;
  - **eksik:** `crm.objection-response.submit`, `crm.visit-evidence-policy.read`, `crm.visit-evidence-exception.read / manage`.
- **sema** (`b.pullukcu@grandmedical.eu`, `d27fa4a6…`) rolleri: Admin, GQD, QADocumentation → ayrı okuyucu rolü gerekmiyor.

**Betik:** `scripts/rbac/grant_w3_authoring_rbac_97c5.py`
- dry-run varsayılan; subtype-4 GUID; tarih alanları yerel 97c5 kaydından aynen kopya;
- idempotent; marker `manual-grant-w3-authoring-rbac`;
- rol / izin oluşturmaz, katalog eksikse durur;
- `--reader-role`, Admin olmayan onaylayıcı için yalnız okuma ve karar anahtarları verir;
- eski `crm.objection-response.approve` verilmez.

Dry-run çıktısı:
```
skip (already granted): Admin ← crm.objection-response.read
skip (already granted): Admin ← crm.objection-response.manage
skip (already granted): Admin ← crm.visit-evidence-policy.manage
skip (already granted): Admin ← platform.workflow.instances.start
skip (already granted): Admin ← platform.workflow.instances.view
skip (already granted): Admin ← platform.workflow.tasks.approve
skip (already granted): Admin ← platform.workflow.tasks.reject
skip (already granted): Admin ← platform.workflow.tasks.cancel
role 'Admin' (6a315467-7d80-4ad8-bd76-78f8f762fe8a): 4 grant(s) to insert: ['crm.objection-response.submit', 'crm.visit-evidence-policy.read', 'crm.visit-evidence-exception.read', 'crm.visit-evidence-exception.manage']
DRY-RUN — nothing written. Re-run with --apply.
```
Marker'lı satır sayısı: 0 (agent yazmadı).

**Kullanıcı adımı:**
1. `py -3 scripts/rbac/grant_w3_authoring_rbac_97c5.py --apply` çalıştır.
2. Admin rolündeki kullanıcılar **yeniden giriş** yapar.

Geri alma: `db.rolePermissions.deleteMany({CreatedBy: "manual-grant-w3-authoring-rbac"})`.

### 3. Kalan (CT, `--apply` + yeniden giriş sonrası)
- **Canlı E4:**
  - itiraz yanıtı taslak → onaya gönder → `CRM-OBJ-RESP` görevi sema'ya → Work Center onay / ret (ret yorumsuz 400 olmalı) → CRM durumu;
  - istisna oluştur → gönder → `CRM-VISIT-EVIDENCE-EXC` görevi sema'ya → onay → etkin ayarda "İstisna" kaynağı;
  - A1–A3, B, C1–C4, AR ekranları mockup ile yan yana.
- Kod değişikliği yok.

## §37 CT kabul — W3-CFG E2 ACCEPTED (2026-10-12)

**Kaynak:** kullanıcının ajanı, `7b280c196` (`wp/vw-w3-cfg`) → test dalında `b40a3702f`. Kod değişikliği yok (rapor + betik).

**CT canlı doğrulaması (yalnız okuma, `diten_personalization_dev`):**
- `workflow_templates`: `CRM-OBJ-RESP` (`b6cc495f…`) ve `CRM-VISIT-EVIDENCE-EXC` (`f28ec41d…`) **Status 2 (Published)**, `ActivePublishedVersionId` dolu.
- `workflow_template_versions` (ikisi de v1, `IsImmutable: true`):
  - `requestedObjectType` doğru (`crm.objection-response` / `crm.visit-evidence-exception`);
  - kökte `options.rejectCommentRequired: true`;
  - tek aşama, tek adım; aday `position:d0b6ce33…` (POS-MED-DIR) / `position:c1acdf12…` (POS-SALES-OPS);
  - SLA 4320 dk, yükseltme 2880 dk, aynı pozisyona.
- **Adayın çözülmesi:** `WorkflowCandidateResolver` her atama türünü kabul ediyor (Birincil, İkincil, Vekâleten). sema'nın POS-SALES-OPS ataması ikincil olduğu için bu önemliydi; görev ona düşer.

**Betik okuması:** `scripts/rbac/grant_w3_authoring_rbac_97c5.py`
- Dry-run varsayılan; GUID'ler subtype-4; tarih alanları yerel belgeden aynen kopyalanıyor; rol ya da izin oluşturmuyor.
- Marker `manual-grant-w3-authoring-rbac`; eklemeden sonra subtype denetimi yapıyor.
- `crm.objection-response.approve` verilmiyor (onay yalnız Workflow ile).
- Admin rol kimliği `6a315467…` = önceki betiklerdeki kimlik.

**Kabul edilen sapmalar:**
- KP-MLR-TR'deki ikinci tıbbi aday (POS-MED-SPC) eklenmedi; paket yalnız MED-DIR diyor.
- İstisna şablonunun süresi tıbbi adımdan alındı (4320 / 2880).

**Sıradaki:** kullanıcı betiği `--apply` ile çalıştırır ve yeniden giriş yapar → CT canlı E4: itiraz akışı + istisna akışı (sema onayı) + mockup görsel karşılaştırması.

## Canlı E4 — W3 yazım ekranları (CT, 2026-10-13)

**Kurulum:**
- Grant betiği kullanıcı tarafından `--apply` ile çalıştırıldı ve yeniden giriş yapıldı.
- Ayrı sekmede 1440 px; mockup `mockups-workspace` sunucusunda (5098) yan yana açıldı.
- Kayıt oluşturma kullanıcı onayıyla yapıldı (1, 2, 3).

**CT düzeltmeleri** (canlıda bulundu, kullanıcı onayıyla yapıldı):
1. `f8293c2f5` — itiraz sayfalarının L10n köprüsü hiç çalışmıyordu.
   - `_L10n.cshtml` içinde `Object.assign(` kapanış parantezi eksikti; Razor, `@Html.Raw(...)` parantezlerini yuttuğu için derleme geçti ama tarayıcı SyntaxError verdi.
   - Sonuç: üç sayfada tüm istemci etiketleri boştu (durum çipi ham "draft" gösteriyordu).
   - Koruma testi `RazorInlineScriptSyntaxTests` eklendi: `Views/` altındaki tüm satır içi köprü betiklerinin parantez dengesini denetliyor. Sabotajda kırmızı.
2. `9f37e0875` — itiraz listesinde Durum ve Son değişiklik sütunları 1440 px'te "+" altına katlanıyordu.
   - Soru sütunu artık iki satıra kaydırılıyor; Durum ve Son değişiklik sütunlarına öncelik verildi.
   - Test eklendi; sabotajda kırmızı.
   - Web **950/0**.

**Akış sonuçları:**

| Adım | Sonuç |
|---|---|
| İtiraz (ALMIBA — TUTUKON'da onaylı iddia yok) taslak → kaydet → onaya gönder (Admin) | ✅ "Onay bekliyor · Admin User…", Geri çek, SoD notu, geçmiş |
| Görev sema'ya (MED-DIR) düştü | ✅ `approval_tasks.AssigneeRef` = sema |
| Yorumsuz ret | ✅ "Neden girilmesi zorunludur." |
| Onay (sema) → CRM `approved`, `approvedBy` = sema | ✅ A3 onaylı: salt okunur, Emekliye ayır + Kopyasından yeni taslak |
| İstisna formu: zaten gevşek kurallar pasif | ✅ |
| İstisna formu: 31 gün | ✅ "En çok 30 gün; seçilen aralık 31 gün.", istek gitmedi |
| İstisna 14 Ekim + 13 Ekim → onaya gönder → sema (POS-SALES-OPS, ikincil atama) onayladı | ✅ planned / active |
| Etkin ayar (Admin User) | ✅ konum = Bilgi, kaynak İstisna; aktif istisna şeridi; diğer alanlar Kiracı |
| Arapça RTL: itiraz listesi + kanıt ayarları | ✅ etiketler AR, kategori BRD'den AR; kültür tr'ye döndü |

**Görsel karşılaştırma:**
- A1 boş, A1 liste, A2 yeni / düzenle, A3 kendi kaydı (SoD), A3 onaylı, B3 yayınlanmış, C1, C1 etkin ayar, C4 liste, C4 yeni, C4 ayrıntı, AR A1, AR C2 → uyumlu.
- Bilinen ve kabul edilen farklar:
  - süzgeçler Golden Slim sağ panelinde;
  - B düzenleyicisi mevcut KP Studio çekmecesinde;
  - tüzel kişilik / iş birimi katmanları pasif;
  - temsilci arama kutusu fazladan;
  - ürün formu yok.
- Küçük açıklar (sonra toplu): A2 soru / yanıt örnek yer tutucuları yok; yeni kayıtta "Oluşturan —"; A3 başlığında "Taslak" çipi yok (yalnız "Onay bekliyor").
- **Platform (bizim değil):** Görev Merkezi bildiriminde `&#39;Onayla&#39;` çift kodlama.

**Test edilmedi** (kayıt ya da veri gerektiriyor):
- C3 değişiklik geçmişi (politika kaydetme);
- B1 düzenlenebilir sunum notları (taslak yol sürümü);
- A3 emekli;
- yetkisiz kullanıcı (UAS-001, birim testleri var).

**Test verisi** (kullanıcı isteğiyle iptal edilmedi):
- itiraz `c9ef2056…` (onaylı);
- istisnalar 13 Ekim (aktif) ve 14 Ekim (planlı), temsilci Admin User.

**Sonuç: W3 yazım ekranları canlıda kabul edildi → W3 tamam (katmanlar PLT-ORG-SEAT sonrası).**
