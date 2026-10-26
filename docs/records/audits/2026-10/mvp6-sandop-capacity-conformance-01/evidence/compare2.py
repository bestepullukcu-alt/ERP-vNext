import re,subprocess
Y='docs/analysis/contracts/sandop-capacity.openapi.yaml'
y=open(Y).read()
declared=set()
for m in re.finditer(r'x-error-codes:\s*\[([^\]]*)\]',y):
    declared|={c.strip() for c in m.group(1).split(',') if c.strip()}
declared|=set(re.findall(r'code:\s*([A-Z][A-Z0-9_]{3,})',y.split('\n  responses:',1)[-1]))

SRC='services/Diten.SupplyChainService/src'
def rg(pat):
    return subprocess.run(['grep','-rhoE',pat,SRC,'--include=*.cs'],capture_output=True,text=True).stdout.splitlines()

# every (status, code) pair the two features can emit
pairs=set()
for l in rg(r'SandopResult\.Error\([0-9]{3},"[A-Z_]+"\)'):
    m=re.search(r'\((\d{3}),"([A-Z_]+)"\)',l); pairs.add((m.group(1),m.group(2),'Sandop'))
for l in rg(r'\.Fail\("[A-Z_]+",[0-9]{3}\)'):
    m=re.search(r'\("([A-Z_]+)",(\d{3})\)',l); pairs.add((m.group(2),m.group(1),'Capacity'))
# factory constants (reachable via Create)
def consts(p): return set(re.findall(r'"([A-Z][A-Z0-9_]{3,})"',open(p).read()))
b='services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/'
factory=consts(b+'SandopPlans/SandopContractError.cs')|consts(b+'CapacityPlans/CapacityContractError.cs')
emitted={c for _,c,_ in pairs}
impl=factory|emitted

print("contract-declared codes:",len(declared))
print("implementation codes   :",len(impl),f"({len(factory)} in factories, {len(emitted)} at emission sites)")
print()
print("A) emitted/declarable in code but NOT in the contract:")
a=sorted(impl-declared); print("   none" if not a else "")
for c in a: print("   -",c)
print()
print("B) in the contract but NOT found anywhere in code:")
bb=sorted(declared-impl); print("   none" if not bb else "")
for c in bb: print("   -",c)
print()
print("C) (status, code) pairs whose STATUS the contract never declares:")
cs=set(re.findall(r"^\s*'?(\d{3})'?:",y,re.M))
for s,c,f in sorted(pairs):
    if s not in cs: print(f"   - {f}: {s} {c}")
