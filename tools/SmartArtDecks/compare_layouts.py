import re, sys
def load(p):
    d = {}
    for line in open(p, encoding='utf-8-sig'):
        m = re.match(r'\s*(\S+)\s+(\d+)/(\d+)\s+mean error\s+([\d.]+) %', line)
        if m: d[m.group(1)] = (float(m.group(4)), int(m.group(2)), int(m.group(3)))
    return d
a, b = load(sys.argv[1]), load(sys.argv[2])
changed = [(k, a[k], b[k]) for k in a if a[k] != b.get(k)]
tot_a = sum(v[0] for v in a.values()); tot_b = sum(v[0] for v in b.values())
print(f"changed {len(changed)}  sum of mean errors {tot_a:.1f} -> {tot_b:.1f}")
for k, x, y in sorted(changed, key=lambda t: t[2][0] - t[1][0]):
    print(f"  {k:32} {x[0]:6.1f}% {x[1]}/{x[2]} -> {y[0]:6.1f}% {y[1]}/{y[2]}")
