"""CT FE capture v1.0: frozen mechanical run evidence, not behavior E2.

Binds supplied fresh runs to the exact committed post-BE/L10N baseline
and six-path contract. Independent lossless/token/receipt/mutation/cleanup
verification and final CI/main-human gates remain separate.
"""
import argparse,collections,datetime,hashlib,json,os,re,subprocess,sys,xml.etree.ElementTree as ET
from pathlib import Path
if not __debug__:raise RuntimeError("Optimized Python disables evidence assertions; refused")
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--repo',required=True);p.add_argument('--base',required=True)
p.add_argument('--out',required=True)
p.add_argument('--web-trx',required=True);p.add_argument('--js-json',required=True)
p.add_argument('--web-assembly',required=True);p.add_argument('--api-trx',required=True)
p.add_argument('--api-assembly',required=True);p.add_argument('--regpv-trx',required=True)
p.add_argument('--regpv-assembly',required=True)
a=p.parse_args();repo=Path(a.repo).resolve();out=Path(a.out).resolve()
assert not out.is_relative_to(repo),'Capture output must stay outside source worktree'
sha=lambda b:hashlib.sha256(b).hexdigest();lf=lambda b:b.replace(b'\r\n',b'\n')
def git(*args):return subprocess.check_output(['git','-C',str(repo),*args],timeout=120)
_blob_cache={}
def cache_blobs(keys):
 keys=sorted(set(keys)-set(_blob_cache))
 if not keys:return
 for commit,path in keys:
  assert re.fullmatch('[0-9a-f]{40}',commit) and '\n' not in path and '\r' not in path
 request=''.join(commit+':'+path+'\n' for commit,path in keys).encode('utf8')
 result=subprocess.run(['git','-C',str(repo),'cat-file','--batch'],input=request,capture_output=True,timeout=180,check=True)
 data=result.stdout;offset=0
 for key in keys:
  end=data.index(b'\n',offset);header=data[offset:end].split()
  assert len(header)==3 and re.fullmatch(b'[0-9a-f]{40}',header[0]) and header[1]==b'blob'
  size=int(header[2]);assert size>=0;offset=end+1
  content=data[offset:offset+size];assert len(content)==size
  offset+=size;assert data[offset:offset+1]==b'\n';offset+=1
  _blob_cache[key]=content
 assert offset==len(data)
def blob(commit,path):
 key=(commit,path)
 if key not in _blob_cache:cache_blobs([key])
 return _blob_cache[key]

paths=['frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/form.js',
 'frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs',
 'frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs',
 'frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js',
 'frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_Form.cshtml',
 'frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_IndexL10n.cshtml']
web_names=['Mvc_update_preserves_untouched_stored_text_and_reference_sequence',
 'Edit_optional_source_reference_and_patient_code_match_service_validation',
 'Edit_round_trip_preserves_subsecond_received_and_awareness_instants',
 'Edit_round_trip_clears_only_the_optional_field_the_user_emptied',
 'Edit_round_trip_sends_the_users_nonempty_change',
 'Edit_requires_a_complete_well_shaped_detail_before_enabling_save',
 'Connected_edit_test_fails_when_browser_runtime_or_update_leg_is_unavailable',
 'Connected_edit_submission_preserves_browser_antiforgery_request_token']
js_names=['pvg edit form prefills every editable field from the stored draft',
 'pvg edit save without a change sends back every stored value',
 'pvg edit save sends an optional field the user emptied as empty',
 'pvg edit refusal after triage shows the not-editable sentence',
 'pvg resources carry the same key set in seven languages without english echo',
 'keeps browser localization references backed by PVG resources or shared localizer keys']
