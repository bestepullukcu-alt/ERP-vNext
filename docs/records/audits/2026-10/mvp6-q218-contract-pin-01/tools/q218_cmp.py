import yaml,sys,json,hashlib
old,new=sys.argv[1],sys.argv[2]
A=yaml.safe_load(open(old)); B=yaml.safe_load(open(new))
def closure(doc,node,seen):
    if isinstance(node,dict):
        for k,v in node.items():
            if k=='$ref' and isinstance(v,str) and v.startswith('#/'):
                if v not in seen:
                    seen.add(v); t=doc
                    for p in v[2:].split('/'): t=t[p]
                    closure(doc,t,seen)
            else: closure(doc,v,seen)
    elif isinstance(node,list):
        for v in node: closure(doc,v,seen)
    return seen
def get(doc,ref):
    t=doc
    for p in ref[2:].split('/'): t=t[p]
    return t
def canon(x): return hashlib.sha256(json.dumps(x,sort_keys=True,default=str).encode()).hexdigest()[:16]
print('info.version',A['info']['version'],B['info']['version'])
print('paths equal set',sorted(A['paths'])==sorted(B['paths']),len(A['paths']))
for p in sorted(B['paths']):
    for m in B['paths'][p]:
        if m in('parameters',): continue
        a=A['paths'].get(p,{}).get(m); b=B['paths'][p][m]
        ca=closure(A,a,set()) if a else set(); cb=closure(B,b,set())
        opeq = a==b
        refs_eq = ca==cb and all(get(A,r)==get(B,r) for r in cb)
        print(f"{m.upper():5} {p:45} opId={b.get('operationId'):24} op_equal={opeq} closure_equal={refs_eq} refs={len(cb)} sha_old={canon(a)} sha_new={canon(b)}")
        if not refs_eq:
            print('     differing refs:',[r for r in cb if r not in ca or get(A,r)!=get(B,r)])
print('schemas differing:',[k for k in B['components']['schemas'] if A['components']['schemas'].get(k)!=B['components']['schemas'][k]])
for sec in B['components']:
    if sec!='schemas': print(sec,'equal',A['components'].get(sec)==B['components'][sec])
