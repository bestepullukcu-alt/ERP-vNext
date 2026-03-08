/**
 * Config
 * -------------------------------------------------------------------------------------
 * ! IMPORTANT: Make sure you clear the browser local storage In order to see the config changes in the template.
 * ! To clear local storage: (https://www.leadshook.com/help/how-to-clear-local-storage-in-google-chrome-browser/).
 */

'use strict';
/* JS global variables
 !Please use the hex color code (#000) here. Don't use rgba(), hsl(), etc
*/
window.config = {
  colors: {
    primary: window.Helpers.getCssVar('primary'),
    secondary: window.Helpers.getCssVar('secondary'),
    success: window.Helpers.getCssVar('success'),
    info: window.Helpers.getCssVar('info'),
    warning: window.Helpers.getCssVar('warning'),
    danger: window.Helpers.getCssVar('danger'),
    dark: window.Helpers.getCssVar('dark'),
    black: window.Helpers.getCssVar('pure-black'),
    white: window.Helpers.getCssVar('white'),
    cardColor: window.Helpers.getCssVar('paper-bg'),
    bodyBg: window.Helpers.getCssVar('body-bg'),
    bodyColor: window.Helpers.getCssVar('body-color'),
    headingColor: window.Helpers.getCssVar('heading-color'),
    textMuted: window.Helpers.getCssVar('secondary-color'),
    borderColor: window.Helpers.getCssVar('border-color')
  },
  colors_label: {
    primary: window.Helpers.getCssVar('primary-bg-subtle'),
    secondary: window.Helpers.getCssVar('secondary-bg-subtle'),
    success: window.Helpers.getCssVar('success-bg-subtle'),
    info: window.Helpers.getCssVar('info-bg-subtle'),
    warning: window.Helpers.getCssVar('warning-bg-subtle'),
    danger: window.Helpers.getCssVar('danger-bg-subtle'),
    dark: window.Helpers.getCssVar('dark-bg-subtle')
  },
  fontFamily: window.Helpers.getCssVar('font-family-base'),
  enableMenuLocalStorage: true
};

window.assetsPath = document.documentElement.getAttribute('data-assets-path');
window.templateName = document.documentElement.getAttribute('data-template');