BASELINE_PATH='docs/records/audits/2026-11/pvg-dl12-fe-baseline-01/BASELINE.json'
BASELINE_LF_SHA256='53575b230ff0dbd6d317e0bedc31d880219d6a9ecd961e353b277dc64e485649'  # Frozen actual post-BE/L10N baseline.
assert isinstance(BASELINE_LF_SHA256,str) and re.fullmatch('[0-9a-f]{64}',BASELINE_LF_SHA256),'Frozen baseline pin is malformed'
bb=blob(a.base,BASELINE_PATH)
assert sha(lf(bb))==BASELINE_LF_SHA256,'Only the exact committed CT baseline is accepted'
baseline=json.loads(bb.decode('utf-8-sig'))
assert baseline['kind']=='post-BE-L10N pre-FE actual baseline'
assert baseline['schema_version']==1 and re.fullmatch('[0-9a-f]{40}',baseline['source_ref'])
assert subprocess.run(['git','-C',str(repo),'merge-base','--is-ancestor',baseline['source_ref'],a.base],capture_output=True).returncode==0
assert baseline['acceptance_passed'] is False
suite_keys={'Diten.Web.Tests','pvg-api','pvg-regpv-base'}
assert set(baseline['suites'])==suite_keys and set(baseline['compiled_sources'])==suite_keys
cache_blobs([(commit,entry['path']) for commit in [a.base,baseline['source_ref']] for entry in baseline['frozen_paths']+[row for suite in suite_keys for row in baseline['compiled_sources'][suite]]]+[(a.base,item['trx_path']) for item in baseline['suites'].values()]+[(a.base,baseline['javascript']['json_path'])])
be_root='services/Diten.PvgService/'
be_prod={be_root+'src/Diten.PvgService.Application/RegPvBase/PvgIntakeDraftApplicationService.cs',
 be_root+'src/Diten.PvgService.Application/RegPvBase/PvgApplicationReasonCodes.cs',
 be_root+'src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs'}
resource_root='frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.'
required_frozen=set(paths)|be_prod|{resource_root+x+'.resx' for x in ['en','tr','fr','es','zh','ar','ru']}
frozen={x['path']:x for x in baseline['frozen_paths']}
assert len(frozen)==len(baseline['frozen_paths']) and required_frozen<=set(frozen)
for entry in baseline['frozen_paths']:
 assert sha(lf(blob(a.base,entry['path'])))==entry['lf_sha256']
 assert sha(lf(blob(baseline['source_ref'],entry['path'])))==entry['lf_sha256']
def pinned_artifact(path,expected):
 assert path.startswith('docs/records/audits/') and '..' not in Path(path).parts
 data=blob(a.base,path);assert sha(lf(data))==expected
 return data
for suite in suite_keys:
 item=baseline['suites'][suite];data=pinned_artifact(item['trx_path'],item['trx_lf_sha256'])
 rows=ET.fromstring(data).findall('.//{*}UnitTestResult')
 assert rows and len({x.get('testId') for x in rows})==len(rows) and all(x.get('outcome')=='Passed' for x in rows)
 assert collections.Counter(x.get('testName','') for x in rows)==collections.Counter(item['case_names'])
 assert all(item['case_names']) and len(item['case_names'])==item['case_count']
 if suite=='Diten.Web.Tests':assert len(rows)==62
 if suite=='pvg-regpv-base':assert len(rows)==211
 if suite=='pvg-api':assert len(rows)>396
 inputs=baseline['compiled_sources'][suite];assert inputs and len({x['path'] for x in inputs})==len(inputs)
 minimum=be_prod if suite=='pvg-api' else ({be_root+'src/Diten.PvgService.Application/RegPvBase/PvgIntakeDraftApplicationService.cs',be_root+'tests/Diten.PvgService.RegPvBase.Tests/PvgIntakeDraftApplicationServiceTests.cs'} if suite=='pvg-regpv-base' else {paths[1],paths[2]})
 assert minimum<={x['path'] for x in inputs}
 for entry in inputs:
  assert sha(lf(blob(a.base,entry['path'])))==entry['lf_sha256']
  assert sha(lf(blob(baseline['source_ref'],entry['path'])))==entry['lf_sha256']
