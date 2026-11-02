# MOD-0357 — bağımsız inceleme isteği 03 (SOP §20.4 / CT-APR-02; R1 düzeltme turu 2)

- Hedef: `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` @ sha256 `289f88b306b3218746cbf6ceb8a7f040b64bd2b6432a9ef2cf747e47205aa9c7`
- Kaynak commit: `8d2c338bc` (dal `chore/ct/mg-ct-01`; `origin/main` dal-ilk birleştirmeyle alındı, `2b0f3bd68`; yerel, push edilmedi)
- Önceki: `ab321ce2…` @ `e18a2294b` — inceleme 02 **KOŞULLU** (0 engel, 3 orta, 4 düşük; inceleme 01'in 10 bulgusu kapandı), kayıt `docs/records/reviews/MOD-0357/2026-10-27-ab321ce2.md`, satır `execution/approvals/reviews.tsv` (`274e417c4`).
- Bu tur = önceki sürüm + yalnız inceleme 02 bulgularının düzeltmesi (sahip sözü `duzelt-2`): S28-10 (oluşturmanın tekrar yolu), S28-11 (seri), S28-1/2/4/6/7/8 değişiklikleri, S23-8, S25-6/7, S26-2/4/10, §24.5 iki yeni neden kodu, Q24 = düşür, Q23 tek ifade. §24.0'da "Review 02 disposition" tablosu. Fark: `git diff e18a2294b 8d2c338bc -- <paket>` (+341 / −96).
- **CT ön denetimi:** `git diff --check` temiz; `\r` yok; `pack_approval.slices()` → DL-1, DL-13…DL-28; sahip cevapları bloğu yerinde (Q24 satırı eklendi); toplantı kodu `826150ef4`'ten beri değişmedi, bu yüzden 02'nin satır numaraları geçerli.
- **CT kararları (yazarın açık bıraktığı teknik sorular; pakete değil buraya yazıldı, inceleyen değerlendirsin):** (1) Devam toplantısı çağrısı toplantı yazıldıktan sonra kesilirse tekrar reddedilir ve taşınan satırlar elle eklenir — kabul, S28-10'da yazılı; dar istisna ileride. (2) Tekrar oluşturmanın 201 yerine 200 dönmesi ve RespondToInvitation'ın davetsiz çağırana toplantı-yok gövdesi dönmesi — kabul (Görev Merkezi eylemi etkisi inceleyen tarafından ölçülsün). (3) 409'un "bu beş değerle bir toplantı var" bilgisini vermesi — kabul, başka veri yok. (4) series-manage'in gelecekteki toplantılara kişi ekleyebilmesi — kabul, kaynak toplantı kapalı kalır.
- İsteyen: depo sahibi (GMG-ERP, K3) · Hazırlayan: CT · Toplantılar · Yazan: `module-pack-author` alt ajanı (Opus · high) · İnceleyen: inceleme 01 ve 02'yi yapan bağlamlardan ayrı, yeni bir Claude alt ajanı. **Model · effort: Sonnet · high** — sahibin 2026-10-29 kuralı (Yönetici · Ofis iletti): salt-okuma işlerinde daha ucuz model; risk HIGH olduğu için effort high.
- Bulgu dosyası: `docs/records/reviews/MOD-0357/<YYYY-AA-GG>-289f88b3.md`; yeni bulgular F-18'den; kapanan 02 bulguları düz metin satırında (S-59).

## İnceleme prompt'u

```text
HEDEF: MOD-0357 (Toplantılar) module pack R1 — bağımsız inceleme 03 (SOP §20.4 / CT-APR-02; düzeltme turu 2). İnceleyensin; paketi yazan değilsin, inceleme 01 ve 02'yi yapan da değilsin.
Yer: C:\dev\ERP-vNext\.claude\worktrees\mg-ct-01 (dal chore/ct/mg-ct-01). Yalnız oku: dosya düzenleme, kod yazma, commit, push yok. Derleme ve test koşma (bilgisayar paylaşımlı). Tek çıktın aşağıdaki bulgu metnidir.
Önce doğrula: `git merge-base --is-ancestor 8d2c338bc HEAD` çıkış 0 ve paketin sha256'sı 289f88b306b3218746cbf6ceb8a7f040b64bd2b6432a9ef2cf747e47205aa9c7. Tutmuyorsa dur ve bildir.

Önce oku: bu istek dosyası (CT kararları dahil); inceleme 02 bulguları (docs/records/reviews/MOD-0357/2026-10-27-ab321ce2.md); paketteki §24.0 "Review 02 disposition" tablosu; fark `git diff e18a2294b 8d2c338bc -- execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md`; §24.31 S28 tamamı (özellikle S28-1, S28-2, S28-4, S28-6, S28-8, S28-10, S28-11); S23, S25, S26 değişen maddeleri; §24.5, §24.7, §25; ilgili kod (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/**: Services/MeetingIdempotencyKeyResolver.cs, Handlers/CommandHandlers/MeetingCommandHandlers.cs, MeetingFollowUpCommandHandlers.cs, MeetingSeriesCommandHandlers.cs, GenerateDueMeetingSeriesHandler.cs, Handlers/QueryHandlers/MeetingReportQueryHandlers.cs, Services/MeetingReportExportDisclaimer.cs; Providers/MeetingWorkItemActionDispatcher.cs ve Görev Merkezi'nin davet eylemini işleyen ön yüz kodu frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/ ve shared/diten-invite-card.js).

Denetle (risk HIGH):
1. İnceleme 02'nin her bulgusu (F-11…F-17) gerçekten kapandı mı? Paket metnini ve kodu ölç. Kapanmayan ya da yarım kalanı yeni bulgu olarak yaz (F-18'den), dayandığı eski bulguyu söyle.
2. S28-10: 409 MEETING_CREATE_CONFLICT gövdesi gerçekten veri taşımıyor mu; 200/201 ayrımı ön yüzü ya da var olan testleri bozar mı (ölç); devam toplantısının "yeni değilse ret" kuralı yeni bir sızıntı ya da veri kaybı doğuruyor mu?
3. S28-11 ve S23-8: seri taraması seri düzenleyeniyle CanView uyguluyor; CreatedFromMeetingId HTTP'den gelemiyor — bu, istek modeli ve komut tanımıyla (MeetingCommands.cs, MeetingModels.cs) gerçekten sağlanabilir mi?
4. S28-1/S28-2: her işleyicinin kendi "bulunamadı" gövdesi bayt bayt aynı mı ölçülebilir; sınıf düzeyinde tarama tarif edildiği gibi sabotajda kırmızıya döner mi; RespondToInvitation değişikliği Görev Merkezi davet eylemini bozar mı (ön yüz MEETING_ATTENDEE_NOT_FOUND kodunu okuyor mu — ölç)?
5. Düzeltme yeni bir çelişki, döngülü bağımlılık, kapsam taşması ya da yeni K2 borcu getirdi mi? §26 seviyeleri hâlâ tek ve savunulabilir mi?
6. CT kararları (1)–(4) savunulabilir mi?
Ölçtüklerini tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz; en üste kullandığın modeli, effort'u ve "oturum: CT · Toplantılar oturumunun inceleme alt ajanı 03 (ayrı bağlam)" yaz):
# MOD-0357 — bağımsız inceleme
hedef: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md @ 289f88b306b3218746cbf6ceb8a7f040b64bd2b6432a9ef2cf747e47205aa9c7
Kapananlar (inceleme 02): <kapandığını ölçtüğün F numaraları, virgülle>
F-18 | ORTA | <bulgu; paket satır numarası ve dayanak dosya> | açık | CT | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (Kapananlar satırı "Kapananlar" ile başlar); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; yeni bulgu yoksa F satırı yazma; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
