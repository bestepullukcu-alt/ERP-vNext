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
- **5011–5060**: Microservice bandı (Backend servis portları)
- **7000+**: Dev tools / özel (mümkünse kullanılmaz; bazı tool’lar kapabilir)

## Aktif Kullanımlar (Şu an)
| Servis Adı | Port | Açıklama |
| :--- | :--- | :--- |
| **Diten.ApiGateway (Ocelot)** | `5000` | Tüm dış isteklerin karşılandığı ana kapı. |
| **Diten.Web (Frontend)** | `5001` | Sneat PRO, Razor Pages ve DataTables arayüzü. |
| **Diten.MDM.Api** | `5050` | Master Data Management (Countries, vb.) |
| **Diten.Auth.Api** | `5056` | Kimlik doğrulama, JWT ve RBAC yönetim servisi. |

> **Kural:** Frontend (5001) hiçbir zaman doğrudan MDM (5050) veya Auth (5056) servisine istek atamaz. Frontend'in yapacağı tüm API çağrıları Gateway (5000) üzerinden geçmek ZORUNDADIR.

## Boş Port Seçme Kuralı (Yeni Servis Açarken)
1) Yeni servis microservice bandından seçilir: **5011–5060**.
2) Seçmeden önce kontrol:
   - `lsof -nP -iTCP:<PORT> | grep LISTEN`
3) Port boşsa bu dosyaya eklenir (Aktif kullanımlar listesine).
4) Servis portu ile Gateway upstream route birlikte eklenir (`routes.md`).

## Çakışma Çözümü (Troubleshooting)
- Port doluysa PID bulunur:
  - `lsof -nP -iTCP:<PORT> | grep LISTEN`
- PID kapat:
  - `kill -9 <PID>`