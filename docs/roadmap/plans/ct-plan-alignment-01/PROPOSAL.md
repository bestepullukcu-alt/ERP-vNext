# CT-PLAN-ALIGN-01 — Planlama isteklerinin depoya karşı ölçümü ve uygulanan öneri

- **Durum:** öneri — çalışma ağacında uygulandı, commit yok. Onay değildir; açık kararlar `DECISIONS.md`'de.
- **Tarih:** 2026-11-01 · **Dal / taban:** `chore/agents/rule-window-02` worktree `speed-01`, HEAD `71711c6b8` (= origin/main `dee36d252` + kart commit'i); kart v1.9 → v1.10, SOP v2.5 r6 → r7.
- **Girdiler (salt okunur, depo dışı):** `Capability_Planning_v32.xlsx` (SHA-256 2b0c0084…e2bf, plan notu), `Capability_Planning_v32_Note.md`, `packs_0504c96ea.csv`, `ct_status_template_v31.csv`, `DR-PLAN-2026-10-31.md` (v1.6), `CP31_1_Review_against_DITEN_IMS_PDMS_EES_2026-11-01.md`; QMS taslağı §7 (DTN-QMS-MAN-0002 v0.7) yalnız görevdeki özetle (dosya verilmedi).
- **Okunan depo kaynakları:** `docs/guides/operations/control-tower-sop.md` (§0.1, §2.1–2.2, §17.1, §20.4, §22.1–22.2), `control-tower-operating-card.md` (Ne değişti, §0 Görev kaynağı, §8), `execution/delivery/ct-queue-registry.tsv` (9 satır), `scripts/ct_queue.py`, `scripts/ct_route.py`, `scripts/pack_approval.py`, `scripts/prompt_gate.py` (dilim okuma), `.antigravity/rules/module-pack-standard.md` (§6, §16), `capability-pack-standard.md` (§4, §8.1), `cross-module-change-consent.md`, `.antigravity/agents/orchestrator.md`, `module-pack-author.md`, `.antigravity/AGENTS-REFERENCE.md` §8, `.antigravity/adapters.json`, `.github/CODEOWNERS`, MOD-0354/0355/0357 paketleri, MOD-0117 paketi (künye), `DCP-005-management-governance-core.md`, `execution/approvals/approvals.tsv` ve `reviews.tsv` (0354/0355 araması), Blueprint master 8.8 `Blueprint_Data` (0354/0355 satırları).

## 1. Ölçüm — istekler bugünkü metne karşı

### 1.1 Plan ne tutuyor (v32 ölçümü)

| Soru | Ölçüm (v32) | Sonuç |
|---|---|---|
| Dilim (DL-n / slice) sütunu var mı? | `Modules` sayfası 48 sütun; dilim sütunu yok. "slice" yalnız MVP çıkış kapısı metninde ("runtime vertical slice"). `ct_status_template_v31.csv`'de de dilim sütunu yok. | İstek 1 (a) ve (c)'nin plan tarafı **koşullu**: "plan dilim satırı depoya girince". |
| Yetenek kimliği 1.x.y sütunu var mı? | Yok. 1.x.y yalnız iki serbest metin hücresinde: `MVPs` satır 53 ("ESBP 1.8.2, 1.8.4"), `Modules` satır 311 ("ESBP 1.6 incl. 1.6.3 … 1.6.4"). Blueprint 8.8 `Blueprint_Data` 1.x.y değil "Capability Group" adı taşır. Depoda ESBP 1.x.y numaralandırması bulunmadı. | İstek 2'nin "plan yetenek kimliği" hücresi bugün çoğu modülde `plan kimliği yok` olur. |
| MVP kimliği? | `Modules` C sütunu (ör. `BLK-10-MVP3`); `MVPs` sayfasında "MVP ID" sütunu boş, blok + numara dolu. | `plan MVP` hücresi doldurulabilir. |
| Blok sahibi? | `Blocks` C sütunu ("Block owner") 11 bloğun hepsinde boş. | (e) için kişi düzeyinde sahip yok; yalnız sahip **blok**. |
| BLK-10 sahipliği | Sahip ("Business") satırı yalnız **MOD-0352** (BLK-10-MVP1) ve **MOD-0138** (MVP2). MOD-0117, 0354, 0355, 0357, 0358, 0359 sahip satırı **BLK-05** (R&D–RA; MVP1/MVP4, "Origin: Integrated R&D–RA Lifecycle … Wave C/0"); BLK-10'da "Uses BLK-05" satırları. | İsteğin (e) maddesindeki "BLK-10 → 0117, 0354, 0355, 0352, 0138" **v32 ile uyuşmuyor** (v37 iddiası, doğrulanmadı — §6). |
| Pack durumu (packs_0504c96ea) | 0117 `review`; 0354, 0355, 0357 `ready-for-dev`. | Depoyla aynı (künye ölçümü). |

### 1.2 İstek 1 (kule tarafı)

| Madde | Bugünkü metin | Fark | Uygulanan |
|---|---|---|---|
| (a) WP dilimi = plan satırındaki paket dilimi | SOP §17.1 `Kapsanan dilim:` (r5) ve §20.4/6: dilim paketin onaylı sürümünde olmalı; `prompt_gate.py` paketin tablosuna karşı denetler. Plan hiç anılmıyor. | Planla eşleşme yok; plan depoda değil (§2.2 "repository authority DEĞİL"). | §20.4/6b-a: eşleşme kuralı, plan dilim satırı depoya girene kadar koşullu; fark sahibe sorulur, dağıtım durmaz. |
| (b) Durum dilim bazında | Kart S-61b: ilerleme "parça = onaylı paketin teslim dilimi"; takip sayfasında dilim cetveli (§22.2/5). REP-001 tablosu modül satırı, "kapsam cümlesi" serbest. | Çoğu var; REP-001 kapsam cümlesi dilimi adlandırmıyor. | §20.4/6b-b + §22.1'e "kapsam cümlesi dilimi adlandırır; sütunlar değişmez". REP-001 dosyası (`status-reporting-and-evidence.md`) izinli listede değil — dokunulmadı. |
| (c) Kayıt kapsamı dilimleri adlandırır | CAP-001 §8.1 + `ct_route.py`: `dcp:<yol>#<Slice>` makine okunur; diğer `scope` serbest metin. 9 satırdan 4'ü (pss-ui, sce-mvp5-wms, pss-wf, mg) DL-n'yi serbest metinde zaten yazıyor; pvg, treasury-intake, hcm-employee, hcm-leave, sce-mvp6 yazmıyor. | `MOD-nnnn DL-n` biçimi yok; araç okumaz. | §20.4/6b-c ve kart §0 "Görev kaynağı": yalnız **yeni** satırlara; var olan satırlar değişmez. |
| (d) 0354 DWS ve 0355 BPM için kuyruk | `mg` satırı: "MOD-0357 … only … MOD-0354 and MOD-0355 of the same domain are not in this queue". 0354/0355'in kuyruğu yok; paketlerde `Teslim dilimleri` tablosu yok; `approvals.tsv`/`reviews.tsv`'de satır yok; paketler en son 2026-09-02'de değişti (CT-APR-01 başlangıcından önce). DCP-005 (approved) üyeleri 0352, 0117, 0354, 0355; §8.1 Delivery slices tablosu yok (dcp: yönlendirmesi bugün mümkün değil). | Geçerli kayıt satırı için tabanda var olan bir kuyruk dosyası, sahip `kuyruk-onay`ı ve dilim tablosu gerekir; hiçbiri yok. | §20.4/6b-d ("bir dilim bir kuyruk"; kuyruksuz modül "kuyruk yok" diye yazılır) ve kart §8 satırı. **Kayıt satırı eklenmedi** (§3). |
| (e) Kayıt ile plan sahip blokta anlaşır | Kart §8: management-governance "Belirsiz" (eski: `mg` kaydı 0357 için var). XMC-001 plan "Uses"u bilmiyor. | Plan sahipliği depoda yok; v32 ile istek farklı. | §20.4/6b-e, kart §0 (`evidence` sahip blok + satır türü ya da "plan satırı yok"), kart §8 satırı (kayıt kanıtıyla), XMC-001 §2 maddesi. |

### 1.3 İstek 2 (.antigravity tarafı)

| Madde | Bugünkü metin | Uygulanan |
|---|---|---|
| Dilim tablosu plan yetenek kimliği + plan MVP | `module-pack-standard` §6 satır 22, §16: `| dilim | ad | kapsadığı bölümler |`; kart S-85 kanıt seviyesi sütununu ister ama standart şablonda yok. Araçlar yalnız ilk sütunu okur (`pack_approval.py` `SLICE_ROW = ^\|\s*(DL-\d+)\s*\|`). | §16 "Plan hizalaması": altı sütun (kanıt seviyesi dahil), değer yoksa `plan kimliği yok` / `plan satırı yok`; yalnız 2026-11-01'den sonra ilk kez yazılan ya da dilim eklenen tablo. |
| DL-1 çekirdek | Tanım yok; MOD-0357'de DL-1 = "S1–S12 içi düzeltmeler". | DL-1 = en küçük çalışan kayıt; diğer dilimler ona dayanır, dayanmayan `DL-1'den bağımsız` yazılır; geriye dönük değil. |
| module-pack-author plan satırlarına göre yazar | Ajan Blueprint, registry, master-development-plan okur; plan yok. | Kesin Kural 9; plan satırı verilmediyse `plan satırı yok`. |
| @orchestrator reddeder | Kapı yalnız `status` (`approved`/`ready-for-dev`). | Kural 2 altına "Dilim kapısı": onaylı sürümde olmayan dilim ve (DL-1 değil + bağımsız değil + DL-1 kabul kaydı yok) → ret; eski tabloda yalnız ilk koşul. |
| Büyük artış FU | FU kimlik biçimi var (§1), eşik yok. | Yeni plan MVP'si ya da yeni yetenek kimliği → `MOD-NNNN-FUxx` (DCP-002 `--parent`); altı yeni dilim + yeni onay. |
| Yetki sırası | §17 Module Pack > Domain Config > AGENTS.md > `.antigravity/`. | Korundu; plan sıranın parçası değil, farkta paket kazanır. |
| DCP | CAP-001 §8.1 başlığı `| Slice | Kapsam | Not |` birebir okunur (`ct_route.py` `SLICE_COLUMNS`). | Plan MVP'si `Not` hücresine; başlık aynı. |
| AGENTS.md | Değişmedi. | Önerilen atıf §5. |

## 2. Değişiklikler ve gereken onay

Satır numaraları değişiklikten sonraki dosyaya göredir; eski/yeni metnin tamamı §9'daki farktadır.

| # | Dosya:satır | Ne değişti | Gereken onay |
|---|---|---|---|
| 1 | `docs/guides/operations/control-tower-sop.md:68-73` | §0.1'e r7 notu | Sahip kural onayı (haftalık kural penceresi, kart S-100); CODEOWNERS kapsamında değil |
| 2 | `control-tower-sop.md:926` | §17.1 `Kapsanan dilim` yorumuna r7 atfı (§36.1 şablonuna dokunulmadı; `prompt_gate` §36.1'i okur) | aynı |
| 3 | `control-tower-sop.md:1452-1470` | §20.4/6b (a)–(e) | aynı |
| 4 | `control-tower-sop.md:1587` | §22.1 kapsam cümlesi dilimi adlandırır | aynı |
| 5 | `docs/guides/operations/control-tower-operating-card.md:1,3` ‡ | kart v1.10, SOP r7 | aynı |
| 6 | `control-tower-operating-card.md:12-16` ‡ | "Ne değişti" satırı (kayıt defteri numarası verilmedi; CT-Setup defterinde son S-125) | aynı |
| 7 | `control-tower-operating-card.md:481-484` ‡ | §0 "Görev kaynağı": yeni satırda `scope` ve `evidence` kuralı | aynı |
| 8 | `control-tower-operating-card.md:911` ‡ | §8 management-governance: Belirsiz → kısmi (`mg` → MOD-0357; 0354/0355 kuyruk yok) | aynı |
| 9 | `.antigravity/rules/module-pack-standard.md:283, 539-555` | §6 satır 22, §16 "Plan hizalaması" | **sahip onayı gerekir** (`.antigravity/**`, AGENTS.md §4) |
| 10 | `.antigravity/agents/module-pack-author.md:23` | Kesin Kural 9 | **sahip onayı gerekir** |
| 11 | `.antigravity/agents/orchestrator.md:19` | Kural 2 altına dilim kapısı | **sahip onayı gerekir** |
| 12 | `.antigravity/rules/capability-pack-standard.md:85-88` | §8.1 `Not` = plan MVP; tek modül `scope` biçimi | **sahip onayı gerekir** |
| 13 | `.antigravity/rules/cross-module-change-consent.md:47-48` | §2: plan "Uses" modülü başka modüldür | **sahip onayı gerekir** |
| 14 | `.antigravity/AGENTS-REFERENCE.md:134` | §8 Yol B adım 2 | **sahip onayı gerekir** |
| 15 | `.claude/agents/module-pack-author.md`, `.codex/agents/module-pack-author.toml` | `sync_adapters.py` üretti (elle değil) | 10 ile birlikte |
| 16 | `docs/roadmap/plans/ct-plan-alignment-01/PROPOSAL.md`, `DECISIONS.md` | yeni belge | sahip okuması |

- ‡ **Kart değişiklikleri commit'lenmiş durumda:** bu turda başka bir oturum `5fa20aa8b` ("rapor — günün biteni…", S-126) commit'ini attı ve kart dosyasını bütünüyle aldı; 5–8 numaralı satırlar o commit'in içinde, çalışma ağacında değil. Bu commit'i ben atmadım. Ayrıca S-126 numarası artık o maddede; bu öneri kart maddesine numara vermedi.
- Sahip bu işi "yap, tek PR'da" diye onayladı; yukarıdaki satırlar yine de dosya başına ayrı onay noktasıdır (DECISIONS K-1).
- **Kuyruk-onay:** bu turda kuyruk satırı yok, kuyruk-onay gerekmiyor. 0354/0355 kuyruğu açılırsa satırın sahibi `kuyruk-onay` verir (K-2, K-3).
- **CODEOWNERS:** `.github/CODEOWNERS` yalnız `.github/`, `execution/approvals/` ve beş betiği kapsar; bu PR'daki hiçbir dosya kapsamda değil, CODEOWNERS incelemesi zorunlu değil. 0354/0355 paket onayı yazılırsa `execution/approvals/` satırları CODEOWNERS incelemesi ister.
- **Paket onayı (CT-APR-01/02):** 0354/0355'e `Teslim dilimleri` eklemek paketi değiştirir → ilk onay HIGH + aynı sürümün bağımsız incelemesi (SOP §20.4/1–4); paketi CT değil `module-pack-author` yazar.

## 3. Yeni kayıt satırları

**Eklenmedi.** Ölçüm: `ct_queue.py` `check_registry` her satırda tam 40 haneli `baseline_commit`, o commit'te var olan kuyruk dosyası ve `owner_confirmation` ∈ {pending, confirmed} ister. 0354/0355 için kuyruk dosyası yok ve commit yasak; satır eklemek `validate`'i kırar ve kaydı geçersiz kılar (bir bozuk satır bütün kaydı yürürlükten düşürür, `in_force_status`). Kuyruk açılmasına karar verilirse önerilen satır biçimi (yer tutucular `<…>`):

```text
queue_id	control_tower	path	baseline_commit	owner_confirmation	scope	evidence	strict	vocabulary
mg-core	<kule adı, K-3>	execution/domains/management-governance/work-packs/mg-core/CT-QUEUE.tsv	<kuyruk dosyasının 40 haneli commit'i>	pending	MOD-0354 DL-1…DL-<n>, MOD-0355 DL-1…DL-<m> — DCP-005 slices 5 (DWS Wave 1) and 6 (BPM process-definition); MOD-0357 stays in mg, MOD-0117 and MOD-0352 not in this queue (XMC-001); not a shared source for other Control Towers	queue file <sha> (<tarih>, <n> rows, all DISCOVERED); packs MOD-0354 / MOD-0355 with Teslim dilimleri, review + approval rows <…>; plan v32: owner block BLK-05 (R&D–RA), BLK-10 Uses — v37 not verified; owner confirmation <tarih> (kuyruk-onay)	yes	sop7
```

DCP-005'e `## 8.1 Delivery slices` tablosu eklenirse `scope` `dcp:execution/portfolio/delivery-capability-packs/DCP-005-management-governance-core.md#<Slice>` ile başlar ve `ct_route.py` okur; DCP değişikliği de CT-APR-02 incelemesi ister.

## 4. Çakışma listesi

| # | Çakışma | Kaynak | Bu öneride |
|---|---|---|---|
| Ç-1 | Plan depo dışında; SOP §2.2 depo dışı kaynağı kapı yapmaz. | SOP §2.1–2.2 | Plan maddeleri koşullu; CT ölçer, sorar, durdurmaz. |
| Ç-2 | Plan yetki sırasında yok (Module Pack > Domain Config > AGENTS.md > `.antigravity/`). | AGENTS.md §1, module-pack-standard §17 | Farkta paket kazanır; paket plana göre kendiliğinden değişmez. |
| Ç-3 | CEO kararı 3 (1 Kasım): DR-PLAN evi ayrı depo, ERP vNext'e commit yok. Plan satırlarını depoya almak bu kararla çelişebilir. | v32 notu, "CEO decisions" 3 | K-4: plan dilim satırları depoya girecek mi — CEO/planlama kararı. |
| Ç-4 | İstek (e) BLK-10 sahipliği v32'de yok: 0117/0354/0355 sahibi BLK-05. | v32 `Modules` | v37 doğrulanana kadar kayda "v32: BLK-05 sahip, BLK-10 Uses; v37 doğrulanmadı" (K-5). |
| Ç-5 | `mg` satırı 0354/0355'i açıkça dışlıyor; var olan satır bu işte değişmez; yeni satır kuyruk dosyası ve commit ister. | registry `mg` | Satır eklenmedi; K-2/K-3. |
| Ç-6 | 0354/0355'te dilim tablosu yok; eklemek ilk onay HIGH + bağımsız inceleme; DCP-005'te §8.1 yok. | SOP §20.4, approvals.tsv | Kuyruk açılırsa sıra: paket dilimleri → inceleme → onay → kuyruk dosyası → kayıt PR'ı. |
| Ç-7 | "DL-1 çekirdek" ile MOD-0357 (DL-1 = düzeltmeler; kuyruk DL-13…DL-28). | MOD-0357 §26 | Yalnız yeni tablolara; geriye dönük değil (K-7). |
| Ç-8 | "DL-1 önce" ile S-117 (dilimler sırayla, bağımsızlar en çok ikisi paralel). | kart S-117, S-99 | DL-1 kabulünden sonra paralel serbest; `DL-1'den bağımsız` işaretli dilim beklemez. |
| Ç-9 | FU kuralı ile tek pakette 28 dilim (MOD-0357). | MOD-0357 | Geriye dönük değil; eşik K-8. |
| Ç-10 | `ct_route.py` DCP §8.1 başlığını birebir okur; yeni sütun aracı kırar. `MOD-nnnn DL-n` biçimini hiçbir araç okumaz. | `ct_route.py` `SLICE_COLUMNS` | Plan MVP `Not` hücresinde; yeni biçim serbest metin. Araçla denetim yok — "sabotaj kanıtı olmayan guard guard değildir" (Çekirdek 10): bu maddeler kapı değil, CT'nin ölçtüğü kural; araç desteği takip işi. |
| Ç-11 | REP-001 tablosu `status_report.py` ile üretilir, dilim adı üretmez; REP-001 dosyası izinli listede değil. | SOP §22.1 | Yalnız SOP notu; araç/REP-001 değişikliği takip işi (başka sahip dosyası: sorulacak). |
| Ç-12 | Orchestrator "onaylı sürüm" CT-APR-01 SHA'lı onaydır; 2026-10-19 öncesi değişmemiş paketlerde (0354/0355) onay satırı yok. | CT-APR-01 | Dilim kapısı yalnız `Teslim dilimleri` tablosu olan pakette; tablo eklemek zaten onay ister. |
| Ç-13 | "Çekirdek dilim onaylanmamış": onay pakete (tüm sürüme) bağlıdır, dilime değil; dilim kabul edilir. | SOP §20.4/4, §29.5 | Uygulanan yorum: DL-1'in **kabul kaydı**; K-6. |
| Ç-14 | Planın CT içe aktarma şablonunda (v31) dilim sütunu yok; dilim bazında durum plana aktarılamaz. | `ct_status_template_v31.csv` | Planlama aracının işi (DR-PLAN v1.7 / araç v32 taslağı); bizde değişiklik yok. |
| Ç-15 | Kural değişikliği haftalık pencereden ve yalnız origin/main'e girince yürürlükte. | kart S-100, "Yürürlük" | Bu dal kural penceresi dalı; birleşene kadar r6 geçerli. |
| Ç-16 | 0357/0358/0359 sahip bloğu planda açık (istek); v32'de BLK-05. Kart §8'de MG kısmi yazıldı (kayıt kanıtı), plan sahipliği yazılmadı. | v32, kart §8 | K-5. |

## 5. AGENTS.md'ye önerilen atıf (uygulanmadı)

§6.1 Kural Haritası'nda Module Pack satırına ya da §7'ye tek cümle: "`Teslim dilimleri` plan satırlarına göre yazılır; DL-1 çekirdek, büyük artış takip paketi — `module-pack-standard` Bölüm 16 'Plan hizalaması'." AGENTS.md 21 877 / 24 000 bayt (uyarı 22 000); cümle ~170 bayt, uyarı eşiğini aşar — boyut bütçesi kararı gerekir (K-9).

## 6. IMS–EES çelişkisi (yalnız rapor — kural değiştirilmedi)

QMS taslağı §7 (DTN-QMS-MAN-0002 v0.7, görevdeki özet): IMS için kabul yalnız adı belirli bir kişi onaylayınca sayılır (D-12); yapay zekâ teknik incelemesi (D-13) ve onay satırı uygulaması (D-16) GQD değerlendirmesi ve CEO imzası olmadan IMS'te geçerli değil; "uygulayan ≠ onaylayan istisnasız".

Bugünkü EES kuralları (kart v1.9 / SOP r6) ile gerilim:

| EES kuralı (bugün) | IMS taslağı | Gerilim |
|---|---|---|
| Dal onayı (S-115): onay ve inceleme satırları CT dalında; dağıtım kapısı ve kabul bunları daldan okur; doğrulanmış ikinci insan ancak modül PR'ı birleşince gelir; o ana kadar onaylayan kanıtı sahibin sohbetteki «onayla» sözü ve `note`. | D-12: adı belirli kişinin onayı; D-16 onay satırı uygulaması GQD + CEO imzası olmadan geçersiz. | Dağıtım ve kabul, IMS anlamında doğrulanmış onaydan önce yürür. |
| Bağımsız inceleme (S-116, S-113): geçmişsiz Claude alt ajanı inceleme satırı yazar ve sayılır; HIGH'da bağımsız son bakış alt ajan. | D-13: AI teknik incelemesi IMS'te geçerli değil (GQD değerlendirmesine kadar). | EES'te sayılan inceleme IMS'te kanıt değil. |
| Tek söz «onayla» (S-117): paket onayı + bütün dilimlerin sırayla başlaması; kule kabul kaydını kendi yazar (K13, `acceptance_record`). | "Uygulayan ≠ onaylayan istisnasız". | CT hem işi yürüten hem kabul kaydını yazan taraf; dilim kabulü adı belirli kişiye bağlı değil. |
| S-41 kalktı: onaylayan ile birleştiren aynı kişi olmak zorunda değil; satırı yazanın kişisi birleştiremez. | Uygulayan ≠ onaylayan. | EES ayrımı yazan/birleştiren üzerinden; IMS ayrımı uygulayan/onaylayan üzerinden — aynı şey değil. |

Bağlam (v32 notu, CEO kararları 1 Kasım): (1) Module Pack ve DCP **EES** dosyalarıdır, IMS/PDMS işiyle değişmez; (4) plan tamamlanması D-12 adlı-kişi onayını bekler (araç v32 taslağı). Yani çelişki EES kayıtlarının IMS kanıtı sayılıp sayılmadığındadır; EES kuralları yürürlükte kalır. Karar GQD + CEO'nundur (K-10). Bu öneri IMS ile ilgili hiçbir kuralı değiştirmez; yeni "orchestrator ret" ve "DL-1 kabul kaydı" koşulları da EES kabulüdür, IMS kabulü değildir.

## 7. v32 – v37 farkı

- Elimizdeki plan **v32**'dir (1 Kasım 2026, "data only"; araçlar `refresh_v31.py`, `import_ct_status_v31.py`; DR-PLAN v1.6 paket içinde, v1.7 taslak ve araç v32 taslağı v32'de yok). İstek **v37**'yi anıyor; v37 verilmedi, hiçbir v37 iddiası doğrulanmadı.
- İstekteki v37 iddiaları ile v32 farkı: (i) BLK-10 sahipliği 0117/0354/0355/0352/0138 — v32'de yalnız 0352/0138 BLK-10 sahibi, diğerleri BLK-05 sahibi, BLK-10 Uses; (ii) 0357/0358/0359 "sahibi açık" — v32'de BLK-05 sahip; (iii) dilim ve 1.x.y yetenek kimliği — v32'de sütun yok; v37'de varsa (a), (c) ve İstek 2'nin plan hücreleri koşulsuz hâle gelebilir.
- v37 gelince yeniden ölçülecekler: sahip bloklar, dilim sütunu, yetenek kimliği sütunu, CT içe aktarma şablonu sütunları, blok sahibi (kişi) alanı.

## 8. Doğrulama

- `python3 .antigravity/scripts/sync_adapters.py` → yazdı; `--check` → "OK 13 adapter(s) match their sources" (OVERSIZE yok).
- `python3 scripts/tests/test_ct_card_commands.py` → 3 test OK.
- `python3 scripts/prompt_gate.py --self-test` → 179/179 passed, exit 0 (SOP §36.1 şablon testi dahil; §36.1 değişmedi).
- `python3 scripts/ct_queue.py validate` → 25 dakikada bitmedi, sonuç yok (kayıt dosyası bu işte değişmedi, `git diff` boş; doğrulama PR öncesi yeniden koşulmalı).


## 9. Eski / yeni metin (uygulanan fark, `git diff -U0`)

`-` eski, `+` yeni; satır numaraları `@@` başlıklarında.

Kart farkı (`git diff -U0 71711c6b8 5fa20aa8b`; yalnız bu önerinin satırları değil, aynı commit'teki S-126 satırları da görünür):

```diff
diff --git a/docs/guides/operations/control-tower-operating-card.md b/docs/guides/operations/control-tower-operating-card.md
index 194c91b36..a3f2e002b 100644
--- a/docs/guides/operations/control-tower-operating-card.md
+++ b/docs/guides/operations/control-tower-operating-card.md
@@ -1 +1 @@
-# CONTROL TOWER — Daily Operating Card v1.9
+# CONTROL TOWER — Daily Operating Card v1.10
@@ -3 +3 @@
-> **Purpose:** günlük Control Tower turu için hızlı gate. Yeni authority yaratmaz. Ayrıntı için [CONTROL TOWER SOP](./control-tower-sop.md) (v2.5 r6).
+> **Purpose:** günlük Control Tower turu için hızlı gate. Yeni authority yaratmaz. Ayrıntı için [CONTROL TOWER SOP](./control-tower-sop.md) (v2.5 r7).
@@ -11,0 +12,8 @@
+- **2026-11-01 · dilim, plan ve kuyruk hizalaması (r7, planlama tarafının isteği; öneri ve açık kararlar
+  `docs/roadmap/plans/ct-plan-alignment-01/`):** iş, durum ve yeni kuyruk kaydı modül dilimiyle (`MOD-nnnn DL-n`)
+  adlandırılır; bir dilim tek kuyrukta; plandaki sahip blok kayıtla anlaşır, plan "Uses" satırı başka modüldür (XMC-001);
+  plan dosyası depoda olmadığı için plan eşleşmesi bugün yalnız ölçülür ve sorulur, dağıtımı durdurmaz (SOP §20.4/6b;
+  §0 "Görev kaynağı", §8). Var olan kayıt satırları değişmez.
+- **2026-11-01 · rapor: günün biteni, bekleme süresi, ekran satırı (S-126, sahip kararı 2026-11-01):** kısa haberin Biten'i
+  yalnız son mesajdan beri bitenler; `Bekliyor:` satırı kaç gündür beklediğini yazar; tam raporda "Ekranda ne zaman
+  görürsünüz" zorunlu ("Sahibe rapor ve mesaj biçimi" 7, tam rapor kuralı 5).
@@ -280,0 +289,4 @@ sade dille yazılır; teknik ayrıntı (test sayıları, sha, commit, dosya yolu
+  7. **Günün biteni ve bekleme süresi (S-126, sahip kararı 2026-11-01 `rapor-ekle`):** kısa haberin **Biten** başlığı
+     yalnız son mesajdan bu yana bitenleri yazar; eski bitenler takip sayfasında durur, mesajda birikmez. Her
+     `Bekliyor:` satırı kaç gündür beklediğini yazar: `- Belge — Bekliyor: Beste — modül PR'ı, 2 gün` (ölçülür: isteğin
+     yazıldığı tarihten bugüne). Tam raporda "Ekranda ne zaman görürsünüz" satırı zorunludur (aşağıdaki kural 5).
@@ -341,2 +353,2 @@ Kurallar:
-5. **"Ekranda ne zaman görürsünüz"** her raporda yazılır; ekranı olmayan modülde ne görüleceği söylenir (ör. "her PR'da
-   otomatik kontrol").
+5. **"Ekranda ne zaman görürsünüz"** her raporda **zorunludur** (S-126, 2026-11-01); satır yoksa rapor eksiktir. Ekranı
+   olmayan modülde ne görüleceği söylenir (ör. "her PR'da otomatik kontrol").
@@ -469 +481,4 @@ Kurallar:
-  `scope` serbest metindir, yönlendirme verisi değildir.
+  `scope` serbest metindir, yönlendirme verisi değildir. **Yeni satırda (r7) `scope` dilimleri adlandırır:** DCP
+  diliminde `dcp:` önekiyle, tek modülde `MOD-nnnn DL-a…DL-b` ile başlar; aynı dilim başka bir kayıtlı kuyrukta
+  olamaz; satırın `evidence` alanı plandaki sahip bloğunu ve satır türünü (sahip / "Uses") yazar ya da "plan satırı
+  yok" der (SOP §20.4/6b-c, -d, -e).
@@ -896 +911 @@ Aynı depoda birden çok CT ve başka ekipler aynı anda çalışır. Şu dosyal
-| management-governance | Belirsiz — sahip ataması bekliyor | `domain-config.md`'deki "Control Tower Gate 2" bir kapıdır, sahiplik değildir |
+| management-governance | **kısmi**: CT · Toplantılar → MOD-0357 (kayıt satırı `mg`); MOD-0354 ve MOD-0355 paket durumu `ready-for-dev`, kuyruk yok (SOP §20.4/6b-d); diğer modüller Belirsiz | `execution/delivery/ct-queue-registry.tsv@dee36d252` satır `mg` · `domain-config.md`'deki "Control Tower Gate 2" bir kapıdır, sahiplik değildir · plan sahipliği (sahip blok) bu tabloya ancak plan satırı depoya girince yazılır |
```

Diğer dosyalar (çalışma ağacı):

```diff
diff --git a/.antigravity/AGENTS-REFERENCE.md b/.antigravity/AGENTS-REFERENCE.md
index ffd2e7e96..90da2a6b6 100644
--- a/.antigravity/AGENTS-REFERENCE.md
+++ b/.antigravity/AGENTS-REFERENCE.md
@@ -134 +134 @@ python3 .antigravity/scripts/verify_datatable_page.py . --area {AreaName} --modu
-2. `module-pack-author` ajanı `execution/domains/{domain}/module-packs/{ID}-{slug}.md` dosyasını `status: draft` ile üretir
+2. `module-pack-author` ajanı `execution/domains/{domain}/module-packs/{ID}-{slug}.md` dosyasını `status: draft` ile üretir; `Teslim dilimleri` tablosu plan satırlarına göre yazılır (her dilimde plan yetenek kimliği ve plan MVP'si, DL-1 çekirdek; pakette olmayan yeni plan MVP'si takip paketi `MOD-NNNN-FUxx`) — [module-pack-standard](rules/module-pack-standard.md) Bölüm 16 "Plan hizalaması"
diff --git a/.antigravity/agents/module-pack-author.md b/.antigravity/agents/module-pack-author.md
index 44e5a3adc..6cbbe2e66 100644
--- a/.antigravity/agents/module-pack-author.md
+++ b/.antigravity/agents/module-pack-author.md
@@ -22,0 +23 @@ Sen ERP-vNext modül sözleşmesi hazırlama ajanısın. Görevin geliştirme ya
+9. **Dilimler plan satırlarına göre (2026-11-01):** `## Teslim dilimleri` tablosunu modülün plan satırlarından yaz: her dilim plan yetenek kimliğini (`1.x.y`) ve hizmet ettiği plan MVP'sini adlandırır, DL-1 çekirdek dilimdir; plan satırı sana verilmediyse (plan dosyası depoda değildir) `plan satırı yok` yaz, değer uydurma. Paketin hiçbir diliminde olmayan yeni plan MVP'si ya da yetenek kimliği takip paketidir (`MOD-NNNN-FUxx`). Kural ve sütunlar: `module-pack-standard.md` Bölüm 16 "Plan hizalaması".
diff --git a/.antigravity/agents/orchestrator.md b/.antigravity/agents/orchestrator.md
index db91e804b..4f6027902 100644
--- a/.antigravity/agents/orchestrator.md
+++ b/.antigravity/agents/orchestrator.md
@@ -18,0 +19 @@ Alt ajanları koordine ederken HİÇBİR AJANIN inisiyatif almasına izin vereme
+    - **[RULE] Dilim kapısı (2026-11-01):** `## Teslim dilimleri` tablosu olan pakette bir WP'yi şu durumlarda **reddet** ve kullanıcıya nedenini söyle: WP'nin dilimi paketin onaylı sürümünde (CT-APR-01 onay satırının bağlı olduğu sürüm) yoksa; WP DL-1 değilse, dilimi `DL-1'den bağımsız` diye yazılmamışsa ve DL-1'in kabul kaydı yoksa. Pakette olmayan yeni plan MVP'si ya da yetenek kimliği getiren iş takip paketidir (`MOD-NNNN-FUxx`); onu WP'ye sığdırma. Eski, tablosu bu kuraldan önce yazılmış paketlerde yalnız ilk koşul uygulanır. Kural: `module-pack-standard.md` Bölüm 16 "Plan hizalaması".
diff --git a/.antigravity/rules/capability-pack-standard.md b/.antigravity/rules/capability-pack-standard.md
index 6ff50bd36..1b69c3677 100644
--- a/.antigravity/rules/capability-pack-standard.md
+++ b/.antigravity/rules/capability-pack-standard.md
@@ -85 +85,4 @@ Okuyan araç `scripts/ct_route.py`'dir. Bölümü olmayan DCP'ler ve önekle ba
-bölüm yeni bir durum eklemez; yetki §5'teki `status`'tan gelir (`approved` ve sonrası).
+bölüm yeni bir durum eklemez; yetki §5'teki `status`'tan gelir (`approved` ve sonrası). 2026-11-01'den sonra yazılan
+tabloda `Not` hücresi dilimin hizmet ettiği plan MVP'sini (ör. `BLK-10-MVP3`) ya da `plan satırı yok` yazar; başlık
+`| Slice | Kapsam | Not |` aynen kalır (araç başlığı birebir okur). Tek modül kuyruğu `scope`'u `MOD-nnnn DL-a…DL-b`
+ile adlandırabilir; bu biçim serbest metindir, araç okumaz (SOP §20.4/6b).
diff --git a/.antigravity/rules/cross-module-change-consent.md b/.antigravity/rules/cross-module-change-consent.md
index 9f353972c..2bc797a84 100644
--- a/.antigravity/rules/cross-module-change-consent.md
+++ b/.antigravity/rules/cross-module-change-consent.md
@@ -46,0 +47,2 @@ gören ajan:
+- Planın (Capability Planning) bir bloğa **"Uses"** satırıyla bağladığı modül: o bloğun kulesi için başka modüldür;
+  sahibi plandaki sahip bloğudur. Plan, kuyruk kaydı ve domain sahipliği farklıysa sorulur (SOP §20.4/6b-e).
diff --git a/.antigravity/rules/module-pack-standard.md b/.antigravity/rules/module-pack-standard.md
index 1ce794f59..45c1b5be0 100644
--- a/.antigravity/rules/module-pack-standard.md
+++ b/.antigravity/rules/module-pack-standard.md
@@ -283 +283 @@ Frontmatter altinda asagidaki bolumler zorunludur:
-| 22 | `Teslim dilimleri` | CT-APR-02'den sonra inceleme isteyen pakette zorunlu: `| DL-n | ad | kapsadığı bölümler |`; her WP bir ya da birkaç dilim alır, paketin tamamını tek WP alamaz (istisna: onay satırında `whole_pack=<WP>`). Bkz. Bölüm 16 "Onaydan önce bağımsız inceleme" |
+| 22 | `Teslim dilimleri` | CT-APR-02'den sonra inceleme isteyen pakette zorunlu: `| DL-n | ad | kapsadığı bölümler |`; her WP bir ya da birkaç dilim alır, paketin tamamını tek WP alamaz (istisna: onay satırında `whole_pack=<WP>`). Bkz. Bölüm 16 "Onaydan önce bağımsız inceleme". 2026-11-01'den sonra ilk kez yazılan tabloda her dilim plan yetenek kimliğini ve plan MVP'sini adlandırır, DL-1 çekirdek dilimdir: Bölüm 16 "Plan hizalaması" |
@@ -538,0 +539,17 @@ Bir modulun **gerceklesme durumu degistiginde** (frontend eklendi, test eklendi,
+### Plan hizalaması, çekirdek dilim ve takip paketi (2026-11-01; öneri `docs/roadmap/plans/ct-plan-alignment-01/`)
+
+- **Kapsam.** 2026-11-01'den sonra ilk kez `## Teslim dilimleri` tablosu yazılan ya da tablosuna yeni dilim eklenen
+  paket. Var olan tablolar ve dilim numaraları geriye dönük değişmez (ör. DL-1'i düzeltme dilimi olan eski paketler).
+- **Sütunlar.** `| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |`.
+  Araçlar yalnız ilk sütunu (`DL-n`) okur; ek sütunlar onların okumasını değiştirmez. `plan yetenek kimliği` plan
+  satırındaki `1.x.y` kimliğidir; plan vermiyorsa `plan kimliği yok`. `plan MVP` plan satırındaki MVP kimliğidir
+  (ör. `BLK-10-MVP3`); modülün plan satırı yoksa `plan satırı yok`. Değer uydurulmaz; kaynağı (plan sürümü ve satırı)
+  `Implementation Notes`'a yazılır.
+- **DL-1 çekirdek dilimdir:** modülün en küçük çalışan kaydı (kimlik, kalıcı veri, yetki, kiracı ve denetim). Diğer
+  dilimler DL-1'e dayanır; DL-1'e dayanmayan dilim `ad` hücresinde `DL-1'den bağımsız` diye yazılır.
+- **Büyük artış takip paketidir.** Onaylı bir pakete, paketin hiçbir diliminde olmayan yeni bir plan MVP'si ya da yeni
+  bir plan yetenek kimliği getiren dilim eklenmez; `MOD-NNNN-FUxx` takip paketi açılır (DCP-002 ön kontrolü
+  `--parent` ile). Bu sınırın altındaki ekleme aynı pakette yeni dilim ve yeni onaydır (CT-APR-01).
+- **Yetki sırası korunur.** Plan bu sıranın parçası değildir (depoda değildir): plan satırı ile paket farklıysa paket
+  kazanır, fark sahibe sorulur; paket plana göre kendiliğinden değiştirilmez.
+
diff --git a/docs/guides/operations/control-tower-sop.md b/docs/guides/operations/control-tower-sop.md
index 35fc4bf7e..4e22fd0c3 100644
--- a/docs/guides/operations/control-tower-sop.md
+++ b/docs/guides/operations/control-tower-sop.md
@@ -67,0 +68,6 @@ records in `docs/records/decisions/2026-10/`):
+Revision r7 (2026-11-01, planlama tarafının iki isteği; öneri ve açık kararlar
+`docs/roadmap/plans/ct-plan-alignment-01/PROPOSAL.md`, `DECISIONS.md`): §20.4/6b eklendi — WP dilimi, durum raporu ve
+kuyruk kapsamı modül dilimiyle (`MOD-nnnn DL-n`) adlandırılır; plan satırıyla eşleşme, plan dilim satırı depoya girene
+kadar koşulludur (plan dosyası depoda değildir, §2.2); bir dilim tek kuyrukta; plandaki sahip blok kayıtla anlaşır,
+"Uses" satırı XMC-001'dir. §22.1'e dilim adı. Yeni kayıt satırlarına uygulanır; var olan satırlar değişmez.
+
@@ -920 +926 @@ Protected Paths:
-Kapsanan dilim:              # r5 (CT-APR-02) — paketin `Teslim dilimleri` DL-n kimlikleri; `tamamı` yalnız onay satırındaki whole_pack=<WP> ile (§20.4)
+Kapsanan dilim:              # r5 (CT-APR-02) — paketin `Teslim dilimleri` DL-n kimlikleri; `tamamı` yalnız onay satırındaki whole_pack=<WP> ile (§20.4); plan satırıyla eşleşme r7 §20.4/6b-a
@@ -1445,0 +1452,19 @@ almasını onaylar. Eski, kurala bağlı olmayan paketlerde eksik alan yalnız u
+**6b. Dilim, plan ve kuyruk hizalaması (r7, 2026-11-01).** Kaynak: planlama tarafının isteği; plan dosyası
+(Capability Planning) depoda değildir ve repository authority değildir (§2.2) — aşağıdaki "plan" maddeleri ancak planın
+ilgili satırı depoya girince kapı olur; o zamana kadar CT yalnız ölçer ve raporlar, dağıtımı plan farkı yüzünden
+durdurmaz.
+- (a) **WP dilimi.** `Kapsanan dilim:` paketin onaylı sürümündeki `Teslim dilimleri` kimlikleridir (madde 6). Plan
+  satırı bir paket dilimi adlandırıyorsa WP'nin dilimi onunla aynıdır; farklıysa CT prompt'tan önce farkı yazar ve
+  sahibe sorar. Plan satırında dilim yoksa (2026-11-01 ölçümü: planda dilim sütunu yok) bu madde koşuludur.
+- (b) **Durum dilim bazında.** Durum, kısa haber ve takip sayfası modül + dilim (`MOD-nnnn DL-n`) ile yazılır; çok
+  modüllü raporda kapsam cümlesi dilimi adlandırır (§22.1). Dilim tablosu olmayan pakette "dilim yok" yazılır.
+- (c) **Kuyruk kapsamı.** 2026-11-01'den sonra eklenen her kayıt satırının `scope`'u teslim ettiği dilimleri adlandırır:
+  DCP diliminde `dcp:<DCP yolu>#<Slice>[, …]` (CAP-001 §8.1; `scripts/ct_route.py` okur), tek modülde
+  `MOD-nnnn DL-a…DL-b` ya da `MOD-nnnn DL-a, DL-c` ile başlar (serbest metindir; araç okumaz, CT ölçer). Var olan
+  satırlar değişmez; değiştirmek sahibin kuyruk onayıyla yeni satır sürümüdür.
+- (d) **Bir dilim, bir kuyruk.** Bir modül dilimi en çok bir kayıtlı kuyruktadır (K15). Onaylı paketi olup hiçbir
+  kuyrukta dilimi olmayan modül raporda "kuyruk yok" diye adıyla yazılır; kuyruk açmak sahibin kararıdır.
+- (e) **Sahip blok.** Kule bir modülü kuyruğuna alırken plandaki sahip bloğunu ve satır türünü (sahip / "Uses") yazar.
+  Plan modülü başka bloğa "Uses" diye bağlıyorsa o modülün dosyası bu kule için başka modüldür (XMC-001). Plan, kayıt
+  ve domain sahipliği (kart §8) farklıysa CT hiçbirini kendiliğinden düzeltmez; farkı sahibe sorar.
+
@@ -1561,0 +1587 @@ Report evidence pointer içermeli; “works” tek başına kanıt değildir.
+- Kapsam cümlesi, paketin dilim tablosu varsa dilimi adlandırır (`DL-n: …`; r7, §20.4/6b-b); sütunlar değişmez.
```
