# MOD-0280-FU02 · MOD-0023 kulesinin S-90 cevabı (onay akışı dilimi DL-3 kapısı)

- Tarih: 2026-10-29 · İsteyen: CT · İzin ve devamsızlık (sahibin seçimi "İş akışı kulesine istek", bu sohbette) ·
  Cevaplayan: CT · İş akışı (MOD-0023 İş Akışı Motoru, kuyruk `pss-wf`) · Yol: Yönetici · Ofis iletti, metin değiştirilmedi
- Paket: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256
  `e9fc8122…` (ready-for-dev; PR bestepullukcu-alt/ERP-vNext#301)
- Niteliği: kulenin **ölçümü**, karar değil; onay taşımaz. Kodu origin/main ile aynı sürümden ölçtü.

## Kulenin cevabı (özet; ham metin sohbette)

1. **Onay tanımı:** izin modülü kendi kodunda yazar — MOD-0023'ün var olan tanım oluşturma ve yayımlama komutlarıyla,
   zaman çizelgesinin `EnsureTemplateAsync` kalıbıyla (`TimesheetApprovalService.cs:327–360`); şablon kodu izin
   modülünün sabiti. MOD-0023 dosyasına yazma yok, sahip kararı gerekmiyor.
2. **Başlatma, iptal ve yeniden okuma:** uygun; komutlar var (`CancelWorkflowTaskCommand` `AllowEscalated` yalnız süreç
   içi). Kulenin rev05 koşuluyla aynı. Uyarı: MOD-0023'ün henüz kodlanmamış bir sonraki diliminden sonra aynı anahtarla
   tekrarda yanıtlar değişecek — aynı anahtar ve farklı gövde 409, iptal tekrarı farklı aktörle 403, başlatmaya yeniden
   oynatma işareti. İzin modülü iptal anahtarını hep aynı aktörle tekrarlamalı ve 403 / 409'u "yeniden oku" diye ele
   almalı; o dilimin kabulünde izin modülü kulenin tüketici listesine eklenecek.
3. **Yarım başlatmayı kapatma** (`SupersedeWorkflowHalfStartCommand`): kod var; görevi olan, `Active` olmayan ya da
   güvenilir başlatmayı reddeder, yaş şartı yok. Reddedilen yeniden oynatmadan sonra kullanım Görevler özelliğindekiyle
   aynı, uygun. İlk başlatma başarısız olunca kalan görevsiz başlatmayı kapatmaya komut sözleşmesi izin veriyor;
   kapattıktan sonra yeni gönderim yeni anahtarla açılmalı (eski anahtar kapalı örneği bulur). MOD-0023 değişikliği yok.
4. **Görev Merkezi kartı:** MOD-0023'ün işi değil (Görev Merkezi tarafı). MOD-0023 yalnız başlatmadaki görüntü
   bağlamını (başlık, alt başlık, etiketler) taşır; kartta kişi adı olmaz.

Kulenin özeti: dört maddenin hiçbiri MOD-0023 kod değişikliği gerektirmiyor; bu yüzden XMC-001 "(a) bu işte yaz" kararı
gerekmiyor.

## CT değerlendirmesi

- **DL-3 kapısı (MOD-0023 kulesi, S-90):** karşılandı. Paketteki kapı maddesi "the MOD-0023 tower's consent under card
  S-90 recorded" bu kayıtla karşılanır; settle 4. adımı (AC-56) iki durum için de uygulanır.
- **Paketle uyum (ölçüldü):**
  - Madde 1: paketin `LeaveApprovalService` ve onay tanımı kaydı aynı kalıbı izler; yeni bir MOD-0023 yeteneği yok.
  - Madde 2: paket zaten yalnız yeniden okunan durumla karar veriyor, tek başına 409'u başarı saymıyor (Runtime
    Constraints 9); iptali yalnız çalışanın settle'ı gönderir, yani aktör hep aynı. 403'ün de "yeniden oku" sayılması
    onay akışı diliminin iş paketi prompt'una yazılır.
  - Madde 3: paket her yeniden gönderimde gönderim numarasını artırır; anahtar yeni olur.
  - Madde 4: kart, Görev Merkezi'nin `IApprovalSourceResolver` arayüzünün izin modülünün kendi klasöründeki uygulamasıyla
    gelir (`Features/WorkAggregation/Services/IApprovalSourceResolver.cs`; zaman çizelgesinin
    `Features/TimeEntry/Providers/TimesheetApprovalSourceResolver.cs` kalıbı). Görev Merkezi koduna ek yok. Paketin
    "Ownership and Boundaries" satırı kartı MOD-0023'e yazıyor; bu, paketin bir sonraki revizyonunda "Görev Merkezi"
    olarak düzeltilecek (kural değiştirmez, bu onayı engellemez).
- **Açık kalan kapı:** XMC-001 — `Diten.Platform`'a kod yazan her dilim için platform-shared-services sahibinin onayı.
  Kartta bu sahip henüz belli değil (sahip kararı).
