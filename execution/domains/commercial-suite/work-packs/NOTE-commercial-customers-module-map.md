# NOT — Ticari müşteri: modül haritası ve temas noktaları (2026-10-14)

> **Durum:** yalnız not. Bugünkü planın (W3E → W5 ∥ W6 → W7 → W9 → Faz 8 → W10 → takipler) **dışında**. Program T yol haritasının en sonunda (K-W13).
>
> **İlgili belgeler:**
> - Yol haritası [ROADMAP-visit-workspace](ROADMAP-visit-workspace.md): K-W13, sıra 14 "Program T".
> - Mockup brief'i [BRIEF-commercial-customer-profile](mockups/commercial-customers/BRIEF-commercial-customer-profile.md).
> - CRM planı sayfası (C1–C9 + referans modüller): https://claude.ai/artifact/Ljf1pnb4ZeUB4aFzix2wk4

## 1. Kararlar (kullanıcı, 2026-10-14)
- **Ticari müşteri = mevcut CRM kurumu (Account).** Blueprint v8.1: Customer (Account) SoR = MOD-0149. Ayrı müşteri kaydı YOK.
- **Ticari profil satan şirketimize (tüzel kişilik) göre:** Diten İlaç A.Ş. / Grand Medical Gulf.
- **Finans bizim sistemimizde:** repoda finans alanı yok → yeni alan.
- **Zamanlama:** en sonda.
- **Mockup:** brief yazıldı, kullanıcı yaptıracak.

## 2. CRM'e eklenecekler (özet; ayrıntı plan sayfasında)

| kod | kalem |
|---|---|
| C1 | Ticari profil (kurum × satan şirket): para birimi, vade kodu, fiyat listesi bağlantısı, iskonto grubu, varsayılan teslim kurumu, vergi kimliği, e-fatura beyanı |
| C1 öneri | **Varsayılan sevk deposu** (bizim depomuz → Inventory lokasyon kaydı). Kullanıcı onayı bekliyor. |
| C2 | Ticari roller: sipariş veren / teslim alan / fatura alan / ödeyen |
| C3 | Sipariş bloğu |
| C4 | Referans veri: kurum türü, ilişki türü, kişi rolü + 5 yeni set |
| C5 | Finans özeti okuma yüzeyi (fail-soft) |
| C6 | Web ekranları |
| C7 | Yetkiler |
| C8 | Olaylar |
| C9 | **W6 köprüsü:** ön sipariş depoyu kurum kimliğiyle tutar. W6 paketlenirken uygulanır; plan içi tek madde. |

## 3. Modüller — ne oldukları

| modül | ad | suite / dalga | sahip olduğu veri | bizimle bağı | durum |
|---|---|---|---|---|---|
| MOD-0150 | Contact & Relationship Mgmt | CRM Core · W-3 | kişiler, kişi–kurum bağları | satın alma / muhasebe kişileri (`contact-role`) | var |
| MOD-0156 | Price Lists & Discount Guardrails | CPQ & Pricing · W-3 | fiyat listeleri, iskonto kuralları, fiyat onayları | profil yalnız liste bağlantısı tutar; W6 MF / iskonto kaynağı | yok |
| MOD-0168 | Order Capture | O2C · W-2 | siparişler, giriş ve doğrulama kayıtları | profil / rol / blok okur; W6 ilk parçası; tahsis + rezervasyonu tetikler | yok |
| MOD-0169 | Billing & Invoicing | O2C · W-2 | faturalama çalıştırmaları, hatalar, alacağa aktarım | fatura alan / ödeyen rollerini okur; 0168 + 0120'ye bağımlı | yok |
| MOD-0120 | Accounts Receivable | ERP Finans · W-3 | alacak faturaları, bakiyeler, tahsilat, alacak dekontları | kredi limiti / bakiye / vadesi geçmiş → CRM okur (C5) | **kullanıcı yapıyor** |
| MOD-0217 | Contract Lifecycle Mgmt | Legal & Contracts · W-3 | sözleşmeler, sürümler, onaylar | taraf = CRM kurum kimliği; CRM yalnız okur (DCP-006) | **kullanıcı yapıyor** |
| MOD-0219 | Obligations & Renewal | Legal & Contracts · W-4 | yükümlülükler, yenileme tarihleri | CRM yalnız okur | yok |
| MOD-0129 | E-Invoicing / E-Archive | ERP Vergi · W-3 | e-fatura gönderimi, alındı, arşiv | e-fatura mükellefi doğrulaması (C1 beyanının yerine geçer) | yok |
| MOD-0128 | VAT/GST & Withholding | ERP Vergi · W-4 | vergi belirleme, tevkifat | vergi kimliğini CRM profilinden okur | yok |
| MOD-0172 | Allocation & ATP/CTP | O2C · W-4 | tahsis kararları, ATP sözleri | siparişle Inventory arasındaki köprü (Program T3) | kayıtlı, kod yok |

