# MOD-0230 — bağımsız inceleme 02: Control Tower karşılaştırması (taslak, commit edilmedi)

> SOP §20.4-4: CT inceleme sonucunu paket ve kodla karşılaştırır, itirazlarını kaydeder, sonucu körü körüne kopyalamaz.
> Bu not karar, onay, paket değişikliği ya da dağıtım izni değildir. İnceleyenin metni
> `docs/records/reviews/MOD-0230/2026-10-21-b48500cf.md`'dedir (ham kopya `.ct-teslim/MOD-0230-review-02/codex-review-02-raw.txt`,
> sha256 `59d5a954a8b8da7a…`). Tek fark: sahibin yapıştırmasının sonundaki "21:19" satırı sohbet arayüzünün zaman
> damgasıdır, inceleme metni değildir; kayda alınmadı. İlk satırdaki `#` işareti yapıştırmada yoktu; eklenmedi.

## Künye

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `b48500cf10e5…`
  (73cce019a; `6b9018ca4`'te aynı bayt, `git diff --quiet` ile ölçüldü).
- İnceleme: Codex, oturum `01a15b88-15f5-7622-8277-43c3e4a5d265`; model "GPT-6" (varyant ölçülemedi); effort ölçülemedi.
- Araç: `pack_approval.parse_findings` → 5 bulgu, 0 sorun; açık ENGEL 3; ORTA 2 (sahip "PVG paket sahibi", son tarih
  2026-10-22). `değerlendirilen olumsuz incelemeler: 4adc6faf` satırı var.
- Karşılaştıran: PVG Control Tower (Claude Code, Opus 5.5). Ölçüm statik, HEAD `6b9018ca4`; derleme / test koşulmadı.

## 1. Bulgu bulgu karşılaştırma

| Bulgu | CT kararı | Ölçülen olgular (`yol:satır@6b9018ca4`) |
|---|---|---|
| 1 — okuma denetimi yok (ENGEL) | kabul | `.antigravity/rules/audit-trail-standard.md` §7 (satır 243'ten): okuma varsayılan olarak denetlenmez, ama "hasta-güvenliği verisi (farmakovijilans vakası) tekil olarak açılır" sınıfı denetlenir; paket bunu `Audited Events` tablosuna okuma satırı olarak yazar; okuma denetimi her zaman merkezi günlüğe (yol a / b) gider. Paketin Audited Events tablosunda yalnız dört yazma komutu var (bölüm satır 787, komut satırları 803–806). `PvgIntakeDraftApplicationService.cs:337` `GetDraftByIdAsync` içinde denetim çağrısı yok. CT'nin kendi karşılaştırma notu 01 ve paket yeniden yazımı bunu kaçırmıştı. |
| 2 — BG-4 başka modülün anahtarlarını kapsıyor (ENGEL) | kabul | BG-4 (satır 910–913) `git grep -n "pvg\." -- services/Diten.PvgService/src` ile tek aile ister; aynı ağaçta `Application/CaseProcessing/PvgCaseProcessingApplicationService.cs:7-10` MOD-0231'in sabitlerini taşır. MOD-0231 başka paketin (Repo Scope); değiştirmek XMC-001 ister. Ölçüt MOD-0230 yüzeyine (`src/*/RegPvBase/**`, `src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs`, denetleyici) daraltılmalı. |
| 3 — eski okumaya dayalı güncelleme (409) için yol sahipliği yok (ENGEL) | kabul | DL-2 yolları (satır 1246–1249) `Application/RegPvBase/` içinde yalnız "komut, sorgu, istek tipleri ve validator"; `IPvgIntakeDraftStore.cs:12` `ValueTask<bool> ReplaceAsync(` sürüm almaz; `PvgIntakeDraftApplicationService.cs:157-165` `false`'u `IntakeDraftNotFound` sayar (239 ve 321'de aynı çağrı); API ve MVC denetleyicisinde sürüm / etag alanı yok (grep boş). BG-12 bu yolları alan bir dilim olmadan ölçülemez. |
| 4 — kodda olmayan metot ve davranış olgu gibi yazılmış (ORTA) | kabul | Paket satır 630–633 ve 652 `IPvgWorkflowTransitionGate.ResolveRouteTargetAsync`'i anlatır; `IPvgWorkflowTransitionGate.cs` yalnız `EvaluateAsync` (satır 5) tanımlar, `ResolveRouteTargetAsync` depoda hiç geçmez. Satır 653 kanıt portunun `Pending` kaydettiğini söyler; `DenyAllEvidenceLinkPort.cs:10` yalnız `EvidenceLinkDenied` döndürür. G-12 doküman–kod ayrımını yazmıştı ama Validation Rules'taki bu cümleler kalmıştı. |
| 5 — liste yalnız ilk 100 kayıt (ORTA) | kabul | `wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js:28-35` `buildListUrl` her istekte `pageNumber=1`, `pageSize=100` koyar; API/uygulama tarafında `totalCount` / `recordsTotal` benzeri alan yok (grep boş). `data_mode: server` bildirimi ve tablo özniteliği bu davranışı düzeltmez. Bugünkü ekranda 100'ü aşan kayıt görünmez — işlevsel bir hata. |

**Önceki incelemenin yedi bulgusu:** inceleyen 1–6'yı kapandı, 7'yi açık (bu incelemenin 3. bulgusu) sayıyor. CT
karşılaştırma notu 01'in ek maddeleriyle tutarlı; itiraz yok.

**CT'nin üç itirazı:** üçü de kabul edildi (inceleyenin "itirazlar" satırı). 3. itirazda inceleyen, Gateway'in anonim
isteği her profilde durdurduğunun bu kayıtla kanıtlanmadığını ekliyor — doğru; üretim profili ölçülmedi (paket "Build-Gate
Gaps" Gateway notu).

## 2. CT'nin itirazları

- Esasa itiraz yok: beş bulgunun beşi de ölçülen olgularla doğrulandı ve seviyeleri yerinde (1–3 kabul ölçütlerini ya
  da GxP kuralını karşılanamaz bırakıyor; 4–5 yanlış olgu ve işlevsel hata).
- 2. bulgunun önerdiği düzeltme için not: ölçüt daraltılırken MOD-0231'in `pvg.mod0231.*` anahtarları için ayrı bir
  açık karar gerekir (MOD-0231 paketinin işi, XMC-001); MOD-0230 paketi onu yalnız "başka paketin konusu" diye anar.

## 3. İncelemenin kaçırdıkları ya da süreç notları

1. **Aynı inceleyen oturumu.** İnceleme 01 ve 02 aynı Codex oturumunda (`01a15b88…`) yapıldı. SOP §20.4-2 yalnız
   "paketi yazan oturumdan ayrı" der; bu ihlal değildir. Ama aynı oturum kendi önceki bulgularına bağlı kalabilir. Bir
   sonraki inceleme (03) için yeni bir Codex oturumu ya da Claude önerilir — öneri, kural değil.
2. **Okuma denetiminin bağımlılığı.** 1. bulgu, okuma denetiminin de merkezi yola (a / b) gitmesini ister; PVG'de bu yol
   OD-PVG-9'daki aynı K4 ortak ileticiye bağlıdır. Paket düzeltmesi okuma satırını OD-PVG-9'a bağlamalı; aksi halde
   yeni bir açık karar doğar. İnceleyen bağımlılığı istedi, hangi açık karara bağlanacağını söylemedi.
3. **5. bulgunun kapsamı.** Hata yalnız paketin değil bugünkü ekranın hatasıdır (100'ü aşan kayıt görünmez). Ekran
   build/test kapısında, üretimde değil; ama kabul kaydı olmayan WP-0230-07'nin "tamam" sayılmaması için bu not
   PVG-M1-02 envanterine de girmelidir.
4. **İnceleme ZIP'inde olmayan kayıt.** `REQ-PVG-CRM-01` (CRM'den advers olay alımı) 73cce019a'dan sonra yazıldı; ZIP'te
   yoktu. Paketin "PVG System Integration — Approved intake contract only" rol satırı sözleşmesiz duruyor; inceleyen bunu
   değerlendiremezdi. Bir sonraki revizyonda paket bu talebe atıf yapmalı (yalnız atıf; sözleşme ertelendi).

## 4. Sonuç ve sıradaki adım (öneri — onay değil)

- İnceleme 02 **UYGUN DEĞİL** haklı; 3 ENGEL + 2 ORTA açık. Bu sürüme onay satırı açılamaz (SOP §20.4-4).
- Sıradaki iş: bulgu dosyasını ve bu notu commit etmek; `pack_approval.py review-row` ile `reviews.tsv` satırı; paketin
  1–5 ve §3 madde 2–4 için revizyonu (`module-pack-author`, Opus · high — effort'un gerçekten high olduğu ölçülerek);
  ardından inceleme 03 (tercihen yeni bir inceleyen oturumu).
- Kod işi ve WP dağıtımı yok; operasyonel runtime kapısı kapalı kalır.
