---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-02
status: approved — D9 2026-10-24 itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
decided_at_local: 2026-10-24
decided_by: depo sahibi (rol, K1); bu kayıt ad yazmaz
decision_source: Treasury Intake Control Tower oturumu (Claude Code); sahibin mesajları "treasury modüllerin çalışması için gereken değişiklikleri yapabiliriz" ve "tamam, başlayalım" (2026-10-24)
scope: Treasury intake için başka modüllerde gereken değişiklikler (XMC-001); ilk somut örnek MOD-0220
bound_to: dal `chore/ct/treasury-intake-ct-01`, taban origin/main 96bedc89374dd8fd06550c49ae2fc65ca07e6298
recorded_by: Treasury Intake Control Tower; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# Treasury intake — sahip kararları 2 (2026-10-24)

## D9 — Treasury için başka modüllerde gereken değişiklikler (XMC-001 onayı)

Sahip, Treasury modüllerinin çalışması için başka modüllerde gereken değişikliklere onay verir. Şartlar:

1. Değişiklik **gereken en küçük ve eklemeli** değişikliktir; var olan tüketicilerin davranışı değişmez.
2. Önce o modülün **pack'ine** değişiklik önerisi olarak yazılır; pack değiştiği için bağımsız inceleme (CT-APR-02) ve
   sürüme bağlı onay (CT-APR-01) gerekir. Kod ancak o pack ve ilgili Treasury pack'i `ready-for-dev` olunca yazılır.
3. Başka bir domain'in sahibi varsa (ör. MDM) karar ona sorulur; bu onay, sahibin cevabının yerine geçmez, sorulmasını
   ve hazırlanmasını yetkilendirir.
4. Aynı dosyayı başka bir Control Tower değiştiriyorsa (ör. MOD-0140, MVP-2) yazılmaz; tek yazar kuralı (K15).

**İlk uygulama — MOD-0220** (TRM-S1, MOD-0126 ile birlikte):
- `GET /api/legal-entities/{id}/lookup-validation` yanıtına `ApprovalStatus` alanının **eklenmesi** (alan kayıtta var,
  `LegalEntity.cs:50`; 404 davranışı değişmez).
- MOD-0220 sözleşme belgesi (mevcut `GET` ve `lookup-validation` uçları) — belge, kod değil.
- Şirketler arası ilişki modeli **eklenmez** (bugün hiçbir Treasury modülü istemiyor; tedarikçi–şirket ilişkisi
  Recovery'de MOD-0140'tadır).

## Karar olmayanlar

"Aktif ama onaylanmamış şirket ödeme yapabilir mi" (MDM sahibine ve sahibe soru); port (EA); MOD-0003 (0.3).

## Bu kayıt ne değildir

Kod, pack onayı, push / PR ya da başka bir Control Tower'ın dosyasına yazma izni değildir.
