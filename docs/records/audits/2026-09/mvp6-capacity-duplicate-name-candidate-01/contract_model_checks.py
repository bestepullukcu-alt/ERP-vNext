#!/usr/bin/env python3
"""Disposable contract model only; never production/Mongo/HTTP execution evidence."""
import copy,json,pathlib,sys,unittest,yaml
P=pathlib.Path(__file__).resolve().parent
D=yaml.safe_load((P/'sandop-capacity.openapi.candidate.yaml').read_text())
EX=D['components']['responses']['CapacityPlanStateConflict']['content']['application/json']['examples']['scenarioNameConflict']['value']
CORR='aaaaaaaa-0000-4000-8000-000000000192'
def request(**kw):
 r=dict(tenant='t',le='le',plan='p',key='k',name='Shift',payload='valid',correlation=CORR,auth=True,grant=True,valid=True,exists=True,state='Draft',fixture=True);r.update(kw);return r
class Model:
 def __init__(self,mutant=None):self.names=set();self.receipts={};self.writes=[0,0,0,0];self.mutant=mutant
 def identity(self,r):
  name=r['name'].strip() if self.mutant=='trim' else r['name']
  return r['tenant'],r['le'],r['plan'],name
 def receiptkey(self,r):return r['tenant'],r['le'],r['plan'],r['key']
 def fingerprint(self,r):return r['name'],r['payload']
 def error(self,code,status,r):
  e=copy.deepcopy(EX);e['error']['code']=code;e['error']['correlationId']=r['correlation'];return status,e,{'X-Correlation-Id':r['correlation']}
 def prepare(self,r):
  if not r['auth']:return self.error('UNAUTHENTICATED',401,r)
  if not r['grant']:return self.error('FORBIDDEN',403,r)
  if not r['valid']:return self.error('INVALID_REQUEST',400,r)
  if not r['exists']:return self.error('UNKNOWN_CAPACITY_PLAN',404,r)
  if self.mutant=='duplicate_before_receipt' and self.identity(r) in self.names:return self.error('CAPACITY_SCENARIO_NAME_CONFLICT',409,r)
  prior=self.receipts.get(self.receiptkey(r))
  if prior:
   if prior[0]!=self.fingerprint(r):return self.error('IDEMPOTENCY_KEY_REUSED',409,r)
   return 201,copy.deepcopy(prior[1]),{'X-Correlation-Id':r['correlation']}
  if r['state']!='Draft':return self.error('CAPACITY_PLAN_STATE_CONFLICT',409,r)
  if not r['fixture']:return self.error('INVALID_CONSTRAINT_REFERENCE',422,r)
  if self.identity(r) in self.names:return self.error('CAPACITY_SCENARIO_NAME_CONFLICT',409,r)
  return None
 def commit(self,r):
  # Two requests may both pass prepare. This set insertion models the unique-name constraint.
  prior=self.receipts.get(self.receiptkey(r))
  if prior:return self.prepare(r)
  if self.identity(r) in self.names:
   return self.error('DEPENDENCY_UNAVAILABLE',503,r) if self.mutant=='race_503' else self.error('CAPACITY_SCENARIO_NAME_CONFLICT',409,r)
  self.names.add(self.identity(r));body={'scenarioId':str(len(self.names)),'name':r['name'],'contractVersion':'v1'}
  self.receipts[self.receiptkey(r)]=(self.fingerprint(r),copy.deepcopy(body));self.writes=[x+1 for x in self.writes];return 201,body,{'X-Correlation-Id':r['correlation']}
 def submit(self,r):return self.prepare(r) or self.commit(r)
 def uncertain(self,r):
  prior=self.receipts.get(self.receiptkey(r))
  if prior:return self.prepare(r)
  if self.mutant=='unknown_as_duplicate':return self.error('CAPACITY_SCENARIO_NAME_CONFLICT',409,r)
  return self.error('COMMIT_RESULT_UNRESOLVED',503,r)
