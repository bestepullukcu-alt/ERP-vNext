#!/usr/bin/env python3
"""
WP-VP-4I (9) — Turkish (and the other tenant languages') labels for the CRM reference sets the Visit Planning screen
shows: institution type (account-type), medical specialty (medical-specialty) and province (city).

Why: in the Turkish UI the Targets tab read "Clinic", "Family Medicine", "Pediatrics". Root cause (2026-10-08): the
Web reads the right language — the DATA has none. A MOD-0048 value carries ONE label (BusinessReferenceDataValue
.DisplayName, no per-language field; the Platform takes no language on its read). These sets were authored in English.
So the per-language labels go into the value's Attributes as label_<lang> (label_tr, label_en, label_fr, label_es,
label_zh, label_ar, label_ru), and the Web's reference-labels endpoint picks label_<UI language>, else the label.
The existing label is never changed; an attribute already there is never overwritten (only MISSING ones are added).

Flow (the BRD maker-checker flow, through the Gateway — never Mongo):
  set (GET sets) → new draft version from the published one (POST sets/{id}/versions) → its values (GET) →
  values + the missing label_<lang> attributes (PUT versions/{id}/values) → validate → submit (maker) →
  approve (CHECKER: another user — sod_submitter_cannot_approve) → publish (Idempotency-Key).
A set that already has a draft is skipped (someone else's work). Without a checker token the run stops after submit and
says so; the checker approves + publishes in the Reference Data screen, or re-runs with BRD_CHECKER_TOKEN.

Province (city): the codes are the territory's area codes ("TR-34-ISTANBUL", read-only from territory_nodes when
the CRM Mongo is reachable, else TR-<plate>-<ASCII NAME>); the label is the Turkish name ("İstanbul", "Şanlıurfa"), the
same proper name in every language. ⚠ The `city` set is not authored yet (MOD-0149 deferred_sets). Publishing it turns
on Contact CityRef validation against it (ContactReferenceValidation: CitySet = "city"), so it is written only with
--include-city, and only when the set already exists (this script never creates a set).

Usage (DRY RUN is the default — nothing is written without --apply):
  py scripts/data-load/add_tr_reference_labels.py                    # dry run, offline: what would be added
  py scripts/data-load/add_tr_reference_labels.py --live             # dry run against the published values (needs BRD_MAKER_TOKEN)
  py scripts/data-load/add_tr_reference_labels.py --apply            # account-type + medical-specialty
  py scripts/data-load/add_tr_reference_labels.py --apply --include-city
Env: GATEWAY_URL=http://localhost:5000  TENANT_ID=97c59330-dbc4-4665-b29c-0c26dbb5cc93
     BRD_MAKER_TOKEN=<access token of the steward who drafts + submits>   (never a password)
     BRD_CHECKER_TOKEN=<access token of ANOTHER steward: approve + publish> (optional)
     MONGO_URI=mongodb://localhost:27017  CRM_DB=DitenERP_Dev              (read-only, province codes only)
"""
import argparse, json, os, sys, unicodedata, urllib.error, urllib.request, uuid

GATEWAY_URL = os.environ.get("GATEWAY_URL", "http://localhost:5000").rstrip("/")
TENANT_ID = os.environ.get("TENANT_ID", "97c59330-dbc4-4665-b29c-0c26dbb5cc93")
MONGO_URI = os.environ.get("MONGO_URI", "mongodb://localhost:27017")
CRM_DB = os.environ.get("CRM_DB", "DitenERP_Dev")
BRD = GATEWAY_URL + "/api/v1/reference-data"

LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"]  # tenant modules: 7 languages

# code → (en, tr, fr, es, zh, ar, ru)
ACCOUNT_TYPES = {
    "organization":    ("Organization", "Kuruluş", "Organisation", "Organización", "组织", "منظمة", "Организация"),
    "hospital":        ("Hospital", "Hastane", "Hôpital", "Hospital", "医院", "مستشفى", "Больница"),
    "pharmacy":        ("Pharmacy", "Eczane", "Pharmacie", "Farmacia", "药房", "صيدلية", "Аптека"),
    "clinic":          ("Clinic", "Klinik", "Clinique", "Clínica", "诊所", "عيادة", "Клиника"),
    "distributor":     ("Distributor", "Distribütör", "Distributeur", "Distribuidor", "分销商", "موزّع", "Дистрибьютор"),
    "wholesaler":      ("Wholesaler", "Toptancı", "Grossiste", "Mayorista", "批发商", "تاجر جملة", "Оптовик"),
    "corporate-group": ("Corporate Group", "Şirketler Grubu", "Groupe d'entreprises", "Grupo empresarial", "企业集团", "مجموعة شركات", "Корпоративная группа"),
    "branch":          ("Branch", "Şube", "Succursale", "Sucursal", "分支机构", "فرع", "Филиал"),
    "other":           ("Other", "Diğer", "Autre", "Otro", "其他", "أخرى", "Другое"),
}

