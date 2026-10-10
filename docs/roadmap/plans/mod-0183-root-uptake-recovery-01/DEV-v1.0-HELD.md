# MVP6-MOD0183-ROOT-UPTAKE-RECOVERY-DEV-01 / v1.0 — HELD

NOT DISPATCHABLE. Bu dosya bir scope/Phase1.5/runtime onayı değildir.
Önce AGENTS, domain, MOD0183 pack/CT bounded acceptance, SOP§17/22, repository/Mongo/tenant/JWT kuralları,
README, scope-and-phase-1.5.md, owner-provenance-decision.md, acceptance.md, prospective-paths.txt oku.

NE: Yetkilendirildiğinde yalnız exact prospective dosyalarda presence-aware detail read uygula.
NEDEN: typed Guid raw absence/nil ayrımını kaybeder; BSON parse historical provenance kanıtı değildir.
NASIL:
1. Fresh branch/HEAD/dirty + source/contract hash; explicit scoped runtime ve Phase1.5 onayını doğrula.
2. Exact contract target artifact/version/hash erişilebilir olmalı. Published canonical hedefi ile isolated approved candidate
   hedefini açık ayır. Unpublished hedefle çalışma ancak açık isolated DEV/VER yetkisiyle; canonical uptake iddiası yok.
3. Tek scoped raw read, ayrı raw-state/result, nil korunması, Detail explicit UUID/null; trace root kaynağı değil.
4. R01–R12 uygula; fresh build/unit, gerçek HTTP/JWT, scoped raw persistence ve iki-process restart kanıtı üret.
5. Existing regression ve relevant architecture tests sonuçlarını dürüst raporla. Eksik veya failed DB ölçümü PASS değildir.
6. Data-owner legacy rollout disposition isolated test başlangıç koşulu değildir. Test fixture'ı production provenance sayma.
7. Source/binary/process hashes, command exits, exact diff ve SOP§22'yi unique /private/tmp/mvp6-root-producer-dev-* altında teslim et.

YAPMA: Owned dışı source/test değişikliği, Program.cs, shared DI/auth/serializer,
Shipment/entity/storage değişikliği, mutation/replay/SourceIntake rewrite, new endpoint, history-mining, provenance marker,
backfill/migration, canonical/guard/pack/git mutation, rollout/DEV GO. Unknown/disputed history için algılanmayan per-record
null oracle yazma. Scope yetmezse exact rework/scope revision raporla; kendiliğinden genişletme.

DOĞRULA: absent≠nil; DTO'ya raw state kayıpsız ulaşır; aynı root farklı GET traces; tenant/LE/deletion; zero GET writes.
OUTPUT: DEV completion independent VER/CT acceptance değildir.
