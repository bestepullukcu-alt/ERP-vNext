# Görev Merkezi — Kullanıcı El Kitabı

> **⏳ BU DOSYA BİR İSKELETTİR. METİN HENÜZ YAZILMADI.**
> **Kayıt:** [BL-074](./product-backlog.md) · **Açıldı:** 2026-08-11 · **Sahibi:** CONTROL TOWER

---

## Bu dosya kimin için

**Okuyucu: Görev Merkezi'ni kullanacak çalışan.** Geliştirici değil, sistem yöneticisi değil.

Bu yüzden metinde **olmayacaklar**: dosya adı, satır numarası, kod adı, *"handler / uç nokta / servis"*
gibi kelimeler. Her şey kullanıcının **ekranda gördüğü** isimlerle anlatılacak.

**Karıştırmayın:** kiracının sistemi **kurması** (şirket, birim, pozisyon, atama) ayrı bir dokümandır —
[`workcenter-onboarding-sop.md`](./workcenter-onboarding-sop.md), ve o **yöneticiye** hitap eder.
Bu dosya **kullanımı** anlatır. İkisi birbirine işaret eder; **içerik kopyalanmaz.**

## Neden metin şimdi yazılmıyor

Ekran metinleri, yerleşim ve terimler **liste / detay / gelen kutusu UX turunda** değişecek. Kılavuz şimdi
yazılırsa **iki hafta içinde yanlış olur** — ve yanlış bir kılavuz, kılavuz olmamasından kötüdür.
Bu, bitirme planının kendi kısıtıdır: *"Dokümantasyon en sonda. Kılavuz ekranları anlatır, UX turu
ekranları değiştirir."* ([`workcenter-completion-plan.md`](./workcenter-completion-plan.md) § Neden bu sıra).

## Yazım kuralları (metin turunda uygulanacak)

1. **Her bölüm bir SORUYA cevap verir**, özellik anlatmaz. Başlık *"Devir"* değil, içerik *"işi
   başkasına nasıl veririm?"* sorusunu karşılar.
2. **Ekran görüntüleri UX turu bitmeden ALINMAYACAK.** Öncesinde alınan her görüntü yeniden çekilecek.
3. **Terimler ekrandaki Türkçe metinle BİREBİR aynı olacak** ve `Resources/Views/Tasks/TasksIndex.tr.resx`
   ile karşılaştırılarak doğrulanacak. Kılavuzda *"görev sahibi"*, ekranda *"Atanan"* yazamaz.
4. **Dil:** önce **Türkçe**. ❓ **Sahibe açık soru:** kılavuz kaç dilde olacak? Tenant *ekranları* 7 dil
   zorunlu, ama bir doküman ekran değildir ve 7 dilde bakım her değişiklikte 7 kat iş demektir.
   **CT önerisi:** önce Türkçe, diğer diller ayrı madde ve gerçek talep geldiğinde. **Karar sahipte.**

### Bağımlılık haritası

| Bölüm | Bekliyor | Neden |
|---|---|---|
| 3 | **UX turu** | Sekme/segment/çip kararlarının **doğrudan** çıktısı |
| 6 (grup içi onay) | [BL-057] | Onaycı listesinin kuralı henüz yok |
| 9 | [BL-057] + [BL-023] | *"Kim kime iş verebilir"* kuralı **henüz yazılmadı** |
| 8 | [BL-065] · [BL-068] | Bildirim tercihleri ve dil davranışı |
| 1 · 2 · 4 · 5 · 7 · 10 · 11 | Yalnız ekran görüntüleri için UX turu | Davranış bugün belli |

---

# İSKELET

## 1. Görev Merkezi nedir
⏳ *UX turundan sonra yazılacak.*
Ne işe yaradığı ve **e-postayla iş takibinden farkı**: iş kimde, ne durumda ve ne zamana kadar — bunlar
bir gelen kutusunda değil, tek bir yerde ve kaybolmadan durur.

## 2. Temel kavramlar
⏳ *UX turundan sonra yazılacak.* Kullanıcının kafasındaki **soru sırasıyla** anlatılacak, sözlük
sırasıyla değil.

- **Görev · son tarih · öncelik** — bir işin en küçük hâli ve ne zamana yetişmesi gerektiği.
- **Bana atanan iş ile havuzdaki iş farkı** — *"üstlenmek"* ne demek: havuzdaki iş henüz **kimsenin
  değildir**, üstlenen kişinin olur.
