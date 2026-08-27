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
    ├── developer-enablement/                    # mevcut: Golden Reference pack'leri
    ├── enterprise-strategy-business-performance/ # governance scaffold; DCP-005
    ├── management-governance/                    # governance scaffold; DWS + BPM; DCP-006
    ├── master-data-management/                  # governance scaffold
    ├── platform-shared-services/                # mevcut: PSS pack'leri
    └── portfolio-delivery/                      # governance scaffold; MOD-0117
```

Her domain altinda:

```text
{domain}/
├── README.md
├── domain-config.md
└── module-packs/
    └── {ID}-{slug}.md
```

> Tarihsel `controls/`, `decisions/` ve `batches/` katmanlari `archive/domains/` altina tasinmistir. Engineering kurallari `.antigravity/rules/`, MVP scope ve modul envanteri `execution/portfolio/master-development-plan.md` uzerinden yurutulur. `docs/platform/master-plan.md` legacy bridge olarak gecici aktif kalir.

## Yetki Hiyerarsisi

`AGENTS.md` Bolum 1'de tanimli:

```text
Module Pack > Domain Config > AGENTS.md > .antigravity/
```

En spesifik katman kazanir.

## Module Pack Adlandirma

```text
{ID}-{slug}.md
```

- `DOMAIN-KISA`: `DEVEN` | `MDM` | `PSS` | `ESBP` | `MG` | `PPM`
- Yeni ERP product module ID: `MOD-NNNN`
- Follow-up ID: `MOD-NNNN-FUxx`
- Delivery Capability Pack ID: `DCP-NNN`
- Developer Enablement golden reference ID: `DEV-NNNN`
- `slug`: kucuk harf + tire

Ornekler:
- `MOD-0018-rbac-abac-authorization.md`
- `MOD-0018-FU12-tenant-authorization-context-foundation.md`
- `DEV-0000-golden-reference-slim.md`

> Registry notu: `execution/registries/module-id-registry.md` canonical kaynaktır. Tarihsel domain-prefixed veya legacy ID'ler migration boyunca geçerli kalır; toplu rename yapılmaz.

## Forward-Only Strateji

Bu katman ileriye donuk calisir. Mevcut eski modullere geriye donuk module pack yazilmaz.
Bu tarihten sonraki yeni moduller veya major feature'lar module pack ile acilir.

## Kullanilmayan Katmanlar

- `batches/`: YOK. `/add-module` workflow'u phase orchestration saglar.
- `snapshots/`: YOK. Git history + `docs/audits/` yeterlidir.
- `controls/`: YOK. Engineering standartlari `.antigravity/rules/`'dedir; arsivlendi.
- `decisions/`: YOK. Scope/MVP kararlari `execution/portfolio/master-development-plan.md`'dedir; `docs/platform/master-plan.md` legacy bridge olarak gecici aktif kalir; arsivlendi.

## Kullanim Rehberi

Yeni bir modul acilirken standard akisi:

1. Domain sec (`DEVEN` / `MDM` / `PSS` / `ESBP` / `MG` / `PPM`)
2. Registry-controlled `{ID}-{slug}.md` module pack dosyasi olustur
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
