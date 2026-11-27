---
decision_id: MOD0186-DOCS-PATH-SHIPMENT-BUNDLE-360-REPIN-01
status: draft
drafted_at_local: 2026-11-27
drafted_by: MVP-6 CT (Beste adına)
decided_by: TBD — Beste (bestepullukcu-alt; MVP-6 / MOD-0186 sahibi)
decision_source: TBD — WP-0186-DL6 §2 madde 2 sorusuna sahibin cevabı
payload_sha256: d1b4811b976450692b91c294079ddc5489ca777e5b45fb31dacd1b850e944996 (taslak ölçümü; §3 koşulu)
supersedes_binding: docs/records/decisions/2026-11/mod0183-docs-path-shipment-bundle-351-repin-owner-decision-01.json (o kayıt değişmez; authority bu kaydı bağlar)
---

# Docs-path authority — shipment-bundle 3.6.0 yeniden sabitleme (sahip kararı TASLAĞI)

**Taslaktır; onay değildir, uygulanmaz.** Sahip "evet" derse CT bu dosyadan iki kesin kayıt üretir (aşağıda §2) ve bu taslak dosyası o
commit'te silinir ya da adı değiştirilir (taslak `main`'e çıkmaz). Desen: `mod0183-docs-path-shipment-bundle-351-repin-owner-decision-01.{json,md}`.

## 1. Onaylanacak olan

1. Kanonik hedef `docs/analysis/contracts/shipment-bundle.openapi.yaml`:
   `8f7564b30af0fdc05bd3b8cae1b66306c6bf1ec682955801c32766a86083e241` (3.5.1) →
   `6024d3f0738a4d55830ced9e97e7b4db41e4c6576ece3da6163e38ade615d7bb` — sözleşme **3.6.0** (MOD-0186 r12 §36.2, O-R11-1 seçenek C;
   `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.6.0.patch` ile MOD-0186 DL-6'da uygulanır).
2. Başka hiçbir şey: taşıyıcı hedefi (`carrier-semantics-v1.1.0.md`, `87557ef9…1fee`) ve tüm mühürlü girdiler bayt-eşit kalır.
   Yeni ek belge `returns-semantics-v3.1.0.md` kanonik hedef **değildir** (authority yalnız YAML ve taşıyıcı ek belgesini hedefler;
   `docs/analysis/contracts/` kural istisnasıdır, DocsPathGuardTests dosya başı açıklaması "SIMPLIFIED 2026-11-02").

## 2. Yazılacak kesin kayıtlar (onaydan sonra, CT'nin ayrı commit'i, DL-6 commit'inin hemen ardından)

- `docs/records/decisions/2026-11/mod0186-docs-path-shipment-bundle-360-repin-owner-decision-01.json`:

  ```json
  {
    "kind": "DOCS_PATH_OWNER_DECISION",
    "status": "APPROVED",
    "decisionId": "MOD0186-DOCS-PATH-SHIPMENT-BUNDLE-360-REPIN-01",
    "approvedBy": "<onay günü: current-role-user-message-2026-11-DD>",
    "payloadSha256": "<§3'te yeniden ölçülen değer; taslak ölçümü d1b4811b976450692b91c294079ddc5489ca777e5b45fb31dacd1b850e944996>"
  }
  ```

  (Biçim 3.5.1 JSON'u ile aynı: 2 boşluk girinti, LF, son satır sonu 3.5.1 dosyasıyla aynı.)
- `…-01.md`: bu taslağın kesin hali (`status: approved`, `decided_by`, `decision_source`, JSON'un sha256'sı).
- `docs/reference/architecture/docs-path-authority.json`: `decision.path` → yeni JSON, `decision.sha256` → yeni JSON'un LF sha256'sı;
  `canonicalTargets[0].sha256` → `6024d3f0…d7bb`. Başka satır değişmez.

## 3. Ölçüm (CT, 2026-11-27) ve koşul

- Yöntem `DocsPathGuardTests.cs:266-268` ile aynı: `canonicalTargets` ham metni + `"\n"` + `sealedInputs` ham metni, UTF-8, SHA-256.
  Yöntem doğrulandı: bugünkü 3.5.1 authority'si (LF sha256 `363e4969448d34f7ade046b97ecb9a524293fdef296652a9004223eecd50dd50`, commit
  `9b89fd8c3`) için aynı hesap `bb025a8a…35e3` verir — 3.5.1 kaydındaki değer.
- Yalnız YAML hedefi `6024d3f0…` yapılınca yük `d1b4811b976450692b91c294079ddc5489ca777e5b45fb31dacd1b850e944996` olur.
- **Koşul:** taban authority, 3.5.1 sabitlemesini taşıyan dosyadır (`9b89fd8c3`). `origin/main` bugün 3.5.0 sabitlemesini taşır (LF
  `01b76936…0f68`); DL-8 ve `9b89fd8c3` `main`'e girmeden (WP-0186-DL6 K-1) bu yük geçerli değildir. Arada herhangi bir mühür ya da hedef
  değişirse yük yeniden ölçülür; bu taslağın onayı yeni özete taşınmaz.
- LF checkout'ta ölç (DocsPathGuard ham baytı özetler; CRLF kopya farklı okur).

## 4. Sınırlar

Politika, şema, okuyucu ya da test değişikliği yok; başka hedef ya da mühür yok. 3.7.0 (MOD-0183 depo alımı) yeniden sabitlemesi bu
kaydın konusu değildir; 3.6.0 özetinden başlar (sürüm kararı "Sıra ve DocsPathGuard" adım 2).
