"""Build the n-gram and word tables embedded in RP.CodeBreaker from public-domain texts.

Usage:  python tools/build_language_data.py <folder of Gutenberg .txt files> <output folder> [prefix]

English (prefix ""): The Adventures of Sherlock Holmes (1661), Pride and Prejudice (1342),
A Tale of Two Cities (98), The War of the Worlds (36), The Thirty-Nine Steps (558).
German (prefix "german."): Faust I (2229), Die Verwandlung (22367), Werther (2407),
Effi Briest (5323), Kabale und Liebe (6498). Umlauts are written AE, OE, UE and ß as SS — as
Enigma operators wrote them, since the machine had only 26 keys.
"""
import re, sys, gzip, collections, os
src, out = sys.argv[1], sys.argv[2]
prefix = sys.argv[3] if len(sys.argv) > 3 else ""
texts = []
for f in sorted(os.listdir(src)):
    t = open(os.path.join(src, f), encoding='utf-8-sig', errors='ignore').read()
    s = t.find('*** START OF'); s = t.find('\n', s) if s >= 0 else 0
    e = t.find('*** END OF'); e = e if e >= 0 else len(t)
    texts.append(t[s:e])
body = '\n'.join(texts).upper()
letters = re.sub('[^A-Z]', '', body)
print('letters', len(letters))
mono = collections.Counter(letters)
quad = collections.Counter(letters[i:i+4] for i in range(len(letters)-3))
bi = collections.Counter(letters[i:i+2] for i in range(len(letters)-1))
words = collections.Counter(re.findall(r"[A-Z]+", re.sub(r"[’']", '', body)))
def w(name, rows):
    with gzip.open(os.path.join(out, prefix + name), 'wt', encoding='ascii', newline='\n') as g:
        for k, v in rows: g.write(f"{k} {v}\n")
w('monograms.txt.gz', sorted(mono.items()))
w('bigrams.txt.gz', sorted(bi.items()))
qrows = [(k, v) for k, v in quad.most_common() if v >= 2]
w('quadgrams.txt.gz', qrows)
wrows = [(k, v) for k, v in words.most_common() if v >= 3 and (len(k) > 1 or k in ('A','I'))][:20000]
w('words.txt.gz', wrows)
print('quad', len(qrows), 'words', len(wrows))
print(mono.most_common(12)); print(quad.most_common(8)); print(wrows[:15])
