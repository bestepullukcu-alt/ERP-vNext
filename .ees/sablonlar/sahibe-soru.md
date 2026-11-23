# Şablon — sahibe soru ve sohbetler arası mesaj

Kim kullanır: kule (K19) ve kişiye soru soran hat. Eski kartın soru ve mesaj biçimi kurallarının sadeleşmiş hâli; kart
satırı değil, biçim. Araç düğme kutusu sunuyorsa düğme; sunmuyorsa cevap sözcükleri.

1. Sormadan önce hâlâ beklediğini ve engelin ana dalda sürdüğünü ölç (Ç18).
2. Mesajın sonunda tek soru; en çok üç seçenek. Onay soruları (push, PR, birleştirme, korunan yol, kapsam, başka modül)
   her zaman tek başına sorulur; onay olmayan 2–4 bağımsız küçük karar bir arada sorulabilir.
3. Önerilen seçenek ilk sırada, "(Önerilen)" ve tek cümlelik nedenle; her seçenek neye izin verdiğini ve neyi yasakladığını
   söyler. Bir olgu soruluyorsa seçenekler olguyu ve izni birlikte adlandırır; izin dolaylı değil, doğrudan istenir.
4. Düğme yoksa her seçeneğin kısa bir cevap sözcüğü olur, her sözcük ayrı kod bloğunda. Önerilmeyen ya da geri alınamaz
   seçenek yalnız numarayla seçilirse tek satırlık teyit istenir; geri alınamaz adımdan önce seçilen etiket aynen tekrar
   edilir ("Seçiminiz: …").
5. Mesaj düz metindir; kod bloğu yalnız kişinin kopyalayacağı metin içindir. Soru için HTML ya da görsel kart kullanılmaz.
6. Kritik çakışma ve engel hiçbir satır sınırına gizlenmez; mesajın başında yazılır.
7. Sohbetler arası mesajda ilk 400 karakter sonucu ve isteği taşır (karşı taraf yalnız bu kadarını görebilir).