js_base=baseline['javascript'];jsb=json.loads(pinned_artifact(js_base['json_path'],js_base['json_lf_sha256']).decode('utf-8-sig'))
jbrows=[x for f in jsb['testResults'] for x in f['assertionResults']]
assert len(jbrows)==34 and jsb['numTotalTests']==34 and jsb['success'] is True and all(x['status']=='passed' for x in jbrows)
assert collections.Counter(x['fullName'] for x in jbrows)==collections.Counter(js_base['case_names'])
assert js_base['case_count']==34 and all(js_base['case_names'])
sources=[]
for rel in paths:
 f=repo/rel;assert f.is_file();b=f.read_bytes()
 sources.append(dict(path=rel,raw_sha256=sha(b),lf_sha256=sha(lf(b)),mtime_ns=f.stat().st_mtime_ns))
changed=set(git('diff','--name-only',a.base,'--').decode().splitlines())
changed.update(git('ls-files','--others','--exclude-standard').decode().splitlines())
commands=[];suites={};issues=[]
def trx(file,assembly,suite):
 f=Path(file).resolve();b=f.read_bytes();xml=ET.fromstring(b);rows=xml.findall('.//{*}UnitTestResult')
 assert rows and len({x.get('testId') for x in rows})==len(rows)
 names=[x.get('testName','') for x in rows];counts=collections.Counter(names)
 prior=collections.Counter(baseline['suites'][suite]['case_names'])
 missing=list((prior-counts).elements())
 dll=Path(assembly).resolve();assert dll.is_relative_to(repo)
 assert dll.name=={'Diten.Web.Tests':'Diten.Web.Tests.dll','pvg-api':'Diten.PvgService.Api.Tests.dll','pvg-regpv-base':'Diten.PvgService.RegPvBase.Tests.dll'}[suite]
 for entry in baseline['compiled_sources'][suite]:
  if entry['path'] not in paths:assert sha(lf((repo/entry['path']).read_bytes()))==entry['lf_sha256']
 times=xml.find('./{*}Times');assert times is not None
 start=datetime.datetime.fromisoformat(times.get('start').replace('Z','+00:00'))
 assert start.tzinfo is not None
 storages=[x.get('storage','') for x in xml.findall('.//{*}UnitTest')]
 bound=bool(storages) and all(os.path.normcase(os.path.normpath(x))==os.path.normcase(str(dll)) for x in storages)
 assembly_after_sources=dll.stat().st_mtime_ns>=max((repo[x['path']]).stat().st_mtime_ns for x in baseline['compiled_sources'][suite])
 fresh=start.timestamp()>=dll.stat().st_mtime and assembly_after_sources
 for script,tail in [('check_trx_no_skipped.py',['--suite',suite,str(f)]),('known_failures.py',['check',str(f),'--suite',suite])]:
  argv=[sys.executable,'-B',str(repo/'scripts'/script),*tail]
  run=subprocess.run(argv,capture_output=True,text=True,encoding='utf-8',timeout=180)
  commands.append(dict(argv=argv,exit_code=run.returncode,stdout=run.stdout,stderr=run.stderr))
 ok=all(x.get('outcome')=='Passed' for x in rows) and not missing and bound and fresh
 summary=dict(path=str(f),raw_sha256=sha(b),lf_sha256=sha(lf(b)),case_count=len(rows),case_names=names,
  missing_baseline_cases=missing,all_passed=all(x.get('outcome')=='Passed' for x in rows),
  assembly=str(dll),assembly_sha256=sha(dll.read_bytes()),all_storage_bound=bound,source_before_assembly=assembly_after_sources,run_after_assembly=fresh)
 suites[suite]=summary
 return ok,names,dll
