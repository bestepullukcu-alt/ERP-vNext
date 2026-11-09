# WF-S17-DL1 — taban çizgisinde durma 01 (2026-10-31)

- İşçi: İş akışı › Kayıtları silmeme (işçi), prompt v1.5 (sha256 `bdd36aca…`), HEAD `3096aadda`, kod değişikliği ve commit yok.
- Durma koşulu: "Taban çizgisinde bilinen borç dışında kırmızı → değişiklik yapmadan DUR."
- CT yeniden ölçümü: işçinin TRX'i `known_failures.py check` ile okundu, sonuç aynı (çıkış 1).

## Ölçüm

| Koşu | Sonuç | `check` |
|---|---|---|
| Platform, filtre `FullyQualifiedName~Workflow` | 877 geçti, 0 kaldı | `platform-app` çıkış 0 |
| TenantArchitecture | 146 toplam, 143 geçti, 3 kaldı | çıkış 1 (BİLİNEN=0, KISMEN-DÜZELDİ=1, YENİ=0, YENİ-AYRINTI=2) |

Parmak izleri (`known_failures.py fingerprint`):

```text
items:5:d9773291e8efe9fa  AuditTrailStandardTests.EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous
items:55:6ee8c6dc31c3a246 AuditTrailStandardTests.EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt
msg:13385a5e6ed357ef      DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders
```

- Kayıtta olmayan iki öğe: `Diten.Platform: RegisterSignableActionsCommand` ve `Diten.Platform: VerifySignatureCommand` (`Features/ESignature/ESignatureRequests.cs`). İş akışı işi değil.
- `DocsPathGuard` mesaj parmak izi kayıttakinden farklı (`msg:439e2be27b6b5cdc`). Hangi satırın yeni olduğu ölçülmedi.
- `tests/known-failures*` son 2026-10-19'da güncellendi; Expected HEAD ile `origin/main` (`763e1062a`) arasında aynı.
- Bağımsız doğrulama: ana dal CI'ı `phase1-gates`, çalışma 41136676851, `763e1062a`: `scripts/run_phase1_gates.sh:237` bu süiti koşuyor; aynı üç test kırmızı, 146 toplam, 143 geçti, 3 kaldı. Yani kırmızılar ana dalda da var ve aynı sayıda; bu CI işi bu yüzden kırmızı. Parmak izleri CI tarafında ölçülmedi, yalnız ad ve sayı karşılaştırıldı.

## CT kararı

Prompt v1.6: mimari ölçüt "çıkış 0" yerine "üç satırlık parmak izi tabanı birebir aynı; yeni kurallar yeşil". Bu gevşetme değil, daha sıkı bir ölçüdür: `check` kayıtlı öğede eksilmeye izin verir, parmak izi eşitliği vermez.

Kayıt tazeleme ve e-imza komutlarının denetim açığı bu işin kapsamı dışındadır (XMC-001); ayrı iş olarak sahibe önerilir. Taban TRX'leri geçici klasörde, kanıt olarak yukarıdaki parmak izleri yeterlidir.
