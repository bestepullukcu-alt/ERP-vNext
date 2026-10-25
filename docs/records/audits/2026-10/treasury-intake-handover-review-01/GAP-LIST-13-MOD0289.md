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

## Gap listesi 14 (2026-10-25, sahip — CONDITIONAL PASS)

İnceleme girdisi `treasury-TRM16-MOD0289-c37f2188d.zip` (sha256 `55b80b0cc92dc06d59cb92c19d569c8319d55a541b76cf5475b510001d9db161`;
manifest 6/6 geçti). Gap 13 büyük ölçüde karşılandı; dört madde kapandı (pack "Gap 14 kapanışı" tablosu):

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| 1 | P1 | Anahtar döndürmede eski yapılandırmalı örnek yalnız eski özeti yazabilir | Kiracı başına `BankDataKeyFence` (`mdm_bank_data_key_fences`, tek `KeySetId`); her hesap yazması aynı işlemde fence'e koşullu artış yapar, eski anahtar setiyle 503 `MDM_BANK_KEYSET_STALE`; fence ilerletme uçuştaki eski yazmayla çakışır; sıralı ilerletme K1→K4; "eksik özet sayısı 0" yalnız fence yeni setteyken nihai; şifreleme anahtarı emekliliği de aynı fence. `AdvanceBankDataKeyFenceCommand`, `WrittenWithKeySetId`. AC-43 (eşzamanlı eski/yeni yazar), AC-44 (şifreleme emekliliği), sabotajlı. |
| 2 | P1 | Banka/şube anahtar alanı kilidi yarışa açık | `DomesticKeyFenceVersion` (Bank, BankBranch): domestic hesap oluşturma ve banka/şube anahtar alanı güncellemesi aynı alana CAS yapar, aynı işlemde; biri 409 (`MDM_BANK_DOMESTIC_KEY_CHANGED` / `MDM_BANK_DOMESTIC_KEY_LOCKED`). AC-45 (100 tekrar, sabotajlı). |
| 3 | P2 | Döndürme işinin atlama koşulu | Atlama yalnız şifreleme güncel ∧ her aktif sürüm için özet var ∧ emekli özet yok; aksi hâlde yalnız eksik adımlar, tek CAS yazma, adımlar idempotent. AC-46. |
| 4 | P2 | Gerçek gönderim kapısında Domestic ayrılmalı | `CountryFormat` şartı yalnız IBAN; Domestic için `DomesticFormat` (v1) / `DomesticVerified` (`IBankDirectorySource` + `IDomesticAccountRuleSource`, `Mdm:BankData:DomesticVerificationGate`); **iki kapı da** MOD-0269 gerçek gönderiminden önce zorunlu. AC-47. |

O-6 hâlâ öneri: sahip / güvenlik onayı ve gerçek Mongo testleri olmadan kapanmaz; AC-13, AC-35…39, AC-43…46 yalnız gerçek
replika setinde geçer sayılır. Ölçülmeyenler pack'te engel: fence kaydının yük altındaki çakışma oranı, `MasterDataAtomicWrite`
yeniden deneme politikasıyla etkileşim, banka dizini ve domestic hesap kuralı kaynağı / sahibi (TBD). Pack: 24 bölüm, AC-1…47
tekil. Belge incelemesidir; build / test / runtime yok.

## Gap listesi 15 (2026-10-25, sahip — CONDITIONAL PASS)

İnceleme girdisi `treasury-TRM16-MOD0289-373513765.zip` (sha256 `948e223f427de7b59c597764b4234de3699487036224262480589c731fe5eefe`;
manifest 6/6 geçti). Gap 14 kapandı; iki writer fence maddesi kapandı (pack "Gap 15 kapanışı" tablosu):

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| 1 | P1 | `KeySetId` güncel anahtar seçimini içermiyor | Kanonik serileştirme (`mdm-bank-keyset/2`, `enc.current=`, `hash.current=`, sıralı anahtar satırları `<kind>\|<version>\|<state>\|<kcv>`), kimlik `ks2-` + SHA-256; sürüm adı deseni ve current'ın `Active` olması açılış koşulu. AC-48 (aynı liste + farklı current → 503 `MDM_BANK_KEYSET_STALE`, sabit değer testi, sabotajlı). |
| 2 | P1 | Yeni kiracının fence'ini ilk writer belirliyor | Servis düzeyi `BankDataKeySetTarget` (`mdm_bank_data_key_set_targets`, `ApprovedKeySetId`, `Version`, `BootstrapSeq`, `ApprovalRef`, ekleme-yalnız `History`), yalnız `ApproveBankDataKeySetTargetCommand` yazar; fence yoksa yazma işlemi hedefte koşullu artış + fence'i onaylı hedefle oluşturma + kayıt; hedefle eşleşmeyen writer 503, hedef yok 503 `MDM_BANK_KEYSET_TARGET_UNAVAILABLE`. AC-49 (yeni kiracı + eski/yeni writer yarışı, 50 tekrar, sabotajlı). |
| ek | — | Fence yük testi ve gerçek Mongo testleri | AC-50 adlı yük testi (≥ 8 paralel writer, 10 dk, koşu sırasında bir fence ilerletmesi; yeniden deneme oranı, p95/p99); eşik DL-2 başında sahip + güvenlik yazar, o zamana kadar geçemez. Ready-for-dev: AC-50 ve gerçek replika seti AC'leri (AC-13, 35…39, 43…46, 48, 49) geçmeden geliştirme kapısı kapanmaz. |

