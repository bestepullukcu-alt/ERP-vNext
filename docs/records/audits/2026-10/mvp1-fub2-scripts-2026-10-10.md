# WP-MVP1-FUB2 — çalışma zamanı yığını, prob ve sabotaj betikleri (2026-10-10)

> Kanıt: `mvp1-fub2-evidence-2026-10-10.md` §4.2, §5, §6. Tek seferlik kanıt betikleri, repo kodu değil.
>
> **Yığın.** Q5 yığınından türetildi (`mod-0175-q5-scripts-2026-10-09.md`, "Program.cs").
> 1. O `Program.cs` dosyasını `FUB2Stack.csproj` ile aynı klasöre koyun.
> 2. `adapt.js` dosyasını çalıştırın. Yeri `pm-block.cs.txt` ile aynı klasör. Değişiklikleri:
>    - DB adları `itest_fub2_*`;
>    - Prism yerine **kendi** `pm-stub.js`'im (PM1 betikler belgesi §2, değişmeden) 14036'da, `--max-http-header-size=65536` ile, tek kalemli bir dünyayla;
>    - C kullanıcısı yok;
>    - iki karantina kaydı (1200 ve 300 EA);
>    - QA rolüne `auth.users.lookup` açıkça verilir (FU-0175-ITEM-02 BLOCKED).
> 3. Worktree'nin AuthService, Platform, Gateway, InventoryService ve Web projelerini **Release** derleyin.
> 4. `dotnet run -- <worktree> <çıktı> <pm-stub.js yolu>` çalıştırın.
>
> **Durdurma.** `<çıktı>/stop` oluşturulunca yığın yalnız kendi PID'lerini kapatır ve `pw.txt`'yi siler.
>
> **Prob.** Sırasıyla `node probe.js <çıktı> holds | check-note | decide 1 | decide 2 | check-open 1 | check-open 2 | replay`.
> - Tarayıcı adımları aralarda yapılır: not düzenleme, kilit ekranı, WorkCenter ret / onay.
> - `drafts.js` ilk (başarısız) `decide` koşusunun taslak kararlarını yeniden kullanmak için yazıldı.
> - `state.js` karar ve kayıt durumunu basar.
>
> **Sabotaj.** `node sabotage.js <worktree> <çıktı>`. Her sabotaj `git checkout -- <dosya>` ile geri alınır ve porcelain kontrol edilir.

## stack/FUB2Stack.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="EphemeralMongo.Core" Version="1.1.3" />
    <PackageReference Include="MongoDB.Driver" Version="2.27.0" />
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
  </ItemGroup>
</Project>
```

## stack/adapt.js

```js
// turns the Q5 stack (mod-0175-q5-scripts-2026-10-09.md, Program.cs) into the FUB2 one (exact replacements)
const fs = require('fs');
const p = __dirname + '/Program.cs';
let s = fs.readFileSync(p, 'utf8');
const rep = (a, b, all) => {
  const n = s.split(a).length - 1;
  if (all ? n < 1 : n !== 1) throw new Error(`anchor x${n}: ${a.slice(0, 80)}`);
  s = all ? s.split(a).join(b) : s.replace(a, () => b);
};
const lines = (from, count, replacement) => {
  const ls = s.split('\n');
  const at = ls.findIndex((l) => l.includes(from));
  if (at < 0) throw new Error('line ' + from);
  ls.splice(at, count, replacement.replace(/\r?\n$/, ''));
  s = ls.join('\n');
};

rep('itest_q5_', 'itest_fub2_', true);
rep('// WP-0175-Q5 runtime stack (WP-0023-FX method).', '// WP-MVP1-FUB2 runtime stack (from the WP-0175-Q5 stack; PRODUCT-MASTER = the OWN pm-stub.js of WP-0173-PM1 on 14036, not Prism).');
rep('var testPassword = "Q5!"', 'var testPassword = "Fb2!"');
// PRODUCT-MASTER: the own pm-stub.js (node --max-http-header-size=65536) with a one-item world instead of Prism (3 lines)
lines('var prism = Directory.GetFiles(', 3, fs.readFileSync(__dirname + '/pm-block.cs.txt', 'utf8'));
rep('await WaitUp("http://127.0.0.1:14036/products/" + Item, "prism");', 'await WaitUp("http://127.0.0.1:14036/products/" + Item, "pm-stub");');
// no user C (5 lines)
lines('// C: a NEW tenant user with no role', 5, '');
rep('"esign.signatures.sign", "esign.signatures.read", "esign.signatures.verify", "esign.my-samples.manage"\n',
    '"esign.signatures.sign", "esign.signatures.read", "esign.signatures.verify", "esign.my-samples.manage",\n        "auth.users.lookup" // the people names (display-label), granted explicitly to the probe role (FU-0175-ITEM-02 is BLOCKED)\n');
