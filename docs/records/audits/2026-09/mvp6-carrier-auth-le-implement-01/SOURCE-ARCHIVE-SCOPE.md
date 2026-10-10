# Source archive scope

`source-archive.tar.gz` is the immutable build-source snapshot for this work package.

- SHA-256: `a10bb93246557475d6518975aa0ee1dd8928ca08781fc2fb0cfbb04136463388`
- Entries: 2,507 files across the 17 transitive local projects needed by the Auth and Platform API builds, plus the exact MDM controller used by the F2 source-path finding.
- Included: Auth, Platform, Platform.Common, Platform.Contracts, the referenced Building Blocks projects, and the referenced PPM contracts project.
- Excluded: `.git`, build/test outputs, temporary build directories, development/local appsettings, and secret-bearing test fixtures.

The exclusions are evidence-safety measures. They do not alter the approved ten target files. `APPLIED-SOURCE-MANIFEST.tsv` remains the controlling changed-source manifest, and `BUILD-SOURCE-SET.sha256` records the writer build input inventory.
