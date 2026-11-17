# Paket standardının akıbeti — bugünkü standart → kısa biçim

Kaynak: `.antigravity/rules/module-pack-standard.md` (685 satır). Kısa biçim: `.ees/standartlar/paket-standardi.md`. Eski standart
eski paketler için yerinde kalır; bu tablo hiçbir şeyin sessizce düşmediğinin kaydıdır. Akıbet: **kalır** (yeni biçimde de var) ·
**koşullu** (yalnız ilgili `K-` bölümü açılınca) · **başka yere** (başka bir kural, şablon ya da araç taşıyor) · **kalkar**
(türetilebilir ya da araçla üretilir).

## 1. Standardın kendi bölümleri (20 ana + 10 alt)
| Bugünkü bölüm | Akıbet | Nereye / neden |
|---|---|---|
| §1 Dosya adı | kalır | kimlik biçimi aynı; `.karar.md` eklendi (KP1) |
| §2 Künye ve alan kuralları | kalır | bütün alanlar aynı adla; dört alan eklendi (KP11) |
| §2 `shell` × düzen tablosu (alt) | başka yere | `frontend-datatable-template.md` + mimari test |
| §2 `entity_base` tablosu (alt) | başka yere | `entity-base-template.md`; alan künyede |
| §3 Altın referans sözleşmesi | koşullu | `ekran` |
| §3 `data_mode` kararı (alt) | koşullu | `ekran` (alan künyede) |
| §4 Arka uç dosya düzeni | başka yere | altın referansın kodu + mimari testler |
| §5 Ön yüz dosya sözleşmesi | başka yere | `frontend-datatable-template.md` + `verify_datatable_page.py` |
| §6 Zorunlu bölümler (22 satır) | kalkar | yedi çekirdek bölüm (aşağıda §2) |
| §7 Kabul ölçütü kuralları | kalır | biçim aynen; ölçüt sonuna dilim ve test eki |
| §7 menü anahtarı kuralı (alt) | koşullu | `ekran`; `check_pack_nav_keys.py` ölçüyor |
| §8 Doğrulama şablonu | kalır | bölüm 4 "Alanlar" |
| §9 Hata yolu şablonu | kalır | her hata yolu bir kabul ölçütü (KP5) |
| §10 Yetki şablonu | kalır | bölüm 4 "İzinler"; anahtar biçimi `permission-key-standard` |
| §10.1 Denetlenen olaylar (AUD-001) | kalır | bölüm 4 "Denetlenen olaylar" |
| §11 Ağ geçidi kararı | koşullu | `rota` |
| §12 Hazırlık kontrol listesi (15 madde) | kalkar | biçim maddelerini `pack_contract_check.py`, içerik maddelerini denetçi ölçer |
| §13 Repo Scope / Protected Paths | kalkar | `AGENTS.md` §4 + çekirdek kart + CODEOWNERS; başka modül yolu bölüm 3 |
| §13 "sık değişen dosya sabitlenmez" (alt) | başka yere | denetçi; GitHub onayına geçince büyük ölçüde kalkar |
| §13.1 Platform seçim listesi kararı | koşullu | `ekran` (yalnız platform modülü) |
| §14 Varlık şeması (`Version` adı) | kalır | bölüm 4 "Alanlar" kuralı |
| §15 Test beklentileri | kalır | standart §7 varsayılan testler; pakette tekrarlanmaz |
| §16 Yaşam döngüsü | kalır | `status` değerleri ve akışı aynen |
| §16 Uygulama durum izleyicisi (alt) | kalkar | araçla üretilir (Ç29) |
| §16 Sürüme bağlı onay CT-APR-01 (alt) | başka yere | onay akışı işi (GitHub onayı); onay yalnız sözleşmeye (KP1) |
| §16 CT-APR-02 teslim dilimleri tablosu (alt) | kalır | bölüm 6, eski araçla uyumlu biçim |
| §16 CT-APR-02 bağımsız inceleme (alt) | başka yere | hat kartı H4, H18–H19 |
| §16 Plan hizalaması, DL-1 çekirdek, büyük artış = FU (alt) | kalır | künye `plan_row`, bölüm 6, KP9 |
| §17 Yetki sırası | başka yere | çekirdek kart Ç2–Ç3 |
| §18 Hızlı şablon | kalkar | `.ees/sablonlar/paket.md` |

Sayım: 30 birim — kalır 12 · koşullu 5 · başka yere 8 · kalkar 5.

## 2. Paket gövdesinin 22 zorunlu bölümü (+ şablondaki Page Help)
| Eski bölüm | Akıbet | Yeni yeri / neden |
|---|---|---|
| Module Summary | kalır | bölüm 1 |
| Ownership and Boundaries | kalır | bölüm 2 |
| Owned Objects | kalır | bölüm 4 nesneler |
| Entity Fields | kalır | bölüm 4 alanlar |
| Repo Scope | kalkar | işçi yolu koddan bulur; başka modül yolu bölüm 3 |
| Protected Paths | kalkar | `AGENTS.md` §4, çekirdek kart, CODEOWNERS |
| Dependencies | kalır | bölüm 3 |
| Runtime Constraints | başka yere | iş kuralı → süreç; teknik değişmez → bölüm 4; kalanı koşullu bölümler |
| Layout & Shell Contract | kalkar | künyedeki `shell`'den türer |
| Backend File Convention | kalkar | altın referans |
| Frontend File Contract | kalkar | altın referans + doğrulayıcı |
| Validation Rules | kalır | bölüm 4 alanlar |
| Failure Path to Verify | kalır | bölüm 5 ölçüt satırları (KP5) |
| Authorization Convention | kalır | bölüm 4 izinler |
| Gateway / API Routing Decision | koşullu | `rota` |
| Acceptance Criteria | kalır | bölüm 5, biçim aynen |
| Test Expectations | kalkar | ölçütteki `test:` eki + standart §7 |
| Ready-for-dev Checklist | kalkar | otomatik kontrol + denetçi |
| Implementation Notes | başka yere | `kararlar/*.karar.md` |
| Follow-up Items | başka yere | bölüm 2 kapsam dışı + backlog |
| Audited Events | kalır | bölüm 4 denetlenen olaylar |
| Teslim dilimleri | kalır | bölüm 6 |
| Page Help (şablonda) | koşullu | `ekran` |

Sayım: 23 — kalır 11 · koşullu 2 · başka yere 3 · kalkar 7.

## 3. Eski SOP mühendislik listesi
SOP§18'in 12 konusu ve bir yeni konu (`zaman`) `.ees/standartlar/kosullu-bolumler.md`'de 13 koşullu bölümdür. SOP§6.1/1–2 (A/B/C profili) ve
§18.0/3 kalkar: yerini künyedeki `conditional:` alır.