rep('var roleName = "q5-qa-"', 'var roleName = "fub2-qa-"');
rep('new { name = roleName, displayName = "Q5 QA", description = "WP-0175-Q5 stack" }', 'new { name = roleName, displayName = "FUB2 QA", description = "WP-MVP1-FUB2 stack" }');
rep('Log($"users: admin={adminId} A={aId} B={bId} C={cId} ({cEmail}, no quarantine key); role {roleName} {granted}/{keys.Length}; company {legalEntity}");',
    'Log($"users: admin={adminId} A={aId} B={bId}; role {roleName} {granted}/{keys.Length}; company {legalEntity}");');
rep('string K(string p) => $"q5s-{run}-{p}-{Guid.NewGuid():N}"[..40];', 'string K(string p) => $"fb2-{run}-{p}-{Guid.NewGuid():N}"[..40];');
rep('    var holdLot = await Lot("Q5-KRN-" + run, future, release: true);       // Karar Ver (partial release)\n    await Receive(holdLot, "1200", "QUARANTINE");\n',
    '    var holdLot = await Lot("FUB2-KRN-" + run, future, release: true);     // the hold of the note / lock probe (reject path)\n    await Receive(holdLot, "1200", "QUARANTINE");\n'
  + '    var holdLot2 = await Lot("FUB2-KRN2-" + run, future, release: true);   // a second hold (approve path)\n    await Receive(holdLot2, "300", "QUARANTINE");\n');
const cut0 = s.indexOf('    var blockLot = await Lot("Q5-BLK-"');
const cut1 = s.indexOf('    await Settings(new { lotReleaseRequiresApproval = true });');
if (cut0 < 0 || cut1 < cut0) throw new Error('cut');
s = s.slice(0, cut0) + s.slice(cut1);
rep('["c"] = cEmail,\n        ["aId"] = aId.ToString(), ["bId"] = bId.ToString(), ["cId"] = cId.ToString(), ["warehouse"] = wh, ["bin"] = bin, ["sample"] = sample,\n        ["holdLot"] = holdLot, ["blockLot"] = blockLot, ["retestLot"] = retestLot, ["draftLot"] = draftLot, ["run"] = run,',
    '\n        ["aId"] = aId.ToString(), ["bId"] = bId.ToString(), ["warehouse"] = wh, ["bin"] = bin, ["sample"] = sample,\n        ["holdLot"] = holdLot, ["holdLot2"] = holdLot2, ["item"] = Item, ["sku"] = Sku, ["run"] = run,');
fs.writeFileSync(p, s);
console.log('adapted');
```

## stack/pm-block.cs.txt

```csharp
    var world = Path.Combine(outDir, "pm-world.json");
    File.WriteAllText(world, $$"""
        { "products": { "{{Item}}": { "itemId": "{{Item}}", "itemKind": "GlobalProduct", "canonicalCode": "GP-000000000001", "name": "Amoksisilin 500mg Kapsül", "lifecycleStatus": "Active", "lotControlled": true, "serialControlled": true } },
          "skus": { "{{Sku}}": { "skuId": "{{Sku}}", "skuLevel": "Lsku", "itemId": "{{Item}}", "itemKind": "GlobalProduct", "canonicalCode": "LSKU-TR-000000000002", "name": "Amoksisilin 500mg 16 Kapsül (TR)", "lifecycleStatus": "Active", "baseUomId": "EA", "lotControlled": true, "serialControlled": false, "stockable": true, "conversions": [] } } }
        """);
    Start("pm-stub", "node", $"--max-http-header-size=65536 \"{args[2]}\" \"{world}\"", outDir, new()); // the OWN PM1 stub on 14036
