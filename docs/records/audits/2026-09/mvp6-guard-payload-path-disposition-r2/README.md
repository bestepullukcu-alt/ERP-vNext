# MVP6-GUARD-PAYLOAD-PATH-DISPOSITION-R2

**Sonuç: exact reviewable aday hazır; owner onayı bekleniyor; activation NO-GO.**
Gerçek checkout’ta yalnız bu yeni inceleme paketi yazıldı. Guard, kural, authority,
canonical sözleşmeler, tarihsel girdiler ve eski onay değiştirilmedi. Git staging,
commit, push yapılmadı. Dal: `feature/mvp6-logistics`; mevcut kirli ağaç korundu.

## Tek final paket

Uygulama için tek kaynak `final.patch`:
`0799c84f02e4e72c26da43fa99355d141546c77abb67719ce4c2a923b59b2421`.
Parça patch’ler yalnız inceleme içindir; final patch ile birlikte tekrar uygulanmaz.
Baseline, HEAD değil mevcut çalışma ağacının byte’larıdır (`baseline-files.json.txt`).
Patch temiz disposable baseline üzerinde hem check edildi hem uygulandı; uygulanan
guard/kural byte’ları test edilen adayla eşit. Baseline değişirse yeniden inceleme gerekir.

- `payload.txt`: reader’ın gerçek algoritmasının ürettiği yeni onay kapsamı.
- `authority-candidate.json.txt`: UNAPPROVED final authority; gerçek hedefi patch içindedir.
- `decision-candidate.json.txt`: yeni kimlikli UNAPPROVED karar taslağı; consent değildir.
- `fixture-fix.patch`: ayrı uygulama yetkisi gereken exact fixture değişikliği.
- `guard-policy.patch`, `rule.patch`, `boundary-tests.patch`: policy ve sınır testleri.
- `artifact-relocation.patch`: eski çalışma artifact’ının byte-identical arşivlenmesi.
- `full-suite.trx`, `full-suite-authorized.log`: tam mimari suite çıktısı.

JSON/C# inceleme dosyaları `.txt` uzantılıdır; bunları yeni taranan JSON/C# dosyaları
olarak records altına kurmak yeni scan hit’leri yaratır. Eski tarihsel kayıtlar düzeltilmedi.

## 1. Byte düzeyinde payload farkı

Production kaynak: `DocsPathGuardTests.ReadAuthority`, eski satır 220–222:

```csharp
var payload = doc.RootElement.GetProperty("canonicalTargets").GetRawText() + "\n" +
    doc.RootElement.GetProperty("sealedInputs").GetRawText();
Hash(Encoding.UTF8.GetBytes(payload));
```

Bu JSON canonicalization değildir. `GetRawText()` authority içindeki iki dizinin
**mevcut iç boşluklarını** korur; araya tek LF (0A) konur; sonuna LF/BOM eklenmez.
Property adları, authority status/decision ve dış süslü parantez hash kapsamına girmez.

| Ölçüm | Önceki onay payload’ı | Reader / R2 final payload |
|---|---|---|
| Byte sayısı | 10743 | 11185 |
| SHA256 | `634208abae660a3a3669cc4c9a5a59105605419a2b1c71eae50c7c16eaa42202` | `b1dd34f588e360b92041275f12b6f5b1f996b7fb1ef3a207cb3f3c91e30364c1` |
| Başlangıç hex | `5b 0a 20 20 7b 0a 20 20 20 20 22` | `5b 0a 20 20 20 20 7b 0a 20 20 20 20 20 20 22` |

İlk fark sıfır tabanlı offset **4**: eski byte `7b` (`{`), yeni byte `20` (boşluk).
Eski payload bağımsız dizilerin iki boşluklu biçimlendirmesini, reader ise authority
nesnesine gömülmüş dizilerin ek girintisini içerir. **442 ek byte yalnız JSON whitespace’tır**;
string dışı whitespace kaldırıldığında token byte’ları tamamen eşittir.
İlk 17 seal nesnesi ve beş ilave seal’in değerleri korunmuştur; toplam **22** seal vardır.
Eski belgelerdeki “altı ilave” ifadesi tarihsel hata olarak yerinde bırakıldı.

