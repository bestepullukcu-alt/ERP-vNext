# execution/ — Domain ve Module Execution Katmani

Bu klasor, Diten ERP vNext'in domain-odakli execution workspace'idir.

## Amac

`.antigravity/` global muhendislik standartlarini tanimlar ("nasil yapilir").
`execution/` ise her domain'in sinirlarini, kararlarini ve aktif modullerini tutar ("ne yapiliyor, nerede").

## Katman Ayrimi

| Katman | Yer | Icerik |
|--------|-----|--------|
| Global | `.antigravity/` | Tum domain'ler icin yeniden kullanilabilir kurallar, ajanlar, workflow'lar |
| Repo Kontrati | `AGENTS.md` | Repo genelinde protected paths, runtime kararlari, yetki hiyerarsisi |
| Domain | `execution/domains/{name}/` | Bir is alaninin sinirlari, kararlari, kontrolleri |
| Module | `execution/domains/{d}/module-packs/*.md` | Tek bir modulun kimligi, acceptance criteria, repo scope |

## Klasor Yapisi

```text
execution/
├── README.md
├── scripts/
│   └── generate-dashboard.py
└── domains/
    ├── master-data-management/
    ├── platform-shared-services/
    └── enterprise-strategy-business-performance/
```

Her domain altinda:

```text
{domain}/
├── README.md
├── domain-config.md
├── decisions/
│   ├── runtime-decisions.md
│   ├── ownership-decisions.md
│   └── deferred-items.md
├── controls/
└── module-packs/
    └── {DOMAIN}-{NNN}-{slug}.md
```

## Yetki Hiyerarsisi

`AGENTS.md` Bolum 1'de tanimli:

```text
Module Pack > Domain Config > AGENTS.md > .antigravity/
```

En spesifik katman kazanir.

## Module Pack Adlandirma

```text
{DOMAIN-KISA}-{NNN}-{slug}.md
```

- `DOMAIN-KISA`: `MDM` | `PSS` | `ESBP`
- `NNN`: 3 haneli sira numarasi
- `slug`: kucuk harf + tire

Ornekler:
- `MDM-001-currency-management.md`
- `PSS-001-identity-access.md`
- `ESBP-001-strategy-core.md`

> Not: `MOD-xxxx` formati kullanilmaz; bu format teknik standart ID'leri icin ayrilmistir.

## Forward-Only Strateji

Bu katman ileriye donuk calisir. Mevcut eski modullere geriye donuk module pack yazilmaz.
Bu tarihten sonraki yeni moduller veya major feature'lar module pack ile acilir.

## Batch / Snapshot Kullanilmiyor

- `batches/`: YOK. `/add-module` workflow'u phase orchestration saglar.
- `snapshots/`: YOK. Git history + `docs/audits/` yeterlidir.

## Kullanim Rehberi

Yeni bir modul acilirken standard akisi:

1. Domain sec (`MDM` / `PSS` / `ESBP`)
2. `module-packs/{DOMAIN}-{NNN}-{slug}.md` dosyasi olustur
3. YAML frontmatter + acceptance criteria doldur
4. Branch ac (`feature/{domain-short}/{id-lower}-{slug}`)
5. Orchestrator cagir ve `/add-module` workflow'unu calistir
6. `status` alanini `draft -> in-progress -> review -> done` akisiyla guncelle

## Dashboard Uretimi

Module pack durumunu tek ekranda gormek icin:

```bash
python3 execution/scripts/generate-dashboard.py .
```

Uretilen dosya: `execution/DASHBOARD.md` (gitignore'da tutulur, local artifact).

## SOP Referansları

Proje operasyonel standartları (SOP) şu dizin altındadır:
- `docs/sop/upstream/`