```

## probe.js

```js
// WP-MVP1-FUB2 runtime probe on the OWN stack only (stack/Program.cs: own mongod, AuthService, Platform, Gateway, InventoryService,
// Web, pm-stub 14036, LE stub 14037). Tokens from this stack's AuthService login; the sign ticket from this stack's esign/reauth with
// the test password (<out>/pw.txt, never printed). The browser steps (note edit, lock, WorkCenter) run between the phases.
// usage: node probe.js <out> <phase>
//   holds      — the two holds of the stack (hold 1: note / lock / reject path, hold 2: approve path) → <out>/holds.json
//   check-note — hold 1 carries the NoteUpdated row the browser wrote (by A, noteChange)
//   decide <n> — A drafts a decision on hold n, signs it (MOD-0022) and submits it (202); updateHold → 409 note lock
//   check-open <n> — after the WorkCenter outcome: hold n state; updateHold 200 (rejected) or 409 HOLD_CLOSED (approved)
//   replay     — the same key twice around a later edit answers the first result; another body → 400
const fs = require('fs');
const [out, phase, arg] = process.argv.slice(2);
const st = JSON.parse(fs.readFileSync(out + '/stack.json', 'utf8'));
const pw = fs.readFileSync(out + '/pw.txt', 'utf8').trim();
const G = st.gateway;
const Q = G + '/api/quarantine';
const resultsFile = out + '/probe-api.json';
const results = fs.existsSync(resultsFile) ? JSON.parse(fs.readFileSync(resultsFile, 'utf8')) : [];
const rec = (id, what, expected, actual, ok) => {
  results.push({ id, phase, what, expected, actual, ok: !!ok, at: new Date().toISOString() });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${id} ${what} — expected ${expected}, got ${actual}`);
};
// a small valid PNG (200 × 60, one stroke) for the drawn appearance the quarantine policy asks for
const png = (() => {
  const zlib = require("zlib");
  const w = 200, h = 60, raw = Buffer.alloc((w * 4 + 1) * h, 255);
  for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; for (let x = 20; x < 180; x++) if (Math.abs(y - (30 + 15 * Math.sin(x / 15))) < 2) raw.fill(0, y * (w * 4 + 1) + 1 + x * 4, y * (w * 4 + 1) + 1 + x * 4 + 3); }
  const crcT = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
  const crc = (b) => { let c = 0xffffffff; for (const x of b) c = crcT[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const chunk = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(raw)), chunk("IEND", Buffer.alloc(0))]).toString("base64");
})();
const key = (p) => `fub2-${p}-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;

async function call(method, url, token, body, extra = {}) {
  const headers = { 'Content-Type': 'application/json', 'X-Tenant-Id': st.tenant, 'Accept-Language': 'tr', ...extra };
  if (url.includes('/api/quarantine') || url.includes('/api/inventory')) headers['X-Legal-Entity-Id'] = st.legalEntity;
  if (token) headers.Authorization = 'Bearer ' + token;
  const res = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch (_) { json = null; }
  return { status: res.status, json, text, etag: res.headers.get('etag') };
}
const data = (r) => (r.json && typeof r.json === 'object' && 'data' in r.json && 'isSuccessful' in r.json ? r.json.data : r.json);
const code = (r) => r.json?.error?.code || r.json?.reason_code || r.json?.errorCodes?.[0]?.code || '';
async function login(email) {
  const r = await call('POST', st.auth + '/api/tenant-auth/login', null, { email, password: pw, rememberMe: false });
  const m = JSON.stringify(r.json || {}).match(/"accessToken":"([^"]+)"/);
  if (!m) throw new Error(`login ${email}: ${r.status}`);
  return m[1];
}
const hold = async (token, n) => {
  const holds = JSON.parse(fs.readFileSync(out + '/holds.json', 'utf8'));
  const r = await call('GET', `${Q}/holds/${holds[n]}`, token);
  return { id: holds[n], r, h: r.json };
};
const patchNote = async (token, holdId, etag, note, k = key('note')) =>
  call('PATCH', `${Q}/holds/${holdId}`, token, { analysisNote: note, idempotencyKey: k }, { 'Idempotency-Key': k, 'If-Match': etag });

