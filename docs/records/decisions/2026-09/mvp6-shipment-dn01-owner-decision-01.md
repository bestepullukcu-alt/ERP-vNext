---
decision_id: MVP6-SHIPMENT-DN01-OWNER-DECISION-01
status: approved (option A, refreshed text)
decided_at_local: 2026-09-26, ~14:36–14:48 +03:00 (approximate window given by CT; the minute of each single answer was not given)
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer in the MVP6 Control Tower conversation (in-app question tool), 2026-09-26
approvedBy: current-role-user-message-2026-09-26
bound_to: docs/roadmap/plans/mvp6-decision-prep-02/DN-01.md sha256 dd33f213f845241df453ecb17fef56b9ef7f8fb88ebcecf1aadd41eb0f70840a; docs/roadmap/plans/mvp6-decision-prep-02/SHA256SUMS sha256 7a22f9e414f482d7c46e45c5f936d50c1ba127ab9fe33a3baf9c7c32175e235d
recorded_by: AL-MVP6-REC-PACK-01 (Q71, chat lane) at 2026-09-26T14:45:53+0300 on CT instruction; CT writes no files
---

# MOD-0183 Shipment UI — DN-01 evidence-only fault proxy for UI183-A10 — option A

## Decision

Owner answer to DN-01 (queue Q10, blocker B03): **A — approve the refreshed text below.** The proxy runs **only inside an evidence kit v1.2 lane on the local Mac**.

## Exact decision text (source `docs/roadmap/plans/mvp6-decision-prep-02/DN-01.md` sha256 `dd33f213f845241df453ecb17fef56b9ef7f8fb88ebcecf1aadd41eb0f70840a`)

> UI183-A10 bağımsız doğrulaması için, yalnız disposable ve loopback-bound evidence ortamında (MVP6 evidence kit v1.2 lane'i; lane port slot'u içinde) çalışacak hash-bound test proxy'sini onaylıyorum. Proxy exact create/transition/POD isteklerinde (a) forward etmeden 500 veya 503 döndürebilir ve (b) upstream başarı yanıtını kaydettikten sonra ilk client yanıtını düşürebilir. Her senaryoda payload hash'i, Idempotency-Key, upstream sonucu, client gözlemi ve DB before/after kaydedilecek; proxy script'inin sha256'sı evidence'a yazılacaktır. Bu yetki production source, Program.cs, controller/handler/repository, shared probe, contract, guard veya operasyonel endpoint değişikliği vermez. Proxy yalnız lane portlarını kullanacak, hiçbir credential tutmayacak veya arşivlemeyecek, 27017'ye ve lane dışı hiçbir porta bağlanmayacak ve cleanup sırasında kaldırılacaktır.

## Consequences stated in the source

1. A chat lane may write the proxy script and its sha256 into the A10 lane evidence folder (text only).
2. The A10 VER runs on the local Mac under kit v1.2, after the kit's validation run (Q24b); .NET, Mongo and a browser are needed.
3. The kit v1.2 is installed but not yet validated (Q24a CT ACCEPTED — installed, not validated).

The quoted text is copied byte-for-byte from the source file named above it (including its `>` quote markers); the source file itself still carries its "NOT APPROVED — prepared text only" heading, which this record supersedes for the option chosen. Source files are not edited (K4).