MEDICAL_SPECIALTIES = {
    "cardiology":            ("Cardiology", "Kardiyoloji", "Cardiologie", "Cardiología", "心脏病学", "أمراض القلب", "Кардиология"),
    "oncology":              ("Oncology", "Onkoloji", "Oncologie", "Oncología", "肿瘤学", "علم الأورام", "Онкология"),
    "pediatrics":            ("Pediatrics", "Çocuk Sağlığı ve Hastalıkları", "Pédiatrie", "Pediatría", "儿科", "طب الأطفال", "Педиатрия"),
    "internal-medicine":     ("Internal Medicine", "İç Hastalıkları", "Médecine interne", "Medicina interna", "内科", "الطب الباطني", "Внутренние болезни"),
    "general-surgery":       ("General Surgery", "Genel Cerrahi", "Chirurgie générale", "Cirugía general", "普通外科", "الجراحة العامة", "Общая хирургия"),
    "neurology":             ("Neurology", "Nöroloji", "Neurologie", "Neurología", "神经内科", "طب الأعصاب", "Неврология"),
    "dermatology":           ("Dermatology", "Dermatoloji", "Dermatologie", "Dermatología", "皮肤科", "الأمراض الجلدية", "Дерматология"),
    "gynecology-obstetrics": ("Gynecology & Obstetrics", "Kadın Hastalıkları ve Doğum", "Gynécologie-obstétrique", "Ginecología y obstetricia", "妇产科", "أمراض النساء والتوليد", "Акушерство и гинекология"),
    "ophthalmology":         ("Ophthalmology", "Göz Hastalıkları", "Ophtalmologie", "Oftalmología", "眼科", "طب العيون", "Офтальмология"),
    "psychiatry":            ("Psychiatry", "Psikiyatri", "Psychiatrie", "Psiquiatría", "精神科", "الطب النفسي", "Психиатрия"),
    "orthopedics":           ("Orthopedics", "Ortopedi ve Travmatoloji", "Orthopédie", "Ortopedia", "骨科", "جراحة العظام", "Ортопедия"),
    "otolaryngology":        ("Otolaryngology", "Kulak Burun Boğaz", "Oto-rhino-laryngologie", "Otorrinolaringología", "耳鼻喉科", "طب الأنف والأذن والحنجرة", "Оториноларингология"),
    "urology":               ("Urology", "Üroloji", "Urologie", "Urología", "泌尿外科", "جراحة المسالك البولية", "Урология"),
    "gastroenterology":      ("Gastroenterology", "Gastroenteroloji", "Gastro-entérologie", "Gastroenterología", "消化内科", "أمراض الجهاز الهضمي", "Гастроэнтерология"),
    "endocrinology":         ("Endocrinology", "Endokrinoloji", "Endocrinologie", "Endocrinología", "内分泌科", "الغدد الصماء", "Эндокринология"),
    "pulmonology":           ("Pulmonology", "Göğüs Hastalıkları", "Pneumologie", "Neumología", "呼吸内科", "أمراض الرئة", "Пульмонология"),
    "nephrology":            ("Nephrology", "Nefroloji", "Néphrologie", "Nefrología", "肾内科", "أمراض الكلى", "Нефрология"),
    "rheumatology":          ("Rheumatology", "Romatoloji", "Rhumatologie", "Reumatología", "风湿科", "أمراض الروماتيزم", "Ревматология"),
    "anesthesiology":        ("Anesthesiology", "Anesteziyoloji ve Reanimasyon", "Anesthésiologie", "Anestesiología", "麻醉科", "التخدير", "Анестезиология"),
    "radiology":             ("Radiology", "Radyoloji", "Radiologie", "Radiología", "放射科", "الأشعة", "Радиология"),
    "family-medicine":       ("Family Medicine", "Aile Hekimliği", "Médecine familiale", "Medicina familiar", "全科医学", "طب الأسرة", "Семейная медицина"),
    "other":                 ("Other", "Diğer", "Autre", "Otra", "其他", "أخرى", "Другая"),
}

