# Doküman Klasör Yapısı (`docs/`)

> Bu kural 2026-09-07'de yazıldı çünkü `docs/` çöplüğe dönmüştü ve onu
> engelleyecek hiçbir kural yoktu. O günkü ölçüm: kökte 29 dosya,
> `audits/` tek düzeyde 172 dosya, toplam 293 dosya / 8.0 MB.

---

## 1. Mantık — tek soru

Bir belgeyi nereye koyacağını **belgenin zamanla nasıl davrandığı** söyler.
Konusu değil, yazarı değil, kime ait olduğu değil. Sırayla sor, ilk "evet"te dur:

| # | soru | klasör |
| :-- | :--- | :--- |
| 1 | Biz mi yazdık? **Hayır**, dışarıdan geldi | `vendor/` |
| 2 | Belirli bir tarihte olan bir şeyi mi kaydediyor, bir daha değişmeyecek mi? | `records/` |
| 3 | Henüz olmamış bir şeyi mi anlatıyor? | `roadmap/` |
| 4 | Birine bir işi nasıl yapacağını mı öğretiyor? | `guides/` |
| 5 | Bugünün gerçeğini mi anlatıyor, değişince güncellenecek mi? | `reference/` |

Beş sorunun dışında kalan belge yoktur. Yeni bir klasör açmadan önce bu tabloyu
oku: yeni bir üst klasör, bu beş sorudan birinin cevabının değiştiği anlamına
gelir ve Control Tower kararıdır.

## 2. Yapı

    docs/
      README.md                       ← kökteki TEK dosya

      reference/                      ← bugünün gerçeği; değişince güncellenir
        architecture/                 mimari kararlar, port sözleşmeleri, spec'ler
        modules/platform/<yetenek>/   platform tarafı modül belgeleri
        modules/tenant/<modül>/       kiracı tarafı modül belgeleri
        blueprint/                    MOD-xxxx kimliklerinin kanonik kaynağı
        integrations/<karşı-taraf>/   dış sistem entegrasyon sözleşmeleri
        reference-data/               referans veri içe aktarım tanımları

      guides/                         ← nasıl yapılır; okuyan bir işi yapar
        <modül>/                      son kullanıcı kılavuzu (resimli, tek dosya HTML)
        operations/                   dev ortam, modül ekleme, ajan kullanımı
        control-tower/                Control Tower SOP ve işletim kartı
        sop-upstream/                 üst kaynaktan gelen SOP kuralları

      records/                        ← o gün ne olduğu; ARTIK DEĞİŞMEZ
        audits/<yyyy-mm>/             denetim ve inceleme çıktıları
        analysis/<konu>/              karşılaştırma ve durum analizleri
        acceptance-reports/           kabul raporları
        releases/                     sürüm notları

      roadmap/                        ← henüz olmamış
        plans/                        ileriye dönük planlar
        backlog/                      product-backlog ve kapanmış maddeler

      vendor/                         ← biz yazmadık; OLDUĞU GİBİ durur
        <paket>/                      bir teslimat = bir klasör

## 3. Kurallar

**K1 · Kökte dosya olmaz.** Yalnız `README.md`.

**K2 · Üst klasör beş taneden biridir.** Altıncısı Control Tower kararı ister.
Sebep: her yeni üst klasör, "belgem nereye gider" sorusunu bir dal daha
karmaşıklaştırır ve karmaşıklaşan kural delinir.

**K3 · Her klasörün bir kırılım ekseni olmalı.** Eksen klasöre göre değişir:
`audits/` **tarih** (`yyyy-mm`), `analysis/` **konu**, `modules/`
**platform/tenant → modül**, `vendor/` **teslimat paketi**.

Ekseni OLMAYAN bir klasörde 20'yi aşan dosya birikmişse kırılım gecikmiştir.
Ekseni olan klasörde sayı ölçüt değildir: `audits/2026-08` 84 dosya taşır ve
bu bir kusur değil — o ay 84 denetim yazılmıştır, ve denetim tarihle aranır.
Yoğun bir ayı ikinci bir eksene bölmek (modül, konu) aramayı kolaylaştırmaz;
bir modülün denetimlerini iki aya dağıtarak zorlaştırır.

**K4 · `records/` yazıldıktan sonra düzeltilmez.** Bir denetim yanlışsa yenisi
yazılır, eskisi durur. Kaydın değeri o gün ne bilindiğini göstermesidir.

