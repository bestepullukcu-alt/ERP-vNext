# Q380: per operation, is the operation object and everything it reaches by $ref equal between two OpenAPI files?
# usage: python3 compare-ops.py <3.0.0 yaml> <3.1.0 yaml>   (PyYAML)
import sys, yaml
a, b = [yaml.safe_load(open(p)) for p in sys.argv[1:3]]
def ops(d): return {op['operationId']: (path, op) for path, item in d['paths'].items() for m, op in item.items() if isinstance(op, dict) and 'operationId' in op}
def resolve(d, ref):
    cur = d
    for part in ref[2:].split('/'): cur = cur[part]
    return cur
def reach(d, node, seen):
    if isinstance(node, dict):
        for k, v in node.items():
            if k == '$ref' and isinstance(v, str) and v.startswith('#/') and v not in seen: seen.add(v); reach(d, resolve(d, v), seen)
            else: reach(d, v, seen)
    elif isinstance(node, list):
        for x in node: reach(d, x, seen)
    return seen
oa, ob = ops(a), ops(b)
for oid in sorted(set(oa) | set(ob)):
    (pa, xa), (pb, xb) = oa[oid], ob[oid]
    refs = reach(a, xa, set()) | reach(b, xb, set())
    same = all(resolve(a, r) == resolve(b, r) for r in refs)
    print(f"{oid:30} {pa:44} op={'EQUAL' if xa == xb else 'DIFF'} reachable({len(refs)})={'EQUAL' if same else 'DIFF'}")
print("paths equal:", set(a['paths']) == set(b['paths']), "| info.version", a['info']['version'], '->', b['info']['version'])
