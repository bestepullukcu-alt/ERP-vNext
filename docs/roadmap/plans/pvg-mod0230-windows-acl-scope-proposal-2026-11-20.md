# MOD-0230 — Windows erişilemeyen dizin testi için tek kapsam önerisi

Durum: **ÖNERİ / bağımsız inceleme ve sahip onayı bekliyor.** Tarih: 2026-11-20.
Bu belge çalışma izni, pack değişikliği, E2 kabulü veya dağıtım kapısı değildir.

Kaynak CT HEAD: `ce02d1828479026aa81e510d9f1e6ae7e43fc15c`. Mevcut kimlik ve revizyon korunur:
MOD-0230 — Case Intake & Triage, revizyon 20. Onaylı pack SHA-256
`186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01`; domain SHA-256
`914c0c1dab0979d3ec48e3d4f6e08646ad0bf4494f9b73a97fa27bca4c5ead9c`.

## Ölçülen sorun ve kapsam sınırı

WIN v1.2'nin normal kapıdan sonraki çalışmasında iki izinli dosyanın WIP değişiklikleri mevcut. CT'nin aktardığı
iki hedef test ve 35 gerçek Mongo testi yeşil; bu belge bunları yeni bir koşuyla doğruladığını iddia etmez.
Salt okunur incelenen `pvg-prep-win-api.trx` ise **395 yürütülen / 394 geçen / 1 başarısız / 0 atlanan** kaydediyor.
Tek hata `Configuration_scan_fails_closed_on_inaccessible_directory` metodunda, satır 1856'daki
`File.SetUnixFileMode` çağrısının Windows'ta `PlatformNotSupportedException` üretmesi. Test erişim reddi önkoşuluna
ve scanner assertion'ına ulaşamıyor. Mevcut iki dosyanın düzeltmesi bunu çözemez; sabotaj/commit ve E2 tamamlanmadı.

Tek öneri: mevcut `WP-0230-DL12-PREP-WIN` kapsamına aşağıdaki **bir dosyanın yalnız bir test metodunun gövdesi**
için açık Windows taşınabilirlik istisnası eklemek:

`services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs`

Metot: `Configuration_scan_fails_closed_on_inaccessible_directory` (mevcut satırlar 1850–1870).
Dosyanın asıl DL-3 sahipliği korunur; dosyanın bütünü PREP-WIN'e açılmaz. Başka test, using bloğu, yardımcı sınıf,
`ScanFixture`, `PvgConfigurationScanner`, host veya ürün davranışı değişmez. Gerekli .NET türleri metot içinde tam
adlarıyla kullanılabilir. Yeni MOD, FU, WP kimliği veya ürün kapsamı önerilmez.

## Somut uygulama sözleşmesi — onaydan sonra

Windows dalında mevcut private `ScanFixture` tarafından oluşturulmuş `fixture.Root/locked` dizininin özgün erişim
ACL'i değişiklikten önce bağımsız bir kopya olarak saklanır. Hedefin aynı fixture içinde, testin kendi `locked`
dizini olduğu doğrulanır; reparse yönlendirmesi veya ortak/üst dizin hedeflenmez. Mevcut .NET 8 ACL API'leriyle
yalnız geçerli test kimliğinin SID'sine, yalnız bu dizinde ve kalıtım yaymadan `ListDirectory` reddi uygulanır.
`ReadPermissions`/`ChangePermissions` ve ACL'i geri yükleme imkânı kapatılmaz; FullControl reddi, genel hesap/grup
ACL'i veya fixture dışı yetki değişimi yoktur.

Erişimi gerçekten reddeden DACL uygulandıktan sonra mevcut
`Directory.EnumerateFileSystemEntries(locked).ToArray()` önkoşulu **UnauthorizedAccessException** üretmelidir.
Runner bunu sağlayamıyorsa test açıkça başarısız olur; skip, early return, sahte exception veya önkoşulu geçerli
sayma yoktur. Ardından gerçek mevcut scanner'ın fixture kökünü tarama çağrısı fail-closed exception assertion'ını
karşılamalıdır. `IgnoreInaccessible=false` ve scanner'ın geri kalan davranışı değişmez.

