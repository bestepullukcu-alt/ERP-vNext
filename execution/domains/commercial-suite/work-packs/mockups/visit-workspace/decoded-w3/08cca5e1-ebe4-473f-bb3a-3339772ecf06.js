// Demo veri — W3 yazım ekranları
(function(){
const P={UROFLEKS:{n:'ÜROFLEKS',f:'0,4 mg kapsül',ind:'BPH'},TUTUKON:{n:'TUTUKON',f:'100 mg film tablet',ind:'Kronik koroner sendrom'},ALMIBA:{n:'ALMIBA',f:'5/10 mg tablet',ind:'Hipertansiyon'}};
const CAT=[
 {l:'Fiyat',ar:'السعر',bg:'var(--wab)',fg:'var(--wa)'},
 {l:'Geri ödeme',ar:'السداد',bg:'var(--inb)',fg:'var(--in)'},
 {l:'Etkinlik',ar:'الفعالية',bg:'var(--okb)',fg:'var(--ok)'},
 {l:'Yan etki',ar:'الآثار الجانبية',bg:'var(--dab)',fg:'var(--da)'},
 {l:'Doz / kullanım',ar:'الجرعة / الاستخدام',bg:'var(--pl)',fg:'var(--pt)'},
 {l:'Erişim / stok',ar:'التوفر / المخزون',bg:'var(--seb)',fg:'var(--se)'},
 {l:'Alışkanlık / rakip memnuniyeti',ar:'العادة / الرضا عن المنافس',bg:'#efe7fd',fg:'#6a3dc0'},
 {l:'Kanıt yetersiz',ar:'الأدلة غير كافية',bg:'#fde7f0',fg:'#b0306a'},
 {l:'Diğer',ar:'أخرى',bg:'transparent',fg:'var(--se)',bd:'1px solid var(--bd2)'}
];
const CLAIMS=[
 {id:'URF-CL-03',p:'UROFLEKS',t:'12. haftada IPSS skorunda −8,1 puan; maksimum akım hızında +2,9 mL/s artış.',ref:'[6] ÜROFLEKS Faz III çalışması, 2024.'},
 {id:'URF-SF-01',p:'UROFLEKS',t:'İlk dozda ortostatik hipotansiyon görülebilir; kahvaltıdan sonra alınması önerilir.',ref:'[5] ÜROFLEKS KÜB, bölüm 4.4.'},
 {id:'URF-DZ-01',p:'UROFLEKS',t:'Günde tek doz, kahvaltıdan sonra tek kapsül.',ref:'[5] ÜROFLEKS KÜB, bölüm 4.2.'},
 {id:'URF-GO-02',p:'UROFLEKS',t:'SGK geri ödeme listesinde; uzman raporu gerekmez.',ref:'[11] SUT Ek-4/A, Eylül 2026.'},
 {id:'URF-CL-05',p:'UROFLEKS',t:'1. haftadan itibaren gece idrara çıkma sıklığında azalma.',ref:'[6] ÜROFLEKS Faz III çalışması, 2024.'},
 {id:'TTK-CL-07',p:'TUTUKON',t:'12 haftada haftalık anjina ataklarında plaseboya göre %48 azalma.',ref:'[2] TUTUKON randomize çalışma, 2023.'},
 {id:'TTK-GO-01',p:'TUTUKON',t:'SGK listesinde; uzman raporu gerekmez.',ref:'[12] SUT Ek-4/A, Eylül 2026.'},
 {id:'TTK-SF-02',p:'TUTUKON',t:'En sık yan etki baş ağrısıdır (%4).',ref:'[1] TUTUKON KÜB, bölüm 4.8.'},
 {id:'TTK-DZ-01',p:'TUTUKON',t:'Yaşlı hastada doz ayarı gerekmez.',ref:'[1] TUTUKON KÜB, bölüm 4.2.'},
 {id:'ALM-HT-02',p:'ALMIBA',t:'Tek tablet kullanımında tedavi uyumu ayrı tabletlere göre %23 daha yüksek.',ref:'[4] ALMIBA uyum analizi, 2025.'},
 {id:'ALM-CL-01',p:'ALMIBA',t:'Günde tek tabletle 24 saat kan basıncı kontrolü.',ref:'[3] ALMIBA KÜB, 2026.'},
 {id:'ALM-GO-01',p:'ALMIBA',t:'Geri ödeme kapsamında.',ref:'[13] SUT Ek-4/A, Eylül 2026.'}
];
const PEOPLE={yazar:{n:'Elif Kaya',r:'İçerik editörü',i:'EK'},onay:{n:'Dr. Murat Aydın',r:'Medikal onaylayıcı',i:'MA'},salt:{n:'Can Yücel',r:'Ürün müdürü · salt okuma',i:'CY'},yetkisiz:{n:'Okan Tekin',r:'Tıbbi mümessil',i:'OT'},admin:{n:'Deniz Er',r:'CRM kiracı yöneticisi',i:'DE'}};
const U=(path,ver,step)=>({path,ver,step});
const OBJ=[
 {id:'o1',p:'UROFLEKS',cat:3,q:'Ortostatik hipotansiyon riski var mı?',q_ar:'هل يوجد خطر انخفاض ضغط الدم الانتصابي؟',a:'KÜB bölüm 4.4\'e göre ilk dozda ortostatik hipotansiyon görülebilir. Kahvaltıdan sonra tek doz alınması önerilir; hastaya ilk günlerde ani ayağa kalkmaması söylenmelidir.',claims:['URF-SF-01','URF-DZ-01'],used:[U('ÜROFLEKS · BPH tanıtım','v3','1 · ÜROFLEKS 0,4 mg'),U('ÜROFLEKS · BPH tanıtım','v4','1 · ÜROFLEKS 0,4 mg')],st:'onayli',by:'Elif Kaya',at:'2026-09-18 10:12',apBy:'Dr. Murat Aydın',apAt:'2026-09-19 16:40',mBy:'Dr. Murat Aydın',mAt:'2026-09-19 16:40'},
 {id:'o2',p:'UROFLEKS',cat:6,q:'Tamsulin ile memnunum, neden değiştireyim?',q_ar:'أنا راضٍ عن تامسولين، لماذا أغيّر؟',a:'Rakip ürünü kötülemeyin; onaylı karşılaştırma iddiası yoktur. Kendi çalışma verimizi paylaşın: 12. haftada IPSS skorunda −8,1 puan. Karşılaştırmalı soru için MSL görüşmesi önerin.',claims:['URF-CL-03'],used:[U('ÜROFLEKS · BPH tanıtım','v3','2 · Klinik kanıt'),U('ÜROFLEKS · BPH tanıtım','v4','2 · Klinik kanıt')],st:'onayli',by:'Elif Kaya',at:'2026-09-18 10:30',apBy:'Dr. Murat Aydın',apAt:'2026-09-19 16:42',mBy:'Dr. Murat Aydın',mAt:'2026-09-19 16:42'},
 {id:'o3',p:'UROFLEKS',cat:0,q:'Fiyatı rakiplerine göre yüksek.',q_ar:'السعر مرتفع مقارنة بالمنافسين.',a:'Ürün geri ödeme kapsamındadır. Hasta katkı payı sorulursa güncel liste fiyatını paylaşın; fiyat karşılaştırması ya da fiyat iddiası kurmayın.',claims:['URF-GO-02'],used:[],st:'taslak',pending:true,by:'Elif Kaya',at:'2026-10-09 14:05',mBy:'Elif Kaya',mAt:'2026-10-10 09:18'},
 {id:'o4',p:'UROFLEKS',cat:1,q:'Reçete için uzman raporu gerekiyor mu?',q_ar:'هل يلزم تقرير طبي متخصص للوصفة؟',a:'Hayır. ÜROFLEKS SGK geri ödeme listesindedir ve uzman raporu gerekmez.',claims:['URF-GO-02'],used:[U('ÜROFLEKS · BPH tanıtım','v4','2 · Klinik kanıt')],st:'onayli',by:'Burak Öz',at:'2026-09-22 11:00',apBy:'Dr. Murat Aydın',apAt:'2026-09-23 09:15',mBy:'Dr. Murat Aydın',mAt:'2026-09-23 09:15'},
 {id:'o5',p:'TUTUKON',cat:6,q:'Koronex ile memnunum.',q_ar:'أنا راضٍ عن كورونكس.',a:'Rakibi kötülemeyin. Günde tek doz ve kalp hızını düşürmeden ek etki özelliğini vurgulayın; beta blokerle kontrol edilemeyen anjinada ek seçenek olarak önerin.',claims:['TTK-CL-07'],used:[U('TUTUKON · Kronik koroner sendrom','v2','1 · TUTUKON 100 mg')],st:'onayli',by:'Burak Öz',at:'2026-08-30 15:20',apBy:'Dr. Murat Aydın',apAt:'2026-09-01 10:05',mBy:'Dr. Murat Aydın',mAt:'2026-09-01 10:05'},
 {id:'o6',p:'TUTUKON',cat:1,q:'Geri ödeme şartı var mı?',q_ar:'هل هناك شروط للسداد؟',a:'TUTUKON SGK listesindedir; uzman raporu gerekmez.',claims:['TTK-GO-01'],used:[U('TUTUKON · Kronik koroner sendrom','v2','1 · TUTUKON 100 mg')],st:'onayli',by:'Burak Öz',at:'2026-08-30 15:32',apBy:'Dr. Murat Aydın',apAt:'2026-09-01 10:06',mBy:'Dr. Murat Aydın',mAt:'2026-09-01 10:06'},
 {id:'o7',p:'TUTUKON',cat:7,q:'Çalışma yalnız 12 hafta; uzun dönem veri var mı?',q_ar:'الدراسة 12 أسبوعًا فقط؛ هل توجد بيانات طويلة الأمد؟',a:'Onaylı iddia 12 haftalık randomize çalışmaya dayanır: haftalık anjina ataklarında %48 azalma. Uzun dönem veriyle ilgili soruyu MSL görüşmesine yönlendirin.',claims:['TTK-CL-07'],used:[],st:'taslak',pending:true,by:'Dr. Murat Aydın',at:'2026-10-08 10:44',mBy:'Dr. Murat Aydın',mAt:'2026-10-08 10:44'},
 {id:'o8',p:'TUTUKON',cat:3,q:'QT uzaması riski nedir?',q_ar:'ما خطر إطالة فترة QT؟',a:'QT uzatan ilaçlarla birlikte kullanımda dikkat edilmelidir.',claims:['TTK-SF-02'],used:[],st:'emekli',by:'Burak Öz',at:'2026-06-12 09:00',apBy:'Dr. Murat Aydın',apAt:'2026-06-13 11:20',retBy:'Dr. Murat Aydın',retAt:'2026-09-01 10:10',mBy:'Dr. Murat Aydın',mAt:'2026-09-01 10:10'},
 {id:'o9',p:'ALMIBA',cat:4,q:'Sabah mı akşam mı verilmeli?',q_ar:'هل يُعطى صباحًا أم مساءً؟',a:'Günde tek tablet; her gün aynı saatte alınması yeterlidir.',claims:['ALM-CL-01'],used:[],st:'taslak',by:'Elif Kaya',at:'2026-10-11 17:02',mBy:'Elif Kaya',mAt:'2026-10-11 17:02'},
 {id:'o10',p:'ALMIBA',cat:2,q:'Ayrı tabletlerden farkı ne?',q_ar:'ما الفرق عن الأقراص المنفصلة؟',a:'Tek tablet kullanımında tedavi uyumu ayrı tabletlere göre %23 daha yüksektir. 24 saat kan basıncı kontrolü sağlar.',claims:['ALM-HT-02','ALM-CL-01'],used:[U('ALMIBA · Hatırlatma','v1','2 · Hatırlatma')],st:'onayli',by:'Elif Kaya',at:'2026-07-04 13:10',apBy:'Dr. Murat Aydın',apAt:'2026-07-05 08:50',mBy:'Dr. Murat Aydın',mAt:'2026-07-05 08:50'},
 {id:'o11',p:'ALMIBA',cat:5,q:'Eczanede bulamıyorum.',q_ar:'لا أجده في الصيدلية.',a:'Ürün ana depolarda mevcuttur. Bölgede stok sorunu varsa bölge müdürünüze iletin; eczane siparişini ziyaretten oluşturabilirsiniz.',claims:[],used:[U('ALMIBA · Hatırlatma','v1','2 · Hatırlatma')],st:'onayli',by:'Burak Öz',at:'2026-07-04 13:40',apBy:'Dr. Murat Aydın',apAt:'2026-07-05 08:55',mBy:'Dr. Murat Aydın',mAt:'2026-07-05 08:55'}
];
// Bilgi yolu: ÜROFLEKS · BPH tanıtım
const STEPS=[
 {t:'ÜROFLEKS 0,4 mg',s:'BPH semptomlarında hızlı rahatlama',b:['1. haftada IPSS iyileşmesi','Gece idrara çıkmada azalma','Günde tek doz'],ref:'[5] ÜROFLEKS KÜB, 2025.',est:60,n:{key:'İlk haftada semptom rahatlaması: hasta tedaviye bağlı kalır.',script:'Hocam, BPH hastalarınızda en sık şikayet gece idrara çıkma. ÜROFLEKS ilk haftadan itibaren IPSS skorunda iyileşme sağlıyor ve günde tek doz. Poliklinikte bu profilde haftada kaç hasta görüyorsunuz?',sure:'60',trans:'Bu hızlı etkinin arkasındaki veriye bakalım.',obj:['o1']}},
 {t:'Klinik kanıt',s:'IPSS skorunda değişim',b:['12. haftada −8,1 puan','Maksimum akım hızı +2,9 mL/s'],ref:'[6] Onaylı iddia metni URF-CL-03.',est:75,n:{key:'12. haftada IPSS −8,1 puan, akım hızı +2,9 mL/s.',script:'12 haftalık çalışmada IPSS skorunda 8,1 puanlık düşüş ve maksimum akım hızında 2,9 mL/s artış görüldü. Bu iki değer onaylı iddia metninde yer alıyor; başka karşılaştırma yapmayın.',sure:'75',trans:'Peki hangi hastada başlamalı, kullanım nasıl?',obj:['o2','o4']}},
 {t:'Kullanım',s:'Doz ve uyarılar',b:['Kahvaltıdan sonra tek doz','Ortostatik hipotansiyon uyarısı','Katarakt cerrahisi öncesi bildirim'],ref:'[5] ÜROFLEKS KÜB, bölüm 4.4.',est:45,n:{key:'Kahvaltıdan sonra tek doz.',script:'Kullanım basit: kahvaltıdan sonra tek kapsül. Katarakt cerrahisi planlanan hastada göz hekimini bilgilendirmesini hatırlatın.',sure:'45',trans:'Hangi hastada düşünebileceğinize bakalım.',obj:[]}},
 {t:'Kimler için?',s:'Uygun hasta profili',b:['Orta-şiddetli alt üriner sistem semptomları','50 yaş üstü, gece idrara çıkma şikayeti'],ref:'[5] ÜROFLEKS KÜB, bölüm 4.1.',est:45,n:{key:'',script:'',sure:'',trans:'',obj:[]}},
 {t:'Güvenlilik',s:'İyi tolere edilir',b:['En sık: baş dönmesi','Ortostatik hipotansiyon uyarısı'],ref:'[5] ÜROFLEKS KÜB, bölüm 4.8.',est:40,n:{key:'Güvenlilik profili KÜB ile uyumlu; en sık baş dönmesi.',script:'Genelde iyi tolere ediliyor. İlk dozda ortostatik hipotansiyona dikkat; bu yüzden kahvaltıdan sonra alınmasını öneriyoruz.',sure:'',trans:'Toparlayayım hocam.',obj:['o1']}},
 {t:'Kapanış',s:'Özet ve bir sonraki adım',b:['Hızlı semptom rahatlaması','Günde tek doz','Geri ödeme kapsamında'],ref:'[5] [6] [11]',est:30,n:{key:'',script:'',sure:'',trans:'',obj:[]}}
];
const PATH={n:'ÜROFLEKS · BPH tanıtım',p:'UROFLEKS',pubVer:'v3',draftVer:'v4',pubAt:'2026-09-24',pubBy:'Dr. Murat Aydın'};
const MON=['Oca','Şub','Mar','Nis','May','Haz','Tem','Ağu','Eyl','Eki','Kas','Ara'];
const MON_AR=['يناير','فبراير','مارس','أبريل','مايو','يونيو','يوليو','أغسطس','سبتمبر','أكتوبر','نوفمبر','ديسمبر'];
function fmt(iso,lang,withTime){if(!iso)return '';const [d,t]=iso.split(' ');const [y,m,dd]=d.split('-').map(Number);const s=lang==='ar'?dd+' '+MON_AR[m-1]+' '+y:dd+' '+MON[m-1]+' '+y;return withTime&&t?s+' · '+t:s}
const NOW='2026-10-12 11:20';
const VISIT_DEF={loc:'zorunlu',dist:'300',acc:'150',noLoc:'isaretle',photo:'istege',sign:true,dur:true,short:'120'};
const VISIT_SAVED={...VISIT_DEF,by:'Deniz Er',at:'2026-09-28 14:12'};
// Katmanlı ziyaret kanıtı ayarı
const SCOPES=[
 {id:'t',type:'tenant',n:'Kiracı varsayılanı',n_ar:'الإعداد الافتراضي للمستأجر',sub:'Grand Medical',parent:null,reps:148,ov:{...VISIT_DEF},by:'Deniz Er',at:'2026-09-28 14:12'},
 {id:'le1',type:'le',n:'Diten İlaç A.Ş.',n_ar:'Diten İlaç A.Ş.',sub:'Türkiye',sub_ar:'تركيا',parent:'t',reps:96,ov:{photo:'zorunlu',short:'180'},by:'Deniz Er',at:'2026-09-15 10:40'},
 {id:'bu1',type:'bu',n:'Hastane Ekibi',n_ar:'فريق المستشفيات',parent:'le1',reps:38,ov:{dist:'500',acc:'250'},by:'Deniz Er',at:'2026-10-02 16:05'},
 {id:'bu2',type:'bu',n:'KAM Ekibi',n_ar:'فريق الحسابات الرئيسية',parent:'le1',reps:12,ov:{loc:'bilgi'},by:'Deniz Er',at:'2026-09-30 09:20'},
 {id:'le2',type:'le',n:'Grand Medical Gulf FZ-LLC',n_ar:'Grand Medical Gulf FZ-LLC',sub:'BAE',sub_ar:'الإمارات',parent:'t',reps:24,ov:{photo:'zorunlu',sign:false},by:'Deniz Er',at:'2026-08-21 12:00'}
];
const SCOPE_ADD={le:[{n:'Arven Sağlık Ürünleri Ltd. Şti.',n_ar:'Arven Sağlık Ürünleri Ltd. Şti.',sub:'Türkiye',sub_ar:'تركيا',reps:28}],le1:[{n:'Bölge Satış Ekibi',n_ar:'فريق مبيعات المنطقة',reps:46}],le2:[{n:'Satış Ekibi',n_ar:'فريق المبيعات',reps:24}]};
const REPS=[
 {id:'r1',n:'Okan Tekin',le:'Diten İlaç A.Ş.',bu:'Hastane Ekibi',reg:'İstanbul Avrupa 3'},
 {id:'r2',n:'Ayşe Demir',le:'Diten İlaç A.Ş.',bu:'KAM Ekibi',reg:'İstanbul Anadolu 1'},
 {id:'r3',n:'Mert Şahin',le:'Diten İlaç A.Ş.',bu:'Bölge Satış Ekibi',reg:'Ankara 2'},
 {id:'r4',n:'Layla Haddad',le:'Grand Medical Gulf FZ-LLC',bu:'Satış Ekibi',reg:'Dubai'},
 {id:'r5',n:'Emre Kılıç',le:'Arven Sağlık Ürünleri Ltd. Şti.',bu:'Eczane Ekibi',reg:'İzmir 1'}
];
const EXC=[
 {id:'x1',rep:'r1',rules:['loc'],from:'2026-10-10',to:'2026-10-16',why:'Tablet GPS modülü arızalı; cihaz değişimi bekleniyor (IT-4471).',by:'Selin Karaca',at:'2026-10-09 17:20',apBy:'Deniz Er',apAt:'2026-10-09 17:45',st:'onayli'},
 {id:'x2',rep:'r2',rules:['noLoc'],from:'2026-10-14',to:'2026-10-28',why:'Memorial Şişli kaydında koordinat yok; MDM düzeltme talebi açıldı (MDM-2291).',by:'Deniz Er',at:'2026-10-11 10:05',apBy:'Selin Karaca',apAt:'2026-10-11 11:30',st:'onayli'},
 {id:'x3',rep:'r3',rules:['loc','photo'],from:'2026-09-01',to:'2026-09-07',why:'Cihaz değişimi; yeni tablet kurulum sürecinde.',by:'Selin Karaca',at:'2026-08-31 16:00',apBy:'Deniz Er',apAt:'2026-08-31 16:30',st:'onayli'},
 {id:'x4',rep:'r4',rules:['photo'],from:'2026-09-20',to:'2026-09-30',why:'Tablet kamerası arızalı.',by:'Deniz Er',at:'2026-09-19 09:10',apBy:'Selin Karaca',apAt:'2026-09-19 10:00',st:'iptal',cBy:'Deniz Er',cAt:'2026-09-24 10:00',cWhy:'Kamera onarıldı; istisnaya gerek kalmadı.'},
 {id:'x5',rep:'r5',rules:['loc'],from:'2026-10-13',to:'2026-10-20',why:'GPS sinyali kurum binasında alınamıyor; IT incelemesi sürüyor (IT-4502).',by:'Deniz Er',at:'2026-10-12 09:40',st:'taslak'},
 {id:'x6',rep:'r3',rules:['loc'],from:'2026-10-13',to:'2026-10-17',why:'Şişli Osmanoğlu Tıp Merkezi konumu yanlış kayıtlı (MDM-2304).',by:'Selin Karaca',at:'2026-10-12 08:55',st:'taslak'}
];
const HIST=[
 {s:'t',at:'2026-09-28 14:12',by:'Deniz Er',f:'short',a:'90 sn',b:'120 sn'},
 {s:'t',at:'2026-09-28 14:12',by:'Deniz Er',f:'loc',a:'Bilgi',b:'Zorunlu'},
 {s:'t',at:'2026-09-03 11:30',by:'Deniz Er',f:'dist',a:'500 m',b:'300 m'},
 {s:'t',at:'2026-06-01 09:00',by:'Deniz Er',f:'-',a:'—',b:'Kapsam oluşturuldu (varsayılanlar)'},
 {s:'le1',at:'2026-09-15 10:40',by:'Deniz Er',f:'photo',a:'Üstten: İsteğe bağlı',b:'Zorunlu'},
 {s:'le1',at:'2026-09-15 10:40',by:'Deniz Er',f:'short',a:'Üstten: 120 sn',b:'180 sn'},
 {s:'le1',at:'2026-09-15 10:38',by:'Deniz Er',f:'-',a:'—',b:'Kapsam oluşturuldu'},
 {s:'bu1',at:'2026-10-02 16:05',by:'Deniz Er',f:'dist',a:'Üstten: 300 m',b:'500 m'},
 {s:'bu1',at:'2026-10-02 16:05',by:'Deniz Er',f:'acc',a:'Üstten: 150 m',b:'250 m'},
 {s:'bu1',at:'2026-09-20 13:15',by:'Deniz Er',f:'noLoc',a:'Engellensin',b:'Üst ayara döndü'},
 {s:'bu1',at:'2026-09-12 10:00',by:'Deniz Er',f:'noLoc',a:'Üstten: Başlatılsın ve işaretlensin',b:'Engellensin'},
 {s:'bu1',at:'2026-09-12 09:58',by:'Deniz Er',f:'-',a:'—',b:'Kapsam oluşturuldu'},
 {s:'bu2',at:'2026-09-30 09:20',by:'Deniz Er',f:'loc',a:'Üstten: Zorunlu',b:'Bilgi'},
 {s:'le2',at:'2026-08-21 12:00',by:'Deniz Er',f:'photo',a:'Üstten: İsteğe bağlı',b:'Zorunlu'},
 {s:'le2',at:'2026-08-21 12:00',by:'Deniz Er',f:'sign',a:'Üstten: Açık',b:'Kapalı'}
];
window.W3D={P,CAT,CLAIMS,PEOPLE,OBJ,STEPS,PATH,fmt,NOW,VISIT_DEF,VISIT_SAVED,SCOPES,SCOPE_ADD,REPS,EXC,HIST};
})();