**K5 · `vendor/` içeriği düzenlenmez.** Dışarıdan gelen belge geldiği adla durur;
sürüm bilgisi ad içindedir. Ondan türettiğimiz çalışma dosyası `vendor/` içine
değil, beş sorunun gösterdiği yere gider.

**K6 · Kullanım kılavuzu `guides/<modül>/` altındadır ve resimlidir.** Markdown
kılavuz üretilmez; biçim `.antigravity/agents/user-manual-generator.md`.

**K7 · 1 MB üzeri ikili dosya tartışılır.** Git ikiliyi sıkıştırmaz; her sürüm
tam boy saklanır.

**K8 · Taşıma referans kırar — protokolsüz taşınmaz.** Bkz. §4.

## 3.1 Modül bitince üretilen belgeler nereye gider

Kurallar bir modül tamamlanınca kullanıcı kılavuzu, API dokümanı ve mimari
denetim üretilmesini istiyor (`orchestrator.md` kapanış listesi,
`add-module.md` Faz 4, `release-checklist.md`). **Nereye konacağını hiçbiri
söylemiyordu.** Tablo bunu bağlar; her satırın gerekçesi §1'deki beş sorudur.

| belge | üreten | nereye | hangi soru |
| :--- | :--- | :--- | :--- |
| Servis README / Quick Start | `documentation-writer` | `services/<servis>/README.md` | **docs'a değil** — kodla birlikte değişir, kodla birlikte gözden geçirilir |
| API dokümanı (Swagger/OpenAPI) | `documentation-writer` | şema koddan üretilir; **anlatısı** `docs/reference/architecture/api/` | bugünün gerçeği (5) |
| ADR — mimari karar kaydı | `documentation-writer` | `docs/records/decisions/<yyyy-mm>/` | tarihli kayıt (2) — ADR düzeltilmez, yerine yenisi yazılır ve eskisi `Superseded` işaretlenir |
| Kullanıcı kılavuzu | `user-manual-generator` | `docs/guides/<modül>/index.html` | nasıl yapılır (4) |
| Modül belgesi (spec, karar notu) | orchestrator | `docs/reference/modules/{platform\|tenant}/<modül>/` | bugünün gerçeği (5) |
| Mimari denetim raporu | denetim adımı | `docs/records/audits/<yyyy-mm>/` | tarihli kayıt (2) |
| CHANGELOG / sürüm notu | `documentation-writer` | `docs/records/releases/` | tarihli kayıt (2) |
| `llms.txt` | `documentation-writer` | **repo kökü** | ajanların giriş noktası; `docs/` altında aranmaz |

⚠ ADR ile `reference/architecture/` karıştırılmaz. ADR **bir kararın o günkü
gerekçesidir** ve dondurulur. `reference/architecture/` **bugünkü mimarinin
anlatısıdır** ve gerçek değişince güncellenir. Aynı konuda ikisi de bulunur.

## 4. Taşıma protokolü

Bir belge yer değiştirdiğinde ona işaret eden her şey kırılır. 2026-09-07
temizliğinde 780'den fazla referans güncellendi; ikisi neredeyse kaçıyordu ve
ikisi de **kod içindeydi** — bir Python kapısı ve bir CSS yorumu.

1. **Ölç** — kaç yerden referans var:

       grep -rl "docs/<eski-yol>" .antigravity execution docs services frontend gateway

2. **`git mv` kullan.** Kopyala-sil değil; geçmiş korunur.
3. **Referansları AYNI commit'te güncelle.** Ayrı commit'e bölme; arada kalan
   commit'te belgeler kırıktır.
4. **Kod uzantılarını da tara.** `.md` yetmez: `.py .sh .ps1 .cs .cshtml .js .css
   .html .json .yaml .xml .resx`. Bu adım atlanırsa kırılan şey belge değil,
   çalışan bir kapı olur. Bu adımı
   `TenantArchitecture.ArchitectureTests.DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders`
   ölçer. Test kırmızıysa taşıma bitmemiştir.
5. **Doğrula** — ölü bağ kalmadığını göster (§5), iddia etme.
6. **Referansı çok olan en sona.** Blueprint xlsx ve `product-backlog.md`
   birden çok workflow'un kanonik kaynağıdır.

