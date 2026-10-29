# PVG beşinci dalga — bağımsız doğrulama: WP-0230-DL7A (AC-1, AC-2, AC-3, AC-14, AC-15, AC-20)

> Control Tower kaydı, 2026-10-28. Kuyrukta `DISPATCHED → VERIFIED` geçişinin kanıtıdır. Entegrasyon ya da kabul kaydı değildir.
> İş, CT dalına birleştirildi (kart S-76). Dalga sonu PR'ı ile `main`'e girecek; `INTEGRATED` geçişi o PR birleştikten sonra yazılır.

## Teslim

| | |
|---|---|
| İşçi dalı | `feature/pvg/wp-0230-dl7a-r1` |
| İş commit'i | `645220dbf`; HEAD^1 taban `5f90c216a` |
| CT dalına birleştirme | `c62618335`, merge commit; CT dalı birinci ebeveyn. Önceden `git merge-tree` ile ölçüldü: çakışma yok (ağaç `542ac066`). |
| DoR | `docs/records/audits/2026-10/pvg-wave5-dor-01/DOR.md` |
| Dağıtım kapısı | `docs/records/audits/2026-10/pvg-wave5-dispatch-01/PQG-WP-0230-DL7A-v1.2.txt` |

## Bağımsız ölçüm: KABUL EDİLEBİLİR

Ölçüm yazardan bağımsız bir CT ölçücüsü tarafından, ayrı bir ölçüm worktree'sinde `645220dbf` üzerinde yapıldı.

- **Kapsam.**
  - Değişen 21 dosyanın hepsi izinli yollarda; `git diff --check` temiz.
  - Korunan yollar değişmedi: `Views/Shared`, `Views/Home`, SharedResource, ortak JS ve CSS, `Program.cs`, iki csproj, `GlobalProductsController`, `services/`, `gateway/`.
  - Form alanları değişmedi; tek ek `name="ExpectedVersion"`.
- **AC-1.** Dört görünüm de `_LayoutTenantShell` kullanıyor.
- **AC-2.** On eylemin onunda da ilk satır doğru `pvg.case-intake-triage.*` anahtarıyla `RequirePage` ya da `RequireJson` denetimi. Anahtar yoksa ViewData yazılmıyor ve Gateway çağrılmıyor.
- **AC-3.** Sayfa eylemleri çıplak 403 döndürüyor ve ortak 403 sayfasına yeniden yürütülüyor; yönlendirme yok. W2 HTTP düzeyinde 403 alıyor; gövdede tablo, form, filtre ya da `index.js` yok, `Location` başlığı yok.
- **AC-14.** Doğrulayıcı tam 12 madde basıyor, hepsi OD-PVG-10 kategorilerinden:
  - dışa aktarma (BL-452): 1;
  - toplu silme metin anahtarları: 3;
  - dışa ve içe aktarma anahtarları: 2;
  - tümünü seç başlığı: 1;
  - toplu işlem bağlantıları: 4;
  - seçimi temizle: 1.

  Paketin "diğer beş" maddesinden ve kategori dışı maddelerden hiçbiri basılmıyor. Fabrika denetimi 15/15.
- **AC-15.** Yedi resx dosyasında anahtar kümesi aynı (88 anahtar). Görünümlerde ve JS'te satır içi stil yok (FG-003).
- **AC-20.**
  - `index.js`'te `createList` bir kez çağrılıyor; `serverSide`, `ajax.data`, `buildListUrl`, `pageNumber`, `pageSize` için 0 satır.
  - Vekilde `X-Diten-` ve `ReadJwtToken` için 0 satır. Kiracı yalnız doğrulanmış kullanıcının `tenant_id` claim'inden alınıyor.
  - W7, denetleyicinin `List` eylemini gerçekten çalıştırıyor ve Gateway URL'ini yakalıyor (15 satır).
  - Sürüm üç formda da gidiyor. 409 sürüm çakışması kontrollü hata olarak gösteriliyor (V4, W5).
- **Gizli bilgi (S-27).** Diff'te e-posta, şifre ya da gerçek token yok. Testin imzalama anahtarı çalışma anında `RandomNumberGenerator` ile üretiliyor.

| Süit (ölçücü, `645220dbf`) | Sonuç | 1 dk yük |
|---|---|---|
| PVG vitest dosyası | 25/25 | 3,8 |
| Tüm vitest | 32 kırmızı; hepsi taban listesinde, yeni kırmızı yok. Tabandaki 33. kırmızı bir PVG testiydi ve artık yeşil. | 3,7 |
| `Diten.Web.Tests` filtresi | 57/57 (W7 15 satır, W3 6 satır) | 11,3 |
| Mimari süit | Tabandaki 3 kırmızı, iletiler bayt bayt aynı | 7,3 |

