# Evidence — CT-PQG-01, follow-up 01: the §36.1 template filled verbatim (2026-10-15)

Codex asked whether the SOP §36.1 template, filled in verbatim, passes the gate: four lines the gate reads carried a
trailing `# …` comment. 9e7297000 and its evidence (`README.md`, `checks.txt`, `run.sanitized.trx`) are not changed.

## Measured on 9e7297000

The template was filled with the real values of the end-to-end prompt (scratch clone, strict queue `pqg-ct`), each
template line kept as written, comments included:

    ENGEL biçim: 'Keşif:' satırı okunamadı (Keşif: <sonuç> · rapor <yol>@<commit> · sha256 <hex>)
    sonuç: engel — biçim                                                      (exit 1)

With the comment on `Risk:` the signal list would read "yok      # §20.2" (an unknown signal), and on `Keşif kararı`
lines the comment ends up inside the reason.

## Fix

- SOP §36.1: the trailing comments on `Risk:`, `Sözleşme yüzeyleri:`, `Keşif:` and `Keşif kararı` are removed; their
  content is a note under the template. The gate does not strip `#` comments: a line is read as it is.
- `prompt_gate.py --self-test`: a new case reads the template from the SOP and fails when a line the gate reads
  carries a trailing `#` comment. Written first: red on 9e7297000's SOP (70/71); green after the fix (71/71).

## After the fix

| Check | Result |
|---|---|
| The fixed template filled verbatim, same values, the gate of the end-to-end clone | exit 0, "hazırlık geçti — DAĞITIM İZNİ DEĞİL" |
| `prompt_gate.py --self-test` | 71/71 |
| The gate's runtime logic (everything above `def self_test`) against 9e7297000 | identical |
| SOP §36.2 to the end of the file, sha256, against 2481004b1 | 905cccd6ecd048a8dfb3bcc5f02615322a76232edbf49383d926039d55a6d06b · 905cccd6ecd048a8dfb3bcc5f02615322a76232edbf49383d926039d55a6d06b |
| `scripts/prompt_gate.py` sha256 | 2634355411cbdaa435d23fdd531917a0d59a530ae28190d1c864037c864e65cb |
| `docs/guides/operations/control-tower-sop.md` sha256 | 6fffd9047944301997582802ee4a05a7734f28dedb39404fa4e352fb581420c9 |
| Diff of the two files against 9e7297000, sha256 | 822fbed9e696329b7d73dcf2364783dad574b935eaeb3267b71a953b56710f31 |

## Architecture suite

Not re-run, by joint decision with Codex: the runtime logic is unchanged and only a SOP example and its self-test case
changed. The run in `README.md` (`run.sanitized.trx`, 131/138, 7 BİLİNEN) belongs to the tree pinned before
9e7297000; this follow-up's difference is the diff above.
