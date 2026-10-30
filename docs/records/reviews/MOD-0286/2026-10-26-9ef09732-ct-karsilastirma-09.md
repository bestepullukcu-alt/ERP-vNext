# MOD-0286 — Control Tower karşılaştırması 09 (Codex inceleme 09, SOP §20.4-4)

- Hedef: paket rev 09 @ 35c9fe94a (sha256 `9ef09732…`). İnceleme: Codex, **UYGUN DEĞİL**, 3 ENGEL (F-19, F-20, F-21) + 1 ORTA (F-22). Bulgu dosyası `2026-10-26-9ef09732.md` (Codex'in metni olduğu gibi; `parse_findings` kuru koşusu: 4 bulgu, 3 açık ENGEL, sorun 0).
- Beyan: "verilemiyor"; sahip kararı `beyan-yok` uygulanır.
- Daraltmanın geri kalanını inceleyen doğruladı: 22 zorunlu bölüm, UIC 32 / RC 24 / CI 15 tekil, 20 AC, dilim başlıkları, `verifier_full.tsv` sayıları (152 sayfa, 1182 anahtar, FU-4 63 sayfa), SharedResource sayıları, D-9'un yedi atfı. Bulgular yalnız DL-3 tasarımına ait.

## CT karşılaştırması (CT ölçümü @ 35c9fe94a)

- **F-19 — kabul.** Paket `:314` `git diff --name-only --diff-filter=ACMR`: `D` (silme) yok; yeniden adlandırmada eski yol da eşlenmiyor. Yalnız `_IndexL10n.cshtml`, `index.l10n.js` ya da bir dil `.resx`'i silen PR sayfayı seçmez; doğrulayıcı bu dosyaların yokluğunu hata sayar.
- **F-20 — kabul.** Paket `:330-332` CI-2 ve CI-4…CI-10 için anahtar ayrıntısı = dosya adı; aynı dosyaya ikinci ihlal anahtarı değiştirmez, `TABAN` sayılır. `:496`'daki "ikinci kırmızı yakalanır" iddiası yalnız CI-3 için doğru. AC-18 sabotajı önceden temiz bir dosyada; aynı dosyaya ikinci ihlal senaryosu yok.
- **F-21 — kabul.** Paket `:439-440` "until then AC-17 is proven by a local run_record run … (AC-17 allows it)"; AC-17 CI kanıtını "once the workflow exists" koşuluyla istiyor. Workflow hiç gelmeden P1 kabul edilebilir — karar kaydı daraltma-01 Karar 1 ("CI'da koşar") ile çelişir. Yerel kanıt ara teslim; nihai kabul CI işinin sürüme bağlı kaydına bağlanmalı. Not: bu, P1 kabulünü CT-Setup'ın workflow teslimine bağlar (sahiplik notu zaten böyle).
- **F-22 — kabul.** Paket `:348` tetikleyici üç ağaç + `PageHelpCatalog.cs`; `:369` CI-12 `platform-search.{en,tr}.json` okuyor ama bu dosyalar tetikleyicide ve seçimde yok.
- Satır numarası notu: inceleyenin AC-17 için verdiği `:536` CT ölçümünde AC-16 satırı; AC-17 bir satır aşağıda. Test Expectations `:578` atfı da bir satır kaymış görünüyor. İçerik doğru; bulguyu etkilemiyor.
- İtiraz yok. Kaçırılan yok.

## Sonraki adım

Rev 10 (paket yazarı): F-19 silme ve yeniden adlandırma eşlemesi + silme sabotajı; F-20 ihlal örneği parmak izi (ör. satır içeriği özeti) + aynı dosyaya ikinci ihlal sabotajı AC-18'e; F-21 AC-17 ve DL-3 nihai kabulü CI işinin kaydına bağlanır, yerel kayıt ara teslim; F-22 `platform-search.*.json` tetikleyiciye ve CI-12 eşlemesine. Sonra inceleme 10. İnceleme satırı 09 sahibin sözüyle.
