# MOD-0023 tasarım görüşmesi isteği 01 — onay başlatmasını anahtarla kapatma (izin kulesinden)

> Kart §7c S-90 madde 5: başka sahibin modülünde yeni yetenek gerekiyor; isteyen kule yazmaya başlamaz, önce sahiple tasarım
> konuşulur ve sahibin kararı kayda girer. Bu belge bir **istektir**; MOD-0023'e iş vermez, karar, onay ya da kod izni değildir.

## Künye

- İsteyen: CT · İzin ve devamsızlık (kuyruk `hcm-leave`, modül MOD-0280-FU02 Leave & Absence Management) — sahip sözü `mod0023-gorus` (2026-10-29)
- Muhatap: MOD-0023 Workflow Designer (Approvals/SLAs/Escalations) sahibi ve kulesi (kuyruk `pss-wf`, "CT · İş akışı"); iletim sahip / Yönetici · Ofis üzerinden (`ct_route.py --kind contract --module MOD-0023` tek hedef bulamadı, çıkış 1)
- Dayanak: mimari yeniden değerlendirme `docs/records/decisions/2026-10/hcm-leave-architecture-reassessment-01.md` (yol 2); bağımsız inceleme 04 bulgu F-20 (`docs/records/reviews/MOD-0280-FU02/2026-10-29-5fb4d1bb.md`)

## İletilecek metin

```text
HEDEF: CT · İş akışı (MOD-0023 Workflow Designer) — tasarım görüşmesi isteği (kart S-90 madde 5), CT · İzin ve devamsızlık'tan. Bu bir istektir; karar, onay ya da iş verme değildir.

Bağlam: MOD-0280-FU02 (izin talebi) onayı MOD-0023 ile başlatıyor (MOD-0280-FU01 puantajın TimesheetApprovalService yöntemiyle). Paket dört bağımsız incelemede aynı kökte takıldı: bir talep iptal edildiğinde ya da yeniden gönderildiğinde, önceki gönderimin MOD-0023 başlatması hâlâ sürüyorsa sonradan açık görev oluşabiliyor. İzin modülü bunu kendi içinde kesin önleyemiyor.

Ölçülen kod (main ile aynı, 2026-10-29):
1. StartWorkflowInstanceHandler.cs:283–298 örneği ayırır; :620–659 ClaimWithStartRowsAsync görev, anlık görüntü ve günlüğü örnek hâlâ Active iken tek koşullu talepte yazar ("False = the claim was lost … or the instance left Active"). Yani örnek kapatılırsa geç kalan adım görev açamaz — bu koruma zaten var.
2. Ama örnek henüz ayrılmamışken bir idempotency anahtarını önceden "kapalı" işaretlemenin yolu yok; anahtar sorgusu "örnek yok" dese bile başlatma sonra örneği ayırıp görevi yazabilir.
3. Örnek düzeyinde iptal yalnız güvenilir servis yolunda var (CancelTrustedWorkflowInstanceHandler → TrustedWorkflowCancellationCoordinator.cs:75–102; ServiceClientId ve DelegatedMakerUserId ister). Platform içi modüller (puantaj, izin) yalnız görev iptali kullanıyor: TimesheetApprovalService.CancelAsync (:230–273) görevi olmayan Active örnekte true döner ve örneği açık bırakır; RetireSubmissionAsync (:206–222) iptal başarısızsa yalnız uyarı yazar.

Önerilen yetenek (öneri — onay değil; adı ve biçimi sizin kararınız): "başlatmayı anahtarla kapat" — tenant + idempotency key + aktör (o anahtarı başlatan kullanıcı) ile çağrılır:
- anahtarla örnek yoksa: o anahtar için kapalı (Cancelled) bir mezar taşı kaydı yazılır; aynı anahtarla sonradan gelen başlatma kapalı örneği bulur ve görev açmaz (FU01'in "kapalı örnek benimsenmez" davranışıyla aynı yönde);
- örnek Active ise (görevli ya da görevsiz): örnek Cancelled olur, açık görevler MOD-0023'ün kendi komutuyla iptal edilir; geç kalan claim başarısız olur;
- örnek karar verilmişse: karar döner, hiçbir şey değişmez.
Sonuç tek cevap: "kapandı" ya da "karar verilmiş (onay/ret)". Bu cevaptan sonra aynı anahtarla görev açılamaz.

Yararı: izinde iptal ve yeniden gönderim günleri ancak bu cevaptan sonra iade eder (inceleme 04 F-20 kapanır); puantajın RetireSubmissionAsync / CancelAsync boşluğu da aynı yetenekle kapanır.

İstenen: (1) MOD-0023 sahibinin tasarım kararı kayda girsin: kabul, değişiklikle kabul ya da ret (ret ise önerdiğiniz yol). (2) Kabul edilirse S-90 madde 2–3: eki izin kulesi önce MOD-0023 paketine öneri olarak yazar (bağımsız inceleme + onay), sonra kodu; siz inceler ve onaylarsınız; merge başkasınındır. (3) Ortak dosya varsa tek yazar kuralı (K15) için hangi dosyaların sizde kaldığını söyleyin.

Dönüşü CT · İzin ve devamsızlık'a iletin. Kişi adı ve e-posta yok; roller.
```

## Bu isteğe kadar izin kulesinde

- Paket revizyon 04 (`status: draft`) ve bulgu dosyası 04 yerelde; revizyon 05 MOD-0023 kararı gelene kadar yazılmaz.
- Revizyon 04'ün diğer içeriği (iki ayrı iptal, denetim zarfı) inceleme 04'te kapandı; yalnız başlatma yarışı bu karara bağlı.
