# WP-0230-DL12-L10N v1.1 — bağımsız ürün kaynak/içerik doğrulaması

Sonuç: **UYGUN / 0 açık engelleyici veya kritik bulgu**.

İnceleyen: /root/pvg_dl12_l10n_prompt_review_01. Ürün yazarı ayrı app worker oturumudur; bu inceleyen uygulama yapmadı. İstenen inherited gpt-6.2-astra/high; gerçek model/effort araçlardan ölçülemediği için verilemiyor. Tarih 2026-11-21 Asia/Baku.

## Kesin kaynak ve yetki

- Taban `ab039c2c53d82edf8413bae4e3304d21b57e04e4`; tree `47c42a86d940992fca0f063f18c3667a3b23342f`.
- Teslim `43f856f22f0549be1d61fab09d40086b755661ba`; tree `72e59b13b99d4fa5ba7ccacb2544145128f4d7f2`. Gerçek commit objesinde tek parent tabandır; base..head tam 1 commit.
- Worker `C:\Users\epenc\.codex\worktrees\pvg-dl12-prep-web\ERP-vNext-git`; dal `feature/pvg/mod-0230-dl12-l10n`; git --no-optional-locks status --porcelain=v1 --untracked-files=all boş. HEAD teslim SHA'sıdır; HEAD diff exit0, cached check exit0.
- Kesin prompt v1.1 SHA256 `501ed6f6cf4284984f4e0bf6c4554f1c065440ba4c1eddc1e3c084043ba69f8c`; 29 kaynak digest `0a0d377739f39830eeed899290d7f5514b8c3b69254ff831855429e958d1a8ac` gerçek Git objelerinden yeniden doğrulandı. Worker before.sources içindeki29 SHA gerçek base git-show ile aynı.
- Rev21 pack LF `ea4ff430d0b2f5f07c8c2482f34311e7897452be597b69b16b822da277b6f7f4`; domain LF `2411fa9500c7696b0751767be23661bc3db6c87468821ac43f7e9d56ba62ef8b` değişmemiştir. Dar7RESX/tekanahtar kapsamı ve önceki HIGH insan onayı/kapanış sınırları korunur.
- Normalgate proof commit `5fe5f148d8e839384f3a3e655e524a980d4fe84b` içindeki L10N capture/report/stderr Git objeleri okundu: actual argv check --queue pvg, --prepare yok; sourceHEAD taban; review commit `85c49b14496ef09e2510dfb6d332667443eb4810`; captureexit0, report DAĞITIM DENETİMİ/sonuç geçti/çıkış0, stderr boş. Rapor rawSHA ve footer yeniden hesaplandı; queue start/end özeti eşit. Yeni normalgate çalıştırılmadı.

## Tam diff ve yedi XML

Gerçek base→head tam diff okundu: yalnız aşağıdaki yedi sayfa RESX yolu, her birinde1 eklenen satır/0 silinen satır; toplam7+/0-. git diff --no-ext-diff --no-textconv ile dış diff sürücüsü kapatıldı; git diff --check base head exit0. SharedResource, bridge, JS, Razor, C#, policy, CI, test, paket veya yönetim kaydı değişikliği yoktur. Her yeni data elemanı satır118dedir.

| Dil | Önce → sonra | xml:space | Yeni gerçek value |
| --- | --- | --- | --- |
| en | 111 → 112 | preserve | This record can no longer be edited. |
| tr | 111 → 112 | preserve | Bu kayıt artık düzenlenemez. |
| fr | 111 → 112 | preserve | Cet enregistrement ne peut plus être modifié. |
| es | 111 → 112 | preserve | Este registro ya no se puede editar. |
| zh | 111 → 112 | preserve | 此记录已无法编辑。 |
| ar | 111 → 112 | preserve | لم يعد من الممكن تعديل هذا السجل. |
| ru | 111 → 112 | preserve | Эту запись больше нельзя редактировать. |

Yedi gerçek base/head Git blobu ve current worker ham dosyaları ayrı okundu. Her dilde unique111→112, duplicate0, empty0, eski anahtarların sırası aynı, tek yeni anahtar DraftNotEditable, yedi keyset eşit. Yeni data öznitelikleri yalnız name ve xml:space=preserve; tek value child ve exact prompt çevirisidir. Yeni metinlerde placeholder/English-echo yoktur. Kısa ret cümleleri action/button Title Case kuralına tabi değildir.