`finally`, assertion veya tarama hata verse de özgün DACL'i ve erişim/kalıtım durumunu geri yükler. ACL geri yükleme
hatası yutulmaz. Kilitlemeden önce ve geri yüklemeden sonra aynı nötr JSON dosyasının listelenebildiği ve scanner'ın
bu dosyayı gerçekten gördüğü erişilebilir pozitif kontrol bulunur. Özgün ACL'in geri geldiği ve fixture dispose
sonrasında kendi geçici kökünün kalmadığı ölçülür; helper'ın best-effort temizliği başarı kanıtı sayılmaz. Bu
kontroller hedef metodun gövdesinde kalır.

Unix dalında mevcut `SetUnixFileMode(None)` ve `finally` ile izinleri geri verme akışı korunur. ACL dalı yalnız
Windows'ta seçilir. Test adı, erişilemezlik önkoşulu ve scanner'ın fail-closed beklentisi gevşetilmez.

## Mevcut bağımlılıklarla yapılabilirlik dayanağı

Test projesi `net8.0` hedefliyor; mevcut framework referansı ve paket listesi okundu. Kurulu
`Microsoft.NETCore.App.Ref/8.0.34/ref/net8.0` içinde `System.IO.FileSystem.AccessControl.dll`,
`System.Security.AccessControl.dll` ve `System.Security.Principal.Windows.dll` mevcut. Aynı referans paketinin XML
belgelerinde `FileSystemAclExtensions.GetAccessControl/SetAccessControl(DirectoryInfo, ...)`,
`FileSystemAccessRule`, `FileSystemRights.ListDirectory`, security-descriptor kopyalama ve
`WindowsIdentity.GetCurrent().User` API'leri bulunuyor. Dolayısıyla kaynak düzeyinde yeni paket/framework gereksinimi
görünmüyor. **Derleme veya ACL deneyi yapılmadı;** gerçek runner'da erişim reddi/geri yükleme başarısı henüz ölçülmedi.
Ek bağımlılık ya da metot dışı değişiklik gerekirse bu önerinin sınırında durulur; sessiz kapsam genişletilmez.

## Kabul ve bağımsız doğrulama

- Windows'ta hedef test 0 başarısız/0 atlanan; tam API süiti 0 başarısız/0 atlanan. Yeni test sayısı varsayılmaz.
  Pozitif erişim kontrolü, gerçek UnauthorizedAccessException önkoşulu ve scanner fail-closed ölçüsü ayrı görünür.
- Mevcut Mongo guard'ları, iki hedef test ve 35 gerçek Mongo testi yeniden yeşil; özgün 27017 yasağı, binary yolu/
  varlık kontrolü, replica-set/transaction koşulları, kendi süreç/port/veri izolasyonu ve cleanup korunur.
- Unix runner'da aynı hedef test ve ilgili API regresyonları geçer; chmod dalı ve güvenlik assertion'ları korunur.
  Windows sonucu Unix çalıştırmasının yerine geçmez.
- İki ayrı **derlenen** sabotaj yalnız dış repository-shaped scratch kopyasında uygulanır: (1) Windows deny
  uygulamasını kaldırmak erişilemezlik önkoşulunu RED yapar; (2) deny yerindeyken scanner'ı inaccessible dizini
  atlayan fail-open davranışa çevirmek scanner assertion'ını RED yapar. İkinci ölçüde önkoşul hâlâ gerçek
  UnauthorizedAccessException olmalıdır. Sabotajlar gerçek çalışma dalına/commit'e taşınmaz.
