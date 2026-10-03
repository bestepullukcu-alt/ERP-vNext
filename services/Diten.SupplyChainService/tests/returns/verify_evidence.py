#!/usr/bin/env python3
import argparse,hashlib,json,pathlib,xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('directory');a=p.parse_args();root=pathlib.Path(a.directory)
records=list(root.rglob('execution.json'));assert records,'No executed records'
for path in records:
 record=json.loads(path.read_text());assert record['exit']==0,(path,record['exit']);assert len(record['binarySha256'])==64
 trx=ET.parse(path.parent/'returns.trx');c=trx.find('.//{*}Counters');assert c is not None;assert int(c.get('failed','0'))==0;assert int(c.get('passed','0'))>0
 print(str(path),c.attrib)
for path in sorted(root.rglob('*')):
 if path.is_file():print(hashlib.sha256(path.read_bytes()).hexdigest(),str(path.relative_to(root)))
