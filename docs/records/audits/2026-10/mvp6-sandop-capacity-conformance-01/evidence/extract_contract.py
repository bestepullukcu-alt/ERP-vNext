import re,sys,json
src=open(sys.argv[1]).read().split('\n')
def indent(l): return len(l)-len(l.lstrip(' '))
# locate paths block
start=next(i for i,l in enumerate(src) if re.match(r'^paths:',l))
end=len(src)
for i in range(start+1,len(src)):
    if src[i] and indent(src[i])==0 and not src[i].startswith('#'):
        end=i; break
block=src[start+1:end]

ops=[]; cur_path=None
i=0
while i<len(block):
    l=block[i]
    m=re.match(r'^  (/\S+):\s*$',l)
    if m: cur_path=m.group(1); i+=1; continue
    m=re.match(r'^    (get|post|put|patch|delete|options):\s*$',l)
    if m and cur_path:
        meth=m.group(1).upper()
        j=i+1; body=[]
        while j<len(block) and (not block[j].strip() or indent(block[j])>4):
            body.append(block[j]); j+=1
        text='\n'.join(body)
        opid=(re.search(r'^\s*operationId:\s*(\S+)',text,re.M) or [None,None])[1]
        # response codes: keys at the indent directly under "responses:"
        codes=[]
        rm=re.search(r'^(\s*)responses:\s*$',text,re.M)
        if rm:
            rlines=text[rm.end():].split('\n')
            base=None
            for rl in rlines:
                if not rl.strip(): continue
                ind=indent(rl)
                if base is None:
                    if ind<=len(rm.group(1)): break
                    base=ind
                if ind<base: break
                if ind==base:
                    cm=re.match(r"^\s*'?(\d{3})'?:",rl)
                    if cm: codes.append(cm.group(1))
        errs=sorted(set(re.findall(r'^\s*code:\s*"?([A-Z][A-Z0-9_]{3,})"?',text,re.M)))
        hdrs=sorted(set(re.findall(r'^\s*-?\s*name:\s*(\S+)\s*$',text,re.M))) if 'in: header' in text else []
        hdr_pairs=re.findall(r'name:\s*(\S+)\s*\n\s*in:\s*header',text)
        reqd=sorted(set(re.findall(r'^\s*-\s+([a-zA-Z][A-Za-z0-9]*)\s*$',text,re.M)))
        ops.append(dict(path=cur_path,method=meth,operationId=opid,codes=sorted(set(codes)),
                        errors=errs,headers=sorted(set(hdr_pairs))))
        i=j; continue
    i+=1
print(json.dumps(ops,indent=1))
