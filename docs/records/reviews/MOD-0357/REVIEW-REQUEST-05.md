# MOD-0357 — bağımsız inceleme isteği 05 (SOP §20.4 / CT-APR-02; R1 düzeltme turu 4)

- Hedef: `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` @ sha256 `5383fd43dc7ec1e266f7fbb8f62d946f24dd194e4acf49403758a6f5f46459b8`
- Kaynak commit: `5cd785c13` (dal `chore/ct/mg-ct-01`; yerel, push edilmedi)
- Önceki: `6207d58c…` @ `59b1b7f28` — inceleme 04 **KOŞULLU** (0 engel, 0 orta, 4 düşük; 03'ün 7 bulgusu kapandı), kayıt `docs/records/reviews/MOD-0357/2026-10-29-6207d58c.md`, satır `execution/approvals/reviews.tsv` (`f6e9a8cb1`).
- Bu tur = önceki sürüm + inceleme 04 bulgularının (F-25…F-28) düzeltmesi + sahibin Q25 cevabı (`devam-katilimci`: düzenleyen olmayan katılımcı devam toplantısı açınca kendiliğinden katılımcı olarak eklenir). Sahip sözü "Evet, düzelt" = `duzelt-4`. §24.0'da "Review 04 disposition" tablosu. Fark: `git diff 59b1b7f28 5cd785c13 -- <paket>` (+285 / −82). §16 K11 tek R1 dışı değişiklik (F-25).
- **CT ön denetimi:** `git diff --check` temiz; `\r` yok; `pack_approval.slices()` → DL-1, DL-13…DL-28; sahip cevapları bloğunda yalnız Q25 satırı eklendi. Toplantı kodu `826150ef4`'ten beri değişmedi. Öneri yazıldıktan sonra ayrı bağlamda bir Sonnet ara kontrolü yapıldı (8 düzeltme işlendi); bu kontrol bir inceleme değildir, kaydı yoktur.
- **CT notları (inceleyen değerlendirsin):** (1) Q25'te sahip yanıt durumu söylemedi; CT `Accepted` seçti (`Pending` olsa Görev Merkezi'nde kendi toplantısı için davet kartı çıkardı) ve yalnız kaynağın tarafını (düzenleyen ya da katılımcı) ekledi, `read-all` sahibini eklemedi. İkisi pakette "CT okuması, sahip söylemedi" diye işaretli. Bu okumalar kodla tutarlı mı, davet e-postası ve Görev Merkezi kartı açısından bir yan etki var mı? (2) `PreAcceptedUserId` yalnız komutta; istek gövdesinden bağlanamadığı paket metninde ölçülebilir biçimde güvenceye alınmış mı?
- İsteyen: depo sahibi (GMG-ERP, K3) · Hazırlayan: CT · Toplantılar · Yazan: `module-pack-author` alt ajanı (Opus · high) · İnceleyen: inceleme 01–04'ü yapan bağlamlardan ayrı yeni bir Claude alt ajanı. **Model · effort: Sonnet · high** (sahibin 2026-10-29 kuralı: salt-okuma işlerinde daha ucuz model; risk HIGH).
- Bulgu dosyası: `docs/records/reviews/MOD-0357/<YYYY-AA-GG>-5383fd43.md`; yeni bulgular F-29'dan; kapanan 04 bulguları düz metin satırında (S-59).

## İnceleme prompt'u

