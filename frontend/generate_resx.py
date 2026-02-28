import os

def create_resx(lang, translations):
    base_dir = "/Users/alitufanoglu/Desktop/ERP-vNext/frontend/Diten.Web/Resources/Views/MDM"
    os.makedirs(base_dir, exist_ok=True)
    file_path = f"{base_dir}/LegalEntities.{lang}.resx"

    content = """<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
"""
    for key, value in translations.items():
        content += f'  <data name="{key}" xml:space="preserve">\n    <value>{value}</value>\n  </data>\n'
    
    content += "</root>"
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(content)

data = {
    "en": {
        "LegalEntitiesTitle": "Legal Entities",
        "AddNewCompany": "Add New Company",
        "Title": "Title",
        "TaxNumber": "Tax Number",
        "TaxOffice": "Tax Office",
        "Email": "Email",
        "Phone": "Phone",
        "CompanyType": "Company Type",
        "Actions": "Actions",
        "ViewBtn": "View",
        "EditBtn": "Edit",
        "NoOrganizationsFound": "No organizations found or could not reach Gateway.",
        "CreateLegalEntityTitle": "Create Legal Entity",
        "AddNewLegalEntity": "Add New Legal Entity (Company)",
        "BackToList": "Back to List",
        "BasicInformation": "Basic Information",
        "ContactDetails": "Contact Details",
        "AdditionalSpecifications": "Additional Specifications",
        "CancelBtn": "Cancel",
        "SaveLegalEntity": "Save Legal Entity",
        "CompanyNamePlaceholder": "Company Name",
        "TaxOfficePlaceholder": "Tax Office",
        "TaxNumberPlaceholder": "Tax Number",
        "CompanyTypePlaceholder": "e.g. LLC, Corp, Inc",
        "SectorPlaceholder": "Industry Sector",
        "EmailPlaceholder": "info@company.com",
        "PhonePlaceholder": "+1 555 123 4567",
        "WebsitePlaceholder": "www.company.com",
        "AddressPlaceholder": "Full business address",
        "PrimaryCurrencyPlaceholder": "USD, EUR, TRY",
        "DefaultTimeZonePlaceholder": "UTC+3",
        "ContactPersonPlaceholder": "Main Contact Name",
        "DefaultLangPlaceholder": "en-US, tr-TR",
        "OrganizationRolePlaceholder": "Vendor, Customer...",
        "LogoUrlPlaceholder": "https://...",
        "FiscalYearStartPlaceholder": "01-01",
        "TaxJurisdictionPlaceholder": "Jurisdiction Code"
    },
    "tr": {
        "LegalEntitiesTitle": "Tüzel Kişiler", "AddNewCompany": "Yeni Şirket Ekle", "Title": "Başlık", "TaxNumber": "Vergi Numarası", "TaxOffice": "Vergi Dairesi", "Email": "E-posta", "Phone": "Telefon", "CompanyType": "Şirket Türü", "Actions": "İşlemler", "ViewBtn": "Görüntüle", "EditBtn": "Düzenle", "NoOrganizationsFound": "Kuruluş bulunamadı veya Gateway'e ulaşılamadı.", "CreateLegalEntityTitle": "Tüzel Kişi Oluştur", "AddNewLegalEntity": "Yeni Tüzel Kişi (Şirket) Ekle", "BackToList": "Listeye Dön", "BasicInformation": "Temel Bilgiler", "ContactDetails": "İletişim Detayları", "AdditionalSpecifications": "Ek Özellikler", "CancelBtn": "İptal", "SaveLegalEntity": "Tüzel Kişiyi Kaydet", "CompanyNamePlaceholder": "Şirket Adı", "TaxOfficePlaceholder": "Vergi Dairesi", "TaxNumberPlaceholder": "Vergi Numarası", "CompanyTypePlaceholder": "Örn. A.Ş., Ltd.", "SectorPlaceholder": "Endüstri Sektörü", "EmailPlaceholder": "info@sirket.com", "PhonePlaceholder": "+90 555 123 4567", "WebsitePlaceholder": "www.sirket.com", "AddressPlaceholder": "Tam iş adresi", "PrimaryCurrencyPlaceholder": "USD, EUR, TRY", "DefaultTimeZonePlaceholder": "UTC+3", "ContactPersonPlaceholder": "Ana İletişim Kişisi", "DefaultLangPlaceholder": "en-US, tr-TR", "OrganizationRolePlaceholder": "Tedarikçi, Müşteri...", "LogoUrlPlaceholder": "https://...", "FiscalYearStartPlaceholder": "01-01", "TaxJurisdictionPlaceholder": "Yargı Kodu"
    },
    "es": {
        "LegalEntitiesTitle": "Entidades Legales", "AddNewCompany": "Agregar Nueva Empresa", "Title": "Título", "TaxNumber": "Número de Impuesto", "TaxOffice": "Oficina de Impuestos", "Email": "Correo Electrónico", "Phone": "Teléfono", "CompanyType": "Tipo de Empresa", "Actions": "Acciones", "ViewBtn": "Ver", "EditBtn": "Editar", "NoOrganizationsFound": "No se encontraron organizaciones o no se pudo contactar con Gateway.", "CreateLegalEntityTitle": "Crear Entidad Legal", "AddNewLegalEntity": "Agregar Nueva Entidad Legal (Empresa)", "BackToList": "Volver a la Lista", "BasicInformation": "Información Básica", "ContactDetails": "Detalles de Contacto", "AdditionalSpecifications": "Especificaciones Adicionales", "CancelBtn": "Cancelar", "SaveLegalEntity": "Guardar Entidad Legal", "CompanyNamePlaceholder": "Nombre de la Empresa", "TaxOfficePlaceholder": "Oficina de Impuestos", "TaxNumberPlaceholder": "Número de Impuesto", "CompanyTypePlaceholder": "Ej. LLC, S.A., S.L.", "SectorPlaceholder": "Sector Industrial", "EmailPlaceholder": "info@empresa.com", "PhonePlaceholder": "+34 555 123 456", "WebsitePlaceholder": "www.empresa.com", "AddressPlaceholder": "Dirección comercial completa", "PrimaryCurrencyPlaceholder": "USD, EUR, TRY", "DefaultTimeZonePlaceholder": "UTC+3", "ContactPersonPlaceholder": "Nombre del Contacto Principal", "DefaultLangPlaceholder": "es-ES, en-US", "OrganizationRolePlaceholder": "Proveedor, Cliente...", "LogoUrlPlaceholder": "https://...", "FiscalYearStartPlaceholder": "01-01", "TaxJurisdictionPlaceholder": "Código de Jurisdicción"
    },
    "ru": {
        "LegalEntitiesTitle": "Юридические лица", "AddNewCompany": "Добавить новую компанию", "Title": "Название", "TaxNumber": "ИНН", "TaxOffice": "Налоговая инспекция", "Email": "Электронная почта", "Phone": "Телефон", "CompanyType": "Тип компании", "Actions": "Действия", "ViewBtn": "Просмотр", "EditBtn": "Редактировать", "NoOrganizationsFound": "Организации не найдены или шлюз недоступен.", "CreateLegalEntityTitle": "Создать юридическое лицо", "AddNewLegalEntity": "Добавить новое юридическое лицо (компанию)", "BackToList": "Вернуться к списку", "BasicInformation": "Основная информация", "ContactDetails": "Контактные данные", "AdditionalSpecifications": "Дополнительные характеристики", "CancelBtn": "Отмена", "SaveLegalEntity": "Сохранить юридическое лицо", "CompanyNamePlaceholder": "Название компании", "TaxOfficePlaceholder": "Налоговая инспекция", "TaxNumberPlaceholder": "ИНН", "CompanyTypePlaceholder": "ООО, ПАО, ЗАО", "SectorPlaceholder": "Отрасль экономики", "EmailPlaceholder": "info@company.ru", "PhonePlaceholder": "+7 999 123 4567", "WebsitePlaceholder": "www.company.ru", "AddressPlaceholder": "Полный рабочий адрес", "PrimaryCurrencyPlaceholder": "RUB, USD, EUR", "DefaultTimeZonePlaceholder": "UTC+3", "ContactPersonPlaceholder": "Контактное лицо", "DefaultLangPlaceholder": "ru-RU, en-US", "OrganizationRolePlaceholder": "Поставщик, Клиент...", "LogoUrlPlaceholder": "https://...", "FiscalYearStartPlaceholder": "01-01", "TaxJurisdictionPlaceholder": "Код юрисдикции"
    },
    "uz": {
        "LegalEntitiesTitle": "Yuridik shaxslar", "AddNewCompany": "Yangi kompaniya qo'shish", "Title": "Sarlavha", "TaxNumber": "Soliq raqami", "TaxOffice": "Soliq idorasi", "Email": "Elektron pochta", "Phone": "Telefon", "CompanyType": "Kompaniya turi", "Actions": "Harakatlar", "ViewBtn": "Ko'rish", "EditBtn": "Tahrirlash", "NoOrganizationsFound": "Tashkilotlar topilmadi.", "CreateLegalEntityTitle": "Yuridik shaxs yaratish", "AddNewLegalEntity": "Yangi yuridik shaxs qo'shish", "BackToList": "Ro'yxatga qaytish", "BasicInformation": "Asosiy ma'lumotlar", "ContactDetails": "Aloqa ma'lumotlari", "AdditionalSpecifications": "Qo'shimcha xususiyatlar", "CancelBtn": "Bekor qilish", "SaveLegalEntity": "Saqlash", "CompanyNamePlaceholder": "Kompaniya nomi", "TaxOfficePlaceholder": "Soliq idorasi", "TaxNumberPlaceholder": "Soliq raqami", "CompanyTypePlaceholder": "MChJ, AJ", "SectorPlaceholder": "Sanoat sektori", "EmailPlaceholder": "info@kompaniya.uz", "PhonePlaceholder": "+998 90 123 45 67", "WebsitePlaceholder": "www.kompaniya.uz", "AddressPlaceholder": "To'liq manzili", "PrimaryCurrencyPlaceholder": "UZS, USD, EUR", "DefaultTimeZonePlaceholder": "UTC+5", "ContactPersonPlaceholder": "Aloqa shaxsi", "DefaultLangPlaceholder": "uz-UZ, en-US", "OrganizationRolePlaceholder": "Yetkazib beruvchi, Mijoz...", "LogoUrlPlaceholder": "https://...", "FiscalYearStartPlaceholder": "01-01", "TaxJurisdictionPlaceholder": "Yurisdiksiya kodi"
    },
    "uk": {
        "LegalEntitiesTitle": "Юридичні особи", "AddNewCompany": "Додати нову компанію", "Title": "Назва", "TaxNumber": "ІПН", "TaxOffice": "Податкова інспекція", "Email": "Електронна пошта", "Phone": "Телефон", "CompanyType": "Тип компанії", "Actions": "Дії", "ViewBtn": "Перегляд", "EditBtn": "Редагувати", "NoOrganizationsFound": "Організації не знайдені.", "CreateLegalEntityTitle": "Створити юридичну особу", "AddNewLegalEntity": "Додати нову юридичну особу", "BackToList": "Повернутися до списку", "BasicInformation": "Основна інформація", "ContactDetails": "Контактні дані", "AdditionalSpecifications": "Додаткові специфікації", "CancelBtn": "Скасувати", "SaveLegalEntity": "Зберегти", "CompanyNamePlaceholder": "Назва компанії", "TaxOfficePlaceholder": "Податкова інспекція", "TaxNumberPlaceholder": "ІПН", "CompanyTypePlaceholder": "ТОВ, ПАТ", "SectorPlaceholder": "Галузь", "EmailPlaceholder": "info@company.ua", "PhonePlaceholder": "+380 50 123 4567", "WebsitePlaceholder": "www.company.ua", "AddressPlaceholder": "Повна адреса", "PrimaryCurrencyPlaceholder": "UAH, USD, EUR", "DefaultTimeZonePlaceholder": "UTC+2", "ContactPersonPlaceholder": "Контактна особа", "DefaultLangPlaceholder": "uk-UA, en-US", "OrganizationRolePlaceholder": "Постачальник, Клієнт...", "LogoUrlPlaceholder": "https://...", "FiscalYearStartPlaceholder": "01-01", "TaxJurisdictionPlaceholder": "Код юрисдикції"
    },
    "ka": {
        "LegalEntitiesTitle": "იურიდიული პირები", "AddNewCompany": "კომპანიის დამატება", "Title": "სათაური", "TaxNumber": "საგადასახადო ნომერი", "TaxOffice": "საგადასახადო ოფისი", "Email": "ელფოსტა", "Phone": "ტელეფონი", "CompanyType": "კომპანიის ტიპი", "Actions": "მოქმედებები", "ViewBtn": "ნახვა", "EditBtn": "რედაქტირება", "NoOrganizationsFound": "ვერ მოიძებნა.", "CreateLegalEntityTitle": "იურიდიული პირის შექმნა", "AddNewLegalEntity": "იურიდიული პირის დამატება", "BackToList": "სიაში დაბრუნება", "BasicInformation": "ძირითადი ინფორმაცია", "ContactDetails": "საკონტაქტო მონაცემები", "AdditionalSpecifications": "დამატებითი", "CancelBtn": "გაუქმება", "SaveLegalEntity": "შენახვა", "CompanyNamePlaceholder": "კომპანიის სახელი", "TaxOfficePlaceholder": "საგადასახადო", "TaxNumberPlaceholder": "ნომერი", "CompanyTypePlaceholder": "შპს, სს", "SectorPlaceholder": "სექტორი", "EmailPlaceholder": "info@company.ge", "PhonePlaceholder": "+995 555 123 456", "WebsitePlaceholder": "www.company.ge", "AddressPlaceholder": "მისამართი", "PrimaryCurrencyPlaceholder": "GEL, USD, EUR", "DefaultTimeZonePlaceholder": "UTC+4", "ContactPersonPlaceholder": "საკონტაქტო პირი", "DefaultLangPlaceholder": "ka-GE, en-US", "OrganizationRolePlaceholder": "მომწოდებელი...", "LogoUrlPlaceholder": "https://...", "FiscalYearStartPlaceholder": "01-01", "TaxJurisdictionPlaceholder": "კოდი"
    },
    "kk": {
        "LegalEntitiesTitle": "Заңды тұлғалар", "AddNewCompany": "Компанияны қосу", "Title": "Атау", "TaxNumber": "СТН", "TaxOffice": "Салық басқармасы", "Email": "Электрондық пошта", "Phone": "Телефон", "CompanyType": "Компанияның түрі", "Actions": "Әрекеттер", "ViewBtn": "Көру", "EditBtn": "Өңдеу", "NoOrganizationsFound": "Ұйымдар табылмады.", "CreateLegalEntityTitle": "Заңды тұлғаны құру", "AddNewLegalEntity": "Заңды тұлға қосу", "BackToList": "Тізімге оралу", "BasicInformation": "Негізгі ақпарат", "ContactDetails": "Байланыс деректері", "AdditionalSpecifications": "Қосымша", "CancelBtn": "Болдырмау", "SaveLegalEntity": "Сақтау", "CompanyNamePlaceholder": "Компанияның атауы", "TaxOfficePlaceholder": "Салық басқармасы", "TaxNumberPlaceholder": "СТН", "CompanyTypePlaceholder": "ЖШС, АҚ", "SectorPlaceholder": "Өнеркәсіп", "EmailPlaceholder": "info@company.kz", "PhonePlaceholder": "+7 701 123 4567", "WebsitePlaceholder": "www.company.kz", "AddressPlaceholder": "Толық мекенжайы", "PrimaryCurrencyPlaceholder": "KZT, USD, EUR", "DefaultTimeZonePlaceholder": "UTC+5", "ContactPersonPlaceholder": "Байланыс тұлғасы", "DefaultLangPlaceholder": "kk-KZ, en-US", "OrganizationRolePlaceholder": "Жеткізуші...", "LogoUrlPlaceholder": "https://...", "FiscalYearStartPlaceholder": "01-01", "TaxJurisdictionPlaceholder": "Юрисдикция коды"
    }
}

for lang, dic in data.items():
    create_resx(lang, dic)

print("Generated all .resx files.")
