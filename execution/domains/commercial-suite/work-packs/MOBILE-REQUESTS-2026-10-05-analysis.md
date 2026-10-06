# Mobil (iOS + Android) backend talepleri — analiz ve eşleme

> **CT, 2026-10-06.** Kaynak: iOS durum raporu (2026-10-05, `main 23c528a`) ve Android durum + yol haritası (2026-10-05, `master aa1c9e1`).
> Her madde bizim kodla karşılaştırıldı (CT kod okuması, 2026-10-06). "Durum" sütunu backend'in bugünkü hâli.

## 1. Özet
- **Mobil tarafta backend'den bağımsız iş neredeyse bitti** (Android ~5–7 gün kalite işi; iOS ~3–4 faz). Kalan ilerleme backend'e bağlı.
- **Bizde hazır ama yayında değil:** MOD-0048 referans setleri (`fix/brd-tenant-crm-sets`, PR açılmadı → `main`'de ve canlıda yok). Android hâlâ **eski** uç adını (`sets/{setCode}`) bekliyor; yeni uç `consumable-sets/{setCode}`.
- **En çok kazandıracak backend işleri:** (1) referans setleri PR + dağıtım; (2) planlanan ziyaret paketi: hedef görünen adları, temsilci sahipliği, rapor yetkileri.
- **Büyük ve tasarım isteyen işler:** Anti-Fraud API, check-in/out (MOD-0280), Fırsat / Aktivite modülleri, HR gateway + token, delta sync.

## 2. Talepler ve durum
### 2.1 CRM
| ID | Talep (kim) | Durum (bizde) | Not |
|---|---|---|---|
| MOD-0048 R1/R2 | Kiracı kullanıcısı CRM referans setlerini okuyabilsin; CRM kaydı Admin olmayana da çalışsın (iOS + Android) | **Kodda TAMAM** — `fix/brd-tenant-crm-sets` (`e55c24d5`, `3238591d`, main ile senkron `5cf18476`); **PR açılmadı, canlıda yok** | Android'e: uç adı `api/lookups/reference-data/consumable-sets/{setCode}/published-values`; yetki = oturum (login-only) |
| F1 | Müşteride sürüm + `expectedVersion` + 409 (Android) | **YOK** — Account komutlarında sürüm yok | Kayıp güncelleme riski; küçük iş |
| F2 | Kısmi güncelleme / ayrı logo ucu (Android) | YOK | Düzenlemede logo silinme riski |
| F3 | 400'de alan adı / makine kodu (Android) | Kısmi (birçok uçta kod var; Account doğrulaması kontrol edilmeli) | — |
| F4 | 409 kodları `account_code_taken`, `external_reference_taken` (Android) | Kontrol edilmeli | — |
| Gate F / G | Fırsat (Opportunity) ve Aktivite (Activity) API'leri (Android) | **YOK** (modül yok) | Blueprint / modül kimliği + tasarım gerekir; büyük |
| Segment | Mobilde segment kapsamı (iOS) | Ürün kararı yok | — |

### 2.2 Planlanan ziyaret (MOD-0155) ve saha
| ID | Talep | Durum | Not |
|---|---|---|---|
| D1 / satır kimliği | Liste / detay / takvimde hedef adı (`targetDisplayName`, `accountDisplayName`, `contactDisplayName`); isteğe bağlı `{code,label}` sözlükleri (iOS + Android) | **YOK** — DTO'da yalnız `ResourceDisplayName`, yolculuk / aşama adı | Küçük-orta |
| B01 | **Sahiplik:** temsilci yalnız kendi ziyaretlerini görsün / değiştirsin, sunucu tarafında (iOS) | **YOK** — sahiplik filtresi yok | Güvenlik; kaynak modeline bağlı |
| T1–T3 | Hedef kuralları: seçilen kişi / işyeri → `targetType + targetId`; pasif hedef `target_inactive`; düzenlemede hedef değişir mi (iOS) | Oluştur / düzenle uçları var (Android kullanıyor); kuralların yazılı sözleşmesi + `target_inactive` kontrol edilmeli | Karar: hedef düzenlemede değişsin mi |
| R-M1 / R-M2 / R-M4, B-02 | Kaynak modeli (kullanıcı ↔ kaynak, 0 / 1 / çok kaynak), `resources/me`'de ad; kullanıcı → bölge eşlemesi (iOS + Android) | **Geçici:** "kullanıcı = kaynak" (`f182ba1b`) | HR / bölge kaynak ataması kararı (açık) |
| F-RBAC / Gate C, B05 | Ziyaret raporu kaydet / sonuç / düzelt için gerçek yetki + saha temsilcisi rolü + dağıtım (iOS + Android) | Anahtarlar **var** (`crm.visit-report.read/record/amend`, `crm.planned-visit.*`) ama yedek yetkiye düşüyor (`crm.territory.*`); **saha temsilcisi rolü 97c5'te yok** | Rol kararı + grant script + yedeğin kaldırılması |
| Rapor yazma sözleşmesi | iOS "sözleşme netleşince" | Uçlar var (`VisitExecution`: rapor oluştur / düzelt / sonuç) | Sözleşme notu verilmeli; **SB-3c değiştirecek** (başlat / tamamla, ürün başına `contentActuals`) |
| SB-3 (bizim yeni işimiz) | — | SB-3b: `contentItems[]` (ek alan) geldi; SB-3c: "Ziyaret yap / Tamamla" gelecek | **SB-3-MOB notu** mobil ekiplere verilmeli (talep etmediler ama etkileyecek) |
| MOD-0280 / Gate E | Check-in / check-out (iOS + Android) | **YOK** (ertelenmiş) | Anti-Fraud ile birlikte |
| Aylık takvim, çevrimdışı, delta sync, sayfalama | iOS | Delta sync YOK | Tasarım gerekir |

### 2.3 Giriş / platform
| ID | Talep | Durum |
|---|---|---|
| Gate B | Kiracı için şifremi unuttum / sıfırla / doğrula (iOS + Android) | YOK (yalnız platform tarafında) |
| Hata kodları | Giriş ve MFA reddinde makine kodu (Android) | YOK (İngilizce cümleler) |
| Tenant adı | Oturum / JWT'de kiracının görünen adı (Android) | Kontrol edilmeli |
| `/me/permissions` | Yetkileri token dışında okumak (iOS, isteğe bağlı) | YOK |
| D-1 / D-2 | Release uygulaması hangi kiracıya bağlanır / girişten önce kiracı seçimi (iOS + Android) | **Ürün kararı** |

### 2.4 HR (Android)
| ID | Talep | Durum |
|---|---|---|
| G1 | Genel gateway tüm `/api/<hr-modül>` rotalarını HR servisine iletsin; tenant token + `X-Tenant-Id` + `X-Legal-Entity-Id`; standart zarf + hata kodları | Kontrol edilmeli (HCM rotaları kısmi) |
| G2 | Token'da `permission (hcm.*)`, `legal_entities`, `tenant_id`, `sub`, `exp`, `pwd_change_required` | **`legal_entities` claim main'de YOK** (bilinen açık: hr-future token gap) |
| G5 | 4 özel modül için staging sözleşme koşusu | Ortam / koordinasyon |
| G8 | Çevrimdışı oluşturmada idempotency (`Idempotency-Key` / istemci ID / benzersiz kod + 409) | Kontrol edilmeli; kopya kayıt riski |
| G4 | Doğrulanmış projeksiyonlarda 404 veren validator | Hata — incelenmeli |

### 2.5 Anti-Fraud (iOS AF-D1…D13, Android B-AF-01…13)
- **Backend'de hiç yok.** Cihaz kaydı, mobil oturum, heartbeat, challenge, kanıt (fotoğraf) yükleme + senkron, App Attest / Play Integrity doğrulaması, güvenilir zaman, kanonik hash + test vektörü, ziyaret durum makinesi, geofence (planlı ziyaret koordinatı), risk sinyali, mutabakat.
- **Önce mimari karar + modül kimliği** (Blueprint / DCP-002), iki mobil ekiple ortak sözleşme.

## 3. CT önerisi — sıra
1. **Hemen (iş bitti, yalnız yayın):** `fix/brd-tenant-crm-sets` PR → main → dağıtım; Android'e yeni uç adını bildir. (Android müşteri oluştur / düzenle ve iOS referans setleri açılır.)
2. **Ziyaret paketi (bizim ziyaret işimizle aynı alan):** D1 hedef görünen adları + B01 temsilci sahipliği + `resources/me` adı + T1–T3 sözleşmesi + F-RBAC (saha temsilcisi rol kararı → grant script, yedek yetkinin kaldırılması). Ardından SB-3c ile birlikte **SB-3-MOB** sözleşme notu.
3. **CRM güvenli düzenleme:** F1 (sürüm + 409), F3 / F4 (kodlar), F2 (logo / kısmi güncelleme).
4. **Giriş:** makine okunur hata kodları, kiracı adı, şifremi unuttum (Gate B).
5. **HR:** G2 (`legal_entities` claim) + G1 + G8 — HR ekibiyle.
6. **Tasarım isteyenler:** Anti-Fraud API, MOD-0280 check-in, Fırsat / Aktivite, delta sync — her biri önce mimari karar.

## 4. Kullanıcı kararları
- D-1 / D-2: release'te kiracı seçimi.
- Saha temsilcisi rolü (97c5) — F-RBAC ve sahiplik (B01) için.
- Kaynak modeli: kullanıcı ↔ kaynak ↔ bölge (B-02).
- T3: düzenlemede hedef değişebilir mi.
- Fırsat / Aktivite ve Anti-Fraud kapsam / sıra.