class Cases(unittest.TestCase):
 mutant=None
 def setUp(self):self.m=Model(self.mutant)
 def code(self,result,status,code):self.assertEqual(result[0],status);self.assertEqual(result[1]['error']['code'],code)
 def test_deterministic_no_second_effect(self):
  self.assertEqual(self.m.submit(request())[0],201);r=self.m.submit(request(key='k2'));self.code(r,409,'CAPACITY_SCENARIO_NAME_CONFLICT');self.assertEqual(self.m.writes,[1]*4);self.assertEqual(r[1],EX);self.assertEqual(r[2]['X-Correlation-Id'],CORR)
 def test_race_same_outcome(self):
  a=request();b=request(key='k2');self.assertIsNone(self.m.prepare(a));self.assertIsNone(self.m.prepare(b));self.assertEqual(self.m.commit(a)[0],201);self.code(self.m.commit(b),409,'CAPACITY_SCENARIO_NAME_CONFLICT');self.assertEqual(self.m.writes,[1]*4);self.assertEqual(len(self.m.receipts),1)
 def test_same_key_race_replays(self):
  a=request();self.assertIsNone(self.m.prepare(a));self.assertIsNone(self.m.prepare(a));one=self.m.commit(a);self.assertEqual(self.m.commit(a),one);self.assertEqual(self.m.writes,[1]*4)
 def test_replay_before_state_fixture_name(self):
  original=self.m.submit(request());r=self.m.submit(request(state='Archived',fixture=False,correlation='bbbbbbbb-0000-4000-8000-000000000192'));self.assertEqual(r[:2],original[:2]);self.assertNotEqual(r[2],original[2]);self.assertEqual(self.m.writes,[1]*4)
 def test_changed_valid_payload_before_business(self):
  self.m.submit(request());self.code(self.m.submit(request(payload='different',state='Archived',fixture=False)),409,'IDEMPOTENCY_KEY_REUSED')
 def test_invalid_payload_before_receipt(self):
  self.m.submit(request());self.code(self.m.submit(request(valid=False)),400,'INVALID_REQUEST')
 def test_target_before_receipt(self):
  self.m.submit(request());self.code(self.m.submit(request(exists=False)),404,'UNKNOWN_CAPACITY_PLAN')
 def test_lifecycle_before_duplicate(self):
  self.m.submit(request());self.code(self.m.submit(request(key='new',state='Ready')),409,'CAPACITY_PLAN_STATE_CONFLICT')
 def test_fixture_before_duplicate(self):
  self.m.submit(request());self.code(self.m.submit(request(key='new',fixture=False)),422,'INVALID_CONSTRAINT_REFERENCE')
 def test_scope_separation(self):
  for kw in [{},{'tenant':'other'},{'le':'other'},{'plan':'other'}]:self.assertEqual(self.m.submit(request(**kw))[0],201)
  self.assertEqual(self.m.writes,[4]*4)
 def test_exact_name_no_normalization(self):
  for i,name in enumerate(['Shift','shift',' Shift','Shift ','é','e\u0301','x'*201]):self.assertEqual(self.m.submit(request(key=str(i),name=name))[0],201)
  self.assertEqual(self.m.writes,[7]*4)
 def test_escape_spelling_same_decoded_name(self):
  self.m.submit(request(name=json.loads('"Shift"')));self.code(self.m.submit(request(key='new',name=json.loads('"\\u0053hift"'))),409,'CAPACITY_SCENARIO_NAME_CONFLICT')
 def test_deleted_name_does_not_occupy_active_key(self):
  self.m.submit(request());self.m.names.remove(self.m.identity(request()));self.assertEqual(self.m.submit(request(key='new'))[0],201)
 def test_unknown_commit_not_duplicate(self):
  self.m.submit(request());self.code(self.m.uncertain(request(key='other')),503,'COMMIT_RESULT_UNRESOLVED');self.assertEqual(self.m.uncertain(request())[0],201)
 def test_auth_grant_precede_receipt(self):
  self.m.submit(request());self.code(self.m.submit(request(auth=False)),401,'UNAUTHENTICATED');self.code(self.m.submit(request(grant=False)),403,'FORBIDDEN')
def run(mutant=None):
 Cases.mutant=mutant;suite=unittest.defaultTestLoader.loadTestsFromTestCase(Cases);r=unittest.TestResult();suite.run(r);return r
r=run();assert r.wasSuccessful(),r.failures
mutants={}
for m in ['trim','duplicate_before_receipt','race_503','unknown_as_duplicate']:
 x=run(m);mutants[m]={'rejected':not x.wasSuccessful(),'failed_cases':[str(t) for t,_ in x.failures+x.errors]};assert not x.wasSuccessful(),m
print(json.dumps({'scope':'E1/E2 in-memory contract model, NOT Mongo race or production evidence','passed':r.testsRun,'failed':len(r.failures)+len(r.errors),'mutants':mutants},indent=2))
