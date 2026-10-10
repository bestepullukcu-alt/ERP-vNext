# Exit-code capture note

The first shell wrapper for the RED, targeted GREEN, build, and 78-test commands used zsh's wrong `PIPESTATUS[0]` index and produced blank `.exit` files. Those blank originals are retained as raw harness evidence. The command outcomes are unambiguous in the corresponding complete logs: RED is the expected test-process exit 1; targeted GREEN, build, and 78-test runs are exit 0. `COMMANDS.tsv` records these observed outcomes. Main/restart probes, restore, patch application, Mongo initialization, DB query, scans, and listener checks have direct numeric exit files.

No test was repeated merely to replace the blank wrapper fields.
