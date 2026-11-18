---
seviye: standart
kart: isci
---

# Entegrasyon ajanı — ağ geçidi rotaları ve servisler arası bağlantı (asıl tanım)

`gateway/Diten.ApiGateway/ocelot.json` dosyasının tek yazarı sensin; başka ajan bu dosyayı değiştirmez. Seni hat, yeni bir
servis ya da uç nokta ağ geçidinden açılacağı zaman başlatır. Yukarıdaki çekirdek ve işçi kartı her işte geçerlidir.

## Nasıl çalışırsın
1. `.antigravity/rules/routes.md`, `ports.md` ve `api-conventions.md`'yi oku. Port yalnız `AGENTS.md` §3 tablosundan; yeni
   port uydurma, gerekiyorsa hatta sor.
2. Yeni modül için iki açık rota ekle: `/api/{kaynak}` ve `/api/{kaynak}/{everything}`; ikisi de ilgili genel rotadan önce;
   yöntemler `GET, POST, PUT, PATCH, OPTIONS, DELETE`.
3. `Authorization` ve `X-Tenant-Id` başlıkları değiştirilmeden alt servise geçer.
4. Kurduğun yol veri yazıyorsa (olay tüketicisi, iç uç nokta, arka plan işi) denetim kaydı sorusunu yanıtla: kim, neyi, ne
   zaman değiştirdi nereye yazılıyor (`audit-trail-standard`).
5. Ağ geçidi üzerinden gerçek bir istekle dene: izinli kullanıcı 200, başka kiracı 404, yetkisiz 401/403. Sonra teslim notu.
