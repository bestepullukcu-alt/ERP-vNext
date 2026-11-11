# PVG ikinci dalga — DoR (kart §1) kanıtı: WP-0230-DL2B ve WP-0230-DL3B

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır, dağıtım izni değildir.
> Dağıtım yalnız şu koşulla yapılır: kuyruk satırları `main`'e girdikten sonra `prompt_gate.py check` dağıtım kipinde (`--prepare` olmadan) çıkış 0 vermeli.
> Tarih 2026-10-26. CT dalı `chore/ct/pvg-ct-01`.
> Sahip kararları: `docs/records/decisions/2026-10/pvg-mod0230-dalga2-kararlari-01.md` @ 2e67261eb (K1–K6, "1 hepsi-evet").

| DoR maddesi | WP-0230-DL2B | WP-0230-DL3B |
|---|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" | aynı |
| Paket yetkisi | `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` revizyon 09 (`7dd6a8da…`) Expected HEAD 05f311784'te onaylı (#220, `pack_approval.py check` çıkış 0). Revizyon 10 bu PR ile onaya gider; ölçütler ve dilim yolları aynıdır. | aynı |
| Sözleşme | AC-21, AC-23, AC-25, AC-27, AC-29; prompt'ta satırıyla alıntılı (55 alıntı, Expected HEAD'de birebir) | AC-9; 17 alıntı, Expected HEAD'de birebir |
| Bağımlılıklar | WP-0230-DL2A main'de (#215); DP-3 gereği AC-22/24/26/28/30 kapsam dışı | WP-0230-DL3A main'de (#213), CT doğruladı, kuyrukta INTEGRATED (K6); AC-10 (OD-PVG-6) kapsam dışı |
| Kapsam | DL-2 yolları + K2: `PvgCaseIntakeTriageEndpoints.cs` içindeki iki `ToApplicationRequest` gövdesi ve `PvgServiceApiHostTests.cs` satır 41 | Yalnız `PvgServiceApiHostTests.cs`: sınıf sonuna ekleme, silinen satır 0 |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl2b`, dal `feature/pvg/wp-0230-dl2b`, Expected HEAD 05f311784, temiz | Worktree `.claude/worktrees/pvg-wp0230-dl3b`, dal `feature/pvg/wp-0230-dl3b`, Expected HEAD 05f311784, temiz |
| Yürütme / şerit | K5: DL3B ile paralel. İkinci biten iş `origin/main`'i birleştirir ve kontrolleri yeniden koşar. Risk HIGH. | K5: DL2B ile paralel. Risk HIGH (yetki, uyum). |
| Hedef ajan | prompt'un ilk satırı | prompt'un ilk satırı |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL2B-v1.3.md` sha256 `39865c66…` @ 6d556ab88 | `…/prompts/WP-0230-DL3B-v1.1.md` sha256 `14b06b5b…` @ 6d556ab88 |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0230/prompts/WP-0230-DL2B-v1.3-inceleme-02.md` @ ee8482ce4 (inceleyen 15). Kritik 0, engelleyici 0. | `…/WP-0230-DL3B-v1.1-inceleme-02.md` @ ee8482ce4 (inceleyen 13). Kritik 0, engelleyici 0. |
| Kapı (`--prepare --review`) | Tek ENGEL: "kuyruk PLANNED" (bu kayıtla READY olur) | aynı |
| Kabul | AC-21/23/25/27/29 testleri ve sabotajları. **Kanıt sınırı:** DL-2 gerekli seviye E4, bu iş E2 üretir. Resmî kabul canlı kapıya kadar açık kalır (KANIT-SEVIYESI-01). | AC-9 testleri ve sabotajları. **Kanıt sınırı:** DL-3 gerekli seviye E3, bu iş E2 üretir. Kabul açık kalır. |
| Keşif | `docs/records/audits/2026-10/pvg-m1-01-discovery-02/MOD-0230-case-intake-triage.txt` @ 4ebe4b15f | aynı |

## Kabulde ayrıca bakılacak inceleme notları ("diğer")

### WP-0230-DL2B (inceleme 15)

1. **K5 sonrası ölçütler:**
   - DL3B dosya başına `using` eklerse "yalnız satır 41" ölçütü 41+k olarak okunur.
   - "origin/main'deki Api sayısı"nı CT aktarır.
   - Birleştirmede çakışma çıkarsa işçi durur; CT çözer.
2. **Update yolunda saklama biçimi:** AwarenessAtUtc'nin UTC'ye çevrilmesi ve kırpma yalnız Create'te yapılıyorsa testler bunu yakalamaz. Kabulde CT, Update yolunu ayrıca ölçer.
3. **ChangedFields:** ProductNameAsReported ve ProductRole değişikliği ile satır sırası ölçülmüyor. Kabulde CT ayrıca ölçer.

### WP-0230-DL3B (inceleme 13)

1. **Kısmi gerçekleştirmeler testlerden geçebiliyor:**
   - Anahtarı büyük/küçük harf duyarlı eşleyen tarayıcı.
   - `CultureInvariant` olmadan `IgnoreCase` (Türkçe kültürde "I/ı" yüzünden anahtar kaçar). Kabulde CT, tr-TR kültürüyle koşu yapar.
   - "production"ı Ordinal karşılaştıran tarayıcı.
   - `.git` hariç tutmasının ölçülmemesi.
2. **Fail-open okumalar:** Küçük harfli `aspnetcore_environment` anahtarı ve yorum satırındaki "ASPNETCORE_ENVIRONMENT is …" ifadesi. Bugün etkisi yok; Known gaps'te olmalı.
3. **T7 CI'da yalnız R1 ortamında koşuyor.** Program.cs metnine bir iddia eklenmesi önerildi; CT kabulde ister.
4. **Metin çelişkisi:** YAPMA "docs/⟨klasör⟩/" dizesini yasaklıyor, T2 ve T8 ise "docs/records" dizesini istiyor. Guard izin veriyor; çelişki yalnız metinde.
5. **K5 birleştirme adımı** işçiye talimat olarak yazılmadı. CT, `origin/main` ile yürütür.
6. **T10'un sınırları** (kök kullanıcı, Windows) Known gaps'te olmalı.