**Akış:**
```
Fiyat 0156 → Sipariş 0168 → Tahsis 0172 → Rezervasyon 0173 → Toplama 0178 → Sevkiyat 0183
                                                                              → Fatura 0169 → E-fatura 0129 → Alacak 0120
                                                                                 (vergi 0128)
```

## 4. MOD-0120 / MOD-0217 ile uyuşulacak noktalar (kullanıcının ekibi)
1. **Müşteri kimliği** = CRM `AccountId` + satan `LegalEntityId`. AR ve CLM kendi müşteri kaydını açmaz.
2. **Ödeme vadesinin sahibi AÇIK:** CRM profilinde ticari koşul mu, AR'da mı? İki yerde tutulmaz.
3. **AR → CRM okuma ucu:** kredi limiti, kullanılan, açık bakiye, vadesi geçmiş, finans bloğu. Olay örneği `ar.customer-credit.changed`.
4. **CLM → CRM okuma ucu:** kurumun sözleşmeleri (durum, tarih, tür, satan şirket).
5. **CRM → AR / CLM:** ticari profil + roller (fatura alan, ödeyen, sipariş veren) + değişiklik / blok olayları (C8).

## 5. Tedarik zinciri temas noktaları (Inventory 5067, SupplyChain 5066)
- **Teslim adresi:** sevkiyat `ShipToReference` bugün serbest metin. CRM teslim alan kurum kimliği + adres anlık görüntüsü olmalı (geri çağırma için zorunlu). Bu, tedarik zinciri ekibinin işi; Program T1 sonrası değişiklik notu.
- **Stok tüzel kişiliğe göre** ayrılmış; profil anahtarıyla aynı boyutta.
- **Sipariş bloğu** → tahsis / rezervasyon başlamaz.
- **İade:** karar 0170 (ticari) → fiziksel 0186 → stok 0173 → alacak dekontu 0120.
- **Geri çağırma (0177):** lot → sevkiyat → teslim alan kurum → CRM kişileri.
- **W6 ön siparişi** müşterinin deposuna gider; bizim stoğumuzu rezerve etmez.
- **CRM asla stok yazmaz.**
- **İsteğe bağlı, ayrı karar:** temsilci numune stoğu Inventory'de (rapordaki numune = stok hareketi).

## 6. Açık sorular (Program T1 paketlenmeden önce)
- Türkiye eczane ön siparişinde siparişi veren depo mu, eczane mi? Eczanelerin ticari profil alıp almayacağını belirler.
- Finans müşteri numarasını kim üretir? Öneri: Finans üretir, CRM dış referans tutar.
- Sipariş bloğunu kim koyar? Öneri: satış operasyonu + finans, kaynak ayrı görünür.
- Ödeme vadesi sahibi (§4 madde 2).
- C1'e "varsayılan sevk deposu" eklensin mi?
