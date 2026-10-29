---
id: HCM-LEAVE-AR-01
kind: ARCHITECTURE_REASSESSMENT_REQUIRED
status: decided
module: MOD-0280-FU02
queue: hcm-leave
decided_by: owner (CT · İzin ve devamsızlık conversation, word `kilit-gonder`)
decided_at: 2026-10-29
---

# MOD-0280-FU02 — mimari yeniden değerlendirme 01: izin talebi ↔ onay başlatma yarışı

> Control Tower kaydıdır (kart §0.2 Y3, SOP §28.2). Paket onayı, kod ya da dağıtım izni değildir.

## Neden açıldı

Aynı kök neden üç ardışık bağımsız inceleme turunda kapanmadı:

| Tur | Paket sürümü | Bulgu | Kök |
|---|---|---|---|
| inceleme 01 (`2026-10-29-ea61e8c6.md`) | revizyon 01 | F-02 | talep, bakiye ve onay başlatmanın birlikte güvenli yazılmaması |
| inceleme 02 (`2026-10-29-47df0fc4.md`) | revizyon 02 | F-10, F-11, F-16 | sistem işinin yeniden denemesi, otomatik iade, belirsiz commit |
| inceleme 03 (`2026-10-29-436bc38b.md`) | revizyon 03 | F-17 | başlatma sürerken iptal ya da yeniden gönderim; geç gelen açık görev |

Ortak kök: MOD-0023 onay örneğinin başlatılması izin talebinin işleminden ayrı ve kendi işleminde yürüyor; başlatma sürerken
talep üzerinde başka bir işlem (iptal, yeniden gönderim) yapılabildiği sürece "başlatma tamamlandı mı" sorusu kesin
cevaplanamıyor. Revizyon 03'ün dayandığı MOD-0280-FU01 yöntemi bu boşluğu log'a yazarak geçiyor
(`TimesheetApprovalService.cs:206–222`; iptal başarısızlığında yalnız uyarı), onaylanmış örnekte iptali de reddediyor
(`:230–243`).

## Değerlendirilen yollar

1. **Talep başına süreli başlatma kilidi (seçildi).** MOD-0023'e dokunmaz; yarışı yapısal olarak önler.
2. **MOD-0023'e korumalı başlatma özelliği** ("nesne iptal edildiyse başlatma"). Daha az yerel kod, ama başka bir kulenin yeni
   yetenek kararını bekler (kart S-90 madde 5); yavaş. Seçilmedi; kilit ileride bu özelliğe devredilebilir.
3. **Yarışı kabul edip denetime yazmak** (revizyon 03). İnceleme 03 F-17 ile reddedildi.

## Karar (sahip, `kilit-gonder`, 2026-10-29)

1. **Başlatma kilidi.** `LeaveRequest` üzerinde `StartLeaseUntilUtc` ve `StartLeaseId`. Gönderim ve yeniden gönderim,
   onay başlatmadan önce kilidi talep ve kişi kilidiyle aynı koşullu yazımda alır; başlatma çağrısı dönünce (başarı ya da
   hata) kilit aynı istekte bırakılır. Kilit varken iptal ve yeniden gönderim 409 `LEAVE_APPROVAL_STARTING` döner.
   Kilidin süresi (10 dakika) yalnız süreç ölürse dolar; dolduktan sonra MOD-0023'e anahtarla yapılan sorgu kesin sonuçtur,
   çünkü MOD-0023'ün başlatması kendi işleminde ya tamamlanmış ya hiç olmamıştır.
2. **Yeniden gönderim.** Kilit boşken önceki gönderimin anahtarıyla sorgu: açık örnek → MOD-0023'ün kendi komutuyla iptal;
   iptal başarısızsa yeniden gönderim **reddedilir** (uyarıyla geçilmez); karar verilmiş örnek → karar uygulanır, yeni
   başlatma olmaz; örnek yok → yeni gönderim numarasıyla başlatılır.
3. **İki ayrı iptal.**
   - Bekleyen talep: kilit boşken anahtarla sorgu; açık görev → MOD-0023 iptali; karar önce geldiyse iptal reddedilir ve
     karar uygulanır; örnek yok ya da iptal başarılı → talep `Cancelled`, ayrılan günler aynı işlemde iade.
   - Onaylanmış izin: MOD-0023'e dokunulmaz, tamamlanmış onay korunur. Başlamamışsa çalışan, başlamışsa İK (kayıt
     düzeltmesi, gerekçe koduyla) iptal eder; talep `Cancelled`, `TakenDays` aynı işlemde iade.
4. **Geç gelen karar yolu kalkar.** Kilit, başlatma sürerken iptali imkânsız kıldığı için "iptalden sonra oluşan örnek"
   durumu yalnız süreç ölümü + kilit süresi dolması ile mümkündür; o durumda da iptalden önce anahtarla sorgu yapılır.
5. **Kalan risk (yazılı):** canlı ama 10 dakikadan uzun asılı kalan bir başlatma çağrısı. Başlatma çağrısına 2 dakikalık
   iptal süresi verilir; kilit bunu kat kat aşar. Risk pakette bilinen sınır olarak yazılır.

## Sonuç

Paket revizyon 04 bu kararla yazılır (`execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md`).
Revizyon 04 de bu kökte bağımsız incelemeden geçemezse yol 2 (MOD-0023 korumalı başlatma) MOD-0023 kulesiyle açılır.

## Ek — yol 2 açıldı (2026-10-29)

Bağımsız inceleme 04 (`docs/records/reviews/MOD-0280-FU02/2026-10-29-5fb4d1bb.md`) F-20 ile UYGUN DEĞİL döndü: süreli kilit,
MOD-0023'ün örneği ayırma ile görev yazmayı iki ayrı işlemde yapması yüzünden kesin değil. Sahip sözü `mod0023-gorus`:
yol 2 açıldı. Tasarım isteği `docs/records/decisions/2026-10/hcm-leave-mod0023-design-request-01.md` ("başlatmayı
anahtarla kapat"). Paket revizyon 05, MOD-0023 sahibinin kararı kayda girene kadar yazılmaz.
