# Dev Runbook (Local)

## Hedef
3 tab ile sistemi deterministik şekilde ayağa kaldırmak.

## “3 Tab” Kuralı
1) Tab-1: Service (ör: MDM)
2) Tab-2: Gateway
3) Tab-3: Test (curl)

## 0) Temizlik Kontrolü (opsiyonel ama önerilir)
```bash
lsof -nP -iTCP:5050 | grep LISTEN
lsof -nP -iTCP:5001 | grep LISTEN