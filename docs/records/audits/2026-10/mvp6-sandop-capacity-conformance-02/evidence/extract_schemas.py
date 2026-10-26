import re,sys,json
src=open(sys.argv[1]).read().split('\n')
def ind(l): return len(l)-len(l.lstrip(' '))
# find components: -> schemas:
si=next(i for i,l in enumerate(src) if re.match(r'^  schemas:\s*$',l))
body=[]
for j in range(si+1,len(src)):
    if src[j].strip() and ind(src[j])<=2: break
    body.append(src[j])

schemas={}
i=0
while i<len(body):
    m=re.match(r'^    ([A-Za-z0-9]+):\s*$',body[i])
    if not m: i+=1; continue
    name=m.group(1); j=i+1; blk=[]
    while j<len(body) and (not body[j].strip() or ind(body[j])>4):
        blk.append(body[j]); j+=1
    text='\n'.join(blk)
    # required: inline list or dashed list
    req=[]
    rm=re.search(r'^\s*required:\s*\[([^\]]*)\]',text,re.M)
    if rm: req=[x.strip() for x in rm.group(1).split(',') if x.strip()]
    else:
        rm2=re.search(r'^(\s*)required:\s*$',text,re.M)
        if rm2:
            for rl in text[rm2.end():].split('\n'):
                dm=re.match(r'^\s*-\s*(\S+)\s*$',rl)
                if dm: req.append(dm.group(1))
                elif rl.strip(): break
    # properties at the level directly under "properties:"
    props={}
    pm=re.search(r'^(\s*)properties:\s*$',text,re.M)
    if pm:
        plines=text[pm.end():].split('\n'); base=None
        for k,pl in enumerate(plines):
            if not pl.strip(): continue
            if base is None:
                base=ind(pl)
            if ind(pl)<base: break
            if ind(pl)==base:
                nm=re.match(r'^\s*([A-Za-z0-9_]+):',pl)
                if nm:
                    # gather this property's own sub-block for nullable/type
                    sub=[]
                    for pl2 in plines[k+1:]:
                        if pl2.strip() and ind(pl2)<=base: break
                        sub.append(pl2)
                    st='\n'.join(sub)+pl
                    props[nm.group(1)]={
                        'nullable': bool(re.search(r'nullable:\s*true',st)),
                        'type': (re.search(r'type:\s*(\S+)',st) or [None,None])[1],
                        'ref': (re.search(r"\$ref:\s*'#/components/schemas/([A-Za-z0-9]+)'",st) or [None,None])[1],
                    }
    schemas[name]={'required':req,'properties':props}
    i=j
print(json.dumps(schemas,indent=1))
