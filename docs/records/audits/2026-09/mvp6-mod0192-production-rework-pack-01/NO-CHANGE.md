# Source preservation check

This work wrote only `docs/records/audits/2026-09/mvp6-mod0192-production-rework-pack-01/` in the main checkout. The main checkout was already dirty (208 status rows on entry); no clean-repository claim is made. The registered Capacity DEV worktree was read only.

After package generation, the registered worktree's promoted pack remained SHA256 `d01bf7a050472a9708a989d7bd4d13ab0a41a81984a272bbb15848f93fcac6e0`; published YAML/annex remained `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` / `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`; `CapacityRepository.cs`, `CapacityLeaseStore.cs` and `CapacityAtomicityTests.cs` remained `74153ee83d4f20c7b464558467025d5a58d7e594607c2b5e76a577d9f9c2d616`, `2912a032deb172909cf7ae8f0730f0c412b0f05239c32aba0b3cf06794517b78`, and `576bf4001c0573f9884012c9aaaffeb57ab4e658b4eddc3be16de25a7acd98b8`. The 43-path archive/worktree check is in `SOURCE-43-CHECK.tsv`.

Disposable patch application was limited to temporary directories and did not alter the registered worktree. `git status --short` in the main checkout counts unrelated concurrent dirty inputs, so only the exact path and hash checks above support this lane's no-change claim.