Eski111 data elemanı tag/öznitelik/text/sıra ve bütün iç çocukların tag/attribute/text/child-tail/sırası recursive signature ile karşılaştırıldı: hepsi eşit. Bunun yanında çok daha güçlü bir bayt karşılaştırması yapıldı: yalnız tam yeni data satırı ve onun satır sonu bir kez çıkarıldığında yeni canonical Git blobu eski Git blobuna byte-byte eşit oldu. Aynı işlem current CRLF ham dosyalarında yapıldığında worker before.json rawSHA birebir geri geldi; normalize edilmiş eski ham dosya base Git blobuna eşittir. Bu7/7 byte recovery dosya başlığı, yorumlar, diğer XML elemanları, eski girinti/satır sonu biçimi ve metinlerin korunmasını da kapsar.

### İlk RED değerlendirmesi

Orijinal commands.json exit dizisi `[0,1,0,0,0,0,0,0,0]`. Tek exit1 ikinci kayıt, ilk ElementTree karşılaştırmasıdır. Gerçek Git XML çiftlerinde tek sibling-tail farkı tüm dillerde son eski eleman OptionsLoadFailed için gözlendi: önce `\n`, sonra `\n  `. Eski elemanın kendisi veya value childı değişmemiştir; bu iki boşluk yeni kardeş data elemanının satırbaşındaki girintisidir. ET.tostring(data) bu kardeş ayırıcı tailini de serileştirdiği için ilk gözlemci reddi oluşmuştur. Yeni satır çıkarılarak bütün eski baytların birebir geri alınması, bunun old-data edit olmadığını ayrıca kanıtlar. İç çocuk içerikleri/taili karşılaştırmadan çıkarılmadı. Ürün kusuru veya kırmızı testle commit istisnası yoktur; üçüncü kayıt düzeltilmiş ölçüm exit0, ardından explicit7path stage/list/tamstageddiff/check0/commitexit0 kayıtlıdır. Kayıtlı süreç çıkışları tarihsel worker beyanıdır; bu incelemenin bağımsız Git/XML ölçümü bunların ürün sonucunu ayrıca doğrular.

### Gerçek raw/LF özeti

LF işlevi JSON kaçış metninden kopyalanmadı: actual bytes([13,10]) → bytes([10]) ile normalizasyon yapıldı. Current worker dosyaları CRLF, Git blobları canonical LFdir. after.json rawSHA current ham baytlara; lfSHA gerçek normalize baytlara ve head Git blobuna eşittir. before.json rawSHA yalnız eklenen yeni satır çıkarılmış CRLF ham dosyaya; lfSHA aynı verinin gerçek LF dönüşümüne/base Git blobuna eşittir. Yedi dilin her iki raw/LF ayrımı7/7 doğrulandı.

| Dil | Önce LF SHA256 | Son ham SHA256 | Son LF/Git SHA256 |
| --- | --- | --- | --- |
| en | `363f3a6f2b75a30deeacf431db7c98171ec28043f7014ac5248ccdf5f4e4dd7d` | `c388f73549cc7506b38601c7094277084865701d8689282a66169aa22272cbdc` | `c84acdcf5f0f3b69d8eadbe2d90ee06f05aabf182c1afed62c4a62d7f58ffeba` |
| tr | `4280b7001b4b8a7c41a3f3b457dd156d64d8b53146f89ce9d2fa7805c952c811` | `6e114bc60dfa2697a635c8516e66a6110d35010d1fcf6e267c2cf1e73a6b23b7` | `bed24e9c64a072f00c6c8cdb9c7a0b7edb553e9d55464c5a37fc168b1c554e0e` |
| fr | `1eddcc40bd378a87dcb2df87ed257e5e27220f134bb95cc00ebf517a6b6f617f` | `21ba1cf5d45a37fa9ac2f991710beb00b3d3d96873c51e521253ae9e92fc198a` | `7a34297b156f66a4bd2c2b5c48e68f229e6cb897bca9084b0c8c6bcbd07bf2a1` |
| es | `740d19552a2ca92870326e82600719fee1fe537de85f507bad902af4afe26e60` | `1641c4c897baaea72cd10564c8ed1b5b70c34f68175a6cb544f5c107a2498370` | `81f09e201114702145470871f1a4dd0efd4ab1dc68fa52e7a635286f5b97dc46` |
| zh | `5c8720568842dfd33388c3ad84d604155076e2d9e1563485947fbdcdcda5c6b8` | `8f792cb6443e3e8042f16b7a6fa63e2dd96505bceb7b9a65ef40c28507817817` | `e153dac3a7c1b9c97920eff85fb02184f378b6e309fe36cfee92f2ba3befbe37` |
| ar | `337fb3e74c03aea9f2b6b6dd21a21d731613ca2c32761a5fca48a9b387e86df1` | `ac8dde37cb427d8c41d417bc39070b135cc64cae80cf9391a2e62d086cd86774` | `834f7262c58f10cac74963b1e4d440935d174480772ae41b42eedd1105dfbc55` |
| ru | `6c7cb4297038c9dd3358a2f12ea906a5ca7e95b0f939c22ccb7964a6694811d1` | `8f169e2235da7975a1920f78e94dc3988b0dcbf3044027ab0e4fed51ed8c705c` | `98ea5b3ad0162cf83c3ead6c942cfef2d668428215ab7c47ee0b492a8168b322` |

