---
status: approved
decided: 2026-10-27
module: MOD-0230
decisions: OD-PVG-6 (uygulama biçimi)
---

# PVG-MOD0230-RTA-01 — Canlı çalışma onayının kayıt biçimi

> **Karar sahibi:** sahip, Enterprise Architecture sıfatıyla (MIMARI-01). Sahibin sözü: "kayit-karar" (2026-10-27, PVG Control Tower sohbeti).
> **Kaydı tutan:** PVG Control Tower.

## Ölçüm

ODPVG6-01 iki şey istiyor: bayrak açıkken `Pvg:Runtime:ApprovalReference` dolu olmalı, ve CI bu değerin "depodaki onay kayıtlarında" bulunduğunu ölçmeli. Ölçüm 2026-10-27'de CT dalı `17b7ff220` üzerinde yapıldı:

- `execution/approvals/approvals.tsv` yalnız paket onaylarını tutuyor. Sütunları `pack` ve `pack_sha256`; kimlik sütunu yok. `pack_approval.py` ve yalnız-ekleme kuralı bu dosyaya bağlı.
- Paketteki "Runtime Authorization Packet" bir taslak. "Operational runtime authorization row" ayrı bir kayıt değil; paketin içinde işaretlenmemiş bir madde.
- Hiçbir PVG yapılandırmasında `ApprovalReference` değeri yok.

## Karar

1. Canlı çalışma onayı (MOD-0230 operasyonel runtime yetkisi), `docs/records/decisions/` altında bir karar kaydı olarak yazılır. Kayıtta iki künye alanı bulunur: `status: approved` ve `runtime_approval_id: RTA-MOD0230-NNN`. `NNN` üç haneli bir sıra numarasıdır.
2. `Pvg:Runtime:ApprovalReference` değeri bu kimliktir, örneğin `RTA-MOD0230-001`.
3. CI denetimi commit edilmiş her `ApprovalReference` değerini alır. `docs/records/decisions/` altında, künyesinde bu `runtime_approval_id`'yi ve `status: approved`'u taşıyan tam bir kayıt bulunmalıdır. Herhangi bir başka karar kimliği yetmez.
4. Yeni bir yönetişim dosyası açılmaz. `execution/approvals/` klasörüne dokunulmaz.
5. Bu kayıt canlı çalışmayı onaylamaz. Bugün hiçbir `runtime_approval_id` yok; operasyonel runtime kapısı kapalı kalıyor.

## Uygulama

WP-0230-DL3D (AC-10) bu biçimi kullanır. Kaydı okuyan denetim, testin adı ve sabotajlar prompt'ta yazılır.

## Gerileme değerlendirmesi

Kod değişmiyor. Karar kayıtları zaten değiştirilemez (K4), bu yüzden bir onayın sonradan sessizce değiştirilmesi de engellenmiş olur.
