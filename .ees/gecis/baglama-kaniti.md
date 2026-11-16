# Bağlama (EES-BAGLAMA-01) — kanıt

Tarih 2026-11-16. Kapsam: uygulama planı A1–A5, A7–A9. Yapılmayanlar: A6 (kökteki `AGENTS.md`'nin çekirdekten üretilmesi,
`git-safety` ve paket standardı değişiklikleri), A10 (35 tanımın arşivi), A11 (eski kart ve SOP'un arşivi), B1 (üretici
betiğin yeri), B2 (i18n betikleri). `.antigravity/**` değişikliği için sahibin onayı bu sohbette kendi sözüyle alındı.

## 1. Ne bağlandı

| Giriş noktası | Kaynak | Taşıdığı kartlar | Claude | Codex / Antigravity |
|---|---|---|---|---|
| `/ees-kule` becerisi | `.ees/beceriler/kule/SKILL.md` | çekirdek + kule | `.claude/skills/ees-kule/` (modele kapalı) | — |
| `/ees-hat` becerisi | `.ees/beceriler/hat/SKILL.md` | çekirdek + hat | `.claude/skills/ees-hat/` (modele kapalı) | — |
| `ees-modul-arastir`, `ees-kullanim-kilavuzu` | `.ees/beceriler/…` | — | `.claude/skills/…` (modele kapalı) | — |
| `ees-isci`, `ees-integration-agent`, `ees-belge-ajani`, `ees-is-analisti`, `ees-module-pack-author`, `ees-l10n-agent` | `.ees/ajanlar/…` | çekirdek + işçi | `.claude/agents/…` (model/efor seviyeden) | `.codex/agents/….toml` (model alanı yok: Codex eşlemesi doğrulanmadı) |
| `ees-denetci`, `ees-security-agent` | `.ees/ajanlar/…` | çekirdek + denetçi | `.claude/agents/…` (yazma araçları kapalı) | `.codex/agents/….toml` (salt-okunur kip alanı doğrulanmadı; salt-okunurluk yalnız metinde) |

Pilotun her ajanı ve becerisi `ees-` öneklidir (önek geçişte kalkar): eski kural metni `isci`, `integration-agent`,
`module-pack-author` gibi adları anıyor, eski bir oturum öneksiz yeni bir ajanı kendi seçebilirdi (ikinci kör kontrol, bulgu
1–2). Yeni beceriler Codex/Antigravity beceri klasörüne (`.agents/skills/`) konmadı: orada modelin kendiliğinden çağırmasını
kapatan bir ayar doğrulanmadı (bulgu 3); pilot Claude'da koşar. `resx-check` eski kaynağından kullanılır. Alt ajanın çekirdeği kendiliğinden yükleyip yüklemediği **ölçülmedi**; ölçüm gerekmedi, çünkü yeni çekirdek bu
adımda kendiliğinden yüklenen bir yere (`.claude/rules/`) konamaz — mevcut oturumları etkilerdi. Bu yüzden çekirdek her yeni
ajan ve becerinin gövdesinin başında.

## 2. Mevcut oturumlar etkilenmiyor (ölçüm)

- Bağlamadan önce alınan sha256 tabanı (16 dosya: eski 13 bağdaştırıcı + `AGENTS.md`, `CLAUDE.md`, `.claude/settings.json`)
  commit'ten sonra `shasum -a 256 -c` ile 16/16 OK.
- `git diff 6359464e6..5d49bdf02` bu 16 dosyada ve `.claude/rules/`, `.agents/rules/` klasörlerinde boş. Kendiliğinden
  yüklenen kural klasörlerinin içeriği aynı: `.claude/rules/` = `agents-core.md`, `localization-standard.md`;
  `.agents/rules/` = `localization-standard.md`.
- Bekçi testi: hiçbir `.ees/` kaynağı (ya da kartı) `.claude/rules/`, `.agents/rules/`, `AGENTS.md`, `CLAUDE.md`'ye üretilemez;
  eski 7 öğe eski kaynağında kalır (`test_sync_adapters_ees.PilotBoundary`).
