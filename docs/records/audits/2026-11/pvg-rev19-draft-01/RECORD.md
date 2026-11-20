# Revizyon 19 — bağımsız taslak incelemesi ve CT kararı

Tarih: 2026-11-20. Taban HEAD: `9fa2ed6755ade67c3a09d70681560e75139f9e4c`; dal `chore/ct/pvg-ct-01`.
Paket SHA-256: `bfe12700a450d0605b19bd3e817bc21411c5fe6a199140b5a51303d09aa6a6b2`.
Domain SHA-256: `73fac2e9fe4cebcb3ec1e99caf0d7268637d6ef6c38bb5d0cdc9fe25aa4b15de`.
İncelenen baytlar reviewed-pack.txt ve reviewed-domain-config.txt içinde hash doğrulanarak saklandı; alternatif otorite değildir.

## Ayrı sohbet sonucu

`docs/records/reviews/MOD-0230/2026-11-20-rev19-separate-chat-01.md`: KOŞULLU, sıfır açık ENGEL. F-6–F-9 taslakta kapandı. Yeni F-10 ORTA ve F-11 DÜŞÜK açık. İnceleyen aynı ayrı sohbet `01a1f849-c6bd-7ba3-b788-6ebf7a7551e9`, gpt-6.2-astra/high. Bu ayrı sohbetin üç turunda hedefler revizyon 17, 18 ve 19 olarak değişti; negatif hükümler kendi baytlarında korunur.

CT F-6–F-9 düzeltmelerini kaynaklarla karşılaştırdı. Ek salt okunur readiness taraması, bilinen RegPvBase Update satırı dışında kesin çelişen test bulmadı. Triage/Route no-field beklentileri, yetkisiz/şekli geçersiz isteklerde permission-only beklentileri kapsam dışında korunur. Null evidence elemanının DTO/validator/builder/Mongo mapper zincirinde açık reddi görülmedi; gerçek HTTP kabulü henüz ölçülmedi.

## Son iki notun CT karşılaştırması

- F-10 kabul. Mevcut `Update_denied_by_evidence_link_preserves_existing_draft_without_sensitive_echo`, sürümsüz istekte evidence reddini bekliyor. `Mutation_without_expected_version_is_version_conflict_and_leaves_store_unchanged`, izin veren portlarla sürüm çakışması bekliyor. İki beklenti, uygulanabilir reporter/evidence kontrollerinin ardından sürüm eşitliği denetlenerek birlikte karşılanabilir. Her iki ret ReplaceAsync çağırmaz. Bu öncelik sözleşmede açık yazılmalı; aynı-snapshot atomik yazma korunur.
- F-11 kabul. Üretim JS antiforgery başlığı ve form üyesini birlikte taşıyor. Başarılı tek update iki taşıyıcıyı ayrı kanıtlamaz. İsimli test, jsdom çıkışında ve MVC'ye iletimde iki değeri ayrı doğrulamalı; tek taşıyıcıyı düşüren sabotaj ilgili assertion'ı kırmalı. Değer loglanmaz.

## Karar

Kartın S-99 orta/düşük bulgular için tek yazar geçişi yaklaşımıyla yalnız bu iki açıklığın revizyon 20 taslağına işlenmesi delege edildi. Yeni ön taslak turu açılmadı. Revizyon 19'un KOŞULLU sonucu revizyon 20 baytlarının resmî incelemesi veya UYGUN sonucu olarak sunulmaz. Yeni kesin sürümün resmî inceleme/onay kapıları açıktır.

VERIFICATION.json gerçek parser/byte/diff ölçümleridir: 45 zorunlu kriter; tek ret draft statüsü; AC1–39/43/44 korunuyor, ürün/test/CI farkı yok. build, ürün testi, Node/npm, Mongo, sabotaj, CI ve canlı ekran ölçülmedi. DL-12 BLOCKED kalır.