# plate → the province's Turkish name (a proper name: the same in every language)
PROVINCES = [
    "Adana", "Adıyaman", "Afyonkarahisar", "Ağrı", "Amasya", "Ankara", "Antalya", "Artvin", "Aydın", "Balıkesir",
    "Bilecik", "Bingöl", "Bitlis", "Bolu", "Burdur", "Bursa", "Çanakkale", "Çankırı", "Çorum", "Denizli",
    "Diyarbakır", "Edirne", "Elazığ", "Erzincan", "Erzurum", "Eskişehir", "Gaziantep", "Giresun", "Gümüşhane", "Hakkari",
    "Hatay", "Isparta", "Mersin", "İstanbul", "İzmir", "Kars", "Kastamonu", "Kayseri", "Kırklareli", "Kırşehir",
    "Kocaeli", "Konya", "Kütahya", "Malatya", "Manisa", "Kahramanmaraş", "Mardin", "Muğla", "Muş", "Nevşehir",
    "Niğde", "Ordu", "Rize", "Sakarya", "Samsun", "Siirt", "Sinop", "Sivas", "Tekirdağ", "Tokat",
    "Trabzon", "Tunceli", "Şanlıurfa", "Uşak", "Van", "Yozgat", "Zonguldak", "Aksaray", "Bayburt", "Karaman",
    "Kırıkkale", "Batman", "Şırnak", "Bartın", "Ardahan", "Iğdır", "Yalova", "Karabük", "Kilis", "Osmaniye", "Düzce",
]

FOLD = str.maketrans("çğıöşüÇĞİÖŞÜ", "cgiosuCGIOSU")


def fold(name):
    """'Şanlıurfa' → 'SANLIURFA' (the territory area-code spelling)."""
    s = unicodedata.normalize("NFC", name).translate(FOLD).upper()
    return "".join(ch for ch in s if ch.isalnum())


def province_codes():
    """{folded name: area code} from territory_nodes (read-only), or {} when the CRM Mongo is not reachable."""
    try:
        import pymongo
        db = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=2000)[CRM_DB]
        out = {}
        for n in db.territory_nodes.find({"TenantId": TENANT_ID, "TerritoryLevel": "area"}, {"Name": 1, "AreaCode": 1, "TerritoryCode": 1}):
            code = n.get("AreaCode") or n.get("TerritoryCode")
            if code and n.get("Name"):
                out[fold(n["Name"])] = code
        return out
    except Exception as ex:  # no pymongo / no Mongo: the generated codes are used
        print(f"  (territory codes not read: {type(ex).__name__}; generated TR-<plate>-<NAME> codes are used)")
        return {}


def city_catalog():
    known = province_codes()
    rows = {}
    for plate, name in enumerate(PROVINCES, start=1):
        code = known.get(fold(name)) or f"TR-{plate:02d}-{fold(name)}"
        rows[code] = tuple(name for _ in LANGS)
    return rows, len(known)


def labels_of(row):
    return {"label_" + lang: text for lang, text in zip(LANGS, row)}


# ── HTTP ──
def call(method, path, token, body=None, idempotency=False, query=""):
    req = urllib.request.Request(BRD + path + query, method=method,
                                 data=None if body is None else json.dumps(body).encode("utf-8"))
    req.add_header("Authorization", "Bearer " + token)
    req.add_header("X-Tenant-Id", TENANT_ID)
    req.add_header("Accept", "application/json")
    if body is not None:
        req.add_header("Content-Type", "application/json")
    if idempotency:
        req.add_header("Idempotency-Key", str(uuid.uuid4()))
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            text = r.read().decode("utf-8")
            return r.status, (json.loads(text) if text else None)
    except urllib.error.HTTPError as e:
        text = e.read().decode("utf-8", "replace")
        try:
            return e.code, json.loads(text)
        except ValueError:
            return e.code, {"raw": text}


def data(resp):
    status, body = resp
    if status >= 300 or not isinstance(body, dict):
        raise RuntimeError(f"HTTP {status}: {json.dumps(body, ensure_ascii=False)[:400]}")
    return body.get("data", body)


def find_set(code, token):
    page = data(call("GET", "/sets", token, query="?search=" + code + "&page_size=50"))
    items = page.get("items", []) if isinstance(page, dict) else []
    hit = [s for s in items if (s.get("setCode") or "").lower() == code.lower()]
    return hit[0] if hit else None


def published_values(code, token):
    query = "?scope_key=" + TENANT_ID
    status, body = call("GET", f"/sets/{code}/published-values", token, query=query)
    if status >= 300 and "scope_key_not_allowed_for_global" in json.dumps(body):
        status, body = call("GET", f"/sets/{code}/published-values", token)
    if status == 404:
        return None
    return data((status, body)).get("items", [])


def missing_attributes(items, catalog):
    """[(code, {label_xx: text})] — only the attributes a value does not have yet; codes the set lacks are reported."""
    plan, unknown = [], []
    by_code = {(i.get("code") or "").lower(): i for i in items}
    for code, row in catalog.items():
        item = by_code.get(code.lower())
        if item is None:
            unknown.append(code)
            continue
        attrs = item.get("attributes") or {}
        add = {k: v for k, v in labels_of(row).items() if not attrs.get(k)}
        if add:
            plan.append((item.get("code"), add))
    return plan, unknown