- ACL geri yükleme, yeniden erişim ve geçici fixture temizliği normal/başarısız assertion yollarında kanıtlanır.
  Yeni başarısızlık yutma, skip veya assertion kaldırma kabul edilmez; kanıt yalnız HTTP/çıkış koduna indirgenmez.

## Kapılar, WIP ve geri dönüş

Önce bu tek kapsam önerisinin bağımsız incelemesi ve insan onayı gerekir. K5 CT takip sahibidir; Beste'nin doğrudan
insan onayı kayıtla bağlanır. Mevcut genel kapsam onayı, DL-3 dosyasındaki bu yeni istisna verilmiş gibi kullanılmaz.
Onaydan sonra paket yazarı gerçek pack/domain uyumunu, PREP-WIN dosya/metot sınırını ve ilgili kabul metnini işler.
Değişen kesin sürüm yeniden resmî inceleme/HIGH onay kaydına bağlanır; yenilenmiş worker prompt'u normal HIGH
inceleme ve dağıtım kapısından geçmeden bu üçüncü dosyada çalışma başlamaz. Bu belge tek başına izin değildir.

**Aynı tek öneride istenen ikinci açık yetki: bir defalık WIP koruma checkpoint'i.** WIN v1.2'nin 394/1/0 sonucu
normal teslim commit koşulunu karşılamadı; dirty iki dosya üzerinde yeni normal kapı başlatılamaz. Bu nedenle sahip
onayı iki somut eylemi birlikte kapsamalıdır: yukarıdaki tek Windows ACL test-metodu istisnası ve aşağıdaki donmuş
iki WIP dosyasının yalnız işçinin aynı özel `feature/pvg/mod-0230-dl12-prep-win` dalında yerel CHECKPOINT commit'i.
Bu, mevcut normal teslim commit koşuluna yalnız bu koruma için **bir defalık açık insan izni** talebidir; izin
verildiği varsayılmaz ve bu belgeyle stage/commit uygulanmaz. Yeni worktree gerektirmeden mevcut çalışma korunur.

Checkpoint'e girebilecek kesin iki dosya ve kaydedilmiş **Windows işçi disk baytlarının SHA-256** değerleri:

| Tek izin istenen WIP yolu | Donmuş işçi disk SHA-256 |
|---|---|
| `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoTestHarness.cs` | `f8a91d03ab788143a62b2578f774d74c76b847e3a3fbb7a513f6edf06a78b053` |
| `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgPersistenceBoundaryScanTests.cs` | `763b3305f1207d7228e099437babc87bafb8140d43956b46b6aaf8933bba068a` |

Kaynak snapshot kaydı `docs/records/audits/2026-11/pvg-dl12-prep-win-01/stopped-v1.2/capture.json`, disk-hash kaydı
aynı dizindeki `green-hashes.json`, tam patch `worker-wip.diff` dosyasıdır; kaydedilen patch SHA-256
`bd7ebf58ac4866fdba94729d0dfb29f688ec87e8a26d7cc80d1f7bbb9c50617f` gerçek patch baytlarıyla eşleşti.
Snapshot işçi HEAD'i `58920519a17e8f4c085738642b33e36689bc8fee` olarak kaydedilmiştir. Commit öncesinde exact iki
staged dosya listesi **ve tam staged diff**, CT/bağımsız denetimden geçmeli; kaynak disk SHA'ları ve snapshot
eşleşmesi doğrulanmalıdır. Üçüncü dosya veya başka değişiklik checkpoint'e giremez.

394/1/0 süitine rağmen bu checkpoint yalnız korumadır: sabotaj, E2, kabul, tamamlanma, push, PR veya CT'ye merge
izni değildir. Bağımsız E2 tamamlanmadan checkpoint CT'ye birleştirilmez. Ardından CT'nin yeni kesin pack/domain
resmî inceleme/HIGH kayıtları işçiye çakışmasız merge ile alınır; temiz yeni ExpectedHEAD ve kaynak paketi ölçülür.
Yenilenmiş HIGH prompt'un normal kapısı 0 olmadan üçüncü dosyada düzeltme başlamaz. Kapı sonrasında yalnız yeni
onaylı metot düzeltilir ve tüm test/sabotaj/cleanup ölçüleri tamamlanır; korunan iki WIP kaynak değişikliği kaybolmaz.