(async () => {
  const a = await login(st.a);
  if (phase === 'holds') {
    const r = await call('GET', `${Q}/holds?page=1&pageSize=50`, a);
    const items = r.json.items;
    const h1 = items.find((h) => h.lot?.lotId === st.holdLot);
    const h2 = items.find((h) => h.lot?.lotId === st.holdLot2);
    fs.writeFileSync(out + '/holds.json', JSON.stringify({ 1: h1.holdId, 2: h2.holdId, n1: h1.holdNumber, n2: h2.holdNumber }));
    rec('H0', 'the two intake holds of the stack', '2 holds', `${h1?.holdNumber} / ${h2?.holdNumber}`, h1 && h2);
  } else if (phase === 'check-note') {
    const { h } = await hold(a, 1);
    const rows = h.history.filter((x) => x.action === 'NoteUpdated');
    const last = rows[rows.length - 1];
    rec('N1', 'the browser note edit wrote ONE NoteUpdated history row (by A, noteChange)', '1 row, by A, noteLength > 0',
      JSON.stringify(rows.map((x) => ({ by: x.by.userId, noteChange: x.noteChange }))), rows.length === 1 && last.by.userId === st.aId && last.noteChange.noteLength > 0);
  } else if (phase === 'decide') {
    const n = Number(arg);
    const { id, r } = await hold(a, n);
    const qty = r.json.quantity;
    const dk = key('dec');
    const earlier = fs.existsSync(out + `/draft-${n}.json`) ? JSON.parse(fs.readFileSync(out + `/draft-${n}.json`, 'utf8')) : null; // a draft of an earlier run is reused
    const draft = earlier ? { status: 201, json: earlier } : await call('POST', `${Q}/decisions`, a, { kind: 'HoldDecision', targetId: id, lines: [{ lineNo: 1, decision: 'Release', quantity: qty }],
      reason: 'FUB2 probe — analysis complete, release', coaChecked: true, idempotencyKey: dk }, { 'Idempotency-Key': dk });
    const decisionId = draft.json?.decisionId;
    if (!earlier && draft.status === 201) fs.writeFileSync(out + `/draft-${n}.json`, JSON.stringify({ decisionId, decisionNumber: draft.json.decisionNumber }));
    const ticket = data(await call('POST', G + '/api/tenant-auth/esign/reauth', a, { password: pw, intent: 'sign' }))?.ticket;
    const signed = await call('POST', G + '/api/v1/esign/signatures', a, {
      ticket, subject: { module: 'quarantine', subjectType: 'quarantine.decision', subjectId: decisionId }, actionCode: 'quarantine.hold.release',
      meaningCode: 'RELEASE', appearance: { type: 'drawn', drawing: { pngBase64: png } }, idempotencyKey: key('sign')
    });
    const signatureId = data(signed)?.id;
    const sk = key('sub');
    const submitted = await call('POST', `${Q}/decisions/${decisionId}/submit`, a, { signatureId, idempotencyKey: sk }, { 'Idempotency-Key': sk });
    rec(`D${n}`, `A drafts, signs (MOD-0022) and submits a release decision on hold ${n}`, 'draft 201, sign 201, submit 202',
      `${draft.status} ${draft.json?.decisionNumber || code(draft)}, ${signed.status} ${data(signed)?.signatureNumber || code(signed)}, ${submitted.status} ${submitted.json?.state || code(submitted)}`,
      draft.status === 201 && signed.status === 201 && submitted.status === 202);
    const after = await hold(a, n);
    const locked = await patchNote(a, id, after.r.etag, 'onay beklerken not');
    rec(`L${n}`, `updateHold while the decision waits for approval`, '409 QUARANTINE_DECISION_ALREADY_PENDING; activeDecision set',
      `${locked.status} ${code(locked)}; activeDecision ${after.h.activeDecision?.decisionNumber} ${after.h.activeDecision?.state}`,
      locked.status === 409 && code(locked) === 'QUARANTINE_DECISION_ALREADY_PENDING' && after.h.activeDecision);
    fs.writeFileSync(out + `/decision-${n}.json`, JSON.stringify({ decisionId, number: draft.json?.decisionNumber, signature: data(signed)?.signatureNumber }));
  } else if (phase === 'check-open') {
    const n = Number(arg);
    const { id, r, h } = await hold(a, n);
    const res = await patchNote(a, id, r.etag, `karar sonrası not ${n}`);
    if (n === 1) {
      rec('O1', 'after the WorkCenter REJECT the lock is gone: updateHold', '200, no activeDecision, one more NoteUpdated row',
        `${res.status}; activeDecision ${h.activeDecision ? 'set' : 'none'}; NoteUpdated rows ${(res.json?.history || []).filter((x) => x.action === 'NoteUpdated').length}`,
        res.status === 200 && !h.activeDecision && res.json.history.filter((x) => x.action === 'NoteUpdated').length === h.history.filter((x) => x.action === 'NoteUpdated').length + 1);
    } else {
      rec('O2', 'after the WorkCenter APPROVE the decision is applied and the hold is closed: updateHold', '409 QUARANTINE_HOLD_CLOSED, status Decided',
        `${res.status} ${code(res)}; status ${h.status}`, res.status === 409 && code(res) === 'QUARANTINE_HOLD_CLOSED' && h.status === 'Decided');
    }
  } else if (phase === 'replay') {
    const { id, r } = await hold(a, 1);
    const k1 = key('rp');
    const first = await patchNote(a, id, r.etag, 'tekrar sınaması 1', k1);
    const next = await hold(a, 1);
    const second = await patchNote(a, id, next.r.etag, 'tekrar sınaması 2');
    const again = await patchNote(a, id, r.etag, 'tekrar sınaması 1', k1);
    const other = await patchNote(a, id, r.etag, 'başka gövde', k1);
    rec('R1', 'same key again after a later edit answers the first result; another body → 400',
      `200 v${first.json?.version} "tekrar sınaması 1"; 400 IDEMPOTENCY_KEY_REUSED`,
      `first ${first.status} v${first.json?.version}, later ${second.status} v${second.json?.version}, replay ${again.status} v${again.json?.version} "${again.json?.analysisNote}" etag ${again.etag}, other ${other.status} ${code(other)}`,
      first.status === 200 && again.status === 200 && again.json.version === first.json.version && again.json.analysisNote === 'tekrar sınaması 1'
        && second.json.version === first.json.version + 1 && other.status === 400 && code(other) === 'IDEMPOTENCY_KEY_REUSED');
  }
  fs.writeFileSync(resultsFile, JSON.stringify(results, null, 2));
})().catch((e) => { console.error('PROBE ERROR', e.message); process.exit(1); });
```

## drafts.js

```js
// writes <out>/draft-<n>.json from the hold detail's Draft decision (the first decide run left one per hold)
const fs = require('fs');
const out = process.argv[2];
const st = JSON.parse(fs.readFileSync(out + '/stack.json', 'utf8'));
const pw = fs.readFileSync(out + '/pw.txt', 'utf8').trim();
const holds = JSON.parse(fs.readFileSync(out + '/holds.json', 'utf8'));
(async () => {
  const l = await (await fetch(st.auth + '/api/tenant-auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': st.tenant }, body: JSON.stringify({ email: st.a, password: pw }) })).text();
  const token = l.match(/"accessToken":"([^"]+)"/)[1];
  for (const n of [1, 2]) {
    const h = await (await fetch(`${st.gateway}/api/quarantine/holds/${holds[n]}`, { headers: { Authorization: 'Bearer ' + token, 'X-Tenant-Id': st.tenant, 'X-Legal-Entity-Id': st.legalEntity } })).json();
    const d = h.decisions.find((x) => x.state === 'Draft');
    fs.writeFileSync(`${out}/draft-${n}.json`, JSON.stringify({ decisionId: d.decisionId, decisionNumber: d.decisionNumber }));
    console.log(n, d.decisionNumber, d.state);
  }
})();
```

## state.js

```js
// prints the decision + hold state of hold n (own stack)
const fs = require('fs');
const [out, n] = process.argv.slice(2);
const st = JSON.parse(fs.readFileSync(out + '/stack.json', 'utf8'));
const pw = fs.readFileSync(out + '/pw.txt', 'utf8').trim();
const holds = JSON.parse(fs.readFileSync(out + '/holds.json', 'utf8'));
const d = JSON.parse(fs.readFileSync(`${out}/draft-${n}.json`, 'utf8'));
(async () => {
  const l = await (await fetch(st.auth + '/api/tenant-auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': st.tenant }, body: JSON.stringify({ email: st.a, password: pw }) })).text();
  const h = { Authorization: 'Bearer ' + l.match(/"accessToken":"([^"]+)"/)[1], 'X-Tenant-Id': st.tenant, 'X-Legal-Entity-Id': st.legalEntity };
  const dec = await (await fetch(`${st.gateway}/api/quarantine/decisions/${d.decisionId}`, { headers: h })).json();
  const hold = await (await fetch(`${st.gateway}/api/quarantine/holds/${holds[n]}`, { headers: h })).json();
  console.log(JSON.stringify({ decision: { number: dec.decisionNumber, state: dec.state, workflow: dec.workflow, applyError: dec.applyError }, hold: { status: hold.status, activeDecision: hold.activeDecision } }, null, 1));
})();
```

## sabotage.js

```js
// WP-MVP1-FUB2 sabotages (a), (b), (d) + (a-web): each one edit to production code, the named test must turn red, then
// `git checkout -- <file>` restores it and `git status --porcelain` must be empty. (c) and (e) have no code to sabotage:
// FU-0175-ITEM-02 and O-ITEM-3 are BLOCKED (evidence §2). INVENTORY_TEST_MONGO is unset: the suite starts its own replica set.
// usage: node sabotage.js <worktree> <out>
const fs = require('fs');
const { execFileSync, spawnSync } = require('child_process');
const [W, out] = process.argv.slice(2);
const INV = 'services/Diten.InventoryService/tests/Diten.InventoryService.Tests';
const WEB = 'frontend/Diten.Web.Tests';
const HANDLER = 'services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Quarantine/Handlers/UpdateHoldHandler.cs';
const cases = [
  { id: 'a', what: 'the note lock removed', file: HANDLER, a: 'if (hold.ActiveDecisionId is not null)', b: 'if (false && hold.ActiveDecisionId is not null)',
    project: INV, filter: 'FullyQualifiedName~HoldNoteLockTests.While_a_submitted_decision_waits_for_approval_the_note_is_locked_and_nothing_is_written' },
  { id: 'b', what: 'the history row not written', file: HANDLER, a: 'await session.InsertHistoryAsync([history], c);', b: '// await session.InsertHistoryAsync([history], c);',
    project: INV, filter: 'FullyQualifiedName~HoldNoteLockTests.A_note_change_writes_one_NoteUpdated_history_row_with_who_when_and_the_change_summary' },
  { id: 'd', what: 'the first-result replay removed (back to the current read)', file: HANDLER, a: '? FirstResult(await DetailAsync(request.HoldId, ct), earlier)', b: '? await DetailAsync(request.HoldId, ct)',
    project: INV, filter: 'FullyQualifiedName~HoldNoteLockTests.The_same_key_answers_the_first_result_even_after_a_later_edit_and_a_different_body_is_400' },
  { id: 'a-web', what: 'the page draws "Notu düzenle" during an open decision', file: 'frontend/Diten.Web/wwwroot/assets/js/Quarantine/details.js',
    a: "show(ui.byId('qr-d-note-edit'), ui.can('STARTREVIEW') && open && !noteLocked);", b: "show(ui.byId('qr-d-note-edit'), ui.can('STARTREVIEW') && open);",
    project: WEB, filter: 'FullyQualifiedName~HoldNoteLockWebTests.While_a_decision_is_open_the_note_edit_is_not_drawn_and_the_lock_explanation_is' }
];
const git = (...a) => execFileSync('git', ['-C', W, ...a], { encoding: 'utf8' });
const env = { ...process.env };
delete env.INVENTORY_TEST_MONGO;
const results = [];
for (const c of cases) {
  const path = `${W}/${c.file}`;
  const text = fs.readFileSync(path, 'utf8');
  if (text.split(c.a).length !== 2) throw new Error(`${c.id}: anchor`);
  fs.writeFileSync(path, text.replace(c.a, () => c.b));
  const run = spawnSync('dotnet', ['test', `${W}/${c.project}`, '-nologo', '--filter', c.filter], { encoding: 'utf8', env, maxBuffer: 64 * 1024 * 1024 });
  const log = `${run.stdout}\n${run.stderr}`;
  fs.writeFileSync(`${out}/sabotage-${c.id}.txt`, log);
  const summary = (log.match(/(Başarısız!|Başarılı!|Failed!|Passed!)[^\n]*/) || ['(no summary)'])[0];
  git('checkout', '--', c.file);
  const porcelain = git('status', '--porcelain').trim();
  const red = /Başarısız!|Failed!/.test(summary) && !/error CS/.test(log);
  results.push({ id: c.id, what: c.what, test: c.filter.split('.').pop(), summary: summary.trim(), red, restoredClean: porcelain === '' });
  console.log(`${c.id}: ${red ? 'RED' : 'NOT RED'} — ${summary.trim()} — restored ${porcelain === '' ? 'clean' : 'DIRTY: ' + porcelain}`);
}
fs.writeFileSync(`${out}/sabotage-results.json`, JSON.stringify(results, null, 2));
```
