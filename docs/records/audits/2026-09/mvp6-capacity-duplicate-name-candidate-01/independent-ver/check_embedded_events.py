import sys,json,pathlib
sys.path.insert(0,"/private/tmp/mvp6-capacity-duplicate-name-candidate-01/tooldeps")
import yaml,jsonschema
p=pathlib.Path("/Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-09/mvp6-capacity-duplicate-name-candidate-01/sandop-capacity.openapi.candidate.yaml")
d=yaml.safe_load(p.read_text());results=[]
for name,s in d["components"]["schemas"].items():
 for ex in s.get("examples",[])+([s["example"]] if "example" in s else []):
  jsonschema.Draft202012Validator(s,resolver=jsonschema.RefResolver.from_schema(d),format_checker=jsonschema.FormatChecker()).validate(ex)
  results.append({"schema":name,"verdict":"PASS"})
assert len(results)==6
print(json.dumps(results,indent=2))