if (typeof TemplateCustomizer !== 'undefined') {
  // MOD-0013: 8 Language Support for Template Customizer
  TemplateCustomizer.LANGUAGES.tr = {
    panel_header: 'Tema Özelleştirici',
    panel_sub_header: 'Gerçek zamanlı düzenleyin ve görün',
    theming_header: 'Temalar',
    color_label: 'Ana Renk',
    theme_label: 'Görünüm',
    skin_label: 'Stiller',
    semiDark_label: 'Yarı Karanlık Menü',
    layout_header: 'Yerleşim',
    layout_label: 'Menü (Gezinti)',
    layout_header_label: 'Başlık Tipleri',
    content_label: 'İçerik Genişliği',
    layout_navbar_label: 'Navigasyon Çubuğu',
    direction_label: 'Yön',
    style_switch_light: 'Açık',
    style_switch_dark: 'Koyu',
    layout_static: 'Sabit Olmayan',
    layout_offcanvas: 'Dışa Açılır',
    layout_fixed: 'Sabit',
    layout_fixed_offcanvas: 'Sabit Dışa Açılır',
    layout_dd_open_label: 'Üzerine Gelince Aç',
    layout_footer_label: 'Sabit Alt Bilgi',
    misc_header: 'Diğer',
    theme_light: 'Açık',
    theme_dark: 'Koyu',
    theme_system: 'Sistem',
    skin_default: 'Varsayılan',
    skin_bordered: 'Kenarlıklı',
    layout_expanded: 'Genişletilmiş',
    layout_collapsed: 'Daraltılmış',
    content_compact: 'Dar',
    content_wide: 'Geniş',
    direction_ltr: 'Soldan Sağa (En)',
    direction_rtl: 'Sağdan Sola (Ar)',
    navbar_sticky: 'Sabit',
    navbar_static: 'Statik',
    navbar_hidden: 'Gizli'
  };

  TemplateCustomizer.LANGUAGES.en = {
    panel_header: 'Template Customizer',
    panel_sub_header: 'Customize and preview in real time',
    theming_header: 'Theming',
    color_label: 'Primary Color',
    theme_label: 'Theme',
    skin_label: 'Skins',
    semiDark_label: 'Semi Dark',
    layout_header: 'Layout',
    layout_label: 'Menu (Navigation)',
    layout_header_label: 'Header Types',
    content_label: 'Content',
    layout_navbar_label: 'Navbar Type',
    direction_label: 'Direction',
    style_switch_light: 'Light',
    style_switch_dark: 'Dark',
    layout_static: 'Static',
    layout_offcanvas: 'Offcanvas',
    layout_fixed: 'Fixed',
    layout_fixed_offcanvas: 'Fixed Offcanvas',
    layout_dd_open_label: 'Open on hover',
    layout_footer_label: 'Fixed Footer',
    misc_header: 'Misc',
    theme_light: 'Light',
    theme_dark: 'Dark',
    theme_system: 'System',
    skin_default: 'Default',
    skin_bordered: 'Bordered',
    layout_expanded: 'Expanded',
    layout_collapsed: 'Collapsed',
    content_compact: 'Compact',
    content_wide: 'Wide',
    direction_ltr: 'Left to Right (En)',
    direction_rtl: 'Right to Left (Ar)',
    navbar_sticky: 'Sticky',
    navbar_static: 'Static',
    navbar_hidden: 'Hidden'
  };

  TemplateCustomizer.LANGUAGES.es = {
    panel_header: 'Personalizador de Plantilla',
    panel_sub_header: 'Personaliza y previsualiza en tiempo real',
    theming_header: 'Temas',
    color_label: 'Color Principal',
    theme_label: 'Apariencia',
    skin_label: 'Estilos',
    semiDark_label: 'Menú Semi Oscuro',
    layout_header: 'Diseño',
    layout_label: 'Menú (Navegación)',
    layout_header_label: 'Tipos de Encabezado',
    content_label: 'Ancho del Contenido',
    layout_navbar_label: 'Barra de Navegación',
    direction_label: 'Dirección',
    style_switch_light: 'Claro',
    style_switch_dark: 'Oscuro',
    layout_static: 'Estático',
    layout_offcanvas: 'Desplegable',
    layout_fixed: 'Fijo',
    layout_fixed_offcanvas: 'Fijo Desplegable',
    layout_dd_open_label: 'Abrir al pasar el ratón',
    layout_footer_label: 'Pie de página fijo',
    misc_header: 'Varios',
    theme_light: 'Claro',
    theme_dark: 'Oscuro',
    theme_system: 'Sistema',
    skin_default: 'Por defecto',
    skin_bordered: 'Con bordes',
    layout_expanded: 'Expandido',
    layout_collapsed: 'Contraído',
    content_compact: 'Compacto',
    content_wide: 'Ancho',
    direction_ltr: 'Izquierda a Derecha',
    direction_rtl: 'Derecha a Izquierda',
    navbar_sticky: 'Fijo',
    navbar_static: 'Estático',
    navbar_hidden: 'Oculto'
  };

  TemplateCustomizer.LANGUAGES.ru = {
    panel_header: 'Настройщик шаблона',
    panel_sub_header: 'Настройка и предварительный просмотр',
    theming_header: 'Темы',
    color_label: 'Основной цвет',
    theme_label: 'Вид',
    skin_label: 'Стили',
    semiDark_label: 'Полутемное меню',
    layout_header: 'Разметка',
    layout_label: 'Меню (Навигация)',
    layout_header_label: 'Типы заголовков',
    content_label: 'Ширина контента',
    layout_navbar_label: 'Панель навигации',
    direction_label: 'Направление',
    style_switch_light: 'Светлый',
    style_switch_dark: 'Темный',
    layout_static: 'Статичный',
    layout_offcanvas: 'Выпадающий',
    layout_fixed: 'Фиксированный',
    layout_fixed_offcanvas: 'Фиксированный выпадающий',
    layout_dd_open_label: 'Открыть при наведении',
    layout_footer_label: 'Фикс. футер',
    misc_header: 'Прочее',
    theme_light: 'Светлый',
    theme_dark: 'Темный',
    theme_system: 'Системный',
    skin_default: 'По умолчанию',
    skin_bordered: 'С рамкой',
    layout_expanded: 'Развернутый',
    layout_collapsed: 'Свернутый',
    content_compact: 'Компактный',
    content_wide: 'Широкий',
    direction_ltr: 'Слева направо',
    direction_rtl: 'Справа налево',
    navbar_sticky: 'Фиксированный',
    navbar_static: 'Статичный',
    navbar_hidden: 'Скрытый'
  };

  TemplateCustomizer.LANGUAGES.uk = {
    panel_header: 'Налаштування шаблону',
    panel_sub_header: 'Налаштування та попередній перегляд',
    theming_header: 'Теми',
    color_label: 'Основний колір',
    theme_label: 'Вигляд',
    skin_label: 'Стилі',
    semiDark_label: 'Напівтемне меню',
    layout_header: 'Розмітка',
    layout_label: 'Меню (Навігація)',
    layout_header_label: 'Типи заголовків',
    content_label: 'Ширина контенту',
    layout_navbar_label: 'Панель навігації',
    direction_label: 'Напрямок',
    style_switch_light: 'Світлий',
    style_switch_dark: 'Темний',
    layout_static: 'Статичний',
    layout_offcanvas: 'Випадаючий',
    layout_fixed: 'Фіксований',
    layout_fixed_offcanvas: 'Фіксований випадаючий',
    layout_dd_open_label: 'Відкрити при наведенні',
    layout_footer_label: 'Фікс. футер',
    misc_header: 'Інше',
    theme_light: 'Світлий',
    theme_dark: 'Темний',
    theme_system: 'Системна',
    skin_default: 'За замовчуванням',
    skin_bordered: 'З рамкою',
    layout_expanded: 'Розгорнутий',
    layout_collapsed: 'Згорнутий',
    content_compact: 'Компактний',
    content_wide: 'Широкий',
    direction_ltr: 'Зліва направо',
    direction_rtl: 'Справа наліво',
    navbar_sticky: 'Фіксований',
    navbar_static: 'Статичний',
    navbar_hidden: 'Прихований'
  };

  TemplateCustomizer.LANGUAGES.ka = {
    panel_header: 'შაბლონის მორგება',
    panel_sub_header: 'მორგება და გადახედვა რეალურ დროში',
    theming_header: 'თემები',
    color_label: 'ძირითადი ფერი',
    theme_label: 'გარეგნობა',
    skin_label: 'სტილები',
    semiDark_label: 'ნახევრად მუქი მენიუ',
    layout_header: 'განლაგება',
    layout_label: 'მენიუ (ნავიგაცია)',
    layout_header_label: 'სათაურის ტიპები',
    content_label: 'შიგთავსის სიგანე',
    layout_navbar_label: 'ნავიგაციის ზოლი',
    direction_label: 'მიმართულება',
    style_switch_light: 'ნათელი',
    style_switch_dark: 'მუქი',
    layout_static: 'სტატიკური',
    layout_offcanvas: 'გამოსაშლელი',
    layout_fixed: 'ფიქსირებული',
    layout_fixed_offcanvas: 'ფიქსირებული გამოსაშლელი',
    layout_dd_open_label: 'გახსნა მიახლოებისას',
    layout_footer_label: 'ფიქსირებული ფუტერი',
    misc_header: 'სხვადასხვა',
    theme_light: 'ნათელი',
    theme_dark: 'მუქი',
    theme_system: 'სისტემური',
    skin_default: 'ნაგულისხმევი',
    skin_bordered: 'ჩარჩოთი',
    layout_expanded: 'გაშლილი',
    layout_collapsed: 'შეკუმშული',
    content_compact: 'კომპაქტური',
    content_wide: 'ფართო',
    direction_ltr: 'მარცხნიდან მარჯვნივ',
    direction_rtl: 'მარჯვნიდან მარცხნივ',
    navbar_sticky: 'ფიქსირებული',
    navbar_static: 'სტატიკური',
    navbar_hidden: 'დამალული'
  };

  TemplateCustomizer.LANGUAGES.kk = {
    panel_header: 'Үлгіні теңшеу',
    panel_sub_header: 'Нақты уақытта теңшеу және алдын ала қарау',
    theming_header: 'Тақырыптар',
    color_label: 'Негізгі түс',
    theme_label: 'Көрініс',
    skin_label: 'Стильдер',
    semiDark_label: 'Жартылай күңгірт мәзір',
    layout_header: 'Орналасу',
    layout_label: 'Мәзір (Навигация)',
    layout_header_label: 'Тақырып түрлері',
    content_label: 'Контент ені',
    layout_navbar_label: 'Навигация тақтасы',
    direction_label: 'Бағыт',
    style_switch_light: 'Ашық',
    style_switch_dark: 'Күңгірт',
    layout_static: 'Статикалық',
    layout_offcanvas: 'Шығатын',
    layout_fixed: 'Бекітілген',
    layout_fixed_offcanvas: 'Бекітілген шығатын',
    layout_dd_open_label: 'Меңзерді апарғанда ашу',
    layout_footer_label: 'Бекітілген футер',
    misc_header: 'Басқа',
    theme_light: 'Ашық',
    theme_dark: 'Күңгірт',
    theme_system: 'Жүйелік',
    skin_default: 'Әдепкі',
    skin_bordered: 'Жиекті',
    layout_expanded: 'Жайылған',
    layout_collapsed: 'Жинақталған',
    content_compact: 'Ықшам',
    content_wide: 'Кең',
    direction_ltr: 'Солдан оңға',
    direction_rtl: 'Оңнан солға',
    navbar_sticky: 'Бекітілген',
    navbar_static: 'Статикалық',
    navbar_hidden: 'Жасырын'
  };

  TemplateCustomizer.LANGUAGES.uz = {
    panel_header: 'Shablonni sozlash',
    panel_sub_header: 'Haqiqiy vaqtda sozlash va ko\'rish',
    theming_header: 'Mavzular',
    color_label: 'Asosiy rang',
    theme_label: 'Ko\'rinish',
    skin_label: 'Stillar',
    semiDark_label: 'Yarim qorong\'i menyu',
    layout_header: 'Joylashuv',
    layout_label: 'Menyu (Navigatsiya)',
    layout_header_label: 'Sarlavha turlari',
    content_label: 'Kontent kengligi',
    layout_navbar_label: 'Navigatsiya paneli',
    direction_label: 'Yo\'nalish',
    style_switch_light: 'Yorug\'',
    style_switch_dark: 'Qorong\'i',
    layout_static: 'Statik',
    layout_offcanvas: 'Chiquvchi',
    layout_fixed: 'Ruxsat etilgan',
    layout_fixed_offcanvas: 'Ruxsat etilgan chiquvchi',
    layout_dd_open_label: 'Sichqoncha bilan ochish',
    layout_footer_label: 'Ruxsat etilgan futer',
    misc_header: 'Boshqa',
    theme_light: 'Yorug\'',
    theme_dark: 'Qorong\'i',
    theme_system: 'Tizim',
    skin_default: 'Standart',
    skin_bordered: 'Ramkali',
    layout_expanded: 'Kengaytirilgan',
    layout_collapsed: 'Yig\'ilgan',
    content_compact: 'Ixcham',
    content_wide: 'Keng',
    direction_ltr: 'Chapdan o\'ngga',
    direction_rtl: 'O\'ngdan chapga',
    navbar_sticky: 'Yopishqoq',
    navbar_static: 'Statik',
    navbar_hidden: 'Yashirin'
  };

  const currentLang = window.CurrentLanguage || document.cookie.match(/\.AspNetCore\.Culture=c=([a-z]{2})/i)?.[1] || 'tr';

  // Apply localized titles to radio option buttons (Thematic labels)
  const t = TemplateCustomizer.LANGUAGES[currentLang];
  if (t) {
    const applyToArr = (arr, map) => {
      arr.forEach(opt => { if (map[opt.name]) opt.title = map[opt.name]; });
    };
    applyToArr(TemplateCustomizer.THEMES, { light: t.theme_light, dark: t.theme_dark, system: t.theme_system });
    applyToArr(TemplateCustomizer.SKINS, { default: t.skin_default, bordered: t.skin_bordered });
    applyToArr(TemplateCustomizer.LAYOUTS, { expanded: t.layout_expanded, collapsed: t.layout_collapsed });
    applyToArr(TemplateCustomizer.CONTENT, { compact: t.content_compact, wide: t.content_wide });
    applyToArr(TemplateCustomizer.DIRECTIONS, { ltr: t.direction_ltr, rtl: t.direction_rtl });
    applyToArr(TemplateCustomizer.NAVBAR_OPTIONS, { sticky: t.navbar_sticky, static: t.navbar_static, hidden: t.navbar_hidden });
  }

  window.templateCustomizer = new TemplateCustomizer({
    displayCustomizer: true,
    lang: currentLang,
    controls: [
      'color',
      'theme',
      'skins',
      'semiDark',
      'layoutCollapsed',
      'layoutNavbarOptions',
      'headerType',
      'contentLayout',
      'rtl'
    ]
  });

  // Ensure UI updates with the correct translations immediately
  if (window.templateCustomizer && typeof window.templateCustomizer.setLang === 'function') {
    window.templateCustomizer.setLang(currentLang);
  }
}


