# WORK PACKAGE — WP-E2E-FIX-2 · Bilgi zinciri yazım ekranları: İçerik, Bilgi Yolu, Yolculuk (E2E bulguları)

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** [E2E TUTUKON](E2E-TUTUKON-content-to-visit-plan.md) bulguları E2-B1, E2-B3, içerik `Version` notu, E3-B1, E3-B3, E3-B4, E4-B1, E4-B2, E4-B3 · [yol haritası](ROADMAP-visit-planning.md) → E2E-FIX.
> - **Kullanıcı:** "test bulgularını paketleyebilirsin" (2026-10-07).
> - **Kapsam:** Web (Knowledge içerik formu, KnowledgePaths stüdyo / inceleme, ContentEngagementJourneys form / detay) + CRM (içerik güncellemede sürüm, bilgi yolu inceleme geri çekme kuralı, yolculuk yayın hata kodları).
> - **Kapsam dışı (backlog, kullanıcı "sonra konuşuruz"):** E2-B2 içerik onaysız yayın, E4-B4 yolculuk yayın SoD.
>
> **Çalışma yeri:** worktree `C:\tmp\e2e-fix-2`, dal `wp/e2e-fix-2`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
> **Paralel paketler:** WP-E2E-FIX-1 (ziyaret yürütme / planlama) ve WP-E2E-FIX-3 (segment / oyun / sıklık). `SharedResource.*.resx`'e dokunma.

## Bağlam (CT kod okuması, 2026-10-07)
Kısaltmalar: **Web** = `frontend/Diten.Web`, **CRM** = `services/Diten.CrmService/src`.

