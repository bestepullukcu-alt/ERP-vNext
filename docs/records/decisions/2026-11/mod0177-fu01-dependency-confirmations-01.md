# MOD-0177-FU01 — bağımlılık teyitleri 01

- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0177-FU01-recall-crm-customer-ref.md` r8, sha256 `fc7f3cd3…df1` (onaylı `bd52f34fc`).
- Kayıt: MVP-1 CT (Beste), oturum `ca7382cf`, 2026-11-05. Kaynak: CRM CT (MOD-0149) iki ileti (2026-11-04 / 2026-11-05; bilgi: MVP-6 CT).
- Bu kayıt paketi değiştirmez; §7 kabul kapılarının durumunu kanıtıyla tutar. DL-1 kabul kaydı buna atıf yapar.

| Kapı | Teyit | Kanıt | Durum |
|---|---|---|---|
| D-3 (Q-1) çağrı yolu | Sunucudan, Gateway (5000) üzerinden, oturumdaki kullanıcının token'ıyla; servis kimliği bu sözleşmede tanımlı değil. Token `tenant_id` taşımazsa 400 (K-11) | CRM CT iletisi 2026-11-04 madde 2; sözleşme taslağı §5a / §1 | **kapandı** — paketin varsayılanı (`ForwardCallerHeadersHandler`), paket revizyonu gerekmez |
| D-2 U-1 şekli | `GET /api/crm/accounts/{accountId:guid}/lookup-validation`; 200 yalnız accountId, code, name, status, isBillable, contractVersion = `crm-account-lookup/1.0` (§2.2); yok / silinmiş / başka kiracı için bayt bayt aynı 404 (§2.1); 503 kaynak okunamıyor; izin `crm.account.lookup-validation` | Teyitli taslak `docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md` sha256 `ec5a10625d0662f4a84bc73dfd799eb7e80ab4b2c8643319d6b786fc1d14b8b0`; MOD-0149 paketi (§30–§31) sha256 `2840220b391bc454f323fc7703501e0c0a2365dbba7439800948833bcfa49508`. İkisi CT tarafından `origin/docs/crm/mod0149-review-01` @ `1ec2d1385`'te yeniden ölçüldü | **şekil teyitli** — §7 D-2 koşulunun "U-1 şekli CRM sahibince teyitli" kolu karşılandı. Sözleşme MOD-0149 DL-1 kabulüyle donar; donan sürümde U-1 şekli değişirse CRM sha'yla önceden yazar (1.x yalnız ekleme, §10) |
| D-4 rol ataması | `crm.account.lookup-validation` → geri çağırma rolleri MOD-0018'in (Ali) işi | CRM CT iletisi madde 2 / 4 | **açık** — Ali |
| D-1 SHIPMENT-BUNDLE 3.5.0 | — | — | **açık** — MVP-6 |

## Uyum notları (paket değişikliği yok)

- Hata eşlemesi CRM tarafından sözleşmeyle uyumlu bulundu: 400 / 5xx / zaman aşımı → `Unresolved` ("Çözülemedi"); 401 / 403 → `Forbidden` ("İzin eksik", kayıt ve dışa aktarım 403); 404 → `NotFound`; hiçbir durumda kimlik gösterilmez (§8.6).
- `accountType` sözleşmede yok; paket `status` ve `isBillable`'ı da kullanmaz.
- Tüketici satırı: CRM, MOD-0177-FU01 main'e gelince sözleşme §5a'daki "aday" satırını "tüketici"ye çevirip §7 D-5 sınırlarını yazacak (1.x ekleme). **Takip: MVP-1 CT, FU01 main'e girince CRM CT'ye haber verir.**
- MOD-0183 / MOD-0186 seçicisi: U-3 (`GET /api/crm/accounts/lookup`, ad ya da koda göre arama, adres yok) MVP-1 CT cevabını (listede ad ve kod, adres seçimden sonra) karşılar; adres eki K-9 açık, acele yok.
