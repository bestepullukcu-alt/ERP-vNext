"""CT capture for DL12 BE: real TRX/production checkers/source scope, never product edits."""
import argparse,datetime,hashlib,json,os,subprocess,sys,xml.etree.ElementTree as ET
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--repo',required=True);p.add_argument('--out',required=True)
p.add_argument('--saved-baseline',action='store_true');p.add_argument('--base')
p.add_argument('--api-trx');p.add_argument('--regpv-trx')
p.add_argument('--api-assembly');p.add_argument('--regpv-assembly')
a=p.parse_args();repo=Path(a.repo).resolve();out=Path(a.out).resolve()
assert not out.is_relative_to(repo),'Output must stay outside worker tree'
sha=lambda b:hashlib.sha256(b).hexdigest()
lf=lambda b:b.replace(b'\r\n',b'\n')
def git(*args):return subprocess.check_output(['git','-C',str(repo),*args])
ROOT='services/Diten.PvgService/'
paths=[ROOT+'src/Diten.PvgService.Application/RegPvBase/PvgIntakeDraftApplicationService.cs',
 ROOT+'src/Diten.PvgService.Application/RegPvBase/PvgApplicationReasonCodes.cs',
 ROOT+'src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs',
 ROOT+'tests/Diten.PvgService.RegPvBase.Tests/PvgIntakeDraftApplicationServiceTests.cs',
 ROOT+'tests/Diten.PvgService.Api.Tests/PvgCaseIntakeEditKeepsStoredDataTests.cs',
 ROOT+'tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoStartRefusalTests.cs',
 ROOT+'tests/Diten.PvgService.Api.Tests/PvgCaseIntakeTriageBindingTests.cs']
sources=[dict(path=x,present=(repo/x).is_file(),raw_sha256=sha((repo/x).read_bytes()) if (repo/x).is_file() else None,
 lf_sha256=sha(lf((repo/x).read_bytes())) if (repo/x).is_file() else None) for x in paths]
record=dict(wrapper_lf_sha256=sha(lf(Path(__file__).read_bytes())),repository=str(repo),
 measured_head=git('rev-parse','HEAD').decode().strip(),sources=sources,commands=[])
def read_trx(path):
 b=Path(path).read_bytes();r=ET.fromstring(b);rs=r.findall('.//{*}UnitTestResult');c=r.find('.//{*}Counters')
 assert c is not None and rs and len({x.get('testId') for x in rs})==len(rs)
 return r,rs,dict(path=str(Path(path).resolve()),raw_sha256=sha(b),lf_sha256=sha(lf(b)),counters=c.attrib,
 result_count=len(rs),all_passed=all(x.get('outcome')=='Passed' for x in rs))
if a.saved_baseline:
 ref='dd051eebde36c6ad61fcbc7fe2560dc24bb341cc'
 wb=git('show',ref+':docs/records/audits/2026-11/pvg-dl12-prep-web-integration-01/integration.trx')
 rb=git('show',ref+':docs/records/audits/2026-11/pvg-dl12-be-baseline-01/regpvbase-baseline.trx')
 assert sha(lf(wb))=='e2772c5aebfba847c54eabbfba7753af6329a8b99650e20ea42b8ce82fe3f4b5'
 assert sha(lf(rb))=='f778cfe8fadab6a70308a130a24ca7c826fb60aaa6921ccec4ceba3ac9ec1ffb'
 wr=ET.fromstring(wb);rr=ET.fromstring(rb);losses=[]
 for r in wr.findall('.//{*}UnitTestResult'):
  s='\n'.join(r.itertext())
  if 'Untouched reporter contact preserved: False' in s and 'Untouched evidence list preserved: False' in s:
   losses.append(dict(test=r.get('testName'),outcome=r.get('outcome'),observation='Existing connected no-loss gap; not a BE-only test'))
 assert len(losses)==2 and all(r.get('outcome')=='Passed' for r in rr.findall('.//{*}UnitTestResult'))
 assert len(rr.findall('.//{*}UnitTestResult'))==211 and not sources[4]['present']
 assert all(x['present'] for i,x in enumerate(sources) if i!=4),'Every one of the six existing BE sources must be present'
 same=True
 for x in sources:
  if not x['present']:continue
  old=git('show',ref+':'+x['path']);same=same and sha(lf(old))==x['lf_sha256']
 assert same,'BE source changed since baseline; saved runtime observations cannot be transferred silently'
 record.update(kind='Saved source-bound observations; not a new test run or BE behavior RED',
  baseline_ref=ref,regpv_existing_passed=211,prior_web_loss_observations=losses,
  new_feature_test_absent=True,be_scope_sources_same_as_saved_baseline=True,acceptance_passed=False)
