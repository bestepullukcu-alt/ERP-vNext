// Demo veri — Ziyaret Çalışma Alanı mockup
(function(){
const NOW = new Date(2026,9,8,10,40);
const P = {
  TUTUKON:{n:'TUTUKON',f:'100 mg film tablet',ind:'Kronik koroner sendrom'},
  ALMIBA:{n:'ALMIBA',f:'5/10 mg tablet',ind:'Hipertansiyon'},
  GLUKOFIT:{n:'GLUKOFİT',f:'850 mg tablet',ind:'Tip 2 diyabet'},
  KARDIVA:{n:'KARDİVA',f:'50 mg tablet',ind:'Kalp yetmezliği'},
  NOROMAX:{n:'NÖROMAX',f:'300 mg kapsül',ind:'Nöropatik ağrı'},
  PULMOSET:{n:'PULMOSET',f:'200 mcg inhaler',ind:'KOAH'},
  UROFLEKS:{n:'ÜROFLEKS',f:'0,4 mg kapsül',ind:'BPH'}
};
const ACC = {
  a1:{n:'Şişli Hamidiye Etfal EAH',t:'Hastane',ad:'Halaskargazi Cd., Şişli'},
  a2:{n:'Şişli Osmanoğlu Tıp Merkezi',t:'Klinik',ad:'Büyükdere Cd., Şişli'},
  a3:{n:'İstanbul Florence Nightingale',t:'Hastane',ad:'Abide-i Hürriyet Cd., Şişli'},
  a4:{n:'Memorial Şişli Hastanesi',t:'Hastane',ad:'Piyalepaşa Blv., Okmeydanı'},
  a5:{n:'Mecidiyeköy ASM',t:'ASM',ad:'Mecidiyeköy, Şişli'},
  ph1:{n:'Şifa Eczanesi',t:'Eczane',ad:'Halaskargazi Cd. 214, Şişli'},
  ph2:{n:'Hayat Eczanesi',t:'Eczane',ad:'Mecidiyeköy Yolu 12, Şişli'}
};
const ADOPT = ['Farkında değil','Farkında','Değerlendiriyor','Deniyor','Düzenli kullanıyor','Savunucu'];
const D = {
  d1:{n:'Dr. Ayşe Kaya',sp:'Kardiyoloji',acc:'a1',seg:'Yüksek potansiyel',prods:['KARDIVA','ALMIBA'],freq:3,tot:420,typ:'Pragmatik',sty:'Analitik',kol:'Bölgesel',best:'Salı · 09:00–10:30'},
  d2:{n:'Doç. Dr. Mehmet Yılmaz',sp:'Dahiliye',acc:'a1',seg:'Sadık kullanıcı',prods:['TUTUKON','ALMIBA'],freq:3,tot:380,typ:'Muhafazakar',sty:'Sonuç odaklı',kol:'Yerel',best:'Pazartesi · 09:30–11:00'},
  d3:{n:'Uzm. Dr. Zeynep Arslan',sp:'Endokrinoloji',acc:'a1',seg:'Yüksek potansiyel',prods:['GLUKOFIT','ALMIBA'],freq:2,tot:310,typ:'Erken benimseyen',sty:'Analitik',kol:'Bölgesel',best:'Çarşamba · 09:00–10:00'},
  d4:{n:'Dr. Burak Demir',sp:'Üroloji',acc:'a2',seg:'Gelişen',prods:['UROFLEKS','TUTUKON'],freq:2,tot:260,typ:'Pragmatik',sty:'İlişki odaklı',kol:'Yerel',best:'Cuma · 11:00–12:00'},
  d5:{n:'Uzm. Dr. Elif Şahin',sp:'Aile Hekimliği',acc:'a2',seg:'Sadık kullanıcı',prods:['TUTUKON','GLUKOFIT'],freq:3,tot:540,typ:'Pragmatik',sty:'İlişki odaklı',kol:'Yerel',best:'Pazartesi · 14:00–15:30'},
  d6:{n:'Prof. Dr. Hakan Öztürk',sp:'Nöroloji',acc:'a3',seg:'Kanaat önderi',prods:['NOROMAX','TUTUKON'],freq:2,tot:280,typ:'Yenilikçi',sty:'Analitik',kol:'Ulusal',best:'Salı · 09:00–10:00'},
  d7:{n:'Uzm. Dr. Selin Aydın',sp:'Endokrinoloji',acc:'a3',seg:'Yüksek potansiyel',prods:['GLUKOFIT','ALMIBA'],freq:3,tot:330,typ:'Erken benimseyen',sty:'Dışavurumcu',kol:'Yerel',best:'Salı · 10:00–11:00'},
  d8:{n:'Dr. Can Koç',sp:'Göğüs Hastalıkları',acc:'a3',seg:'Gelişen',prods:['PULMOSET'],freq:2,tot:290,typ:'Muhafazakar',sty:'Sonuç odaklı',kol:'Yerel',best:'Salı · 10:30–11:30'},
  d9:{n:'Uzm. Dr. Deniz Çelik',sp:'Dahiliye',acc:'a4',seg:'Yüksek potansiyel',prods:['TUTUKON','ALMIBA'],freq:3,tot:400,typ:'Pragmatik',sty:'Sonuç odaklı',kol:'Yerel',best:'Cuma · 10:00–11:00'},
  d10:{n:'Dr. Gizem Yıldız',sp:'Kardiyoloji',acc:'a4',seg:'Sadık kullanıcı',prods:['KARDIVA','ALMIBA'],freq:2,tot:360,typ:'Erken benimseyen',sty:'İlişki odaklı',kol:'Bölgesel',best:'Cuma · 09:30–10:30'},
  d11:{n:'Dr. Emre Aksoy',sp:'Genel Cerrahi',acc:'a1',seg:'Gelişen',prods:['TUTUKON'],freq:1,tot:220,typ:'Muhafazakar',sty:'Sonuç odaklı',kol:'Yerel',best:'Pazartesi · 10:00–11:00'},
  d12:{n:'Uzm. Dr. Merve Polat',sp:'Aile Hekimliği',acc:'a5',seg:'Sadık kullanıcı',prods:['TUTUKON','GLUKOFIT'],freq:2,tot:610,typ:'Pragmatik',sty:'İlişki odaklı',kol:'Yerel',best:'Çarşamba · 13:30–15:00'},
  d13:{n:'Dr. Ozan Kurt',sp:'Kardiyoloji',acc:'a4',seg:'Yüksek potansiyel',prods:['KARDIVA','ALMIBA'],freq:3,tot:350,typ:'Yenilikçi',sty:'Analitik',kol:'Bölgesel',best:'Perşembe · 09:30–10:30'},
  d14:{n:'Uzm. Dr. Ceren Erdem',sp:'Dahiliye',acc:'a2',seg:'Gelişen',prods:['TUTUKON','ALMIBA'],freq:3,tot:390,typ:'Erken benimseyen',sty:'Dışavurumcu',kol:'Yerel',best:'Perşembe · 10:30–11:30'},
  d15:{n:'Dr. Kerem Aslan',sp:'Üroloji',acc:'a3',seg:'Yüksek potansiyel',prods:['UROFLEKS','TUTUKON'],freq:3,tot:300,typ:'Pragmatik',sty:'Analitik',kol:'Bölgesel',best:'Perşembe · 11:00–12:30'},
  d16:{n:'Uzm. Dr. Nazlı Güneş',sp:'Endokrinoloji',acc:'a4',seg:'Kanaat önderi',prods:['GLUKOFIT','ALMIBA'],freq:2,tot:340,typ:'Yenilikçi',sty:'Analitik',kol:'Ulusal',best:'Perşembe · 14:00–15:00'},
  ph1:{n:'Şifa Eczanesi',sp:'Eczane',acc:'ph1',ph:true,prods:['TUTUKON','ALMIBA','GLUKOFIT'],freq:2,who:'Ecz. Murat Er'},
  ph2:{n:'Hayat Eczanesi',sp:'Eczane',acc:'ph2',ph:true,prods:['TUTUKON','GLUKOFIT'],freq:2,who:'Ecz. Sevgi Tan'}
};
// doktor × ürün: endikasyon hastası, pay %, aşama, ilgi
let s=17;const rnd=()=>(s=(s*16807)%2147483647)/2147483647;
Object.entries(D).forEach(([id,d])=>{d.id=id;d.k={};d.prods.forEach((p,i)=>{d.k[p]={ind:Math.round((d.tot||300)*(0.12+rnd()*0.3)),pay:Math.round(8+rnd()*42),st:1+Math.floor(rnd()*4),ilgi:['İlgili','Nötr','İlgili','İlgisiz'][Math.floor(rnd()*4)]}})});
D.d2.k.TUTUKON={ind:96,pay:30,st:4,ilgi:'İlgili'};D.d15.k.UROFLEKS={ind:84,pay:14,st:2,ilgi:'İlgili'};D.d15.k.TUTUKON={ind:41,pay:22,st:3,ilgi:'Nötr'};
const ROLE={t:{l:'tanıtım',st:'Klinik kanıt · 2. mesaj',steps:6,min:10},h:{l:'hatırlatma',st:'Hatırlatma',steps:2,min:4}};
const ST = {
  taslak:{l:'Taslak',ic:'bx-edit-alt',bg:'transparent',fg:'var(--mu)',bd:'1px dashed var(--pbd)'},
  plan:{l:'Planlı',ic:'bx-calendar-check',bg:'var(--pl)',fg:'var(--pt)',bd:'1px solid var(--pl)'},
  hazir:{l:'Bugün · başlamaya hazır',ic:'bx-play-circle',bg:'var(--p)',fg:'#fff',bd:'1px solid var(--p)'},
  devam:{l:'Devam ediyor',ic:'bx-radio-circle-marked',bg:'var(--inb)',fg:'var(--in)',bd:'1px solid var(--inc)'},
  raporEksik:{l:'Rapor eksik',ic:'bx-time-five',bg:'var(--wab)',fg:'var(--wa)',bd:'1px solid var(--wab)'},
  raporlandi:{l:'Raporlandı',ic:'bx-check-circle',bg:'var(--okb)',fg:'var(--ok)',bd:'1px solid var(--okb)'},
  kacirildi:{l:'Kaçırıldı',ic:'bx-error-circle',bg:'var(--dab)',fg:'var(--da)',bd:'1px solid var(--dab)'},
  yapilamadi:{l:'Yapılamadı',ic:'bx-x-circle',bg:'var(--seb)',fg:'var(--se)',bd:'1px solid var(--seb)'},
  ertelendi:{l:'Ertelendi',ic:'bx-calendar-edit',bg:'var(--seb)',fg:'var(--se)',bd:'1px solid var(--seb)'},
  iptal:{l:'İptal',ic:'bx-block',bg:'var(--seb)',fg:'var(--se)',bd:'1px solid var(--seb)',strike:true},
  suredoldu:{l:'Süre doldu',ic:'bx-lock-alt',bg:'var(--dab)',fg:'var(--da)',bd:'1px solid var(--dab)'}
};
const REASONS=['Doktor yok / izinde','Doktor zamanı yok / görüşmeyi reddetti','Kurum kapalı / girişe izin yok','Temsilci izinli / hasta','Toplantı / eğitim çakışması','Ulaşım / hava koşulu','Doktor kurumdan ayrıldı / hedef pasif','Diğer'];
const OBJ=['Fiyat','Geri ödeme','Etkinlik','Yan etki','Doz / kullanım','Erişim / stok','Alışkanlık / rakip memnuniyeti','Kanıt yetersiz','Diğer'];
const REQ=['Literatür / çalışma','Numune','Hasta materyali','Eğitim / toplantı daveti','MSL görüşmesi','Diğer'];
const ADV=['Etkinlik','Güvenlik','Doz kolaylığı','Fiyat','Geri ödeme','Erişim / stok','Kanıt düzeyi','Marka bilinirliği'];
const COMP={TUTUKON:['Koronex 100','Anjiyosed'],ALMIBA:['Tensilox','Amlopres','Valsen Plus'],GLUKOFIT:['Diaform','Metgluk XR'],KARDIVA:['Kardiyonil','Cormax'],NOROMAX:['Gabanor','Pregaval'],PULMOSET:['Bronkair','Tiovent'],UROFLEKS:['Prostam','Tamsulin']};
// ziyaretler
const V=[];let vid=0;
const add=(date,st,en,doc,prods,status,x)=>{V.push(Object.assign({id:'v'+(++vid),date,start:st,end:en,doc,prods,status},x||{}))};
const T=(p,r)=>({p,r});
// 40. hafta (geçmiş)
[['2026-09-28','09:30','09:55','d1'],['2026-09-28','14:00','14:20','d12'],['2026-09-29','10:00','10:25','d6'],['2026-09-30','09:30','09:55','d3'],['2026-09-30','14:00','14:20','d5'],['2026-10-01','10:00','10:25','d9'],['2026-10-01','11:00','11:20','d14'],['2026-10-02','09:30','09:55','d10'],['2026-10-02','11:00','11:25','d15']].forEach(a=>{const d=D[a[3]];add(a[0],a[1],a[2],a[3],[T(d.prods[0],'t')].concat(d.prods[1]?[T(d.prods[1],'h')]:[]),'raporlandi',{repAt:a[0]})});
// 41. hafta
add('2026-10-05','09:00','09:25','d1',[T('KARDIVA','t'),T('ALMIBA','h')],'raporlandi');
add('2026-10-05','09:40','10:00','d2',[T('TUTUKON','t'),T('ALMIBA','h')],'suredoldu',{doneAt:'2026-10-05T10:02'});
add('2026-10-05','10:15','10:35','d11',[T('TUTUKON','t')],'kacirildi');
add('2026-10-05','14:00','14:20','d5',[T('TUTUKON','t'),T('GLUKOFIT','h')],'raporlandi');
add('2026-10-05','14:40','15:00','d4',[T('UROFLEKS','t'),T('TUTUKON','h')],'yapilamadi',{reason:'Doktor yok / izinde',note:'Kongrede, 9 Ekim\'de dönüyor.',reasonAt:'5 Eki 15:10'});
add('2026-10-06','09:30','09:55','d6',[T('NOROMAX','t'),T('TUTUKON','h')],'raporlandi');
add('2026-10-06','10:05','10:30','d7',[T('GLUKOFIT','t'),T('ALMIBA','h')],'raporlandi');
add('2026-10-06','10:45','11:05','d8',[T('PULMOSET','t')],'ertelendi',{reason:'Doktor zamanı yok / görüşmeyi reddetti',note:'Ameliyat listesi uzadı. 13 Eki\'ye kaydırıldı.',reasonAt:'6 Eki 10:50',newDate:'2026-10-13'});
add('2026-10-06','14:00','14:25','d9',[T('TUTUKON','t'),T('ALMIBA','h')],'kacirildi');
add('2026-10-06','14:40','15:00','d10',[T('KARDIVA','t')],'iptal',{reason:'Toplantı / eğitim çakışması',note:'Bölge toplantısı öne alındı.',reasonAt:'5 Eki 18:20'});
add('2026-10-07','09:30','09:55','d3',[T('GLUKOFIT','t'),T('ALMIBA','h')],'raporlandi',{repAt:'2026-10-07T10:05'});
add('2026-10-07','10:15','10:40','ph1',[T('TUTUKON','t'),T('ALMIBA','h'),T('GLUKOFIT','h')],'raporlandi');
add('2026-10-07','14:00','14:20','d12',[T('TUTUKON','t'),T('GLUKOFIT','h')],'raporlandi');
add('2026-10-07','17:15','17:40','d11',[T('TUTUKON','t')],'raporEksik',{unplanned:true,doneAt:'2026-10-07T17:40'});
add('2026-10-08','09:50','10:20','d13',[T('KARDIVA','t'),T('ALMIBA','h')],'raporlandi',{repAt:'2026-10-08T10:34'});
add('2026-10-08','10:30','10:55','d14',[T('TUTUKON','t'),T('ALMIBA','h')],'devam',{startedAt:'2026-10-08T10:31'});
add('2026-10-08','11:30','11:55','d15',[T('UROFLEKS','t'),T('TUTUKON','h')],'hazir');
add('2026-10-08','14:00','14:25','d16',[T('GLUKOFIT','t'),T('ALMIBA','h')],'hazir');
add('2026-10-08','15:00','15:20','ph2',[T('TUTUKON','t'),T('GLUKOFIT','h')],'hazir');
add('2026-10-09','09:30','09:55','d10',[T('KARDIVA','t'),T('ALMIBA','h')],'plan',{pinned:true});
add('2026-10-09','10:15','10:40','d9',[T('TUTUKON','t'),T('ALMIBA','h')],'plan');
add('2026-10-09','11:00','11:20','d4',[T('UROFLEKS','t'),T('TUTUKON','h')],'plan');
add('2026-10-09','14:00','14:20','d2',[T('TUTUKON','t'),T('ALMIBA','h')],'plan');
// 42–44. hafta taslak
const order=['d1','d3','d11','d5','d4','d6','d7','d8','d13','d9','d10','d16','d2','d12','d14','d15','ph1'];
const slots=[['09:30','09:55'],['10:15','10:40'],['11:00','11:25'],['14:00','14:25']];
const days=['2026-10-12','2026-10-13','2026-10-14','2026-10-15','2026-10-16','2026-10-19','2026-10-20','2026-10-21','2026-10-22','2026-10-23','2026-10-26','2026-10-27'];
let oi=0;days.forEach((dd,di)=>{const n=dd==='2026-10-16'?3:4;for(let k=0;k<n;k++){const id=order[oi++%order.length];const d=D[id];add(dd,slots[k][0],slots[k][1],id,[T(d.prods[0],'t')].concat(d.prods[1]?[T(d.prods[1],'h')]:[]),'taslak')}});
add('2026-10-13','15:00','15:20','d8',[T('PULMOSET','t')],'taslak',{pinned:true,fromPost:true});
const HOL={'2026-10-28':'Yarım gün · Cumhuriyet Bayramı arifesi','2026-10-29':'Tatil · Cumhuriyet Bayramı'};
const WEEKS={40:{st:'gecmis',r:'28 Eyl–2 Eki'},41:{st:'onayli',r:'5–9 Eki',cap:38.3},42:{st:'taslak',r:'12–16 Eki',cap:38.3,slip:2},43:{st:'taslak',r:'19–23 Eki',cap:38.3},44:{st:'taslak',r:'26–30 Eki',cap:26.5},45:{st:'bos',r:'2–6 Kas'}};
const WST={gecmis:{l:'Geçmiş',bg:'var(--seb)',fg:'var(--se)'},onayli:{l:'Onaylı',bg:'var(--okb)',fg:'var(--ok)'},taslak:{l:'Taslak · otomatik',bg:'var(--pl)',fg:'var(--pt)'},bos:{l:'Boş',bg:'var(--soft)',fg:'var(--mu)'}};
const AYLAR=['Oca','Şub','Mar','Nis','May','Haz','Tem','Ağu','Eyl','Eki','Kas','Ara'];
const GUN=['Paz','Pzt','Sal','Çar','Per','Cum','Cmt'];
const GUNL=['Pazar','Pazartesi','Salı','Çarşamba','Perşembe','Cuma','Cumartesi'];
function dt(v,k){const [y,m,d]=v.date.split('-').map(Number);const [h,mi]=v[k].split(':').map(Number);return new Date(y,m-1,d,h,mi)}
function fmtD(ds){const [y,m,d]=ds.split('-').map(Number);const x=new Date(y,m-1,d);return d+' '+AYLAR[m-1]+' '+GUNL[x.getDay()]}
function hoursLeft(v){
  if(v.status==='raporEksik'){const done=new Date(v.doneAt||dt(v,'end'));return Math.max(0,Math.round((done.getTime()+48*3600e3-NOW.getTime())/3600e3))}
  if(v.status==='kacirildi'){return Math.round((dt(v,'start').getTime()+48*3600e3-NOW.getTime())/3600e3)}
  return null}
function minsSince(iso){return Math.round((NOW.getTime()-new Date(iso).getTime())/60000)}
function dur(v){return Math.round((dt(v,'end')-dt(v,'start'))/60000)}
function estimate(v){const t=v.prods.filter(p=>p.r==='t').length,h=v.prods.length-t;const m=t*ROLE.t.min+h*ROLE.h.min+8;return {t,h,m,l:[t?t+' tanıtım':'',h?h+' hatırlatma':''].filter(Boolean).join(' + ')+' + rapor ≈ '+m+' dk'}}
function weekOf(ds){const [y,m,d]=ds.split('-').map(Number);const x=new Date(y,m-1,d);const j=new Date(2026,0,1);const mon=new Date(x);mon.setDate(x.getDate()-((x.getDay()+6)%7));const w1=new Date(j);w1.setDate(j.getDate()-((j.getDay()+6)%7));return Math.round((mon-w1)/(7*864e5))+1}
const SLIDES={
  TUTUKON:[{t:'TUTUKON 100 mg',s:'Kronik koroner sendromda günde tek doz',b:['Anjina ataklarında azalma','Egzersiz kapasitesinde artış','Günde tek doz, yemekle ilişkisiz'],ref:'[1] TUTUKON KÜB, 2025.'},{t:'Klinik kanıt',s:'12 haftalık randomize çalışma',b:['Haftalık anjina atağı: −%48 (plaseboya göre)','Nitrat kullanımı: −%41','Bırakma oranı plaseboya benzer'],ref:'[2] Onaylı iddia metni TTK-CL-07.'},{t:'Kimler için?',s:'Uygun hasta profili',b:['Beta bloker ile kontrol altına alınamayan anjina','Kalp hızını düşürmeden ek etki','Yaşlı hastada doz ayarı gerekmez'],ref:'[1] TUTUKON KÜB, bölüm 4.2.'},{t:'Güvenlilik',s:'İyi tolere edilir',b:['En sık: baş ağrısı (%4)','QT etkileşimi uyarısı','Gebelikte önerilmez'],ref:'[1] TUTUKON KÜB, bölüm 4.4, 4.8.'}],
  ALMIBA:[{t:'ALMIBA 5/10 mg',s:'Hipertansiyonda tek tablette ikili kontrol',b:['24 saat kan basıncı kontrolü','Tek tablet, günde bir kez'],ref:'[3] ALMIBA KÜB, 2026.'},{t:'Hatırlatma',s:'Tedavi uyumunda tek tablet farkı',b:['Uyum: ayrı tabletlere göre +%23','Geri ödeme kapsamında'],ref:'[4] Onaylı iddia metni ALM-HT-02.'}],
  UROFLEKS:[{t:'ÜROFLEKS 0,4 mg',s:'BPH semptomlarında hızlı rahatlama',b:['1. haftada IPSS iyileşmesi','Gece idrara çıkmada azalma','Günde tek doz'],ref:'[5] ÜROFLEKS KÜB, 2025.'},{t:'Klinik kanıt',s:'IPSS skorunda değişim',b:['12. haftada −8,1 puan','Maksimum akım hızı +2,9 mL/s'],ref:'[6] Onaylı iddia metni URF-CL-03.'},{t:'Kullanım',s:'Doz ve uyarılar',b:['Kahvaltıdan sonra tek doz','Ortostatik hipotansiyon uyarısı','Katarakt cerrahisi öncesi bildirim'],ref:'[5] ÜROFLEKS KÜB, bölüm 4.4.'}],
  GLUKOFIT:[{t:'GLUKOFİT 850 mg',s:'Tip 2 diyabette temel tedavi',b:['HbA1c\'de anlamlı düşüş','Kilo nötr'],ref:'[7] GLUKOFİT KÜB.'},{t:'Hatırlatma',s:'GİS toleransı',b:['Yemekle birlikte alım','Yavaş doz titrasyonu'],ref:'[7] GLUKOFİT KÜB, 4.2.'}],
  KARDIVA:[{t:'KARDİVA 50 mg',s:'Kalp yetmezliğinde hastaneye yatışta azalma',b:['Yatış riski −%21'],ref:'[8] KARDİVA KÜB.'}],
  NOROMAX:[{t:'NÖROMAX 300 mg',s:'Nöropatik ağrıda gece rahatlığı',b:['Uyku kalitesinde iyileşme'],ref:'[9] NÖROMAX KÜB.'}],
  PULMOSET:[{t:'PULMOSET 200 mcg',s:'KOAH idame tedavisi',b:['Alevlenmelerde azalma'],ref:'[10] PULMOSET KÜB.'}]
};

const COMPD={
 'Koronex 100':{f:'Medira İlaç',api:'trimetazidin',form:'35 mg MR tablet',fiyat:'₺182',go:'Geri ödemede',pk:'%31',pb:'%27',of:'TUTUKON'},
 'Anjiyosed':{f:'Novaton Pharma',api:'ivabradin',form:'5 mg tablet',fiyat:'₺298',go:'Geri ödemede · rapor şartlı',pk:'%18',pb:'%22',of:'TUTUKON'},
 'Tensilox':{f:'Bilgen İlaç',api:'amlodipin + valsartan',form:'5/160 mg tablet',fiyat:'₺245',go:'Geri ödemede',pk:'%34',pb:'%29',of:'ALMIBA'},
 'Amlopres':{f:'Teraform',api:'amlodipin + perindopril',form:'5/5 mg tablet',fiyat:'₺198',go:'Geri ödemede',pk:'%21',pb:'%25',of:'ALMIBA'},
 'Valsen Plus':{f:'Asklepa',api:'valsartan + hidroklorotiyazid',form:'160/12,5 mg tablet',fiyat:'₺176',go:'Geri ödemede',pk:'%12',pb:'%15',of:'ALMIBA'},
 'Diaform':{f:'Medira İlaç',api:'metformin',form:'1000 mg tablet',fiyat:'₺78',go:'Geri ödemede',pk:'%41',pb:'%38',of:'GLUKOFIT'},
 'Metgluk XR':{f:'Novaton Pharma',api:'metformin XR',form:'750 mg tablet',fiyat:'₺112',go:'Geri ödemede',pk:'%19',pb:'%17',of:'GLUKOFIT'},
 'Kardiyonil':{f:'Bilgen İlaç',api:'karvedilol',form:'25 mg tablet',fiyat:'₺96',go:'Geri ödemede',pk:'%28',pb:'%31',of:'KARDIVA'},
 'Cormax':{f:'Teraform',api:'metoprolol süksinat',form:'50 mg tablet',fiyat:'₺134',go:'Geri ödemede',pk:'%22',pb:'%20',of:'KARDIVA'},
 'Gabanor':{f:'Asklepa',api:'gabapentin',form:'300 mg kapsül',fiyat:'₺118',go:'Geri ödemede',pk:'%26',pb:'%24',of:'NOROMAX'},
 'Pregaval':{f:'Medira İlaç',api:'pregabalin',form:'150 mg kapsül',fiyat:'₺205',go:'Geri ödemede · rapor şartlı',pk:'%37',pb:'%33',of:'NOROMAX'},
 'Bronkair':{f:'Novaton Pharma',api:'formoterol + budesonid',form:'160/4,5 mcg inhaler',fiyat:'₺420',go:'Geri ödemede',pk:'%30',pb:'%28',of:'PULMOSET'},
 'Tiovent':{f:'Bilgen İlaç',api:'tiotropium',form:'18 mcg inhaler',fiyat:'₺468',go:'Geri ödemede',pk:'%25',pb:'%27',of:'PULMOSET'},
 'Prostam':{f:'Teraform',api:'silodosin',form:'8 mg kapsül',fiyat:'₺265',go:'Geri ödemede',pk:'%20',pb:'%18',of:'UROFLEKS'},
 'Tamsulin':{f:'Asklepa',api:'tamsulosin',form:'0,4 mg kapsül',fiyat:'₺142',go:'Geri ödemede',pk:'%44',pb:'%41',of:'UROFLEKS'}
};
const NOTES={
 UROFLEKS:[
  {key:'İlk haftada semptom rahatlaması: hasta tedaviye bağlı kalır.',script:'Hocam, BPH hastalarınızda en sık şikayet gece idrara çıkma. ÜROFLEKS ilk haftadan itibaren IPSS skorunda iyileşme sağlıyor ve günde tek doz. Poliklinikte bu profilde haftada kaç hasta görüyorsunuz?',sure:60,trans:'Bu hızlı etkinin arkasındaki veriye bakalım.',obj:[{q:'Ortostatik hipotansiyon riski?',a:'KÜB 4.4: ilk dozda dikkat önerilir; kahvaltıdan sonra tek doz alınır. [5]'}]},
  {key:'12. haftada IPSS −8,1 puan, akım hızı +2,9 mL/s.',script:'12 haftalık çalışmada IPSS skorunda 8,1 puanlık düşüş ve maksimum akım hızında 2,9 mL/s artış görüldü. Bu iki değer onaylı iddia metninde yer alıyor; başka karşılaştırma yapmayın.',sure:75,trans:'Peki hangi hastada başlamalı, kullanım nasıl?',obj:[{q:'Tamsulin ile farkı ne?',a:'Onaylı karşılaştırma iddiası yok; yalnız kendi çalışma verimizi paylaşın. Karşılaştırmalı soru için MSL görüşmesi önerin.',comp:'Tamsulin'},{q:'Fiyatı yüksek.',a:'Geri ödeme kapsamında; hasta katkı payını sorarsa güncel liste fiyatını paylaşın, fiyat iddiası kurmayın.',comp:'Prostam'}]},
  {key:'Kahvaltıdan sonra tek doz.',script:'Kullanım basit: kahvaltıdan sonra tek kapsül. Katarakt cerrahisi planlanan hastada göz hekimini bilgilendirmesini hatırlatın.',sure:45,trans:'Şimdi kısaca TUTUKON\'u hatırlatmak istiyorum.',obj:[]}
 ],
 TUTUKON:[
  {key:'Beta bloker yetmediğinde kalp hızını düşürmeden ek etki.',script:'Geçen ziyarette geri ödemeyi sormuştunuz: TUTUKON SGK listesinde. Beta blokerle kontrol edilemeyen anjinada ek seçenek olarak düşünebilirsiniz.',sure:45,trans:'Teşekkürler hocam; literatürü 12 Ekim\'e kadar ileteceğim.',obj:[{q:'Koronex ile memnunum.',a:'Rakibi kötülemeyin. Günde tek doz ve kalp hızını etkilememe özelliğini vurgulayın. [1]',comp:'Koronex 100'},{q:'Geri ödeme şartı var mı?',a:'SGK listesinde; uzman raporu gerekmez. [2]'}]},
  {key:'12 haftada anjina atağı −%48.',script:'Randomize çalışmada haftalık anjina ataklarında plaseboya göre %48 azalma görüldü; nitrat kullanımı da %41 azaldı.',sure:60,trans:'Hangi hastada düşünebileceğinize bakalım.',obj:[]},
  {key:'Yaşlı hastada doz ayarı gerekmez.',script:'Uygun hasta: beta blokerle kontrol altına alınamayan anjina. Yaşlı hastada doz ayarı gerekmiyor.',sure:45,trans:'Güvenlilik verisine kısaca değineyim.',obj:[]},
  {key:'En sık yan etki baş ağrısı (%4).',script:'Genelde iyi tolere ediliyor; en sık baş ağrısı. QT uzatan ilaçlarla birlikte dikkat.',sure:40,trans:'Sorularınız varsa alayım.',obj:[]}
 ]
};
const DOCNOTE={
 d15:['Son ziyaret 21 Eyl: ÜROFLEKS · Değerlendiriyor · ilgili.','Açık talep: literatür, 12 Eki\'ye kadar.','Açık itiraz: TUTUKON geri ödeme · kısmen giderildi.','Verilen numune: 2 kutu ÜROFLEKS (21 Eyl).','İletişim stili analitik: veriyle, kısa konuşun.']
};

const INDP={
 TUTUKON:[['Kronik koroner sendrom','Beta bloker ile kontrol edilemeyen anjina'],['Kronik koroner sendrom','Diyabet eşlik eden'],['Kronik koroner sendrom','75 yaş üstü']],
 ALMIBA:[['Hipertansiyon','Monoterapi ile kontrol edilemeyen'],['Hipertansiyon','Yeni tanı, evre 2'],['Hipertansiyon','Diyabet eşlik eden']],
 GLUKOFIT:[['Tip 2 diyabet','Yeni tanı'],['Tip 2 diyabet','Obez (VKİ ≥ 30)'],['Prediyabet','Yüksek risk']],
 UROFLEKS:[['BPH','Orta–şiddetli alt üriner sistem semptomu'],['BPH','Gece idrara çıkma baskın'],['Üreter taşı','Distal taş, ekspulsiyon tedavisi']],
 KARDIVA:[['Kalp yetmezliği','HFrEF, NYHA II–III'],['Kalp yetmezliği','Yatış sonrası ilk 3 ay']],
 NOROMAX:[['Nöropatik ağrı','Diyabetik nöropati'],['Nöropatik ağrı','Postherpetik nevralji']],
 PULMOSET:[['KOAH','Orta–ağır, sık alevlenme'],['Astım','ICS ile kontrolsüz']]
};
const SKU={TUTUKON:[['TUTUKON 100 mg · 28 tb',360],['TUTUKON 100 mg · 56 tb',690]],ALMIBA:[['ALMIBA 5/10 mg · 28 tb',210],['ALMIBA 5/10 mg · 84 tb',590]],GLUKOFIT:[['GLUKOFİT 850 mg · 60 tb',95],['GLUKOFİT 850 mg · 100 tb',148]],UROFLEKS:[['ÜROFLEKS 0,4 mg · 30 kps',290]],KARDIVA:[['KARDİVA 50 mg · 28 tb',420]],NOROMAX:[['NÖROMAX 300 mg · 56 kps',330]],PULMOSET:[['PULMOSET 200 mcg · 60 doz',510]]};
const DEPO=['Selçuk Ecza Deposu · Şişli','Hedef Alliance · Kağıthane','Nevzat Ecza · Beyoğlu','As Ecza · Mecidiyeköy'];
const CUR={TRY:{s:'₺',r:1},EUR:{s:'€',r:41.2},USD:{s:'$',r:35.6}};
const PEOPLE={rep:{n:'Okan Tekin',m:'okan.tekin@grandmedical.eu',r:'Tıbbi Mümessil · İstanbul Avrupa 3'},mgr:{n:'Selin Karaca',m:'selin.karaca@grandmedical.eu',r:'Bölge Müdürü · İstanbul Avrupa'},pv:{n:'Farmakovijilans',m:'pv@grandmedical.eu',r:'Farmakovijilans Birimi'},msl:{n:'Dr. Tolga Uçar',m:'tolga.ucar@grandmedical.eu',r:'MSL · Üroloji / Kardiyoloji'}};
window.ZD={INDP,SKU,DEPO,CUR,PEOPLE,COMPD,NOTES,DOCNOTE,NOW,P,ACC,D,ADOPT,ROLE,ST,REASONS,OBJ,REQ,ADV,COMP,V,HOL,WEEKS,WST,AYLAR,GUN,GUNL,SLIDES,dt,fmtD,hoursLeft,minsSince,dur,estimate,weekOf,TODAY:'2026-10-08'};
})();
