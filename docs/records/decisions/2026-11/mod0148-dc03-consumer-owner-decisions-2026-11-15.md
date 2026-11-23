# MOD-0148 — DC-03 tüketici kararları (2026-11-15)

- Sahip: Natig (K1, MOD-0148 modül sahibi, MVP-6 CT). Kaynak: sahibin sohbet cevabı, 2026-11-15 (soru 1: Beste'nin önerisi kabul;
  soru 2: «Evet, denetlesin»; soru 3: varsayılandan sıkı seçenek; soru 4: MOD-0148 manifesti).
- Konu: MOD-0360-FU01 r2 (Supplier User Binding, DC-03) paketinin MOD-0148 sahibine sorduğu SQ-7, SQ-8, SQ-11, SQ-14.
  Paket: `execution/domains/platform-shared-services/module-packs/MOD-0360-FU01-supplier-user-binding.md`, sha256
  `8086bf9aefa3f8e7b3db664893149d372def69153d1552cb8149b3d05aaa2d4a` (ready-for-dev; `execution/approvals/approvals.tsv` CT-APR-01
  satırı, onaylayan Ali, 2026-11-13). Merkez Auth ekibi (Beste) geliştirmeye başladı ve seçimlerin MOD-0148 paketine yazılmasını istedi.
- Bağlama koşulu (Auth'un, değişmedi): `eligibleForTransactions = true`, `onboardingStatus = Approved`, `purchasingEnabled = true`
  (MOD-0360-FU01 D6, AC-27).

## Kararlar

- **K-1 — sabit izin kümesi (SQ-7):** `supplychain.supplier-portal.read` ve `.submit`, `supplier_user` token'ına sabit küme olarak
  girer; kiracı rolü yoktur.
- **K-2 — `amr` yeniden denetimi (SQ-8):** e-posta kodu adımını Auth zorlar; portal da token'ın `amr` claim'ini yeniden denetler.
  E-posta kodu adımı yoksa 403.
- **K-3 — `binding_rev` çağrısı (SQ-11):** portal `createOwnPortalSubmission` ve `submitOwnPortalSubmission` öncesinde Auth'un S2S
  `binding_rev` ucunu çağırır. Uyuşmazlık 403; Auth erişilemezse 503 (kapalı-başarısız). Servis adı `Diten.SupplyChain`, audience
  `TRUSTED_SUPPLIER_BINDING_CONSUMER` (değerler SQ-3, Beste 2026-11-13, `dc03-supplier-user-binding-owner-decisions-01.md`). Bu bir kod
  kapısıdır; her kabul satırının bir sabotajı vardır. Sonuç: bağın kaldırılması bir sonraki yazmada hemen etkili olur.
- **K-4 — katalog (SQ-14):** iki anahtar Auth kataloğuna MOD-0148 manifestinden girer, MOD-0360-FU01'in tedarikçi kapısı dilimi
  (DL-2) başlamadan önce.

## Ek — 2026-11-16: bağımsız inceleme cevapları

Kaynak: CT-APR-02 incelemesi `docs/records/reviews/MOD-0148/2026-11-15-75b33fcb.md` (KOŞULLU; paket `75b33fcb…`); sahibin soru
kutusu cevapları, 2026-11-16 (üçü de önerilen seçenek).

- **K-5 (F-2) — D2 kalır:** iki anahtar şirket kullanıcılarına yalnız Roller → İzinler yoluyla verilir; varsayılan rol şablonu, tam
  katalog / SuperAdmin ve modül eşitlemesi gibi otomatik yollardan **verilmez**. Bunun için Auth sahibinden (Ali) iki anahtarı
  otomatik yolların dışında tutması istenir (XMC-001; bu iş Auth dosyasına dokunmaz); P4 ondan sonra itilir.
- **K-6 (F-3) — get ve submit 404:** e-posta kodu eksikken (K-2) ve bağ eskimişken (K-3), 403 tanımlamayan kendi kaydını okuma ve
  gönderim işlemleri 404 döner; durum, liste ve oluşturma 403 kalır. Ayrıntı kodu yalnız loga yazılır. Bu, K-2 ve K-3'teki "403"ü
  bu iki işlem için daraltır.
- **K-7 (F-4) — §2d istisnası kabul, sınırlı:** iki anahtar, henüz hiçbir uç onları zorlamadan manifestle kataloğa girer. Sınır: yalnız
  bu iki anahtar, kapsam `Tenant`; gerekçe MOD-0360-FU01 DL-2 kapısı. DL-1'in ilk ucuyla ters yön testi sabotajlı koşulur.
- Zaman notu: K-4, 2026-10-26 D2'nin "anahtarlar DL-1 ile yazılır" zamanını öne alır; bu revizyonun CT-APR-01'inden sonra kalır.
- Yazar düzeltmeleri (sahip kararı gerektirmeyen): F-1 (P4 bu revizyonun onayından muaf değil; tek yazar = MVP-6 CT), F-5 (denetim
  sırası §14, D8 sapması kayıtlı), F-6 (manifest değerleri ve katalog kanıtı), F-7 (karşılaştırma türleri; yalnız Auth'un adlı
  404'ü "bağ eskidi"), F-9 (tekrar istekte de P3).

## Pakette

`execution/domains/supply-chain-execution/module-packs/MOD-0148-supplier-portal.md` metin revizyonu 2026-11-15: §0.0 "DC-03 consumer
rules" P1–P4 (K-1…K-4), "Undeclared statuses" (get ve submit'te 403), test kimlikleri T-0148-V1-PERMSET-01 / AMR-01 / BREV-01 /
CAT-01, §5, §13, §14, §16, §18, §19, "Teslim dilimleri" notu.

## Açık kalanlar

- Bu revizyonun bağımsız incelemesi (CT-APR-02) ve yeni CT-APR-01 onay satırı: 2026-10-28 satırı önceki sürüme bağlıdır.
- MOD-0360-FU01 tarafı: SQ-7, SQ-8, SQ-11, SQ-14 cevaplarının o pakete yazılması Auth sahibinin işidir (XMC-001); bu kayıt yalnız
  MOD-0148 tarafını bağlar.
- ~~§2d istisnası sahip kabulü~~ — K-7 (2026-11-16).
- Auth sahibine (Ali) iki istek (XMC-001, bu kayıt Auth'u bağlamaz): (1) iki portal anahtarını otomatik verme yollarının dışında
  tutmak (K-5); (2) AC-19 "bu kiracıda bağ yok" 404'ünün gövde kodunu adlandırmak ve listede olmayan `service_name` için AC-19
  (401) ile SQ-3 kaydını (403) hizalamak (inceleme F-7, F-8).
- Bu düzeltilmiş sürümün yeniden bağımsız incelemesi (CT-APR-02) ve CT-APR-01 satırı.