| Bulgu | Kök (dosya:satır) |
|---|---|
| E2-B1 ürün seçici 100 | İçerik formundaki Ürün açılırı sunucuda çiziliyor: `Web/Views/CRM/Knowledge/_Form.cshtml:318-321` ← `LoadGlobalProductOptionsAsync` (`Web/Controllers/CRM/KnowledgeController.cs:612-618`) tek sayfa `selector?pageSize=100`; MDM sınırı 100 (`GetGlobalProductSelectorValidator.cs:11`). Aramalı vekil hazır: `GET /CRM/Knowledge/api/global-product-options?search=&pageNumber=&pageSize=` (`KnowledgeController.cs:257-289`), tekil kimlik okuması `:272-277`; Select2 ajax örneği `wwwroot/assets/js/CRM/Knowledge/taxonomy.js:797-800`. |
| E2-B3 ham kodlar | `_Form.cshtml:44,52,60,75` `<option value="@x">@x</option>`; tür / durum / kaynak sözleşme sözlüğü (`KnowledgeController.cs:421-423`), diller sabit (`Models/CRM/KnowledgeViewModels.cs:87`). `KnowledgeIndex.tr.resx`'te değer anahtarı yok. |
| İçerik `Version` 0 | `UpdateKnowledgeContentHandler` (`CRM/Application/Features/Knowledge/Content/Handlers/KnowledgeContentCommandHandlers.cs:342-467`) `Version`'a dokunmuyor; depo düz replace (`Persistence/Repositories/KnowledgeRepositories.cs:76-80`). Doğru desen: `KnowledgePathRepository.cs:51-58` (`expectedVersion` filtresi + `+1`). |
| E3-B1 etiket | `wwwroot/assets/js/CRM/KnowledgePaths/workspace.js:163` `slot.required ? SlotRequired : SlotOptional`; `slot.required` yuvadaki öğelerden türetiliyor (`Controllers/CRM/KnowledgePathsController.Studio.cs:380`), boş yuva "isteğe bağlı". Engel sayımı `status === 'under'` (`workspace.js:83`) ← `count < step.MinSelection` (`KnowledgePathStudioReader.cs:121`). |
| E3-B3 son onay mesajı | `review.js:216` onayda her zaman `DecisionApproved` ("…Sıradaki adım Görev Merkezi'nde açılır", `KnowledgePathStudio.tr.resx:429`); model yalnız `currentStepName` taşıyor (`KnowledgePathsController.Review.cs:482`), son adım bilgisi yok. |
| E3-B4 geri çekme | Düğme `Views/CRM/KnowledgePaths/Workspace.cshtml:32-33`, `workspace-review.js:142` `canWithdrawReview`; bayrak `KnowledgePathsController.Review.cs:297` = `latestOpen && (canManage \|\| IsSubmitter)`. Sunucu: vekil yalnız Manage (`:54-56`), CRM `[HasPermission(Perms.Manage)]` (`Api/Controllers/CRM/KnowledgePathsController.cs:188-191`), `WithdrawKnowledgePathReviewHandler` (`KnowledgePathReviewHandlers.cs:227-270`) gönderen kontrolü yapmıyor. |
| E4-B1 "repeated" | `wwwroot/assets/js/CRM/ContentEngagementJourneys/form.js:96` ve `Views/CRM/ContentEngagementJourneys/Details.cshtml:155` `pathUsageCountInJourney > 1` (aynı yol iki aşamada) — `repeatable` bayrağıyla ilgisi yok; etiket "Tekrar" (`ContentEngagementJourneysIndex.tr.resx:82`) "tekrarlanabilir" diye okunuyor. Sayım `ContentEngagementJourneyMapper.cs:77-80` (`null`/`""` yol kodları eşit sayılıyor). |
| E4-B2 | Varsayılan: `form.js:176` `stage ? !!stage.isRequired : false`. V-J11 (ve V-J10) İngilizce sabit metin, kodsuz: `ContentEngagementJourneyCommandHandlers.cs:365-370`, `:382`; `details.js:15-18` sunucu metnini aynen gösteriyor. Onay: `details.js:22` `L.PublishConfirm \|\| L.AreYouSure`; `PublishConfirm` resx'te var (`tr.resx:74`) ama `Views/CRM/ContentEngagementJourneys/_IndexL10n.cshtml:6-16` listesinde yok → "Emin misiniz?" + global alt metin "Devam etmek istediğinize emin misiniz?". Listede eksik diğerleri: `IsRequired`, `Repeatable`, `NewVersionConfirm`, `ArchiveStageConfirm`, `BranchConditions` (`form.js:94-99,229`; `details.js:32`). |
| E4-B3 yol listesi | `form.js:69-73` yalnız `status=published&effectiveAt=…&includeArchived=false`; vekil sorguyu aynen geçiriyor (`ContentEngagementJourneysController.cs:235-237`); CRM liste ucu `subjectId`, `topicId`, `audienceProfileId`, `language` destekliyor (`Api/Controllers/CRM/KnowledgePathsController.cs:31-43`). |

Hata kodu → mesaj eşlemesi: yalnız `wwwroot/assets/js/CRM/KnowledgePaths/studio-common.js:30-85` (`ERRORS` + `errorText(body)`); yolculuk ve içerik betikleri ham sunucu metni gösteriyor.

## NE
### 1. İçerik formu (E2-B1, E2-B3, sürüm)
- **E2-B1:** Ürün alanı aramalı seçiciye döner (mevcut `global-product-options` vekili + Select2 ajax, `taxonomy.js` deseni). Düzenlemede kayıtlı ürün tekil okuma ile ad + kodla görünür. Sunucuda 100'lük ön yükleme kalkar.
- **E2-B3:** tür / durum / kaynak / dil seçenekleri yerelleştirilmiş etiketle (değer = kod, metin = etiket). Dil adları için mevcut bir dil sözlüğü / kültür adı deseni varsa onu kullan.
- **Sürüm:** içerik güncellemesi `Version`'ı artırır ve okunan sürüme göre koşullu yazar (`KnowledgePathRepository` deseni). Eşzamanlı yazma (filtre tutmazsa) → 409 `concurrency_conflict` (mevcut CRM kodu varsa onu kullan). **Komut imzası değişmez** (beklenen sürüm handler'ın okuduğu kayıttan gelir); Web / mobil sözleşmesi değişmez.

