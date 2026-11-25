# MOD-0230 — sıra koşulunda "ana dal" okuması (CT okuması, sahip kararı değil)

> Control Tower kaydı, 2026-11-02. `pvg-mod0230-kapali-kayit-04.md` KK-7'yi değiştirmez; tek PR düzenindeki anlamını yazar.

- **Soru.** KK-7 sıra koşulunu "`INTEGRATED` (bağımsız doğrulandı, ana dala birleşti)" diye tanımlar. WP-0230-DL10-R1 kuyrukta `INTEGRATED`; commit'i `9136f4702` CT dalında (`27b952a9d`), `origin/main`'de değil (`git merge-base --is-ancestor 9136f4702 origin/main` çıkış 1). DL-11 ve DL-5 bu koşulu bekler.
- **Okuma.** KK-7 2026-10-31'de yazıldı. Ertesi gün sahip dal onayına ve modül sonunda tek PR'a geçti (S-115, 2026-11-01). Kart "dalga başına tek PR" maddesi (S-76) de aynı yolu tanımlar: CT, işçi dalını kendi CT dalına birleştirir. Bu düzende birleştirme dalı CT dalıdır; main'e yalnız modül sonundaki PR girer. KK-7'deki "ana dal" bu yüzden **CT bütünleştirme dalı** okunur. Main okunursa kalan hiçbir dilim modül PR'ından önce başlayamaz ve modül PR'ı da bütün dilimleri bekler; döngü olur.
- **Emsal.** WP-0230-DL7A, DL7B-R1 ve DL7C CT dalında `INTEGRATED` yapıldı (`docs/records/audits/2026-11/pvg-wave7-integration-01/INTEGRATION.md`). Main'e sonra PR #329 ile girdiler.
- **Sınır.** Bu okuma sıra koşulu içindir. Kabul (KK-8) ve main'e giriş modül PR'ında ölçülür. Sahip aksini söylerse DL-11 dağıtımı durur ve bu kayıt geçersiz olur.
