---
record_id: TREASURY-MOD0289-GAP-LIST-13
kind: sahip inceleme bulguları ve kapanışları (TRM-16 — MOD-0289 draft pack) — onay değildir
received_at_local: 2026-10-25
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS — kapsam ve SoR sınırı doğru")
reviewed_input: .ct-teslim/treasury-intake/treasury-TRM16-MOD0289-158e0dc93.zip (sha256 221c3468c3181848222fdfff70000ffdd41cedd28783fc6a82ae658067dfe670)
fixed_in: MOD-0289 pack, karar kaydı D23, MOD-0289 yerleşim notu (superseded bandı) — bu kaydın commit'i
decisions: D23 (O-9 ilişkili şirketler, O-1 dört göz, O-2 aktif / etkinleştirmede +onaylı, O-3 ayrı izin + gerekçe)
previous: GAP-LIST-11-TRM15.md
---

# MOD-0289 — gap listesi 13 ve kapanışları

| # | Öncelik | Gap | Kapanış (pack "Gap 13 kapanışı" tablosu) |
|---|---|---|---|
| P1-a | P1 | Domestic hesap tekilliği yanlış çakışabilir | Hesap türüne göre tekillik anahtarı ve ayrı normalizasyon: IBAN → kanonik IBAN; Domestic → ülke + banka kodu (+ bankada `DomesticKeyIncludesBranch` true ise şube kodu) + hesap numarası (baştaki sıfırlar korunur). Domestic hesap ulusal banka kodu ister; anahtarı besleyen alanlar hesap varken kilitli (409). Bilinen sınır: aynı hesabın bir kez IBAN, bir kez domestic girilmesi v1'de yakalanmaz (ülke format kapısıyla kapanır). AC-12 güncellendi; AC-33, AC-34. |
| P1-b | P1 | HMAC anahtar döndürmede mükerrer kontrolü belirsiz | Kayıtta `UniquenessKeyDigests` (`"{sürüm}:{hex}"`) ve `HashKeyVersions`; her yazma her aktif sürüm için özet yazar; özet dizisinde tekil indeks; beş adımlı yeniden özetleme, devam edebilen döndürme çalışanı (varsayılan kapalı), aktif sürümü olmayan kayıt varken açılış reddi. AC-35 (farklı anahtar sürümleriyle aynı hesap = mükerrer, eşzamanlı test, sabotajlı), AC-36. |
| P1-c | P1 | Tedarikçi hesabı kapsamı onaylanmamış istisnaya dayanıyor | D23/1 kural oldu: ilişkili şirket kullanıcısı liste / detay / lookup görür, maskesiz okuma ayrıca izin + gerekçe; ilişkisiz (OnHold, Blocked, bilinmeyen tedarikçi, şirket başlığı yok dahil) liste yok, detay / lookup / maskesiz / yazma 404; `read-tenant-wide` maskeli görür, ilişkisiz maskesiz 403 `MDM_BANK_ACCOUNT_RELATIONSHIP_REQUIRED`; MOD-0140 erişilemez → 503. AC-41 (DL-3), AC-24/25 güncellendi. Yan etki: kiracı genelinde tedarikçi hesabı yazan rol yok. |
| P2 | P2 | IBAN kontrolünün sınırı | "Biçim + mod-97 geçerli" ile "ülke formatı geçerli" ayrıldı; v1 yalnız ilkini yapar, kayıtta `IdentifierCheckLevel = FormatAndChecksum` (DTO, liste rozeti, lookup). `Mdm:BankData:IbanCountryFormatGate` + `IIbanCountryFormatSource` portu DL-2'de; kapı açıkken geçersiz 400, kaynak yok 503; MOD-0269 gerçek gönderimi öncesi zorunlu kapı. AC-40. |
| iyileştirme 1 | — | Lookup: tüketici yalnız `referenceable`'a bakmamalı | `referenceable` yalnız hesap kaydının aktifliği; tüketici E1–E9 uygunluk alanlarını (kimlik, yaşam döngüsü, referenceable, banka durumu, sahip türü / kimliği, sahip durumu, para birimi, kontrol düzeyi, sözleşme sürümü) kontrol eder; `identifierType`, `identifierCheckLevel` eklendi; her alan için negatif fikstür; AC-42 (üretici testleri + fikstürler). Tüketici pack'lerinin fikstür koşuları takip maddesi. |
| iyileştirme 2 | — | Şifreleme (O-6) somut değil | AES-256-GCM, her şifrelemede CSPRNG 96-bit nonce (tekrar yok), ilişkili veri = TenantId + hesap kimliği + anahtar sürümü; bozuk / yanlış anahtar / başka kayda taşınmış zarf → 500 `MDM_BANK_PROTECTION_INTEGRITY_FAILED`, değer dönmez; anahtar yok / geçersiz → servis açılmaz, çalışırken erişilemez → 503; karışık sürümler okunur, yeniden şifreleme işi taze nonce ile taşır. AC-37, AC-38, AC-39. **O-6 hâlâ öneri** (sahip kararı yok). |
| okuma | — | Yerleşim notu hâlâ "PROPOSAL" | Notun başına "D20/1 ile superseded" bandı eklendi; metin tarihsel olarak değişmedi. |

D23 ile O-1, O-2, O-3, O-9 kararlaştı; diğer açık kararlar (O-4…O-8, O-10…O-12) öneri olarak kalır.

Kontroller (yazar ajan, CT tekrar saydı): 24 bölüm, MOD-0126 ile aynı sıra; AC-1…AC-42 tekil; her AC bir dilimde kapanıyor.
Ölçülmeyenler pack'te engel olarak yazılı: dizi alanında kısmi tekil indeksin gerçek replika setinde davranışı, `AesGcm`'in
hedef çatıda varlığı ve üretim gizli bilgi deposu, denetim niyeti yolunda sistem aktörü, MOD-0140 1.3.0'da "çağıran şirketle
aktif ilişkili tedarikçiler" listeleme işlemi (1.2.0'da yalnız durum süzgeci, `supplier.openapi.yaml:26-31`). Belge
incelemesidir; build / test / runtime yok.