else:
 assert all([a.base,a.api_trx,a.regpv_trx,a.api_assembly,a.regpv_assembly])
 ar,ars,api=read_trx(a.api_trx);rr,rrs,regpv=read_trx(a.regpv_trx)
 commands=[]
 for file,suite in [(a.api_trx,'pvg-api'),(a.regpv_trx,'pvg-regpv-base')]:
  for script,tail in [('check_trx_no_skipped.py',[file]),('known_failures.py',['check',file,'--suite',suite])]:
   argv=[sys.executable,'-B',str(repo/'scripts'/script),*tail]
   run=subprocess.run(argv,capture_output=True,text=True,encoding='utf-8')
   commands.append(dict(argv=argv,exit_code=run.returncode,stdout=run.stdout,stderr=run.stderr))
 changed=set(git('diff','--name-only',a.base,'--').decode().splitlines())
 changed.update(git('ls-files','--others','--exclude-standard').decode().splitlines())
 scope_ok=changed==set(paths) and all(x['present'] for x in sources)
 new=[x for x in ars if '.PvgCaseIntakeEditKeepsStoredDataTests.' in x.get('testName','')]
 required_cases=['Update_of_a_triaged_or_routed_draft_is_refused_and_changes_nothing',
 'Update_is_allowed_only_in_intake_created_and_intake_updated',
 'Update_never_erases_triage_or_route_fields',
 'Update_refusals_for_another_tenant_or_a_caller_without_the_key_never_say_not_editable',
 'Update_requires_the_version_read_with_the_editability_snapshot',
 'Concurrent_triage_between_update_read_and_replace_is_not_overwritten',
 'Field_policy_denial_does_not_disclose_draft_editability',
 'Field_policy_denial_does_not_disclose_record_existence',
 'Get_by_id_returns_the_fifteen_stored_fields',
 'Get_by_id_refused_by_field_security_returns_none_of_the_fifteen_values',
 'Update_with_the_values_read_back_keeps_every_stored_field']
 case_presence={n:any(('.PvgCaseIntakeEditKeepsStoredDataTests.'+n) in x.get('testName','') for x in new) for n in required_cases}
 targets=['Pvg_mongo_launch_arguments_follow_the_host_platform_without_weakening_isolation',
 'Pvg_mongo_test_harness_refuses_port_27017_and_fails_without_mongod',
 'Configuration_scan_fails_closed_on_inaccessible_directory']
 target_ok=all(sum(x.get('testName','').endswith(t) and x.get('outcome')=='Passed' for x in ars)==1 for t in targets)
 mongo=sum(any(k in x.get('testName','') for k in ['.Persistence.PvgMongoStoreTests.',
 '.Persistence.PvgMongoApiTests.','.Persistence.PvgMongoDedicatedTests.']) for x in ars)
 assemblies=[]
 for xml,file,idxs in [(ar,a.api_assembly,[0,1,2,4,5,6]),(rr,a.regpv_assembly,[0,1,3])]:
  f=Path(file).resolve();assert f.is_relative_to(repo) and f.suffix=='.dll'
  times=xml.find('./{*}Times');assert times is not None
  start=datetime.datetime.fromisoformat(times.get('start').replace('Z','+00:00'))
  assert start.tzinfo is not None
  storage=[x.get('storage','') for x in xml.findall('.//{*}UnitTest')]
  match=bool(storage) and all(os.path.normcase(os.path.normpath(x))==os.path.normcase(str(f)) for x in storage)
  fresh=f.stat().st_mtime_ns>=max((repo/paths[i]).stat().st_mtime_ns for i in idxs) and start.timestamp()>=f.stat().st_mtime
  assemblies.append(dict(path=str(f),sha256=sha(f.read_bytes()),all_storage_bound=match,fresh_source_assembly_trx=fresh))
 passed=(api['all_passed'] and regpv['all_passed'] and len(new)>0 and len(ars)==396+len(new)
  and all(case_presence.values()) and len(rrs)==211 and mongo==35 and target_ok and scope_ok and all(x['exit_code']==0 for x in commands)
  and all(x['all_storage_bound'] and x['fresh_source_assembly_trx'] for x in assemblies))
 record.update(kind='Normal full API and RegPvBase supplied runs',base=a.base,api=api,regpv=regpv,
  changed_paths=sorted(changed),only_seven_paths=scope_ok,new_feature_cases=len(new),required_named_cases=case_presence,expected_api_count=396+len(new),
  real_mongo_cases=mongo,win_targets_passed=target_ok,commands=commands,assemblies=assemblies,acceptance_passed=passed)
record['limits']='Capture alone does not prove method-only edits, policy/snapshot/race/DTO/no-loss behavior, two-store use, compiling sabotages or cleanup. Independent source/runtime E2 is mandatory; final browser no-loss is FE. Saved baseline is GREEN existing RegPvBase plus observed connected gap, not a newly executed BE RED.'
out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes((json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode())
print(json.dumps(dict(acceptance_passed=record['acceptance_passed'],kind=record['kind'])))
sys.exit(0 if record['acceptance_passed'] else 1)