web_ok,wn,web_dll=trx(a.web_trx,a.web_assembly,'Diten.Web.Tests')
api_ok,an,api_dll=trx(a.api_trx,a.api_assembly,'pvg-api')
regpv_ok,rn,regpv_dll=trx(a.regpv_trx,a.regpv_assembly,'pvg-regpv-base')
web_only=all('.PvgCaseIntakeTriageWebTests.' in n for n in wn)
named_web={n:any(re.search(r'(?:^|\.)PvgCaseIntakeTriageWebTests\.'+re.escape(n)+r'(?:\(.*\))?\Z',full) is not None for full in wn) for n in web_names}
jfile=Path(a.js_json).resolve();jb=jfile.read_bytes();j=json.loads(jb.decode('utf-8-sig'))
assert j.get('testResults')
jrows=[case for file in j['testResults'] for case in file.get('assertionResults',[])]
jn=[x.get('fullName','') for x in jrows];jc=collections.Counter(jn)
jprior=collections.Counter(baseline['javascript']['case_names']);jmissing=list((jprior-jc).elements())
named_js={n:any(full==n or full.endswith(' '+n) for full in jn) for n in js_names}
js_files=all(os.path.normcase(os.path.normpath(x['name']))==os.path.normcase(str(repo/paths[3])) for x in j['testResults'])
js_fresh=isinstance(j.get('startTime'),(int,float)) and j['startTime']/1000>=max(x['mtime_ns'] for x in sources)/1e9
js_ok=(j.get('success') is True and len(jrows)==j.get('numTotalTests') and all(x.get('status')=='passed' for x in jrows)
 and j.get('numFailedTests')==0 and j.get('numPendingTests')==0 and j.get('numTodoTests',0)==0
 and not jmissing and all(named_js.values()) and js_files and js_fresh)
web_fresh_after_sources=web_dll.stat().st_mtime_ns>=max(x['mtime_ns'] for x in sources)
api_unchanged=(collections.Counter(an)==collections.Counter(baseline['suites']['pvg-api']['case_names']))
regpv_unchanged=(collections.Counter(rn)==collections.Counter(baseline['suites']['pvg-regpv-base']['case_names']))
passed=(changed==set(paths) and web_ok and web_only and all(named_web.values()) and api_ok and regpv_ok
 and api_unchanged and regpv_unchanged and js_ok and web_fresh_after_sources and all(x['exit_code']==0 for x in commands))
record=dict(kind='CT FE capture v1.0 of supplied fresh runs; not independent behavior E2',
 wrapper_lf_sha256=sha(lf(Path(__file__).read_bytes())),baseline_path=BASELINE_PATH,baseline_lf_sha256=BASELINE_LF_SHA256,baseline_source_ref=baseline['source_ref'],measured_head=git('rev-parse','HEAD').decode().strip(),base=a.base,
 sources=sources,changed_paths=sorted(changed),only_six_paths=changed==set(paths),suites=suites,
 all_web_results_from_target_class=web_only,named_web_cases=named_web,web_assembly_after_sources=web_fresh_after_sources,
 api_cases_unchanged=api_unchanged,regpv_cases_unchanged=regpv_unchanged,
 javascript=dict(path=str(jfile),raw_sha256=sha(jb),case_count=len(jrows),case_names=jn,missing_baseline_cases=jmissing,
  named_cases=named_js,exact_target_file=js_files,fresh=js_fresh,all_passed=js_ok),commands=commands,
 acceptance_passed=passed,limits='Mechanical capture only. Counts/names/hash/CLI outcomes alone never prove lossless values, complete-detail shapes, all29/nested omissions, nullable/date/null-array corpus, actual Razor/JS/FormData/MVC/API/store equality, four token carriers, token negatives, method bounds, compiling sabotages, two stores or cleanup. Independent actual E2 and final Linux/Phase1/main-human gates remain mandatory. Baseline is existing GREEN observations, not new behavior RED.')
out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes((json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode())
print(json.dumps(dict(acceptance_passed=passed,web_cases=len(wn),javascript_cases=len(jn))))
sys.exit(0 if passed else 1)
