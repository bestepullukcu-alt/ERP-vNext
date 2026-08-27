---
description: Diten ERP vNext lokal geliştirme ortamı için standart port atamaları ve çakışma çözümleri.
---

# Port Selection and Collision Rules

## Amaç
Local development ve ileride environment’larda port çakışmalarını önlemek.
Yeni servis açarken “rastgele port” seçilmez. Repo-specific canonical tahsis tablosu
[`AGENTS.md` §3](../../AGENTS.md#3-port-%C5%9Femas%C4%B1) içindedir. Bu dosya tahsis otoritesi değil,
port seçme ve çakışma önleme kuralıdır.

## Port Bandları
- **5000**: Gateway (Ocelot) — dev
- **5001**: Frontend (Diten.Web) — dev
- **5011–5099**: Microservice bandı (backend servis portları; yalnız `AGENTS.md` §3 kaydıyla tahsis edilir)
- **7000+**: Dev tools / özel (mümkünse kullanılmaz; bazı tool’lar kapabilir)

## Canonical Tahsis Otoritesi
- Aktif, planlı ve fallback servis tahsislerinin tamamı `AGENTS.md` §3'te tutulur.
- Domain config ve module pack'ler topology gerekçesini ve activation koşullarını tanımlayabilir; ikinci bir
  global port tablosu oluşturamaz.
- `Planned` port kaydı service scaffold veya runtime authority değildir.

> **Kural:** Frontend (5001) hiçbir zaman doğrudan servis portlarına istek atamaz. Frontend'in yapacağı tüm API çağrıları Gateway (5000) üzerinden geçmek ZORUNDADIR.

Yeni veya değişen Ocelot rotasını yalnız `integration-agent`, açık route/runtime authority sonrasında ekler.

## Boş Port Seçme Kuralı (Yeni Servis Açarken)
1) Yeni servis microservice bandından aday seçer; mevcut canonical tahsisler için önce `AGENTS.md` §3 okunur.
2) Seçmeden önce kontrol:
   - `lsof -nP -iTCP:<PORT> | grep LISTEN`
3) Port boş olsa bile kendiliğinden tahsis edilmiş sayılmaz; kullanıcı/owner onayıyla `AGENTS.md` §3'e eklenir.
4) Gateway upstream route ancak ilgili servis runtime authority aldıktan sonra ve `integration-agent` üzerinden
   eklenir (`routes.md`).

## Çakışma Çözümü (Troubleshooting)
- Port doluysa PID bulunur:
  - `lsof -nP -iTCP:<PORT> | grep LISTEN`
- Mevcut PID, kullanıcı açıkça izin vermeden kapatılmaz veya yeniden başlatılmaz. Önce canonical tahsis ve
  çalıştırılan profile doğrulanır.