- **İzleyici** — görür, **iş yapmaz**. Neden eklenir.
- **İnceleme** — **tamamlamadan önce** biri işe bakar.
- **Onay** — **başlamadan önce** yönetici izin verir. (İkisinin farkı § 6'da.)
- **Devir** — işi başkasına verme; ne zaman mümkündür.
- **Yinelenen görev** — her ay kendiliğinden doğan iş.

## 3. Ekranlarda ne nerede
⏳ **EN SON YAZILACAK — bu bölüm UX turunun kararlarına DOĞRUDAN bağlı.**

- **Sekmeler ne anlama gelir** → *sahiplik* (iş kimin).
- **Segmentler ne anlama gelir** → *durum* (iş nerede).
- **Çipler ne anlama gelir** → *tip ve sinyal* (dikkat çekilen şey).

> Bugün segment ile çip **görsel olarak birbirine benziyor** ve bu bilinen bir açık ([BL-017]); ayrım
> UX turunda keskinleştirilecek. Kılavuz o karardan sonra yazılabilir.

## 4. Görev oluşturma
⏳ *UX turundan sonra yazılacak (davranış belli, ekran görüntüsü bekliyor).*

- **Kendime · bir kişiye · bir havuza** — hangisi ne zaman seçilir.
- **Son tarih neden zorunlu** — son tarih *isteyenin taahhüdüdür*; her görevde vardır.
- **Planlama alanları neden bazen görünmüyor** — başlangıç tarihi ve tahmin **işi yapanın planıdır**;
  başkasına iş verirken görünmezler, çünkü onun adına plan yapılmaz.
- **Ek alanlar** — şirketin kendi tanımladığı alanlar; her kiracıda farklıdır.

## 5. İş nasıl ilerler
⏳ *UX turundan sonra yazılacak.*
Yaşam döngüsü **kullanıcı diliyle**: açıldı → planlandı → başladı → incelemede → tamamlandı.
Hangi düğme ne zaman çıkar, ve **neden bazen kapalı** (kapalı düğme her zaman bir sebeple kapalıdır ve
sebebi ekranda yazar).

## 6. Onay ve inceleme
⏳ *UX turundan sonra yazılacak · grup içi onay kısmı [BL-057] bekliyor.*

- **Farkları ne** — **onay BAŞLAMADAN** önce, **inceleme BİTİRMEDEN** önce.
- **Onay kararı nerede verilir.**
- **Farklı şirketteki birinden onay istemek (grup içi onay)** — mümkündür ve meşrudur; onaycı listesi
  şirket sınırıyla değil **yetkiyle** belirlenir. ⏳ Kural [BL-057]'de yazılıyor.

## 7. Yinelenen görevler
⏳ *UX turundan sonra yazılacak.*
Kural nasıl kurulur, görev ne zaman doğar, kural nasıl duraklatılır.

## 8. Bildirimler
⏳ *UX turundan sonra yazılacak · davranış [BL-065] · dil [BL-068].*

- Hangi olaylarda e-posta gelir.
- **Kendi yaptığın işten neden e-posta gelmez** — sistem sana kendi yaptığını haber vermez.
- Son tarih hatırlatması nasıl ayarlanır (**seçilmezse hatırlatma gelmez** — bu kasıtlıdır).
- Görev başına bildirim tercihleri.

## 9. Kim kime iş verebilir
⏳ **[BL-057] ve [BL-023] bitmeden YAZILAMAZ — kural henüz yok.**

- Kendi şirketin · altındakiler · sana açıkça verilen kapsam.
- **Üstüne iş atanmaz, TALEP gönderilir.**

## 10. Sık sorulan sorular
⏳ *UX turundan sonra yazılacak.* Her biri kullanıcının **kelimeleriyle** sorulacak:

- *"Aradığım kişi listede yok"* — ne yapmalı, **kime söylemeli**. (Sebepler yönetici tarafında:
  [`workcenter-onboarding-sop.md` § Bölüm 2](./workcenter-onboarding-sop.md). Kılavuz sebebi
  **anlatmaz**, kullanıcıyı doğru kişiye yönlendirir.)
- *"Hatırlatma gelmedi"*
- *"Görev siz bakarken değişti" uyarısı* — ne demek, ne yapmalı.
- *"Bu görevi neden kapatamıyorum"*

## 11. Terim sözlüğü
⏳ *EN SON — ekran metinleri kesinleştikten sonra.*
Ekranda geçen her terimin **tek cümlelik** karşılığı. Terimler `TasksIndex.tr.resx` ile karşılaştırılarak
doğrulanacak; kılavuz ile ekran **farklı kelime kullanamaz**.

---

## Bu kılavuzun kapsamadıkları

- **Kurulum ve ana veri** (şirket, birim, pozisyon, kullanıcı, atama) →
  [`workcenter-onboarding-sop.md`](./workcenter-onboarding-sop.md) — [BL-073]
- **Dev ortam** → [`dev-environment.md`](./dev-environment.md)
- **API dokümanı** (uç noktalar, sözleşme, hata kodları) → ayrı iş,
  [`workcenter-completion-plan.md`](./workcenter-completion-plan.md) § Aşama 6
