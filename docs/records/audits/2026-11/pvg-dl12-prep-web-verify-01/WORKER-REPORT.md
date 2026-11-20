**WP-0230-DL12-PREP-WEB v1.2 tamamlandı.** Gerçek Razor Edit formu ve üretim `form.js` ile gönderilen veri, MVC model binding ve `ToPayload` üzerinden PVG test host’una ulaştı. Depodan yeniden okumada **bir detail okuması, bir güncelleme ve sürümün 1’den 2’ye yükseldiği** doğrulandı. Tarayıcıda değiştirilen anlatı depoda bulundu. [Taşıma kanıtı](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/verified/transport-evidence.json)

**Ana DL-12 için açık bulgu:** Kullanıcının dokunmadığı bildirici iletişim özeti ve kanıt referansları bu kayıtta korunmadı. Kayıpsız düzenleme ve eksiksiz detail yanıtı ana iş paketinde açık. Gerçek GitHub Linux Phase 1 koşusu, bağımsız E2 doğrulaması ve CT entegrasyonu bekliyor.

| Teslim | Sonuç |
|---|---|
| Dal ve worktree | `feature/pvg/mod-0230-dl12-prep-web` · [işçi ağacı](C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-web/ERP-vNext-git) |
| Başlangıç → tek yerel commit | `58920519a17e8f4c085738642b33e36689bc8fee` → `15791333afa89e447210b8d877dc5407693e9b5b` |
| Git durumu | Çalışma ağacı temiz. Tam staged diff incelendi. `git diff --check` temiz. Push veya PR açılmadı. |
| Model / efor | İstenen `gpt-6.2-astra` / `high`. Gerçek oturum değerleri görünmediği için doğrulanamadı. |
| Pack SHA-256 | Git blob’u (LF) `186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01`. Disk (CRLF) `49397653fd62ef071503442668caf0ff9b124415cf84c4f6378cd03bf9d0b7e3`. LF’ye çevrilen baytlar eşleşti. |

**Üç dosya değişti.** Mevcut C# test sınıfına bağlantılı testler ve gömülü Node/jsdom programı eklendi. Test projesine yalnız `pvgapi` alias’lı API referansı eklendi. Phase 1 iş akışına sabit SHA’lı Node 22 kurulumu ve mevcut lock dosyasıyla `npm ci` adımları eklendi. Üretim JS, Razor, MVC ve PVG host kaynakları ile iki npm dosyasının özetleri başlangıçla eşleşti. [Kaynak SHA-256 ölçümleri](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/source-hashes.json)

**Yerel doğrulama:** .NET SDK `8.0.428`, Node `v22.23.5`, npm `10.9.9`. Temiz `dotnet restore` ve `frontend/Diten.Web` içinde `npm ci --no-audit --no-fund` çıkış `0` verdi, 102 npm paketi kuruldu. `PvgCaseIntakeTriageWebTests` filtresiyle **62 geçti, 0 başarısız, 0 atlandı**, çıkış `0`. Bunların 13’ü yeni bağlantılı vakaydı. Test süresi 3 dk 24 sn, toplam komut süresi 3 dk 38 sn, başlangıç CPU yükü %14, mantıksal işlemci sayısı 12. SDK ilk restore sırasında ASP.NET Core geliştirme sertifikası kurduğunu bildirdi. [Restore günlüğü](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/restore.log) · [npm günlüğü](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/npm-ci.log) · [Son TRX](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/verified/pvg-prep-web.trx)

**Antiforgery ve hata yolları:** Razor token’ının varlığı ve eşitliği hem jsdom çıktısında hem MVC’nin aldığı istekte, header ve form üyesi için ayrı ayrı doğrulandı. İki taşıyıcı eksikken veya token bozukken MVC `400` döndürdü, API güncellemesi sıfır kaldı ve depo sürümü değişmedi. Dokuz runtime, protokol, JS çalıştırma ve güncelleme hatasının her biri bağlantılı testi beklenen nedenle başarısız kıldı. Normal ve sabotaj koşularında kaydedilen 19 çocuk PID’den çalışan kalmadı. [PID kaydı](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/verified/child-cleanup.json)

| Worktree dışı sabotaj | Sonuç | Kaynak SHA-256 öneki | Log SHA-256 öneki |
|---|---|---|---|
| JS çalıştırma kanıtı kontrolünü kaldır | RED | `a053e99f335b` | `9025ff735eb2` |
| Gerçek güncellemeyi sabit HTTP 200 ile değiştir | RED | `bbf92f8d59b7` | `5309d97ca97a` |
| MVC’ye iletilen header’ı düşür | RED | `7c16ac19240e` | `d597a68e391b` |
| MVC’ye iletilen form üyesini düşür | RED | `be169bab665f` | `3d0af5736bfb` |
| Bozuk token’ı köprüde onar | RED | `6eb0b4b12430` | `fcbe6f680e3b` |
| Tarayıcı çıktısında yalnız header’ı düşür | RED | `0d7d1fb7b162` | `4a6e4781761d` |
| Tarayıcı çıktısında yalnız form üyesini düşür | RED | `5a079f5aa415` | `e85e79a41866` |

Yedi değişiklik de derlendi ve ilgili test assertion’ında çıkış `1` verdi. [Test adları, tam kaynak, log ve TRX özetleri](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/verified/sabotages.json)
