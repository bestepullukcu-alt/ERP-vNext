# Zaman çizelgesi onay geri çekmesindeki açık — sahibe not (MOD-0023 kulesinden; MOD-0280-FU01 başka kulenin işi)

- Tarih: 2026-10-29 · Ölçüm: `chore/ct/pss-wf-ct-01` @4c08fc75a (= origin/main o gün); salt okunur, çalıştırılmadı.
- Kaynak: İzin ve devamsızlık kulesinin MOD-0023 tasarım isteği (yerel 72a00e83b, `hcm-leave-mod0023-design-request-01.md`) ve
  bu kulenin ölçümü. Bu not karar ya da iş verme değildir; kod yazılmadı (XMC-001).

## Sade anlatım

Bir çalışan haftalık zaman çizelgesini onaya gönderip hemen geri çektiğinde, sistem bazen "geri çekildi" der ama arkada onay
akışı açık kalır. Gönderim yarım kalmışsa (akış açılmış ama onay görevi henüz yazılmamışsa) geri çekme "iptal edecek görev yok"
diye başarılı döner. Sonra aynı gönderim tamamlanırsa yöneticinin önüne, çalışanın geri çektiği hafta için bir onay görevi
düşebilir.

## Olgular (dosya:satır @4c08fc75a)

- `services/Diten.Platform/src/Diten.Platform.Application/Features/TimeEntry/Services/TimesheetApprovalService.cs:230-262` —
  `CancelAsync` yalnız açık görevleri MOD-0023'ün kendi iptal komutuyla iptal eder; örnek yoksa ya da iptal / zaman aşımı ise
  `true` döner (:233-237). Görevi henüz yazılmamış `Active` bir örnekte döngü boş kalır ve sonuç başarıdır; örnek açık kalır.
- Tek adımlı akışta görev iptali örneği kapatır (`WorkflowTaskTransitionSupport.cs:670`, `Cancelled`) — ama yalnız görev
  varsa.
- Aynı anahtarla yeniden başlatma, `Active` örnekte eksik görevi tamamlar (`StartWorkflowInstanceHandler.cs`, eksik başlatma
  dalı); kapalı örneği yeniden açmaz.
- MOD-0023'ün terk edilmiş başlatma kapatması (BL-549, `CloseAbandonedWorkflowStartHandler.cs`,
  `WorkflowStartRecoverySweepJob.cs`) örnek kimliğiyle ve yalnız terk edilmiş ayırmada çalışır; anahtarla kapatma yolu yok.

## Çıkarım (ölçülmedi)

- Düzeltme yolu tüketici tarafında mümkün görünüyor: geri çekmeden önce başlatmayı aynı anahtar ve olgularla yeniden
  oynatmak (görevin yazıldığını doğrulamak), sonra görevi iptal etmek; "görevsiz Active örnekte true" başarı sayılmamalı.
  İzin kulesi kendi tasarımında (rev 05) bu yolu seçti.
- MOD-0023 tarafında "başlatmayı anahtarla kapat" yeteneği daha genel bir çözüm olur; İzin kulesi bunu artık acil
  görmüyor (iyileştirme).

## Kimin işi

- Zaman çizelgesi (MOD-0280-FU01): CT · İzin ve devamsızlık / zaman kulesi — düzeltme o kulenin onayıyla.
- MOD-0023 yeteneği istenirse: bu kule, sahibin tasarım kararıyla (kart S-90/5), §17 DL-2'den sonra (aynı dosyalar).
