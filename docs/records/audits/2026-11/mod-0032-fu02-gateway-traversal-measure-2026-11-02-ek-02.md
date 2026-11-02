# Ağ geçidi yol aşma ölçümü — ek 02: meşru yolların korumasız tabanı (2026-11-02, CT · Yetki)

Ek kayıt; önceki kayıtlar değiştirilmez. Üçüncü bağımsız bakış F-12 ve F-6'ya cevaptır.

- Ölçülen commit: kule dalı `chore/ct/auth-ct-01` @ `f30a65627` (ağ geçidi kodu ve testleri `6f169f677` ile aynı:
  bu aralıkta yalnız paket ve kayıt dosyaları değişti).
- Konak: var olan `Mod0290Fu04ProductMasterTraversalTests` (gerçek Kestrel + Ocelot + gönderilen `ocelot.json`'un
  `/api/product-master/{everything}` rotası, servis yerine kayıt tutan Kestrel). Program.cs'in kimlik doğrulama, kiracı
  çözümü, CORS, sağlık ve metrik ara katmanları bu konakta YOKTUR.
- Komut: `dotnet test gateway/Diten.ApiGateway.Tests --filter "FullyQualifiedName~Mod0290Fu04ProductMasterTraversalTests" --logger "console;verbosity=detailed"`
- Geçici fark (koşudan sonra geri alındı; `git status --short gateway` boş): `%252e%252e%252f…` satırının altına on
  `InlineData` satırı eklendi (aşağıdaki tablonun ilk sütunu).
- Sonuç: `Toplam test sayısı: 17 · Geçti: 17`.

| İstek | Ağ geçidi | Servisin RawTarget'i | Servisin yönlendirdiği yol |
|---|---|---|---|
| `/api/product-master/files/report.v1.pdf` | 200 | aynı | aynı |
| `/api/product-master/v1.2/items` | 200 | aynı | aynı |
| `/api/product-master/files/.../x` | 200 | aynı | aynı |
| `/api/product-master/files/a..b` | 200 | aynı | aynı |
| `/api/product-master/files/..foo` | 200 | aynı | aynı |
| `/api/product-master/files/.well-known` | 200 | aynı | aynı |
| `/api/product-master/files/100%25.pdf` | 200 | `/api/product-master/files/100%25.pdf` | `/api/product-master/files/100%.pdf` |
| `/api/product-master/files/a%2Fb` | 200 | `/api/product-master/files/a%2Fb` | `/api/product-master/files/a%2Fb` |
| `/api/product-master/files/x?q=../..` | 200 | `/api/product-master/files/x?q=../..` | `/api/product-master/files/x` |
| `/api/product-master/files/a%255cb` | 200 | `/api/product-master/files/a%5cb` | `/api/product-master/files/a\b` |

Gözlem: bütün meşru satırlar korumasız tabanda 200 alır ve rota öneki içinde kalır; `a%2Fb` ve sorgu dizesi olduğu gibi
iletilir. Son satır: bir kez kodlanmış `%255c`, ağ geçidinde `%5c`'ye çözülür ve servis onu `\` olarak yönlendirir
(çift çözüm meşru veride de gerçekleşir).

Ölçülmeyen (bu konakta ara katman yok): CORS ön uçuşunun 204'ü, kimliksiz isteğin taban cevabı (AC-5), `/metrics`
eşleşmesi, gerçek `Program.cs`'in Kestrel'de açılması.
