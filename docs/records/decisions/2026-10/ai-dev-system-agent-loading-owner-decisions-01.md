---
decision_id: AI-DEV-SYSTEM-AGENT-LOADING-OWNER-DECISIONS-01
status: approved — the decisions below were in force on 2026-10-15; the "Open" section lists items that are NOT decisions
decided_at_local: 2026-10-14T12:38+03:00 to 2026-10-15T10:41+03:00 (each decision carries its own time)
decided_by: the owner of this work stream (the user of the AI session; this record adds no name)
decision_source: owner messages in the Claude Code session "AI" (accdcf92), 2026-10-14/15. Most texts were drafted by Codex and issued by the owner by pasting them. The session transcript is on the owner's machine, not in the repository.
scope: the AI development system — `.antigravity/` (agents, skills, rules, workflows), the generated tool adapters, `AGENTS.md`, `CLAUDE.md`
bound_to: commits 16d751f19 and e66404988 (PR #145, open, not merged); ea3729ddd and 88205c3d6 (branch chore/agents/i5b-agents-md-budget, local only, not pushed)
recorded_by: the AI session (Claude Code) on 2026-10-15, on the owner's instruction of 2026-10-15T12:53+03:00; this record makes no decision of its own
---

# AI development system, agent loading (İ5) — owner decisions in force

Every quotation is copied byte-for-byte from the owner's message, the source's spelling included. Decisions that were
later superseded or completed are not repeated; see the last section. Items still waiting for the owner are listed
under "Open" and are not decisions.

## 1. Approach B — controlled simplification

2026-10-14T12:52+03:00:

> Mevcut ERP AI geliştirme sistemi için mimari yaklaşım olarak B — Kontrollü Sadeleştirme prensipte seçildi.

Limits given with it, 2026-10-14T12:38+03:00:

> Ancak bu, önerdiğin bütün agent arşivleme, rol birleştirme ve orchestrator sadeleştirme kararlarının onaylandığı anlamına gelmiyor.

> Kanıtsız sadeleştirme istemiyoruz.

**Status:** in force. `.antigravity/` stays the single source; tool adapters are generated from it
(`.antigravity/scripts/sync_adapters.py`). No agent or skill has been deleted or merged.

## 2. İ5-A pilot — agent behaviour

2026-10-14T17:06+03:00:

> 2. security-agent'ın mevcut davranışı korunsun:
>    yalnız açıkça istendiğinde çağrılsın.
> 3. Otomatik tetiklemeyi artırma.
> 4. Antigravity doğrulaması açık kalsın.
> 5. Windows CI doğrulaması açık kalsın.

**Status:** items 2 and 3 in force (security-agent runs only when named). Items 4 and 5 are open; see "Open".

## 3. Git delivery of İ5-A and İ1-A.2b — one PR, no merge

2026-10-14T23:31+03:00:

> 1. add-module.md içindeki yanlış test adı
>    BL-644 kapsamında kalsın.
>    Bu PR'a ekleme yapma.
>
> 2. İ5-A ve İ1-A.2b için oluşturulan iki
>    yerel commit'i kabul ediyorum:
>    - 16d751f19
>    - e66404988
>
> 3. Tek PR açılmasını onaylıyorum.

> 8. PR'ı main'e birleştirme.
>    Merge için ayrıca onayımı bekle.

**Status:** in force. PR #145 is open with 16d751f19 and e66404988. Merging needs an approving review from another
write-access account and the owner's separate approval.

## 4. İ5-B — option C (compact core, single source, generated adapter)

2026-10-15T01:05+03:00:

> Karar: Seçenek C onaylandı.
> Önerdiğin kompakt çekirdek + tek kaynak + otomatik adaptör üretimi mimarisiyle ilerle.

Critical acceptance conditions given with it:

> * Güvenlik, yetki, tenant izolasyonu, Git tek-yazar ve kullanıcı onayı kuralları korunmalı.
> * Kompakt çekirdek yalnızca bilgilendirme özeti olmamalı; temel davranış kurallarını açıkça ifade etmeli.
> * Ayrıntı gerektiğinde ilgili kaynak belgelerin açılması için açık yönlendirme bulunmalı.
> * Salt anahtar kelime kontrolüyle yetinme; kritik davranışların korunduğunu mevcut sözleşme testleri ve davranış testleriyle doğrula.
> * Mevcut `AGENTS.md` bölüm numaraları ve belgelerdeki bağlantılar bozulmamalı.
> * Üretilen adaptörler elle değiştirilmemeli.
> * İ5-A'nın başarılı testlerinde gerileme olmamalı.
> * Test edilemeyen platform davranışlarını `NOT VERIFIED` olarak raporla.

Technical acceptance, 2026-10-15T02:26+03:00:

> İ5-A ve İ5-B teknik kabul testlerini tamamladık. Git entegrasyonları ayrı takip ediliyor.

Follow-up commit, 2026-10-15T10:41+03:00:

> Yerel commit `a6082e248` korunsun. İ5-B takip commit'i `88205c3d6` de kabul edildi.

**Status:** in force. ea3729ddd and 88205c3d6 exist only on the owner's machine (push held, see the Control Tower
record, decision 5).

## 5. Scope of the AI session — owner statement

2026-10-15T00:04+03:00, phrased as a question:

> bızım gorevımız bu degıl ama senle bız sadece .antgravty ve contol tower  ısınıyyapıyor bu dıger control towerın ısı degıl mı

**Status:** an owner statement, not a formal decision; applied since. The AI session works only on the AI development
system and the Control Tower process; product and security fixes belong to the module Control Towers.

## Open — NOT decisions

| Item | State on 2026-10-15 | Evidence |
|---|---|---|
| Antigravity loading of the İ5-A and İ5-B adapters | NOT VERIFIED: Antigravity has no headless CLI | `AGENTS.md` on the İ5-B branch is 20,364 bytes (limit 24,000) |
| Windows CI (decision 2, item 5) | The Windows job of `agent-adapters.yml` passed in PR #145 CI; the owner has not recorded the item as closed | PR #145 checks |
| PR #145 merge | Waiting for a write-access review and the owner's merge approval | — |
| İ5-B push and PR | Held: the repository is public | Control Tower record, decision 5 |
| `fix/mvp1-sec-esign1` | Adds "5070 Manufacturing" to the `CLAUDE.md` port block that İ5-B removes; the line must move to `AGENTS.md` §3 when that branch merges | — |
| BL-644 (wrong test name in `add-module.md`) | A separate work item (decision 3, item 1) | — |

## Not repeated here

- 2026-10-14T17:06+03:00, items 6 and 7 (no merge before the closure PR; no new commit, push or PR): the closure PR #144
  merged on 2026-10-14 22:18; decision 3 replaced both.
- İ1-A.1 and İ1-A.2a are delivered (PR #142, merged 2026-10-14). İ1-A.2b is commit e66404988 in PR #145.
