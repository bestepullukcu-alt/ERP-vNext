# MOD-0174 / MOD-0175 ITEM — betikler (WP-0174-ITEM)

Kanıt: `mod-0174-item-evidence-2026-10-09.md`. Betikler repoda tutulmuyor; tekrar üretilebilsin diye burada aynen duruyor. Hiçbiri
sahibin filosuna (4010, 5000, 5001, 5056–5061, 5066) ya da 27017'ye bağlanmaz.

## 1. `sabotage.js` — K3 sabotajları (a)–(e)

Kullanım: `node sabotage.js <worktree> <a..e> apply|restore`. Her sabotaj tek bir metin değişikliğidir; çapa tam bir kez
bulunamazsa betik hata verir. `restore` yedeği bayt bayt geri yazar.

Koşu:
- (a)–(d): Inventory test projesi derlenir, ardından şu filtreyle koşulur (30 test):
  `--filter "FullyQualifiedName~ItemLevelLotTests|…~StockableSkuTests|…~ItemQuarantineTests|…~UpdateHoldTests|…~LotFlagFromSku"`.
- (e): Web test projesi `--filter "FullyQualifiedName~Quarantine"` ile koşulur.
- Her koşudan sonra `restore` yapılır ve testler yeniden yeşil koşulur.

**Uyarı:** derleme hatası veren bir sabotaj, önceki ikiliyle koşar. Bu yüzden derleme çıktısı her koşuda kontrol edilir; (b)'nin ilk
koşusu bu nedenle geçersiz sayıldı.

```js
// WP-0174-ITEM K3 sabotages: node sabotage.js <worktree> <a..e> apply|restore. One textual change each (fails loudly when the
// anchor is missing or repeated); restore writes the backup back byte for byte.
const fs = require('fs');
const path = require('path');
const [wt, which, mode] = process.argv.slice(2);
const A = 'services/Diten.InventoryService/src/Diten.InventoryService.Application/';
const plan = {
  // (a) TRACE: the Item level is no longer parsed by createLot (the 1.2.0 refusal comes back)
  a: [A + 'Features/Traceability/Handlers/CommandHandlers/CreateLotHandler.cs',
      'if (!WireNames.TryParseSkuLevel(request.SkuLevel, out var skuLevel))',
      'if (!WireNames.TryParseSkuLevel(request.SkuLevel, out var skuLevel) || skuLevel == SkuLevel.Item)'],
  // (b) QUARANTINE: a hold answers an Item bucket with one of the three 1.0.0 values
  b: [A + 'Features/Quarantine/Services/QuarantineMapping.cs',
      'Item = new ItemRefDto(Id(hold.ItemId), Id(hold.SkuId), WireNames.ToWire(hold.SkuLevel)),',
      'Item = new ItemRefDto(Id(hold.ItemId), Id(hold.SkuId), WireNames.ToWire(hold.SkuLevel == Diten.InventoryService.Domain.Enums.SkuLevel.Item ? Diten.InventoryService.Domain.Enums.SkuLevel.Lsku : hold.SkuLevel)),'],
  // (c) stockable: false is no longer enforced
  c: ['services/Diten.InventoryService/src/Diten.InventoryService.Domain/Rules/ProductMasterStockRules.cs',
      'public static bool IsStockable(bool? published) => published != false;',
      'public static bool IsStockable(bool? published) => true;'],
  // (d) updateHold: no version check at all (the handler check AND the version-conditioned replace take the stored version)
  d: [A + 'Features/Quarantine/Handlers/UpdateHoldHandler.cs',
      '        if (hold.Version != request.ExpectedVersion)\n',
      '        request = request with { ExpectedVersion = hold.Version };\n        if (false)\n'],
  // (e) Web: the person-name resolver resolves nothing
  e: ['frontend/Diten.Web/Models/Quarantine/QuarantinePeople.cs',
      'return answers.Where(a => !string.IsNullOrWhiteSpace(a.Label)).ToDictionary(a => a.Id, a => a.Label!, StringComparer.OrdinalIgnoreCase);',
      'return new Dictionary<string, string>();']
};
const [rel, from, to] = plan[which];
const file = path.join(wt, rel);
const backup = path.join(__dirname, `sab-${which}.bak`);
if (mode === 'apply') {
  const raw = fs.readFileSync(file);
  fs.writeFileSync(backup, raw);
  let t = raw.toString('utf8');
  const crlf = t.includes('\r\n');
  t = t.split('\r\n').join('\n');
  if (t.split(from).length !== 2) throw new Error('anchor not found exactly once for ' + which);
  t = t.split(from).join(to);
  fs.writeFileSync(file, crlf ? t.split('\n').join('\r\n') : t);
  console.log(`applied ${which} to ${path.basename(file)}`);
} else {
  fs.writeFileSync(file, fs.readFileSync(backup));
  fs.unlinkSync(backup);
  console.log(`restored ${which}`);
}
```

## 2. Yoklama yığını (`probe/Program.cs` + `pm-stub.js`, scratchpad)

Yığın, Q5 karantina yığınının (kullanıcılar A/B, QA rolü, ayarlar, `INV-QUARANTINE-RELEASE` şablonu) ve PM1 ürün ana verisi
taklidinin birleşimi. `pm-stub.js` PM1 betikler belgesindekiyle aynıdır (`mod-0173-pm1-scripts-2026-10-09.md` §2).

Kendi süreçlerini başlatır:
- yalıtılmış mongod;
- AuthService (geliştirme seed'i) ve Platform (Workflow, MOD-0022, karantina konu sağlayıcısı → kendi gateway'i);
- Gateway (geçici ocelot kopyası), InventoryService ve Web — her biri `--urls` + `ASPNETCORE_URLS` ile kendi portunda;
- `pm-stub.js` (14036) ve şirket taklidi (14037). İkisi de node `--max-http-header-size=65536` ile çalışır; SuperAdmin token'ı
  uzun olduğu için gerekli.

Ürün ana verisi dünyası (hepsi `Item` seviyesinde, `skuId == itemId`):

| kod | kalem | lot bayrağı | stoklanabilir |
|---|---|---|---|
| RM-…0001 | Laktoz monohidrat | true | evet |
| RM-…0002 | Mikrokristal selüloz | true | evet |
| RM-…0003 | Talk | null (bilinmiyor) | evet |
| RM-…0004 | Numune şişesi | false | `stockable: false` |

Ardından:
- QA rolü (30 anahtar, `auth.users.lookup` dahil) A ve B'ye verilir;
- ayarlarda onaylayıcı aday olarak `user:B` tanımlanır ve şablon hazırlanır;
- API kontrolleri koşulur (`api-checks.json`);
- `READY` yazılır ve `stop` dosyası beklenir.

Şifre yalnız `pw.txt`'ye yazılır. `stop` dosyası görülünce yalnız kendi PID listesindeki süreçler kapatılır, mongod bırakılır ve
`pw.txt` silinir.