**Yeni engel (ölçüldü):** `ApproveBankDataKeySetTargetCommand` için kabul edilmiş denetim yolu yok — MDM denetim niyeti yolu
kiracısız kayıtta merkezi anahtarı üretemez (`AuditIntentContract.cs:12@373513765`); hedef kaydın kendi geçmişi ve her
kiracının fence denetim niyeti hedef `Version` / `ApprovalRef` taşır; merkezi denetime yazılması CT kararı ister. O-6 hâlâ
öneri. Ölçülmeyenler: `ApprovalRef`'in arkasındaki insan onayı, yük testi eşikleri, çok sayıda yeni kiracıda hedef kaydı
çakışması. Pack: 24 bölüm, AC-1…50 tekil. Belge incelemesidir; build / test / runtime yok.

## Gap listesi 16 (2026-10-25, sahip — CONDITIONAL PASS)

İnceleme girdisi `treasury-TRM16-MOD0289-9518927bd.zip` (sha256 `3cb6e19a0f715838ee316a1791da689db47e4ff5e566796a9b4195e79b2309f0`;
manifest 6/6 geçti). Gap 15 kapandı; iki madde kapandı (pack "Gap 15b / Gap 16 kapanışı" tablosu):

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| 1 | P1 | Eski worker hedefi geriye çevirebilir | Onaylı yapılandırmada kesin artan `ApprovalRevision` + `ExpectedPreviousKeySetId`; `ApproveBankDataKeySetTargetCommand` tek atomik koşullu güncelleme (worker kendi seti = hedef ∧ revizyon > kayıtlı ∧ kayıtlı set = beklenen önceki ∧ hedef set kayıtlarda kullanılan tüm anahtar sürümlerini içeriyor); retler 409 `MDM_BANK_KEYSET_TARGET_REVISION_STALE` / `…_PREVIOUS_MISMATCH` / `…_INCOMPLETE`, hedef değişmez; geri dönüş yalnız yeni, açık revizyonla. Aynı açık fence ilerletmesinde de kapatıldı (`AppliedTargetRevision`, yalnız daha yüksek revizyon). AC-51, AC-52 (sabotajlı). |
| 2 | P2 | Hazırlık kapısında döngü | İki kapı ayrıldı: tasarım / onay kapısı = `ready-for-dev` (kararlar, onaylar, sözleşmeler, yazılmış eşikler, yalnız okuyarak ölçümler); dilim kabul kapısı (Teslim dilimleri'nde dilim başına tablo: AC geçişleri, gerçek replika seti AC'leri, DL-2'de AC-50, build, tüketici kanıtı) — dilim kabulü / merge öncesi, ready-for-dev öncesi değil. İsteğe bağlı ön kontrol (öneri): S-1 dizi üzerinde kısmi tekil indeks, S-2 `AesGcm` — atılacak, üretim yolu yok, merge yok, sahip onayıyla. |

**CT notu:** aynı döngü dört Treasury pack'inde de vardı (ready-for-dev listesinde "MOD-0023 AC-W… geçti"; §17 kodu DL-2/DL-3
ile yazılıyor). Düzeltildi: ready-for-dev yalnız §17 onayını ister; AC-W1/W2/W5…W12 dilim kabul kapısında; dilim önkoşulları
aynı şekilde (MOD-0126, 0119, 0120, 0124).

Açık engeller görünür: O-6 (sahip + güvenlik), kiracısız hedef kaydının merkezi denetim yolu (CT kararı). Pack: 24 bölüm,
AC-1…52 tekil. Belge incelemesidir; build / test / runtime yok.
