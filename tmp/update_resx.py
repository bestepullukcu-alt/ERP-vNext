import sys

def append_to_resx(filepath, xml_string):
    with open(filepath, "r") as f:
        content = f.read()
    content = content.replace("</root>", xml_string)
    with open(filepath, "w") as f:
        f.write(content)

en_xml = """  <data name="Sector" xml:space="preserve">
    <value>Sector</value>
  </data>
  <data name="ContactPerson" xml:space="preserve">
    <value>Contact Person</value>
  </data>
  <data name="OrganizationRole" xml:space="preserve">
    <value>Organization Role</value>
  </data>
  <data name="ParentLegalEntityId" xml:space="preserve">
    <value>Parent Legal Entity</value>
  </data>
  <data name="LogoUrl" xml:space="preserve">
    <value>Logo URL</value>
  </data>
  <data name="Website" xml:space="preserve">
    <value>Website</value>
  </data>
  <data name="Address" xml:space="preserve">
    <value>Address</value>
  </data>
  <data name="DefaultCommunicationLanguage" xml:space="preserve">
    <value>Default Language</value>
  </data>
  <data name="DefaultTimeZone" xml:space="preserve">
    <value>Default Time Zone</value>
  </data>
  <data name="TaxJurisdiction" xml:space="preserve">
    <value>Tax Jurisdiction</value>
  </data>
  <data name="PrimaryCurrency" xml:space="preserve">
    <value>Primary Currency</value>
  </data>
  <data name="FiscalYearStart" xml:space="preserve">
    <value>Fiscal Year Start</value>
  </data>
  <data name="RegistrationDate" xml:space="preserve">
    <value>Registration Date</value>
  </data>
  <data name="EffectiveFromDate" xml:space="preserve">
    <value>Effective From Date</value>
  </data>
</root>"""

tr_xml = """  <data name="Sector" xml:space="preserve">
    <value>Sektör</value>
  </data>
  <data name="ContactPerson" xml:space="preserve">
    <value>İletişim Kişisi</value>
  </data>
  <data name="OrganizationRole" xml:space="preserve">
    <value>Organizasyon Rolü</value>
  </data>
  <data name="ParentLegalEntityId" xml:space="preserve">
    <value>Bağlı Olduğu Şirket (Üst)</value>
  </data>
  <data name="LogoUrl" xml:space="preserve">
    <value>Logo URL (Bağlantı)</value>
  </data>
  <data name="Website" xml:space="preserve">
    <value>Web Sitesi</value>
  </data>
  <data name="Address" xml:space="preserve">
    <value>Adres</value>
  </data>
  <data name="DefaultCommunicationLanguage" xml:space="preserve">
    <value>İletişim Dili</value>
  </data>
  <data name="DefaultTimeZone" xml:space="preserve">
    <value>Saat Dilimi</value>
  </data>
  <data name="TaxJurisdiction" xml:space="preserve">
    <value>Vergi / Yargı Çevresi</value>
  </data>
  <data name="PrimaryCurrency" xml:space="preserve">
    <value>Ana Para Birimi</value>
  </data>
  <data name="FiscalYearStart" xml:space="preserve">
    <value>Mali Yıl Başlangıcı</value>
  </data>
  <data name="RegistrationDate" xml:space="preserve">
    <value>Kayıt Tarihi</value>
  </data>
  <data name="EffectiveFromDate" xml:space="preserve">
    <value>Geçerlilik Tarihi</value>
  </data>
</root>"""

append_to_resx("frontend/Diten.Web/Resources/Views/MDM/LegalEntities.en.resx", en_xml)
append_to_resx("frontend/Diten.Web/Resources/Views/MDM/LegalEntities.tr.resx", tr_xml)