## 5. Denetim — kural yazılı değil, ölçülür

    # K1 — kökte README disinda dosya (0 olmali)
    find docs -maxdepth 1 -type f ! -name 'README.md' ! -name '.DS_Store' | wc -l

    # K2 — ust klasor sayisi (5 olmali: guides records reference roadmap vendor)
    find docs -maxdepth 1 -type d -mindepth 1 | wc -l

    # K3 — ekseni OLMAYAN klasorde 20'yi asan dosya (tarih klasorleri haric)
    find docs -type d ! -path '*/audits/2*' -exec sh -c \
      'n=$(ls -p "$1" | grep -vc /); [ "$n" -gt 20 ] && echo "$n $1"' _ {} \;

    # K7 — 1 MB ustu ikili
    find docs -type f -size +1M

    # §4 — olu md bagi
    grep -rho '](\S*\.md)' docs --include='*.md' | sed 's/](//;s/)//' | sort -u


## Candidate rule — exact legacy authority and immutable evidence

This amendment has NO effect until an explicit owner decision is bound to the exact
`docs/reference/architecture/docs-path-authority.json` payload and the implementation diff.
The candidate manifest is UNAPPROVED; installing this candidate must remain red.

The five-folder policy, code scan extensions, traversal and presence checks remain in force.
No folder wildcard or blanket records exclusion is permitted. An approved disposition may
classify an exact SHA-256-sealed input as active-tool, historical-data (.json), or historical-tool (.py).
Historical tools are immutable completed-candidate/verifier scripts, not runnable current-canonical dependencies;
they have no canonical targets and remain under records. Every disposition
must identify a byte-verified provenance file and the exact line linking its path and hash.
Active tools must resolve all registered canonical dependencies by exact path and SHA-256.
Historical hashes describe the earlier snapshot, not today's canonical content; never rewrite them.
Historical-tool seals require exact file SHA-256 and records provenance; they grant no execution or publication authority.

The machine-readable authority record is schema-validated (unknown fields/duplicate keys rejected),
read and scanned as part of the same inventory. Only its validated structured path fields may
represent legacy paths. A decision must bind the exact canonicalTargets + sealedInputs payload,
state APPROVED, and record the accountable owner and decision ID. This checks record integrity,
not the authenticity of a human signature; CT must establish the human authorization separately.
Synthetic decisions are accepted only by isolated test fixtures, never the repository entrypoint.

Missing files, symlinks/reparse points, path traversal, wildcards, wrong hashes, unknown source
files, changed evidence, stale dispositions and missing/invalid approval records fail closed.
Any new path/content requires a newly reviewed disposition; an existing record is not a waiver
for future changes. Existing immutable evidence/manifests/contracts remain byte-identical.

## R2 exact recovery snapshot disposition (requires new owner approval)

Only the following historical-data seals may remain outside records:
- `docs/roadmap/plans/mod-0183-root-uptake-recovery-01/input-manifest.json`, SHA256 `a70f79d7892d83ddbe772653a06cb009ebfe7e2b72549d11dd956f72ab9f83fe`.
- `docs/roadmap/plans/mod-0183-root-uptake-recovery-01/baseline.json`, SHA256 `74e31359aaeb662645d224d9602cb801f8f81a433cc75cf09840d852fd2e878e`.

These are exact immutable snapshots, not a roadmap prefix exemption. Existing
records-only provenance, approved payload binding, source hash, empty historical
targets, inventory consumption and every other fail-closed check still apply.
Changing either path or byte content requires a new reviewed rule and payload.

## K5 · Manifest hangi dizinden doğrulanır, manifestin içinde yazar

`ARTIFACTS.sha256` yazan her kayıt, dosyanın **ilk satırına** doğrulama dizinini bir yorum
olarak koyar:

```
# verify from: repository root
```
ya da
```
# verify from: this folder
```

**Neden.** Lane'ler iki konvansiyonu da kullanıyor — Q339 ve Q366 kök-göreli yol yazdı,
Q374 `./`-göreli. CT her ikisini de yanlış dizinden doğrulayıp önce 24, sonra 15 "hata"
raporladı; iki durumda da dosyalar el değmemişti.

Belirtilmemiş bir çalışma dizinine bağlı doğrulama, **gerçek bir negatifi kurcalanmış bir
dosyadan ayırt edilemez kılar** — manifestin var olma sebebinin tam tersi. Ledger: Q377.

## K6 · Mühür kaydın kendi dosyalarını kapsar; repo dosyaları ayrı dosyada kaydedilir

Bir kayıt klasörü **iki** manifest taşır:

