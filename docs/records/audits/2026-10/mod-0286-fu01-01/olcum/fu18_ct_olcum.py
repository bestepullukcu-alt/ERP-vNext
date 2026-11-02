"""CT'nin kendi ölçümü: js/pages/** dosyaları hangi görünüm klasöründen yükleniyor. Alt ajanın betiğinden bağımsız, düz metin araması. Depo kökünden."""
import collections
import os

V = "frontend/Diten.Web/Views"
J = "frontend/Diten.Web/wwwroot/assets/js/pages"
views = {}
for d, _, fs in os.walk(V):
    for f in fs:
        if f.endswith(".cshtml"):
            p = os.path.join(d, f)
            views[p] = open(p, encoding="utf-8", errors="replace").read()
js = []
for d, _, fs in os.walk(J):
    for f in fs:
        if f.endswith(".js"):
            js.append(os.path.join(d, f)[len("frontend/Diten.Web/wwwroot/"):])
bins = collections.Counter()
none = []
for j in sorted(js):
    dirs = {os.path.dirname(p)[len(V) + 1:] for p, t in views.items() if j in t}
    k = "0" if not dirs else ("1" if len(dirs) == 1 else "2+")
    bins[k] += 1
    if not dirs:
        none.append(j)
print("pages toplam", len(js), dict(bins))
for n in none:
    print("  referanssız:", n)
