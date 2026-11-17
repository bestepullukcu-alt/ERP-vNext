# MOD-0040-FU02 DL-2 FIX-01 — CT kabulü (2026-11-12)

- CT: MVP-1 CT (oturum `ca7382cf`; ölçüm CT'nin doğrulama alt ajanı, izole worktree, detached `fbb36feef`). Talimat: WP-0040-FU02-DL2-FIX-01.
- Hedef: `a3f329ece` (yalnız `OrganizationRecordCodeHttpTests.cs`, +214 / −19; üretim kodu değişmedi; `Skip` yok).
- Önceki kayıt: `mod-0040-fu02-dl2-ct-acceptance-2026-11-12.md` — KOŞULLU (ORTA-1 POS çakışma araması testsiz, CT-8 sabotajı yeşil;
  ORTA-2 AC-54 kısmi).
- Sonuç: **VERIFIED** — ORTA-1 ve ORTA-2 kapandı → **MOD-0040-FU02 DL-2 VERIFIED**. Canlıya alma kapısı ayrı: A-18 / sözleşme N-12 (MDM
  kısa kod kalıcı kilidi, Ali) — paket §22 DL-2 satırı.

## Eklenen vakalar
`AC55_pos_…`, `AC54a_…` (Theory: Suspended / Unavailable × ORG / POS), `AC54b_pos_…`, `AC54c_pos_…`, `AC54d_pos_…` — gerçek HTTP + gerçek Mongo.

## Sabotajlar (her biri ayrı, bayt-eşit geri; sha256 ile doğrulandı)
| # | Sabotaj | Test | Sonuç |
|---|---|---|---|
| B1 | CT-8: `PositionRepository` kod geçmişine `IsDeleted != true` süzgeci | `AC55_pos_a_legacy_position_code_…` | **kırmızı** (2026-11-12 öğlen yeşildi) |
| B2 | AC-54c POS: `CreatePositionCommandHandler` referans kontrolü reddi atlanır | `AC54c_pos_…` | kırmızı |
| B3 | AC-54b POS: kayıp yazımın uyarı logu yazılmaz | `AC54b_pos_…` | kırmızı |
| B4 | AC-54a: askıdaki kiracının ret satırı platform geneline yazılır (`AllocateRecordCodeHandler`) | `AC54a_…(SuspendedTenant, POS / ORG)` | kırmızı (2) |

## Takımlar (`fbb36feef`, süzgeçsiz; `DITEN_TEST_MONGOD` mongod 9.0; 27017 ve AccountKindAcceptanceHost kullanılmadı)
| Takım | Sonuç |
|---|---|
| Platform.Application | 9302 / 19 — yalnız bilinen 19 Mongo ortam kırmızısı |
| TenantArchitecture | 224 / 5 — `DocsPathGuardTests` ×5 (main tabanı) |
| Gateway | 371 / 0 |
| Platform.BackgroundJobs | 92 / 1 — `PlatformApiStartupExecutionModeTests.Actual_api_host_serves_health_…` (Release Platform API derlemesi ister; ortam) |

## Not
- AC54d_pos için ayrı sabotaj koşulmadı (ORG eşiyle aynı yapı, geçiyor) — DÜŞÜK, kabulü bekletmez.

VERIFIED