def apply_set(code, catalog, maker, checker):
    s = find_set(code, maker)
    if s is None:
        print(f"  {code}: set not found — skipped (this script never creates a set)")
        return
    if s.get("activeDraftVersionId"):
        print(f"  {code}: a draft already exists ({s['activeDraftVersionId']}) — skipped, finish or discard it first")
        return
    if not s.get("publishedVersionId"):
        print(f"  {code}: nothing published — skipped")
        return
    draft = data(call("POST", f"/sets/{s['setId']}/versions", maker, {"source_version_id": s["publishedVersionId"]}))
    vid = draft["versionId"]
    values = data(call("GET", f"/versions/{vid}/values", maker))
    catalog_lc = {k.lower(): v for k, v in catalog.items()}
    changed, out = 0, []
    for v in values.get("items", []):
        attrs = dict(v.get("attributes") or {})
        row = catalog_lc.get((v.get("code") or "").lower())
        if row:
            for k, text in labels_of(row).items():
                if not attrs.get(k):
                    attrs[k] = text
                    changed += 1
        out.append({"code": v["code"], "label": v["label"], "description": v.get("description"), "is_active": v.get("isActive", True),
                    "sort_order": v.get("sortOrder", 0), "parent_value_code": v.get("parentValueCode"), "attributes": attrs or None})
    data(call("PUT", f"/versions/{vid}/values", maker, {"expected_concurrency_token": values.get("concurrencyToken"), "values": out}))
    data(call("POST", f"/versions/{vid}/validate", maker, {}))
    token = data(call("GET", f"/versions/{vid}", maker))["concurrencyToken"]
    data(call("POST", f"/versions/{vid}/submit", maker, {"expected_concurrency_token": token}))
    print(f"  {code}: draft {vid} — {changed} label attribute(s) added, submitted")
    if not checker:
        print(f"  {code}: NOT published — a second steward approves + publishes it (Reference Data screen, or BRD_CHECKER_TOKEN)")
        return
    token = data(call("GET", f"/versions/{vid}", checker))["concurrencyToken"]
    data(call("POST", f"/versions/{vid}/approve", checker, {"decision": "approve", "expected_concurrency_token": token}, idempotency=True))
    token = data(call("GET", f"/versions/{vid}", checker))["concurrencyToken"]
    data(call("POST", f"/versions/{vid}/publish", checker, {"publish_mode": "Immediate", "expected_concurrency_token": token}, idempotency=True))
    print(f"  {code}: approved + published")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write (default: dry run)")
    ap.add_argument("--live", action="store_true", help="dry run against the published values (needs BRD_MAKER_TOKEN)")
    ap.add_argument("--include-city", action="store_true", help="also the province set 'city' (see the warning above)")
    args = ap.parse_args()

    cities, from_territory = city_catalog()
    sets = [("account-type", ACCOUNT_TYPES), ("medical-specialty", MEDICAL_SPECIALTIES), ("city", cities)]
    maker = os.environ.get("BRD_MAKER_TOKEN", "")
    checker = os.environ.get("BRD_CHECKER_TOKEN", "")

    if args.apply:
        if not maker:
            sys.exit("--apply needs BRD_MAKER_TOKEN (an access token; never a password)")
        for code, catalog in sets:
            if code == "city" and not args.include_city:
                print("  city: skipped (--include-city not given)")
                continue
            apply_set(code, catalog, maker, checker)
        return

    print("DRY RUN — nothing is written. Re-run with --apply to write.")
    print(f"Tenant {TENANT_ID} · languages {', '.join(LANGS)} · attributes label_<lang> (the label itself is not changed)")
    for code, catalog in sets:
        print(f"\n== {code} ({len(catalog)} values)" + (f" — codes from territory_nodes: {from_territory}" if code == "city" else ""))
        if code == "city":
            print("   ⚠ written only with --include-city and only if the set exists: publishing it turns on Contact CityRef validation")
        if args.live:
            if not maker:
                sys.exit("--live needs BRD_MAKER_TOKEN")
            items = published_values(code, maker)
            if items is None:
                print("   set not published for this tenant — nothing to add to")
                continue
            plan, unknown = missing_attributes(items, catalog)
            for c, add in plan:
                print(f"   + {c}: " + ", ".join(f"{k}={v}" for k, v in add.items()))
            if unknown:
                print(f"   (not in the set, ignored: {', '.join(unknown)})")
            if not plan:
                print("   nothing missing")
        else:
            for c, row in catalog.items():
                print(f"   + {c}: tr={row[1]} · en={row[0]} · fr={row[2]} · es={row[3]} · zh={row[4]} · ar={row[5]} · ru={row[6]}"
                      if code != "city" else f"   + {c}: {row[1]} (all 7 languages)")


if __name__ == "__main__":
    main()
