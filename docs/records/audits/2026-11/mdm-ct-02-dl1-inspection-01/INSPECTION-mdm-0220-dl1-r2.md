---
record_id: MDM-CT-02-DL1-INSPECTION-MDM-0220-02
status: inceleme notu — dağıtım izni değil, onay değil
prepared_at_local: 2026-11-09
prepared_by: CT · Ürün ana verisi (chore/ct/mdm-ct-02 @f304329e8)
supersedes: INSPECTION-mdm-0220-dl1.md (aynı klasör; o not değiştirilmez)
wp: MDM-0220-DL1 (kuyruk mdm, execution/domains/master-data-management/work-packs/CT-QUEUE.tsv:3)
pack: execution/domains/master-data-management/module-packs/MOD-0220-legal-entity-foundation.md (sha256 1b1a068088a96249d18e87064d5563f832b6e8f0a5df9bb171baefd8bbb0cf02)
discovery: docs/records/audits/2026-11/mdm-ct-02-dl1-inspection-01/DISCOVERY-mod0220-r2.txt @a4e109322 (rapor-sha256 8c63ba65ce7ffb8e7f7264bdbd9f6cb1f115890fd3c731e53c991eea7be70d93)
prompt: execution/domains/master-data-management/work-packs/prompts/MDM-0220-DL1-v1.1.md
---

# MDM-0220-DL1 inceleme notu r2 — MOD-0220 §22 revizyonu (1b1a0680) ve yeni keşif

Salt okunur ölçüm. Derleme, test, servis koşulmadı. Kod okumaları `f304329e88600a6d3f9d7c486a6c740497b8b52d`
(bundan sonra `@f304329e8`) ağacından. r1 notunun (@13ca54a01) §2 kod ölçümleri burada yeniden ölçülmedi, yalnız
değişmedikleri gösterildi (§2).

## 1. Okunan kaynaklar

| Kaynak | Ölçü |
|---|---|
| Paket §22 ve "Teslim dilimleri" (satır 883–1098) | dosya sha256 `1b1a0680…` (ölçüldü, `shasum -a 256`) |
| `docs/records/reviews/MOD-0220/2026-11-06-1b1a0680.md` | aynı sha256 için UYGUN; açık iki düşük bulgu F-12, F-13 (sahip K3, 2026-11-13) |
| `execution/approvals/approvals.tsv:61` | `1b1a0680…`, approve, HIGH, bestepullukcu-alt, 2026-11-09, not "yalnız §22 · whole_pack=MDM-0220-DL1" |
| Keşif r2 (914 satır) | dosya sha256 `8a628cb0…`; satır 914 `rapor-sha256: 8c63ba65…` — `Keşif:` satırına yazılan değer `rapor-sha256`'dır; sorgu `Corporate Secretarial / Entity Management` (kayıt adı) |
| `docs/records/decisions/2026-11/mdm-mod0220-s22-delivery-owner-decision-01.md` | "Ana veri önce, ayrı" + "Yeniden uygula" (2026-11-06) |
| `docs/records/decisions/2026-11/mdm-mod0220-s22-hold-lift-owner-decision-01.md` | D84/4 ve D97/5 tutuşu kaldırıldı; geçerlik onay satırıyla birlikte |
| `docs/records/decisions/2026-11/mdm-mod0220-treasury-notice-01.md` | Hazine'ye D10/2 değişikliği ve D82/2 sorusu gönderildi (2026-11-09); cevap açık |
| `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` §3 | 13 okuma noktası; tipli okumalar `ReadFromJsonAsync` (Web varsayılanı), adla okumalar `JsonDocument` / `TryGetProperty` |
| `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs:418` | `builder.Services.AddControllers();` — özel JSON ayarı yok (`AddJsonOptions` / `JsonSerializerOptions` MdmService API katmanında yok, `git grep`) |
| `services/Diten.MdmService/tests/Diten.MdmService.Api.Tests/MdmHttpHost.cs:116, 401-408, 438` | `Factory` (WebApplicationFactory), `Client(token)`, `MdmHttpCollection` |
| Emsal `execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_accept.py`, `scripts/run_record.py` | tek koşu kanıtı; run_record temiz ağaç ister, commit atmaz |

