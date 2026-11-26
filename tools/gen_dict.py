# 依 OpenCC 1.4.2 的 BUILD 規則產生衍生詞庫，並複製需要的原始詞庫
import sys, os, shutil, collections, opencc
SRC, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
PREF = '# @reverse-prefer:'
def entries(path):
    prefs = {}
    out = []
    for line in open(path, encoding='utf-8'):
        t = line.rstrip('\r\n')
        if t.startswith(PREF):
            f = t[len(PREF):].split(); prefs[f[0]] = f[-1]; continue
        if not t or t.startswith('#'): continue
        k, v = t.split('\t', 1)
        out.append((k, v.split()))
    return out, prefs
NEED = ['STPhrases','STCharacters','TWPhrases','TWVariants','TWVariantsPhrases',
        'TSPhrases','TSCharacters','TWPhrasesRev','TWVariantsRevPhrases']
for n in NEED:
    shutil.copy(os.path.join(SRC, n + '.txt'), os.path.join(OUT, n + '.txt'))
# TWVariantsRev = swap(TWVariants)（含 reverse-preference）
ents, prefs = entries(os.path.join(SRC, 'TWVariants.txt'))
d = collections.OrderedDict()
for k, vs in ents:
    for v in vs: d.setdefault(v, []).append(k)
for k, pv in prefs.items():
    vs = d[k]; vs.insert(0, vs.pop(vs.index(pv)))
with open(os.path.join(OUT, 'TWVariantsRev.txt'), 'w', encoding='utf-8', newline='\n') as f:
    f.write('# Generated: reverse of TWVariants.txt (OpenCC 1.4.2)\n')
    for k in sorted(d): f.write(k + '\t' + ' '.join(d[k]) + '\n')
# STPhrases_GeneratedFromRegionalPhrases：HK/TW 用語的 key 經 t2s 後，長度 >= 3 的反向對應
cc = opencc.OpenCC('t2s')
coll = collections.defaultdict(list)
for n in ['HKPhrases', 'TWPhrases']:
    for k, vs in entries(os.path.join(SRC, n + '.txt'))[0]:
        ck = cc.convert(k)
        if len(ck) < 3: continue
        coll[ck].append(k)
conf = {k: v for k, v in coll.items() if len(set(v)) > 1}
assert not conf, conf
with open(os.path.join(OUT, 'STPhrases_GeneratedFromRegionalPhrases.txt'), 'w', encoding='utf-8', newline='\n') as f:
    f.write('# Generated from HKPhrases.txt, TWPhrases.txt keys via t2s (OpenCC 1.4.2)\n')
    for k in sorted(coll): f.write(k + '\t' + coll[k][0] + '\n')
for n in sorted(os.listdir(OUT)):
    print(n, os.path.getsize(os.path.join(OUT, n)))
