---
description: Diten ERP vNext lokal geliştirme ortamı için standart port atamaları ve çakışma çözümleri.
---

# Port Registry (Single Source of Truth)

## Amaç
Local development ve ileride environment’larda port çakışmalarını önlemek.
Yeni servis açarken “rastgele port” seçilmez. Diten ERP vNext vizyonuna sadık kalınır.

## Port Bandları
- **5000**: Gateway (Ocelot) — dev
- **5001**: Frontend (Diten.Web) — dev
- **5011–5064**: Microservice bandı (Backend servis portları) — band 5060'tan 5064'e genişletildi (CT kararı 2026-09-14; HumanCapitalService=5063, TalentEcosystemService=5064 kurtarma servisleri için).
- **7000+**: Dev tools / özel (mümkünse kullanılmaz; bazı tool’lar kapabilir)

## Aktif Kullanımlar (Şu an)
| Servis Adı | Port | Açıklama |
| :--- | :--- | :--- |
| **Diten.ApiGateway (Ocelot)** | `5000` | Tüm dış isteklerin karşılandığı ana kapı. |
| **Diten.Web (Frontend)** | `5001` | Sneat PRO, Razor Pages ve DataTables arayüzü. |
| **Diten.Auth.Api** | `5056` | Kimlik doğrulama, JWT ve RBAC yönetim servisi. |
| **Diten.Platform.API** | `5057` | Platform shared services ve personalization. |
| **Diten.DevEnablementService.Api** | `5058` | Golden reference ve developer enablement modülleri. |
| **Diten.MdmService.Api** | `5059` | Master data: tüzel kişilik, ürün, marka. |
| **Diten.HcmService.Api** | `5060` | Core HR / employment records. |
| **Diten.HumanCapitalService.Api** | `5063` | HCM readiness — applicant intake, onboarding, competency/development. |
| **Diten.TalentEcosystemService.Api** | `5064` | TEP — talent ecosystem / association & career readiness. ⚠ Bandın **son portu** (band 5064'e genişletildi — CT kararı 2026-09-14). |

> **Kural:** Frontend (5001) hiçbir zaman doğrudan servis portlarına istek atamaz. Frontend'in yapacağı tüm API çağrıları Gateway (5000) üzerinden geçmek ZORUNDADIR.

## Boş Port Seçme Kuralı (Yeni Servis Açarken)
1) Yeni servis microservice bandından seçilir: **5011–5060**.
   ⚠ **BANT DOLDU (2026-09-07):** 5060 HCM tarafından kullanılıyor. Yeni bir servis
   eklenmeden önce bandın genişletilmesi bir Control Tower kararıdır — boş bir port
   varsaymak, çakışan iki servisin sessizce birbirini beklemesiyle sonuçlanır.
2) Seçmeden önce kontrol:
   - `lsof -nP -iTCP:<PORT> | grep LISTEN`
3) Port boşsa bu dosyaya eklenir (Aktif kullanımlar listesine).
4) Servis portu ile Gateway upstream route birlikte eklenir (`routes.md`).

## Çakışma Çözümü (Troubleshooting)
- Port doluysa PID bulunur:
  - `lsof -nP -iTCP:<PORT> | grep LISTEN`
- PID kapat:
  - `kill -9 <PID>`