```text
HEDEF: MOD-0357 (Toplantılar) module pack R1 — bağımsız inceleme 05 (SOP §20.4 / CT-APR-02; düzeltme turu 4). İnceleyensin; paketi yazan değilsin, inceleme 01–04'ü yapan da değilsin.
Yer: C:\dev\ERP-vNext\.claude\worktrees\mg-ct-01 (dal chore/ct/mg-ct-01). Yalnız oku: dosya düzenleme, kod yazma, commit, push yok. Derleme ve test koşma (bilgisayar paylaşımlı). Tek çıktın aşağıdaki bulgu metnidir.
Önce doğrula: `git merge-base --is-ancestor 5cd785c13 HEAD` çıkış 0 ve paketin sha256'sı 5383fd43dc7ec1e266f7fbb8f62d946f24dd194e4acf49403758a6f5f46459b8. Tutmuyorsa dur ve bildir.

Kapsam: yalnız bu turun farkı ve onun dokunduğu yerler. Önce oku: bu istek dosyası; inceleme 04 bulguları (docs/records/reviews/MOD-0357/2026-10-29-6207d58c.md); paketteki §24.0 "Review 04 disposition" tablosu; fark `git diff 59b1b7f28 5cd785c13 -- execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md`; değişen bölümlerin tamamı (§16 K11, R1-D12, §24.5, §24.7, S15-10, S21, S25-7, S26-10, S28 özellikle S28-10 ve S28-11, §24.25 Q25 ve sahip cevapları bloğu, §25); ilgili kod (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/** — özellikle Handlers/CommandHandlers/MeetingCommandHandlers.cs, MeetingFollowUpCommandHandlers.cs, GenerateDueMeetingSeriesHandler.cs, AgendaItemCommandHandlers.cs, katılımcı ve davet işleyicileri; Infrastructure/Persistence/Repositories/MeetingRepositories.cs; Infrastructure/Services/MeetingInviteMailer.cs; Görev Merkezi toplantı sağlayıcısı; API/Controllers/MeetingsController.cs ve istek modelleri; frontend/Diten.Web/wwwroot/assets/js/Meetings/follow-up-dialog.js, form.js; services/Diten.Platform/tests/** içinde paketin adını verdiği test dosyaları).

Denetle (risk HIGH, ama yalnız bu turun değişiklikleri):
1. İnceleme 04'ün her bulgusu (F-25…F-28) gerçekten kapandı mı? Kapanmayanı yeni bulgu olarak yaz (F-29'dan), dayandığı eski bulguyu söyle.
2. Q25 kuralı (S28-10 "Scheduler as attendee"): kim eklenir, kim eklenmez (düzenleyenin kendisi, kaynağın katılımcısı, read-all sahibi, platform operatörü); Accepted durumu davet e-postası, Görev Merkezi kartı ve yanıt akışıyla tutarlı mı; yeniden deneme ve idempotency anahtarıyla çift satır riski var mı; gizli devam toplantısında (S15-10) sızıntı var mı; `PreAcceptedUserId` gövdeden bağlanamıyor mu ve bu bir testle güvence altında mı?
3. Tamamlama yaşam döngüsü kuralı: yalnız planlı toplantıda mı, iptal ve tamamlanmış mevcut toplantı için ne döner, test satırları var mı?
4. F-25 savunma amaçlı yaratıcı kuralı ve kaldırılan S26-10 form ipucu: 7 dil anahtarları ve testler buna göre güncellendi mi, ölü metin kaldı mı?
5. CT notları (1) ve (2).
6. Düzeltme yeni bir çelişki, döngülü bağımlılık (S21 artık S28'e bağlı), kapsam taşması ya da yeni K2 borcu getirdi mi; §24.7 toplamı hücrelerin toplamı mı; §26 seviyeleri tek mi; sahip cevapları bloğunda Q25 dışında bir değişiklik var mı?
Ölçtüklerini tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz; en üste kullandığın modeli, effort'u ve "oturum: CT · Toplantılar oturumunun inceleme alt ajanı 05 (ayrı bağlam)" yaz):
# MOD-0357 — bağımsız inceleme
hedef: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md @ 5383fd43dc7ec1e266f7fbb8f62d946f24dd194e4acf49403758a6f5f46459b8
Kapananlar (inceleme 04): <kapandığını ölçtüğün F numaraları, virgülle>
F-29 | ORTA | <bulgu; paket satır numarası ve dayanak dosya> | açık | CT | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (Kapananlar satırı "Kapananlar" ile başlar); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; yeni bulgu yoksa F satırı yazma; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