- **Değişen tek şey (dosya değil, liste):** eski bir Claude oturumunun ajan listesinde 8 yeni ad ve açıklaması görünür; Codex
  oturumunun ajan listesi de büyür (beceri listesi büyümez). Ajan adları `ees-` önekli ve açıklamaları "YALNIZ yeni akışın
  pilot hattı başlatır; eski kule ve hatlar KULLANMAZ" der. Yeni beceriler Claude'da `disable-model-invocation: true` taşır,
  yani yalnız kişi `/ees-kule` ya da `/ees-hat` yazınca açılır. Ajanların listeden gizlenmesinin araçta bir yolu bilinmiyor; eski bir kulenin bir yeni ajanı kendi
  seçmesi açıklama sayesinde düşük olasılıklıdır ama sıfır değildir (**ölçülmedi**).

## 3. Testler

| Komut | Sonuç |
|---|---|
| `python3 .antigravity/scripts/sync_adapters.py --check` | `OK 33 adapter(s) match their sources` |
| `python3 -m unittest scripts/tests/test_sync_adapters_ees.py` (yeni, 18 test) | OK |
| `python3 -m unittest scripts/tests/test_sync_adapters_budget.py` (59 test) | OK (133 sn) |
| `dotnet test tests/architecture/…ArchitectureTests.csproj` (belge yolu bekçisi `.ees/adapters.json`'u da tarar) | 231/231 |

## 4. Bozma kanıtları (her biri geri alındı, sha256 ile ölçüldü; son durum `OK 33`)

| # | Bozma | Sonuç |
|---|---|---|
| S1 | İşçi kartı 63 satır (sınır 60) | `--check` çıkış 1: `OVERLINES .ees/kartlar/isci.md has 63 lines; limit 60` (+ kartı taşıyan bağdaştırıcılarda DRIFT) |
| S2 | Üretilmiş `.claude/agents/ees-isci.md` elle düzenlendi | çıkış 1: `DRIFT .claude/agents/ees-isci.md` |
| S3 | Çekirdek kart değişti, yeniden üretilmedi | çıkış 1: çekirdeği taşıyan 18 bağdaştırıcının hepsi DRIFT |
| S4 | Ajan kaynağında `seviye: orta` | çıkış 2: `unknown seviye 'orta'` |
| S5 | `.ees` kaynağı `.claude/rules/`'a hedeflendi | pilot sınırı testi kırmızı: `ees-isci -> .claude/rules/ees-cekirdek.md` |
| S6 | Üreticiden satır sınırı denetimi silindi | `test_too_many_lines_in_a_card_fails` kırmızı |
| S7 | Üretici kartları gövdeye eklemiyor | `--check` çıkış 1, bütün kartlı bağdaştırıcılar DRIFT |
| S8 | Eski `control-tower` öğesinin kaynağı `.ees`'e çevrildi | `test_old_adapters_keep_their_old_source` kırmızı |
| S9 | Yeni bir beceri modele açıldı | `test_new_skills_are_never_invoked_by_the_model_in_claude` kırmızı |
| S10 | Kaynakta `kart: denetci`, listede işçi kartı | `--check` çıkış 2: kart alanı listeyle çelişiyor |
| S11 | `ees-hat` becerisi `.agents/skills/`'e hedeflendi | pilot sınırı testi kırmızı: `ees-hat -> .agents/skills/ees-hat/SKILL.md` |
| S12 | Pilot ajanı öneksiz adla (`.claude/agents/isci.md`) bağlandı | `test_every_pilot_entry_point_has_the_ees_prefix` kırmızı |

Betik: `bozma.py` (çalışma klasöründe, depoya girmedi); her bozmayı uygular, sınar, dosyayı yedeğinden geri koyar.

## 5. Windows

agent-adapters CI Windows'ta `python …sync_adapters.py --check` koşuyor. Yeni kod satır sonlarını okurken CRLF'yi LF'ye
çeviriyor (kart birleştirme `read_text`, satır sınırı `read_bytes().replace(b"\r\n", b"\n")`); bunun testi
`test_line_count_ignores_windows_line_ends`. Windows'ta koşturulmadı (bu makinede Windows yok); CI ilk koşusunda görülecek.

## 6. Bilinen eskimiş atıf

`AGENTS.md` §6.1 hâlâ `.antigravity/adapters.json` diyor (bağlantı değil, kod metni; ölü bağlantı denetimi etkilenmez).
`AGENTS.md` bu adımda değişmedi (mevcut oturumları etkilemesin); A6'da düzeltilecek.

## 7. Kör kontroller

**Kör kontrol 1** (kartlar, ilk adım): `kor-kontrol.md`.

**Kör kontrol 2** (bu bağlama; Opus, yüksek efor; fark 6359464e6..5d49bdf02): mevcut oturumlar etkilenmiyor (16 dosya üç
noktada aynı, kendi sha256 ölçümü), yeni giriş noktaları doğru kartı doğru sırada yüklüyor, model alanları seviyeye uygun,
CRLF benzetiminde `--check` aynı. Kendi 6 bellek-içi bozmasında her biri bir testi kırdı. 9 önemli, 7 küçük bulgu:

| No | Bulgu | Ne yapıldı |
|---|---|---|
| 1–2 | `integration-agent` ve `isci` eski kural metnindeki adlarla aynı; eski oturum seçebilir | pilotun bütün ajan ve becerileri `ees-` önekli; bekçi testi |
| 3 | Codex/Antigravity yeni becerileri kendiliğinden çağırabilir | `.agents/skills/`'e konmadı; bekçi testi |
| 4 | Kartlar eski ajan adlarını kullanıyordu | kartlarda `ees-` adları (yalnız ad; kural metni değişmedi) |
| 5 | Giriş noktasının yüklemediği satır kimliklerine atıf (K16, K18, M2, H19, H3, K11; D-12/D-25 açıklamasız) | atıflar yerinde yazıldı; D-12/D-25 "DİTEN kararı" diye açıklandı; her giriş noktasında çözümsüz atıf kalmadığı betikle ölçüldü |
| 6 | Pilot oturum `AGENTS.md` Çekirdeğini de yüklüyor: kural 8 (her push/PR açık onay) ↔ Ç23 | `ees-kule` ve `ees-hat` gövdesinde açık hüküm: pilotta dilimin paket onayı o dilim için push/PR'ın açık onayıdır (sahip kararı); başka çelişkide sor. `AGENTS.md` A6'da |
| 7 | "En üst seviye" M2 ile "yüksek riskte bir üst" belirsiz | K5: "Üst+, seviye tablosunun üstü" |
| 8 | Güvenlik ajanı standart seviyede | `seviye: ust` |
| 9 | Belge ajanı kılavuz becerisini çağıramaz (modele kapalı) | gövdeye kaynak dosyanın yolu |
| 10 | Codex salt-okunur kip alanı yok | doğrulanmalı; yukarıdaki tabloda yazılı |
| 11 | İ4 gerekçesi eskidi (`.ees/` artık kod sahibi listesinde) | düzeltildi |
| 12 | `AGENTS.md` §6.1 ve model dosyası eski manifest yolunu anıyor | model dosyası düzeltildi; `AGENTS.md` A6'da |
| 13 | DRIFT iletisi bileşik kaynağı adlandırmıyor | ileti artık "kart + kart + kaynak" der |
| 14 | Kaynaktaki `kart:` alanı denetlenmiyor | üretici çelişkide hata verir; test + S10 |
| 15 | Eski kaynağa `seviye:` eklenirse eski kopya değişir; kanıt dosyası commit'te yoktu | `test_old_sources_carry_no_seviye`; bu dosya commit'lendi |
| 16 | ≤3 dokunuş hedefi soru sayımıyla belirsiz | H2: üç zorunlu dokunuş; hedef kodlamada hiç soru olmaması |

Düzeltmeler ikinci bir kör turla denetlenmedi (kalite hatası bir kez düzeltilir).

## 8. Codex turu 1 (UYGUN DEĞİL: 2 engel, 7 önemli) ve sahibin 16 Kasım kararları — tek düzeltme turu

| No | Codex bulgusu | Ne yapıldı | Kanıt |
|---|---|---|---|
| 1 ENGEL | Yasak hedefe üretim engellenmiyor; `./.ees/…` testi atlatıyor | Üreticide `validate_manifest`: yazmadan önce yol kanonik mi, `.ees` kaynağı (ya da kartı) her oturumun yüklediği / kendiliğinden çağırdığı yere gidiyor mu; testler bu gerçek doğrulamayı koşuyor; yazmadan durduğu uçtan uca test | S5, S11, S13, S18; `test_a_refused_manifest_writes_nothing` |
| 2 ENGEL | Paket yazarı ilk paketi hazırlayamıyor | İ1: onaylı paket şartı yalnız üretim koduna; plan, iş süreci ve paket taslağı onaydan önce (iş analisti, paket yazarı) | kart metni |
| 3 | H8 ↔ Ç6 | Ç6: ajan PR birleştirmez ve onaylamaz, ana dala ya da başkasının dalına birleştirmez; hattın kendi işçi dallarını kendi dilim dalına yerelde alması bu yasağa girmez (H8'de de yazılı) | kart metni |
| 4 | Yorumlu `seviye:` / `kart:` sessizce atlanıyor | Anahtar varsa yazım tam olmalı, yoksa hata | S14, S15; `test_a_commented_seviye_or_kart_line_fails` |
| 5 | Eşlemesiz araçta hedefin kendi model alanı kabul ediliyor | Seviyeli kaynakta hedef hiçbir model alanı yazamaz | S16; `test_an_unmapped_tool_target_may_not_write_its_own_model` |
| 6 | `ees-` denetimi gerçek `name` alanını görmüyor | Üretici id, gerçek `name` ve yolda öneki ve araç + tür başına ad tekliğini denetler; ajan açıklamalarında "/hat" → "/ees-hat" (16 yer) | S12, S17; iki test |
| 7 | Yerel commit izni pilotta çözülmemiş | `ees-kule` / `ees-hat` ilk commit ya da yüklemeden önce kişiden tek başlangıç cümlesini ister ve aynen kaydeder; yoksa kök kural 8 her adımda | beceri gövdeleri, İ1, Ç23, H13 |
| 8 | Son dilimin kılavuzu birleşmemiş kodu anlatamıyor | Kılavuz teslim edilen PR'ın doğrulanmış commit'indeki işlevleri anlatabilir (H23, belge ajanı, kılavuz becerisi) | kart metni |
| 9 | Belge ajanının işi Ç29'a takılıyor | Ç29: yasak elle operasyon kaydı/raporu; paketin ya da modül sonu listesinin istediği ürün belgeleri teslimattır | kart metni |

Sahip kararları (16 Kasım, dokuz karara "Evet, hepsi"): B-1 dilim başına PR, tek oturuşta incele + onayla + birleştir, iki
küçük dilim tek PR → H25, H16, şablonlar; B-2 paket taslağı onaydan önce taslak PR → Ç23, H4, PR şablonu; B-3 paket onayı
("paketi onaylıyorum") paket sürümünün bütün dilimleri için kod yazma izni, paket değişikliği PR'la → H5; B-4 kişi başı tek
hesap, asıl + yedek onaylayıcı → K14, H5; B-5 `business_process:` / `plan_row:`, süreç sayfası ilk paketle, kısa takip paketi
→ H4, paket yazarı ve iş analisti kaynakları (`.ees/standartlar/…` ve `.ees/sablonlar/paket.md`, `is-sureci.md` yalnız atıf;
onları başka parça yazıyor).

Bozma betiği bu turda 18 durum koştu (S1–S18); hepsi kırmızı, hepsi geri alındı, son durum `OK 33`.

## 9. Kör kontrol 3 (Opus, yüksek efor; Codex'in dokuz senaryosu + "paket yok, yeni hat" yürüyüşü) — tek düzeltme

Sonuç: 9 Codex bulgusunun 7'si kapandı; 3 ve 7 kök kural 8 karşısında kısmen açıktı. Yeni 14 bulgu (7 önemli):

| No | Bulgu | Ne yapıldı |
|---|---|---|
| Y1 | Beceri kuralları hedef yoluna değil `kind`'a bakıyordu | `/skills/` içeren her `.ees` hedefi modele kapalı olmalı, `.agents/skills/` her türde yasak; `kind` izin listesi; S19 + 2 test |
| Y2 | Sembolik bağla kart eski kaynak adıyla taşınabiliyordu | `read_text` yolun hiçbir parçasında sembolik bağ kabul etmez; test |
| Y3 | Tırnaklı anahtar sessiz geçiyordu | hata; S20 + test |
| Y4 | Önek yolun herhangi bir yerinde; `codex` ↔ `codex+antigravity` ayrı ad alanı; iç içe `CLAUDE.md` | önek giriş adında; araç ailesi tek ad alanı; hiçbir kopya `CLAUDE.md`/`AGENTS.md` adını taşıyamaz; 3 test |
| Y5 | İş analisti tanımı kulenin başlatmasına izin vermiyordu | açıklama: kule ya da pilot hat |
| Y6 | "Paketi onaylıyorum"dan dilim 1'e geçiş tanımsız | `ees-hat` gövdesinde dört adım: onaylayıcı paket PR'ını onaylar ve birleştirir; durum alanını ajan değiştirmez; eski onay defteri pilotta yok; dilim 1 dalı paket commit'inden |
| Y7 | Onaylayıcı PR'ı açan hesap olamaz | H25'e yazıldı; o durumda yedek onaylayıcı |
| Y8 | Onay yorumunun yazarı doğrulanamıyordu | açılış brifinde onaylayıcı hesapları; yorum yazarı eşlenir (H5, `ees-hat`) |
| Y9 | Başlangıç cümlesi yerel birleştirmeyi anmıyordu | cümleye eklendi |
| Y10 | Başlangıç onayının ara dönem yeri ve durum sayfasının yeri yoktu | PR açıklaması "Ajan çalıştırması"; durum sayfası bağlantısını kişi verir |
| Y11 | MVP planının PR yolu yoktu | süreç sayfası ilk paket PR'ında; plan ayrı PR, kişinin sözüyle |
| Y12 | H11 yerel dal silme ↔ Ç5 | Ç5 "uzak ya da başkasına ait dal"; H11 kendi yerel işçi dalları |
| Y13 | Dokunuş sayımı iki yerde farklıydı | H2 ve çalıştırma kaydı aynı tanım: dilim başına; cümle ve paket onayı ilk dilime |
| Y14 | Cümlenin işçi brifine konması "aktarma" gibi okunuyordu | beceri gövdesi: aynı hat içinde aynı onayın kullanımı, başka sohbete taşınmaz |

**Yönetime açık kalanlar (karar ya da başka parça):**
1. Paketin `status:` alanının onaydan sonra kimce değiştirileceği — paket standardını yazan parça tanımlamalı; o güne dek kişi
   onay PR'ında değiştirir. Kök `AGENTS.md` kural 7 "yalnız approved/ready-for-dev paket" diyor.
2. Ajanlar kişinin GitHub hesabıyla PR açıyor; o kişi kendi PR'ını onaylayamaz. Kişi başı tek hesap (B-4) ile pilotun her PR'ını
   yedek onaylayıcı onaylamak zorunda kalır. Kalıcı çözüm yöneticinin C3 maddesi (birleştirme yetkisi olmayan GitHub App).
3. Hat genelinde dokunuş: dilim başına ≤3 tutuyor; hattın ilk dilimi cümle + paket onayı + dilim oturuşu ile tam 3, soruya
   pay yok. Kule ve hat cümlesi ayrı oturumlarda iki kez istenebilir.
4. Ç1 (rehber paketin üstünde) ile kök kural 1 (paket en üstte) farklı; yön daha sıkı, zarar yok.

Bozma: 20 durum (S1–S20), hepsi kırmızı, hepsi geri alındı, son durum `OK 33`.

## 10. Codex turu 2 (UYGUN DEĞİL: 2 ENGEL, ikisi üreticide) — yapısal düzeltme

Ortak neden: doğrulama liste dosyasının nasıl yazıldığına bakıyordu, üreticinin gerçekte ne yazacağına değil. Düzeltme tek
mekanizma: `sync_adapters.py` `validate_outputs` — bütün kopyalar önce bellekte üretilir; her (gerçek hedef yolu, üretilmiş
metin) çifti, ilk yazmadan önce (ve `--check`'te de) denetlenir; biri uymazsa hiçbir dosya yazılmaz, çıkış 2.

| Bulgu | Kapandığı yer | Test (gerçek `main()`, geçici depo kopyası, yazma kipinde; ret sonrası bütün dosyalar aynı) |
|---|---|---|
| ENGEL 1 — hedef / üst dizin sembolik bağı | `validate_outputs` (a): hedefin ve her üst parçanın sembolik bağ olmaması; çözülmüş yolun depo ve yönetilen klasör içinde olması; yasak yerlerin çözülmüş yolda denetlenmesi | `test_a_target_that_is_a_symbolic_link…`, `test_a_parent_folder_that_is_a_symbolic_link…` |
| ENGEL 2 — denetlenen ad ≠ yazılan ad | `validate_outputs` (b): ad üretilmiş metinden (Markdown künyesi / TOML) okunur; `validate_manifest`: biçimin kullanmadığı `fields`/`frontmatter` hata | `test_a_name_masked_by_an_unused_fields_object…`, `test_the_generated_name_is_checked_not_the_manifest`, `test_a_generated_name_used_twice…` |
| ÖNEMLİ 3 — akış biçimli künye | `ees_key`: anahtar künyenin herhangi bir yerinde geçiyorsa tek desteklenen yazım (`anahtar: değer`, kendi satırında, bir kez) zorunlu | `test_a_flow_style_frontmatter…`, `test_a_flow_style_mapping_with_other_keys…`, `test_a_commented_field…` |
| ÖNEMLİ 4 — beceri hedefinde model | `render`: seviye her türde hedefin model alanını yasaklar, eşleme yalnız ajanda; `validate_outputs` (c): üretilmiş model alanları seviyenin verdiğiyle birebir (beceride hiç) | `test_a_skill_target_with_its_own_model…`, `test_an_unmapped_tool_target…`, `test_a_generated_model_that_differs_from_the_seviye…` |
| DÜŞÜK 5, 6 — `pack_contract_check` çıktı ve önbellek | güvenli çıktı kodlaması, yazdırma/boşaltma hataları yakalanır, `sys.dont_write_bytecode` | `OutputSafety` (ASCII çıkış 0, kapalı boru çıkış 0, `.pyc` yok) |
| DÜŞÜK 7 — taslak PR şablonu | `pr-aciklamasi.md`: taslak PR'da "Başlangıç onayı" satırı dolar, cümle ara dönemde orada aynen | — |

Bozma: çıktı doğrulaması çağrılmayınca 5 test, sembolik bağ denetimi kaldırılınca 2 test, üretilmiş ad denetimi
kaldırılınca 1 test kırmızı; hepsi geri alındı. Sembolik bağ denetimi kaldırılsa bile yazma, çözülmüş yol denetiminde
durur (iki katman). Eski 13 kopya yeni doğrulamadan geçer ve bayt bayt aynı üretilir (`test_the_old_outputs_are_generated_byte_identical`).

## 11. Kör kontrol 4 (son bağımsız bakış; Opus, yüksek efor) ve düzeltme

Codex'in iki turdaki ölçülmüş 9 senaryosunun hepsi (a–i) korundu: çıkış 2, hiçbir dosya değişmedi; eski 13 kopya yeni
doğrulamadan geçti, bayt bayt aynı. Yeni çeşitlerden 8 delik çıktı; hepsi aynı ilkeyle (okunamayan reddedilir, izin
listeleri üreticide sabit, her şey ilk yazmadan önce) kapatıldı:

| Çeşit | Delik | Ne yapıldı | Test (`test_sync_adapters_outputs.py`) |
|---|---|---|---|
| N8, N11 (engel), N9, N10 | Üretilmiş başlıkta okunamayan satır atlanıyordu; tırnaklı/boşluklu anahtar ya da satır sonlu değerle ad ve model gizlenebiliyordu | `generated_header`: anlaşılmayan her satır hata; `validate_manifest`: başlık anahtarı düz ad, değer satır sonu ve denetim karakteri içermez | `n8_n9`, `n10_n11`, `an_unreadable_generated_header_line` |
| N1, N1d | ASCII olmayan benzer harf (ſ) APFS'te `.ees`/`.claude/rules`'a katlanıyordu | yollar yalnız ASCII; yönetilen klasörler üreticide sabit liste | `n1`, `n1d_n13_n14` |
| N2, N3 | Sert bağ hedefi ya da kaynağı | sert bağ hedef ve kaynak reddi; yazma geçici adla + `os.replace` | `n2`, `n3` |
| N4, N15 | Dizin hedef, tanımsız dosya: yarım yazma | dizin hedef reddi; tanımsız dosya taraması ilk yazmadan önce ("nothing written") | `n4`, `n15` |
| N6 | `tool` etiketi denetlenmiyordu (eşleme düşüyor, ad alanı ayrılıyor) | `tool` sabit dört değer ve her biri yalnız kendi klasörüne yazar | `n6` |
| N7 | Aynı ad farklı türde | ad tekliği araç başına, türden bağımsız | `n7` |
| N12 | BOM künyeyi gizliyordu | BOM'lu kaynak reddi | `n12` |
| N13, N14 | Yönetilen klasör listeye eklenebiliyordu | sabit liste | `n1d_n13_n14` |

Bozma: sekiz yeni denetimin her biri tek tek kaldırıldı, her seferinde en az bir test kırmızı; hepsi geri alındı.