## 2. Kod değişmedi (r1 ölçümleri geçerli)

`git diff --name-only 13ca54a01 f304329e8 -- services/ frontend/ tests/ .github/` yalnız MOD-0290-FU06 dosyalarını
gösterir (ProductItemSkuMasterManifestProvider.cs, Application.Tests csproj, iki ModuleRegistration testi). LegalEntity
dosyaları, işleyici, depo ve testler değişmedi: `LegalEntityModels.cs:100-106` (6 üyeli kayıt) ve `:238-245`
(`ToLookupDto`), `ValidateLegalEntityReferenceHandler.cs:20` aynen. `git grep -n LegalEntityLookupDto f304329e8 --
services/ frontend/ tests/`: yapım yalnız `LegalEntityModels.cs:239`; MdmService dışında yalnız iki açıklama satırı;
testlerde geçiş yok. Tek yazar (D9/4): `git log --all --since=2026-09-15` `LegalEntityModels.cs` ve
`LegalEntityReferenceValidationTests.cs` için boş. İşçi dalı `feature/mdm/mdm-0220-dl1-r1` ve worktree henüz yok.

## 3. Keşif r2 bulguları — karar

Raporda 6 BULGU var (satır 27–32). Talimattaki `Keşif kararı 1..6` bu tabloyla birebirdir.

| n | Bulgu (rapor satırı) | Karar | Gerekçe |
|---|---|---|---|
| 1 | blueprint MOD-0220 'Corporate Secretarial / Entity Management', veri nesnesi legal entities (27) | parent | İş bu modülün §22 DL-1 dilimi; yeni kimlik yok |
| 2 | registry MOD-0220 (ready-for-dev) (28) | parent | Aynı modülün kanonik kimliği |
| 3 | paket MOD-0220-legal-entity-foundation.md (29) | parent | Yetki paketi: §22 revizyonu `1b1a0680…`, inceleme UYGUN, onay satırı `approvals.tsv:61` |
| 4 | görev TRM-07 (30) | yeni | Hazine'nin MOD-0220 çatal çözümü kararı (Recovery SoR kalır, LE↔LE ilişki modeli eklenmez); tek ekin TRM-14 olduğunu söyler; kod yazmaz, bu iş ona dokunmaz |
| 5 | görev TRM-14 (31) | parent | Hazine'deki takip satırı; kod mdm kuyruğunda; TRM-14 kapanışı §22.6a takipleriyle ayrı |
| 6 | görev MDM-0220-DL1 (32) | parent | Bu işin kendi kuyruk satırı |

r1 keşfindeki 19 bulgunun çoğu (MOD-0289, MOD-0018-FU21/22, MOD-0290-FU03, CAND-CAP-0008-FU03, MOD-0362-FU03, gateway,
Q363/396/469/470/477/486) r2'de yok: sorgu kayıt adıyla koşuldu, eşleşme kümesi değişti. Gerekçeleri r1 notunda duruyor;
hiçbiri bu işle yol paylaşmıyordu.

## 4. r1 açık kayıtları — durum

| r1 | Durum |
|---|---|
| A-1 `whole_pack=TRM-14` ↔ WP `MDM-0220-DL1` | **kapandı** — yeni onay satırı `whole_pack=MDM-0220-DL1` |
| A-2 keşif sorgusu işe bağlanmıyor | **kapandı** — r2 sorgusu kayıt adı (`module-id-registry.md` MOD-0220) |
| A-3 §22.5 "MOD-0126 DL-1 ile aynı WP" ↔ ayrı MDM WP | **kapandı** — paket revizyonu MDM tarafını ayrı ve önce gelen WP olarak teslim ediyor (§22.5, Teslim dilimleri) |
| A-4 talimat incelemesi | açık (v1.1 için; HIGH) |
| §8 S1 sabotajının korunan dosyaya geçici yazımı | paket §22.4 AC-T2 sabotajı açıkça işleyiciyi değiştirir; v1.1'de yalnız kule betiği içinde, geri alma `git diff --exit-code` ile; incelemede okunur |

