# Eleven MUST FIX — Document Claim Validation Report (H)

> Kural: Koddan doğrulanamayan compliance iddiası TRUE sayılmaz. "Validated" yalnızca validation plan/report, onaylı protokol, IQ/OQ/PQ, controlled release, traceability, deviation/CAPA, yetkili e-imza varsa kabul. Bunlar **repo'da yok** → tavan verdict UNPROVEN.

## Temel kod bulguları (tüm iddiaların dayanağı)

1. **"Di10-PV" = uygulama/marka adı, veritabanı DEĞİL.** Kanıt: sadece `Account/login.cshtml`, `forgetpassword.cshtml`, `ResetPassword.cshtml` başlıkları ("Welcome to Di10-PV"), `_EmptyLayout/_Layout` OG-meta, ve `GetGoogleContactsHandler.cs:75 ApplicationName="Di10-PV"`. **"Di10-PV" adında koleksiyon/DB/connection yok.**
2. **Gerçek persistence = 5 ayrı MongoDB** (`PvOrganization`, `PvTenant`, `PvUser`, `PvLookup`, `PvSurvey`), hepsi `mongodb://localhost:27017`. Safety data → `PvOrganization` DB, `SafetyReport` koleksiyonu.
3. **Reconciliation kodda = LCPPV Monthly Reconciliation** (`LcppvMonthlyReconcilationController` + anket). Veritabanı-veritabanı vaka mutabakatı **yok**.
4. **FSAD** terimi kod/config/deployment'ta **hiç yok** (0 eşleşme).
5. **Validation kanıtı yok:** IQ/OQ/PQ, validation package, controlled release, traceability, deviation/CAPA — hiçbiri repo'da bulunamadı.

## Verdict tablosu

| # | Document | Claim | Code/runtime evidence | Actual state | Verdict | Severity | Required correction |
|---|---|---|---|---|---|---|---|
| 1 | AGR-0001 v0.4 | "validated shared instance / the validated Di10-PV database" | Di10-PV = marka adı; ayrı DB yok; validation kanıtı yok | Safety data tek Mongo `PvOrganization` DB'sinde, tenant=host-based mantıksal ayrım; validasyon kanıtı yok | **UNPROVEN** (validated kısmı **FALSE**) | Yüksek | "validated" ifadesini kaldır veya QMS validation kanıtına referansla; "database" yerine mantıksal store tanımı |
| 2 | RSK-0001 v0.4 | "two MAHs ... share the Di10-PV database" | Tenant izolasyonu `TenantId` string + host-based; "paylaşım" tenant konfigürasyonuna bağlı | İki MAH aynı fiziksel Mongo DB'de ayrı `TenantId` ile olabilir; bu "shared validated database" değil, mantıksal multi-tenant | **PARTIALLY_TRUE** | Yüksek | "shared database" ≠ "shared validated instance"; tenant-ayrımı netleştir |
| 3 | SOP-0015 v0.3 | reconciliation "against the validated Di10-PV database" | Kodda reconciliation = LCPPV anketi | DB-mutabakatı diye bir mekanizma kodda yok | **FALSE / UNPROVEN** | Yüksek | Reconciliation'ın gerçek kaynağını (hangi iki liste/sistem) tanımla; LCPPV ile karıştırma |
| 4 | MTX-0006 v0.3 | "Single validated safety database of record for both systems" | Tek `PvOrganization` Mongo DB safety verisini tutuyor; "validated" kanıtı yok; "both systems" belirsiz | Tek mantıksal safety store var ama validated değil | **UNPROVEN** | Yüksek | "validated" iddiasını kanıtla veya çıkar; "both systems"i tanımla |
| 5 | FRM-0008 v0.1 | "Source of truth — reconcile against the validated Di10-PV database" | Aynı; reconcile mekanizması kodda LCPPV anketi | SoT olarak `PvOrganization` DB olabilir ama validated/reconcile iddiası kanıtsız | **UNPROVEN** | Orta-Yüksek | SoT'yi netleştir; validated/reconcile dilini düzelt |
| 6 | SOP-0003 v0.4 | "No case exists only in a spreadsheet tracker" + Di10-PV EU KPI kaynağı | Case verisi Mongo'da; **KPI/read-model kodda YOK** | Case'ler DB'de tutuluyor (spreadsheet iddiası kısmen destekli) ama KPI motoru yok | **PARTIALLY_TRUE** | Orta | KPI kaynağının kodda olmadığını belirt; EU KPI üretimini tanımla |
| 7 | SOP-0001 v0.7 §6.14 | spreadsheet iddiası + "Turkey — oversight of the standalone FSAD" | FSAD kodda yok; standalone Türk sistemi kodda yok | FSAD/standalone Türk sistemi bu repolarda mevcut değil | **CANNOT_VERIFY / UNPROVEN** | Yüksek | FSAD'ın ne olduğunu ve nerede olduğunu belgele; kod dışıysa açıkça yaz |
| 8 | PLN-0003 v0.2 | audit programı "the shared safety database (Di10-PV)" her iki sistemi kapsıyor | Kodda audit trail YOK; "shared" mantıksal | Denetim programı iddiası kod audit yokluğuyla çelişir | **INTERNALLY_CONTRADICTORY** | Yüksek | Kodda audit trail olmadığını kabul et; audit kapsamını yeniden tanımla |
| 9 | AGR-0005 v0.1 | "Turkish MAH operates a standalone local Turkish system with its own local master file" | Standalone Türk sistemi/master-file kodda yok | Bu repolarda standalone Türk sistemi kanıtı yok | **CANNOT_VERIFY** | Orta | Sistemi/master-file'ı harici olarak belgele veya iddiayı düzelt |
| 10 | LOG-0001 v0.7 | "Replace the Excel tracker with the validated Di10-PV database" | Di10-PV = marka; DB validated değil; Excel→DB göçü kodda otomasyon yok | Case DB'de tutulabilir ama "validated" kanıtsız | **UNPROVEN** | Orta-Yüksek | "validated" çıkar; Excel emekliye ayırma sürecini kanıtla |
| 11 | (11. / çapraz çelişki) | "Di10-PV" tutarlı bir "validated database" olarak kullanımı | Kodda Di10-PV yalnızca marka; 5 ayrı DB; validation yok; reconciliation=anket; FSAD yok | Dokümanlar boyunca "Di10-PV" **tutarsız** biçimde application/deployment/database anlamında kullanılıyor | **INTERNALLY_CONTRADICTORY** | Yüksek | Tek bir tanım seç: "Di10-PV" = uygulama markası; safety store'u ayrı adlandır |