## Orijinal kanıt dosyaları

Aşağıdaki dört orijinal dosya doğrudan worker Temp dizininden ham bayt olarak okundu ve RAW-WORKER-ARTIFACTS.json içindeki validate=True base64 çözümüyle byte-byte eşit bulundu. Length/SHA hem envelope hem PUBLISH-MANIFEST.json ile eşittir. Manifestin diğer üç dosyaya bağları da doğrulandı. JSON yayınındaki Git satır sonu dönüşümü ham kanıt yerine kullanılmadı.

| Orijinal | Bayt | raw SHA256 |
| --- | ---: | --- |
| before.json | 6976 | `6621ea24e877279875402ef063e4661c8469021311a4cb4d0803ed3bb59f2fda` |
| after.json | 4323 | `1485e7c8e60939e853007eddfb79180a031e29ada681072bdeb242746afdd57e` |
| commands.json | 13745 | `828249a4acd47a7964d676f1abee8ab56d296543e64e7470941582904b4d32b1` |
| manifest.json | 1882 | `3134c13410d8cf334e3f479e382a6b2493b0bdd8e8e8daa6042c536608ff62b4` |

Kaynak orijinal dizini: `C:/Users/epenc/AppData/Local/Temp/pvg-dl12-l10n-evidence/WP-0230-DL12-L10N-1795207314259`. Manifest base/head/tree/branch/commits1/numstat/clean alanları gerçek Git ölçümleriyle aynı. commands.json içindeki explicit stage listesi yedi izinli dosyayla sınırlıdır; list/tam staged diff/check/commit adımları ve tek gözlemci RED gizlenmemiştir. Publish manifest report hashinin LF karşılığı da doğrulandı; bu orijinal dört JSON hamSHA kontrolünden ayrı tutuldu.

## Ölçüm kaydı ve sınır

Bağımsız actualmeasurements.json: 55077 bayt; raw SHA256 `268337ca4e3c7a6185e833fd2ccb36ed42a8fc1b82fc7de9422488fa671b1116`. Gerçek argv, exit, stdout byte/SHA ve stderr kayıtları, tam diff, commit objesi,7dil imza/bayt/tail ölçümleri ve ham kaynak manifesti bu JSONdadır.

Salt-okunur Git çağrıları --no-optional-locks ile, Python-B/UTF8/no-bytecode altında çalıştırıldı. Yalnız izin verilen dış klasörde actualmeasurements.json ve bu değişmez REVIEW.md yazıldı. Repo/worker dosyası, index/ref/queue/commit/push/PR/merge işlemi yapılmadı. CT entegrasyonu bu incelemede yapılmadı.

Bu LOW metin teslimi için source/XML/diff/rawkanıt doğrulaması UYGUNdur; açık ürün bulgusu0. Çalışma testi, dotnet/npm/UI/DB/runtime/bridge/E4/live veya operasyon başarısı ölçülmedi ve iddia edilmez. resx-check yalnız SharedResource kapsamlıdır; PAGE.RESX üzerinde çalıştırıldığı söylenmez. Bu teknik kaynak/içerik kararı otomatik CT merge, runtime veya yayın yetkisi oluşturmaz.
