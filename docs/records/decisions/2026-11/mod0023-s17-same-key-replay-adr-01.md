---
status: taslak — CT incelemesi ve sahip okuması bekliyor; bu belge yeni bir karar vermez, alınmış kararları kayda geçirir
decided: 2026-10-30 (O-S17-3), 2026-10-31 (S17-8); kapanış kaydı 2026-11-02
module: MOD-0023
work_packages: WF-S17-DL1, WF-S17-DL2 (Hazine isteği TRM-15)
decision_ids: O-S17-3 = (i) · S17-6, S17-8, O-S17-2 = (b) · D25/2-3 · D32/2-3
---

# MOD-0023 §17 — Aynı anahtar yanıtı: gövdeye bağlı tekrar, silmeme

> **Karar sahibi:** depo sahibi, MOD-0023 sahibi. Kararlar CT · İş akışı sohbetinde butonlu sorularla, sahibin kendi tıklamasıyla alındı;
> kayıtları `docs/records/decisions/2026-10/mod0023-s17-owner-decisions-01.md`…`-05.md`. Bu ADR onları bir araya getirir ve gerekçelerini
> dondurur; başka bir karar eklemez.
> **Sınır:** Bu kayıt push, PR, birleştirme ya da dağıtım izni değildir. TRM-15 bu işle kapanmaz (bkz. Sonuçlar).
> **Dayanak:** paket §17.9 → §17.8 → §17.7; `docs/records/audits/2026-10/mod0023-s17-inspection-01/` (INSPECTION, DL1-DOGRULAMA-01,
> DL2-DOGRULAMA-01/02, DL2-ISCI-RAPORU-01, DL2-TUKETICI-TABLOSU-01).

## Bağlam

Hazine servisi (MOD-0126, 0119, 0120, 0124) MOD-0023'te örnek başlatır ve geçiş kaydeder; anahtarları niyet başına belirleyicidir.
Bilinmeyen sonuçtan sonra uzlaştırması (a) **kendi** örneğini/geçişini anahtarla bulmak, (b) anahtarın sonsuza dek bağlı kalmasına güvenip
aynı anahtarla yeniden göndermek zorundadır. Kod incelemesi (INSPECTION.md, @4c08fc75a) bugünkü davranışın bunu sağlamadığını gösterdi:

- Bütün başlatmanın tekrarı olguya ve başlatana bakmadan 200 dönüyordu (`StartWorkflowInstanceHandler.cs:385-391` @4c08fc75a); `idempotentReplay` alanı yoktu.
- Sıralı geçiş tekrarı yalnız aktörü karşılaştırıyordu; aynı aktörün farklı gövdesi sessizce eski yanıtı alıyordu.
- İptal tekrarı hiçbir şeyi karşılaştırmıyordu: başka aktörün iptal tekrarı 200 alıyordu.
- Üç iş akışı kaydı tipinin devralınan bir silme yolu vardı (`RepositoryBase<T>.DeleteAsync`, yalnız `IsDeleted` yazar); anahtarın "sonsuza dek bağlı" kalması bu yola güveniyordu.
- Gecikmeli özdeş ikiz, kaydı yazılmış olsa bile "karar verilemez" ya da "sürüm bayat" reddi alıyordu ve bu ret, Hazine kuralına göre (§17.7/6) **kesin ret** sayılıyordu.

## Karar