## 5. Paket revizyonunun talimata girişi

- **F-12 (DÜŞÜK):** `git show 393baf735:<paket> | shasum -a 256` = `91248a47…` (onaylı önceki sürüm). `git diff 393baf735
  f304329e8 -- <paket>`: değişen parçalar §22 başlık satırı (değişen bölümler listesinde yazmıyor), durum paragrafı,
  AC-T3 tüketici okuma biçimi maddesi, AC-T4, §22.5, §22.6a, Teslim dilimleri. §22.3, AC-T1, AC-T2, AC-T5 iki sürümde
  bayt bayt aynı (bölüm sınırlarıyla kesilip karşılaştırıldı). Talimat: işçi farkı okur, listeyi rapora yazar, bu dört
  parçada değişiklik görürse durur.
- **F-13 (DÜŞÜK):** tüketici okuma biçimi testinin gövdesi Api test sunucusundan HTTP ile alınır — gerçek `ToLookupDto`
  çıktısı, üretim MVC hattının ayarlarıyla (Program.cs:418). Süreç içi ayar gerekirse yalnız DI'dan
  (`IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>`). Tipli okuma tüketicilerin varsayılanıyla (`JsonSerializerDefaults.Web`,
  bilinmeyen üye yok sayılır) altı üyeli test-içi kayda; adla okuma `JsonDocument` / `TryGetProperty`.
- **AC-T3 sabotajı:** S5 (üye adı `Code` → `EntityCode`) ve S6 (`Referenceable` bool → string) `LegalEntityModels.cs`'te;
  yeni test dosyası DTO tipine başvurmadığı için kırmızı derleme hatası değil test sonucudur (yalnız Api.Tests koşar).
- **AC-T4:** MDM WP kabul şartı = yeni okuma biçimi testi + sabotajı + var olan MdmService testleri değişmeden;
  dört tüketici koşusu "regresyon dumanı; uyumluluk kanıtı değildir"; takip kısmı §22.6a.
- **Kanıt:** tek run_record koşusu (`docs/records/audits/2026-11/mdm-0220-dl1/accept-run-1`); betik S-83 gereği kulenin
  (`execution/domains/master-data-management/work-packs/scripts/mdm_0220_dl1_accept.py`, henüz yazılmadı; talimatta
  yer tutucu). TRX'ler `.gitignore`'da (`*.trx`, satır 32): kanıt commit'inde `git add -f -- <tam yol>` ile tek tek.

## 6. Risk sınıfı

**HIGH** (r1 §7 gerekçesi aynen): paylaşılan sözleşme (13 okuma noktası), Hazine ödeme uygunluğu, kiracı sınırı AC-T2'de.
§9.1 sinyalleri: `sözleşme`, `kiracı`.

## 7. Açık kalanlar (kule / sahip)

1. Kabul betiği `mdm_0220_dl1_accept.py` (S-83) ve kırmızı tabanı; talimattaki `YER-TUTUCU-KULE` sha256'sı.
2. Paket `1b1a0680…` ve onay satırı `approvals.tsv:61` henüz `origin/main`'de değil (main'deki paket `91248a47…`);
   Teslim dilimleri "o satır main'de değilken kod yazılmaz" der — dağıtımdan önce kule işi.
3. Talimat v1.1'in bağımsız incelemesi (HIGH) ve Expected HEAD güncellemesi (bugünkü `f304329e8` bu notu ve talimatı
   içermez).
4. Kuyruk satırı `CT-QUEUE.tsv:3` metni hâlâ "onay approvals.tsv 91248a47 (2026-10-30)" diyor — kule güncellemesi.
5. Sözleşme taslağının başlığı `x-queue: treasury-intake TRM-14` (§22.6a MDM satırı) — ayrı MDM işi.
6. Hazine'nin D82/2 cevabı açık; bu WP ona bağlı değil.
7. AC-T4 takip kısmı (§22.6a) ve MdmService tam koşusu (PR CI); TRM-14 kapanış şartları.
8. Belge kaymaları (r1 §9/8) değişmedi: §22.2 depo satır atfı, §4/§12 durum adı kayması (§22.6 kapsam dışı).
