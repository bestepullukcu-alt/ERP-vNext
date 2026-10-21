# DC-02 — XMC-001 question for the MOD-0140 (Procurement) owner (v0.1)

    created : 2026-10-21 · status: DRAFT, not sent · measured: origin/main 9d44e8a1f (read-only)
    basis   : DECISION-DRAFT.md §4.0 (Natig's supplier model, 2026-10-21) and §5a (F10–F17)

Format: `.antigravity/rules/cross-module-change-consent.md` §1. SCE asks; nothing in `Diten.ProcurementService` is
touched until the answer. Paths below are under `services/Diten.ProcurementService/src/`.

> **Başka modülde düzeltme gerekiyor — bunu da düzeltelim mi?**
> - **Modül:** MOD-0140 Supplier Onboarding (KYC/Sanctions/Docs) — `Diten.ProcurementService` · **Dosya:** `Domain/Entities/Supplier.cs:6-21,76-125`, `Persistence/Repositories/SupplierRepository.cs:141-148`, `Persistence/SupplierIndexConfiguration.cs:21-39`, `…/ValidateSuppliersHandler.cs`, `…/SubmitOnboardingCaseHandler.cs:46-83`, `Api/Program.cs:38-60`, `Infrastructure/Middleware/TenantResolutionMiddleware.cs:77-109`
> - **Sorun:** Tedarikçinin "uygun mu?" sorusuna bugün anlamlı bir cevap yok: durum yalnız oluşturmada `Active` yazılıyor ve hiç değişmiyor; hiçbir işlem (PO, sözleşme, fatura, RFx, teklif) durumu okumuyor; onboarding hiçbir zaman `Approved`/`Rejected` olmuyor (iş akışı kimliği sahte `WF-xxxx`); `validate` onayı döndürmüyor. Ayrıca kayıt Kiracı + LE başına tutuluyor — yönetimin 2026-10-21 kararıyla çelişiyor.
> - **Neden şimdi:** MOD-0147 (Supplier Performance) ve MOD-0148 (Supplier Portal) her yazmadan önce bu cevaba dayanacak (DC-02). MOD-0147, MOD-0216 TPRM bittikten sonra başlıyor; aşağıdaki iş o zamana kadar bitmeli.
> - **İstenen (sizin tasarımınız, bizim beklentimiz):**
>   1. **Model (karar verildi — Natig, 2026-10-21):** kiracı düzeyinde tek tedarikçi ana kaydı; **onay durumu, ödeme koşulları ve satın alma ilişkisi LE başına**. Bugünkü LE başına kayıtların ana kayda birleştirilmesi ve eski LE başına kimliklerden ana kimliğe bir **eşleme** (MOD-0147/0148 ana kimliği tutacak).
>   2. **Onay ve durum yaşam döngüsü:** onboarding'in gerçek iş akışıyla (MOD-0023) `Approved`/`Rejected`'a geçmesi; `Active`/`OnHold`/`Blocked`/`Inactive` geçişleri için komutlar; kiracı düzeyinde bir engel (ör. yaptırım) varsa hangi düzeyde tutulacağı. Procurement'ın kendi işlemlerinin (PO, fatura…) durumu dikkate alıp almayacağı sizin kararınız.
>   3. **Sözleşme halefi (`supplier.openapi.yaml` v1.3.0):** `validate` her `supplierId` için ana kayıt kimliğini, çağıranın LE'sindeki ilişkinin onay durumunu ve yaşam döngüsü durumunu ayrı alanlarla dönsün. v1.2.0 `FROZEN`; halef sizin.
>   4. **Servis kimliği:** SupplyChain'in kendi kimliğiyle sorabilmesi için Procurement'ta yalnız `validate` için bir servis-token şeması (Platform'daki `TrustedServiceTokenValidationExtensions` biçimi) — Auth tarafında yeni audience ve istemci kaydı Auth sahibinin işi (BL-652 Seçenek 2 ile aynı desen).
>   5. (Daha önce iletildi — C2) Başlıkla gelen LE'nin kiracıya ait ve aktif olduğunun MDM'le doğrulanması; ayrıca kiracının varlığı bugün kontrol edilmiyor.
> - **Risk:** 🟡 — ertelenirse MOD-0147/0148 tedarikçi uygunluğunu doğrulayamaz; ilk dilim (C8) "bilinen tedarikçi" kontrolünden öteye geçemez ve onaylanmamış tedarikçilere puan/portal açılabilir. Tenant izolasyonu bugün de korunuyor (kiracılar arası sızıntı yok).
> - **Seçenekler:** (a) bu işte, SCE yazar, siz incelersiniz · (b) ayrı iş, Procurement ekibi yazar · (c) şimdilik bırak, kayda geç
> - **Önerim:** **(b)** — model, yaşam döngüsü ve sözleşme MOD-0140'ın sistem-kaydı sorumluluğu; birleştirme ve kimlik eşlemesi veri göçü ister. SCE, sözleşme halefini ve servis kimliğini tüketen okuyucuyu (`ISupplierEligibilityReader`) ve testlerini yazar. Sıra önerisi: 1 → 2 → 3 → 4; 5 bağımsız.

## Notes for the sender

- Item 1 is an owner decision already made (Natig); items 2–5 are the Procurement owner's design within it.
- The DC-02 draft itself stays `TASLAK` until D2–D6 are decided; D2 jointly with this owner.
- Open in the plan: plan v10.0 C1/C3; DECISION-DRAFT §4.0, §5a.