1. **Gövdeye bağlı tekrar (D25/3, paket §17.9/7).** Aynı (görev, eylem, anahtar) için sıra: aktör (başkaysa 403 `WORKFLOW_ACTOR_DENIED`), gövde (farklıysa
   409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT`, yeni satır yok), kayıtlı yanıt (200 `isIdempotent`). Gövde = neden kodu, açıklama, kanıt, e-imza kimliği (yalnız Approve)
   ve RequestInfo hedefi. Approve, Reject, RequestInfo ve **Cancel** buna girer; Delegate girmez. Yeni kod, yeniden denenebilir `WORKFLOW_TRANSITION_CONFLICT`'ten bilerek ayrıdır.
2. **Başlatma (§17.7/5, D25/1).** Bir anahtarın her tekrarı, örnek bugün hangi durumda olursa olsun, ilk istenen olgularla karşılaştırılır; farklı olgu ya da başka başlatan
   409 `WORKFLOW_INSTANCE_START_CONFLICT`. Yanıta sonda, isteğe bağlı `idempotentReplay` eklenir (ilk yazım `false`, geri kalan `true`).
3. **O-S17-3 = (i) (kayıt S17-6, 2026-10-30).** Approve/Reject'te "bayat sürüm" ve "karar verilemez" retleri verilmeden önce (görev, eylem, anahtar) kaydı **bir kez daha** okunur;
   satır varsa aktöre ve gövdeye bağlı tekrar olarak yanıtlanır. Mutlu yol fazladan okuma yapmaz.
4. **RequestInfo hedefi ayrı alanda.** `WorkflowTransitionLog.TargetPrincipalId` eklenir (kırpılmış hedef ya da `""`; `null` = alan öncesi satır); `Comment` biçimi değişmez.
   Eski satırlarda hedef ayrı karşılaştırılmaz, yalnız `Comment` üzerinden bakılır.
5. **Yinelenen anahtar eşlemesi.** RequestInfo'nun benzersiz dizine (`ux_workflow_transition_logs_task_action_idempotency_active`) çarpan yazması, o satırı okuyup aktör- ve gövdeye bağlı tekrar olarak yanıtlanır.
6. **Silmeme (§17.7/2, D32/2, AC-W15).** `WorkflowInstance`, `ApprovalTask`, `WorkflowTransitionLog` silinmez, süresi dolmaz. Üç depo devralınan `DeleteAsync`'i yazmadan reddeder;
   `Diten.Platform.Common` değişmez; hiçbir arayüze silme üyesi eklenmez. Kişisel veri silme isteğinde satır silinmez, kişi alanları yerinde takma adlandırılır (D32/3; yöntem O-S17-1, açık).
7. **Silinmişi okuma.** `by-objects` varsayılanda silinmişi gizler; `includeDeleted=true` ile `isDeleted` işaretiyle döndürür. `history` bayrak almaz ve silinmiş örnekte 404 verir (**O-S17-2 = (b)**, S17-2).
   Yeni okuma üyesinin arayüz varsayılan gövdesi fırlatır.
8. **Dördüncü depo (S17-8, 2026-10-31).** `WorkflowStartRecoveryStore` bu iş için override almaz; mimari bekçi onu metin okuyarak kapalı tutar, kapatma paketin bir sonraki revizyonuna kalır.

## Alternatifler (paketteki reddedilenler ve seçilmeyenler)

| Alternatif | Neden seçilmedi |
|---|---|
| **O-S17-3 (ii):** Approve/Reject'te bugünkü 409'lar kalır; `WORKFLOW_TASK_INVALID_STATE` aynı anahtarla yeniden gönderimden sonra "bilinmiyor" sayılır (kural her tüketicide: Hazine, Tasks, Timesheet, WorkCenter, Web). | Paket önerisi (i): gecikmiş özdeş yeniden denemenin yanıtı üreticide **bir kez**, anahtar başına tek indeksli okumayla düzelir; her tüketicide kural yazmak yerine. Sahip (i)'i seçti (S17-6). |
| Eski satırlar için yalnız aktöre bağlı tekrar (gövde karşılaştırması yok). | Neden, açıklama ve kanıt karşılaştırmasını da düşürürdü (pakette açıkça "Rejected"). Seçilen: `null` hedef alanı varsa hedef ayrı karşılaştırılmaz, gövdenin geri kalanı karşılaştırılır. |
| Delegate'i AC-W14/AC-W17'ye katmak (gövde karşılaştırması paylaşılan yardımcının içinde). | Delegate'in çağıranları (Diten.Web `delegateTask`, WorkCenter dağıtıcısı) tüketici denetimine girerdi. Seçilen: gövde karşılaştırması eylem başına eklenir, Delegate'in tekrarı değişmez (pakette "Rejected"). |
| RequestInfo hedefini yalnız `Comment` üzerinden karşılaştırmak. | (hedef `P`, açıklama `not`) ile (hedef yok, açıklama `not target:P`) aynı `Comment`'i saklar; ikinci istek birincinin tekrarı sanılırdı. Ayrı alan seçildi. |
| Gövde çakışması için `WORKFLOW_TRANSITION_CONFLICT`'i yeniden kullanmak. | O kod yeniden denenebilir "bilinmiyor"dur (§17.7/6); gövdesi başka istek için anahtar bağlıdır. Ayrı kod şart (§17.9/7), kablo değeri testle sabitlendi (doğrulama 02, B-1). |
| `history`'ye `includeDeleted` eklemek (O-S17-2 (a)). | Politika "hiçbir şey silinmez"dir, bayrak savunmadır; (b) zaten yeniden gönderimi durdurur, daha az depo üyesi ister ve `history` yanıtını değiştirmez. |
| Yeni depo üyesine süzen ya da soyut varsayılan. | Süzen varsayılan, `GetAllForTenantAsync` üzerinden silinmişi sessizce atar ("bayrak yok sayıldı" hatası); soyut üye bütün test çiftlerini kırardı. Fırlatan varsayılan yüksek sesle başarısız olur. |
| `RepositoryBase<T>.DeleteAsync`'i `Platform.Common`'da kapatmak. | Başka modüller aynı tabanı kullanır; paket `Platform.Common`'a dokunmayı yasaklar. Üç türemiş depoda override seçildi. |
| Dördüncü depoya (`WorkflowStartRecoveryStore`) override yazmak. | Paketin izinli yolu değildi (S17-8); bekçiyle geçip sonra kapatma seçildi. Açık kayıt olarak kalır. |

## Sonuçlar

**Olumlu**
- Hazine uzlaştırması anahtarla kendi örneğini/geçişini bulur; farklı niyet aynı anahtarla bağlanmaya çalışılırsa 409 görür; silinmiş satır görürse yeniden göndermez.
- Başka aktörün iptal tekrarı artık 200 almaz (200 → 403).
- Silmeme, davranış testleri (gerçek Mongo) ve iki mimari bekçiyle korunur: `WorkflowRetentionGuardTests` (DL-1) ve `WorkflowRetentionSourceGuardTests` (DL-2).

**Tüketiciyi etkileyenler (ayrıntı: sürüm notu, tüketici tablosu)**
- Bütün başlatma tekrarı, olgu ya da başlatan farklıysa 200 yerine 409 alır. Yarım başlatmada aynı karşılaştırma DL-2'den önce de vardı (doğrulama 01, B-4).
- Aynı anahtar + başka gövde, kaydedilmiş 200 yerine 409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT` alır. Task Center anahtarı aktör-, metin- ve imzasızdır; ön yüzde yeni kod için eşleme yok (karar gerektirir).
- Genel uçtan gönderilen güvenilir başlatma tekrarı 200 yerine 409 alır (pakette yazılı değil; testle sabit).

**Bedeller ve kalanlar**
- Eşzamanlı çakışmada kaybeden gövdesi ne olursa olsun 409 `WORKFLOW_TRANSITION_CONFLICT`/503 alır; sürüm çiti gövdeleri ayıramaz. Cancel'ın geç ikizi değişmedi (409 `WORKFLOW_TASK_INVALID_STATE` mümkün).
- `idempotentReplay=true`, "ben yazmadım" kanıtı değildir (commit sonucu bilinmeyen ilk yazma da `true` alabilir; B-3, kabul edildi).
- Dördüncü depo devralınan silme yolunu taşır (S17-8). Bekçi metin okur, sözdizimi ağacı okumaz; bekçi başlığındaki kaçış yolları kalır (using takma adı, yansıma, `Features/Workflow` dışı Application yazımları).
- Eski satırlarda RequestInfo hedef çarpışması yakalanmaz (kabul edilen sınır).
- AC-W17 ve AC-W18 tüketici sahip kayıtları, AC-W3 MDM/Web gövde kanıtı bekliyor. **O-S17-1** (kişisel veri silme yöntemi) açık. **TRM-15 kapanmış değildir.**

## Gerileme değerlendirmesi

- Kiracı yalıtımı gevşemez: yeni okuma üyesi kiracı koşulunu korur; geçiş anahtar aramasındaki kiracı süzgecinin guard'ı DL-2 sonunda eklendi (8 durum; süzgeç kaldırılınca kırmızı — işçi sabotajı, CT yeniden koşmadı).
- Yetki gevşemez: hiçbir `[HasPermission]` ve rota değişmedi; `includeDeleted` yeni izin istemez (D32/6).
- Gevşeyen tek şey bekçinin kapsamıdır: metin okuyan bir bekçi, belirtilen kaçış yollarını görmez. Risk: dar; bugün üç tipe silme ya da işaret yazan üretim yolu bulunmadı (arama). Dördüncü depoya DL-2'ye kadar bir yol eklenseydi DL-1 bekçisi yakalamazdı; DL-2 bekçisi bunu kapattı.
- Geri alma maliyeti: gövde karşılaştırması ve başlatma karşılaştırması tüketici sözleşmesini değiştirdi; geri alınırsa tüketiciler 409'a göre yazılmış olabilir (Tasks, Inventory, CRM, MDM, Web çağıranları 409'u okur). Bu yüzden geri almak da tüketici denetimi ister.
