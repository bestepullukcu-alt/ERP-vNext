#!/usr/bin/env python3
import csv
from pathlib import Path

ROOT=Path(__file__).resolve().parents[4]
BASE=ROOT/'docs/roadmap/plans/mvp6-effort-auth-and-integration-update-04/EFFORT.tsv'
OUT=Path(__file__).resolve().parent
with BASE.open(newline='',encoding='utf-8') as f: rows=list(csv.DictReader(f,delimiter='\t'))
fields=list(rows[0])
for r in rows:
    if r['id']=='0184-4-DELIVERED':
        r.update(optimistic_hours='37.2',most_likely_hours='52',pessimistic_hours='74',evidence_or_scope='docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-exec-01/BROWSER-ACCEPTANCE.tsv',reason_boundary='Real browser list/create/reload/row-status/UAS flows close the existing final UI-flow reserve; durable PNG and CT acceptance remain outside this row')
    elif r['id']=='0184-4-REMAINING':
        r.update(optimistic_hours='0',most_likely_hours='0',pessimistic_hours='0',evidence_or_scope='docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-exec-01/BROWSER-ACCEPTANCE.tsv',reason_boundary='Exact browser UI-flow reserve transferred to delivered; no screenshot or CT credit')
    elif r['id']=='0184-5-DELIVERED':
        r.update(optimistic_hours='16.8',most_likely_hours='24',pessimistic_hours='32.8',evidence_or_scope='docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-exec-01/ACCEPTANCE.tsv',reason_boundary='Composed Web-to-Gateway-to-Carrier with real Auth, tenant/LE isolation, restart persistence and refresh revocation closes the existing module integration reserve')
    elif r['id']=='0184-5-REMAINING':
        r.update(optimistic_hours='0',most_likely_hours='0',pessimistic_hours='0',evidence_or_scope='docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-exec-01/ACCEPTANCE.tsv',reason_boundary='Exact composed integration reserve transferred to delivered; shared Auth implementation/VER remains counted only under SHARED')
    elif r['id']=='0184-6-REMAINING':
        r.update(evidence_or_scope='docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-exec-01/CARRIER-REAL-AUTH-E2E-CT-HANDOFF.md',reason_boundary='E2E execution complete, but the co-mingled final acceptance reserve is retained because durable PNG is OPEN and independent CT/full-module disposition is PENDING')

with (OUT/'EFFORT.tsv').open('w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=fields,delimiter='\t',lineterminator='\n');w.writeheader();w.writerows(rows)

def sums(xs):
    d={s:[0.,0.,0.] for s in ('DELIVERED','REMAINING')}
    for r in xs:
        for i,k in enumerate(('optimistic_hours','most_likely_hours','pessimistic_hours')):d[r['state']][i]+=float(r[k])
    return d
modules=[];cats=[]
for r in rows:
    if r['module'] not in modules:modules.append(r['module'])
    if r['category'] not in cats:cats.append(r['category'])
with (OUT/'MODULE-SUMMARY.tsv').open('w',newline='') as f:
    names=['scope','delivered_most_likely','remaining_most_likely','total_most_likely','completion_percent'];w=csv.DictWriter(f,fieldnames=names,delimiter='\t',lineterminator='\n');w.writeheader()
    for m in modules+['portfolio']:
        t=sums(rows if m=='portfolio' else [r for r in rows if r['module']==m]);d=t['DELIVERED'][1];rem=t['REMAINING'][1];w.writerow(dict(scope=m,delivered_most_likely=f'{d:g}',remaining_most_likely=f'{rem:g}',total_most_likely=f'{d+rem:g}',completion_percent=f'{100*d/(d+rem):.1f}'))
with (OUT/'MODULE-CATEGORY-PERCENT.tsv').open('w',newline='') as f:
    names=['scope']+cats+['Overall'];w=csv.DictWriter(f,fieldnames=names,delimiter='\t',lineterminator='\n');w.writeheader()
    for m in modules+['portfolio']:
        xs=rows if m=='portfolio' else [r for r in rows if r['module']==m];o={'scope':m}
        for c in cats:
            t=sums([r for r in xs if r['category']==c]);d=t['DELIVERED'][1];rem=t['REMAINING'][1];o[c]=f'{100*d/(d+rem):.1f}' if d+rem else 'N/A'
        t=sums(xs);d=t['DELIVERED'][1];rem=t['REMAINING'][1];o['Overall']=f'{100*d/(d+rem):.1f}';w.writerow(o)
with (OUT/'RECALCULATION.tsv').open('w',newline='') as f:
    names=['scope','category','delivered_optimistic','delivered_most_likely','delivered_pessimistic','remaining_optimistic','remaining_most_likely','remaining_pessimistic','most_likely_completion_percent'];w=csv.DictWriter(f,fieldnames=names,delimiter='\t',lineterminator='\n');w.writeheader()
    for s in modules+['portfolio']:
        sr=rows if s=='portfolio' else [r for r in rows if r['module']==s]
        for c in cats+['TOTAL']:
            xs=sr if c=='TOTAL' else [r for r in sr if r['category']==c]
            if not xs:continue
            t=sums(xs);d=t['DELIVERED'];rem=t['REMAINING'];den=d[1]+rem[1];w.writerow(dict(scope=s,category=c,delivered_optimistic=f'{d[0]:g}',delivered_most_likely=f'{d[1]:g}',delivered_pessimistic=f'{d[2]:g}',remaining_optimistic=f'{rem[0]:g}',remaining_most_likely=f'{rem[1]:g}',remaining_pessimistic=f'{rem[2]:g}',most_likely_completion_percent=f'{100*d[1]/den:.1f}' if den else 'N/A'))
assert len(rows)==103
t=sums(rows);assert t['DELIVERED'][1]==1408 and t['REMAINING'][1]==1396 and sum((t['DELIVERED'][1],t['REMAINING'][1]))==2804