```
ARTIFACTS.sha256            yalnız docs/records/<bu kayıt>/** altındaki dosyalar
                            `shasum -c` sonsuza dek geçer
                            tek bir FAILED = gerçekten kurcalanmış

SOURCE-AS-MEASURED.sha256   lane'in dokunduğu repo yolları ve ölçüm anındaki hash'leri
                            ASLA `shasum -c` edilmez — kayıt amaçlıdır
                            "ölçtüğümde kod neye benziyordu" sorusunu cevaplar
```

İkisinin de ilk satırı K5 gereği hangi dizinden okunduğunu söyler, ve
`SOURCE-AS-MEASURED.sha256` ayrıca **doğrulama amaçlı olmadığını** yazar.

**Neden.** Bir manifest iki farklı soruyu cevaplıyordu ve ikisinin ömrü farklı: kaydın kendi
dosyaları değişmez, repo dosyaları değişmeye devam eder. Paylaşılan bir dosyayı hash'leyen
mühür, bir sonraki lane o dosyaya dokunduğunda düşer — ve düşmesi beklenen bir mühür,
okuyucuya **düşen mühürleri yok saymayı** öğretir; bir mührün asla yapmaması gereken tek şey.

> **MEASURED CASE 2026-10-04:** üç mühür aynı anda düşüyordu ve kimse fark etmemişti.
> `mvp6-r2-returns-ui-01` 50 girdinin **8'i** (yedi `SharedResource.*.resx` artı
> `SupplyChainService.Api/Program.cs`), `mvp6-r4a-carriers-ui-01` 37'nin **1'i**,
> `mvp6-r3-recipe-amend-guard-01` 6'nın **1'i**. Onu da masumdu — sonraki lane'lerin
> beklenen düzenlemeleri. R-4b yalnız kendi sebep olduğu tek hatayı raporladı çünkü yalnız
> onu kontrol etmişti. Ledger Q415, Q417.

**Repo dosyasının hash'i atılmaz, taşınır.** R-4b, R-4a'nın mühürlediği `Program.cs` hash'ini
kullanarak "iki satır çıkarılınca birebir aynı" diyebildi. O kanıt değerlidir; kaybolmaması
için `SOURCE-AS-MEASURED.sha256`'da yaşar.

**Geriye dönük uygulanmaz.** Mühürlenmiş kayıt düzeltilmez (K4). Yukarıdaki üç kayıt beklenen
kaymayı taşır ve bu burada kayıtlıdır; kural yeni kayıtlar için geçerlidir.

## K4 ne zaman başlar — CT hükmü, 2026-10-05

K4'ün saati **ilk bayt yazıldığında değil, kayıt CT'ye mühürlü olarak bildirildiğinde**
başlar.

- **Bildirimden önce** kayıt lane'in taslağıdır. Lane kendi rakamının yanlış olduğunu fark
  edip düzeltip yeniden mühürlerse bu *düzeltme* değil, *yazmayı bitirme*dir.
- **Bildirimden sonra** kayıt değişmez. Sonraki her düzeltme yeni bir kayıttır.

**Neden bu çizgi.** K4'ün amacı, güvenilmeye başlanmış bir kaydın sessizce revize
edilememesidir — kaydı kanıt yapan şey budur. Güvenilme CT'ye "mühürlendi" denildiği anda
başlar. Çizgiyi daha erkeye çekmek, lane'i **bilerek yanlış bir rakamı yayınlamaya** ve sonra
onu düzelten ikinci bir kayıt yazmaya zorlar; o zaman birincil kayıt bilinerek yanlış olur ve
her okuyucunun düzeltmeyi ayrıca bulması gerekir. Bu, K4'ün önlemek istediğinden kötüdür.

**İki şart.** Düzeltme (a) bildirimden önce olmalı ve (b) raporda **açıkça beyan edilmelidir**.
Beyan edilmeyen bir yeniden mühürleme, çizginin hangi tarafında olursa olsun ihlaldir.

> **MEASURED CASE 2026-10-05:** aynı soru iki lane tarafından, iki kez soruldu. Q425 yazım
> sonrası sayımının 59 değil 60 olduğunu fark etti; Q439 "65" yazdıktan sonra daha önce temiz
> olan üç dosyayı düzenlemenin onları sayıma **soktuğunu** gördü ve gerçek rakamın 64→68
> olduğunu küme karşılaştırmasıyla doğruladı. İkisi de bildirimden önce düzeltti, ikisi de
> raporunda beyan etti, ve ikisi de CT'den hüküm istedi. Ledger Q440.
