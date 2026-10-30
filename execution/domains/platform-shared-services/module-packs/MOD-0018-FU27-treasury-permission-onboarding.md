---
id: MOD-0018-FU27
name: Treasury Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
owner: auth-owner (MOD-0018 sahibi inceler ve teyit eder) / yazar Treasury Intake CT
branch: feature/pss/mod-0018-fu27-treasury-permission-onboarding (henüz açılmadı; FU numarası KARAR D80/1 ile verildi)
started: 2026-10-28
target: TBD
form_field_count: 0
parent: MOD-0018
consumer_module: MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0289, MOD-0217
---

# MOD-0018-FU27 — Treasury Permission Onboarding

> **Taslak — kod başlatma izni değildir.** Bu paket izin kataloğu, rol, hak (grant), rol üyeliği, hak eşitlemesi
> (entitlement), token, manifest, gezinme, yapılandırma, gizli bilgi ya da kiracı verisi oluşturmaz. Kod yalnız paket
> `approved` / `ready-for-dev` olduktan ve CT-APR-01 onay satırı main'e girdikten sonra, Bölüm 5'teki tam yol listesiyle ve
> dilim sırasıyla yazılır. Platform ve MDM yollarına dokunmak ayrıca XMC-001 onayı ister (Bölüm 5, D63/2).
>
> **KARAR D103 (kaynak Y-28, 2026-10-30):** Bölüm 14.2 "Görünen adlar" tablosu MOD-0018 sahibinin düzeltmeleriyle güncellendi (rol 26 ve 27
> `tr`; "Manager" fr, es, zh, ru tek terim). ar, ru, zh anadil kontrolsüz. Yedi `SharedResource` dosyası DL koduyla yazılır; bu revizyon
> kod değildir. D80/2 (a) ve (b) değişmedi: FU27 son hâli teyidi bu teyide dahil değildir.
>
> **Yetki dayanağı.** MOD-0018 Auth eki Treasury tarafından yazılır; MOD-0018 sahibi inceler ve teyit eder (Y-15, Y-16;
> `docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md`). Y-14 geçersizdir ve bu pakete
> işlenmedi. Modül tanımı biçimi değişmedi; bu paket mevcut MOD-0018 onboarding kalıbını (FU20 / FU22) izler (Y-16/2).
> **D53** geçerlidir, D53/1 **KARAR D70/3** ile sıkılaştı: bu paketin metni main'de **ve** MOD-0018 sahibinin teyidi (teyit karar
> dosyasına kaynak olarak yazılır) = altı tüketici paketin paket kapısı (D53/1 + D70/3; metnin main'de olması tek başına yetmez);
> bu paketin kodu main'de = tüketicilerin DL-1 kabul kapısı (D53/2). **D56** aynen geçerlidir. **KARAR D80/2 (kaynak Y-19) — şartlı
> teyit:** MOD-0018 sahibi D69–D78 sürümünü (main `4c08fc75a`) şartla teyit etti; bu kapı tüketicilerin **DL-1'ini bekletmez**; bu paketin **DL-5 kodundan
> önce** iki şart kapanmalı: (a) K-W5 denetim izi için defter kararı, (b) rol atama adımlarının (rola izin bağlama, kişiye rol
> verme) denetim borcu (Teslim dilimleri DL-5 önkoşulu, Ready-for-dev Checklist). D79'dan sonraki değişiklikler (7 dil tablosu vb.)
> sahip teyidine ayrıca gider.
>
> **Kaynak (tek doğruluk kaynağı, D62/1).** `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` — main'de
> (`origin/main` @ `f81d34df7`, 2026-10-28 ölçümü): "D47 rol matrisi (CT04a; D49 ile güncellendi — işleyen tek matris)"
> bölümü, D54, D56, D57, D62, D63. **D64** (D63/3'ün yerine; waive-sod Auth veri yapısı bu pakette, DL-5; tetik Y-17) **main'dedir**:
> PR #276 (`3bb1ab838`; ölçüm 2026-10-28 `git merge-base --is-ancestor 4d8a4050a 3bb1ab838` → 0); D62/1 gereği DL-5'in paket
> kapısı olan "D64 main'de" maddesi kapandı (Ready-for-dev Checklist). D66, D67 ve D68 PR #281 ile main'dedir (`c32cabe6b`);
> D69 ve D70 main'de değildir (D69 yerel commit `db69732cb`; D70 yerel dal `chore/ct/treasury-intake-ct-05`, main'de değil); D71 de main'de değildir
> (yerel dal `chore/ct/treasury-intake-ct-05`, main'de değil; 2026-10-28). D46 tablosu tarihçedir
> ("YERİNE GEÇTİ — UYGULAMAYIN") ve kullanılmadı. Anahtar adları altı paketin Authorization Convention bölümleriyle
> mekanik olarak karşılaştırıldı (Bölüm 19). D72, D73, D74 ve D75 yerel commit'tir (`2047e94c6`, `776c6fc54`, `5d2655c72`,
> `da2aba808`; dal `chore/ct/treasury-intake-ct-05`), main'de değildir; D76 karar dosyasında yazılı, commit'lenmemiştir (ölçüm
> 2026-10-28, `git log origin/main..HEAD`; `git merge-base --is-ancestor <commit> origin/main` → 1).
>
> **Revizyon (2026-10-29, KARAR D80 / FU27 — kimlik ve şartlı Auth teyidi):** Kaynak: karar dosyası D80 ve kaynak yanıtı Y-19
> (MOD-0018 sahibi Ali, 2026-10-29); D80 yerel commit `728409ca3` (dal `chore/ct/treasury-intake-ct-05`), **main'de değil** (ölçüm
> 2026-10-29, `git merge-base --is-ancestor 728409ca3 origin/main` → 1) — D62/1 gereği bu revizyonun paket kapısı D80'in main'e
> girmesidir (Bölüm 7, Ready-for-dev Checklist). (1) **D80/1 — K-1 KAPANDI (MOD-0018 sahibi kararı, Y-19):** kimlik
> **MOD-0018-FU27**; dosya adı, `id`, başlık ve `branch` FU27'ye çevrildi (eski geçici ad FU-TBD); kayıt satırı
> `execution/registries/module-id-registry.md`'ye paketle birlikte yazıldı; DCP-002 ön kontrolü yeniden koşuldu (DCP-002 kimlik
> paragrafı). (2) **D80/2 — Auth paket kapısı (D70/3) şartlı teyit:** MOD-0018 sahibi D69–D78 sürümünü (main `4c08fc75a`) şartla
> teyit etti; DL-1 bekletilmez; **DL-5 kodundan önce** (a) K-W5 denetim izi için defter kararı ve (b) rol atama adımlarının (rola
> izin bağlama, kişiye rol verme) denetim borcu kapanmalı — Teslim dilimleri DL-5 önkoşulu ve Ready-for-dev Checklist'te iki
> madde. D79'dan sonraki değişiklikler — 27 rolün 7 dilli görünen ad tablosu (Bölüm 14.2 "Görünen adlar") vb. — sahip teyidine
> **ayrıca** gider; o teyit henüz yok. (3) **D80/3:** sahip D79/9'u (K-7: sabit kod + 7 dil kaynak) ve D67/1'i (aday sorgusu
> DL-6 ayrı dilim) teyit etti. (4) **D80/4 — açık (sahipte):** K-W5, K-A1, K-A2, K-A3; D79 önerilerinin kabul listesi (K-1b, K-2,
> K-3, K-W2, K-W4, K-W6) ve K-5; K-W7 ve K-A4b teyidi. Değişen yerler: frontmatter (`id`, `branch`), başlık, D53 paragrafı, D70
> revizyon notu (D70/3 işareti), DCP-002 kimlik paragrafı, Bölüm 7 (D79 satırı ölçümü, `auth-gunlugu` satırına D80/2 (a), yeni D80 ve `auth-rbac-gunlugu` satırları), 18, 21, Teslim
> dilimleri (DL-5 satırı, DL-5 önkoşulu, tüketici paket kapısı cümlesi), Açık kararlar (K-1, K-7 ve D80/4 listesindeki satırlar).
> Mevcut AC numaraları ve biçimi değişmedi; yeni AC yok; teslim dilimleri DL-1…DL-6 değişmedi.
>
> **Revizyon (2026-10-29, KARAR D79 — Auth eki küçük kararları):** Kaynak: karar dosyası D79 (bu revizyon anında karar
> dosyasında yazılı, **commit'lenmemiş; main'de değil** — D62/1 gereği bu revizyonun paket kapısı D79'un main'e girmesidir,
> Bölüm 7, Ready-for-dev Checklist). Dokuz açık karar kapandı; hepsi **MOD-0018 sahibinin teyidine Auth ekiyle gider** (D79
> giriş paragrafı, Y-15): **K-1b** paket başına bir modül kodu (D79/1); **K-2** modül hakkıyla Admin / Viewer'a hiçbir Treasury
> anahtarı (D79/2); **K-3** varsayılan kiracı SuperAdmin tam katalog yolu bugünkü davranış (D79/3); **K-W2** `waive-sod` izni
> açık atamayla süresiz, her kullanım ayrıca geçerli atama ister, token üretimine dokunulmaz (D79/4); **K-W4** kiracı uçlarının
> kök yolu `/api/roles/sod-waiver-assignments`, Gateway değişmez (D79/5); **K-W6** sorgu yanıtı `docs/analysis/contracts/`
> altında sürümlü sözleşme dosyası, MOD-0126 sahte istemcisi ona karşı (D79/6); **K-A2 (b)** Auth yalnız kiracı tabanını verir,
> şirket kapsamını tüketici süzer (D79/7); **K-A3** hizmet hesabı aday değil, geçici kilitli kullanıcı aday kalır (D79/8);
> **K-7** `RoleName` İngilizce PascalCase sabit kod, görünen ad rol ekranlarında kod → kaynak eşlemesiyle **7 dilde** — 27 rolün
> 7 dilli adlarını Treasury yazar (Bölüm 14.2 yeni tablo), MOD-0018 sahibi teyit eder (D79/9; Çekirdek kural 4). D79/9 Bölüm 11'in
> "Platform modülü — 2 dil" ifadesini kaldırır: rol ekranı kiracıya görünür. Eşlemenin yeri mevcut desendir (`SharedResource`
> `RoleName_{RoleName}` anahtarı + `RoleNames` köprüsü; ölçüm Bölüm 5.5, kural Bölüm 11) — bu yüzden yeni Bölüm 5.5 ön yüz dosya listesi eklendi
> ve Bölüm 6 / 9 bu listeye göre daraldı. Açık kalan (MOD-0018 sahibinde, D79): Auth ekinin teyidi, K-1 FU numarası, K-W5 K2
> denetim izi, K-A1 uç adı / sözleşme dosyası, K-5 (Beste ile) [D80 revizyonu: K-1 kapandı — FU27 (D80/1); Auth eki şartla
> teyit edildi (D80/2); açık liste D80/4]. Değişen yerler: Bölüm 2, 3, 4, 5.2, 5.3, 5.4, yeni 5.5, 6, 7,
> 8.1, 8.2, 8.5, 8.6, 8.7, 9, 11, 12, 13, 14.2 (yeni 7 dilli ad tablosu), 14.3, 15, 16 (**AC-5, AC-7, AC-23, AC-34, AC-38, AC-40,
> AC-43 genişletildi / karara çekildi**; AC-34 "isteğe bağlı" → "zorunlu", D79/6), 17, 18, 19, Açık kararlar. Yeni AC yok;
> mevcut AC numaraları değişmedi; teslim dilimleri DL-1…DL-6 değişmedi.
>
> **Revizyon (2026-10-28, KARAR D76 — D75 uygulamasının teyitleri):** Kaynak: karar dosyası D76 (2026-10-28 ölçümünde karar
> dosyasında yazılı, **commit'lenmemiş; main'de değil** — D62/1 gereği bu revizyonun paket kapısı D76'nın main'e girmesidir,
> Bölüm 7, Ready-for-dev Checklist). (1) **D76/1:** rol üyeliği denetimi muafiyet **atama ve geri alma** uçlarındadır (izin +
> üyelik); kiracı okuma ucu (`GET /api/roles/sod-waiver-assignments?paymentBatchId=`) yalnız `auth.payment-sod-waivers.read`
> izni ister, üyelik denetimi yoktur. Geri alma ucundaki denetim artık CT çıkarımı değil karardır. (2) **D76/2 — denetim sırası:**
> izin ve rol üyeliği → kendine atama → alan doğrulama; rolsüz kişi kendini alıcı yazarsa önce 403 `SOD_WAIVER_ASSIGN_FORBIDDEN`
> döner (`SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN` değil). Ölçüm: AuthService'te `ValidationBehavior` MediatR boru hattında
> işleyiciden önce koşar (`Application/DependencyInjection.cs:21`) — üyelik ve kendine atama denetimi yalnız işleyicide kalırsa
> alan doğrulaması önce koşar ve D76/2 sırası bozulur (Bölüm 5.3, 8.5). (3) Ölçümle eskiyen ifade düzeltildi: D75
> "commit'lenmemiş" → yerel commit `da2aba808`, main'de değil (Kaynak paragrafı, Bölüm 7). Değişen yerler: Bölüm 5.3, 7, 8.5,
> 12, 13, 14.3, 16 (**AC-29 genişletildi**), 17, 18, Teslim dilimleri DL-5 satırı, Açık kararlar K-W7. Yeni hata kodu yok;
> mevcut AC numaraları değişmedi; yeni AC yok. MOD-0018 sahibinin teyidi Auth ekiyle.
>
> **Revizyon (2026-10-28, KARAR D75 — D73 / D74 uygulamasının açık noktaları):** Kaynak: karar dosyası D75 (2026-10-28
> ölçümünde karar dosyasında yazılı, **commit'lenmemiş; main'de değil** — D62/1 gereği bu revizyonun paket kapısı D75'in main'e
> girmesidir, Bölüm 7, Ready-for-dev Checklist). (1) **D75/1 — K-W7 KAPANDI:** 27. rol Muafiyet yetkilisini Treasury hak
> profili **izinsiz** ayrılmış sistem rolü olarak oluşturur (K-W7 seçeneği (a); ad çakışma ön denetimine girer); iki Auth anahtarı
> role yalnız açık (`Manual`) rol-izin atamasıyla bağlanır. Atayan denetimi **izin + rol üyeliği**dir (CT önerisi "yalnız izin"
> seçilmedi): çağıran hem `auth.payment-sod-waivers.assign` iznini hem Muafiyet yetkilisi rol üyeliğini taşımalıdır; izin başka
> role elle bağlansa da rolsüz kişi atayamaz (403 `SOD_WAIVER_ASSIGN_FORBIDDEN`, mevcut kod). Geri alma ucu yaratma ucuyla aynı
> izni kullandığından (Bölüm 14.3 "aynı") aynı denetimi kullanır — CT çıkarımıydı (D75/1 yalnız atayanı adlandırır); **KARAR
> D76/1** ile karar oldu. (2) **D75/2 — ortak sırrın iki sınırı kabul edildi** (Bölüm 8.7 "Sınır" ölçümü artık karar): (a) sırrı tutan her
> servis iç uçları çağırabilir, çağıran ayırt edilmez; (b) kullanıcı token'ı olmayan arka plan işleri iç uçları çağıramaz — aday
> listesi yalnız kullanıcının başlattığı gönderimde hesaplanır, yeniden deneme (Reconcile dahil) ilk listeyi kullanır. Servis
> kimliği ileride gelirse yeniden değerlendirilir. (3) Ölçümle eskiyen ifadeler düzeltildi: D72 ve D74 "commit'lenmemiş" →
> yerel commit, main'de değil (Kaynak paragrafı, Bölüm 7). Değişen yerler: Bölüm 2, 3, 5.1, 5.3, 7, 8.1, 8.2, 8.5, 8.6, 8.7, 12,
> 13, 14.2 (rol 27), 14.3, 16 (**AC-29 ve AC-43 genişletildi**), 17, 18, Teslim dilimleri DL-5 satırı, Açık kararlar K-W7.
> Yeni hata kodu yok; mevcut AC numaraları değişmedi; yeni AC yok. MOD-0018 sahibinin teyidi Auth ekiyle.
>
> **Revizyon (2026-10-28, KARAR D74 — Auth eki inceleme bulguları, Y-18):** Kaynak: karar dosyası D74 ve kaynak yanıtı Y-18
> (MOD-0018 sahibi Ali; 2026-10-28 ölçümünde ikisi de çalışma ağacında yazılı, **commit'lenmemiş; main'de değil** [D75 revizyonu
> ölçümü: D74 yerel commit `5d2655c72`, main'de değil] — D62/1 gereği
> bu revizyonun paket kapısı D74'ün main'e girmesidir, Bölüm 7, Ready-for-dev Checklist). (1) **D74/1 — iç uç kimliği:** DL-5
> muafiyet iç ucu ve DL-6 aday sorgusu Auth'un mevcut ortak sır desenini (`X-Internal-Api-Key`, `IInternalEventAuthService`)
> kullanır; kiracı istekten değil, çağrıyı başlatan kullanıcının iletilen token'ından (`tenant_id` claim'i) alınır; yeni servis
> kimliği doğrulama katmanı yok; token yok / kiracı çıkarılamazsa kapalı-başarısız. Desen kodda ölçüldü (yeni Bölüm 8.7). Servis
> kimliği token'ı (`InternalServiceIdentityTokensController`) bu pakette **kullanılmaz (D74/1)**. **K-W3 ve K-A1 KAPANDI — KARAR
> D74/1** (K-A1'in uç adı ve sözleşme dosyası CT önerisi olarak kalır, MOD-0018 sahibinin teyidiyle). (2) **D74/2 — açık atama
> sayısı:** Treasury eki 10 açık atama anahtarı ekler (8 Treasury + `auth.payment-sod-waivers.assign`, `.read`); koddaki liste
> 10 → 20 (önceki metin "→ 18" diyordu). **K-W1 KAPANDI — KARAR D74/2–3.** (3) **D74/3 — Muafiyet yetkilisi:** yeni rol 27
> `PaymentSodWaiverAuthority` (ad önerisi, K-7) — yalnız iki Auth anahtarı, açık atamayla; atayan bu rolü taşır, taşımayan 403,
> kendine atama 403; "Hazine yöneticisi ya da K1" ifadesi kalktı. Rol sayısı 26 → 27 (26 Treasury modül rolü + 1 Auth rolü,
> DL-5); modül anahtarı sayısı 117 değişmez (iki Auth anahtarı Treasury modül anahtarı değildir — karar dosyası matris sayım
> kuralı). Rolün oluşturulma yolu yeni açık karar **K-W7**. Değişen yerler: Bölüm 1, 2, 3, 4, 5.1, 5.3, 5.4, 7, 8.2, 8.5, 8.6,
> yeni 8.7, 12, 13, 14.1, 14.2, 14.3, 14.4, 15, 16 (**AC-26, AC-29, AC-32, AC-35 genişletildi; yeni AC-42, AC-43**), 17, 18,
> 19, Teslim dilimleri DL-5 / DL-6 satırları, Açık kararlar K-W1 / K-W3 / K-A1 / yeni K-W7; ayrıca Bölüm 11 (görünen ad sayısı). MOD-0018 sahibinin teyidi Auth
> ekiyle (D74/3). Mevcut AC numaraları değişmedi.
>
> **Revizyon (2026-10-28, KARAR D72 — MOD-0140 Revizyon 5 hizalaması):** Kaynak: karar dosyası D72 (2026-10-28 ölçümünde
> karar dosyasında yazılı, **commit'lenmemiş; main'de değil** [D75 revizyonu ölçümü: D72 yerel commit `2047e94c6`, main'de değil]
> — D62/1 gereği bu revizyonun paket kapısı D72'nin main'e girmesidir,
> Bölüm 7, Ready-for-dev Checklist) ve MOD-0140 paketi Revizyon 5 (main'de `ready-for-dev`, sha256 `b3259e40c33c47200516710dfd50f7676494d848978787f8726ac636527ce5c2`;
> §14, S-2, S-4). **D72/3–4:** MOD-0140'ın anahtarları `procurement.suppliers.eligibility.read` ve `procurement.suppliers.read`
> ile MOD-0220'nin `mdm.legal-entities.read`'i (BL-652'ye kadar — MOD-0140 S-2) MOD-0140'ı çağıran Treasury ve MDM rollerine
> **çapraz modül bağımlılığı** olarak rol şablonuyla gelir (D70/1 deseni, K-4 (b)); eski "`procurement.*` kapsam dışı" ifadesi
> bu iki anahtar için kalktı. Roller paketlerden ölçüldü (MOD-0140'ı çağıran komutlar): `eligibility.read` → 11
> `TreasuryApOperator`, 12 `TreasuryApApprover`, 13 `TreasuryApOperationsManager`, 20 `BankDataViewer`, 21 `BankDataManager`,
> 22 `BankAccountActivator`; `procurement.suppliers.read` → 11, 13 (AP görüntü adı ve form seçicisi — D72/4);
> `mdm.legal-entities.read` → 20, 21, 22'ye eklendi (11–13'te zaten vardı). MOD-0126 rolleri almaz — MOD-0126 v1 MOD-0140'ı
> çağırmaz (MOD-0126 O-14, KARAR D28: alacaklı varlık doğrulaması v1'de yalnız biçim; ölçüldü `MOD-0126-payments-approvals.md:745,
> 1964`). Bağımlı anahtar olduğu için **117 modül anahtarı, 26 rol ve rol başına modül anahtarı sayıları değişmez**; bağımlı
> anahtar sayısı 5 → 7. Değişen yerler: Bölüm 1 (a), 2, 7, 14.2 (bağımlı anahtar paragrafı), 16 (**AC-3 ve AC-14 genişletildi**),
> 17, 18, 19, Açık kararlar K-4. MOD-0018 sahibinin teyidi gerekir (D72/3). Mevcut AC numaraları değişmedi; yeni AC yok.
>
> **Revizyon (2026-10-28, KARAR D71 — elle atamada iş akışı izni kesişimi):** Kaynak: karar dosyası D71 (2026-10-28 ölçümünde
> **main'de değil** — yerel dal `chore/ct/treasury-intake-ct-05`, main'de değil; D62/1 gereği K-A4b kapanışının paket kapısı D71'in main'e girmesidir —
> Bölüm 7, Ready-for-dev Checklist). Tüketici servisler (MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0217) onaylayıcı aday
> listesini `platform.workflow.tasks.approve` iznine sahip kullanıcılarla da kesiştirir: DL-6 sorgusuna ayrı çağrı (tier-2
> kesişimiyle aynı desen); kesişimi **tüketici** yapar, sorgu yine tek anahtar alır (sorgu sözleşmesi değişmez). Çağrı okunamazsa
> tüketicide kapalı-başarısız; kesişim boşsa tüketicinin 422 "uygun onaylayıcı yok" dalı (D68 / D70/2). **K-A4b KAPANDI — KARAR
> D71** (seçenek (a)). D70/1 (rol şablonu) değişmedi; 117 anahtar / 26 rol değişmez. MOD-0018 sahibinin teyidi Auth ekiyle
> birlikte. Değişen yerler: Bölüm 7 (D71 satırı), 8.6 (tüketici anahtarları paragrafı), 17 (DL-6 satırı), 18, Açık kararlar
> K-A4b. Mevcut AC numaraları değişmedi; yeni AC yok.
>
> **Revizyon (2026-10-28, KARAR D70 — onaylayıcı rollerin iş akışı izni ve Auth paket kapısı):** (1) **D70/1:** bütün onaylayıcı
> rollere (7 `TreasuryPaymentApprover`, 8 `TreasuryPaymentTier2Approver`, 12 `TreasuryApApprover`, 15 `TreasuryArApprover`,
> 17 `TreasuryArTier2AdjustmentApprover`, 19 `TreasuryCashPositionApprover`, 26 `ContractVersionApprover` — matristen ölçüldü:
> `platform.workflow.tasks.approve` / `.reject` satırındaki roller + D70/1 ile Sözleşme onaylayıcı) `platform.workflow.tasks.approve`
> ve `.reject` çapraz modül bağımlılığı olarak rol şablonuyla gelir: **K-4 (b) KAPANDI — KARAR D70/1**, **K-A4 KAPANDI — D70/1**
> (iş akışı izni aday şartı değildir, rol şablonuyla gelir; aday sorgusu tek anahtar — modül onay anahtarı — alır). Rol şablonu
> yoluyla tutarlı; elle atamada açık nokta — **yeni açık karar K-A4b** (iki seçenek, Açık kararlar; **KARAR D71 ile kapandı** — yukarıdaki D71 notu). Bağımlı anahtar
> olduğu için 117 modül anahtarı ve rol başına modül anahtarı sayıları değişmez. Değişen yerler: Bölüm 1 (a), 2, 8.6, 14.2
> (bağımlı anahtar paragrafı), 16 (**AC-3 ve AC-18 genişletildi**), 17, 18, Teslim dilimleri, Açık kararlar K-4 / K-A4. Bağımlı
> manifestin `platform.workflow.*` tohum sahipli anahtarlarla (`SeedOwnedTenantScopeKeys`, FU24/25) olası çatışması K-4'te ölçüm notu
> olarak kalır. (2) **D70/3:** D53/1 Auth paket metni kapısı = metin main'de + MOD-0018 sahibinin teyidi (teyit karar dosyasına kaynak
> olarak yazılır) — yukarıdaki D53 paragrafı ve Ready-for-dev Checklist; açık [D80 revizyonu: **şartlı teyit — Y-19 / KARAR
> D80/2**; DL-1 bekletilmez, DL-5 kodundan önce K-W5 defter kararı + rol atama denetim borcu]. (3) Ölçümle eskiyen ifadeler düzeltildi: D64 PR #276,
> D66 PR #281 ile main'de (Kaynak paragrafı, Bölüm 7, Ready-for-dev Checklist). D70 2026-10-28 ölçümünde **main'de değil**
> (yerel dal `chore/ct/treasury-intake-ct-05`, main'de değil); D62/1 gereği K-4 / K-A4 kapanışının ve D70/3 kapısının paket kapısı D70'in main'e
> girmesidir (Ready-for-dev Checklist). MOD-0018 sahibinin teyidi gerekir (D70/1). Mevcut AC numaraları değişmedi; yeni AC yok.
>
> **Revizyon (2026-10-28, KARAR D69 — sözleşme sürümü onay anahtarı):** işleyen matrise yeni anahtar `mdm.contracts.versions.approve` ve yeni rol
> "Sözleşme onaylayıcı" (`mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.versions.approve`; belge deposu izni yok — D56/2; açık atama
> değil) eklendi: toplam **117 anahtar, 26 rol**; DL-3 **18 anahtar, 4 rol, 1 açık atama** (rol 26 `ContractVersionApprover`, ad
> önerisi K-7). Yeni anahtarın eylemi `approve`'dur, `read` değil: Viewer dışlama listesi (18 `read` anahtarı) değişmez. Bölüm 8.6
> tüketici anahtarı satırında MOD-0217 "açık" → `mdm.contracts.versions.approve` (D69; MOD-0217 O-3 (b) kapandı). Değişen yerler: Bölüm 1, 2, 3, 4, 5.1, 7,
> 8.1, 8.2, 8.6, 11, 12, 13, 14.2, 16 (DL-3 başlığı; **AC-7, AC-18, AC-19 genişletildi**), 17, 18, 19 (mekanik uzlaştırma yeniden
> koşuldu), Teslim dilimleri DL-3 satırı, K-2. Rolün bağımlı MOD-0023 görev anahtarlarını (`platform.workflow.tasks.approve` /
> `.reject`) taşıyıp taşımayacağını D69 yazmadı — K-4'e eklendi (K-A4 ona bağlıydı; ikisi D70/1 ile kapandı). D69 2026-10-28 ölçümünde **main'de değil**
> (yerel commit `db69732cb`); D62/1 gereği DL-3'ün paket kapısı D69'un main'e girmesini bekler (Ready-for-dev
> Checklist). MOD-0018 sahibinin teyidi Auth ekiyle birlikte (D69). Mevcut AC numaraları değişmedi; yeni AC yok.
>
> **Revizyon (2026-10-28, D66/1 — onaylayıcı aday sorgusu):** MOD-0023 şablonları atamasız olduğundan (Y-12) onaylayıcı aday
> tabanı Auth'tan gelir: "bu izne sahip kullanıcılar" sorgusu bu pakete **yeni dilim DL-6** olarak eklendi (kapsam g; Bölüm 1,
> 2, 3, 5.4, 7, 8.6, 10, 12, 13, 14.4, 15, 17, 18, 21, Teslim dilimleri, Açık kararlar K-A1…K-A4; **yeni AC-35…AC-41**). D56/3'ün muafiyet aday listesi ayrı
> tanım olarak kalmaz: DL-6 sorgusunun `waive-sod` + parti biçimidir (Bölüm 8.5, 8.6 — tek aday sorgusu). **Neden DL-1 değil:**
> DL-1 kodu tüketicilerin DL-1 kabul kapısıdır (D53/2) ve iç uç servis kimliği kararını (K-A1 / K-W3) ister; DL-6 tüketicilerin
> onay dilimlerinden (MOD-0126 DL-2, MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3, MOD-0217 DL-3) önce main'de olmalıdır,
> DL-1'i bekletmez. Kaynak: karar dosyası D66 — PR #281 ile main'de (`c32cabe6b`; ölçüm 2026-10-28 `git merge-base
> --is-ancestor 3c98e7bfc c32cabe6b` → 0); D62/1 gereği DL-6'nın "D66 main'de" paket kapısı kapandı. D66/1 MOD-0018 sahibinin teyidini
> ister (Ready-for-dev Checklist). Açık kalanlar K-A1…K-A3 (K-A4 D70/1 ile kapandı). Mevcut AC numaraları değişmedi.
>
> **DCP-002 kimlik durumu — K-1 KAPANDI — MOD-0018 sahibi kararı (Y-19 / D80/1): MOD-0018-FU27.** Ön kontrol yeniden koşuldu
> (2026-10-29): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU27 --name "Treasury Permission
> Onboarding" --parent MOD-0018` → `OK  MOD-0018-FU27: proven against Blueprint/registry.` (çıkış 0). Kayıt satırı
> (`execution/registries/module-id-registry.md`, FU22 satırının ardından) paketle birlikte yazıldı; FU23–FU26'nın kayıt satırı
> yoktur (aşağıdaki ölçüm; arşiv kayıt durumu bu paketin işi değildir). Aşağısı K-1 kapanmadan önceki ölçümdür (tarihçe; eski
> geçici ad FU-TBD): ana kayıtta (`execution/registries/module-id-registry.md`) en
> büyük kayıtlı numara MOD-0018-FU22'dir; ancak FU23–FU26 paket dosyaları arşiv dallarında vardır
> (`origin/archive/pc2/feature/pss/mod-0018-fu24-fu26-governance-reconciliation` ağacında `MOD-0018-FU23…FU26-*.md`) ve
> FU23–FU25 kodda anılır (`SeedOwnedTenantScopeKeys.cs:7`, `InternalPermissionsController.cs:150`). Doğrulayıcı arşivi
> görmez: `verify_module_id.py . --check-id MOD-0018-FU23 --name "Treasury Permission Onboarding" --parent MOD-0018` → `OK`
> (yanlış olumlu). Aday `MOD-0018-FU27` için aynı komut `OK MOD-0018-FU27: proven against Blueprint/registry` (çıkış 0).
> `--discover "Treasury permission onboarding"` çıkış 3 ("inceleme gerekli"; Blueprint 0, registry 6, paket 8 bulgu;
> rapor-sha256 `959839624f40c2bfff71597652b244318421e166c958276b03e2f29dfcd6c045`). Auth kulesi numarayı kendisi vereceğini
> yazmıştı (Y-6, Y-10/1). Bu yüzden dosya adı ve `id` o tarihte `FU-TBD` idi; `FU-TBD` doğrulayıcıdan geçmez (beklenen,
> kapalı-başarısız). Numara Y-19 ile verildi; dosya adı, `id`, `branch` ve kayıt satırı birlikte yazıldı (D80/1).
>
> **Golden Reference kararı.** Arka uç yetki / katalog / onboarding işi. Razor ya da DataTable yüzeyi yoktur:
> `shell: none`, `golden_reference: none`, `form_field_count: 0` bilinçlidir. DL-5 Auth'ta kiracıya ait yeni bir kayıt türü
> açtığı için `entity_base: EntityBase` (Auth için somut taban sınıf).

## 1. Module Summary

Bu FU, Treasury'nin altı paketinin (MOD-0126 Ödeme ve onaylar, MOD-0119 Borç / AP, MOD-0120 Alacak / AR, MOD-0124 Nakit
pozisyonu, MOD-0289 Banka ve banka hesabı kaydı, MOD-0217 Sözleşme yaşam döngüsü) izinlerini Auth'a katar. Kapsam MOD-0018
sahibinin a–f listesidir (Y-10/1), D64 ile eklenen muafiyet ataması ve D66/1 ile eklenen onaylayıcı aday sorgusudur:

| | Kapsam | Dilim |
|---|---|---|
| a | 26 Treasury rol şablonu (D47 rol matrisi, D49, D69 ve D70/1 ile güncel) + DL-5'te 27. rol Muafiyet yetkilisi (**KARAR D74/3**; yalnız iki Auth anahtarı, açık atamayla) — ad ve anahtar ataması matristen birebir; onaylayıcı rollerde `platform.workflow.tasks.approve` / `.reject` çapraz modül bağımlılığı (D70/1); MOD-0140'ı çağıran AP ve banka rollerinde `procurement.suppliers.eligibility.read` / `procurement.suppliers.read` ve banka rollerinde `mdm.legal-entities.read` çapraz modül bağımlılığı (D72/3–4) | DL-1, DL-2, DL-3 |
| b | 117 modül izin anahtarı (D57/4 ve D69 sonrası) — sayıldı, matrisle uzlaştırıldı (Bölüm 19) | DL-1, DL-2, DL-3 |
| c | 10 "yalnız açık atama" (`ExplicitGrantOnlyPermissions`) anahtarı (**KARAR D74/2**): 8 Treasury anahtarı (`waive-sod` dahil; DL-1, DL-2, DL-3) + 2 Auth anahtarı `auth.payment-sod-waivers.assign` / `.read` (DL-5); koddaki liste 10 → 20 | DL-1, DL-2, DL-3, DL-5 |
| d | `waive-sod` atamasının parti bağı (D54) — **D64 ile bu paketin kapsamında**, ayrı dilim; muafiyet aday listesi (D56/3) D66/1 ile DL-6 aday sorgusunun `waive-sod` + parti biçimidir (parti bağını bu dilimin kaydı verir) | DL-5 |
| e | SEC-002 eki (D56/1): Platform izin listesi + MDM istemcisinin modül kodu | DL-4 |
| f | İki yeni ayar anahtarı `finance-treasury.payment-sod-settings.read` / `.update` (D57/4) | DL-1 |
| g | Onaylayıcı aday sorgusu — "bu izne sahip kullanıcılar" (iç uç, `X-Internal-Api-Key` + iletilen kullanıcı token'ı — **KARAR D74/1**; kiracı token'dan, yalnız etkin kullanıcı, açık atama anahtarında yalnız açık ataması olanlar; şirket kapsamı K-A2) — **D66/1** (D56/3 yönünde) | DL-6 |

Sözleşme rollerine belge deposu izni verilmez (D56/2). `mdm.contracts.*` kaydı MOD-0217 onayından sonra yapılır (Y-6,
Y-10/1) — bu yüzden ayrı dilim (DL-3) ve koşullu.

Anahtarlar mevcut yolla kaydolur (Y-16/4): tüketici servisin modül manifesti → Platform genel kataloğu → Auth eşitlemesi
(BL-592 deseni, `docs/guides/operations/post-deploy-steps.md:317-320`). Bu FU anahtarı **beyan etmez** (beyan tüketici
paketinin manifest adımıdır); Auth tarafında (1) açık atama listesini, (2) varsayılan rol tabanından dışlamayı, (3) modül
hakkıyla kurulan özel hak profilini ve 26 ayrılmış Treasury sistem rolünü, (4) muafiyet atama kaydını, iki Auth anahtarını
ve 27. rol Muafiyet yetkilisini (D74/2–3), (5) onaylayıcı aday sorgusunu sahiplenir.

Neden özel profil gerekir (kod gerçeği, ölçüldü): genel eşitleme bir modül hakkıyla Admin'e modülün **bütün** anahtarlarını,
Viewer'a bütün `read` eylemli anahtarlarını verir (`EntitlementPermissionSyncService.cs:572-580`). Ayrıca varsayılan Viewer
tabanı, modül hakkı olmayan kiracılarda dahil, `read` eylemli her kiracı anahtarını verir
(`DefaultRolePermissionTemplate.cs:207-213`; dışlama yalnız `EntitlementOnlyViewerPermissions`, `:120-168`). Treasury'nin
117 anahtarından 18'i `read` eylemlidir (Bölüm 4). Genel davranış Treasury'de görev ayrılığını (SoD) çökertir — Admin aynı
partiyi hazırlayıp onaylama izinlerinin ikisini birden alır — ve finans / banka verisini her Viewer'a açar.

## 2. Ownership and Boundaries

**Bu FU / Auth sahibi (MOD-0018; yazar Treasury CT, inceleme ve teyit sahibin):**

- 10 anahtarın `ExplicitGrantOnlyPermissions` listesine girmesi (8 Treasury + 2 Auth, KARAR D74/2; liste 10 → 20).
- 18 `read` eylemli anahtarın (ve modül hakkı olmadan verilmemesi gereken diğerlerinin) varsayılan rol tabanından dışlanması.
- Treasury modül kodları için tam küme doğrulamalı, saf (persist etmeyen) hak profili ve 26 ayrılmış Treasury sistem rol şablonu.
- Kiracı + kaynak modül sınırlı, idempotent hak verme ve geri alma; kullanıcıya otomatik rol üyeliği yok.
- DL-5: `waive-sod` parti bağlı muafiyet atama kaydı, yazma ucu, "bu parti için geçerli atamalar" sorgu ucu, K2 denetim kaydı;
  iki Auth anahtarı (`auth.payment-sod-waivers.assign`, `.read`; yalnız açık atama) ve 27. rol **Muafiyet yetkilisi**
  (`PaymentSodWaiverAuthority`, KARAR D74/3; **KARAR D75/1**: Treasury hak profili rolü izinsiz oluşturur, iki anahtar açık
  atamayla bağlanır; atayan izin + rol üyeliği taşır); iç uç kimliği Bölüm 8.7 (KARAR D74/1; sınırları KARAR D75/2).
- DL-6 (D66/1): "bu izne sahip kullanıcılar" aday sorgusu — salt okuma iç uç (kimlik Bölüm 8.7, KARAR D74/1); kiracı sınırlı, yalnız etkin kullanıcı, açık
  atama anahtarında yalnız açık atama; `waive-sod` için parti biçimi (D56/3 ile birleşik). Auth SoD kuralı uygulamaz.

**Tüketici paket sahipleri (beyan ve çalışma anı):**

- Modül manifesti ve anahtar beyanı: dört finans paketi için tek yazar MOD-0126 DL-1 (servis ortak yapı taşı; MOD-0119
  `:557`, MOD-0124 `:643`); MOD-0289 ve MOD-0217 için MDM manifest sağlayıcıları (MOD-0217 `ContractLifecycleManifestProvider.cs`).
- `[HasPermission]` zorlaması ve çalışma anı SoD kuralları (MOD-0126 R1–R3, R9; MOD-0119 R1–R3; MOD-0120 A1, A2, A3, A7;
  MOD-0124 R1–R3; MOD-0289 D23/2; MOD-0217 D47/5). Rol şablonu SoD değildir; bir kişiye birden çok rol verilebilir.
- Muafiyetin kullanım anı denetimi MOD-0126'dadır (AC-42, AC-43; 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`). Bu FU MOD-0126 paketini
  değiştirmez; DL-5 onun okuduğu alanları ve sorguyu sağlar.
- Aday sorgusunun sonucuna paketin kendi kurallarını uygulamak (MOD-0126 R1–R4, MOD-0119 R1–R3, MOD-0120 / MOD-0124 SoD
  dışlamaları, MOD-0217 hazırlayan hariç) ve kalanı MOD-0023'e `StartWorkflowInstanceRequest.CandidatePrincipalIds` olarak
  göndermek tüketicinindir (D66/1; şablonlar atamasız — Y-12); ayrı dizin istemcisi taşınmaz.

**Platform sahibi (Beste) — XMC-001:** DL-4'teki Platform dosyaları (Bölüm 5). Onay gelene kadar dokunulmaz (D63/2).
**MDM sahibi — XMC-001:** DL-4'teki MDM istemci dosyası.

**Kapsam dışı:**

- Manifest / anahtar beyanı, tüketici servis kodu, Gateway, ekran, gezinme.
- `platform.document-repository.*` hiçbir role verilmez (D56/2); belge deposu servis kimliği desteği MOD-0262-FU01 / Platform'un
  işidir (D56/2, Y-8).
- Bağımlı anahtarların (`mdm.legal-entities.read`, `platform.workflow.instances.start`, `platform.workflow.tasks.approve`,
  `.reject`, `.request-info`; **KARAR D72/3–4** ile `procurement.suppliers.eligibility.read`, `procurement.suppliers.read`) kaydı
  — o modüllerin sahibindedir (D47 rol matrisi giriş paragrafı; `procurement.suppliers.*` için MOD-0140, Auth katalog / seed
  kaydı MOD-0140 §18 "DL-3 canlı öncesi" maddesi). Rollere ulaşma yolu **K-4 (b) — KARAR D70/1** (D72/3 aynı deseni ister):
  Treasury manifestinde çapraz modül bağımlılığı olarak beyan edilir, profil verir (Bölüm 14.2).
- `crm.*`, MOD-0031 okuma izinleri ve `procurement.*`'ın D72/3–4 dışındaki anahtarları (matrisin anahtar kapsamı dışı;
  `procurement.suppliers.eligibility.read` / `procurement.suppliers.read` artık bağımlı anahtardır — yukarıdaki madde).
- Platform modül kataloğu / kiracı hakkı (entitlement) kayıtları, kiracı verisi, rol üyeliği ataması, token içeriği,
  Production / Staging işlemleri.
- `waive-sod` iznini token'da süreli yapmak (token düzenlemesi) — **K-W2 KAPANDI — KARAR D79/4:** izin açık atamayla
  süresizdir, her kullanım ayrıca geçerli DL-5 ataması ister; token üretimine dokunulmaz.

## 3. Owned Objects

| Nesne | Sahip | Kesin değişmez |
|---|---|---|
| 117 Treasury `Permission` tanımı | Auth genel kataloğu (beyan tüketici manifestinde) | Anahtar başına tek etkin genel tanım; `TenantId` yok; `Permission.Module` = Bölüm 8'deki modül kodu |
| 10 açık atama anahtarı (8 Treasury + 2 Auth, KARAR D74/2) | `ExplicitGrantOnlyPermissions` | Hiçbir otomatik yol (SuperAdmin tam katalog, modül hakkı eşitlemesi, varsayılan rol) vermez |
| Treasury hak profili | Auth `Application/Common/Services` | Saf eşleme; modül kodu başına tam anahtar kümesi; eksik / fazla / bilinmeyen anahtar → mutasyondan önce ret |
| 27 ayrılmış sistem rolü (26 Treasury + Muafiyet yetkilisi, KARAR D74/3) | Auth, kiracı başına sistem rolü | Bölüm 14'teki tam anahtar kümesi; kullanıcı ataması otomatik değil |
| `PaymentSodWaiverAuthority` — Muafiyet yetkilisi (DL-5, KARAR D74/3) | Auth, kiracı başına sistem rolü | Yalnız `auth.payment-sod-waivers.assign` ve `.read`; ikisi de açık atama — hiçbir otomatik yol rolü doldurmaz; rolü Treasury hak profili izinsiz oluşturur (**KARAR D75/1**); atayan izin + rol üyeliği taşır |
| Admin / Viewer Treasury varsayılanı | Auth hak profili | **Hiçbir Treasury anahtarı — KARAR D79/2** (K-2 kapandı) |
| Modül kaynaklı haklar | Auth | Kiracı sınırlı; `GrantSource = Module`; `SourceModuleCode` = anahtarın modül kodu |
| `PaymentSodWaiverAssignment` (DL-5) | Auth, kiracıya ait kayıt | Bölüm 4; hedef parti zorunlu; süre ≤ 24 saat; onaylayan ve gerekçe kodu zorunlu |
| `CreatePaymentSodWaiverAssignmentCommand`, `RevokePaymentSodWaiverAssignmentCommand` (DL-5) | Auth | K2 denetim (Bölüm 21) |
| `GetPaymentSodWaiverAssignmentsForBatchQuery` (DL-5) | Auth | Kiracı uçları ve iç (servis) ucu aynı sorgu; yalnız geçerli atamaları döner (Bölüm 8) |
| `GetPermissionCandidatesQuery` (DL-6) | Auth | Yalnız iç (servis) uç; salt okuma; Bölüm 8.6 kuralları; yanıt yalnız kullanıcı kimlikleri |

DL-1…DL-4 ve DL-6 yeni kalıcı varlık açmaz; mevcut `Permission`, `Role`, `RolePermission`, `RoleAssignmentVersion`, `User`,
`UserRole`, manifest ve kiracı hakkı modelleri kullanılır (DL-6 yalnız bir okuma dizini ekler, Bölüm 5.4).

## 4. Entity Fields

**Mevcut nesneler (DL-1…DL-3):**

| Nesne / alan | Değer ya da kural |
|---|---|
| `Permission.Key` | Bölüm 14'teki 117 anahtardan biri; küçük harf, noktalı, ≥ 3 parça (PKS-001 §1); takma ad ya da joker yok |
| `Permission.Module` | Anahtarın tüketici modül kodu — paket başına bir kod (Bölüm 8.1, **KARAR D79/1**, K-1b) |
| `Permission.Resource` / `.Action` | `PermissionKeyParser` (ilk parça modül, son parça eylem, aradakiler kaynak) — ör. `mdm.contracts.value.read` → kaynak `contracts.value`, eylem `read` |
| `Permission.Scope` | `Tenant` (kiracıya atanabilir); katalog satırı geneldir |
| `Role.Name` | 27 ayrılmış ad (Bölüm 14; 26 Treasury + `PaymentSodWaiverAuthority`, D74/3); kiracı sistem rolü; aynı adlı sistem dışı rol → çakışma, hak yok |
| `RolePermission.TenantId` | Sunucu tarafından türetilir; boş / uyuşmaz → kapalı-başarısız |
| `RolePermission.GrantSource` / `.SourceModuleCode` | `Module` / anahtarın modül kodu |

`read` eylemli 18 anahtar (varsayılan Viewer tabanının seçtiği eylem): `finance-treasury.payment-batches.read`,
`finance-treasury.payment-trace.read`, `finance-treasury.payment-evidence.read`, `finance-treasury.payment-exceptions.read`,
`finance-treasury.payment-sod-settings.read`, `finance-treasury.ap-invoices.read`, `finance-treasury.ap-invoice-trace.read`,
`finance-treasury.receivable-invoices.read`, `finance-treasury.receivable-trace.read`, `finance-treasury.cash-positions.read`,
`finance-treasury.cash-position-threshold-policies.read`, `mdm.banks.read`, `mdm.bank-accounts.read`,
`mdm.bank-accounts.sensitive-reads.read`, `mdm.bank-account-purposes.read`, `mdm.contracts.read`, `mdm.contracts.value.read`,
`mdm.contracts.documents.read`.

**Yeni kayıt (DL-5): `PaymentSodWaiverAssignment` (`EntityBase`, kiracıya ait, koleksiyon `paymentSodWaiverAssignments`)**

| Alan | Tür | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | Evet | JWT'den; istekte yok |
| `GranteeUserId` | Guid | Evet | Aynı kiracının kullanıcısı; çağıran olamaz |
| `TargetPaymentBatchId` | Guid | Evet | Boş Guid yasak; Auth partiyi doğrulamaz (opak), MOD-0126 rotadaki partiyle eşler (D54) |
| `StartsAtUtc` | DateTimeOffset | Evet | Sunucu saatinden en çok 5 dk önce (saat kayması), en çok 24 saat sonra |
| `ExpiresAtUtc` | DateTimeOffset | Evet | `StartsAtUtc < ExpiresAtUtc ≤ StartsAtUtc + 24 saat` |
| `ApprovedByUserId` | Guid | Evet | Sunucu türetir = çağıran; istekte yok; `GranteeUserId`'den farklı |
| `ReasonCode` | string | Evet | Kapalı liste `ACIL-ODEME`, `TEK-YETKILI`, `DENETIM-ISTEGI` (D49/4) |
| `RevokedAtUtc` / `RevokedByUserId` | DateTimeOffset? / Guid? | Hayır | Yalnız geri alma komutu yazar; geri alınan atama geçerli değildir |
| `IsDeleted` / `DeletedAt` | — | — | Soft delete (EntityBase); fiziksel silme yok; v1'de silme komutu yok |
| `Version` | — | — | Teknik eşzamanlılık alanı (EntityBase) |

Dizinler: `(TenantId, TargetPaymentBatchId, ExpiresAtUtc)` ve `(TenantId, GranteeUserId, TargetPaymentBatchId)`; ikisi de
kısmi `IsDeleted = false`. Benzersizlik kısıtı yok (aynı kişiye aynı partide ardışık atama mümkün; sorgu yalnız geçerli olanı döner).

## 5. Repo Scope

Bu planlama görevi yalnız bu paketi yazar. Sonraki, ayrı onaylı çalışma anı teslimi aşağıdaki **joker içermeyen** listeyle
sınırlıdır ("yeni" yazılmayan dosya mevcuttur; satır numaraları `chore/ct/treasury-intake-ct-05` @ `5cfd67813` ölçümüdür).

### 5.1 Auth — DL-1, DL-2, DL-3 (MOD-0018 sahibi alanı)

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` — `Keys` kümesi
  `:89-94` (bugün 10 anahtar → 20: DL-1…DL-3'te 8 Treasury anahtarı, DL-5'te 2 Auth anahtarı — **KARAR D74/2**); her yeni
  anahtar için gerekçeli sabit (dosyanın deseni `:10-87`).
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs` —
  `EntitlementOnlyViewerPermissions` `:120-168` (Viewer `read` kuralı `:207-213`).
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/TreasuryEntitlementGrantProfile.cs` — **yeni**;
  saf, modül kodu başına tam küme + 26 rol şablonu (desen `ProductAbbreviationEntitlementGrantProfile.cs:9-100`); DL-5 bu
  profile 27. rolü (Muafiyet yetkilisi, izinsiz ayrılmış rol) ekler (**KARAR D75/1**, K-W7 (a)).
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs` — rol adı
  listeleri `:94`, `:96-110`; `GrantModuleWithKeysAsync` profil seçimi `:151-200`; `GrantPermissionsToRolesAsync` `:282-346`
  (açık atama süzgeci `:294` aynen kalır); `RevokeModuleAsync` `:373-390`; `SelectForRole` `:572-580`;
  `ReconcileSpecialProfilesAsync` `:582-625`.

**Auth testleri:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs` — yalnız gerileme
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/TreasuryEntitlementGrantProfileTests.cs` — **yeni**
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/TreasuryPermissionOnboardingMongoTests.cs` —
  **yeni**; gerçek, ayrı sahipli Mongo; sabit veritabanı `diten_auth_treasury_itest`; açık `MONGO_TEST_URI`; uygulama portu
  27017 yasak (FU20 2026-09-10 eki deseni, `MongoDbIndexConfigurations.cs:129` `EnsurePermissionOnboardingIndexesAsync`).
- Mevcut `ProductAbbreviationEntitlementGrantProfileTests`, FU22 / BL-592 testleri — salt okunur gerileme.

### 5.2 Platform ve MDM — DL-4 (SEC-002 eki, D56/1) — **"Dokunulacak dosyalar" (XMC-001 isteği, D63/2)**

**Platform (sahibi Beste — XMC-001 onayı Beste'den; MOD-0018 sahibi bu onayı vermez) — XMC-001 VERİLDİ (Y-22 / KARAR D88/3):**
yalnız aşağıdaki üç dosya (doğrulayıcı, testleri, `post-deploy-steps.md`) + ortam ayarı adımı; başka Platform dosyasına dokunulmaz;
her izin yalnız kendi modül koduyla eşleşir (banka izni ürün modülü adına sorulamaz); `DITENMDMSERVICE` kimlik kontrolü değişmez;
testler: yeni tavan ve çiftler, yanlış modül kodlu çiftin reddi, bilinmeyen çiftin 403'ü — sabotaj kanıtıyla; kod FU27 onaylandıktan
sonra yazılır. MDM tarafı (`PlatformTrustedLegalEntityScopeProviderClient.cs` ve testleri) MDM sahibinin XMC-001'ine bağlı.

| Dosya | Satır (ölçüm) | Bugün | Değişiklik |
|---|---|---|---|
| `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs` | `:15` | `MaxAllowedPairs = 38` | tavan, eklenen çift sayısı kadar artar (K-5 listesine bağlı) |
| aynı dosya | `:35-56` | `FirstConsumerPermissions` — yalnız ürün anahtarları (38) | K-5'teki `mdm.bank-accounts.*` / `mdm.contracts.*` anahtarları eklenir |
| aynı dosya | `:97-101` (`:99`) | `AllowsPair` modül kodunu `"product-item-sku-master"`'a sabitler | izinli modül kodu kümesi (ürün + MOD-0289 ve MOD-0217'nin kendi kodları — paket başına bir kod, **KARAR D79/1**); çift → modül kodu eşlemesi kesin |
| aynı dosya | `:103-115` (`:111`) | `IsValidConfiguration` her çiftin modül kodunu sabit koda zorlar | aynı küme; bir çift yanlış modül koduyla gelirse yapılandırma geçersiz (bugünkü kapalı-başarısız davranış korunur) |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeCredentialAuthenticatorTests.cs` | `:146`, `:253` (tavan 38 sabitlemesi); `:19-32`, `:69-133` (modül kodu ve izin listesi testleri) | — | yeni tavan; yeni çiftler için gerçek doğrulayıcı testi; ürün çiftleri değişmez; yanlış modül kodlu çift reddi |
| Ortam yapılandırması `TrustedLegalEntityScopeCredential:Mdm:AllowedPairs` | depoda yok (her ortam verir) | 38 çift | yeni çiftler (sıra Bölüm 8) |
| `docs/guides/operations/post-deploy-steps.md` | yeni adım (desen `:297-320`, BL-592 §7) | — | sıra, geri alma ve kontrol |

**Koşullu (K-6 "evet" ise — finans tüketicisi Platform kapsam ucunu kullanacaksa) — GEREKİYOR: K-6 "evet" (Platform sahibi, Y-23 /
KARAR D92/1; D88/2'deki "hayır" düzeltildi). XMC-001'i Beste finans çift listesi, dosya-satır listesi ve sabotajlı testleri gördükten sonra
verecek (gönderildi, 2026-10-29). Aşağıdaki tablo ilk ölçümdür; güncel satır listesi Beste'ye giden pakettedir. Önceki notun tarihçesi: finans tüketicisi için **ikinci kimlik bilgisi bağlama** değişiklikleri
yapılmaz. `TrustedLegalEntityScopeCredentialAuthenticator.cs` yukarıdaki XMC-001 kapsamında (izin listesi, tavan, modül kodu kümesi)
değişir; tablodaki satırları (`:11`, `:27`, `:76-81` — tüketici kimliği / `Mdm` bağlaması) ise değişmez:**

| Dosya | Satır | Bugün |
|---|---|---|
| `services/Diten.Platform/src/Diten.Platform.API/Configuration/TrustedLegalEntityScopeCredentialOptions.cs` | `:7` | tek bağlama `Mdm` |
| `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs` | `:11`, `:27`, `:76-81` | tüketici `DITENMDMSERVICE` sabit; yalnız `options.Value.Mdm` okunur |
| `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeRequestExecutor.cs` | `:68` | `AllowsPair` çağrısı (tüketiciye göre seçim gerekir) |
| `services/Diten.Platform/src/Diten.Platform.API/Program.cs` | `:192-193`, `:213-215` | seçenek ve doğrulayıcı kaydı |
| `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalLegalEntityScopeResolutionController.cs` | `:15` (sınıf; ilgili satırlar ölçülmedi) | MDM kimlik bilgisiyle çağrı |

**MDM (sahibi MDM sahibi — XMC-001 onayı ondan) — XMC-001 VERİLDİ (Ali, Y-26 / KARAR D97/1):** yalnız aşağıdaki dosya ve testleri; sıra Auth izinleri → Platform kodu → Platform ortam ayarı → MDM istemcisi (§8.3).

| Dosya | Satır | Bugün | Değişiklik |
|---|---|---|---|
| `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs` | `:18` | `private const string ModuleCode = "product-item-sku-master"` | izinli modül kodu kümesi |
| aynı dosya | `:162-171` (`:170`) | `IsValidRequest` başka modül kodunu 400 `LEGAL_ENTITY_SCOPE_REQUEST_INVALID` ile reddeder | MOD-0289 / MOD-0217 kodları kabul; bilinmeyen kod yine 400 |
| aynı dosya | `:260`, `:282`, `:362`, `:377`, `:412` | yanıttaki `moduleCode` istenenle birebir karşılaştırılır | değişmez (gerileme) |
| `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/PlatformTrustedLegalEntityScopeProviderClientTests.cs` | — | — | yeni kodlar + bilinmeyen kod reddi |
| `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeProviderContractTests.cs` | — | — | gerileme |

### 5.3 Auth — DL-5 (waive-sod parti bağı, D64)

Golden Reference arka uç klasör düzeni (Bölüm 10):

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/PaymentSodWaiverAssignment.cs` — **yeni**
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/PaymentSodWaiverAssignments/Commands/CreatePaymentSodWaiverAssignmentCommand.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Commands/RevokePaymentSodWaiverAssignmentCommand.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Queries/GetPaymentSodWaiverAssignmentsForBatchQuery.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Handlers/CommandHandlers/CreatePaymentSodWaiverAssignmentHandler.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Handlers/CommandHandlers/RevokePaymentSodWaiverAssignmentHandler.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Handlers/QueryHandlers/GetPaymentSodWaiverAssignmentsForBatchHandler.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/Validators/CreatePaymentSodWaiverAssignmentValidator.cs` — **yeni**
- `…/Features/PaymentSodWaiverAssignments/PaymentSodWaiverAssignmentsModels.cs` — **yeni** (tek dosyada DTO'lar)
- `…/Features/PaymentSodWaiverAssignments/PaymentSodWaiverAssignmentErrorCodes.cs` — **yeni** (desen `Features/Roles/RoleErrorCodes.cs`)
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IPaymentSodWaiverAssignmentRepository.cs` — **yeni**
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PaymentSodWaiverAssignmentRepository.cs` — **yeni**
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs` — iki dizin (`:26` `EnsureIndexesAsync` içinden)
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs` — depo kaydı (`:101-103` deseni)
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PaymentSodWaiverAssignmentsController.cs` — **yeni**; kiracı uçları
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPaymentSodWaiverAssignmentsController.cs` — **yeni**; iç
  uç; kimlik Bölüm 8.7 (**KARAR D74/1**: `X-Internal-Api-Key` + iletilen kullanıcı token'ından kiracı; desen
  `InternalPermissionsController.cs:21, 49-52`; servis kimliği token'ı kullanılmaz)
- Testler (**yeni**): `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/SodWaivers/CreatePaymentSodWaiverAssignmentHandlerTests.cs`,
  `…/SodWaivers/GetPaymentSodWaiverAssignmentsForBatchHandlerTests.cs`, `…/SodWaivers/PaymentSodWaiverAssignmentAuditTests.cs`,
  `…/SodWaivers/PaymentSodWaiverAssignmentMongoTests.cs` (sabit `diten_auth_sodwaiver_itest`, `MONGO_TEST_URI`, 27017 yasak),
  `…/SodWaivers/PaymentSodWaiverAssignmentContractTests.cs` (MOD-0126 tüketici sözleşmesi),
  `…/SodWaivers/InternalPaymentSodWaiverAssignmentsAuthTests.cs` (iç uç kimliği ve token'dan kiracı — AC-32; gerçek denetleyiciyle,
  desen `…/Permissions/InternalPermissionsControllerTests.cs`).
- **KARAR D74/2–3:** iki Auth anahtarı (`auth.payment-sod-waivers.assign`, `.read`) `ExplicitGrantOnlyPermissions.cs`'e (Bölüm 5.1)
  ve Auth'un kendi katalog / tohum yoluna girer (katalog dosyası DL-5 başında ölçülür; `DataSeeder.cs` Bölüm 6'da korumalı —
  gerekirse ayrı onay); 27. rol Muafiyet yetkilisi — oluşturulma dosyası Bölüm 5.1'deki `TreasuryEntitlementGrantProfile.cs`
  (**KARAR D75/1**, K-W7 (a): rol izinsiz oluşturulur).
- **KARAR D75/1 — atayan rol üyeliği denetimi:** `CreatePaymentSodWaiverAssignmentHandler.cs` ve
  `RevokePaymentSodWaiverAssignmentHandler.cs` (yukarıda, **yeni**) izne ek olarak çağıranın kiracıdaki Muafiyet yetkilisi rol
  üyeliğini okur. Salt okuma tüketilen (değişmez; ölçüm 2026-10-28): `IRoleRepository.GetByNameAndTenantAsync`
  (`Application/Common/Interfaces/IRoleRepository.cs:8`; rol `IsSystem = true` olmalı — `Domain/Entities/Role.cs:20`) ve
  `IUserRoleRepository.ExistsAsync(userId, roleId, tenantId)` (`IUserRoleRepository.cs:10`; silinmemiş satır —
  `Persistence/Repositories/UserRoleRepository.cs:71-76`). Geri alma ucundaki üyelik denetimi **KARAR D76/1**'dir; okuma
  sorgusu (`GetPaymentSodWaiverAssignmentsForBatchHandler.cs`) üyelik okumaz (yalnız `.read` izni, D76/1).
- **KARAR D76/2 — denetim sırası:** izin ve rol üyeliği → kendine atama → alan doğrulama
  (`CreatePaymentSodWaiverAssignmentValidator.cs` ve alıcı varlığı). Ölçüm (2026-10-28): `ValidationBehavior`
  (`Application/Common/Behaviors/ValidationBehavior.cs`) MediatR boru hattına işleyiciden önce kayıtlıdır
  (`Application/DependencyInjection.cs:21`); üyelik ve kendine atama denetimi yalnız işleyicide kalırsa doğrulayıcı önce koşar ve
  sıra bozulur; istekteki `approvedByUserId` alanı model bağlamada reddedilirse o 400 de üyelikten önce döner. Bu yüzden iki
  denetim doğrulayıcıdan ve bu alan reddinden önce koşan bir yere konur; yerin seçimi (ör. komuta özel ön adım ya da
  doğrulayıcının yalnız sıra sonrası çağrılması) DL-5 başında MOD-0018 sahibiyle yapılır — paket yeri sabitlemez, sırayı
  sabitler (AC-29 sıra testi).

**DL-5'te Platform dosyası yok (ölçüm):** atama kaydı, uçlar ve denetim Auth içindedir; denetim önce-yaz kalıbıyla Auth
günlüğüne gider — **ayrı yazıcı tipi `IPaymentSodWaiverAuditWriter`** (KARAR D97/2 (a), D98; yukarıda). Gateway: **K-W4 KAPANDI — KARAR D79/5:**
kiracı uçlarının kök yolu `/api/roles/sod-waiver-assignments`; mevcut `/api/roles/{everything}`
(`gateway/Diten.ApiGateway/ocelot.json:2232`) altında kalır, Gateway değişmez, `integration-agent` görevi yoktur.

**Sözleşme dosyası (K-W6 KAPANDI — KARAR D79/6):** `docs/analysis/contracts/` altında DL-5 sorgu yanıtının **yeni**, sürümlü
sözleşme dosyası (alan adları Bölüm 8.5; hata kodları dahil). Dosya adı klasörün desenini izler (`*-semantics-vX.Y.Z.md` ya da
`*.openapi.yaml`; ad DL-5 başında yazılır). `…/SodWaivers/PaymentSodWaiverAssignmentContractTests.cs` ve MOD-0126 sahte istemcisi
bu dosyaya karşı yazılır (AC-34).

**K-W5 okuma yolu — KARAR D97/2 (Ali, Y-26) → paket metni KARAR D98** (ölçüm ct-05 `264863e80`; yol `c`'nin beş koşulu,
`.antigravity/rules/audit-trail-standard.md` §5.c; meta-denetim §7):

- **Ayrı yazıcı tipi (D97/2 (a)):** `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IPaymentSodWaiverAuditWriter.cs` — **yeni**;
  iki metot `WriteCreatedAsync(...)` ve `WriteRevokedAsync(...)` (yalnız bu iki komut çağırır; önce-yaz, çağıranın verdiği denetim
  kimliğiyle bir kez yazar). Uygulama `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PaymentSodWaiverAuditWriter.cs`
  — **yeni**; `authAuditLogs` koleksiyonuna `EntityType = "PaymentSodWaiverAssignment"` + `EntityId` ile yazar. `IAuthAuditService`
  (`IAuthAuditService.cs:18-19` `WriteOnceAsync`) **değişmez**; `IssueServiceIdentityTokenHandler.cs:123,151` ve
  `ServiceClientRegistryReconciler.cs:94,170` aynı yolu kullanmaya devam eder, İ5 istisnası (`Diten.AuthService.md:26`) kaymaz.
  DI kaydı `Persistence/DependencyInjection.cs` (`:109` deseni).
- **Nesne alanı (öneri 2):** `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/AuthAuditLog.cs` (`:3-21`) — yalnız
  ekleme `EntityType` (string?) ve `EntityId` (Guid?); eski kayıtlar boş kalır; kurucu değişmez, yeni alanlar yalnız yeni yazıcıda
  dolar. Dizin `Persistence/Configurations/MongoDbIndexConfigurations.cs` (`:83-89` yanına) `(TenantId, EntityType, EntityId,
  OccurredAt)`.
- **Okuma arayüzü (öneri 3, kural koşul 2):** `Application/Common/Interfaces/IPaymentSodWaiverAuditReader.cs` — **yeni**; tek
  üye `GetHistoryAsync(tenantId, assignmentId, ct)` (güncelleme / silme üyesi yok); uygulaması
  `Persistence/Repositories/PaymentSodWaiverAuditReader.cs` — **yeni** (yalnız `Find`).
- **Geçmiş sorgusu ve uç:** `…/Features/PaymentSodWaiverAssignments/Queries/GetPaymentSodWaiverAssignmentHistoryQuery.cs`,
  `…/Handlers/QueryHandlers/GetPaymentSodWaiverAssignmentHistoryHandler.cs` — **yeni**; liste ucu için
  `…/Queries/GetPaymentSodWaiverAssignmentsQuery.cs` + `…/Handlers/QueryHandlers/GetPaymentSodWaiverAssignmentsHandler.cs` —
  **yeni** (`paymentBatchId`siz biçim: kiracının bütün atamaları, sayfalı, en yeni önce; `paymentBatchId` almaz — parti biçimi
  mevcut `GetPaymentSodWaiverAssignmentsForBatchQuery`'dir, KARAR D99/4, D100/1). Uçlar `PaymentSodWaiverAssignmentsController.cs`'te
  (Bölüm 14.3).
- **Meta-denetim (öneri 5, D97/2 (b)):** `Application/Common/Interfaces/IPlatformAuditForwarder.cs` (`:9-21`) `PlatformAuditEvent`'e
  yalnız ekleme `bool IsMetaAudit = false`; `Infrastructure/Services/PlatformAuditForwarder.cs` (`:77-100`) yükte
  `IsMetaAudit` gönderir (Platform `AuditAppendRequest.IsMetaAudit`, `Diten.Platform.Application/Contracts/Audit/AuditAppendRequest.cs:27`
  — Platform dosyası **değişmez**). Bugün anahtar boşsa iletici yalnız uyarı yazıp döner (`PlatformAuditForwarder.cs:55-59`) —
  okuma satırı sessizce kaybolur, Platform / bağlantı hataları da yalnız uyarıyla yutulur (`:111-138`); bu yüzden geçmiş sorgusu
  okuma satırını sonucu dönen ayrı iletici üyesiyle (`TryForwardAsync`, yalnız ekleme — KARAR D99/1) gönderir ve teslim
  edilmedikçe (anahtar `PlatformService:InternalApiKey` boş — `Infrastructure/Settings/PlatformServiceOptions.cs:9` —, 2xx dışı,
  bağlantı hatası, zaman aşımı) 503 `SOD_WAIVER_HISTORY_AUDIT_UNAVAILABLE` döner, veri dönmez (AC-46). Üretimde anahtar zaten başlangıçta zorunludur (`Infrastructure/DependencyInjection.cs:226`, `:27`);
  kural yine de uçta sınanır (geliştirme / test ortamı ve yanlış yapılandırma).
- **Defter ve mimari test (öneri 6):** `tests/architecture/audit-ledger/Diten.AuthService.md` `## İzler`'e yeni satır
  `auth-sod-muafiyet-gecmisi | c | yazıcı | IPaymentSodWaiverAuditWriter.WriteCreatedAsync/IPaymentSodWaiverAuditWriter.WriteRevokedAsync` + beş koşul kanıt notu; `auth-gunlugu` (`:13`) `aday` kalır;
  `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs` `AcceptedTrails` (`:99`, Auth satırı `:103`
  yanına) yeni üçlü; `PinnedCounts` (`:61`, `Debt 18 / Exceptions 1 / K2Debt 11`) **değişmez** (iki yeni komut kabul edilmiş ize
  yazar, borç eklemez); `tests/architecture/audit-ledger/CT-DECISIONS.md` satırı (D97/2). Belirteç biçimi `Tip.Metot`, işleyicinin
  enjekte bağımlılık üzerinden çağrısıyla eşleşir (`AuditTrailStandard/AuditTrailMeasurement.cs:275-279`, `:362-377`).
- **Ekran (öneri 4, D97/2 (c)):** Roller sayfası (`frontend/Diten.Web/Views/Governance/Roles/Index.cshtml`, `_LayoutTenantShell` —
  kiracı tarafı, `:8`) — **yeni** kısmi görünüm `Views/Governance/Roles/_SodWaiverAssignments.cshtml` (salt-okunur liste +
  "Geçmiş" ekranı; yalnız `Perms.Has("auth.payment-sod-waivers.read")` iken çizilir — UAS-001); `Index.cshtml`'e kısmi
  görünüm satırı; JS `wwwroot/assets/js/Governance/Roles/sod-waivers.js` — **yeni**; `index.l10n.js` (`:5-14` anahtar listesi);
  `_IndexL10n.cshtml` köprüsü; Web vekili `Controllers/RolesController.cs` (`:17-18`) iki **yeni** GET eylemi
  (`sod-waiver-assignments`, `sod-waiver-assignments/{id:guid}/history`) Auth uçlarına iletir; yetki Auth'ta `[HasPermission]`
  (`RolesController.cs:13-16` deseni). Metin **7 dil** — `Resources/Views/Governance/Roles/RolesIndex.{en,tr,fr,es,zh,ar,ru}.resx`
  (bugün 38 giriş, 7 dil). Roller sayfasının sayfa yardımı varsa yeni bölüm için bir paragraf eklenir (DL-5 başında ölçülür).
- **Testler (yeni):** `…/SodWaivers/PaymentSodWaiverAuditWriterTests.cs` (iki metot, önce-yaz, aynı kimlikle ikinci çağrı yazmaz),
  `…/SodWaivers/PaymentSodWaiverHistoryTests.cs` (HTTP: kiracı 404, izinsiz 403, iletici anahtarı boş → 503, meta-denetim
  satırı), `…/SodWaivers/PaymentSodWaiverAuditMongoTests.cs` (gerçek Mongo: `EntityType`/`EntityId` dizini, okuma arayüzünde
  güncelleme / silme yolu yok), `frontend/Diten.Web.Tests` Roller ekranı testi (bölüm izinsiz çizilmez, 7 dil kaynak eşliği).

### 5.4 Auth — DL-6 (onaylayıcı aday sorgusu, D66/1)

Golden Reference arka uç klasör düzeni (Bölüm 10); satır numaraları `chore/ct/treasury-intake-ct-05` @ `b9396b0e6` ölçümü:

- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/PermissionCandidates/Queries/GetPermissionCandidatesQuery.cs` — **yeni**
- `…/Features/PermissionCandidates/Handlers/QueryHandlers/GetPermissionCandidatesHandler.cs` — **yeni**
- `…/Features/PermissionCandidates/Validators/GetPermissionCandidatesValidator.cs` — **yeni**
- `…/Features/PermissionCandidates/PermissionCandidatesModels.cs` — **yeni** (tek dosyada DTO'lar)
- `…/Features/PermissionCandidates/PermissionCandidateErrorCodes.cs` — **yeni** (desen `Features/Roles/RoleErrorCodes.cs`)
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IRolePermissionRepository.cs` — izin → rol
  ters araması (kiracı sınırlı, `GrantSource` ile); bugün yalnız rol → izin yönü var (`:7-8`, `:17`)
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RolePermissionRepository.cs` — uygulaması
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs` — `rolePermissions`
  için `(TenantId, PermissionId)` dizini (bugün yalnız `(RoleId, PermissionId, TenantId)` tekil ve `(RoleId, TenantId)`, `:162-168`)
- Salt okuma tüketilen (değişmez): `IUserRoleRepository.GetUserIdsByRoleAsync` (`IUserRoleRepository.cs:14`),
  `IUserRepository`, `TenantEffectivePermissionResolver` (`Application/Common/Authorization/TenantEffectivePermissionResolver.cs`;
  giriş / yenileme işleyicilerinin kullandığı — `LoginCommandHandler.cs:28`, `RefreshTokenCommandHandler.cs:26`)
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionCandidatesController.cs` — **yeni**; iç uç
  (kimlik Bölüm 8.7, **KARAR D74/1**; DL-5 iç ucuyla aynı desen)
- Testler (**yeni**): `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/PermissionCandidates/GetPermissionCandidatesHandlerTests.cs`,
  `…/PermissionCandidates/PermissionCandidatesMongoTests.cs` (sabit `diten_auth_candidates_itest`, `MONGO_TEST_URI`, 27017 yasak),
  `…/PermissionCandidates/PermissionCandidatesContractTests.cs` (tüketici sözleşmesi, sürümlü sözleşme dosyasına karşı — K-A1 CT önerisi),
  `…/PermissionCandidates/InternalPermissionCandidatesAuthTests.cs` (iç uç kimliği ve token'dan kiracı — AC-35; gerçek denetleyiciyle,
  desen `…/Permissions/InternalPermissionsControllerTests.cs`).
- Auth'tan Platform kapsam çözümüne bağlantı **yoktur** — **K-A2 KAPANDI — KARAR D79/7 (b):** Auth yalnız kiracı tabanını
  verir, şirket kapsamını tüketici kendi kapsam portuyla süzer (Platform'a yeni bağımlılık yok; XMC-001 Beste gerekmez).

**DL-6'da Platform dosyası yok (KARAR D79/7, K-A2 (b)):** sorgu Auth içindedir; Gateway değişmez (iç uç).

### 5.5 Web — rol görünen adları (K-7 KAPANDI — KARAR D79/9)

`RoleName` sabit koddur; görünen ad rol ekranlarında kod → kaynak eşlemesiyle **7 dilde** çıkar (en, tr, fr, es, zh, ar, ru;
Çekirdek kural 4 — rol ekranı kiracıya görünür). Eşlemenin yeri mevcut desendir (ölçüm 2026-10-29, `frontend/Diten.Web`;
FIX-ROLEPERMS-ROLENAME-L10N-STICKY): `SharedResource` anahtarı `RoleName_{RoleName}` (bugün yalnız `SuperAdmin`, `Admin`,
`Viewer`, `ReadOnly` — `Resources/SharedResource.en.resx:567-570`; yedi dilin hepsinde 4'er satır), Razor köprüsü `RoleNames`
sözlüğü (`window.L10n.RoleNames`), ekran `isSystem` rolde `L.RoleNames[role.name]`, yoksa saklı `displayName` gösterir
(`wwwroot/assets/js/Governance/RoleAssignments/index.js:37`, `…/UserRoleAssignments/index.js:99`). Rol adları birden fazla ekranda
göründüğü için kaynak `SharedResource`'tadır, ekran kaynağında değil (localization-standard §3 A). Dilime göre: roller 1–19
DL-1, 20–22 DL-2, 23–26 DL-3, 27 DL-5 anahtarlarını ekler.

- `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` — yedi dosyanın her birine 27 `RoleName_{RoleName}`
  satırı (değerler Bölüm 14.2 "Görünen adlar" tablosu; İngilizce dışı dilde değer en ile aynı olamaz — localization-standard §1
  "Placeholder YASAK").
- `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` (`RoleNames` sözlüğü `:152-158`) ve
  `frontend/Diten.Web/Views/Governance/UserRoleAssignments/_IndexL10n.cshtml` (`:32-38`) — 27 giriş eklenir.
- `frontend/Diten.Web/Views/Governance/Roles/_IndexL10n.cshtml` ve `frontend/Diten.Web/wwwroot/assets/js/Governance/Roles/index.js`
  — **ölçüm:** Roller ekranı bugün `RoleNames` köprüsünü kullanmaz, saklı `displayName`'i basar (`index.js:104, 107, 218`); aynı
  köprü ve `isSystem` kuralı eklenir (liste sütunu, hızlı görünüm başlığı).
- `frontend/Diten.Web.Tests/Governance/TreasuryRoleNameL10nGuardTests.cs` — **yeni**; beklenen rol adı kümesini üretim
  kaynağından türetir (`TreasuryEntitlementGrantProfile.cs` rol adları; desen
  `frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs` — elle yazılmış liste yok) ve AC-7'deki denetimleri koşar.

Saklı `Role.DisplayName` mevcut desende İngilizce kalır (ad tablosunun `en` sütunu); yalnız köprü anahtarı eksikse geri dönüş
olarak görünür — guard testi bu durumu kırmızı yapar. Rol **açıklaması** (`Role.Description`, Roller ekranı hızlı görünümü
`index.js:111`) D79/9'un kapsamında değildir (aşağıda Açık kararlar K-7 notu).

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint çalışma kitapları, DCP'ler, backlog ve ilgisiz yönetişim dosyaları.
- `execution/registries/module-id-registry.md` ve `execution/domains/platform-shared-services/domain-config.md` bu görevde (öneri raporda).
- Altı tüketici paket ve MOD-0018-FU9…FU22 dahil diğer bütün paketler; özellikle MOD-0126 (D64: değiştirilmez).
- Bölüm 5 dışındaki bütün Auth dosyaları: `DataSeeder.cs`, token üretimi / yenileme, kullanıcı-rol atama işleyicileri,
  `FullCatalogPermissionGrantService.cs` (değişmez — **KARAR D79/3**: bugünkü davranış), FU20 / FU22 / BL-592 profil üretim kodu, `appsettings*`, gizli bilgiler.
- `services/Diten.Platform/**` ve `services/Diten.MdmService/**` — Bölüm 5.2 dışında; Bölüm 5.2 de XMC-001 onayına kadar.
- `services/Diten.FinanceTreasuryService/**` (henüz yok; MOD-0126 DL-1'in), `gateway/**` (`ocelot.json` yalnız `integration-agent`),
  `frontend/**` — Bölüm 5.5'teki dosyalar dışında (KARAR D79/9; o dosyalarda yalnız rol görünen adı satırları).
- Katalog / hak eşitlemesi çalıştırma, rol üyeliği, kiracı verisi, Production / Staging.

## 7. Dependencies

| Bağımlılık | Durum (ölçüm 2026-10-28) | Etki |
|---|---|---|
| Master parent `MOD-0018` RBAC / ABAC; FU13 token / önbellek sınırlı bayatlık | var | haklar yalnız token yenileme / yeni girişle görünür |
| Modül manifestleri (anahtar beyanı): MOD-0126 DL-1 (dört finans paketi için tek yazar), MOD-0289 ve MOD-0217 MDM manifestleri | yok (servis yok / paket taslak) | DL-1/2/3 kodu manifestten **önce** dağıtılır (Bölüm 8 sırası); aksi hâlde Viewer tabanı `read` anahtarlarını her kiracıya kalıcı verir (BL-592 §7 (c), `post-deploy-steps.md:305`) |
| Modül kodu değerleri (K-1b) | yapı **KARAR D79/1**: paket başına bir kod (kiracı modül bazında alır); **değerler** tüketici paketlerin manifest adımında — bugün seçilmedi (MOD-0289 `:146` "SEC-002 modül kodu değeri seçilmedi (ek paketinde)") | profil, kiracı hakkı ve DL-4 çiftleri bu değere bağlı |
| Platform modül kataloğu / kiracı hakkı kaydı (Treasury modül kodları) | yok | hak olmadan profil hiçbir hak vermez (beklenen) |
| MOD-0217 onayı | `draft` | DL-3 kodu onaydan sonra (Y-6, Y-10/1) |
| XMC-001 — Beste (Platform), MDM sahibi | **verildi** — Beste, Platform dosyaları (Y-22 / D88/3); Ali, MDM istemcisi ve testleri (Y-26 / D97/1); finans tüketicisi Platform dosyaları için Beste'nin XMC-001'i açık (D92/1) | DL-4 dosyalarına yalnız verilen dosya kapsamında ve §8.3 sırasıyla dokunulur |
| D64 main'de | **var** — PR #276 (`3bb1ab838`; `git merge-base --is-ancestor 4d8a4050a 3bb1ab838` → 0) | DL-5 paket kapısı (D62/1) — kapandı |
| Auth K2 denetim yolu: önce-yaz `IAuthAuditService.WriteOnceAsync` | var (`IAuthAuditService.cs:18`; kullanım `IssueServiceIdentityTokenHandler.cs:123,151`) | DL-5 için uyulabilir yol |
| Auth denetim defteri: `auth-gunlugu` izi | **`aday`** — okuyan uç yok (`tests/architecture/audit-ledger/Diten.AuthService.md` `## İzler`) | **DL-5 ENGELİ:** yeni K2 komutu kabul edilmiş ize yazılmalı; CT defter kararı ya da okuyan uç gerekir (K-W5); DL-5 kodundan önce kapanması sahip şartıdır — **KARAR D80/2 (a)** (Y-19) **KARAR D97/2 (Ali, Y-26):** okuma yolu kabul — yeni iz `auth-sod-muafiyet-gecmisi` (belirteç: iki komut için ayrı yazıcı tipinin metodu), `AuthAuditLog` `EntityType` / `EntityId`, `GET /api/roles/sod-waiver-assignments/{id}/history`, Roller ekranında salt-okunur atama listesi + "Geçmiş", meta-denetim okuma satırı (iletici anahtarı yapılandırma şartı); DL-5 paket metni bu yolla yazıldı (**KARAR D98**, D99: Bölüm 5.3, 8.5, 14.3, 21; AC-44…AC-47); engel iki koşulla kalkar: metin CT-APR-02'den geçer **ve** MOD-0018 sahibi D79 sonrası değişiklikleri teyit eder (D98/6). |
| FinanceTreasuryService → Auth iç uç kimliği (DL-5 iç ucu; **KARAR D74/1**) | servis yok; Auth'ta ortak sır deseni var (`X-Internal-Api-Key`, `IInternalEventAuthService.IsAuthorized` — `InternalEventAuthService.cs:16-24`; kullanım `InternalPermissionsController.cs:21, 49-52`, `InternalRolesController.cs:11, 30-33`; yapılandırma `InternalEventAuth:ApiKey`, `Infrastructure/DependencyInjection.cs:225`); kullanıcı token'ı doğrulaması var (JwtBearer varsayılan şema, `Infrastructure/DependencyInjection.cs:42-56`; `Program.cs:215`). Servis kimliği token'ı (`InternalServiceIdentityTokensController.cs`) **kullanılmaz (D74/1)** | Bölüm 8.7; çağıran servis ortak sırrı ve kullanıcı token'ını iletir (tüketici paketin işi) |
| MOD-0126 tüketici sözleşmesi | paket metninde: port `IPaymentSodWaiverAssignmentSource`, okunan alanlar (bitiş, başlangıç, onaylayan, gerekçe kodu, hedef parti), aday sorgusu (MOD-0126 `:1253-1259`, AC-42, AC-43) | DL-5 yanıt biçimi bununla birebir uyumlu (Bölüm 8.5) |
| D66 main'de | **var** — PR #281 (`c32cabe6b`; `git merge-base --is-ancestor 3c98e7bfc c32cabe6b` → 0) | DL-6 paket kapısı (D62/1) — kapandı |
| D70 main'de | **var** — PR #283 (main `9e6ec3138`; ölçüm 2026-10-29) | K-4 / K-A4 kapanışının (DL-1, DL-3, DL-6) ve D70/3 kapısının paket kapısı (D62/1) |
| D71 main'de | **var** — PR #283 (main `9e6ec3138`; ölçüm 2026-10-29) | K-A4b kapanışının (DL-6; tüketici aday kesişimi) paket kapısı (D62/1) |
| D72 main'de | **var** — PR #286 (main `9e6ec3138`; ölçüm 2026-10-29) | D72/3–4 bağımlı anahtarlarının (DL-1, DL-2) paket kapısı (D62/1) |
| MOD-0140 anahtarları `procurement.suppliers.eligibility.read` / `procurement.suppliers.read` (D72/3–4) | `procurement.suppliers.read` Procurement manifestinde var (ölçüldü: `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/ModuleRegistration/SupplierOnboardingManifestProvider.cs:30`); `eligibility.read` kodda yok (ölçüldü: `services/**` 0 eşleşme), MOD-0140 Revizyon 5 §14'te tanımlı (main'de `ready-for-dev`, sha256 `b3259e40…ce5c2`), Auth kataloğu / seed kaydı MOD-0140 §18 "DL-3 canlı öncesi" maddesinde (sahip Auth; açık) | bağımlı anahtar olarak beyan edilir, profil verir (K-4 (b)); katalogda henüz olmayan bağımlı anahtarın profilde nasıl davrandığı K-4 ölçüm notuyla birlikte ölçülür |
| Aday sorgusu tüketicileri (D66/1): MOD-0126 DL-2, MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3, MOD-0217 DL-3 | paket metinlerinde: aday tabanı bu paketin DL-6'sı, tüketici kendi kurallarını uygular, `CandidatePrincipalIds` (Y-12); izin anahtarları Bölüm 8.6 tablosu; MOD-0217 anahtarı `mdm.contracts.versions.approve` (KARAR D69; MOD-0217 O-3 (b) kapandı) | DL-6 kodu tüketici dilimlerinin kabul kapısı (Teslim dilimleri) |
| FinanceTreasuryService ve MdmService → Auth iç uç kimliği (DL-6 iç ucu; **KARAR D74/1**) | FinanceTreasury servisi yok; MdmService var; desen DL-5 satırıyla aynı (ortak sır + iletilen kullanıcı token'ı); servis kimliği token'ı **kullanılmaz (D74/1)** | Bölüm 8.7 |
| D74 main'de | **var** — PR #286 (main `9e6ec3138`; ölçüm 2026-10-29) | K-W1 / K-W3 / K-A1 kapanışının, 27. rolün ve 10 açık atama sayımının (DL-1…DL-3, DL-5, DL-6) paket kapısı (D62/1) |
| D75 main'de | **var** — PR #286 (main `9e6ec3138`; ölçüm 2026-10-29) | K-W7 kapanışının (27. rolün oluşturulma yolu, atayanın izin + rol üyeliği denetimi — DL-5) ve Bölüm 8.7 sınır kabulünün (DL-5, DL-6) paket kapısı (D62/1) |
| D76 main'de | **var** — PR #286 (main `9e6ec3138`; ölçüm 2026-10-29) | geri alma ucundaki üyelik denetiminin, okuma ucunun yalnız `.read` istemesinin (D76/1) ve denetim sırasının (D76/2) — DL-5 — paket kapısı (D62/1) |
| D79 main'de | değil (yerel commit `9897c4e04`, dal `chore/ct/treasury-intake-ct-05`; main'de değil; ölçüm 2026-10-29, D80 revizyonu) | K-1b, K-2, K-3, K-7 (DL-1…DL-3, DL-5 rol adı), K-W2, K-W4, K-W6 (DL-5), K-A2, K-A3 (DL-6) kapanışlarının ve 27 rolün 7 dilli görünen adının (Bölüm 5.5, 14.2) paket kapısı (D62/1) |
| D80 main'de | değil (yerel commit `728409ca3`, dal `chore/ct/treasury-intake-ct-05`; main'de değil; ölçüm 2026-10-29) | K-1 kapanışının (FU27, D80/1), Auth paket kapısının şartlı teyidinin (D80/2) ve K-7 / DL-6 sahip teyidinin (D80/3) paket kapısı (D62/1) |
| Auth denetim defteri: `auth-rbac-gunlugu` izi (rol / izin değişiklikleri) | **`aday`** — okuyan uç yok (`tests/architecture/audit-ledger/Diten.AuthService.md` `## İzler`) | **DL-5 kodu önkoşulu — KARAR D80/2 (b):** rol atama adımlarının (rola izin bağlama, kişiye rol verme) denetim borcu DL-5 kodundan önce kapanmalı (sahip şartı, Y-19); kapanış yolu bu pakette seçilmedi |
| Kullanıcı → şirket kapsamı | Auth'ta yok (ölçüm: `services/Diten.AuthService/src`'de `LegalEntity` yalnız `EntitlementPermissionSyncService.cs` ve `ProductLegalEntityScopeEntitlementGrantProfile.cs`'te, ürün profili); Platform `IDataScopeResolver.ResolveAsync(tenantId, userId, moduleCode, featureCode, …)` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/IDataScopeResolver.cs:5-10`) | **K-A2 KAPANDI — KARAR D79/7 (b):** Auth bu bağımlılığı almaz; şirket kapsamını tüketici kendi kapsam portuyla süzer |

## 8. Runtime Constraints

### 8.1 Modül kodu ve profil seçimi (DL-1…DL-3)

- Her Treasury anahtarı tam bir modül koduna aittir. **K-1b KAPANDI — KARAR D79/1:** paket başına bir modül kodu — kiracı modül
  bazında alır (AP'yi AR'siz alabilir); kod değerleri tüketici paketlerin manifest adımında yazılır. Çapraz modüllü roller (Hazine yöneticisi: MOD-0126 + MOD-0124; Hazine konsolide görüntüleyici: dört finans modülü)
  her kaynak modülden ayrı `RolePermission` satırı alır; bir modülün hakkı düşünce yalnız o modülün satırları silinir.
- Profil bir modül kodunun **bütün** beyan kümesini sahiplenir: o modül kodu için genel kalan (generic residual) boştur. Beyan
  kümesi profildeki tam kümeye eşit değilse (eksik, fazla, bilinmeyen eylem, takma ad, joker) mutasyondan **önce** hata —
  eksik ve fazla anahtarlar adlarıyla yazılır (BL-592 FIX1 deseni).
- Mutasyon sırası (FU22 §8 deseni): normalleştir → profil alt kümelerini tespit et → beyan kümesini ve katalogu tam doğrula →
  26 Treasury rol adının hepsini (KARAR D75/1 ile 27. ad dahil) çakışma için önceden denetle (görünür bir sistem dışı aynı adlı rol → hiçbir rol / hak yazılmaz) →
  rolleri idempotent oluştur → hakları ver / gereksizleri geri al → yalnız eşleşen kiracı + kaynak modül satırlarına dokun;
  `Manual` / `System` / başka modül / başka kiracı satırları korunur.
- Hak eksik / kapalı / süresi dolmuş → eşleşen modül kaynaklı Treasury hakları bütün rollerden kalkar. Ulaşılamaz / bozuk /
  zaman aşımı / belirsiz hak durumu → ne verir ne geri alır; yeniden denenir. İptal belirteci yayılır.
- Hak yalnız rol hakkı yaratır; **kullanıcıya rol üyeliği otomatik değildir**.

### 8.2 Varsayılan roller

- Açık atama listesindeki 10 anahtar (8 Treasury + 2 Auth, **KARAR D74/2**) hiçbir otomatik yoldan (SuperAdmin tam katalog
  `FullCatalogPermissionGrantService`, modül eşitlemesi `:294`, varsayılan rol `DefaultRolePermissionTemplate.cs:181-183`)
  verilmez. `auth.payment-sod-waivers.read` `read` eylemlidir; varsayılan Viewer tabanına açık atama listesi sayesinde girmez
  (`:181-183`), ayrıca dışlama listesi gerekmez. 27. rol Muafiyet yetkilisinin iki anahtarı da bu listededir: rolü hiçbir
  otomatik yol doldurmaz, anahtarlar role yalnız yetkili kişinin açık (`GrantSource = Manual`) rol-izin atamasıyla bağlanır
  (`ExplicitGrantOnlyPermissions.cs:3-7`). Rolü Treasury hak profili izinsiz oluşturur (**KARAR D75/1**, K-W7 kapandı); izin
  ataması rol üyeliğinin yerini tutmaz — atayan ikisini de taşır (Bölüm 8.5).
- 117 anahtarın **hiçbiri** varsayılan Viewer tabanına girmez: 18 `read` eylemli anahtar `EntitlementOnlyViewerPermissions`'a
  eklenir (diğer 99 anahtar — D69'un `mdm.contracts.versions.approve`'si dahil, eylemi `approve` — `read` eylemli olmadığından o kurala zaten girmez).
- Modül hakkıyla Admin ve Viewer: **hiçbir Treasury anahtarı — K-2 KAPANDI — KARAR D79/2.** Matris 117 anahtarın hepsini bir
  role ya da açık atamaya yerleştirmiştir; Admin'e fazladan verilen her anahtar matris dışıdır.
- SuperAdmin (yalnız varsayılan kiracı, `FullCatalogPermissionGrantService.cs`) açık atama dışındaki anahtarları tam katalog
  yoluyla alır — FU20 / FU22'de de böyledir. **K-3 KAPANDI — KARAR D79/3:** bugünkü davranış korunur (açık atama dışı hepsi;
  varsayılan kiracı iş kiracısı değildir); `FullCatalogPermissionGrantService.cs` değişmez.

### 8.3 Dağıtım sırası (BL-592 §7 deseni)

1. Auth DL-1/2/3 kodu (açık atama + Viewer dışlaması + profil). 2. Tüketici manifesti (anahtarları ilk beyan eden adım).
3. (DL-4) Platform kodu, sonra Platform yapılandırması (`AllowedPairs`), sonra MDM istemcisi. Ters sıra: (2) Auth'tan önce
gelirse eski Auth `read` anahtarlarını her kiracının Viewer'ına verir ve geri almaz; (3)'te yapılandırma koddan önce gelirse
Platform MDM kimlik bilgisini tamamen reddeder (her güvenilir kapsam sorusu 403). Geri alma ters sırada.

### 8.4 SEC-002 eki (DL-4)

- Yalnız MDM tüketicisi (D56/1): MOD-0289 ve MOD-0217 anahtarlarıyla Platform güvenilir kapsam sorusu çalışır. Çift listesi
  K-5 (hangi anahtarlar şirket kapsamı sorar). `read-tenant-wide` anahtarları kapsam sormaz (kapsamı atlar), öneri: listede yok.
- Kapalı-başarısız davranış değişmez: bilinmeyen çift → 403; yanlış modül kodlu yapılandırma → kimlik bilgisi reddi;
  hata / boş liste / belirsizlik → erişim yok (Y-4/6).
- Finans servisinin kapsam yolu (`IFinanceTreasuryDataScopeSource`, Y-4/6) bugün Platform'da tüketici olarak yok; Platform
  ucu yalnız MDM kimlik bilgisini kabul eder (`TrustedLegalEntityScopeCredentialAuthenticator.cs:11`,
  `TrustedLegalEntityScopeCredentialOptions.cs:7`). **K-6 düzeltildi — "evet" (Y-23 / KARAR D92/1):** finans servisi ikinci tüketici olur (kendi kimliği, gizli anahtarı, çift listesi; MDM bağlaması değişmez). Eski not (Y-22 / D88/2, "hayır"): finans servisi Platform
  kapsam ucunu doğrudan kullanmaz; soranın kimliği yalnız MDM (`DITENMDMSERVICE`); dört finans paketi kendi kapsam yolunu yazar
  (O-19 / O-22 / O-14 / O-23 — yolun ayrıntısı Platform sahibine soruldu, D88/4).

### 8.5 `waive-sod` parti bağlı atama (DL-5, D54 + D64)

- Atama kaydı **kullanım yetkisidir, izin değildir**: MOD-0126 muafiyet ucu `[HasPermission("finance-treasury.payment-batches.waive-sod")]`
  ister (izin açık atamayla role verilir) **ve** Auth'tan o kullanıcı + o parti için geçerli atama okur. Bu ikili yapı
  **KARAR D79/4**'tür (**K-W2 KAPANDI**): izin açık atamayla süresizdir, her kullanım ayrıca geçerli atama ister; token
  üretimine dokunulmaz (izin token'da süreli yapılmaz).
- Geçerlilik sorgu anında hesaplanır: `StartsAtUtc ≤ şimdi < ExpiresAtUtc`, geri alınmamış, silinmemiş, `TargetPaymentBatchId`
  istenen partiye eşit ve boş değil. Bitişte arka plan işi yoktur; atama kendiliğinden geçersizleşir.
- Yazma: onaylayan (çağıran) atamayı başka bir kullanıcı için yaratır; `ApprovedByUserId` sunucudan; kendine atama yasak
  (403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN`). Atamayı onaylama yetkisi Auth anahtarı `auth.payment-sod-waivers.assign`'dır,
  okuma `auth.payment-sod-waivers.read` (**KARAR D74/2–3**; K-W1 kapandı); ikisi yalnız açık atamayla ve yalnız 27. rol
  **Muafiyet yetkilisi** (`PaymentSodWaiverAuthority`) üzerinden verilir. **Atayan denetimi izin + rol üyeliğidir (KARAR
  D75/1):** yaratma ucu (1) `[HasPermission("auth.payment-sod-waivers.assign")]` ister ve (2) işleyici, token kiracısında adı
  `PaymentSodWaiverAuthority` olan sistem rolünde (`IsSystem = true`) çağıranın silinmemiş `UserRole` satırını arar
  (Bölüm 5.3); ikisinden biri yoksa → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN`, kayıt ve denetim yok. İzin başka bir role elle
  bağlansa da rolsüz kişi atayamaz; rol kiracıda yoksa kimse atayamaz (kapalı-başarısız). Geri alma ucu aynı izni ve aynı
  üyelik denetimini kullanır (Bölüm 14.3 "aynı"; **KARAR D76/1**); kiracı okuma ucu (`GET …?paymentBatchId=`) yalnız
  `auth.payment-sod-waivers.read` izni ister, üyelik denetimi yoktur (**KARAR D76/1**). **Denetim sırası (KARAR D76/2):**
  (1) izin ve rol üyeliği → (2) kendine atama → (3) alan doğrulama (hedef parti, süre, gerekçe kodu, alıcının kiracıda varlığı,
  istekteki `approvedByUserId`); ilk başarısız adım yanıtı belirler. Rolsüz kişi kendini alıcı yazarsa → 403
  `SOD_WAIVER_ASSIGN_FORBIDDEN` (`SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN` değil); rolsüz kişinin geçersiz alanlı isteği → 403
  `SOD_WAIVER_ASSIGN_FORBIDDEN` (400 değil); rol üyesi kendini alıcı yazar ve bir alan da geçersizse → 403
  `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN` (400 değil). Yerleşim ölçümü Bölüm 5.3. Üyelik denetimi kayıttan ve
  denetim yazımından öncedir. "Hazine yöneticisi ya da K1" kuralı kalktı (D49/4'ün atayan kısmının yerine D74/3): Hazine
  yöneticisi rolü bu anahtarları taşımaz; atayanın kim olacağını rol üyeliği belirler.
- Onaylayanın partiyi oluşturan / gönderen / son güncelleyen olmaması Auth'ta denetlenemez (parti verisi Auth'ta yok); MOD-0126
  kullanım anında denetler (AC-43 (d)). Auth `ApprovedByUserId`'yi her yanıtta taşır.
- Sorgu yanıtı (MOD-0126 portu `IPaymentSodWaiverAssignmentSource` için; alan adları `docs/analysis/contracts/` altındaki
  sürümlü sözleşme dosyasında dondurulur, MOD-0126 sahte istemcisi ona karşı yazılır — **K-W6 KAPANDI — KARAR D79/6**, Bölüm 5.3):
  her öğe `assignmentId`, `granteeUserId`, `targetPaymentBatchId`, `startsAtUtc`, `expiresAtUtc`, `approvedByUserId`,
  `reasonCode`. `granteeUserId` süzgeci kullanım anı içindir (MOD-0126 AC-43). **Muafiyet aday listesi (D56/3) burada ayrıca
  tanımlanmaz (D66/1 — tek aday sorgusu):** DL-6 aday sorgusunun `waive-sod` + parti biçimidir (Bölüm 8.6) ve aynı geçerlilik
  kuralını bu kayıttan okur; süzgeçsiz biçim yalnız kiracı uçlarındaki K1 kullanım incelemesi içindir (Bölüm 14.3).
- Tüketici kuralı (MOD-0126'nın, burada yalnız sözleşme olarak): yanıt yok / okunamıyor / alan eksik / hedef parti uyuşmuyor →
  403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; Auth'un sorgusu bunu destekler: başka partiye bağlı atama **hiç dönmez**, hedef partisiz
  satır **hiç dönmez**.
- Kiracı: `TenantId` JWT'den (kiracı uçları) ya da iç uçta çağrıyı başlatan kullanıcının iletilen token'ından (**KARAR D74/1**,
  Bölüm 8.7); istekte (gövde, sorgu dizesi, `X-Tenant-Id`) kiracı okunmaz; başka kiracının atama kimliği ya da kullanıcısı
  (`granteeUserId`) → 404; başka kiracının partisi için sorgu boş döner (Auth partiyi doğrulamaz — opak).
- **Denetim izi ve geçmiş (K-W5 — KARAR D97/2, D98):** iki komut denetimi yeni iz `auth-sod-muafiyet-gecmisi`'ye yazar
  (`IPaymentSodWaiverAuditWriter.WriteCreatedAsync` / `.WriteRevokedAsync`; `EntityType = PaymentSodWaiverAssignment`, `EntityId` = atama). Geçmiş
  `GET /api/roles/sod-waiver-assignments/{id}/history` ile okunur: izin `auth.payment-sod-waivers.read` (kaydı gören izini de
  görür — yeni anahtar yok, açık atama listesi değişmez); kiracı JWT'den, başka kiracının ya da olmayan atamanın geçmişi 404;
  yanıt yalnız bu atamanın denetim satırları (olay, zaman, eylemi yapan, önce / sonra alanları), en eskiden yeniye. Her geçmiş
  okuması merkezi günlüğe bir okuma satırı (`IsMetaAudit = true`) iletir. **Teslim teyidi (KARAR D99/1 — K1 önerisi, MOD-0018
  sahibinin teyidine gider):** mevcut `ForwardAsync` en iyi çabadır ve Platform / bağlantı hatasını yalnız uyarıyla yutar
  (`PlatformAuditForwarder.cs:111-138`); geçmiş ucu bunun yerine sonucu dönen ayrı bir üye kullanır (`IPlatformAuditForwarder`'a yalnız
  ekleme, ör. `TryForwardAsync` → teslim edildi / edilmedi; mevcut çağıranlar değişmez). Satır teslim edilmedikçe (anahtar boş,
  Platform 2xx dışı, bağlantı hatası, zaman aşımı) uç veri döndürmez → 503 `SOD_WAIVER_HISTORY_AUDIT_UNAVAILABLE` (kapalı-başarısız
  okuma; D97/2 (b)'nin genişletilmesi). Liste ucu (aynı `GET /api/roles/sod-waiver-assignments`, `paymentBatchId`siz biçim, sayfalı — Bölüm 14.3 tek satır)
  aynı izinle Roller ekranındaki salt-okunur listeyi besler; ekran yazma eylemi sunmaz (atama yine API ile).

### 8.6 Onaylayıcı aday sorgusu (DL-6, KARAR D66/1)

- **Amaç.** MOD-0023 şablonları atamasızdır; onaylayıcıları başlatma isteğinin `CandidatePrincipalIds` alanı belirler (Y-12).
  Aday **tabanı** bu sorgudur: "bu izne sahip kullanıcılar". Tüketici sonuca kendi paketinin kurallarını uygular ve kalanı
  `CandidatePrincipalIds` olarak gönderir (D66/1). Auth SoD kuralı, eşik ya da kayıt durumu bilmez; yalnız tabanı verir.
- **Uç (ad CT önerisi, K-A1):** `GET /internal/permission-candidates?permissionKey={key}[&paymentBatchId={id}]` — iç uç,
  Gateway'den geçmez; kimlik **KARAR D74/1** (Bölüm 8.7): `X-Internal-Api-Key` ortak sırrı + çağrıyı başlatan kullanıcının
  iletilen token'ı; DL-5 iç ucuyla aynı desen. Çağıranlar FinanceTreasuryService ve MdmService (MOD-0217) — ortak sır çağıran
  servisi ayırt etmez (Bölüm 8.7 "Sınır", **kabul edildi — KARAR D75/2 (a)**). Sorgu yalnız kullanıcının başlattığı gönderimde
  çağrılır; tüketicinin yeniden denemesi (Reconcile dahil) niyetteki ilk listeyi kullanır, sorguyu yeniden çağırmaz (**KARAR
  D75/2 (b)**).
- **Kiracı.** `TenantId` iletilen kullanıcı token'ının `tenant_id` claim'inden (**KARAR D74/1**); istekte (gövde, sorgu dizesi,
  `X-Tenant-Id`) kiracı okunmaz. Başka kiracının kullanıcısı, rolü ya da hakkı hiç dönmez.
- **İzin sahipliği = token'a giren küme.** Kullanıcının kiracıdaki rolleri → rol hakları → kiracının modül hakkıyla etkin
  izin kümesi; hesap giriş / yenilemedekiyle aynıdır (`TenantEffectivePermissionResolver`). Anahtar katalogda yoksa ya da
  kiracının o modül hakkı yoksa kimse aday değildir.
- **Açık atama anahtarı** (`ExplicitGrantOnlyPermissions`, Bölüm 14.1): yalnız `GrantSource = Manual` rol hakkından gelen
  sahiplik sayılır (`Domain/Entities/GrantSource.cs:14-23`); `System` ya da `Module` kaynaklı bir satır (otomatik yoldan
  sızmış) sahiplik saymaz.
- **Yalnız etkin kullanıcı:** `IsActive = true` (`User.cs:31`), silinmemiş, davet beklemiyor (`IsInvitationPending() = false`,
  `:118`), platform hesabı değil (`IsPlatformAccount = false`, `:50`). **K-A3 KAPANDI — KARAR D79/8:** hizmet hesabı
  (`AccountKind = Service`, `:56`) aday değildir; geçici kilitli kullanıcı (`LockoutEnd` gelecekte, `:34`) aday kalır (kilit
  geçicidir, onay görevi bekleyebilir). Dışlanan yalnız `Service`'tir; varsayılan `AccountKind.Unknown` (sınıflanmamış hesap,
  `Domain/Enums/AccountKind.cs:20`) dışlanmaz — CT okuması, MOD-0018 sahibinin teyidinde.
- **Şirket kapsamı.** Aday yalnız kaydın şirketinde (ödeyen şirket / fatura / pozisyon / sözleşme şirketi) kapsamı olan
  kullanıcıdır. Auth'ta kullanıcı → şirket verisi yoktur; kapsam Platform `IDataScopeResolver`'dadır (Bölüm 7). **K-A2
  KAPANDI — KARAR D79/7 (b):** Auth yalnız kiracı tabanını verir; şirket kapsamını tüketici kendi kapsam portuyla süzer —
  FinanceTreasury `IFinanceTreasuryDataScopeSource`, MDM `IContractDataScopeSource`; Auth'a Platform bağımlılığı eklenmez. Sorgu
  `legalEntityId` almaz; sözleşme dosyası yanıtın şirket süzgeci taşımadığını yazar. Şirket kapsamı dışındaki kullanıcı
  `CandidatePrincipalIds`'e girmez (tüketicinin aday AC'si — AC-40).
- **Muafiyet adayları (D56/3 ile birleşik).** `permissionKey = finance-treasury.payment-batches.waive-sod` için `paymentBatchId`
  zorunludur; yanıt yalnız `waive-sod`'u açık atamayla tutan, etkin ve bu partiye geçerli DL-5 ataması (Bölüm 8.5 geçerlilik
  kuralı) olan kullanıcılardır. Bu biçim DL-5 kaydını okur: DL-5 kodu main'de değilken `waive-sod` sorgusu kapalı-başarısız
  yanıt verir (kod sözleşme dosyasında, K-A1) ve tüketici muafiyet adayı eklemez (MOD-0126 AC-42; muafiyet yolu kapalı — D54,
  D66/3). Başka anahtarda `paymentBatchId` → 400.
- **Tüketicilerin anahtarları** (Bölüm 14.2 ile ölçüldü): MOD-0126 `finance-treasury.payment-batches.approve` (tier-2 partide
  `approve` ∩ `finance-treasury.payment-batches.tier2-approve`, R4; muafiyette `waive-sod` + parti); MOD-0119
  `finance-treasury.ap-invoices.approve`; MOD-0120 `finance-treasury.receivable-adjustments.approve` (eşik üstü
  `approve` ∩ `finance-treasury.receivable-adjustments.tier2-approve`); MOD-0124 `finance-treasury.cash-positions.approve`; MOD-0217
  `mdm.contracts.versions.approve` (**KARAR D69**; rol "Sözleşme onaylayıcı", açık atama değil; MOD-0217 hazırlayanı dışlar — MOD-0217 AC-33). Sorgu tek anahtar alır; kesişimi (∩) tüketici servis iki ayrı çağrıyla alır (sahip denetimi 61466e504, P2) — tier-2
  yetkisi tek başına aday yapmaz, çünkü karar ucu temel `approve`'u da ister. **K-A4 KAPANDI (KARAR D70/1)**: iş akışı izni rol şablonuyla gelir (Bölüm 14.2; onaylayıcı rollerin hepsinde), sorgu ikinci anahtar almaz. **İş akışı izni kesişimi — K-A4b KAPANDI (KARAR D71):** her tüketici aday listesini `platform.workflow.tasks.approve` iznine sahip kullanıcılarla da kesiştirir — bu sorguya `permissionKey = platform.workflow.tasks.approve` ile **ayrı bir çağrı** yapar ve kesişimi kendisi alır (tier-2 kesişimiyle aynı desen; tüketici tabanları: MOD-0126 `approve ∩ platform.workflow.tasks.approve`, tier-2 partide `approve ∩ tier2-approve ∩ platform.workflow.tasks.approve`; MOD-0119 `ap-invoices.approve ∩ …`; MOD-0120 `receivable-adjustments.approve ∩ …`, eşik üstü `∩ tier2-approve ∩ …`; MOD-0124 `cash-positions.approve ∩ …`; MOD-0217 `mdm.contracts.versions.approve ∩ …`). Böylece modül onay anahtarı rol dışı elle (`GrantSource = Manual`) verilmiş ve `platform.workflow.tasks.approve` tutmayan kişi aday olmaz (MOD-0023 görevinde karar veremeyecek kişi aday olmaz). Sorgu tek anahtar almaya devam eder; Auth kesişim yapmaz. İş akışı izni çağrısı okunamazsa tüketicide gönderme kapalı-başarısızdır; kesişim boşsa tüketicinin 422 "uygun onaylayıcı yok" dalı (D68 / D70/2). Sorgu `platform.workflow.tasks.approve` için de yanıt vermelidir (Bölüm 12 `permissionKey` kuralı: PKS-001 biçimi ve katalogda tanımlı); bu anahtar tohum sahipli olduğundan (`SeedOwnedTenantScopeKeys`, K-4 ölçüm notu) "kiracının o modül hakkı" kuralının bu anahtarda nasıl uygulanacağı DL-6 başında ölçülür.
- **Yanıt:** `permissionKey`, `principalIds` (Guid listesi, tekil, sıralı); ad, e-posta, rol adı ya da başka kişi verisi yok.
  Boş liste hata değildir (200). Alan adları ve hata kodları sürümlü sözleşme dosyasında dondurulur (dosya K-A1'in açık
  parçası; yer ve biçim K-W6 deseni — **KARAR D79/6**: `docs/analysis/contracts/` altında sürümlü).
- **Tüketici kuralı (sözleşme olarak):** her hata yanıtı ve okunamayan gövde "okunamaz"dır → gönderme kapalı-başarısız; Auth'un
  hiçbir yanıtı "aday yok → kural atla" ya da "herkes aday" anlamına gelmez.
- **Okuma denetimi:** sorgu okumadır, ayrıca denetlenmez (Bölüm 21).

### 8.7 İç uç kimliği (DL-5 iç ucu, DL-6 — KARAR D74/1)

Ölçüm (2026-10-28, `chore/ct/treasury-intake-ct-05` @ `776c6fc54`, `services/Diten.AuthService/src/`):

- **Ortak sır (mevcut desen).** İstek `X-Internal-Api-Key` başlığını taşır; denetleyici işin başında
  `IInternalEventAuthService.IsAuthorized(Request.Headers["X-Internal-Api-Key"])` çağırır, başarısızsa 401
  `{ message = "internal authentication failed" }` döner (`Api/Controllers/InternalPermissionsController.cs:21, 49-52`;
  aynı desen `InternalRolesController.cs:11, 30-33`, `InternalUsersController.cs:33, 71`, `InternalESignTicketsController.cs:19,
  34-37`). Doğrulayıcı yapılandırılmış anahtar boşsa ya da başlık yoksa `false`, değilse sıralı (ordinal) eşitlik
  (`Infrastructure/Services/InternalEventAuthService.cs:16-24`); yapılandırma `InternalEventAuth:ApiKey`, gizli bilgi gereksinimi
  (`Infrastructure/DependencyInjection.cs:225`).
- **Kiracı token'dan (bu paketin farkı).** Mevcut iç uçlar kiracıyı istek gövdesinden alır (`InternalRolesController.cs:35`,
  `InternalESignTicketsController.cs:40`); D74/1 bu iki uçta bundan bilinçli ayrılır. Çağıran servis, çağrıyı başlatan
  kullanıcının bearer token'ını `Authorization` başlığıyla iletir; Auth onu mevcut JwtBearer doğrulamasıyla (varsayılan şema;
  issuer, audience, ömür, imza — `Infrastructure/DependencyInjection.cs:42-56`; `Program.cs:215` `UseAuthentication`) doğrular ve
  kiracıyı `User.FindFirst("tenant_id")` ile okur (Auth içi desen `TenantAuthController.cs:81`, `AuthController.cs:118`).
  `/internal/*` kiracı ara katmanından muaftır (`Infrastructure/Middleware/TenantResolutionMiddleware.cs:134-143`): ara katman
  kiracı çözmez, geliştirme kiracı atlaması (`:87-94`) da bu yolda çalışmaz — kiracı yalnız denetleyicide token'dan okunur.
  Gövde, sorgu dizesi ve `X-Tenant-Id` içindeki kiracı değeri okunmaz (yok sayılır).
- **Kapalı-başarısız.** Ortak sır yok / yanlış → 401 (token'a bakılmadan). Ortak sır doğru ama kullanıcı token'ı yok, geçersiz,
  süresi dolmuş ya da `tenant_id` yok / Guid değil → 401; işleyici çağrılmaz, hiçbir veri dönmez. Hiçbir durumda kiracı istekten
  ya da varsayılandan tamamlanmaz. Tüketicide her 401 "okunamaz"dır (Bölüm 8.5, 8.6 tüketici kuralı).
- **Yeni katman yok.** Yeni kimlik doğrulama şeması, politika, hedef kitle ya da servis kimliği token'ı eklenmez; servis kimliği
  token ucu (`Api/Controllers/Internal/InternalServiceIdentityTokensController.cs`) bu pakette **kullanılmaz (D74/1)**.
- **Kullanıcı izni.** İletilen token yalnız kiracıyı belirler; iç uç token'daki kullanıcıdan ayrıca izin istemez (ortak sır
  denetimi yetkidir; kiracı kullanıcıları uca Gateway'den ulaşamaz — `gateway/Diten.ApiGateway/ocelot.json`'da `/internal`
  rotası yok, ölçüldü).
- **Sınır — Kabul edildi — KARAR D75/2.** (a) **Çağıran ayırt edilmez:** ortak sır Auth'un bütün iç uçlarında aynıdır;
  "yalnız FinanceTreasuryService (DL-5) / FinanceTreasuryService ve MdmService (DL-6)" kod düzeyinde zorlanmaz, sırrı tutan her
  servis çağırabilir (ölçüm yukarıda; kabul edilen risk). (b) **Arka plan işleri iç uçları çağıramaz:** kullanıcı token'ı
  olmayan çağrı 401 alır (kapalı-başarısız). Bu yüzden onaylayıcı aday listesi (DL-6) yalnız kullanıcının başlattığı gönderimde
  hesaplanır; yeniden deneme (Reconcile'ın aynı anahtarla yeniden başlatması dahil) niyetteki ilk listeyi kullanır ve DL-6'yı
  yeniden çağırmaz (tüketici paketlerin kuralı). DL-5 iç ucu muafiyetin kullanım anında (kullanıcının onay isteğinde) okunur —
  her kullanım ayrıca geçerli atama ister, izin token'da süreli değildir (**KARAR D79/4**).
  Servis kimliği ileride gelirse iki sınır yeniden değerlendirilir (D75/2); o zamana kadar bu pakete servis kimliği eklenmez (D74/1).

## 9. Layout & Shell Contract

`shell: none`. Bu FU Razor görünümü, ön yüz rotası, DataTable ya da gezinme öğesi sahiplenmez. Hiçbir `.cshtml` dosyası
oluşturulmaz; yalnız Bölüm 5.5'teki mevcut üç `_IndexL10n.cshtml` köprüsüne rol görünen adı girişleri eklenir (**KARAR D79/9**)
— sayfa, yerleşim (layout) ya da kabuk değişmez, bu yüzden `Layout = "_LayoutPlatformAdmin"` / `Layout = "_LayoutTenantShell"`
yazılacak dosya yoktur. Atama ekranı ayrı paket kararıdır (Bölüm 11, 20). **DL-5 istisnası (KARAR D97/2, D98):** mevcut Roller sayfasına
(`_LayoutTenantShell`) salt-okunur muafiyet atamaları bölümü ve "Geçmiş" kısmi görünümü eklenir; yeni sayfa, rota ya da
kabuk değişikliği yoktur.

## 10. Backend File Convention

- DL-1…DL-3 CRUD değildir; CQRS klasörü açmaz. Profil `Application/Common/Services` altında tek, saf, statik eşlemedir (FU20 /
  FU22 / BL-592 deseni); kalıcılık yalnız mevcut `EntitlementPermissionSyncService` üzerinden.
- DL-5 Golden Reference arka uç düzenini birebir izler: `Features/PaymentSodWaiverAssignments/{Commands, Queries,
  Handlers/CommandHandlers, Handlers/QueryHandlers, Validators}` + tek `PaymentSodWaiverAssignmentsModels.cs`. Command / Query
  `sealed record`; işleyici `{Verb}{Module}Handler`, doğrulayıcı `{Verb}{Module}Validator` (Command / Query soneki yok).
  Yanıt `Response<T>`. Bir dosyada tek `public` tür.
- DL-6 aynı düzeni izler: `Features/PermissionCandidates/{Queries, Handlers/QueryHandlers, Validators}` + tek
  `PermissionCandidatesModels.cs`; `GetPermissionCandidatesQuery` `sealed record`, `GetPermissionCandidatesHandler`,
  `GetPermissionCandidatesValidator`. Komut yok.
- İptal belirteci depo sınırına kadar taşınır.

## 11. Frontend File Contract

Yeni ekran, sayfa ya da ön yüz rotası yoktur. Atama ekranı yok (v1'de atama API ile; kök yol **KARAR D79/5**). **DL-5 (KARAR
D97/2, D98):** mevcut Roller sayfasında salt-okunur muafiyet atamaları listesi + "Geçmiş" (dosyalar Bölüm 5.3); yalnız
`auth.payment-sod-waivers.read` iken çizilir (UAS-001), yazma düğmesi yok; metin 7 dil (sayfa kaynağı `RolesIndex.{lang}.resx`). Ön yüz işi
yalnız rol görünen adlarının yerelleştirilmesidir (**K-7 KAPANDI — KARAR D79/9**; dosya listesi Bölüm 5.5): rol ekranları
(Roller, Rol izinleri, Kullanıcı rolleri — `Views/Governance/{Roles, RoleAssignments, UserRoleAssignments}`) sistem rolünün
adını kod → kaynak eşlemesiyle gösterir.

**Kullanıcıya görünen metin — 7 dil (KARAR D79/9).** Rol ekranları kiracıya görünür; Çekirdek kural 4 gereği 7 dil (en, tr, fr,
es, zh, ar, ru) — önceki "Platform modülü, 2 dil" ifadesi kalktı. Görünen metin 27 rol şablonunun (26 Treasury + Muafiyet
yetkilisi, D74/3) görünen adıdır (Bölüm 14.2 "Görünen adlar" tablosu). Kod → kaynak eşlemesi:

- **Kod:** `Role.Name` = `RoleName` (İngilizce PascalCase, sabit; dilden bağımsız).
- **Kaynak anahtarı:** `RoleName_{RoleName}` (ör. `RoleName_TreasuryPaymentViewer`) — `Resources/SharedResource.{lang}.resx`;
  mevcut `RoleName_SuperAdmin` / `_Admin` / `_Viewer` / `_ReadOnly` deseni. Ekran kaynağına (`RolesIndex.{lang}.resx`) konmaz:
  aynı ad üç ekranda görünür (localization-standard §3 A).
- **Köprü:** her ekranın `_IndexL10n.cshtml`'indeki `RoleNames` sözlüğü (`window.L10n.RoleNames`); ekran yalnız `isSystem`
  rolde sözlüğe bakar, özel rolde kullanıcının yazdığı `DisplayName` aynen kalır.
- **Kural:** İngilizce dışı değer en ile aynı olamaz ("Placeholder YASAK", localization-standard §1); çeviri belirsizse teslim
  durur ve MOD-0018 sahibinden kurumsal metin istenir (aynı kural). Görünen ad yerinde ham kod (`TreasuryPaymentViewer`) ya da
saklı İngilizce ad görünmez; Roller ekranının ayrı "Ad" (kod) sütunu bugünkü gibi kodu gösterir.

Rol açıklaması D79/9'un kapsamında değildir (Açık kararlar K-7 notu). Hata yanıtları sabit kod taşır (`errorCodes`), metni ekran
eşler (`RoleErrorCodes.cs` deseni); bu FU yeni hata metni yazmaz.

## 12. Validation Rules

| Girdi / karar | Zorunlu | Kural | Hata |
|---|---|---|---|
| Modül beyan kümesi | Evet | Bölüm 14'teki modül kodu kümesine birebir eşit | Mutasyondan önce ret; eksik / fazla adlarıyla |
| Açık atama anahtarı | Evet | 10 anahtar (8 Treasury + 2 Auth, KARAR D74/2) hiçbir otomatik yolda yok; 8 Treasury anahtarı hiçbir rol şablonunda yok; 2 Auth anahtarı yalnız 27. rolde ve yalnız açık (`Manual`) atamayla | Herhangi bir otomatik hak → test kırmızı |
| Viewer tabanı | Evet | 117 anahtarın hiçbiri | Herhangi biri → test kırmızı |
| Admin / Viewer modül hakkıyla | Evet | Hiçbir Treasury anahtarı (**KARAR D79/2**, K-2) | Fazla hak → test kırmızı |
| Rol adları | Evet | 26 Treasury adı (+ 27. ad `PaymentSodWaiverAuthority`, D74/3; profil oluşturur — **KARAR D75/1**); aynı adlı sistem dışı rol → bütün profil mutasyonu durur | Rol ve hak yazılmaz |
| Rol görünen adı (**KARAR D79/9**, K-7) | Evet | Profildeki her `RoleName` için `RoleName_{RoleName}` anahtarı yedi `SharedResource` dosyasının hepsinde boş olmayan değerle; İngilizce dışı değer en ile aynı değil; üç `RoleNames` köprüsünde giriş var (Bölüm 5.5) | Eksik dil / boş / en kopyası / köprüde eksik → test kırmızı (AC-7) |
| `platform.document-repository.*` | Evet | Hiçbir rolde yok (D56/2) | Test kırmızı |
| DL-5 `TargetPaymentBatchId` | Evet | Boş Guid değil | 400 `SOD_WAIVER_TARGET_BATCH_REQUIRED` |
| DL-5 `GranteeUserId` | Evet | Aynı kiracıda var; çağıran değil | yok / başka kiracı → 404 `SOD_WAIVER_GRANTEE_NOT_FOUND`; kendine → 403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN` |
| DL-5 `StartsAtUtc` / `ExpiresAtUtc` | Evet | Bölüm 4; süre 0 < s ≤ 24 saat | 400 `SOD_WAIVER_DURATION_INVALID` |
| DL-5 `ReasonCode` | Evet | Kapalı liste (D49/4) | 400 `SOD_WAIVER_REASON_CODE_INVALID` |
| DL-5 onaylayan | Evet | Sunucu türetir; çağıran `auth.payment-sod-waivers.assign` iznini **ve** Muafiyet yetkilisi rol üyeliğini (token kiracısında `PaymentSodWaiverAuthority` sistem rolünde silinmemiş `UserRole`) birlikte taşır — iki ayrı denetim (KARAR D74/2–3, **KARAR D75/1**); alıcı çağıran olamaz | izni yok → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN`; izni var ama rol üyesi değil (izin başka role elle bağlanmış) → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN` (D75/1; geri almada da — **KARAR D76/1**); kendine → 403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN`; istekte `approvedByUserId` alanı → 400 (bilinmeyen alan). **Sıra (KARAR D76/2):** izin + üyelik → kendine atama → bu tablodaki alan kuralları; rolsüz + kendini alıcı → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN` |
| DL-5 sorgu partisi | Evet | Boş Guid değil | 400 (MOD-0126 bunu 403'e çevirir) |
| DL-6 `permissionKey` | Evet | PKS-001 biçimi ve katalogda tanımlı | 400 (kod sözleşme dosyasında — K-A1 CT önerisi) |
| DL-6 `paymentBatchId` | `waive-sod`'da evet, diğerlerinde yasak | Boş Guid değil | 400 |
| DL-5 / DL-6 iç uç çağıranı (**KARAR D74/1**, Bölüm 8.7) | Evet | `X-Internal-Api-Key` = yapılandırılmış ortak sır; `Authorization` başlığında çağrıyı başlatan kullanıcının geçerli token'ı (`tenant_id` Guid) | sır yok / yanlış → 401; token yok / geçersiz / süresi dolmuş / `tenant_id` yok → 401; hiçbir veri dönmez |
| DL-5 / DL-6 iç uç kiracısı | Evet | Yalnız token'ın `tenant_id`'si; gövde, sorgu dizesi ve `X-Tenant-Id` kiracısı yok sayılır | başka kiracının kaynağı (atama kimliği, `granteeUserId`) → 404; başka kiracının kullanıcısı aday yanıtında yok |

## 13. Failure Path to Verify

- **Beyan kümesi eksik / fazla:** profil hatası, sıfır rol / hak mutasyonu; eksik ve fazla anahtar adlarıyla.
- **Katalogda bir anahtar eksik / silinmiş / farklı:** rol oluşturma ve hak yok.
- **Rol adı çakışması** (26 Treasury adından ilki, ortadaki, sonuncusu; 27. ad `PaymentSodWaiverAuthority` için AC-43): sıfır yeni rol, sıfır hak; kiracının önceki durumu aynen.
- **Etkin hak:** Bölüm 14'teki tam matris; tekrar oynatma kopya üretmez.
- **Hak düştü / kapandı / süresi doldu:** eşleşen Treasury hakları bütün 26 rolden ve Admin / Viewer'dan kalkar; başka modül
  ve elle verilen haklar kalır.
- **Hak belirsiz:** ne verir ne geri alır, başarı bildirilmez.
- **Kiracılar arası:** A kiracısının eşitlemesi B'nin rol / hak durumunu yaratmaz, silmez, göstermez.
- **Manifest Auth'tan önce** (sıra ihlali sınaması, birim testte): yeni Auth koduyla `read` anahtarı hak olmadan hiçbir Viewer'a gitmez.
- **DL-4 bilinmeyen çift / yanlış modül kodu:** 403 / yapılandırma geçersiz; ürün çiftleri etkilenmez.
- **DL-5 hedef partisiz istek** → 400, kayıt yok, denetim yok; **hedef partisiz bozuk satır** (veritabanında elle) → sorgu
  döndürmez → MOD-0126 403.
- **DL-5 başka partiye bağlı atama** → B partisi sorgusunda dönmez → MOD-0126 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`.
- **DL-5 süre 24 saat + 1 sn** → 400; **bitiş geçmiş** → sorgu döndürmez.
- **DL-5 onaylayan = alıcı** → 403; **Muafiyet yetkilisi rolünü (`auth.payment-sod-waivers.assign`) taşımayan çağıran** → 403
  (KARAR D74/3); **`auth.payment-sod-waivers.assign` başka role elle bağlanmış, çağıran o rolde ama Muafiyet yetkilisi rol
  üyesi değil** → yaratma ve geri alma 403 `SOD_WAIVER_ASSIGN_FORBIDDEN`, kayıt ve denetim yok (KARAR D75/1, geri alma **KARAR
  D76/1**); **kiracıda Muafiyet yetkilisi rolü yok** → 403 (kapalı-başarısız); **başka kiracının kullanıcısı / ataması** → 404.
- **DL-5 denetim sırası (KARAR D76/2):** rolsüz kişi kendini alıcı yazar → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN` (kendine atama kodu
  değil); rolsüz kişi geçersiz alanla ister → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN` (400 değil); rol üyesi kendini alıcı yazar ve
  gerekçe kodu geçersiz → 403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN` (400 değil). **Okuma ucu (KARAR D76/1):** `.read` izni olan,
  Muafiyet yetkilisi rol üyesi olmayan kullanıcı → 200 (üyelik denetimi yok); izni olmayan → 403.
- **DL-5 / DL-6 iç uç:** ortak sır yok / yanlış → 401; kullanıcı token'ı yok / süresi dolmuş / `tenant_id` yok → 401 (KARAR D74/1);
  gövdede ya da `X-Tenant-Id`'de B kiracısı, token'da A → yanıt yalnız A'nın verisi (gövdedeki kiracı yok sayılır).
- **DL-5 denetim yazılamıyor** → komut başarısız, atama yazılmaz (K2, önce-yaz).
- **DL-5 eşzamanlı geri alma** → `Version` çakışması 409; sessiz üzerine yazma yok.
- **DL-6 pasif / silinmiş / davet bekleyen / platform hesabı / hizmet hesabı (`AccountKind = Service`)** izin sahibi → aday değil;
  **geçici kilitli** (`LockoutEnd` gelecekte) izin sahibi → aday (**KARAR D79/8**).
- **Rol görünen adı bir dilde yok / boş / en ile aynı / köprüde yok** → guard testi kırmızı (yakalanmasa ekran o dilde saklı
  İngilizce adı gösterirdi; **KARAR D79/9**, AC-7).
- **DL-6 açık atama anahtarı `Module` / `System` kaynaklı satırdan** → aday değil; yalnız `Manual` satır sayılır.
- **DL-6 kiracının modül hakkı yok** (rol hakkı duruyor) → aday değil (token'a girmeyen izin adaylık vermez).
- **DL-6 başka kiracının kullanıcısı** → yanıtta hiç yok.
- **DL-6 `waive-sod` başka partiye bağlı / süresi geçmiş / geri alınmış atama** → yanıtta yok; DL-5 kodu yokken → kapalı-başarısız.
- **DL-6 Auth ulaşılamıyor / hata** → tüketici gönderimi kapalı-başarısız (MOD-0023 çağrısı yok).
- **Yetkisiz aktör:** `platform_admin` geçişi kabul kanıtı değildir; testler doğrudan `tenant_user` ile koşar.

## 14. Authorization Convention

- Politika: tüketici uçlarında `[Authorize]` + her eylemde `[HasPermission("…")]` (tüketici paketlerin işi). Aktör `tenant_user`.
- Biçim PKS-001: küçük harf, noktalı, ≥ 3 parça, çok kelimede tire. Ad alanları `finance-treasury.*` (D11/4) ve `mdm.*`.
  Not: PKS-001 §4 ad alanı tablosunda `finance-treasury.*` satırı yok (öneri raporda; `.antigravity/**` korumalı).

### 14.1 Yalnız açık atama (`ExplicitGrantOnlyPermissions`) — 10 anahtar (8 Treasury + 2 Auth, KARAR D74/2)

| Anahtar | Dayanak | Not |
|---|---|---|
| `finance-treasury.payment-batches.waive-sod` | D47/1, D49/4, D54 | kullanım ayrıca DL-5 atamasını ister (Bölüm 8.5) |
| `finance-treasury.payment-batches.read-tenant-wide` | D14/1 | yalnız okuma |
| `finance-treasury.ap-invoices.read-tenant-wide` | D14/1 | yalnız okuma |
| `finance-treasury.receivable-invoices.read-tenant-wide` | D14/1 | yalnız okuma |
| `finance-treasury.cash-positions.read-tenant-wide` | D14/1 | yalnız okuma |
| `mdm.bank-accounts.read-tenant-wide` | D23/1 | yalnız okuma, maskeli |
| `mdm.bank-accounts.read-unmasked` | D23/4 | gerekçe kodu + değerden önce denetim (MOD-0289) |
| `mdm.contracts.read-tenant-wide` | D41/1 | yalnız okuma; DL-3 ile (MOD-0217 onayından sonra) |
| `auth.payment-sod-waivers.assign` | **D74/2–3** (K-W1 kapandı) | Auth anahtarı (MOD-0018; Treasury modül anahtarı değil); DL-5; muafiyet ataması yaratma / geri alma; yalnız 27. rol Muafiyet yetkilisi üzerinden |
| `auth.payment-sod-waivers.read` | **D74/2–3** | Auth anahtarı; DL-5; atamaları okuma (K1 kullanım incelemesi); yalnız 27. rol üzerinden; `read` eylemli, Viewer tabanına açık atama listesi sayesinde girmez |

Koddaki liste bugün 10 anahtar (`ExplicitGrantOnlyPermissions.cs:89-94`, hiçbiri Treasury'nin değil) → bu ekle **20**.

### 14.2 Rol şablonları — 27 (26 Treasury rolü: D47 rol matrisi, D49 ve D69 ile güncel; + Muafiyet yetkilisi, KARAR D74/3)

`RoleName` sistem rol adıdır: İngilizce PascalCase **sabit kod** (**K-7 KAPANDI — KARAR D79/9**; MOD-0018 sahibinin teyidine Auth
ekiyle gider); `tr` adı matristeki addır. Görünen ad yedi dilde aşağıdaki "Görünen adlar" tablosundadır (kod → kaynak eşlemesi
Bölüm 11). "(+X)" X'in bütün anahtarlarını
miras alır (D49/1). Sayı = rolün tam modül anahtarı sayısı (mekanik genişletme, Bölüm 19); bağımlı anahtarlar sayıya girmez
(tablonun altında).

**Finans — 19 rol (DL-1):**

| # | RoleName | en | tr | Anahtarlar | Sayı |
|---|---|---|---|---|---|
| 1 | `TreasuryPaymentViewer` | Payment Viewer | Ödeme görüntüleyici | `finance-treasury.payment-batches.read`, `finance-treasury.payment-evidence.read`, `finance-treasury.payment-exceptions.read`, `finance-treasury.payment-trace.read` | 4 |
| 2 | `TreasuryApViewer` | AP Viewer | AP görüntüleyici | `finance-treasury.ap-invoices.read`, `finance-treasury.ap-invoice-trace.read` | 2 |
| 3 | `TreasuryArViewer` | AR Viewer | AR görüntüleyici | `finance-treasury.receivable-invoices.read`, `finance-treasury.receivable-trace.read` | 2 |
| 4 | `TreasuryCashPositionViewer` | Cash Position Viewer | Nakit pozisyonu görüntüleyici | `finance-treasury.cash-positions.read`, `finance-treasury.cash-position-threshold-policies.read` | 2 |
| 5 | `TreasuryConsolidatedViewer` | Treasury Consolidated Viewer | Hazine konsolide görüntüleyici | 1–4'ün birleşimi (10 anahtar); hiçbir rol miras almaz; yalnız gerekçeyle atanır | 10 |
| 6 | `TreasuryPaymentPreparer` | Payment Preparer | Ödeme hazırlayıcı | (+1) + `finance-treasury.payment-batches.create`, `.update`, `.delete`, `.validate`, `.submit`, `.reconcile`, `finance-treasury.payment-evidence.link`, `finance-treasury.payment-attestations.create` | 12 |
| 7 | `TreasuryPaymentApprover` | Payment Approver | Ödeme onaylayıcı | (+1) + `finance-treasury.payment-batches.approve`, `.reject`, `.request-evidence`, `.reconcile` | 8 |
| 8 | `TreasuryPaymentTier2Approver` | Payment Approver (Tier 2) | Ödeme onaylayıcı (2. kademe) | (+7) + `finance-treasury.payment-batches.tier2-approve` | 9 |
| 9 | `TreasuryPaymentReleaser` | Payment Releaser | Ödeme serbest bırakıcı | (+1) + `finance-treasury.payment-batches.release`, `.reconcile` | 6 |
| 10 | `TreasuryManager` | Treasury Manager | Hazine yöneticisi | (+1) (+4) + `finance-treasury.payment-batches.bulk-delete`, `finance-treasury.payment-batches.reconcile`, `finance-treasury.cash-position-threshold-policies.create`, `.update`, `.delete`, `.bulk-delete`, `finance-treasury.cash-positions.bulk-delete`, `finance-treasury.cash-positions.workflow-reconcile`, `finance-treasury.cash-positions.export-reconcile`, `finance-treasury.payment-sod-settings.read`, `finance-treasury.payment-sod-settings.update` (D57/4). **Yok:** approve, reject, request-evidence, release, tier2-approve, waive-sod, publish, AP / AR okuması (D47/1) | 17 |
| 11 | `TreasuryApOperator` | AP Operator | Borç (AP) operatörü | (+2) + `finance-treasury.ap-invoices.create`, `.update`, `.delete`, `.validate`, `.submit`, `.reconcile`, `finance-treasury.ap-invoice-exceptions.create`, `finance-treasury.ap-invoice-evidence.link` | 10 |
| 12 | `TreasuryApApprover` | AP Approver | Borç (AP) onaylayıcı | (+2) + `finance-treasury.ap-invoices.approve`, `.reject`, `.request-info`, `.reconcile` (D50/1) | 6 |
| 13 | `TreasuryApOperationsManager` | AP Operations Manager | AP operasyon yöneticisi | (+11) + `finance-treasury.ap-invoices.hold`, `.release-hold`, `.bulk-delete`, `finance-treasury.ap-invoice-exceptions.resolve`, `.reopen`. **Yok:** approve, reject, request-info (D49/3) | 15 |
| 14 | `TreasuryArOperator` | AR Operator | Alacak (AR) operatörü | (+3) + `finance-treasury.receivable-invoices.create`, `.update`, `.delete`, `.issue`, `.collection-manage`, `finance-treasury.receivable-payments.record`, `finance-treasury.receivable-adjustments.request`, `.reconcile` | 10 |
| 15 | `TreasuryArApprover` | AR Approver | Alacak (AR) onaylayıcı | (+3) + `finance-treasury.receivable-adjustments.approve`, `.reject`, `.reconcile` (D50/1) | 5 |
| 16 | `TreasuryArOperationsManager` | AR Operations Manager | AR operasyon yöneticisi | (+14) + `finance-treasury.receivable-invoices.bulk-delete`. **Yok:** adjustments approve / reject / tier2-approve | 11 |
| 17 | `TreasuryArTier2AdjustmentApprover` | AR Tier-2 Adjustment Approver | AR 2. kademe düzeltme onaylayıcı | (+15) + `finance-treasury.receivable-adjustments.tier2-approve` (D50/2; tek başına atanır) | 6 |
| 18 | `TreasuryCashPositionPreparer` | Cash Position Preparer | Nakit pozisyonu hazırlayıcı | (+4) + `finance-treasury.cash-positions.create`, `.update`, `.delete`, `.reconcile`, `.attach-evidence`, `.submit`, `.workflow-reconcile`, `.export-evidence`, `.export-reconcile` | 11 |
| 19 | `TreasuryCashPositionApprover` | Cash Position Approver | Nakit pozisyonu onaylayıcı | (+4) + `finance-treasury.cash-positions.approve`, `.reject`, `.publish`, `.workflow-reconcile` | 6 |

**Ana veri — 7 rol (DL-2: 20–22, DL-3: 23–26):**

| # | RoleName | en | tr | Anahtarlar | Sayı |
|---|---|---|---|---|---|
| 20 | `BankDataViewer` | Bank Data Viewer | Banka verisi görüntüleyici | `mdm.banks.read`, `mdm.bank-accounts.read`, `mdm.bank-account-purposes.read` | 3 |
| 21 | `BankDataManager` | Bank Data Manager | Banka verisi yöneticisi | (+20) + `mdm.banks.create`, `.update`, `.activate`, `.deactivate`, `.delete`, `.bulk-delete`, `mdm.bank-accounts.create`, `.update`, `.delete`, `.bulk-delete`, `mdm.bank-accounts.sensitive-reads.read`, `mdm.bank-account-purposes.create`, `.update`, `.delete`, `.activate`, `.deactivate`. **Yok:** `mdm.bank-accounts.activate` / `.deactivate` (D47/2) | 19 |
| 22 | `BankAccountActivator` | Bank Account Activator | Banka hesabı etkinleştirici | (+20) + `mdm.bank-accounts.activate`, `mdm.bank-accounts.deactivate` | 5 |
| 23 | `ContractViewer` | Contract Viewer | Sözleşme görüntüleyici | `mdm.contracts.read`, `mdm.contracts.documents.read` (belge deposu izni yok — D56/2) | 2 |
| 24 | `ContractManager` | Contract Manager | Sözleşme yöneticisi | (+23) + `mdm.contracts.create`, `.update`, `.delete`, `.bulk-delete`, `mdm.contracts.amendments.create`, `mdm.contracts.renewal-decisions.create`, `mdm.contracts.submit-approval`, `mdm.contracts.terminate`, `mdm.contracts.archive`, `mdm.contracts.value.read`, `mdm.contracts.value.write` (D49/2), `mdm.contracts.documents.create`, `mdm.contracts.documents.delete`. **Yok:** `mdm.contracts.record-signature` (D47/5) | 15 |
| 25 | `ContractSignatureRecorder` | Contract Signature Recorder | Sözleşme imza kaydedici | `mdm.contracts.record-signature`, `mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.documents.create` | 4 |
| 26 | `ContractVersionApprover` | Contract Version Approver | Sözleşme sürümü onaylayıcı | `mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.versions.approve` (**D69**; belge deposu izni yok — D56/2; açık atama değil). **Yok:** `mdm.contracts.submit-approval` ve yazma anahtarları | 3 |

**Auth — 1 rol (DL-5, KARAR D74/3):**

| # | RoleName | en | tr | Anahtarlar | Sayı |
|---|---|---|---|---|---|
| 27 | `PaymentSodWaiverAuthority` (KARAR D79/9) | Payment SoD Waiver Authority | Muafiyet atama yetkilisi | `auth.payment-sod-waivers.assign`, `auth.payment-sod-waivers.read` — ikisi de yalnız açık atama (Bölüm 14.1): rol hiçbir otomatik yoldan dolmaz, anahtarlar role açık (`Manual`) rol-izin atamasıyla bağlanır; rol kullanıcıya açık atamayla verilir. **Yok:** hiçbir `finance-treasury.*` / `mdm.*` anahtarı, `waive-sod`, bağımlı anahtar. Ödeme sürecinde rolü olan kişi kendine muafiyet veremez (kendine atama 403, D64 / D74/3). Rolü Treasury hak profili izinsiz oluşturur; atayan izin + bu rolün üyeliğini taşır (**KARAR D75/1**) | 0 (+ 2 Auth) |

Sayı sütunu Treasury modül anahtarı sayısıdır; iki Auth anahtarı 117'ye girmez (karar dosyası matris sayım kuralı: modül
anahtarı = altı Treasury paketinin anahtarları). Toplam: 117 modül anahtarı + 2 Auth anahtarı; 27 rol.

**Görünen adlar — 27 rol × 7 dil (KARAR D79/9; Treasury yazar, MOD-0018 sahibi teyit eder):** her satır
`SharedResource.{lang}.resx` içinde `RoleName_{RoleName}` anahtarının değeridir (Bölüm 5.5, 11). `en` ve `tr` yukarıdaki
tablolardan aynen alındı; fr, es, zh, ar ve ru Treasury'nin çevirisidir (sahip teyidinde düzeltilebilir; düzeltme bu tabloya ve
yedi kaynak dosyaya birlikte yazılır).

| # | Kod (`RoleName`) | en | tr | fr | es | zh | ar | ru |
|---|---|---|---|---|---|---|---|---|
| 1 | `TreasuryPaymentViewer` | Payment Viewer | Ödeme görüntüleyici | Lecteur des paiements | Visualizador de pagos | 付款查看者 | مشاهد المدفوعات | Наблюдатель платежей |
| 2 | `TreasuryApViewer` | AP Viewer | AP görüntüleyici | Lecteur des comptes fournisseurs | Visualizador de cuentas por pagar | 应付账款查看者 | مشاهد الحسابات الدائنة | Наблюдатель кредиторской задолженности |
| 3 | `TreasuryArViewer` | AR Viewer | AR görüntüleyici | Lecteur des comptes clients | Visualizador de cuentas por cobrar | 应收账款查看者 | مشاهد الحسابات المدينة | Наблюдатель дебиторской задолженности |
| 4 | `TreasuryCashPositionViewer` | Cash Position Viewer | Nakit pozisyonu görüntüleyici | Lecteur de la position de trésorerie | Visualizador de posición de caja | 现金头寸查看者 | مشاهد المركز النقدي | Наблюдатель денежной позиции |
| 5 | `TreasuryConsolidatedViewer` | Treasury Consolidated Viewer | Hazine konsolide görüntüleyici | Lecteur consolidé de la trésorerie | Visualizador consolidado de tesorería | 资金合并查看者 | مشاهد الخزينة الموحد | Сводный наблюдатель казначейства |
| 6 | `TreasuryPaymentPreparer` | Payment Preparer | Ödeme hazırlayıcı | Préparateur des paiements | Preparador de pagos | 付款编制人 | معد المدفوعات | Составитель платежей |
| 7 | `TreasuryPaymentApprover` | Payment Approver | Ödeme onaylayıcı | Approbateur des paiements | Aprobador de pagos | 付款审批人 | معتمد المدفوعات | Утверждающий платежи |
| 8 | `TreasuryPaymentTier2Approver` | Payment Approver (Tier 2) | Ödeme onaylayıcı (2. kademe) | Approbateur des paiements (niveau 2) | Aprobador de pagos (nivel 2) | 付款审批人（二级） | معتمد المدفوعات (المستوى 2) | Утверждающий платежи (уровень 2) |
| 9 | `TreasuryPaymentReleaser` | Payment Releaser | Ödeme serbest bırakıcı | Responsable de la libération des paiements | Liberador de pagos | 付款放行人 | مسؤول الإفراج عن المدفوعات | Ответственный за выпуск платежей |
| 10 | `TreasuryManager` | Treasury Manager | Hazine yöneticisi | Gestionnaire de la trésorerie | Gestor de tesorería | 资金经理 | مدير الخزينة | Менеджер казначейства |
| 11 | `TreasuryApOperator` | AP Operator | Borç (AP) operatörü | Opérateur comptes fournisseurs | Operador de cuentas por pagar | 应付账款操作员 | مشغل الحسابات الدائنة | Оператор кредиторской задолженности |
| 12 | `TreasuryApApprover` | AP Approver | Borç (AP) onaylayıcı | Approbateur comptes fournisseurs | Aprobador de cuentas por pagar | 应付账款审批人 | معتمد الحسابات الدائنة | Утверждающий по кредиторской задолженности |
| 13 | `TreasuryApOperationsManager` | AP Operations Manager | AP operasyon yöneticisi | Gestionnaire des opérations fournisseurs | Gestor de operaciones de cuentas por pagar | 应付账款运营经理 | مدير عمليات الحسابات الدائنة | Менеджер операций по кредиторской задолженности |
| 14 | `TreasuryArOperator` | AR Operator | Alacak (AR) operatörü | Opérateur comptes clients | Operador de cuentas por cobrar | 应收账款操作员 | مشغل الحسابات المدينة | Оператор дебиторской задолженности |
| 15 | `TreasuryArApprover` | AR Approver | Alacak (AR) onaylayıcı | Approbateur comptes clients | Aprobador de cuentas por cobrar | 应收账款审批人 | معتمد الحسابات المدينة | Утверждающий по дебиторской задолженности |
| 16 | `TreasuryArOperationsManager` | AR Operations Manager | AR operasyon yöneticisi | Gestionnaire des opérations clients | Gestor de operaciones de cuentas por cobrar | 应收账款运营经理 | مدير عمليات الحسابات المدينة | Менеджер операций по дебиторской задолженности |
| 17 | `TreasuryArTier2AdjustmentApprover` | AR Tier-2 Adjustment Approver | AR 2. kademe düzeltme onaylayıcı | Approbateur des ajustements clients (niveau 2) | Aprobador de ajustes de cuentas por cobrar (nivel 2) | 应收账款调整审批人（二级） | معتمد تسويات الحسابات المدينة (المستوى 2) | Утверждающий корректировки дебиторской задолженности (уровень 2) |
| 18 | `TreasuryCashPositionPreparer` | Cash Position Preparer | Nakit pozisyonu hazırlayıcı | Préparateur de la position de trésorerie | Preparador de posición de caja | 现金头寸编制人 | معد المركز النقدي | Составитель денежной позиции |
| 19 | `TreasuryCashPositionApprover` | Cash Position Approver | Nakit pozisyonu onaylayıcı | Approbateur de la position de trésorerie | Aprobador de posición de caja | 现金头寸审批人 | معتمد المركز النقدي | Утверждающий денежную позицию |
| 20 | `BankDataViewer` | Bank Data Viewer | Banka verisi görüntüleyici | Lecteur des données bancaires | Visualizador de datos bancarios | 银行数据查看者 | مشاهد البيانات المصرفية | Наблюдатель банковских данных |
| 21 | `BankDataManager` | Bank Data Manager | Banka verisi yöneticisi | Gestionnaire des données bancaires | Gestor de datos bancarios | 银行数据经理 | مدير البيانات المصرفية | Менеджер банковских данных |
| 22 | `BankAccountActivator` | Bank Account Activator | Banka hesabı etkinleştirici | Responsable de l'activation des comptes bancaires | Activador de cuentas bancarias | 银行账户激活人 | مسؤول تفعيل الحسابات المصرفية | Ответственный за активацию банковских счетов |
| 23 | `ContractViewer` | Contract Viewer | Sözleşme görüntüleyici | Lecteur des contrats | Visualizador de contratos | 合同查看者 | مشاهد العقود | Наблюдатель контрактов |
| 24 | `ContractManager` | Contract Manager | Sözleşme yöneticisi | Gestionnaire des contrats | Gestor de contratos | 合同经理 | مدير العقود | Менеджер контрактов |
| 25 | `ContractSignatureRecorder` | Contract Signature Recorder | Sözleşme imza kaydedici | Responsable de l'enregistrement des signatures de contrats | Registrador de firmas de contratos | 合同签署登记人 | مسجل توقيعات العقود | Регистратор подписей контрактов |
| 26 | `ContractVersionApprover` | Contract Version Approver | Sözleşme sürümü onaylayıcı | Approbateur des versions de contrat | Aprobador de versiones de contrato | 合同版本审批人 | معتمد إصدارات العقود | Утверждающий версии контрактов |
| 27 | `PaymentSodWaiverAuthority` | Payment SoD Waiver Authority | Muafiyet atama yetkilisi | Responsable des dérogations SoD (paiements) | Responsable de exenciones SoD (pagos) | 付款职责分离豁免授权人 | مسؤول إعفاءات فصل المهام (المدفوعات) | Уполномоченный по исключениям SoD (платежи) |

Satır sayısı 27 = Bölüm 14.2 rol sayısı; kod sütunu yukarıdaki üç tablonun `RoleName` sütunuyla birebir aynıdır. "SoD" fr, es
ve ru'da kısaltma olarak kalır (ürün terimi; en ile aynı olan tek parça budur, değerin tamamı değil). **KARAR D103 (Y-28):** MOD-0018 sahibi
tabloyu düzeltmelerle teyit etti. `tr`: rol 26 "Sözleşme sürümü onaylayıcı", rol 27 "Muafiyet atama yetkilisi"; "Manager" her dilde
tek terim (tr yöneticisi, fr Gestionnaire, es Gestor, zh 经理, ar مدير, ru Менеджер). Paket metnindeki kısa adlar ("Sözleşme onaylayıcı",
"Muafiyet yetkilisi") karar kayıtlarının adıdır; görünen ad bu tablodur. **ar, ru ve zh için anadil kontrolü yapılmadı (düşük güven,
sahip notu);** kontrol yapılınca düzeltme bu tabloya ve yedi kaynak dosyaya birlikte yazılır.

**Bağımlı anahtarlar (başka modülün; kayıt o sahipte; rollere Treasury manifestinde çapraz modül bağımlılığı olarak beyanla,
profil verir — K-4 (b), KARAR D70/1):** `mdm.legal-entities.read` (MOD-0220): 6–19 arasındaki
hazırlayan / operatör / karar veren / serbest bırakan rolleri, AP / AR operasyon yöneticileri, AR 2. kademe ve Hazine
yöneticisi (görüntüleyicilerde yok); `platform.workflow.instances.start` (MOD-0023): 6, 11, 13, 14, 16, 18, 24;
`platform.workflow.tasks.approve` / `.reject`: 7, 8, 12, 15, 17, 19, 26 — bütün onaylayıcı roller (**KARAR D70/1**; 26
Sözleşme onaylayıcı D70/1 ile eklendi);
`platform.workflow.tasks.request-info`: 7, 8, 12.
**KARAR D72/3–4 (MOD-0140'ı kullanıcı token'ıyla çağıran roller; MOD-0140 §14, S-2, S-4):**
`procurement.suppliers.eligibility.read` (MOD-0140): 11, 12, 13 (AP: Create / Update / Validate / Submit, Approve, ReleaseHold —
MOD-0119 Runtime Constraints (b)) ve 20, 21, 22 (banka: tedarikçi hesabı okumaları, yazmaları, etkinleştirme — MOD-0289
Authorization Convention); `procurement.suppliers.read` (MOD-0140): 11, 13 (AP görüntü adı `GET /api/suppliers/{id}` ve form
seçicisi — D72/4); `mdm.legal-entities.read` ayrıca 20, 21, 22 (BL-652 Option 2'ye kadar — MOD-0140 S-2; yoksa MOD-0140 403
`LEGAL_ENTITY_LOOKUP_FORBIDDEN`). Roller 1–10 ve 14–19 (görüntüleyiciler, ödeme, Hazine yöneticisi, AR, nakit) ve 23–26
(sözleşme) bu iki `procurement.*` anahtarını almaz — MOD-0140'ı çağırmazlar (AP görüntüleyici adı kayıttaki kopyadan okur;
MOD-0126 v1: O-14, KARAR D28). Bağımlı anahtar sayısı 7.

### 14.3 DL-5 Auth uçları ve izinleri

| Uç (kök yol **KARAR D79/5**, K-W4 kapandı) | Politika | İzin |
|---|---|---|
| `POST /api/roles/sod-waiver-assignments` | `[Authorize]` | `auth.payment-sod-waivers.assign` (**KARAR D74/2–3**; yalnız açık atama, 27. rol Muafiyet yetkilisi; rolü taşımayan 403, kendine atama 403) **+ işleyicide Muafiyet yetkilisi rol üyeliği** (**KARAR D75/1**; izni var ama rol üyesi değil → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN`) |
| `POST /api/roles/sod-waiver-assignments/{id}/revoke` | `[Authorize]` | `auth.payment-sod-waivers.assign` (aynı; rol üyeliği denetimi dahil — **KARAR D76/1**) |
| `GET /api/roles/sod-waiver-assignments[?paymentBatchId=][&page=&pageSize=]` — **tek uç, iki biçim (KARAR D98, D99/4)** | `[Authorize]` | `auth.payment-sod-waivers.read` (**KARAR D74/2–3**; 27. rol); yalnız izin, rol üyeliği denetimi yok (**KARAR D76/1**); kiracı JWT'den. **`paymentBatchId` verilirse:** bugünkü parti biçimi — yalnız geçerli atamalar, yanıt alanları ve sözleşme dosyası aynen (D79/6; `GetPaymentSodWaiverAssignmentsForBatchQuery`, K1 kullanım incelemesi). **Verilmezse:** Roller ekranı listesi — kiracının bütün atamaları (geçerli, süresi dolmuş, geri alınmış; durum alanıyla), sayfalı, en yeni önce (`GetPaymentSodWaiverAssignmentsQuery`). Denetleyici biçimi `paymentBatchId`'nin varlığına göre seçer; parti biçiminde `page` / `pageSize` verilirse 400 (iki biçim karıştırılmaz) |
| `GET /api/roles/sod-waiver-assignments/{id}/history` (**KARAR D97/2, D98**) | `[Authorize]` | `auth.payment-sod-waivers.read`; başka kiracı / yok → 404; iletici anahtarı boş → 503 `SOD_WAIVER_HISTORY_AUDIT_UNAVAILABLE`; her okuma bir meta-denetim satırı |
| `GET /internal/sod-waiver-assignments/payment-batches/{batchId}[?granteeUserId=]` | `X-Internal-Api-Key` + iletilen kullanıcı token'ı (**KARAR D74/1**, Bölüm 8.7); `[HasPermission]` yok (iç uç) | ortak sırrı tutan servis (amaçlanan çağıran FinanceTreasuryService; sır çağıranı ayırt etmez); kiracı token'dan |

### 14.4 DL-6 iç uç (D66/1)

| Uç (ad CT önerisi, K-A1) | Politika | İzin |
|---|---|---|
| `GET /internal/permission-candidates?permissionKey={key}[&paymentBatchId={id}]` | `X-Internal-Api-Key` + iletilen kullanıcı token'ı (**KARAR D74/1**, Bölüm 8.7; DL-5 iç ucuyla aynı) | ortak sırrı tutan servis (amaçlanan çağıranlar FinanceTreasuryService ve MdmService); kiracı token'dan; token'daki kullanıcıdan ayrıca izin istenmez (iç uç) |

## 15. Gateway / API Routing Decision

- DL-1…DL-4: Gateway değişikliği **gereksiz**; uç ya da yöntem eklenmez.
- DL-5: **K-W4 KAPANDI — KARAR D79/5:** kiracı uçlarının kök yolu `/api/roles/sod-waiver-assignments`; mevcut
  `/api/roles/{everything}` catch-all altında kalır (`gateway/Diten.ApiGateway/ocelot.json:2232`, aşağı akış Auth 5056), Gateway
  **değişmez**, `integration-agent` görevi yoktur. İç uç
  Gateway'den geçmez (servisten servise); kimlik **KARAR D74/1** (Bölüm 8.7) — Gateway'e kimlik ya da rota eklenmez
  (`ocelot.json`'da `/internal` rotası yok, ölçüldü).
- DL-6: iç uç, Gateway'den geçmez; Gateway **değişmez** (kimlik D74/1, DL-5 iç ucuyla aynı).
- Ön yüz yazma uçlarını çağırmaz; DL-5'te Roller sayfası Web vekili üzerinden yalnız liste ve geçmiş uçlarını okur (KARAR D98;
  `/api/roles/{everything}` altında, Gateway değişmez). Bölüm 5.5'in rol görünen adı işi yeni uç ya da rota istemez (mevcut `GET /api/roles`
  yanıtındaki `name` kodu kullanılır).

## 16. Acceptance Criteria

**DL-1 — finans onboarding'i (76 anahtar, 19 rol, 5 açık atama, iki SoD ayar anahtarı):**

- [ ] AC-1 (zorunlu) — `ExplicitGrantOnlyPermissions.Keys` beş finans anahtarını içerir (`waive-sod` ve dört `read-tenant-wide`);
  test: SuperAdmin tam katalog yolu, modül hakkı eşitlemesi ve varsayılan rol seçimi bu beş anahtardan hiçbirini vermez.
  Sabotaj: anahtar listeden çıkarılınca test kırmızıya döner (üç yolun her biri ayrı).
- [ ] AC-2 (zorunlu) — Modül hakkı olmayan kiracıda varsayılan Viewer ve Admin hiçbir `finance-treasury.*` anahtarı tutmaz
  (`DefaultRolePermissionTemplate.SelectFor` 76 anahtarlı katalogla). Sabotaj: 11 `read` eylemli finans anahtarından biri
  `EntitlementOnlyViewerPermissions`'tan çıkarılınca test kırmızı.
- [ ] AC-3 (zorunlu) — Etkin hakla 19 finans rolü Bölüm 14.2'deki kümeyi birebir alır (rol başına küme eşitliği, sayılar
  4/2/2/2/10/12/8/9/6/17/10/6/15/10/5/11/6/11/6); kullanıcı-rol satırı sıfır. Sabotaj: profilde bir role bir anahtar eklenince
  ya da çıkarılınca o rolün testi kırmızı. **Rol şablonu testi (KARAR D70/1, K-4 (b)):** altı finans onaylayıcı rolünün (7, 8, 12,
  15, 17, 19) her birinde `platform.workflow.tasks.approve` ve `.reject` bağımlı izinlerinin ikisi de vardır; onaylayıcı olmayan
  rollerde (1–6, 9–11, 13, 14, 16, 18) yoktur. Sabotaj: profilde bir onaylayıcı rolünden iki bağımlı izinden biri kaldırılınca o
  rolün testi kırmızıya döner. **Tedarikçi bağımlı izinleri (KARAR D72/3–4):** 11, 12, 13'ün her birinde
  `procurement.suppliers.eligibility.read` ve `mdm.legal-entities.read` vardır; `procurement.suppliers.read` yalnız 11 ve 13'tedir
  (12'de yoktur); 1–10 ve 14–19 iki `procurement.*` anahtarından hiçbirini taşımaz. Bağımlı anahtarlar modül anahtarı
  sayısına girmez (sayılar yukarıdaki gibi kalır; 117 değişmez). Sabotaj: profilde 11'den `procurement.suppliers.eligibility.read`
  ya da 13'ten `procurement.suppliers.read` kaldırılınca, ya da 2'ye (AP görüntüleyici) `procurement.suppliers.read` eklenince o
  rolün testi kırmızıya döner.
- [ ] AC-4 (zorunlu) — Görev ayrılığı yapı testi: hiçbir rol aynı nesne türünde hazırlama / oluşturma / gönderme eylemini onay /
  ret eylemiyle birlikte taşımaz (ödeme partisi, AP faturası, AR düzeltmesi, nakit pozisyonu); `TreasuryManager` approve,
  reject, request-evidence, release, tier2-approve, waive-sod, publish ve AP / AR okuması taşımaz. Sabotaj: `TreasuryManager`'a
  `payment-batches.approve` eklenince kırmızı.
- [ ] AC-5 (zorunlu) — Modül hakkıyla Admin ve Viewer hiçbir Treasury anahtarı almaz (**KARAR D79/2**, K-2: boş küme). Sabotaj: genel kalan Treasury
  modül kodu için yeniden açılınca (Admin'e tüm anahtarlar) kırmızı.
- [ ] AC-6 (zorunlu) — Beyan kümesi tam değilse (bir anahtar eksik, bir yabancı anahtar, bilinmeyen eylem, büyük harf) hiçbir
  rol / hak yazılmaz ve hata eksik / fazla anahtarları adıyla verir (dört ayrı test).
- [ ] AC-7 (zorunlu) — 26 ayrılmış Treasury rol adından (27. rol `PaymentSodWaiverAuthority` için AC-43) ilki, ortadaki ve sonuncusu (`ContractVersionApprover`, D69) için sistem dışı çakışma: sıfır rol, sıfır hak
  (parametreli test; FU22 yedi rol testinin deseni). **Görünen ad — 7 dil (KARAR D79/9, K-7):** 27 rolün 7 dilde görünen adı
  vardır; eksik dil → test kırmızı. `TreasuryRoleNameL10nGuardTests` (Bölüm 5.5) beklenen rol adı kümesini üretim profilinden
  (`TreasuryEntitlementGrantProfile`) türetir — elle yazılmış liste yok — ve her `RoleName` için şunları denetler: (i)
  `RoleName_{RoleName}` anahtarı yedi `SharedResource.{lang}.resx` dosyasının (en, tr, fr, es, zh, ar, ru) hepsinde vardır ve
  değeri boş değildir; (ii) İngilizce dışı altı dilde değer `en` değeriyle aynı değildir (localization-standard §1); (iii) üç
  `RoleNames` köprüsünde (Roller, Rol izinleri, Kullanıcı rolleri `_IndexL10n.cshtml`) giriş vardır; (iv) değerler Bölüm 14.2
  "Görünen adlar" tablosuyla eşittir. Kapsam dilimle büyür (DL-1 sonunda 19, DL-2 22, DL-3 26, DL-5 27 ad — profil o dilimde
  ne tutuyorsa); paketin tamamı teslim edildiğinde tam 27 ad × 7 dil. Sabotaj: bir rolün `RoleName_*` satırı tek bir dil
  dosyasından (ör. `SharedResource.ar.resx`) silinince, değeri `en` ile aynı yapılınca ya da bir köprüden girişi kaldırılınca
  test kırmızıya döner; profile anahtarı olmayan yeni bir rol eklenince de kırmızıya döner (liste üretimden geldiği için).
- [ ] AC-8 (zorunlu) — Hak düştü / kapandı / süresi doldu: eşleşen Treasury modül hakları bütün rollerden kalkar; `Manual`,
  `System`, başka modül ve başka kiracı satırları kalır; çapraz modüllü rollerde (`TreasuryManager`,
  `TreasuryConsolidatedViewer`) yalnız düşen modülün satırları kalkar.
- [ ] AC-9 (zorunlu) — Belirsiz hak durumu ne verir ne geri alır; tekrar oynatma idempotenttir; A / B kiracı yalıtımı.
- [ ] AC-10 (zorunlu) — `finance-treasury.payment-sod-settings.read` / `.update` yalnız `TreasuryManager`'da; açık atama
  listesinde değil (D57/4).
- [ ] AC-11 (zorunlu) — Gerçek Mongo testi (`diten_auth_treasury_itest`, `MONGO_TEST_URI`, 27017 değil): rol / hak kardinalitesi,
  tekrar oynatma, geri al / geri yükle, kaynak koruma, kiracı yalıtımı, sıfır otomatik `userRoles`; sahte / bellek içi yok, atlanmaz.
- [ ] AC-12 (zorunlu) — FU20 ABB (`1/1/3/5/3/2`), FU22, BL-592 ve ürün Admin / Viewer matrisleri değişmez (gerileme koşusu, sayılar raporda).

**DL-2 — banka onboarding'i (MOD-0289: 23 anahtar, 3 rol, 2 açık atama):**

- [ ] AC-13 (zorunlu) — `mdm.bank-accounts.read-tenant-wide` ve `mdm.bank-accounts.read-unmasked` açık atama listesinde; AC-1'in
  üç yol testi bu iki anahtar için. Sabotaj AC-1 gibi.
- [ ] AC-14 (zorunlu) — Roller 20–22 Bölüm 14.2'deki kümeyi birebir alır (3/19/5); `BankDataManager`'da
  `mdm.bank-accounts.activate` / `.deactivate` yok (D47/2). Sabotaj: `BankDataManager`'a `activate` eklenince kırmızı.
  **Tedarikçi bağımlı izinleri (KARAR D72/3):** 20, 21, 22'nin her birinde `procurement.suppliers.eligibility.read` ve
  `mdm.legal-entities.read` bağımlı izinleri vardır; hiçbirinde `procurement.suppliers.read` yoktur; modül anahtarı sayıları
  (3/19/5) değişmez. Sabotaj: profilde 20'den (`BankDataViewer`) `procurement.suppliers.eligibility.read` ya da
  `mdm.legal-entities.read` kaldırılınca, ya da 21'e `procurement.suppliers.read` eklenince o rolün testi kırmızıya döner.
- [ ] AC-15 (zorunlu) — Hiçbir finans rolü (1–19) `mdm.banks.read` ya da `mdm.bank-accounts.read` taşımaz (D47/4).
- [ ] AC-16 (zorunlu) — Dört `read` eylemli MOD-0289 anahtarı (`mdm.banks.read`, `mdm.bank-accounts.read`,
  `mdm.bank-accounts.sensitive-reads.read`, `mdm.bank-account-purposes.read`) hak olmadan hiçbir Viewer'a gitmez. Sabotaj AC-2 gibi.

**DL-3 — sözleşme onboarding'i (MOD-0217: 18 anahtar, 4 rol, 1 açık atama — D69 dahil) — kodu MOD-0217 onayından sonra:**

- [ ] AC-17 (zorunlu) — `mdm.contracts.read-tenant-wide` açık atama listesinde; AC-1'in üç yol testi.
- [ ] AC-18 (zorunlu) — Roller 23–26 Bölüm 14.2'deki kümeyi birebir alır (2/15/4/3); `ContractManager`'da
  `mdm.contracts.record-signature` ve `mdm.contracts.versions.approve` yok (D47/5, D69). `mdm.contracts.versions.approve` yalnız `ContractVersionApprover`'dadır,
  açık atama listesinde değildir (D69: "açık atama değil"); `ContractVersionApprover` `mdm.contracts.submit-approval` ve yazma anahtarı
  taşımaz (aynı nesne türünde gönderme + onay yok — AC-4'ün yapı testi deseni, sözleşme sürümü için bu dilimde). Sabotaj: `ContractManager`'a
  `record-signature` ya da `mdm.contracts.versions.approve` eklenince, ya da `mdm.contracts.versions.approve` açık atama listesine konunca kırmızı.
  **Rol şablonu testi (KARAR D70/1, K-4 (b)):** `ContractVersionApprover` `platform.workflow.tasks.approve` ve `.reject` bağımlı
  izinlerinin ikisini de taşır; 23–25 taşımaz. Sabotaj: profilde `ContractVersionApprover`'dan iki bağımlı izinden biri kaldırılınca
  test kırmızıya döner.
- [ ] AC-19 (zorunlu) — 26 Treasury rolünün hiçbiri (27. rol için AC-43; D69'un `ContractVersionApprover`'ı dahil) `platform.document-repository.read` ya da `.manage` taşımaz (D56/2); profil kümesinde
  `platform.document-repository.*` öneki yoktur. Sabotaj: `ContractViewer`'a `platform.document-repository.read` eklenince kırmızı.
- [ ] AC-20 (zorunlu) — Üç `read` eylemli MOD-0217 anahtarı (`mdm.contracts.read`, `.value.read`, `.documents.read`) hak olmadan
  hiçbir Viewer'a gitmez.
- [ ] AC-21 (zorunlu) — DL-3 kodu yalnız MOD-0217 `approved` / `ready-for-dev` ve onay satırı main'de iken birleşir (PR kontrolü).

**DL-4 — SEC-002 eki (D56/1):**

- [ ] AC-22 (zorunlu) — Platform doğrulayıcısı K-5 listesindeki (modül kodu, anahtar) çiftlerini kabul eder; tavan yeni sayıya
  eşittir; ürün çiftleri (38) aynen geçerlidir; listede olmayan anahtar ve yanlış modül kodlu çift reddedilir (gerçek
  doğrulayıcıyla test). Sabotaj: `AllowsPair`'deki modül kodu denetimi kaldırılınca yanlış modül kodlu çift testi kırmızı.
- [ ] AC-23 (zorunlu) — MDM istemcisi MOD-0289 ve MOD-0217'nin kendi modül kodlarıyla (paket başına bir kod — **KARAR D79/1**, K-1b) istek gönderir; bilinmeyen modül kodu 400
  `LEGAL_ENTITY_SCOPE_REQUEST_INVALID`; yanıttaki `moduleCode` istenenden farklıysa kapalı-başarısız (mevcut davranış).
- [ ] AC-24 (zorunlu) — Uçtan uca (dev ortamı, iki şirket iki kullanıcı): MOD-0289 anahtarıyla kapsam sorusu yalnız kullanıcının
  şirketlerini döner; hata / boş liste → erişim yok (MOD-0289 AC-17'nin sağlayıcı yarısı).
- [ ] AC-25 (zorunlu) — `post-deploy-steps.md` adımı sırayı (Platform kodu → yapılandırma → MDM istemcisi), geri almayı ve kontrolü
  yazar; XMC-001 onay kaydı (Beste, MDM sahibi) PR açıklamasında.

**DL-5 — `waive-sod` parti bağlı atama (D54 + D64; MOD-0018 sahibinin kabul şartları):**

- [ ] AC-26 (zorunlu) — **Kiracı yalıtımı:** her atama `TenantId` taşır (kiracı uçlarında JWT'den, iç uçta iletilen kullanıcı
  token'ından — **KARAR D74/1**; istekte yok); B kiracısının kullanıcısı için yaratma, B'nin atamasını geri alma ya da okuma → 404;
  B'nin partisi için sorgu boş. İç uçta (D74/1): token'ı A kiracılı çağrı, gövde / sorgu dizesi / `X-Tenant-Id` B'yi gösterse de
  yalnız A'nın atamalarını döner; `granteeUserId` B'nin kullanıcısıysa 404. Sabotaj: depo sorgusundan `TenantId` süzgeci
  kaldırılınca A / B testi kırmızı; iç uç kiracıyı token yerine istekten (gövde, sorgu dizesi ya da `X-Tenant-Id`) okuyacak
  şekilde değiştirilince "token A, istek B" testi kırmızıya döner.
- [ ] AC-27 (zorunlu) — **Hedef parti zorunlu:** hedef partisiz istek 400 `SOD_WAIVER_TARGET_BATCH_REQUIRED`, kayıt ve denetim
  yok; hedef partisiz bozuk satır (testte doğrudan yazılmış) sorguda dönmez; A partisine bağlı atama B partisi sorgusunda dönmez.
  Sözleşme testi (MOD-0126 portunun sahte tüketicisiyle): yanıt yok / bozuk / alan eksik / hedef uyuşmuyor / Auth ulaşılamıyor
  durumlarının her biri tüketicide 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` kararına gider (beş ayrı test). Sabotaj: sorgudan parti
  süzgeci kaldırılınca A→B testi kırmızı.
- [ ] AC-28 (zorunlu) — **En çok 24 saat:** süre = 24 saat kabul; 24 saat + 1 sn → 400 `SOD_WAIVER_DURATION_INVALID`; bitiş ≤
  başlangıç → 400; başlangıç 5 dk'dan eski → 400; bitişi geçmiş atama sorguda dönmez (sahte saat, `TimeProvider`). Sabotaj:
  doğrulayıcıdaki üst sınır kaldırılınca 24 saat + 1 sn testi kırmızı; sorgudaki `ExpiresAtUtc > şimdi` koşulu kaldırılınca
  süresi geçmiş testi kırmızı.
- [ ] AC-29 (zorunlu) — **Onaylayan ve gerekçe kodu zorunlu:** `ApprovedByUserId` = çağıran (sunucu türetir); istekte
  `approvedByUserId` → 400; çağıran = alıcı → 403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN`; gerekçe kodu yok
  ya da listede değil → 400 `SOD_WAIVER_REASON_CODE_INVALID`. Her yanıt öğesi `approvedByUserId` ve `reasonCode` taşır.
  **Atayan Muafiyet yetkilisi (KARAR D74/3):** atayan 27. rol `PaymentSodWaiverAuthority`'yi taşır ve
  `auth.payment-sod-waivers.assign`'ı o rolden açık atamayla tutar → yaratma ve geri alma başarılı; rolü taşımayan çağıran (Hazine
  yöneticisi rolündeki kullanıcı dahil — "Hazine yöneticisi ya da K1" kuralı kalktı) → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN`, kayıt ve
  denetim yok; rolü taşıyan kişi kendini alıcı yazarsa → 403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN`. **İzin + rol üyeliği
  (KARAR D75/1):** `auth.payment-sod-waivers.assign` testte başka bir role açık (`Manual`) atamayla bağlanır ve o rolü taşıyan,
  Muafiyet yetkilisi rol üyesi olmayan kullanıcı yaratma ve geri alma ister → ikisi de 403 `SOD_WAIVER_ASSIGN_FORBIDDEN`, kayıt
  ve denetim yok (izni var ama rol üyesi değil); aynı kullanıcı Muafiyet yetkilisi rolüne eklenince yaratma başarılı. Tersi:
  profilin izinsiz oluşturduğu Muafiyet yetkilisi rolünün üyesi, iki anahtar role henüz açık atamayla bağlanmamışken → 403
  `SOD_WAIVER_ASSIGN_FORBIDDEN` (rol üyesi ama izni yok). Sabotaj: kendine atama denetimi kaldırılınca kendine atama testi
  kırmızı; yaratma ucundan `[HasPermission("auth.payment-sod-waivers.assign")]` kaldırılınca "rol üyesi ama izni yok → 403"
  testi kırmızıya döner; işleyicideki rol üyeliği denetimi kaldırılınca "izni var ama rol üyesi değil → 403" testi kırmızıya
  döner. **Geri alma ve okuma (KARAR D76/1):** geri alma ucundaki üyelik denetimi kaldırılınca geri almadaki "izni var ama rol
  üyesi değil → 403" testi kırmızıya döner; kiracı okuma ucu (`GET …?paymentBatchId=`) `auth.payment-sod-waivers.read` izni olan,
  Muafiyet yetkilisi rol üyesi olmayan kullanıcıya 200 döner (okumaya üyelik denetimi eklenince bu test kırmızı). **Denetim
  sırası (KARAR D76/2):** parametreli sıra testi — (i) rolsüz (izni var, üye değil) kişi kendini alıcı yazar → 403
  `SOD_WAIVER_ASSIGN_FORBIDDEN` (`SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN` değil); (ii) rolsüz kişi geçersiz gerekçe kodu / süre /
  hedef parti ya da `approvedByUserId` alanıyla ister → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN` (400 değil); (iii) rol üyesi kendini
  alıcı yazar ve gerekçe kodu geçersiz → 403 `SOD_WAIVER_SELF_ASSIGNMENT_FORBIDDEN` (400 değil); (iv) rol üyesi başkasını alıcı
  yazar ve gerekçe kodu geçersiz → 400 `SOD_WAIVER_REASON_CODE_INVALID`; hepsinde kayıt ve denetim yok. Test gerçek MediatR boru
  hattıyla koşar (`ValidationBehavior` dahil; Bölüm 5.3 ölçümü). Sabotaj: sıra ters çevrilince (alan doğrulaması ya da kendine
  atama denetimi üyelikten önce, ya da alan doğrulaması kendine atamadan önce) (i)–(iii)'ten en az biri kırmızıya döner.
- [ ] AC-30 (zorunlu) — **Denetim kaydı (K2):** yaratma ve geri alma `auth.sod-waiver-assignment.created` / `.revoked` olayını
  iş yazımından **önce** ayrı yazıcı tipiyle (`IPaymentSodWaiverAuditWriter.WriteCreatedAsync` / `.WriteRevokedAsync` — KARAR D97/2 (a), D98) yazar; olay alıcı, hedef parti, başlangıç, bitiş, onaylayan ve gerekçe kodunu taşır;
  denetim yazımı başarısızsa atama yazılmaz ve komut başarısız döner. Sabotaj: denetim yazıcısı hata verecek şekilde
  ayarlandığında atama koleksiyonunda satır kalırsa test kırmızı.
- [ ] AC-31 (zorunlu) — Geri alınan atama sorguda dönmez; eşzamanlı iki geri alma → biri 409; fiziksel silme yok.
- [ ] AC-32 (zorunlu) — **İç sorgu ucu kimliği (KARAR D74/1, Bölüm 8.7):** ortak sır `X-Internal-Api-Key` yok ya da yanlış →
  401, işleyici çağrılmaz; sır doğru ama `Authorization`'da kullanıcı token'ı yok, geçersiz, süresi dolmuş ya da `tenant_id`
  claim'i yok → 401 (kapalı-başarısız, veri yok); gövde / sorgu dizesi / `X-Tenant-Id`'deki kiracı yok sayılır, kiracı yalnız
  token'dan; başka kiracının ataması / `granteeUserId`'si → 404. Servis kimliği token'ı ve yeni kimlik doğrulama şeması yoktur
  (D74/1). Sabotaj: ortak sır denetimi kaldırılınca "sır yok → 401" testi kırmızı; kiracı token yerine istekten okununca "token A,
  istek B" testi kırmızı; `tenant_id` yokken varsayılan / istek kiracısına düşülünce "kiracısız token → 401" testi kırmızıya döner.
- [ ] AC-33 (zorunlu) — Gerçek Mongo testi (`diten_auth_sodwaiver_itest`, `MONGO_TEST_URI`, 27017 değil): iki dizin, A / B kiracı,
  A→B parti, süre dolumu, geri alma.
- [ ] AC-34 (zorunlu) — **KARAR D79/6** (K-W6 kapandı; önceden isteğe bağlıydı). Yanıt biçimi `docs/analysis/contracts/` altında
  sürümlü sözleşme dosyasıyla dondurulur (Bölüm 5.3) ve MOD-0126 sahte istemcisi o dosyaya karşı yazılır; Auth sözleşme testi
  (`PaymentSodWaiverAssignmentContractTests`) yanıtı aynı dosyaya karşı denetler. Sabotaj: yanıttan bir alan (ör. `reasonCode`)
  çıkarılınca ya da adı değişince sözleşme testi kırmızıya döner.

**DL-6 — onaylayıcı aday sorgusu (KARAR D66/1; MOD-0018 sahibinin teyidi):**

- [ ] AC-35 (zorunlu) — **Kiracı ve iç uç (KARAR D74/1, Bölüm 8.7):** kiracı yalnız iletilen kullanıcı token'ının `tenant_id`'sinden
  gelir; A kiracılı token'la sorgu yalnız A'nın kullanıcılarını döner; B kiracısında aynı rol / izinle tanımlı kullanıcı A
  yanıtında yoktur (kiracılar arası erişim yok); sorgu dizesi ya da `X-Tenant-Id` B'yi gösterse de yanıt A'nındır. Ortak sır
  `X-Internal-Api-Key` yok / yanlış → 401; sır doğru, kullanıcı token'ı yok / geçersiz / süresi dolmuş / `tenant_id` yok → 401
  (kapalı-başarısız; boş liste dönmez). Servis kimliği token'ı kullanılmaz. Sabotaj: depo sorgusundan `TenantId` süzgeci
  kaldırılınca A / B testi kırmızıya döner; kiracı token yerine istekten okununca "token A, istek B" testi kırmızıya döner;
  ortak sır denetimi kaldırılınca "sır yok → 401" testi kırmızıya döner.
- [ ] AC-36 (zorunlu) — **İzin sahipliği token kümesiyle aynı:** rolü üzerinden `finance-treasury.payment-batches.approve` tutan
  kullanıcı aday; rolden hak geri alınınca aday değil; kiracının o modül hakkı yokken (rol hakkı duruyor) aday değil;
  katalogda olmayan anahtar 400. Sabotaj: işleyici `TenantEffectivePermissionResolver`'ı atlayıp ham rol hakkını sayınca
  "modül hakkı yok" testi kırmızıya döner.
- [ ] AC-37 (zorunlu) — **Açık atama anahtarında yalnız açık atama:** `waive-sod` `GrantSource = Manual` satırından → aday;
  aynı anahtar `Module` ya da `System` kaynaklı satırdan (testte doğrudan yazılmış) → aday değil. Sabotaj: `GrantSource`
  süzgeci kaldırılınca `Module` satırı testi kırmızıya döner.
- [ ] AC-38 (zorunlu) — **Yalnız etkin kullanıcı:** aynı izinle pasif (`IsActive = false`), silinmiş, daveti bekleyen ve
  platform hesabı kullanıcılar aday değildir (dört ayrı test). **KARAR D79/8 (K-A3):** hizmet hesabı (`AccountKind = Service`)
  aday değildir; geçici kilitli kullanıcı (`LockoutEnd` gelecekte) aday kalır (iki ayrı test). Sabotaj: etkinlik süzgeci
  kaldırılınca pasif kullanıcı testi kırmızıya döner; hizmet hesabı süzgeci kaldırılınca hizmet hesabı testi, kilit süzgeci
  eklenince geçici kilitli kullanıcı testi kırmızıya döner.
- [ ] AC-39 (zorunlu) — **Muafiyet biçimi (D56/3 ile birleşik):** `waive-sod` + A partisi sorgusu yalnız A'ya geçerli DL-5
  ataması olan kullanıcıları döner; B partisine bağlı, süresi geçmiş ya da geri alınmış atama dönmez; `paymentBatchId`'siz
  `waive-sod` sorgusu ve başka anahtarla `paymentBatchId` 400; DL-5 kaydı yokken (DL-5 kodu öncesi) sorgu kapalı-başarısız
  yanıt verir, boş liste vermez. Sabotaj: parti süzgeci kaldırılınca A→B testi kırmızıya döner. (DL-5 kodu gelmeden yalnız
  kapalı-başarısız dalı kanıtlanır; atama dalları DL-5 ile birlikte kapanır.)
- [ ] AC-40 (zorunlu) — **Şirket kapsamı (KARAR D79/7, K-A2 (b)):** sözleşme dosyası yanıtın şirket süzgeci taşımadığını
  yazar ve beş tüketici paketin aday AC'si şirket dışı izin sahibini dışlar (MOD-0126 AC-11, MOD-0119 AC-10, MOD-0120 AC-9,
  MOD-0124 AC-10, MOD-0217 AC-33). Auth sorgusu `legalEntityId` almaz ve Platform kapsam çözümüne bağlanmaz (Bölüm 5.4; Auth'a
  Platform bağımlılığı eklenmez). Sabotaj: tüketici paketlerin
  aday testlerinde kapsam süzgeci kaldırılınca şirket dışı izin sahibi testi kırmızıya döner (her paketin kendi AC'si).
- [ ] AC-41 (zorunlu) — Yanıt yalnız `permissionKey` ve `principalIds` taşır (ad, e-posta, rol adı yok — sözleşme testi K-A1
  dosyasına karşı); gerçek Mongo testi (`diten_auth_candidates_itest`, `MONGO_TEST_URI`, 27017 değil): `(TenantId,
  PermissionId)` dizini, A / B kiracı, rol hakkı geri al / geri yükle; sahte / bellek içi yok, atlanmaz.

**DL-5 eki — KARAR D74/2–3 (iki Auth anahtarı ve Muafiyet yetkilisi):**

- [ ] AC-42 (zorunlu) — **İki Auth anahtarı yalnız açık atama (D74/2):** `ExplicitGrantOnlyPermissions.Keys`
  `auth.payment-sod-waivers.assign` ve `auth.payment-sod-waivers.read`'i içerir; SuperAdmin tam katalog yolu, modül hakkı
  eşitlemesi ve varsayılan rol seçimi ikisinden hiçbirini vermez (`.read` `read` eylemli olduğu hâlde varsayılan Viewer'a
  gitmez). DL-1…DL-3 ve DL-5 birlikteyken `Keys` tam 20 öğedir (bugünkü 10 + Treasury eki 10); test beklenen kümeyi bu paketin
  Bölüm 14.1'inden alır ve üretim kümesiyle eşitler. Sabotaj: iki anahtardan biri listeden çıkarılınca o anahtarın üç yol
  testi kırmızı (`.read` için Viewer testi dahil).
- [ ] AC-43 (zorunlu) — **Muafiyet yetkilisi rol şablonu (D74/3):** 27. rol `PaymentSodWaiverAuthority` (7 dilli görünen adı
  Bölüm 14.2 "Görünen adlar" tablosu; DL-5 ile AC-7'nin görünen ad denetimine girer — **KARAR D79/9**) yalnız iki Auth anahtarını taşıyabilir; hiçbir otomatik yol (profil, modül eşitlemesi, varsayılan rol, SuperAdmin)
  role anahtar yazmaz — anahtarlar yalnız açık (`GrantSource = Manual`) rol-izin atamasıyla bağlanır; rol hiçbir
  `finance-treasury.*` / `mdm.*` anahtarı, `waive-sod` ya da bağımlı anahtar taşımaz; 1–26 rollerinin hiçbiri (Hazine yöneticisi
  dahil) iki Auth anahtarını taşımaz; kullanıcı-rol satırı otomatik yazılmaz. **Oluşturulma yolu (KARAR D75/1, K-W7 (a)):**
  Treasury hak profili rolü izinsiz ayrılmış sistem rolü (`IsSystem = true`) olarak oluşturur — profil eşitlemesinden sonra rol
  vardır ve sıfır `RolePermission` satırı taşır; tekrar oynatma ikinci rol üretmez; iki anahtar role açık (`Manual`) atamayla
  bağlandıktan sonra yeniden eşitleme bu satırları silmez. Ad çakışma ön denetimine girer: aynı adlı sistem dışı rol → sıfır
  rol, sıfır hak (AC-7 deseni). **Atayan izin + rol üyeliği (D75/1):** atama ucu yalnız bu rolün üyesine açıktır (AC-29) —
  `auth.payment-sod-waivers.assign` başka role elle bağlansa da o rolün rolsüz üyesi atayamaz. Sabotaj: profilde ya da
  eşitlemede role `auth.payment-sod-waivers.assign` otomatik eklenince, ya da `TreasuryManager`'a eklenince test kırmızıya
  döner; profil rolü oluşturmayınca "eşitlemeden sonra rol var, sıfır hak" testi kırmızıya döner; eşitleme `Manual` satırları
  silince "yeniden eşitleme açık atamayı korur" testi kırmızıya döner.
- [ ] AC-44 (zorunlu) — **Kabul edilmiş iz (KARAR D97/2, D98):** `Diten.AuthService.md` `## İzler`'de
  `auth-sod-muafiyet-gecmisi | c | yazıcı | IPaymentSodWaiverAuditWriter.WriteCreatedAsync/IPaymentSodWaiverAuditWriter.WriteRevokedAsync` satırı ve beş koşul notu; `AcceptedTrails`'te aynı üçlü;
  `AuditTrailStandard` testleri yeşil; iki komut "denetleniyor" ölçülür, `PinnedCounts` Auth değeri (`Debt 18, Exceptions 1,
  K2Debt 11`) değişmez; `IssueServiceIdentityTokenCommand` İ5 satırı aynen kalır; `auth-gunlugu` `aday` kalır. Sabotaj: iki
  işleyiciden biri yazıcı yerine `IAuthAuditService.WriteOnceAsync`'i çağırınca ya da `AcceptedTrails` üçlüsü kaldırılınca test
  kırmızıya döner.
- [ ] AC-45 (zorunlu) — **Geçmiş ucu (öneri 3; kural koşul 1–3):** `GET /api/roles/sod-waiver-assignments/{id}/history` yalnız o
  atamanın satırlarını (`EntityType` + `EntityId` ile) döner; izinsiz 403; başka kiracının atama kimliği 404; olmayan kimlik 404;
  yaratma ve geri alma satırları alıcı, hedef parti, süre, onaylayan, gerekçe kodu, geri alan ve zamanı taşır. Okuma arayüzünde
  güncelleme / silme üyesi yoktur (mimari test) ve gerçek Mongo'da geçmiş satırları uç üzerinden değiştirilemez. Sabotaj: kiracı
  süzgeci kaldırılınca başka kiracı testi kırmızıya döner.
  **Liste ucunun iki biçimi (KARAR D99/4, D100/1) — sözleşme testleri:** (i) `paymentBatchId` var, `page` / `pageSize` yok →
  parti biçimi, yalnız geçerli atamalar, yanıt D79/6 sözleşme dosyasıyla aynı; (ii) `paymentBatchId` yok → sayfalı tam liste
  (geçerli, süresi dolmuş, geri alınmış; durum alanı), sayfalama paketle sabit (KARAR D101/1): varsayılan `page = 1`, `pageSize = 20` (Auth deseni
  `Features/Users/Queries/GetAllUsersQuery.cs:15`), üst sınır `pageSize = 100`; `page < 1`, `pageSize < 1` ya da `pageSize > 100`
  → 400 `SOD_WAIVER_PAGING_INVALID` (kırpma yok); yanıt `DTOs/PaginatedResult.cs` biçiminde; 21 atamalı kiracıda varsayılan istek
  20 öğe + `TotalCount = 21` döner, `pageSize = 101` → 400; en yeni önce;
  (iii) `paymentBatchId` + `page` ya da `pageSize` birlikte → 400 (iki biçim karıştırılmaz); (iv) iki biçimde de başka kiracının
  ataması dönmez. Sabotaj: denetleyici biçim seçimini kaldırıp hep tam listeyi döndürünce (i) kırmızıya döner.
- [ ] AC-46 (zorunlu) — **Meta-denetim teslimi ve yapılandırma şartı (öneri 5, KARAR D97/2 (b), D99/1):** geçmiş okuması
  `IsMetaAudit = true` okuma satırını sonucu dönen iletici üyesiyle gönderir ve **yalnız teslim teyidi gelince** veri döndürür.
  HTTP testleri gerçek `PlatformAuditForwarder` ile, Platform iç ucunun sözleşmesine (`POST /api/internal/audit/append`,
  `X-Internal-Api-Key`, gövde `AuditAppendRequest` + `IsMetaAudit`) uyan saplama sunucuya karşı koşar: (i) 2xx → 200 + veri ve
  saplamada gövdesi `IsMetaAudit = true`, `EntityType = PaymentSodWaiverAssignment`, `EntityId` = atama olan tek satır; (ii) anahtar
  boş → 503 `SOD_WAIVER_HISTORY_AUDIT_UNAVAILABLE`, saplamaya çağrı yok; (iii) saplama 500 / 401 → 503, veri yok; (iv) bağlantı
  reddi ve zaman aşımı → 503, veri yok. Mevcut `ForwardAsync` çağıranlarının davranışı değişmez (gerileme testi). Dev ortamı
  kanıtı: bir geçmiş okumasından sonra Platform merkezi günlüğünde meta-denetim satırı görülür; `post-deploy-steps.md` Auth adımı
  anahtarın dolu olduğunu ve bu satırın görüldüğünü kontrol eder. Sabotaj: teslim teyidi yok sayılıp (`ForwardAsync`'e dönülüp)
  veri döndürülünce (iii) ve (iv) kırmızıya döner; anahtar denetimi kaldırılınca (ii) kırmızıya döner.
- [ ] AC-47 (zorunlu) — **Roller ekranı (öneri 4, KARAR D97/2 (c)):** `auth.payment-sod-waivers.read` izni olan kullanıcı Roller
  sayfasında salt-okunur muafiyet atamaları listesini ve satırın "Geçmiş"ini görür; izni olmayana bölüm çizilmez (iskelet yok,
  yönlendirme yok — UAS-001); yazma düğmesi yok. Bütün görünen metin `RolesIndex.{en,tr,fr,es,zh,ar,ru}.resx`'te, İngilizce dışı
  değer en ile aynı değil, `index.l10n.js` anahtar listesi eksiksiz. Sabotaj: bir dil dosyasından yeni anahtar silinince ya da
  izin denetimi kaldırılınca `Diten.Web.Tests` kırmızıya döner.

## 17. Test Expectations

| Alan | Gerekli kanıt |
|---|---|
| Profil birim | 117 anahtar, 26 Treasury rolü (+ 27. rol Muafiyet yetkilisi — AC-43, D74/3), rol başına küme eşitliği, modül kodu başına tam küme, eksik / fazla / bilinmeyen eylem reddi; yedi onaylayıcı rolde (7, 8, 12, 15, 17, 19, 26) `platform.workflow.tasks.approve` / `.reject` bağımlı izinleri (D70/1; AC-3, AC-18); tedarikçi bağımlı izinleri — `procurement.suppliers.eligibility.read` 11, 12, 13, 20, 21, 22; `procurement.suppliers.read` yalnız 11, 13; `mdm.legal-entities.read` 20, 21, 22'de de (D72/3–4; AC-3, AC-14) |
| Taban | Hak olmadan Admin / Viewer'da sıfır Treasury anahtarı; 18 `read` anahtarı dışlama listesinde; modül hakkıyla da Admin / Viewer'da sıfır (KARAR D79/2, AC-5); SuperAdmin tam katalog yolu bugünkü gibi (KARAR D79/3; FU20 / FU22 gerilemesi, AC-12) |
| Rol görünen adı (KARAR D79/9) | `frontend/Diten.Web.Tests` guard testi (AC-7): üretim profilinden türetilen her `RoleName` × 7 dil `SharedResource` anahtarı, en kopyası yok, üç köprüde giriş, Bölüm 14.2 tablosuyla eşit; sabotaj kanıtı (bir dil dosyasından satır silme → kırmızı → geri koy → yeşil) |
| Açık atama | 10 anahtar (8 Treasury + 2 Auth, D74/2) × 3 otomatik yol (SuperAdmin, modül eşitlemesi, varsayılan rol); üretim listesi 20 öğe (AC-1, AC-13, AC-17, AC-42) |
| SoD yapı | Aynı nesne türünde hazırlama + karar birlikte yok; `TreasuryManager` yasak listesi |
| Bileşik eşitleme | Çapraz modüllü rollerde kaynak modül ayrımı; ürün modülü profilleri (FU20 / FU22 / BL-592) etkilenmez |
| Ön denetim atomikliği | 26 Treasury rol adında (KARAR D75/1 ile 27. ad dahil) parametreli çakışma → sıfır mutasyon |
| Hak döngüsü | Etkin / eksik / kapalı / süresi dolmuş / belirsiz; geri yükleme kesinliği |
| Yalıtım | A / B kiracı, `Manual` / `System` / başka modül koruma |
| DL-4 | Gerçek Platform doğrulayıcısı ve MDM istemcisi testleri; dev ortamı iki şirket iki kullanıcı |
| DL-5 | **K-W5 okuma yolu (KARAR D98):** ayrı yazıcı tipi, `AcceptedTrails` + defter (AC-44), geçmiş ucu kiracı / izin / salt-okunur (AC-45), meta-denetim + anahtar boş → 503 (AC-46), Roller ekranı izin + 7 dil (AC-47); İşleyici, doğrulayıcı, denetim (hata enjeksiyonu), sözleşme, gerçek Mongo; iç uç kimliği KARAR D74/1 — ortak sır, iletilen token'dan kiracı, istekteki kiracının yok sayılması, kapalı-başarısız 401 (AC-26, AC-32); atayan Muafiyet yetkilisi, rolsüz 403, kendine 403, izni var ama rol üyesi değil 403 ve rol üyesi ama izni yok 403 — yaratma ve geri alma (AC-29, KARAR D75/1, geri alma KARAR D76/1); okuma ucu yalnız `.read` (D76/1); denetim sırası testi gerçek boru hattıyla ve sıra sabotajı (AC-29, KARAR D76/2); rol şablonu, profilin rolü izinsiz oluşturması ve iki Auth anahtarı (AC-42, AC-43, D75/1); kiracı uçları `/api/roles/sod-waiver-assignments` altında (KARAR D79/5); sürümlü sözleşme dosyasına karşı Auth sözleşme testi ve MOD-0126 sahte istemcisi (AC-34, KARAR D79/6); token üretimi değişmez (KARAR D79/4) |
| DL-6 | İşleyici (kiracı, etkin izin hesabı, açık atama kaynağı, etkinlik — hizmet hesabı aday değil, geçici kilitli aday (KARAR D79/8, AC-38) —, muafiyet biçimi; şirket kapsamı Auth'ta yok, tüketicide (KARAR D79/7, AC-40); `permissionKey = platform.workflow.tasks.approve` sorgusu — KARAR D71), doğrulayıcı, sözleşme, gerçek Mongo, iç uç kimliği (KARAR D74/1 — ortak sır + token'dan kiracı; AC-35); tüketici tarafı (anahtar başına ayrı çağrı ve iş akışı izni kesişimi dahil — KARAR D71) her paketin aday AC'sinde sahte istemciyle (MOD-0126 AC-11, MOD-0119 AC-10, MOD-0120 AC-9, MOD-0124 AC-10, MOD-0217 AC-33) |
| Gerçek Mongo | Ayrı sahipli örnek, sabit veritabanı, `MONGO_TEST_URI`; uygulama 27017 yasak; sahte / bellek içi kanıt kalıcılık iddiasını kapatmaz |
| Derleme / takımlar | Odaklı Auth testleri, tam AuthService takımı, Auth API Release derlemesi; Bölüm 5.5 için odaklı `Diten.Web.Tests` (guard + mevcut `RoleAssignments` / `UserRoleAssignments` / Roller JS testleri gerileme); DL-4'te Platform ve MDM odaklı + tam takım; gerçek sayılar ve önceden bilinen kırmızılar (FU20 kaydındaki `UserLookupValidationContractTests` gibi) taban karşılaştırmasıyla |

Kural üretim koduyla ölçülür: testler `DefaultRolePermissionTemplate`, `ExplicitGrantOnlyPermissions`,
`EntitlementPermissionSyncService` ve yeni profilin kendisini çağırır; anahtar listesinin test içinde kopyası "beklenen" olarak
yalnız matristen (bu paketten) gelir ve üretim kümesiyle eşitlenir. Her sabotaj ayrı commit'te değil, test içinde değiştirilmiş
üretim girdisiyle ya da teslim kaydında "kaldır → kırmızı → geri koy → yeşil" kanıtıyla gösterilir.

## 18. Ready-for-dev Checklist

- [x] Golden Reference kararı yazıldı (`none`; DL-5 arka uç düzeni Golden Reference'a birebir).
- [x] Frontmatter zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`).
- [x] 117 anahtar, 26 rol ve 8 açık atama matrisle mekanik uzlaştırıldı (Bölüm 19; D69 sonrası yeniden koşuldu). D74 eki
  (2 Auth anahtarı, açık atama toplamı 10, 27. rol) karar dosyasının matris bölümüne yazıldı; Treasury modül sayıları değişmedi
  (Bölüm 19).
- [x] D74 main'de (D62/1) — K-W1 / K-W3 / K-A1 kapanışının, 27. rolün ve 10 açık atama sayımının paket kapısı — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] D75 main'de (D62/1) — K-W7 kapanışının (rolün izinsiz oluşturulması, atayanın izin + rol üyeliği denetimi) ve Bölüm 8.7
  sınır kabulünün (D75/2) paket kapısı — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] D76 main'de (D62/1) — geri alma ucundaki üyelik denetiminin, okuma ucunun yalnız `.read` istemesinin (D76/1) ve denetim
  sırasının (D76/2) paket kapısı (DL-5) — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] D69 main'de (D62/1) — DL-3 paket kapısı ({K} ve Sözleşme onaylayıcı) — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] FU numarası verildi; dosya adı, `id`, `branch` ve kayıt satırı yazıldı; `verify_module_id.py --check-id` geçti (K-1) —
  **K-1 KAPANDI — MOD-0018 sahibi kararı (Y-19 / D80/1): MOD-0018-FU27**; `verify_module_id.py . --check-id MOD-0018-FU27 --name
  "Treasury Permission Onboarding" --parent MOD-0018` → `OK  MOD-0018-FU27: proven against Blueprint/registry.` (çıkış 0,
  2026-10-29); kayıt satırı `execution/registries/module-id-registry.md` (D80 main'de, `317a48b13`).
- [ ] MOD-0018 sahibi incelemesi ve teyidi (Y-15/3, Y-16; D69'un anahtarı ve rolü dahil — D69; onaylayıcı rollerdeki iki iş akışı
  bağımlı izni dahil — D70/1; tüketicinin iş akışı izni kesişimi dahil — D71; tedarikçi bağımlı izinleri dahil — D72/3–4;
  iç uç kimliği, iki Auth anahtarı ve Muafiyet yetkilisi rolü dahil — D74; rolün oluşturulma yolu, atayanın izin + rol
  üyeliği denetimi dahil — D75; geri alma ucuna aynı denetimin uygulanması, okuma ucunun yalnız `.read` istemesi ve denetim
  sırası dahil — KARAR D76/1–2; D79'un dokuz kararı ve 27 rolün 7 dilli görünen adları dahil — KARAR D79). **Şartlı teyit
  verildi — Y-19 / KARAR D80/2** (D69–D78 sürümü, main `4c08fc75a`; şartlar DL-5 kodundan önce, aşağıdaki iki D80/2 maddesi);
  K-7 ve DL-6 teyitli (D80/3). **Sahipteki liste (D80/4) Y-21 / KARAR D84/1 ile kapandı:** K-1b, K-2, K-3, K-W2, K-W4, K-W6,
  K-W7, K-A1, K-A2, K-A3, K-A4b. Açık kalan: D79'dan sonraki değişikliklerin ayrı teyidi (27 rolün 7 dil tablosu vb., D80/2; sahip
  kontrol ediyor — Y-21), K-W5 CT defter kararı (yöntem teyitli — D84/2; D86 geri alındı — D87, açık) ve K-5 anahtar listesinin Platform sahibince kontrolü (yöntem teyitli — D84/3; liste K1 kararı — D88/1; Beste'nin kontrolü bekliyor — Y-22).
- [x] D79 main'de (D62/1) — K-1b, K-2, K-3, K-7, K-W2, K-W4, K-W6, K-A2, K-A3 kapanışlarının ve Bölüm 5.5 / 14.2 görünen ad
  tablosunun paket kapısı — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] D53/1 tüketici paket kapısı: bu paketin metni main'de **+ MOD-0018 sahibinin teyidi** (**KARAR D70/3**; metnin main'de olması
  tek başına yetmez, teyit karar dosyasına kaynak olarak yazılır) — altı tüketici paketin paket kapısı. **Şartlı teyit — Y-19 /
  KARAR D80/2:** D69–D78 sürümü (main `4c08fc75a`) teyitli; kapı tüketicilerin **DL-1'ini bekletmez**; şartlar bu paketin DL-5
  kodundan önce kapanır (aşağıdaki iki D80/2 maddesi); D79'dan sonraki değişiklikler ayrıca teyide gider; D80 main'de (`317a48b13`) — **işaretlendi: şartlı teyit (Y-19 / KARAR D80/2 → KARAR D83/1);** şart — K-W5 defteri ve rol atama denetimi MOD-0018 Treasury Auth ekinin DL-5 kodundan önce (FU27, D80/2 (a)(b) maddeleri açık).
- [x] D70 main'de (D62/1) — K-4 / K-A4 kapanışının (DL-1, DL-3, DL-6) ve D70/3 kapısının paket kapısı — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] D71 main'de (D62/1) — K-A4b kapanışının (DL-6; tüketici aday kesişimi) paket kapısı — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] D80 main'de (D62/1) — K-1 kapanışının (FU27, D80/1), Auth paket kapısı şartlı teyidinin (D80/2) ve K-7 / DL-6 teyidinin
  (D80/3) paket kapısı — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] D72 main'de (D62/1) — D72/3–4 tedarikçi bağımlı anahtarlarının (DL-1 roller 11–13, DL-2 roller 20–22) paket kapısı;
  MOD-0018 sahibinin teyidi bu anahtarları da kapsar (D72/3) — **main'de** (`treasury-intake-owner-decisions-05.md` @ `317a48b13`).
- [x] K-1b modül kodu yapısı — **KAPANDI — KARAR D79/1** (paket başına bir kod; MOD-0018 sahibinin teyidine Auth ekiyle gider).
  Kod **değerleri** tüketici paketlerin manifest adımında yazılır — DL-1/2/3 ve DL-4 bunlara bağlıdır (Bölüm 7).
- [x] K-2 Admin / Viewer varsayılanı — **KAPANDI — KARAR D79/2** (hiçbiri); K-3 SuperAdmin tam katalog — **KAPANDI — KARAR
  D79/3** (bugünkü davranış); K-7 rol adları — **KAPANDI — KARAR D79/9** (sabit kod + 7 dilli görünen ad; Bölüm 5.5, 14.2) —
  üçü de MOD-0018 sahibinin teyidine Auth ekiyle gider (K-4 bağımlı anahtarlar **KAPANDI — KARAR D70/1**, (b)). **K-7 sahip
  teyitli — KARAR D80/3** (Y-19); **K-1b, K-2, K-3 sahip teyitli — Y-21 / KARAR D84/1.**
- [ ] Bağımsız inceleme (CT-APR-02) ve CT-APR-01 onay satırı main'de.
- [ ] XMC-001 onayı: Beste (Platform dosyaları) ve MDM sahibi (MDM istemcisi); K-5 çift listesi — DL-4 için. **Beste: verildi,
  dosya kapsamıyla (Y-22 / D88/3); K-5 listesi K1 kararı (D88/1); MDM sahibinin XMC-001'i verildi (Ali, Y-26 / KARAR D97/1: yalnız `PlatformTrustedLegalEntityScopeProviderClient.cs` ve testleri); finans tüketicisi Platform dosyaları için Beste'nin XMC-001'i açık (D92/1).**
- [ ] MOD-0217 `approved` / `ready-for-dev` — DL-3 kodu için.
- [x] D64 main'de (D62/1) — DL-5 paket kapısı. Kaynak: PR #276 (`3bb1ab838`; `git merge-base --is-ancestor 4d8a4050a 3bb1ab838` → 0, 2026-10-28).
- [ ] **KARAR D80/2 (a) — sahip şartı (Y-19):** K-W5 denetim izi için defter kararı — `auth-gunlugu` izinin K2 için kabulü ya da
  başka kabul edilmiş iz (CT defter kararı, K-W5) — **DL-5 kodundan önce** (DL-1'i bekletmez). **KARAR D86 geri alındı (KARAR
  D87):** K2 borcu yolu geçmez — K2 borcundaki komut denetleniyor olmalı (`AuditTrailStandardTests.cs:616-619`, `IsAudited`
  yalnız kabul edilmiş izde doğru); önce-yaz `aday` izi kabul edilmiş ize çevirmez (§4.4 ≠ §5.c-3). Önerilen kapanış (MOD-0018
  sahibinin kararı): DL-5'e `auth-gunlugu` için kiracı + izin denetimli okuma yolu, kaydın ekranından geçmiş, yol `c`'nin beş
  koşulunun kanıtı, iz belirtecinin deftere ve kabul listesine işlenmesi, günlük okumasının meta-denetimi (§7); önce-yaz korunur — **KARAR D97/2 (Ali, Y-26):** sahip okuma yolunu kabul etti (öneri 1, 2, 3, 5, 6 ve 4 — ekran: Roller ekranında salt-okunur muafiyet atamaları listesi + "Geçmiş" bölümü); düzeltmeler: (a) iz belirteci iki komut için ayrı bir yazıcı tipinin metodu (`WriteOnceAsync` + komut adı biçimi yok; `IssueServiceIdentityTokenHandler`'ın istisnası kaymaz), (b) Auth'un merkezi iletici anahtarı boşsa okuma satırı sessizce kaybolur — kabul ölçütüne yapılandırma şartı yazılır, (c) ekran kiracı tarafındaysa 7 dil; paket metni yazıldı (**KARAR D98**, D99); **işaret iki koşulla konur (D98/6):** (1) bu metin CT-APR-02'den geçer, (2) MOD-0018
  sahibi D79 sonrası değişiklikleri (D98 dahil, D99/1 teslim teyidi önerisi dahil) teyit eder — ikisi gelene kadar işaretsiz.
- [ ] **KARAR D80/2 (b) — sahip şartı (Y-19):** rol atama adımlarının (rola izin bağlama, kişiye rol verme) denetim borcu kapandı
  (defterde rol / izin değişikliklerinin izi `auth-rbac-gunlugu` bugün `aday`, Bölüm 7, 21; kapanış yolu bu pakette seçilmedi) —
  **DL-5 kodundan önce** (DL-1'i bekletmez).
- [x] K-W2 (token) **KAPANDI — KARAR D79/4**, K-W4 (yol) **KAPANDI — KARAR D79/5**, K-W6 (sözleşme dosyası) **KAPANDI — KARAR
  D79/6** — DL-5 için; MOD-0018 sahibinin teyidine Auth ekiyle gider
  (K-W1 atama onay izni **KAPANDI — KARAR D74/2–3**; K-W3 iç uç kimliği **KAPANDI — KARAR D74/1**; K-W7 Muafiyet yetkilisi
  rolünün oluşturulma yolu ve atayan denetimi **KAPANDI — KARAR D75/1**). **K-W2, K-W4, K-W6, K-W7 sahip teyitli — Y-21 / KARAR
  D84/1.**
- [x] D66 main'de (D62/1) — DL-6 paket kapısı. Kaynak: PR #281 (`c32cabe6b`; `git merge-base --is-ancestor 3c98e7bfc c32cabe6b` → 0, 2026-10-28).
- [x] MOD-0018 sahibinin DL-6 teyidi (D66/1: "MOD-0018 sahibinin teyidi gerekir") — DL-6 için. **KARAR D80/3** (Y-19): sahip D67/1'i
  (aday sorgusu DL-6 ayrı dilim) teyit etti; DL-6'nın kalan açıkları K-A1 / K-A2 / K-A3 teyidi — **Y-21 / KARAR D84/1 ile kapandı** — ve D80'in
  main'e girmesi — **main'de** (`317a48b13`).
- [x] K-A1'in kalanı (uç adı ve sözleşme dosyası — CT önerisi, MOD-0018 sahibinin teyidi; kimlik **KAPANDI — KARAR D74/1**) — DL-6
  için (K-A2 şirket kapsamı **KAPANDI — KARAR D79/7** (b); K-A3 etkin kullanıcı tanımının kalanı **KAPANDI — KARAR D79/8**; ikisi
  MOD-0018 sahibinin teyidine Auth ekiyle gider; K-A4 bağımlı iş akışı anahtarı **KAPANDI — D70/1**; K-A4b elle atamada iş akışı izni olmayan aday **KAPANDI — KARAR D71**:
  tüketici kesiştirir) — **sahip teyitli: Y-21 / KARAR D84/1** (uç `/internal/permission-candidates` + sürümlü sözleşme dosyası;
  K-A2 (b), K-A3, K-A4b teyitli).
- [ ] Yeni sayfa yok, tur yok (UXH-001: sayfa sahiplenilmiyor); Bölüm 5.5 yalnız mevcut rol ekranlarına görünen ad kaynağı ekler
  (KARAR D79/9); **DL-5 (KARAR D97/2, D98):** mevcut Roller sayfasına salt-okunur muafiyet atamaları bölümü + "Geçmiş" (izinle
  çizilir, 7 dil, AC-47; sayfa yardımı varsa bir paragraf) — DL-5 için.
- [ ] 27 rolün fr / es / zh / ar / ru görünen adları MOD-0018 sahibince teyit edildi (KARAR D79/9: "Treasury yazar, MOD-0018
  sahibi teyit eder") — DL-1 / DL-2 / DL-3 / DL-5 rol adı satırları için. D79'dan sonraki değişiklik olduğu için Y-19'un şartlı
  teyidine girmez; sahip teyidine **ayrıca** gider (KARAR D80/2) — teyit yok.

## 19. Implementation Notes

**Matris uzlaştırması (2026-10-28, mekanik; D69 sonrası yeniden koşuldu).** Karar dosyasının "(4) Kapsama tablosu" satırları
ayrıştırıldı: 121 satır = 117 modül anahtarı + 4 bağımlı satır (`platform.workflow.tasks.approve` / `.reject` tek satır olduğundan
bağımlı anahtar sayısı 5); yinelenen yok. Modül dağılımı MOD-0126 22, MOD-0119 19, MOD-0120 15, MOD-0124 20, MOD-0289 23, MOD-0217 18
— karar dosyasının sayım paragrafıyla eşit. 26 rol (finans 19, ana veri 7), "(2) / (3)" tablolarından miras kuralıyla genişletildi:
rollerin birleşimi 109 anahtar, açık atama 8, kesişim boş, birleşim 117 = tablo; eksik ve fazla yok; rol başına sayılar Bölüm 14.2
ile aynı (yeni rol 26: 3). D69 öncesi aynı betik 120 satır / 116 / 108 / 25 rol verdi. Altı paketin metninde tam yazılmış `finance-treasury.*` / `mdm.*`
anahtarları tablonun dışında anahtar içermiyor (fazla görünenler olay adlarıdır: `…payment-batch.created` vb.; MOD-0217'deki
`mdm.contracts.documents` bir önek anması). MOD-0289'un üç amaç anahtarı (`mdm.bank-account-purposes.update` / `.delete` /
`.deactivate`) pakette yalnız kısaltılmış (`.update`) biçimde geçiyor — fark değil. Y-7'deki "113 anahtar" D47/D48 dönemine
aittir; D49/2 (+1 `mdm.contracts.value.write`), D57/4 (+2 SoD ayar anahtarı) ve D69 (+1 `mdm.contracts.versions.approve`) ile 117.
**D72/3–4 (2026-10-28):** karar dosyasının kapsama tablosu değişmedi (D72 tabloya satır eklemez); iki bağımlı anahtar
(`procurement.suppliers.eligibility.read`, `procurement.suppliers.read`) D72 metninden ve tüketici paketlerin MOD-0140 çağrı
yerlerinden (MOD-0119 Runtime Constraints (b), MOD-0289 Authorization Convention) ölçülerek Bölüm 14.2'ye yazıldı; bağımlı
anahtar sayısı 5 → 7, modül anahtarı 117 ve rol 26 değişmedi.
**D74/2–3 (2026-10-28):** iki Auth anahtarı (`auth.payment-sod-waivers.assign`, `.read`) karar dosyasının matris sayım kuralına
göre modül anahtarı sayılmaz (sayım paragrafı: "117 modül anahtarı (MOD-0126 22, … MOD-0217 18)" — altı Treasury paketinin
anahtarları; kapsama tablosu "(4)" yalnız onları ve bağımlı satırları listeler). Bu yüzden 117 ve kapsama tablosunun 121 satırı
değişmez; matrise "117 modül anahtarı + 2 Auth anahtarı" ve 27. rol satırı yazıldı. Açık atama: 8 Treasury + 2 Auth = 10; kod
listesi 10 → 20 (önceki metindeki "→ 18" yalnız Treasury anahtarlarını sayıyordu — Y-18 bulgu 2). Rol: 26 Treasury + 1 Auth = 27.

**Kod gerçeği (ölçüm).** `ExplicitGrantOnlyPermissions` bugün 10 anahtar taşır (`:89-94`), hiçbiri Treasury'nin değil. Viewer
`read` kuralı ve dışlama listesi `DefaultRolePermissionTemplate.cs:120-168, 207-213`; genel modül seçimi
`EntitlementPermissionSyncService.cs:572-580`; mevcut özel profiller ABB, FU22 ve ürün kimliği (`:96-110`, `:582-625`). Platform
güvenilir kapsam doğrulayıcısı modül kodunu ve izin listesini sabitler (`TrustedLegalEntityScopeCredentialAuthenticator.cs:35-56,
97-115`); MDM istemcisi aynı kodu sabitler (`PlatformTrustedLegalEntityScopeProviderClient.cs:18, 170`). Auth'ta süreli ya da
nesneye bağlı atama yapısı yoktur (`UserRole`, `RolePermission` süresiz); DL-5 yeni kayıt türüdür. Auth denetiminde önce-yaz
kalıbı vardır (`IAuthAuditService.WriteOnceAsync`), ama `auth-gunlugu` izi defterde `aday`dır.

**Neden ayrı FU.** FU16–FU22 ürün modülüne aittir; Treasury altı yeni modül kodu, 26 rol (+ Auth Muafiyet yetkilisi, D74/3), çapraz modüllü rol ve süreli parti
bağlı atama getirir. Önceki FU'lara katmak tamamlanmış kapsamları genişletir.

**İlgili kayıtlı FU.** MOD-0018-FU11 "Temporary Access Pipeline Binding" (`planned`) süreli erişimin doğal evidir. K-W2 **KARAR
D79/4** ile kapandı: izin token'da süreli yapılmaz, bu paket FU11'e dayanmaz; süreli izin ileride FU11'le gelirse DL-5'in
kullanım kuralı yeniden değerlendirilir.

**Rol görünen adı ölçümü (2026-10-29, KARAR D79/9).** `RoleName_{RoleName}` deseni `SharedResource`'ta yalnız dört temel
sistem rolü için vardır (`SuperAdmin`, `Admin`, `Viewer`, `ReadOnly`; yedi dilde 4'er satır); köprü `RoleNames` sözlüğü
`RoleAssignments` ve `UserRoleAssignments` ekranlarında var, Roller ekranında yok (Bölüm 5.5). Sözlük bugün her köprüde elle
yazılmış girişlerdir; 27 giriş üç köprüye aynen yazılır, kayma riskini AC-7 guard testi (iii) yakalar. Rol adlarının
`SharedResource` yerine ekran kaynağına (`RolesIndex.{lang}.resx`) konmaması localization-standard §3 A'dandır (üç ekranda görünür).

## 20. Follow-up Items

- `BANK-ACCOUNT-LOOKUP` 1.0.0 bir finans akışında okunmaya başlayınca okuyan rol yeniden ölçülür (D47/4 bölüm 5) — bu pakete revizyon.
- `PartiallyApproved` / çok adımlı onay (D62/2) gelince ek onay rolleri ayrı revizyon.
- Muafiyet atama ekranı (Rol ekranı ya da Hazine ekranı) ve 5 iş günlük K1 inceleme raporu ayrı paket.
- PKS-001 §4 ad alanı tablosuna `finance-treasury.*` satırı (korumalı yol, ayrı onay).
- Local Development / Production hak eşitlemesi, rol üyelikleri, token yenileme ve izin / ret duman testi ayrı operasyon kapısı.

## 21. Audited Events

Servisin denetim altyapısı bugün: `auth-kullanici-iletimi` (yol b, en iyi çaba) kabul edilmiş; `auth-gunlugu` ve
`auth-rbac-gunlugu` **`aday`** (`tests/architecture/audit-ledger/Diten.AuthService.md` `## İzler`). DL-1…DL-4 ve DL-6 yeni yazma komutu
eklemez (DL-6 yalnız okuma sorgusudur) (mevcut hak eşitlemesi yolunu kullanır; durumu defterde olduğu gibi kalır, yeni K2 borcu yazılmaz). DL-5'in iki komutu
K2 sınıfıdır (yetki değiştirir).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreatePaymentSodWaiverAssignmentCommand | auth.sod-waiver-assignment.created | PaymentSodWaiverAssignment | c — yeni iz `auth-sod-muafiyet-gecmisi`, önce-yaz `IPaymentSodWaiverAuditWriter.WriteCreatedAsync` (KARAR D97/2, D98; okuma ucu + Roller ekranı "Geçmiş" — yol `c`'nin beş koşulu, AC-44…AC-47) | AfterState: GranteeUserId, TargetPaymentBatchId, StartsAtUtc, ExpiresAtUtc, ApprovedByUserId, ReasonCode | — |
| RevokePaymentSodWaiverAssignmentCommand | auth.sod-waiver-assignment.revoked | PaymentSodWaiverAssignment | c — aynı iz, `IPaymentSodWaiverAuditWriter.WriteRevokedAsync` (D98) | Before/After: RevokedAtUtc, RevokedByUserId (+ atamanın kimliği ve hedef partisi) | — |
| GetPaymentSodWaiverAssignmentHistoryQuery (**okuma satırı** — audit-trail-standard §7, denetim günlüğünün kendisi okunur; KARAR D102/1) | auth.sod-waiver-assignment.history.read | PaymentSodWaiverAssignment (okunan atamanın kimliği) | b — merkezi günlüğe meta-denetim satırı (`IsMetaAudit = true`), Auth iletici üzerinden teslim teyitli (`TryForwardAsync`, D99/1); teslim yoksa okuma yok, 503 (AC-46). Eşdeğer iz okuma için kullanılmaz | — (okuma; değişen alan yok — satır okuyan kullanıcı, kiracı, atama kimliği ve zamanı taşır) | — |

- **K2 (kapalı-başarısız):** iki komut da denetim kaydını iş yazımından önce yazar; yazılamazsa komut başarısız olur, atama
  yazılmaz (AC-30). "En iyi çaba" yoktur. İz kabul edilmeden komut yazılamaz (audit-trail-standard §4.4: kabul edilmiş K2 yolu
  yoksa Control Tower'a yazılır) — bu yüzden DL-5'in kapısında.
- **Sahip şartları — KARAR D80/2 (Y-19), DL-5 kodundan önce:** (a) K-W5 denetim izi için defter kararı (yukarıdaki iki komutun
  izi) — **sahip kararı verildi — KARAR D97/2 (Y-26)**, paket metni **KARAR D98**, D99 (yeni dar iz, okuma ucu, Roller ekranı,
  meta-denetim teslim teyidi); işaret iki koşulla: CT-APR-02 + MOD-0018 sahibinin D79 sonrası teyidi (D98/6); (b) rol atama adımlarının (rola izin bağlama, kişiye rol verme) denetim borcu — defterde `auth-rbac-gunlugu` (rol / izin
  değişiklikleri) bugün `aday`; bu borcun hangi yolla kapanacağı bu pakette seçilmedi. İkisi de DL-1'i bekletmez.
- **Kayda yazılmayan:** token, istek başlıkları, kullanıcının kişi verisi (yalnız kimlikler).
- **Okuma denetimi:** iç sorgu, kiracı listesi ve DL-6 aday sorgusu okumadır; ayrıca denetlenmez — **istisna:** geçmiş okuması
  denetim kaydının okunmasıdır, meta-denetim satırı yazar (§7; AC-46) — tabloda ayrı okuma satırı (KARAR D102/1) (muafiyetin **kullanımı** MOD-0126'nın K2
  olayıdır, `…approved-with-sod-waiver`).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | finans onboarding'i — 76 `finance-treasury.*` anahtarı, 19 rol, 5 açık atama, iki SoD ayar anahtarı (a, b, c, f); AC-1…AC-12; ayrıntı: §5.1, §8.1–8.3, §14.2 satır 1–19 | Module Summary, Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention, Acceptance Criteria |
| DL-2 | banka onboarding'i — MOD-0289: 23 anahtar, 3 rol, 2 açık atama; AC-13…AC-16; ayrıntı: §5.1, §8.1–8.3, §14.2 satır 20–22 | Repo Scope, Runtime Constraints, Authorization Convention, Acceptance Criteria |
| DL-3 | sözleşme onboarding'i — MOD-0217: 18 anahtar, 4 rol, 1 açık atama (D69 dahil); **koşullu: MOD-0217 onayından sonra** (Y-6, Y-10/1); AC-17…AC-21; ayrıntı: §5.1, §8.1–8.3, §14.2 satır 23–26 | Repo Scope, Runtime Constraints, Authorization Convention, Acceptance Criteria |
| DL-4 | SEC-002 eki — Platform izin listesi + MDM istemcisinin modül kodu (e; D56/1); XMC-001 Beste + MDM sahibi; AC-22…AC-25; ayrıntı: §5.2, §8.3, §8.4 | Repo Scope, Runtime Constraints, Acceptance Criteria |
| DL-5 | `waive-sod` parti bağlı atama — kayıt, yazma ucu, "bu parti için geçerli atamalar" sorgusu, K2 denetim (d; D54 + D64); **kod önkoşulu KARAR D80/2: K-W5 defter kararı + rol atama denetim borcu (tablonun altı)**; AC-26…AC-34, AC-42, AC-43 (D74), AC-44…AC-47 (K-W5 okuma yolu — KARAR D98); iki Auth anahtarı ve 27. rol Muafiyet yetkilisi (D74/2–3; rol izinsiz, atayan izin + rol üyeliği — D75/1; geri almada da, okuma yalnız `.read`, denetim sırası — D76/1–2); ayrıntı: §4 yeni kayıt, §5.3, §8.5, §8.7, §14.1 (Auth satırları), §14.2 rol 27, §14.3 | Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Audited Events |
| DL-6 | onaylayıcı aday sorgusu — "bu izne sahip kullanıcılar" iç ucu (g; D66/1); D56/3 muafiyet aday listesi bunun `waive-sod` + parti biçimi; AC-35…AC-41; ayrıntı: §5.4, §8.6, §8.7 (iç uç kimliği, D74/1), §14.4 | Owned Objects, Repo Scope, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria |

Tüketici paket kapısı (D53/1, **KARAR D70/3**): bu paketin metni main'de + MOD-0018 sahibinin teyidi (karar dosyasında kaynak) —
**şartlı teyit, Y-19 / KARAR D80/2** (D69–D78 sürümü, main `4c08fc75a`): tüketicilerin DL-1'ini bekletmez; D79'dan sonraki
değişiklikler (7 dil tablosu vb.) sahip teyidine ayrıca gider.
**DL-5 önkoşulu (KARAR D80/2, sahip şartı):** DL-5 kodundan önce (a) K-W5 denetim izi için defter kararı, (b) rol atama
adımlarının (rola izin bağlama, kişiye rol verme) denetim borcu kapanır (Ready-for-dev Checklist, Bölüm 7, 21); DL-1'i bekletmez.
Tüketici kapıları (D53/2): MOD-0126 / 0119 / 0120 / 0124 DL-1 kabulü → bu paketin DL-1 kodu main'de; MOD-0289 DL-1 → DL-2
(+ DL-4, D56/1); MOD-0217 DL-1 → DL-3 (+ DL-4). MOD-0126 DL-2: DL-5 paket metni (muafiyet alanları, D56/3) dilim önkoşulu;
DL-5 kodu DL-2 kabulünü **beklemez** — muafiyet yolu 403 ile kapalıyken DL-2 kabul edilir, muafiyet DL-5 main'e girince ayrı
kabul edilir (**KARAR D66/3**, D56/3'teki "DL-5 kodu main'de = DL-2 kabul kapısı" kuralının yerine). Aday sorgusu (D66/1;
D53 / D56/3 deseni): DL-6 paket metni main'de = tüketicinin onay diliminin önkoşulu, DL-6 kodu main'de = o dilimin kabul
kapısı — MOD-0126 DL-2, MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3, MOD-0217 DL-3. DL-6, DL-1'i beklemez; DL-5'e yalnız
`waive-sod` biçimi için dayanır (o biçim DL-5'e kadar kapalı-başarısız).

## Açık kararlar

| # | Konu | Seçenekler | CT önerisi | Sahip | Son tarih |
|---|---|---|---|---|---|
| K-1 | FU numarası | FU27 (doğrulayıcı `OK`) · Auth kulesinin vereceği numara · arşivdeki FU23–FU26'nın önce kayda alınması | **KAPANDI — MOD-0018 sahibi kararı (Y-19 / D80/1): FU27** — dosya adı, `id`, `branch` ve kayıt satırı yazıldı; `verify_module_id.py --check-id MOD-0018-FU27` → `OK` (2026-10-29); arşiv FU23–FU26'nın kayıt satırı yok (bu paketin işi değil) | MOD-0018 sahibi (Ali) | kapandı (2026-10-29) |
| K-1b | Treasury modül kodları (manifest `ModuleCode`) | paket başına bir kod · dört finans paketine tek kod + MDM'de paket başına | **KAPANDI — KARAR D79/1 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** paket başına bir kod (kiracı modül bazında alır); değerler tüketici paketlerin manifest adımında **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0126 DL-1 yazarı (finans), MDM sahibi (MOD-0289 / MOD-0217), MOD-0018 sahibi teyidi | DL-1 başlamadan |
| K-2 | Modül hakkıyla Admin / Viewer Treasury varsayılanı | hiçbiri · yalnız görüntüleyici kümeleri · FU20 gibi `read` | **KAPANDI — KARAR D79/2 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** hiçbiri (matris 117 anahtarın hepsini yerleştirdi) **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi + K1; teyit MOD-0018 sahibi (D79) | DL-1 başlamadan (teyit) |
| K-3 | Varsayılan kiracı SuperAdmin tam katalog yolu Treasury anahtarlarını alsın mı | bugünkü davranış (açık atama dışı hepsi) · Treasury'yi dışla | **KAPANDI — KARAR D79/3 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** bugünkü davranış (açık atama dışı hepsi; FU20 / FU22 deseni); varsayılan kiracı iş kiracısı değil **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi; teyit Auth ekiyle (D79) | DL-1 başlamadan (teyit) |
| K-4 | Bağımlı 5 anahtar rollere nasıl ulaşır (D69'un Sözleşme onaylayıcısının `platform.workflow.tasks.approve` / `.reject` alıp almayacağı dahil) | (a) ilgili modülün kendi yolu, rolde yok (b) Treasury manifestinde çapraz modül bağımlılığı olarak beyan, profil verir (ürün kimliği `DeclaredCrossModuleDependencyKeys` deseni) (c) elle atama | **KAPANDI — KARAR D70/1: (b)**; bütün onaylayıcı rollere (7, 8, 12, 15, 17, 19, 26) `platform.workflow.tasks.approve` / `.reject` çapraz modül bağımlılığı olarak (AC-3, AC-18). D70/1 iki görev anahtarını adlandırır; öteki üç bağımlı anahtarın (`mdm.legal-entities.read`, `platform.workflow.instances.start`, `platform.workflow.tasks.request-info`) aynı yolla gelmesi CT çıkarımıdır — MOD-0018 sahibinin D70/1 teyidiyle birlikte teyit edilir. **KARAR D72/3–4:** `procurement.suppliers.eligibility.read` ve `procurement.suppliers.read` aynı yolla (b) gelir; `mdm.legal-entities.read` banka rollerine (20–22) de — bağımlı anahtar sayısı 7 (Bölüm 14.2). **Ölçüm notu (açık):** `platform.workflow.*` tohum sahipli anahtarlardır (`SeedOwnedTenantScopeKeys`, FU24/25); bağımlı manifestle kapsam / sınıf çatışması ölçülmeli | MOD-0018 sahibi + MOD-0023 / MOD-0220 sahipleri; teyit MOD-0018 sahibi (D70/1) | DL-1 başlamadan (teyit) |
| K-5 | DL-4'te Platform izin listesine girecek (modül kodu, anahtar) çiftleri | MOD-0289 + MOD-0217'nin şirket kapsamı soran anahtarları (`read-tenant-wide` hariç) | hesap ve sözleşme anahtarları; banka / amaç (referans veri) kapsam sorar mı MOD-0289 sahibi söyler **Yalnız yöntem sahip teyitli — Y-21 / KARAR D84/3:** banka / amaç anahtarlarının kapsam sorup sormadığını MOD-0289 / MOD-0217 sahibi söyler; Platform dosyaları Beste'den (XMC-001). **KAPANDI — liste KARAR D88/1 (K1, MOD-0289 / MOD-0217 için sahip kararı — D19, D20/1):** MOD-0289 (9): `mdm.bank-accounts.read`, `mdm.bank-accounts.create`, `mdm.bank-accounts.update`, `mdm.bank-accounts.activate`, `mdm.bank-accounts.deactivate`, `mdm.bank-accounts.delete`, `mdm.bank-accounts.bulk-delete`, `mdm.bank-accounts.read-unmasked`, `mdm.bank-accounts.sensitive-reads.read`; MOD-0217 (14): `mdm.contracts.read`, `mdm.contracts.create`, `mdm.contracts.update`, `mdm.contracts.delete`, `mdm.contracts.bulk-delete`, `mdm.contracts.documents.read`, `mdm.contracts.documents.create`, `mdm.contracts.documents.delete`, `mdm.contracts.submit-approval`, `mdm.contracts.record-signature`, `mdm.contracts.amendments.create`, `mdm.contracts.renewal-decisions.create`, `mdm.contracts.terminate`, `mdm.contracts.archive`. Listede yok: `*.read-tenant-wide` (kapsamı atlar), `mdm.banks.*` ve `mdm.bank-account-purposes.*` (kiracı geneli referans), `mdm.contracts.versions.approve` (MDM'de uç yok; DL-6), `mdm.contracts.value.read` / `.value.write` (alan düzeyi). Beste kontrol eder (Y-22). | MOD-0289 / MOD-0217 sahibi; Beste (Platform) | DL-4 başlamadan |
| K-6 | SEC-002 eki finans tüketicisini (FinanceTreasuryService → Platform kapsam ucu) de kapsar mı | evet (ikinci kimlik bilgisi bağlaması, Bölüm 5.2 koşullu dosyalar) · hayır (dört finans paketinin O-19 / O-22 / O-14 / O-23 işi) | **DÜZELTİLDİ — "evet" (Beste, Y-23 / KARAR D92/1):** finans servisi Platform güvenilir kapsam ucunun ikinci tüketicisi; §5.2 koşullu Platform dosyaları gerekir; XMC-001 Beste'de (çift listesi + dosya-satır + sabotajlı testler gönderildi). Önceki: "hayır" (Y-22 / D88/2). | K1 + Beste | kapandı (2026-10-29; Platform kodu açık) |
| K-7 | Rol adları ve Türkçe görünen ad | `RoleName` İngilizce PascalCase + `DisplayName` İngilizce (mevcut desen, tek dil) · tr ad için rol ekranında kod → kaynak eşlemesi | **KAPANDI — KARAR D79/9 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** `RoleName` İngilizce PascalCase sabit kod; görünen ad rol ekranlarında kod → kaynak eşlemesiyle (`SharedResource` `RoleName_{RoleName}` + `RoleNames` köprüsü, Bölüm 5.5, 11) **7 dilde** (en, tr, fr, es, zh, ar, ru; Çekirdek kural 4); 27 rolün 7 dilli adlarını Treasury yazdı (Bölüm 14.2 "Görünen adlar"), MOD-0018 sahibi teyit eder; AC-7 guard testi. **Not (karar değil):** rol açıklaması (`Role.Description`, Roller ekranı hızlı görünümü) D79/9'un kapsamında değildir — açıklama yazılacaksa aynı 7 dil kuralına girer; MOD-0018 sahibinin teyidinde sorulur **D80/3: sahip teyit etti (Y-19)** — 27 rolün 7 dilli ad tablosu D79'dan sonraki değişiklik olarak sahip teyidine ayrıca gider (D80/2) | MOD-0018 sahibi; teyit Auth ekiyle (D79/9) | DL-1 başlamadan (teyit) |
| K-W1 | Atamayı onaylama izni | yeni Auth anahtarı (öneri `auth.payment-sod-waivers.assign`, `.read`; yalnız açık atama) · başka yol | **KAPANDI — KARAR D74/2–3:** iki yeni Auth anahtarı `auth.payment-sod-waivers.assign` ve `.read`, yalnız açık atama (liste 10 → 20); yalnız yeni 27. rol **Muafiyet yetkilisi** (`PaymentSodWaiverAuthority`) üzerinden; atayan bu rolü taşır, taşımayan 403, kendine atama 403. Önceki öneri "Hazine yöneticisi kişilerine / K1'e elle" kalktı | MOD-0018 sahibi + K1; teyit MOD-0018 sahibi (D74/3) | DL-5 başlamadan (teyit) |
| K-W2 | `waive-sod` izninin token'da süreli olması | (a) izin açık atamayla süresiz, kullanım ayrıca geçerli atama ister (b) atama izni token'a koyar ve bitişte düşürür (token üretimi değişir; FU11) | **KAPANDI — KARAR D79/4 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** (a) — izin açık atamayla süresiz; her kullanım ayrıca geçerli atama ister (MOD-0126 her kullanımda atamayı okur); token üretimine dokunulmaz **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi; teyit Auth ekiyle (D79) | DL-5 başlamadan (teyit) |
| K-W3 | İç sorgu ucunun kimlik doğrulaması | servis kimliği token'ı (MOD-0033-FU02, `InternalServiceIdentityTokensController`) · `X-Internal-Api-Key` (`InternalRolesController` deseni) | **KAPANDI — KARAR D74/1:** `X-Internal-Api-Key` (mevcut ortak sır deseni); kiracı istekten değil, çağrıyı başlatan kullanıcının iletilen token'ından; yeni doğrulama katmanı yok; token yok / kiracı çıkarılamazsa kapalı-başarısız (Bölüm 8.7). Servis kimliği token'ı kullanılmaz | MOD-0018 sahibi; teyit Auth ekiyle | DL-5 başlamadan (teyit) |
| K-W4 | Kiracı uçlarının kök yolu | `/api/roles/sod-waiver-assignments` (Gateway değişmez) · ayrı kök (`integration-agent`) | **KAPANDI — KARAR D79/5 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** `/api/roles/sod-waiver-assignments`; Gateway değişmez **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi; teyit Auth ekiyle (D79) | DL-5 başlamadan (teyit) |
| K-W5 | K2 denetim izi | `auth-gunlugu` izinin kabulü (okuyan uç ya da CT defter kararı) · merkezi iletimin kapalı-başarısız yolu | `auth-gunlugu` + önce-yaz; CT defter kararı **Yöntem sahip teyitli — Y-21 / KARAR D84/2:** `auth-gunlugu` + önce-yaz; **CT defter kararı açık — KARAR D86 geri alındı (D87):** K2 borcu yolu testi geçmez; önerilen yol `auth-gunlugu` okuma yolu + yol `c` kanıtı (MOD-0018 sahibine önerildi). DL-5 kodu için engel. **KARAR D97/2 (Ali, Y-26):** okuma yolu kabul (öneri 1–6; ekran evet — Roller ekranı salt-okunur liste + Geçmiş) + düzeltmeler (a) ayrı yazıcı tipi, (b) iletici anahtarı yapılandırma şartı AC'de, (c) kiracı tarafı ekran → 7 dil; **DL-5 paket metni yazıldı — KARAR D98, D99** (Bölüm 5.3, 8.5, 14.3, 21; AC-44…AC-47); **açık:** CT-APR-02 ve MOD-0018 sahibinin D79 sonrası teyidi (D99/1 teslim teyidi önerisi dahil). | Treasury CT + MOD-0018 sahibi (defter) | DL-5 başlamadan |
| K-W6 | Sorgu yanıtının sözleşme dosyası | `docs/analysis/contracts/` altında sürümlü dosya · yalnız pakette | **KAPANDI — KARAR D79/6 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** `docs/analysis/contracts/` altında sürümlü dosya; MOD-0126 sahte istemcisi ona karşı (AC-34 zorunlu) **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi + MOD-0126 yazarı; teyit Auth ekiyle (D79) | DL-5 başlamadan (teyit) |
| K-W7 | Muafiyet yetkilisi rolünün (27, KARAR D74/3) oluşturulma yolu ve izin denetimi | (a) Treasury hak profili MOD-0126 modül hakkıyla rolü izinsiz ayrılmış sistem rolü olarak oluşturur (ad çakışma ön denetimine girer); iki anahtar role açık (`Manual`) rol-izin atamasıyla bağlanır · (b) rol oluşturulmaz, yetkili kişi rolü ve iki anahtarı elle kurar · (c) Auth tohum / taşıma ile her kiracıda oluşturulur. Ek soru: atayan denetimi yalnız izinle mi (`[HasPermission("auth.payment-sod-waivers.assign")]`), yoksa izin + rol üyeliğiyle mi — anahtar elle başka role bağlanırsa rolsüz kişi de atayabilir | **KAPANDI — KARAR D75/1:** (a) — rolü Treasury hak profili izinsiz ayrılmış sistem rolü olarak oluşturur, izinler açık (`Manual`) atamayla verilir; atayan denetimi **izin + rol üyeliği** (CT önerisi "yalnız izin" seçilmedi): atayan hem `auth.payment-sod-waivers.assign` iznini hem Muafiyet yetkilisi rol üyeliğini taşır; izin başka role elle bağlansa da rolsüz kişi atayamaz (403 `SOD_WAIVER_ASSIGN_FORBIDDEN`; AC-29, AC-43). Geri alma ucuna aynı denetim **KARAR D76/1** (okuma ucu yalnız `.read`); denetim sırası izin + üyelik → kendine atama → alan doğrulama, rolsüz + kendini alıcı → 403 `SOD_WAIVER_ASSIGN_FORBIDDEN` (**KARAR D76/2**; Bölüm 8.5, AC-29) **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi; teyit Auth ekiyle (D75/1) | DL-5 başlamadan (teyit) |
| K-A1 | DL-6 iç ucunun adı, kimliği ve sözleşme dosyası | uç `GET /internal/permission-candidates` · başka ad; kimlik K-W3 ile aynı (servis kimliği token'ı) · `X-Internal-Api-Key` (`InternalRolesController` deseni); sözleşme dosyası `docs/analysis/contracts/` altında sürümlü · yalnız pakette | **KAPANDI — kimlik KARAR D74/1; uç adı ve sözleşme dosyası Y-21 / KARAR D84/1** (K-W3 ile aynı: ortak sır + iletilen kullanıcı token'ından kiracı; Bölüm 8.7). Kalan iki parça CT önerisi olarak MOD-0018 sahibinin teyidinde: uç adı `/internal/permission-candidates`; sürümlü sözleşme dosyası (hata kodları dahil) **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi + tüketici yazarları | DL-6 başlamadan (teyit) |
| K-A2 | Şirket kapsamı nerede uygulanır | (a) Auth sorguya `legalEntityId` alır ve Platform kapsam çözümünü sorar (Auth → Platform yeni bağımlılık; Platform tarafı XMC-001 Beste) · (b) Auth kiracı tabanını verir, tüketici kendi kapsam portuyla süzer | **KAPANDI — KARAR D79/7 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** (b) — Auth yalnız kiracı tabanını verir; şirket kapsamını tüketici kendi kapsam portuyla süzer (Auth'ta kullanıcı → şirket verisi yok; tüketicilerin kapsam portu zaten DL-1 kabul kapısında, D51/2; Platform'a yeni bağımlılık yok) **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | K1 + MOD-0018 sahibi; teyit Auth ekiyle (D79) | DL-6 başlamadan (teyit) |
| K-A3 | "Etkin kullanıcı" tanımının kalanı | hizmet hesabı (`AccountKind = Service`) aday mı · geçici kilitli (`LockoutEnd` gelecekte) aday mı | **KAPANDI — KARAR D79/8 (MOD-0018 sahibinin teyidine Auth ekiyle gider):** hizmet hesabı (`AccountKind = Service`) aday değil; geçici kilitli kullanıcı aday kalır (kilit geçicidir, onay görevi bekleyebilir); AC-38 **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi; teyit Auth ekiyle (D79) | DL-6 başlamadan (teyit) |
| K-A4 | Bağımlı `platform.workflow.tasks.approve` aday şartı mı | (a) yalnız modül onay anahtarı · (b) modül anahtarı + bağımlı MOD-0023 anahtarı birlikte (sorgu iki anahtar alır) | **KAPANDI — D70/1 (iş akışı izni aday şartı değildir, rol şablonuyla gelir):** (a) — sorgu yalnız modül onay anahtarını alır; K-4 (b) ile iki görev anahtarı bütün onaylayıcı rollerde birlikte bulunur | MOD-0018 sahibi + MOD-0023 sahibi; teyit MOD-0018 sahibi (D70/1) | DL-6 başlamadan (teyit) |
| K-A4b | Elle atama: modül onay anahtarı rol dışı elle verilmiş, `platform.workflow.tasks.approve` tutmayan kişi aday olsun mu (D70/1 rol şablonu yolunu kapatır, bu durumu yazmaz) | (a) tüketici aday listesini iş akışı izniyle de kesiştirir (DL-6 sorgusu `platform.workflow.tasks.approve` için ikinci çağrı; kesişim dışı kişi aday değil) · (b) kesiştirmez; onay anahtarının elle verilmesi iş akışı izninin de elle verilmesini gerektiren atama kuralı / denetim olur (rol şablonu yolu tek yol sayılır) | **KAPANDI — KARAR D71: (a)** — beş tüketici (MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0217) aday listesini `platform.workflow.tasks.approve` ile kesiştirir (ayrı çağrı, tier-2 deseni; sorgu tek anahtar alır); çağrı okunamazsa kapalı-başarısız, kesişim boşsa tüketicinin 422 dalı; D70/1 değişmez; paket kapısı D71'in main'e girmesi (D62/1) **Sahip teyit etti — Y-21 / KARAR D84/1 (2026-10-29).** | MOD-0018 sahibi + K1 (+ MOD-0023 sahibi); teyit MOD-0018 sahibi (D71) | DL-6 başlamadan (teyit) |
