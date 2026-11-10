# MOD-0040-FU02 — sahip kararları 04 (2026-11-10)

Paket r12 (`d29f9c5e…`, `36465315c`); bağımsız inceleme 04 tur 1 (`docs/records/reviews/MOD-0040-FU02/2026-11-10-d29f9c5e.md`, KOŞULLU).
Sahip: Beste (K2). r13 bu kararları işler.

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| F-1 | Depo arayüzüne eklenen yöntem listede olmayan test sahtelerini bozar | Yöntem ayrı dar bir arayüze taşınır (`IOrganizationReportingGraphRepository` emsali); yalnız iki Mongo deposu ve `InMemoryTenantOrganizationRepositories.cs` uygular. **XMC-001 ek onayı:** `services/Diten.Platform/tests/**/TenantOrganization/InMemoryTenantOrganizationRepositories.cs` (MOD-0288) — yalnız bu arayüzün uygulanması; başka değişiklik yok | Beste | 2026-11-10 |
| F-2 | POS'un şirketini okuma yolu | **(b)** — `CreatePositionCommandHandler` birimi ikinci kez `GetByIdAsync` ile okur; `PositionReferenceGuard.cs` değişmez; iki okuma arasında şirket değişimi §5.2 gereği kabul | Beste | 2026-11-10 |
| F-4 | Tür bağımsız kısa kod kilidinin ters yönü | DL-2 kodu yazılabilir; **A-18 (MDM kalıcı kısa kod kilidi, sözleşme N-12) kararı DL-2'nin canlıya alma kapısıdır** (Bölüm 18.1). İki yön ve "başka şirkette aynı önek" durumu pakete yazılır; Treasury CT ve MDM sahibine bildirim + kurtarma yolu | Beste | 2026-11-10 |