İki mevcut izinli dosyanın WIP'si korunur; bu öneri onları geri almaz veya tamamlanmış/entegre saymaz. Olası geri
dönüş yalnız yetkilendirilmiş hedef metot düzeltmesini ayrı diff/commit sınırında geri almaktır; ilk iki dosya ve
aktif WEB işçisi etkilenmez. ACL rollback'i testin `finally` sorumluluğudur; repo rollback'ine bırakılmaz.
PREP-WIN ancak yeni ölçümler, bağımsız E2 ve entegrasyon sonrası tamamlanabilir; ana feature iki hazırlığın kendi
kapıları ve bağımsız E2 entegrasyonlarından sonra açılır. Operasyonel runtime kapalı kalır.

## Salt okunur kaynak ve kanıt bağları

| Kaynak | Ölçülen dayanak / SHA-256 |
|---|---|
| `PvgServiceApiHostTests.cs:1850–1870` | Testin mevcut chmod/önkoşul/finally akışı. Windows disk CRLF SHA-256 `1b37d1ccace74b60753df0cd8f339bc0fd3aa0748baa878ff96988390bf64bcc`; kaynak HEAD `ce02d182...` Git blob LF SHA-256 `ac6dc215a62335b282af56a2d38c2c1bde7e32ea3627666448735de1048fc01d` |
| Aynı dosya `:1898–1928`, `:2004–2011`, `:2125–2149` | Private temp fixture, best-effort dispose, `IgnoreInaccessible=false` ve gerçek scanner yürüyüşü |
| `Diten.PvgService.Api.Tests.csproj` | `net8.0`, mevcut proje. Windows disk CRLF SHA-256 `278406f96cb884227448f0f04b57acc86839823b73e10050ced69ef9e74bf666`; kaynak HEAD `ce02d182...` Git blob LF SHA-256 `09842861bc5bd092e5fdae93c5848e2e23561e990076f73bc4ae50cfdc4e6a8b` |
| `C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/win-58920519-01/pvg-prep-win-api.trx` | 394 PASS/1 FAIL/0 skip; `c3b30fb5a2ce4881d6a24e60480b2c4c6cdac69d1178a0455a6791b65c559404` |
| Aynı kanıt dizininde `api-command.txt` | Gerçek test komutu kaydı; `d78c0696cca0a68372d007066fc0c9cb753199f1b9fea4632fbe0efce48325b7` |

Yukarıdaki iki kaynak metnin CRLF disk baytları yalnız CRLF→LF dönüşümüyle kaynak commit'in Git blob baytlarına
birebir eşittir; ZIP Git blob'u taşıdığında LF hash'i esas alınır, disk hash'iyle karıştırılmaz. TRX satırındaki
`c3b30...` hash'i belirtilen Temp kaynağının **ham baytlarıdır**; Git/staging normalizasyonu uygulanmış kopyanın
hash'i olduğu iddia edilmez. Böyle bir kopya hazırlanırsa CT ham/Git çiftini ayrı metadata ile bağlar.

Temp kanıtları yerel kaynaktır; bu belge onların kalıcı arşivlenmiş olduğunu iddia etmez. CT kesin sürüm incelemesi
için kanıt paketine bağlar. Bu hazırlamada kod/ACL/deneme/build/test/restore/npm/commit çalıştırılmadı; yalnız bu
öneri dosyası yazıldı. İstenen model/effort `gpt-6.2-astra/high`; gerçek runtime değeri bu bağlamda **verilemiyor**.