`payload-generator.cs.txt` aynı System.Text.Json/UTF8/SHA256 algoritmasıyla çalıştırıldı.
Final authority yazıldıktan sonra tekrar üretim birebir aynı payload’ı verdi.
JSON semantiği aynı kalsa bile yeniden girintileme, CRLF veya dizi içi whitespace değişimi
hash’i değiştirir. Final authority’nin dizileri yeniden serialize edilirse payload yeniden
üretilmeli ve hash farklıysa yeni onay alınmalıdır. Mevcut onay yeni hash’e taşınmadı.

## 2. Roadmap disposition seçenekleri

| Seçenek | Gerekli işlem / policy etkisi | Değerlendirme |
|---|---|---|
| Açık yetkili taşıma | İki JSON’u `docs/records/audits/2026-09/mod-0183-root-uptake-recovery-01/` altına byte-identical `git mv`; canlı referanslar aynı işlem/commit içinde güncellenir. Yeni path’ler için yeni provenance kaydı, seal ve payload gerekir. Eski records/provenance/snapshot içindeki orijinal referanslar değiştirilmez; append-only relocation kaydıyla çözülür. | Records-only kuralı korunur, fakat daha geniş path/provenance/payload değişimi gerekir. Bu pakette seçilmedi ve taşımaya yetki varsayılmadı. |
| Exact-path kuralı — seçilen | İki roadmap path’i yalnız mevcut SHA256 ve `historical-data` türüyle kabul edilir. | Kaynak/provenance/path byte’larını koruyan en dar aday. Roadmap klasörüne genel istisna yoktur. |

İki exact path/hash, `guard-policy.patch` ve `rule.patch` içinde açıkça sabittir.
Provenance hâlâ records altında olmak ve exact hash/line eşleşmek zorundadır.
Karar/payload doğrulaması, canonical target doğrulaması, historical empty-target şartı,
symlink/traversal reddi, presence kontrolü ve consumed-inventory eşitliği korunur.
Komşu path, farklı hash ve active-tool türü üç yeni testte path kapısında reddedildi.

**Yalnız records’a kopyalamak çözüm değildir:** tarayıcı orijinal roadmap JSON’larını
okumaya devam eder; original scan hit’leri kalır. Bu aday kopya seal eklemez; orijinal
path’leri exact hash ile doğrudan disposition eder. `relocation-reference-inventory.txt`
taşıma seçeneğinin referans envanterini içerir (çok uzun satırların metni kısaltılmıştır).

## 3. Eski working artifact

`mvp6-root-guard-disposition-recovery-01/authority-candidate.json` ayrı, taranan bir
working artifact’tır; final seal listesine eklenmez. Final patch onu aynı dizinde
`authority-candidate.json.archived.txt` adına **%100 aynı byte’larla** taşır ve aynı
işlemde yeni `relocation.md` kaydı ekler. Gerçek checkout’ta taşınmadı.

Canlı kod/plan/config referansı bulunmadı; bulunan eski manifest, patch, audit ve
baseline referansları tarihsel snapshot/provenance’tır. Bunları rewrite etmek yerine
relocation kaydı eski path → archive path + SHA256 bağlantısını sağlar. Arşiv kopyası
pakette `archived-working-authority.txt` olarak da korunur. Uygulamadan önce hash
kontrolü, taşıma ve relocation kaydı tek işlemde tamamlanmalıdır; ek canlı referans
ortaya çıkarsa aynı işlemde güncellenmeden activation yapılmamalıdır.

## 4. Fixture uygulama yetkisi ayrı

Eski `7aed354f…` fixture patch’i uygulanmaz: üç seal’i koşulsuz tekrar ekliyordu;
final 22 seal ile duplicate-source oluşturur. Yeni `fixture-fix.patch` hash’i:
`87932c261ff46f6c7464f9f28d556b7a9e8b0783411c9200b6d86d30aa8b66bf`.

