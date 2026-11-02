# MOD-0230 DL-11 — menü çeviri testinin eski kırmızısı tabandır (sahip kararı)

> Control Tower kaydı, 2026-11-02 13:57. Sahibin sözü, bu kule sohbetinde: «taban-kabul».

- **Durum.** `frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs` Expected HEAD `a729c2f46`'da iki testte kırmızı (`:42` ve `:169` çevresindeki iki test). Neden: sayfasız iki modül, MOD-0189 (`MrpManifestProvider.cs:8`) ve MOD-0191 (`SafetyStockManifestProvider.cs:13`), `PagelessUntil` listesinde değil. `Nav.Module.MRPREPLENISHMENT` ve `Nav.Module.SAFETYSTOCKOPTIMIZATION` yedi `SharedResource.*.resx` dosyasının hiçbirinde yok; CT ölçtü (`SharedResource.en.resx`'te 0 eşleşme), `origin/main`'de de yok. `MOD-0183-DL2-EVIDENCE.md:46-50` aynı iki kırmızıyı koşarak ölçmüş. Prompt incelemesi `WP-0230-DL11-v1.0-inceleme-01.md` F-1 (ENGEL).
- **Karar.** Bu iki kırmızı, adları ve hata satırlarıyla DL-11'in tabanıdır. DL-11 o modüllere dokunmaz (XMC-001). İşin ölçüsü şudur: bu testlerde yeni bir hata satırı çıkmaz. `Nav.Module.CASEINTAKETRIAGE` ve `Nav.Domain.PHARMACOVIGILANCE` yedi dilde eklenir. Her sabotaj, taban satırlarına ek olarak çıkan yeni hata satırıyla kırmızı sayılır. Paketteki "0 failed" şartı bu iki taban kırmızısı dışında aynen geçerlidir.
- **Önerilen ve seçilmeyen yol.** «önce-onlar»: iki modülün eksiği kendi kulelerinde kapanır, DL-11 bekler.
- **Gerileme değerlendirmesi.** PVG'nin kendi anahtarları yedi dilde eklenir ve sabotajla ölçülür; PVG tarafında gerileme yok. İki modülün eksiği onların borcu olarak kalır. O borç kapandığında taban kendiliğinden yeşile döner ve bu kayıt geçersizleşir.
- **Sınır.** Bu söz yalnız bu testin bu iki taban kırmızısı içindir. Başka kırmızıyı, paketin başka şartını ya da kabulü (AC-38, E5) değiştirmez.