### 2. Bilgi Yolu (E3-B1, E3-B3, E3-B4)
- **E3-B1:** yuva etiketi yolun adım kuralından: `MinSelection > 0` → "Zorunlu", değilse "İsteğe bağlı" — boşken de dolarken de aynı. Engel sayımı değişmez.
- **E3-B3:** inceleme modeline "son adım mı" (ya da kalan adım sayısı) eklenir; son onayda mesaj "Onay kaydedildi. İnceleme tamamlandı." (7 dil), diğerlerinde bugünkü mesaj.
- **E3-B4 (CT kuralı):** incelemeyi **yalnız gönderen** geri çekebilir. Sunucu `WithdrawKnowledgePathReviewHandler` gönderen değilse 403 `withdraw_not_submitter`; Web düğmesi yalnız gönderende görünür. Manage izni yine gerekir (iki koşul birlikte). *Kullanıcı yöneticinin de geri çekebilmesini isterse bu madde düşer — CT raporunda ayrıca sorulur.*

### 3. Yolculuk (E4-B1, E4-B2, E4-B3)
- **E4-B1:** rozet anlamı korunur, adı düzeltilir: "Yol başka aşamada da kullanılıyor" (kısa: "Yol tekrarı"), tekrarlanabilir rozetinden görsel olarak ayrı. Boş yol kodu (`null`/`""`) sayıma girmez.
- **E4-B2:**
  - Yeni aşama formunda "Zorunlu" **açık** başlar (düzenlemede kayıtlı değer).
  - V-J10 / V-J11 (ve aynı handler'daki diğer yayın kuralları) **hata koduyla** döner (`errors: [code, message]`, ör. `journey_publish_requires_required_stage`); Web kod → yerelleştirilmiş mesaj (7 dil).
  - `PublishConfirm` ve listede eksik anahtarlar (`IsRequired`, `Repeatable`, `NewVersionConfirm`, `ArchiveStageConfirm`, `BranchConditions`) `_IndexL10n.cshtml` listesine eklenir; onay penceresinde tek, anlamlı soru (alt metin tekrar etmez).
- **E4-B3:** aşamanın önerilen yol listesi yolculuğun **konusuyla** (subjectId) süzülür; yolculuğun dili varsa dil de. Konusuz yolculukta bugünkü gibi tüm yayımlanmış yollar + bilgi notu.

## KORU / YAPMA
- **Yeni yazma komutu YOK** (AUD-001 26 sabit). Değişen: mevcut güncelleme / geri çekme / yayın handler'larında kural + hata kodu.
- E2-B2 ve E4-B4'e dokunma (backlog).
- Ekran tasarımı değişmez (yalnız etiket, seçici türü, rozet adı).
- Yeni metinler **7 dil**; TR diakritikli; `SharedResource`'a dokunma. Seed / grant / göç YOK; `E2E-TUT-` kayıtları silinmez.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
Web (699/0), CRM Application (2303/0/5; PII flake), mimari (38/1; **26 sabit**). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. İçerik formu ürün alanı aramalı vekili kullanır; sunucu 100'lük listeyi artık yüklemez (Web testi / kaynak testi).
2. İçerik seçenekleri yerelleştirilmiş etiket taşır; 7 dilde anahtar var.
3. İçerik güncellemesi `Version`'ı 1 artırır; araya giren yazma → 409.
4. Yuva etiketi `MinSelection > 0` → Zorunlu (boş ve dolu yuvada aynı).
5. Son inceleme adımında onay mesajı "tamamlandı"; ara adımda bugünkü mesaj.
6. Gönderen olmayan yönetici geri çekemez (403 `withdraw_not_submitter`), gönderen çekebilir; Web bayrağı yalnız gönderende true.
7. "Yol tekrarı" sayımı boş yol kodunu saymaz.
8. Yolculuk yayını zorunlu aşamasızsa hata kodu döner; Web TR mesaj gösterir.
9. Aşama yol listesi isteği `subjectId` taşır.
10. `_IndexL10n` listesi `form.js` / `details.js`'te kullanılan tüm anahtarları içerir (kaynak testi).

**Sabotaj (kırmızı kanıtla, geri al):**
1. Geri çekmede gönderen kontrolünü kaldır → test 6 kırmızı.
2. İçerik sürüm artışını kaldır → test 3 kırmızı.

### E4 (CT, fleet; ayrı sekme; salt okuma ağırlıklı)
- İçerik formunda "TUTUKON" aranıp bulunuyor (kaydetmeden).
- Açılırlar TR etiketli.
- `KP-2026-269A07` stüdyosunda yuva etiketleri tutarlı.
- `CEJ-2026-8AD806` ayrıntısında "Yol tekrarı" rozeti yalnız aynı yolu paylaşan aşamalarda; yeni aşama formunda Zorunlu açık; önerilen yol listesinde yalnız TUTUKON yolları.
- Geri çekme ve yayın hata mesajı: test kaydında, kullanıcı onayıyla.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-E2E-FIX-2 · Bilgi zinciri yazım ekranları: İçerik, Bilgi Yolu, Yolculuk (E2E bulguları)
Repository: C:\tmp\e2e-fix-2 (worktree) · Branch: wp/e2e-fix-2 · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-E2E-FIX-2-knowledge-chain-authoring-screens.md — önce tamamını oku (Bağlam tablosundaki dosya:satır kanıtları CT okumasıdır, doğrula). Ayrıca: …/E2E-TUTUKON-content-to-visit-plan.md (§E2–E4) · frontend/Diten.Web/Controllers/CRM/{KnowledgeController,KnowledgePathsController.*,ContentEngagementJourneysController}.cs · frontend/Diten.Web/wwwroot/assets/js/CRM/{Knowledge,KnowledgePaths,ContentEngagementJourneys}/** · frontend/Diten.Web/Views/CRM/{Knowledge,KnowledgePaths,ContentEngagementJourneys}/** · services/Diten.CrmService/src/**/Features/Knowledge/** · .antigravity/rules/audit-trail-standard.md.

NE:
(1) İçerik — E2-B1 ürün alanı aramalı seçici (mevcut global-product-options vekili + Select2 ajax, taxonomy.js deseni; düzenlemede kayıtlı ürün adıyla); E2-B3 tür/durum/kaynak/dil yerelleştirilmiş etiket; güncelleme Version'ı artırır + okunan sürüme koşullu yazar (KnowledgePathRepository deseni), çakışma 409, komut imzası değişmez.
(2) Bilgi Yolu — E3-B1 yuva etiketi MinSelection>0'dan; E3-B3 son onayda "inceleme tamamlandı" mesajı (modele son-adım bilgisi); E3-B4 geri çekme yalnız gönderen (sunucu 403 withdraw_not_submitter + Web düğmesi), Manage izni yine gerekir.
(3) Yolculuk — E4-B1 rozet "Yol tekrarı" (repeatable'dan ayrı; boş yol kodu sayılmaz); E4-B2 yeni aşama Zorunlu açık başlar, yayın kuralları (V-J10/V-J11…) hata koduyla + Web TR mesaj, _IndexL10n eksik anahtarlar (PublishConfirm, IsRequired, Repeatable, NewVersionConfirm, ArchiveStageConfirm, BranchConditions), tek anlamlı onay sorusu; E4-B3 önerilen yol listesi yolculuğun subjectId (+dil) ile süzülür.
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (AUD-001 26 sabit); E2-B2 / E4-B4'e DOKUNMA (backlog); tasarım değişmez; yeni metinler 7 dil (TR diakritik), SharedResource'a dokunma; seed/grant/göç YOK; E2E-TUT kayıtları silinmez.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — Web (699/0) · CRM Application (2303/0/5, PII flake) · mimari (38/1, 26 SABİT); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–10. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "fix(crm,web): WP-E2E-FIX-2 — knowledge chain authoring fixes (product search, labels, content version, path slot/review, journey stage/publish)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), yeni hata kodları listesi, E3-B4 kural değişikliğinin etkisi (kim artık geri çekemiyor). §22 TÜRKÇE. K13.
```
