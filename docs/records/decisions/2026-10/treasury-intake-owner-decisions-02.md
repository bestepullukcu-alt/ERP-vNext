---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-02
status: approved — D9–D15 2026-10-24 itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
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

## D10 — Üç karar (2026-10-24, sahibin soru kutusundaki seçimleri; üçü de önerilen seçenek)

1. **Ödeme yapan şirket hem aktif hem onaylı olmalı.** Soru: "Aktif ama onay durumu 'onaylandı' olmayan bir şirket
   adına ödeme yapılabilsin mi?" — Cevap: "Hayır, onaylı olmalı". Kontrolü MOD-0126 yapar (`OperationalStatus = Active`
   ve `ApprovalStatus = Approved`); MOD-0126 pack'inde kabul ölçütüdür.
2. **MOD-0220 eki onaylandı.** Cevap: "Evet, ekle" — `lookup-validation` yanıtına `ApprovalStatus` alanı (yalnız ekleme;
   404 davranışı değişmez) + MOD-0220 sözleşme belgesi. MOD-0220 pack değişikliği önerisiyle, CT-APR-02/01'den geçer;
   kod MOD-0126 ile birlikte (TRM-14). Sahip burada MDM domain sahibinin kararını kendisi verdi (sahip K1).
3. **Port 5071.** Cevap: "5071". Ölçüm (2026-10-24, yapılandırma dosyaları): 5056–5070 kullanımda (5069
   `SafetyStockService` — AGENTS §3'te yok, ayrı kayma); 5071 ve sonrası hiçbir yapılandırmada yok. Sahip burada EA
   kararını kendisi verdi (AGENTS §3; geçmiş tahsisler de "kullanıcı onaylı"). **AGENTS.md §3 ve
   `.antigravity/rules/ports.md` (korunan yol; bant 5011–5064 yazıyor) değişikliği ayrı onaydır**; bu kayıt yapmaz.

## D11 — MOD-0126 pack kararları (2026-10-24, soru kutusu; dördü de önerilen seçenek)

1. **SoD tier-2 eşiği (O-4):** "Kiracı ayarı, para birimli" — her kiracı eşiğini ve para birimini ayarlar; ayar yoksa
   her onay ikinci kademe ister; farklı para birimindeki parti ikinci kademeye gider.
2. **AdminDirect (PP-05, O-1):** "Hayır, düşür" — tüm onaylar MOD-0023 iş akışı ve SoD kontrolünden geçer.
3. **K2 (O-6):** "Evet, K2" — onay, red ve muafiyet komutları fail-closed; denetim kaydı iş verisiyle aynı işlemde.
4. **İzin öneki (O-5):** "finance-treasury.*".

## D12 — MOD-0126 gap listesinden doğan kararlar (2026-10-24, soru kutusu; ikisi de önerilen seçenek)

1. **Muafiyet kapsamı (O-17):** "Yalnız tier-2 izni" — muafiyet yalnız R4'ü kaldırır; oluşturan, gönderen ve son
   güncelleyen asla onaylayamaz; aday seçimi, karar kaydı ve Release aynı modeli uygular.
2. **Idempotency (O-18):** "Tüm yazma komutlarında zorunlu" — anahtar yoksa 400; sunucu anahtar üretmez; makbuz kapsamı
   kiracı + şirket + aktör + işlem + hedef.

## D13 — Muafiyet adayı (2026-10-24, soru kutusu; önerilen seçenek)

"Yalnız tier-2 aday yoksa" — normalde yalnız tier-2 izni olanlar aday; R1–R3 dışlamasından sonra kiracıda hiç tier-2
aday kalmıyorsa `waive-sod` izni olanlar muafiyet adayı olarak eklenir ve yalnız muafiyetli onay verebilir (gerekçe +
kanıt). Normal onay ve muafiyet yetkisi ayrı değerlendirilir.

## D14 — Dört Treasury pack'i için ortak kararlar (2026-10-24, soru kutusu; dördü de önerilen seçenek)

1. **Veri kapsamı (SEC-002):** "Yetkili olduğu şirketler" — kapsam alanı `LegalEntityId`; kullanıcı yalnız yetkili olduğu
   şirketlerin kayıtlarını görür; kiracıdaki tüm şirketleri görmek ayrı, yalnız açık atanan `…read-tenant-wide` iznidir
   (MOD-0126, MOD-0119, MOD-0120, MOD-0124).
2. **Makbuz ve niyet tipi:** "Tek ortak tip" — servis genelinde tek `FinanceTreasuryCommandReceipt` ve ortak niyet
   altyapısı; dört pack bunu kullanır; kurallar MOD-0126'daki gibi (D12/2, gap listeleri 1–3).
3. **AP/AR K2:** "Evet" — AP ve AR'da parayı etkileyen kararlar (onay, red, iptal/silme, elle ödeme kaydı) K2.
   Cash Positioning yayımı K2 değildir (para yetkisi değil; yine aynı işlemde yazılır).
4. **Şirket kuralı (D10/1) genişletmesi:** "Evet, yazma işlemlerinde" — AP, AR ve Cash Positioning'de borç, alacak ya da
   nakit pozisyonu oluşturan/değiştiren her işlemde şirket aktif ve onaylı olmalı; okumalar etkilenmez.

## D15 — Pack düzeltme kararları (2026-10-24, soru kutusu; ikisi de önerilen seçenek)

1. **MOD-0120 teslim dilimleri:** "DL-4'ü DL-3 ile birleştir" — elle tahsilat kaydı (RecordPayment) DL-3'te, iptal/silme
   onayıyla birlikte; eski DL-5 (ekran) DL-4 oldu; O-9 (elle tahsilat bu sürümde mi) açık kalır.
2. **Reconcile muafiyeti:** "Evet, muaf" — belirsiz sonucu kapatan Reconcile komutları "şirket aktif ve onaylı" kuralından
   muaftır (D10/1, D14/4); yeni işlem başlatmazlar, kontrol edilmiş işlemin uzak sonucunu kaydederler (dört pack).

## Karar olmayanlar

MOD-0003 (0.3); AGENTS.md §2/§3/§9 ve ports.md düzenlemesi; 5069 kayması (SafetyStockService sahibi).

## Bu kayıt ne değildir

Kod, pack onayı, push / PR ya da başka bir Control Tower'ın dosyasına yazma izni değildir.
