import re, sys
# Per-slide mean error from verbose smartart output; with two files, side by side.
def load(p):
    d, cur = {}, None
    for line in open(p, encoding='utf-8-sig'):
        m = re.match(r'\s*slide\s+(\d+)\s+(\S+): (\d+)/(\d+)', line)
        if m:
            cur = (int(m.group(1)), m.group(2))
            d[cur] = [int(m.group(3)), int(m.group(4)), []]
            continue
        m = re.match(r'\s+\S+\s+ppt \(.*\)\s+sz\s*\d*\s+ours \(.*\)\s+sz\s*\d*\s+([\d.]+) %', line)
        if m and cur:
            d[cur][2].append(float(m.group(1)))
    return {k: (v[0], v[1], sum(v[2]) / len(v[2]) if v[2] else 0) for k, v in d.items()}
a = load(sys.argv[1])
b = load(sys.argv[2]) if len(sys.argv) > 2 else a
for k in sorted(a):
    x, y = a[k], b.get(k, (0, 0, 0))
    mark = '' if abs(x[2] - y[2]) < 0.05 else ('  better' if y[2] < x[2] else '  WORSE')
    print(f"{k[0]:4} {k[1]:28} {x[0]:3}/{x[1]:<3} {x[2]:6.1f}%   {y[0]:3}/{y[1]:<3} {y[2]:6.1f}%{mark}")
print(f"sum {sum(v[2] for v in a.values()):.1f} -> {sum(v[2] for v in b.values()):.1f}")
