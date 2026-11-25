# Rev21 ilk resmî tur — CT değerlendirmesi

2026-11-20. Pack aee01181/domain c3f94d54 kaynak b60aabc77. Diyalog ve geçmişsiz son bakış ham raporları ayrı kaydedildi; her ikisi KOŞULLU, açık ENGEL0. İnceleme başarısı geliştirme/dağıtım kapısı değildir.

Diyalog F-1 kabul: domain ebeveynlik cümlesi ters; paket yazarı yeni kapsam eklemeden pack/domain'i H ∈ parents(M) anlamına hizalıyor. F-2 kabul: eski auth ölçümü güncel gibi okunmamalı; yazar mevcut tarihsel kaynak bağını koruyup gerçek tenant_id/X-Tenant-Id, canonical permission ve MVC page/JSON kontrollerini tarihli ölçümden ayırıyor. Bu iki düzeltmenin son baytları henüz alınmadı; düzelmiş sayılmaz.

Diyalog F-3 kabul: CLI --suite kendi başına TRX'in Web koşusu olduğunu kanıtlamaz. CT kabul üreticisi artık bağımsız normal TRX'i immutable a2586b89 commit'indeki gerçek Git blob'una, okumadan önce/sonra canonical LF içerik eşitliği ve SHA-256 ile bağlar. Hash `d8e4459f1779fee3cdcd68b49cecf6949d13f00df459dbc136a11633c8646e91`; orijinal ham hash rapor/manifestte ayrı korunur. Bu bağ normal CLI0/0 ve tam beş hücre farkına EK koşuldur; başka suite'in yeşil TRX'i eşit olamaz. Yeni kesin betikle baseline-red-v1.1.json üretildi: normal CLI1/2, acceptanceRED, gerçek TRXbefore/after eşliği true, tek eski token productionvalidatorRED. Onarım henüz uygulanmadı. İşçi eski v1.0 prompt'u almadı; yeni kaynak/HEAD/üreticiyle v1.1 hazırlanacak.

Son bakış F-1 kabul: rev20 doğrudan sahip onayı ilk ZIP'te eksik; sonraki resmî ZIP'e asıl karar ve bildirilen diğer mevcut güvenlik/UI/approval kaynakları eklenecek. İzin yokmuş veya onay yeniden verilmiş gibi gösterilmez. İlk tur raporları aynen kalır. İkinci tur yalnız değişen kesin kaynakları ve eksik girdileri tekrar ölçer; başka insan kapsam kararı gerekmez, kapsam genişletilmez.
