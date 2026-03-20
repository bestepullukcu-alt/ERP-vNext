# ERP-vNext Working Agreement

Bu kural `yalnizca duzeltme/fix/refactor` islerinde gecerlidir.

- Yeni sayfa
- Yeni modül
- Yeni feature
- Sifirdan gelistirme

gibi taleplerde bu onay akisi zorunlu degildir; normal calisma akisi uygulanir.

Duzenleme veya hata giderme taleplerinde asagidaki sira zorunludur:

1. Once istenen kod duzeltmesi uygulanir.
2. Duzeltme tamamlaninca sonuc kullaniciya sunulur ve cozumun kabul edilip edilmedigi sorulur.
3. Kullanici onay vermeden `.antigravity` altinda hicbir dosya kontrol edilmez ve guncellenmez.
4. Kullanici duzeltmeyi reddederse `.antigravity` kontrol asamasi atlanir ve yeni bir duzeltme denenir.
5. Ayni konusma icinde birden fazla duzeltme denemesi yapilabilir. Bu ara denemeler icin `.antigravity` kontrolu yapilmaz.
6. Yalnizca kullanicinin acikca kabul ettigi nihai duzeltme baz alinir.
7. Bu onaydan sonra `.antigravity` altinda etkilenebilecek kural, workflow, skill, agent veya script dosyalari kontrol edilir.
8. `.antigravity` altinda degisiklik gerekiyorsa once degistirilmesi onerilen dosyalar listelenir ve kullanicidan ikinci bir onay alinir.
9. Kullanici ikinci onayi verirse `.antigravity` dosyalari guncellenir; vermezse kod duzeltmesi kalir ama `.antigravity` tarafina dokunulmaz.

Varsayilan akis:

- Faz 1: Kod duzeltmesi
- Faz 2: Kullanici kabul/red karari
- Faz 3: Kabul edilen nihai durum icin `.antigravity` etki kontrolu
- Faz 4: `.antigravity` guncelleme onayi
- Faz 5: Gerekirse `.antigravity` guncellemesi
