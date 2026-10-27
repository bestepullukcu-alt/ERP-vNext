# MOD-0357 — bağımsız inceleme isteği 01 (SOP §20.4 / CT-APR-02; revizyon R1)

- Hedef: `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` @ sha256 `bd7b82cefc1f326e09cc174ee122ede72c4ebf60dda767fe727b4793468f2922` (revizyon R1, taslak)
- Kaynak commit: `6a1e3a855` (dal `chore/ct/mg-ct-01`; `origin/main` `a974171fd` dal-ilk birleştirmeyle alındı, `ed2acbf4c`; yerel, push edilmedi)
- Önceki sürüm: `826150ef4` (sha256 `3c53dd513b66792e3e18dabf34f1ffc642c086ba721c51d0823d2aa83fca8494`), `status: ready-for-dev`, S1–S12 teslim edildi. Bu paket için `execution/approvals/reviews.tsv`'de hiç inceleme satırı yok; bu, paketin ilk bağımsız incelemesidir.
- R1 = önceki sürüm + yalnız eklemeler (1432 satır ekleme, 0 silme; §1–§23 değişmedi): başlık bandında taslak notu; §24 (S13–S27, R1-D1…D14, alanlar, doğrulama, dilim sırası, RCS-001 notu, kayıt tutarsızlıkları, ertelenenler, açık sorular ve sahip cevapları, canlı kullanım hazırlığı, doğrulanmayanlar); §25 Denetlenen olaylar; §26 Teslim dilimleri (DL-1, DL-13…DL-27, dilim başına kanıt seviyesi, S-85).
- Sahip kararları bu sürümde: Q1–Q12, Q15, Q16 `hepsi-evet`; Q19–Q22 `dort-evet`; Q13, Q14 CT kararı (kaynaklı); Q17 açık (QA süreç sahibinin adı — yalnız katalog verisini bekletir); Q18 CT cevabı (yöneticinin kodlama notu R1 ile çelişmiyor).
- **CT ön denetimi (gönderilmeden önce):**
  - `git diff --check` temiz; dosyada `\r` yok (S-87).
  - `pack_approval.slices()` → DL-1, DL-13…DL-27; her dilim satırında gerekli kanıt seviyesi var.
  - Desen ölçümü (S-65): başlık yer tutucu deseni yöneticinin 46 katalog başlığıyla denendi. Dar desen `\{[A-Za-z]+\}` 8 başlığı reddediyordu (`{Team/Period}`, `{Budget Item}` …); genişletilmiş desen `\{[A-Za-z][A-Za-z /]{0,39}\}` 0 red. Paket §24.5 genişletilmiş deseni yazar.
  - `MTG-` dizgesi kodda zaten kullanılıyor (`frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js:9626`, `frontend/Diten.Web/tests/wcn-review-meeting-flow.test.js:53`) → toplantı numarası öneki `MTG` olamaz (Q12 cevabı).
  - Canlı ölçüm (sahibin oturumu, salt okunur, 2026-10-27): canlıda toplantı türü 0, görev türü 6, 22 aktif kullanıcının 17'sinde pozisyon var; Zaman Kaydı Ayarları API'si `reminderJobEnabled: false`.
  - `pack_approval.py check` koşulmadı (paket commit'lidir ama onay satırı yok; beklenen sonuç "onaylı değil").
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · Toplantılar oturumu · Yazan: `module-pack-author` alt ajanı (Opus · high istendi) · Tarih: 2026-10-27
- İnceleyen: sahip seçer — **Claude** (bu depoya erişimi olan, paketi yazmamış ayrı bir oturum; ZIP gerekmez) ya da **Codex** (depoya erişimi yok; CT ayrıca ZIP hazırlar). Model · effort: Opus · high (§17.1 r5 zor bağımsız denetim başlangıç profili); beyan verilemiyorsa "verilemiyor".
- Bulgu dosyası: `docs/records/reviews/MOD-0357/<YYYY-AA-GG>-bd7b82ce.md`; bulgular F-1'den.
- Sonra: ham sonuç commit'siz taslak olarak bu klasöre yazılır → CT her bulguyu paket ve kodla karşılaştırır (kabul / itiraz) → sahibin sözüyle bulgu dosyası commit → `review-row` → onay satırı (HIGH; ilk onay) → PR yalnız zorunlu olduğu için (onay/inceleme satırı, §7b (a)) sahibin sözüyle.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (Claude oturumu için; inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: MOD-0357 (Management Review & Cadence, ürün adı "Toplantılar") module pack revizyon R1 — bağımsız inceleme 01 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin, CT · Toplantılar oturumu da değilsin.
Yer: C:\dev\ERP-vNext\.claude\worktrees\mg-ct-01 (dal chore/ct/mg-ct-01, commit 6a1e3a855). Yalnız oku: dosya düzenleme, kod yazma, commit, push yok. Tek çıktın aşağıdaki bulgu metnidir.
Önce doğrula: `git rev-parse HEAD` 6a1e3a855 ile başlamalı ve `sha256sum execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` = bd7b82cefc1f326e09cc174ee122ede72c4ebf60dda767fe727b4793468f2922. Tutmuyorsa dur ve bildir.
Model · Effort (istenen): Opus · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortam vermiyorsa "verilemiyor" yaz, uydurma.

Bu paketin ilk bağımsız incelemesidir. R1 yalnız ekleme yapar: `git diff 826150ef4 6a1e3a855 -- <paket>` (1432 ekleme, 0 silme). §1–§23 (teslim edilmiş S1–S12) bağlam içindir; R1 ile çelişki ararken oku.
Önce oku: docs/records/reviews/MOD-0357/REVIEW-REQUEST-01.md (CT ön denetimi); paket §24–§26 tamamı; ADR-003 (docs/records/decisions/2026-09/ADR-003-meetings-are-mod-0357-now-and-live-in-platform.md); docs/roadmap/plans/post-closure-program-2026-10-12.md §5.5–§5.7 (bağlayıcı yönetici kararları); docs/roadmap/plans/meetings-mod0357-work-list-2026-10-27.md; .antigravity/rules/module-pack-standard.md, audit-trail-standard.md (AUD-001 §4.3–§4.4, §10 K1/K10), record-coding-standard.md (RCS-001), page-help-and-keyboard.md (UXH-001 §2, §5, §6), localization-standard.md, multi-tenancy.md, permission-key-standard.md; docs/guides/operations/control-tower-sop.md §20.4 ve §23 (E0…E5; `git show origin/main:` ile); kod: services/Diten.Platform/src/Diten.Platform.Domain/Entities/Meetings/**, Application/Features/Meetings/**, Application/Features/Tasks/Services/ReviewMeeting*.cs, Application/Features/WorkAggregation/Calendar/**, tests/architecture/audit-ledger/Diten.Platform.md, frontend/Diten.Web/wwwroot/assets/js/Meetings/**, Controllers/MeetingsController.cs.
Derleme ve test koşamazsın (istersen salt okunur `py -3 scripts/pack_approval.py` ve `git` komutları koşabilirsin); ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Kimlik ve kapsam: R1 yeni kimlik açıyor mu; kapsam ADR-003 ve §5.7 kararlarıyla uyumlu mu; kapsam dışı listesi gerçekten dışarıda mı (özellikle MOD-0024 Features/Tasks/** ve TaskItem'a dokunan bir şey var mı — XMC-001)?
- Model kararları R1-D1…D14: göç yok + "Unassigned" (D2), gizlilik tek yönlü (D5), Repeat üçüncü oluşturma yolu açmıyor mu (D7), yer tutucu deseni (D8, §24.5), hazırlık görevleri asla engel değil (D13, ADR-003 §4–§5) — savunulabilir ve kodla tutarlı mı?
- Yetki ve kiracı: yeni uçların hepsi var olan izinlere bağlı mı (PKS-001), kiracılar arası 404, UAS-001; S26'nın `platform.tasks.create` denetimi sunucuda mı?
- Gizlilik (S15): rapor, dışa aktarım, Görev Merkezi, takvim akışı, bildirim ve `.ics` hiçbir yerde başlık/gündem sızdırmıyor mu; BL-541 ile ilişkisi doğru kurulmuş mu?
- Denetim (AUD-001): §25 tablosu bütün yazma komutlarını kapsıyor mu; Q13 (AuditCategory.Meetings = 18) ve Q14 (yayın/düzeltme K2 sınıfı, bilinen borçta kalır, yol a'ya taşınmaz) kurala uygun mu; yeni K2 borcu doğuran bir dilim var mı?
- Ölçülebilirlik: her S13–S27 kabul ölçütü ölçülebilir mi; sabotaj kanıtı tarif edilmiş mi; 7 dil ve yardım metni (UXH-001) her sayfa değişikliğinde var mı?
- Dilim tablosu (§26): pack_approval.slices() kuralına uyuyor mu; her satırda tek, savunulabilir kanıt seviyesi var mı (SOP §23); bağımlılıklar (§24.7) döngüsüz ve sıra mantıklı mı?
- Kayıt tutarsızlıkları (§24.23) doğru ölçülmüş mü; paket başka bir yerde kendisiyle çelişiyor mu (başlık bandı "draft" vs künye "ready-for-dev" gibi)?
- Bulgu varsa F-1'den başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0357 — bağımsız inceleme
hedef: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md @ bd7b82cefc1f326e09cc174ee122ede72c4ebf60dda767fe727b4793468f2922
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