Yeni exact diff yalnız fixture’ın `ReadAllText`/`WriteAllText` kopyasını `File.Copy`
ile değiştirir; BOM/encoding dahil byte’ları korur. Seal/provenance üretmez, mevcut
seal’i yeniden hash’leyip meşrulaştırmaz, fixture listesini genişletmez. Bu yeni diff’in
real-checkout uygulama izni eski disposable hazırlama izninden türetilmez.
Final patch bu diff’i içerir; onay metni fixture iznini ayrıca açıkça sayar.

## 5. Entegre doğrulama ve sınırlar

Disposable checkout: `/private/tmp/mvp6-guard-payload-path-r2/checkout`.
Mevcut tracked + untracked içerik kopyalandı; tarayıcının görebildiği ignored working
artifact ayrıca dahil edildi. Test öncesi önerilen arşiv taşıması yalnız burada yapıldı.

Tam komut:

```text
dotnet test /private/tmp/mvp6-guard-payload-path-r2/checkout/tests/architecture/TenantArchitecture.ArchitectureTests --no-restore --no-build --logger 'trx;LogFileName=r2-full-authorized.trx'
```

İlk restore/build tamamlandı, ancak VSTest sandbox socket nedeniyle abort etti.
İzinli tekrar: **58 test, 54 PASS, 4 FAIL, 0 SKIP; exit 1**.

- DocsPathGuard: **38 PASS, 1 FAIL**. 34 mevcut negatif, bir pozitif fixture ve üç yeni
  exact-path negatif test geçti. Production girişindeki tek hata, synthetic kararın
  `DOCS_PATH_OWNER_DECISION` yerine `SYNTHETIC_TEST_ONLY` olmasıdır — beklenen fail-closed davranış.
- Disposable harness: **2 PASS**. Aynı private `ReadAuthority` / `VerifyRoot` gerçek
  metotları reflection ile çalıştırıldı: bütün repo inventory scan’i syntheticFixture=true
  ile geçti; production reader false ile synthetic kaydı reddetti. Scan algoritması
  değiştirilmedi; harness final uygulama diff’ine dahil değildir.
- Diğer **3 FAIL**: `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun`
  (Platform PpmAuditRetentionPolicySeedMongoTests, DisposableStandaloneMongo) ve iki
  JwtClockSkewGuard testi (HumanCapitalService, TalentEcosystemService Program.cs).
  İlgili dosyalar/testler bu adayda değişmedi. Bunlar tam-suite readiness engelidir;
  kapsam dışı düzeltme yapılmadı, baseline test koşusu iddia edilmedi.

**Synthetic kayıt production consent değildir.** Delivered authority/decision
UNAPPROVED kalır. Test checkout’unda yalnız status/kind/owner/decision hash binding’i
synthetic fixture olarak değiştirildi; iki payload dizisi final byte’larıyla aynı kaldı.
Production positive green henüz kanıtlanamaz; gerçek yeni owner kaydı olmadan
beklenen red kaldırılmadı. Full-suite green veya activation onayı talep edilmiyor.

22 kaynak seal’i, provenance ve canonical hash’leri doğrulandı. Tüm önceden mevcut
real-checkout dosyaları paket yazılmadan önce baseline hash’leriyle tekrar karşılaştırıldı:
değişiklik yok. `preservation.txt` ve `seal-verification.json.txt` kanıttır.

## 6. Tek seferlik karar

Kopyalanabilir tam karar metni `OWNER-APPROVAL.md` içindedir. Bu metin mevcut task’ın
aday hazırlama yetkisinden ayrı, **gelecekteki exact uygulama için** istenir. Kullanıcı
kararı gelmeden bu paketin hiçbir diff’i gerçek checkout’a uygulanmayacaktır.
Authority/decision taslakları özellikle UNAPPROVED kalır; owner kararı sonrasında
oluşturulacak gerçek consent kaydının byte’ları ayrıca hash’lenmelidir. Bu paket o
gelecekteki dosyanın hash’ini uydurmaz ve activation yetkisi vermez.