## Doğrudan sorulara cevaplar

- **Gerçekte tek validated shared Di10-PV database var mı?** Hayır. "Di10-PV" marka adı; safety data tek Mongo `PvOrganization` DB'sinde; **validated kanıtı yok**.
- **İki MAH aynı fiziksel/mantıksal DB'yi mi kullanıyor?** Muhtemelen aynı fiziksel Mongo instance, `TenantId` ile mantıksal ayrım — ama bu "shared validated database" değildir.
- **Tenant ayrımı ile shared database aynı şey mi?** Hayır. Tenant ayrımı mantıksal izolasyondur; "shared validated database" farklı (ve kanıtsız) bir iddiadır.
- **Türkiye ayrı standalone sistem mi kullanıyor?** Bu repolarda kanıtı yok (CANNOT_VERIFY).
- **FSAD nedir ve kod/deployment'ta var mı?** Kodda hiç yok. Kod dışı bir kavram/sistem olabilir; belgelenmeli.
- **Excel tracker hâlâ operasyonel source of truth olabilir mi?** Kod, case'leri DB'de tutabildiğini gösteriyor; ancak KPI/read-model yokluğu bazı çıktıların hâlâ Excel'de üretilebileceği anlamına gelebilir (UNPROVEN).
- **Reconciliation gerçekte hangi iki kaynak arasında?** Kodda yalnızca LCPPV **aylık anketi** var; DB-DB mutabakatı yok. Gerçek kaynaklar belgelenmeli.
- **"Validated" iddiasını destekleyen kanıt var mı?** Hayır — validation package/IQ-OQ-PQ/controlled release/e-imza repo'da yok.
- **Kodun var olması validasyon kanıtı sayılır mı?** **Hayır.** Kodun çalışması ≠ validated system. Açıkça belirtilmiştir.
- **Di10-PV ürün adı mı, deployment mi, database mi, application mı?** Kodda **application/brand adı**. Dokümanlarda tutarsız kullanılıyor (INTERNALLY_CONTRADICTORY).

**Genel:** 11 iddianın hiçbiri koddan TRUE değil. Dağılım: FALSE/UNPROVEN ağırlıklı, 2 INTERNALLY_CONTRADICTORY, 2 CANNOT_VERIFY, 2 PARTIALLY_TRUE. Doküman iddialarının kanıtlanma oranı **%0–10 (High confidence)**.