Yük kuralı iki kez çiğnendi. `Diten.Web.Tests` ilk koşusu 31,4 yükte başladı (yeşil); tablodaki koşu, yük 11,3'e inince yapılan tekrardır. "wrongkey" sabotajı 16,2 yükte koşuldu; beklenen kırmızıyı verdiği için tekrarlanmadı.

| Ölçücünün sabotajı | Kırmızıya dönen |
|---|---|
| `RouteDraft`'tan `RequireJson` kaldırıldı | W1, W3 (`RouteDraft` satırı) |
| `Details`'te okuma yerine güncelleme anahtarı | W1 |
| `index.js`'e `?pageNumber=1&pageSize=100` geri kondu | V1 ve iki statik liste testi |
| Vekilde 100 sınırı kaldırıldı | W7'nin iki `length=500` satırı |
| tr resx'ten `VersionConflict` silindi | V6 ve l10n referans testi |
| 409 başarı gibi gösterildi (`form.js`) | V4 |

Sabotajların hepsi geri alındı. Dosya sha256'ları git'tekiyle aynı.

## CT dalında birleşik ölçüm

- **Kod ağacı aynı.** `git diff 645220dbf c62618335` yalnız `docs/` ve `execution/` altında 74 dosya gösteriyor. `frontend/`, `services/`, `gateway/` ve `tests/` farkı boş. Bu yüzden ölçücünün vitest ve `Diten.Web.Tests` sonuçları birleşik dal için de geçerli. Bu checkout'ta `node_modules` olmadığı için vitest burada koşulamadı.
- **Mimari süit birleşik dalda koşuldu**, çünkü belge farkı onu etkileyebilir. Sonuç: 141/146.
  - Tabandaki 3 kırmızı aynı: `AuditTrailStandardTests` ×2 ve `DocsPathGuardTests`.
  - İki ek kırmızı `TenantContradictionSiteGuardTests`'ten geliyor: beklenen 10, bulunan 15.
  - Nedeni ortamdır, kod değildir. Test diskteki her `TenantResolutionMiddleware.cs` dosyasını sayıyor. Bu checkout'ta, inceleme paketlerinden açılmış 5 kopya var: `.ct-teslim/MOD-0230-review-03` … `review-07`, her birinde `gateway/…/TenantResolutionMiddleware.cs`. Bu klasör `.git/info/exclude` ile dışlanıyor ve `git ls-files .ct-teslim` 0 dosya döndürüyor.
  - İzlenen dosyalarda site sayısı 10; ölçücünün temiz worktree'sinde bu iki test yeşildi.

| Birleşik dal ölçümü | Sonuç | 1 dk yük |
|---|---|---|
| Mimari süit | 141/146. Tabandaki 3 kırmızı ile `.ct-teslim` kopyalarından gelen 2 ortam kırmızısı. | 3,4 → 3,5 |

## Bulgular

- **ORTA (bu işten önce de vardı, gerileme değil).** 88 anahtarın 87'si altı dilde İngilizce kopya; yalnız yeni `VersionConflict` çevrilmiş. Tabanda da durum aynıydı.
  - AC-15 metni yalnız "aynı anahtar kümesi" istediği için karşılanıyor.
  - Ama `localization-standard`'ın placeholder yasağı ve Çekirdek madde 4 karşılanmıyor. V6 yalnız yeni anahtarları denetliyor.
  - Karar sahibe soruluyor: çeviriler DL7B'ye mi katılsın, yoksa ayrı bir çeviri işi mi açılsın.
- **BİLGİ.** Ortak `/Home/Status/403` sayfası kabuksuz ve tek dilli. Prompt'ta bilinen boşluk olarak duruyor. İlgili backlog BL-336; ayrı satırı CT açacak.
- **BİLGİ (ortam).** Mimari süit, `.ct-teslim` altındaki açılmış inceleme kopyalarını da tarıyor. CT checkout'larında süit bu yüzden fazladan kırmızı verebilir. Kurulum notu olarak iletilecek.

## Kanıt seviyesi

İş E2 kanıt üretir. DL-7 için gerekli seviye E4'tür. Yeni izin anahtarları henüz hiçbir role verilmedi (grant MOD-0018'in işi), bu yüzden canlı ekran ölçülemedi. Kabul canlı kapıya kadar açık kalır. CI bugün vitest'i ve `Diten.Web.Tests`'i koşmuyor; bu testler yalnız yerelde koşuluyor.
