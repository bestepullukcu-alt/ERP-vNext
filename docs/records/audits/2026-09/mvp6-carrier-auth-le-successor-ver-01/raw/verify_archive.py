import csv
import hashlib
import pathlib
import sys
import tarfile


repository = pathlib.Path(__file__).resolve().parents[6]
package = repository / "docs/records/audits/2026-09/mvp6-carrier-auth-le-successor-01"
rows = list(csv.DictReader((package / "SOURCE-MANIFEST.tsv").open(), delimiter="\t"))

with tarfile.open(package / "source-archive.tar.gz", "r:gz") as archive:
    members = {
        member.name.removeprefix("./"): member
        for member in archive.getmembers()
        if member.isfile()
    }
    passed = 0
    for row in rows:
        path = row["path"]
        member = members.get(path)
        observed = (
            hashlib.sha256(archive.extractfile(member).read()).hexdigest()
            if member is not None
            else "MISSING"
        )
        result = "PASS" if observed == row["target_sha256"] else "FAIL"
        print(result, path, row["target_sha256"], observed, sep="\t")
        passed += result == "PASS"

print("SUMMARY", f"{passed}/{len(rows)}", sep="\t")
sys.exit(0 if passed == len(rows) else 1)
