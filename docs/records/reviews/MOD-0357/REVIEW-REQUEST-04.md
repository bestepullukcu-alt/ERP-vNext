# MOD-0357 — bağımsız inceleme isteği 04 (SOP §20.4 / CT-APR-02; R1 düzeltme turu 3)

- Hedef: `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` @ sha256 `6207d58c8b6be9261bf34f9b82b0994689d7874ca35727d35970ecf8314a9a94`
- Kaynak commit: `59b1b7f28` (dal `chore/ct/mg-ct-01`; `origin/main` ile birleşik; yerel, push edilmedi)
- Önceki: `289f88b3…` @ `8d2c338bc` — inceleme 03 **KOŞULLU** (0 engel, 1 orta, 6 düşük; 02'nin 7 bulgusu kapandı), kayıt `docs/records/reviews/MOD-0357/2026-10-29-289f88b3.md`, satır `execution/approvals/reviews.tsv` (`347596318`).
- Bu tur = önceki sürüm + yalnız inceleme 03 bulgularının düzeltmesi (sahip sözü "Evet, düzelt" = `duzelt-3`). §24.0'da "Review 03 disposition" tablosu. Fark: `git diff 8d2c338bc 59b1b7f28 -- <paket>` (+379 / −119). §16 K11 tek R1 dışı değişiklik (daraltma).
- **CT ön denetimi:** `git diff --check` temiz; `\r` yok; `pack_approval.slices()` → DL-1, DL-13…DL-28; sahip cevapları bloğu değişmedi; yeni açık soru Q25 (sahibe ayrıca sorulacak). Toplantı kodu `826150ef4`'ten beri değişmedi.
- **CT notları (inceleyen değerlendirsin):** (1) S28-2 taraması için yeni, yalnız testte kullanılan bir paket (`Microsoft.CodeAnalysis.CSharp`) öneriliyor — bağımlılık eklemek savunulabilir mi, daha hafif bir yol var mı? (2) Yazar `GenerateDueMeetingSeriesCommand`'ın denetim yolu a'ya geçişini S21'de bıraktı; S28'deki "zincirsiz yedek" bu komutun yazdığı şeyi değiştiriyor mu (R1-D12)?
- İsteyen: depo sahibi (GMG-ERP, K3) · Hazırlayan: CT · Toplantılar · Yazan: `module-pack-author` alt ajanı (Opus · high) · İnceleyen: inceleme 01, 02, 03'ü yapan bağlamlardan ayrı yeni bir Claude alt ajanı. **Model · effort: Sonnet · high** (sahibin 2026-10-29 kuralı: salt-okuma işlerinde daha ucuz model; risk HIGH).
- Bulgu dosyası: `docs/records/reviews/MOD-0357/<YYYY-AA-GG>-6207d58c.md`; yeni bulgular F-25'ten; kapanan 03 bulguları düz metin satırında (S-59).

## İnceleme prompt'u

```text
HEDEF: MOD-0357 (Toplantılar) module pack R1 — bağımsız inceleme 04 (SOP §20.4 / CT-APR-02; düzeltme turu 3). İnceleyensin; paketi yazan değilsin, inceleme 01, 02, 03'ü yapan da değilsin.
Yer: C:\dev\ERP-vNext\.claude\worktrees\mg-ct-01 (dal chore/ct/mg-ct-01). Yalnız oku: dosya düzenleme, kod yazma, commit, push yok. Derleme ve test koşma (bilgisayar paylaşımlı). Tek çıktın aşağıdaki bulgu metnidir.
Önce doğrula: `git merge-base --is-ancestor 59b1b7f28 HEAD` çıkış 0 ve paketin sha256'sı 6207d58c8b6be9261bf34f9b82b0994689d7874ca35727d35970ecf8314a9a94. Tutmuyorsa dur ve bildir.

Kapsam: yalnız bu turun farkı ve onun dokunduğu yerler. Önce oku: bu istek dosyası; inceleme 03 bulguları (docs/records/reviews/MOD-0357/2026-10-29-289f88b3.md); paketteki §24.0 "Review 03 disposition" tablosu; fark `git diff 8d2c338bc 59b1b7f28 -- execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md`; değişen bölümlerin tamamı (§16 K11, R1-D15, §24.5, §24.7, S23-8, S26-10, S28 ve §25, §26 DL-28); ilgili kod (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/** — özellikle Handlers/CommandHandlers/GenerateDueMeetingSeriesHandler.cs, MeetingFollowUpCommandHandlers.cs, MeetingCommandHandlers.cs, Handlers/QueryHandlers/MeetingReportQueryHandlers.cs, Services/MeetingReportExportSerializer.cs; frontend/Diten.Web/wwwroot/assets/js/Meetings/follow-up-dialog.js, form.js, api.js; services/Diten.Platform/tests/** içinde paketin adını verdiği test dosyaları; test projelerinin .csproj dosyaları).

Denetle (risk HIGH, ama yalnız bu turun değişiklikleri):
1. İnceleme 03'ün her bulgusu (F-18…F-24) gerçekten kapandı mı? Kapanmayanı yeni bulgu olarak yaz (F-25'ten), dayandığı eski bulguyu söyle.
2. F-18 zincirsiz yedek: seri durmadan ilerliyor mu, hiçbir taşıma (bağ, gündem, görev) sızmıyor mu, yedek oluşturma da başarısız olursa ne oluyor, çift toplantı riski var mı?
3. MEETING_CREATOR_CANNOT_VIEW ön reddi: bugünkü kullanıcı akışlarını (asistan başkası adına açar) bozuyor mu; devam yolunda uygulanmaması (Q25) tutarlı mı?
4. F-24 tamamlama istisnası: yalnız görebilene, idempotent, sızıntısız mı?
5. CT notları (1) ve (2).
6. Düzeltme yeni bir çelişki, döngülü bağımlılık, kapsam taşması ya da yeni K2 borcu getirdi mi; §24.7 toplamı hücrelerin toplamı mı; §26 seviyeleri tek mi?
Ölçtüklerini tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz; en üste kullandığın modeli, effort'u ve "oturum: CT · Toplantılar oturumunun inceleme alt ajanı 04 (ayrı bağlam)" yaz):
# MOD-0357 — bağımsız inceleme
hedef: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md @ 6207d58c8b6be9261bf34f9b82b0994689d7874ca35727d35970ecf8314a9a94
Kapananlar (inceleme 03): <kapandığını ölçtüğün F numaraları, virgülle>
F-25 | ORTA | <bulgu; paket satır numarası ve dayanak dosya> | açık | CT | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (Kapananlar satırı "Kapananlar" ile başlar); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; yeni bulgu yoksa F satırı yazma; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
