# MOD-0357 — bağımsız inceleme isteği 06 (SOP §20.4 / CT-APR-02; Codex, R1 bütünü)

- Hedef: `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` @ sha256 `5383fd43dc7ec1e266f7fbb8f62d946f24dd194e4acf49403758a6f5f46459b8`
- Kaynak commit: `5cd785c13` (dal `chore/ct/mg-ct-01`; yerel, push edilmedi). ZIP bu isteği içeren commit'ten üretilir; paket iki commit'te de aynıdır.
- Önceki: inceleme 01–05 (hepsi Claude alt ajanı, ayrı bağlam). Son: 05 @ `5383fd43…` **KOŞULLU** (0 engel, 0 orta, 4 düşük: F-29…F-32, açık), kayıt `docs/records/reviews/MOD-0357/2026-10-29-5383fd43.md`, satır `execution/approvals/reviews.tsv` (`a6ebd987b`).
- **Neden Codex:** beş incelemenin hepsi aynı model ailesinden. Bu tur farklı bir satıcının modeliyle R1'in bütününe bakar (yalnız son fark değil).
- Açık bulgular F-29…F-32 bilinir; yeniden yazma, yalnız katılmıyorsan ya da daha ağır görüyorsan söyle.
- İsteyen: depo sahibi (GMG-ERP, K3) · Hazırlayan: CT · Toplantılar · Yazan: `module-pack-author` alt ajanı (Opus · high) · İnceleyen: Codex (sahip verir). Model ve effort'u bulgu dosyasının en üstüne sen yaz.
- Bulgu dosyası: `docs/records/reviews/MOD-0357/<YYYY-AA-GG>-5383fd43-codex.md`; yeni bulgular F-33'ten.

## İnceleme prompt'u

```text
HEDEF: MOD-0357 (Toplantılar, Management Review & Cadence) module pack R1 — bağımsız inceleme 06 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan ve önceki beş incelemeyi yapan Claude bağlamlarından ayrısın.
Girdi: bu ZIP. Her dosya tek bir commit'ten birebir alındı; MANIFEST.sha256 her dosyanın SHA-256'sını verir. Önce doğrula: paket dosyasının sha256'sı 5383fd43dc7ec1e266f7fbb8f62d946f24dd194e4acf49403758a6f5f46459b8 ve MANIFEST tutuyor. Tutmuyorsa dur ve bildir. excerpts/ altında son onaylı sürümden (826150ef4) bu sürüme paket farkı var.
Yalnız oku. Derleme ve test koşma. Tek çıktın aşağıdaki bulgu metnidir.

Kapsam: R1 bütünü — paketin §24 (R1-D1…D15, S13–S28, sahip cevapları), §25 Denetlenen olaylar, §26 Teslim dilimleri ve R1'in dokunduğu §16 K11 kutusu. Önce oku: bu istek; inceleme 01–05 bulgu dosyaları (docs/records/reviews/MOD-0357/); paket; AGENTS.md ve .antigravity/rules/ altındaki kural dosyaları; ilgili kod (services/Diten.Platform/**/Meetings/**, kontrolcüler, ReviewMeetingDecisionGate, frontend/Diten.Web/**/Meetings/**).

Denetle (risk HIGH):
1. Erişim güvenliği (S28): görme yetkisi olmayan biri herhangi bir yazma yolundan toplantıya, gündeme, karara, katılımcıya, seriye ya da devam toplantısına dokunabilir mi; kiracılar arası erişim 404 mü; her eylemde [HasPermission] var mı?
2. Dilimler kodla tutarlı mı: paketin "bugün kod şöyle" dediği her yer gerçekten öyle mi; dilimler yazıldığı gibi koda dökülebilir mi; test ve sabotaj satırları kuralı gerçekten yakalar mı?
3. Q25 (devam toplantısını açan taraf "Accepted" katılımcı olarak eklenir; yalnız kaynağın tarafı) ve PreAcceptedUserId'in istek gövdesinden bağlanamaması.
4. Denetim kaydı (AUD-001): §25 olayları gerçekten yazılabilir mi (AuditBehavior üst veriyi işleyiciden önce alır), K2 kapalı-başarısız, yol a/b/c ayrımı doğru mu?
5. Yerelleştirme (7 dil), kayıt kodu (RCS-001, tür öneki MTY, ortak servis henüz yok), yardım ve tur (UXH-001).
6. Bağımlılık sırası, §24.7 tahmin toplamı, §26 kanıt seviyeleri; çelişki, kapsam taşması ya da başka modüle (XMC-001) izinsiz dokunuş var mı?
Ölçtüklerini tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz; en üste kullandığın modeli, effort'u ve "oturum: Codex, sahip tarafından verildi" yaz):
# MOD-0357 — bağımsız inceleme
hedef: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md @ 5383fd43dc7ec1e266f7fbb8f62d946f24dd194e4acf49403758a6f5f46459b8
Kapananlar (inceleme 05): <kapandığını ölçtüğün F numaraları, virgülle; yoksa "yok">
F-33 | ORTA | <bulgu; paket satır numarası ve dayanak dosya> | açık | CT | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (Kapananlar satırı "Kapananlar" ile başlar); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; yeni bulgu yoksa F satırı yazma; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
