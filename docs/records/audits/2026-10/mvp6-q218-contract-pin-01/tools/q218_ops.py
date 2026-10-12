import yaml,sys,json
D=yaml.safe_load(open(sys.argv[1]))
def res(n):
    while isinstance(n,dict) and '$ref' in n:
        t=D
        for p in n['$ref'][2:].split('/'): t=t[p]
        n=t
    return n
for path,m in [('/shipments/{shipmentId}','get'),('/carriers','get')]:
    op=D['paths'][path][m]
    print('=====',m.upper(),path,op['operationId'],'| security',op.get('security',D.get('security')))
    pl=D['paths'][path].get('parameters',[])+op.get('parameters',[])
    for p in pl:
        r=res(p); print('  PARAM',r['in'],r['name'],'required=',r.get('required',False),json.dumps(r.get('schema')), '<-',p.get('$ref',''))
    print('  requestBody',op.get('requestBody'))
    for code,r in op['responses'].items():
        rr=res(r); c=rr.get('content',{})
        sch={k:v.get('schema') for k,v in c.items()}
        ex=[]
        for v in c.values():
            for name,e in (v.get('examples') or {}).items():
                val=res(e).get('value',{})
                if isinstance(val,dict) and 'error' in val: ex.append(val['error'].get('code'))
        print('  RESP',code,r.get('$ref',''),json.dumps(sch),'headers',list((rr.get('headers') or {}).keys()),'errorCodes',ex)
for s in ['ShipmentDetail','ShipmentLine','ProofOfDelivery','ShipmentStatus','CarrierListResponse','CarrierSummary','CarrierStatus','TransportMode','Error','ContractVersion']:
    if s in D['components']['schemas']: print('--',s,json.dumps(D['components']['schemas'][s],default=str)[:2500])
print('servers',D.get('servers'))
