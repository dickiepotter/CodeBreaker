# RP.CodeBreaker — codes, ciphers and how they are broken, explained

> A C# library for making and breaking codes and ciphers, written to be **read and understood**
> before it is used. It is a **tutorial in code**: a guided tour from Caesar's shift to the Enigma
> machine and beyond, aimed at anyone meeting this material for the first time. It assumes nothing
> beyond **school maths** — counting, a little probability, the idea of a logarithm — and its theme
> throughout is **difficulty**: how big each cipher's haystack of keys is, how the code breaker's
> insight and the scraps of information they hold shrink it, and where the line falls between what can
> be broken in a few minutes and what cannot be broken at all.

*This project began with a visit to Bletchley Park. Standing in front of the rebuilt bombe, what struck
me was not the machinery but the reasoning: a few words the code breakers expected to find in a
message turned a problem bigger than the number of grains of sand on Earth into one a room of
whirring drums could finish before lunch. This library tries to make that reasoning something you can
run, measure and change.*

**Try it in your browser:** [the showcase](https://dickiepotter.github.io/CodeBreaker/) — the same
library compiled to WebAssembly, running in a Web Worker behind a Svelte front end.

---

## Contents

- [What this project is for](#what-this-project-is-for)
- [A few words we will use](#a-few-words-we-will-use)
- [Codes and ciphers: converting both ways](#codes-and-ciphers-converting-both-ways)
- [How difficulty is measured](#how-difficulty-is-measured)
- [The difficulty ladder: what falls in a few minutes?](#the-difficulty-ladder-what-falls-in-a-few-minutes)
- [How known information shrinks the search](#how-known-information-shrinks-the-search)
- [When no amount of computing will do: unicity distance](#when-no-amount-of-computing-will-do-unicity-distance)
- [Teaching a computer what English looks like](#teaching-a-computer-what-english-looks-like)
- [The first look: what kind of cipher is it?](#the-first-look-what-kind-of-cipher-is-it)
- [The ciphers, one by one](#the-ciphers-one-by-one)
  - [Caesar, ROT13 and Atbash](#caesar-rot13-and-atbash)
  - [Affine](#affine)
  - [Simple substitution](#simple-substitution)
  - [Polybius square](#polybius-square)
  - [Vigenère, Beaufort and the keyed Vigenère](#vigenère-beaufort-and-the-keyed-vigenère)
  - [Autokey](#autokey)
  - [Rail fence](#rail-fence)
  - [Columnar transposition](#columnar-transposition)
  - [Playfair](#playfair)
  - [Hill](#hill)
  - [Bifid and ADFGVX](#bifid-and-adfgvx)
  - [The one-time pad, and the pad used twice](#the-one-time-pad-and-the-pad-used-twice)
  - [Repeating-key XOR](#repeating-key-xor)
  - [Enigma](#enigma)
- [Breaking Enigma: the bombe](#breaking-enigma-the-bombe)
- [Modern cryptography: where the shortcuts run out](#modern-cryptography-where-the-shortcuts-run-out)
- [Famous cases](#famous-cases)
- [Common situations, and what to do](#common-situations-and-what-to-do)
- [Using the library](#using-the-library)
- [The demo website](#the-demo-website)
- [Building, testing and deploying](#building-testing-and-deploying)
- [Code reused from other projects, and keeping it in sync](#code-reused-from-other-projects-and-keeping-it-in-sync)
- [Points of interest](#points-of-interest)
- [Future considerations](#future-considerations)
- [Status and history](#status-and-history)

---

## What this project is for

Most writing about cryptography falls into two camps. One tells the stories — Mary, Queen of Scots,
the Zimmermann telegram, Bletchley Park — without ever showing how the codes were actually broken.
The other is mathematics for specialists. This library sits between them: every cipher is a small,
readable class, every attack is a short, readable method, and this document explains each one from
the ground up, with the numbers measured by the code rather than quoted from somewhere else.

A few principles run through everything:

- **Difficulty first.** Every cipher has a key space (how many keys there are), every attack returns a
  `Difficulty` — a funnel showing how the search shrank at each step — and the
  `CipherCatalogue` places everything on one ladder, from Caesar to AES, against a line marked "a
  few minutes".
- **No unexplained jargon.** Wherever a term appears, it is introduced in plain language first. If you
  can count, you can follow it.
- **Verbose on purpose.** This document goes into far more detail than a working programmer strictly
  needs. If any part seems patronising, please do not be offended — it is written for a wide audience,
  including people meeting these ideas for the first time.
- **Honest numbers.** The timings and work figures quoted here come from running the library (on one
  core of an ordinary laptop, in Release mode). Where something is an estimate — the cost of factoring
  a 2048-bit number, say — it says so.
- **Clarity over speed.** The attacks are quick enough to break everything they claim to in seconds,
  but they are written to be followed, not to win competitions.

> **A word on purpose.** Everything here is historical or deliberately weak. Breaking a Caesar shift
> or a 1940s Enigma message harms no one, and understanding *why* they fell is the best possible
> education in why modern systems are built the way they are. The [modern
> section](#modern-cryptography-where-the-shortcuts-run-out) shows, with numbers, why the same
> techniques get nowhere against properly-used modern cryptography.

---

## A few words we will use

- **Plaintext** — the message as written. **Ciphertext** — the message after encryption.
- **Encrypt / decrypt** (or **encipher / decipher**) — turn plaintext into ciphertext, and back.
- **Key** — the secret setting that makes your encryption different from everyone else's using the
  same method: the shift of a Caesar cipher, the keyword of a Vigenère, the daily settings of an Enigma.
- **Key space** — the set of all possible keys. Its *size* is the most important number in this
  library: it is the size of the haystack a brute-force search must work through.
- **Brute force** — trying every key until one works. Always possible in principle; the question is
  always *how long*.
- **Bits** — a way of writing big numbers: *n* bits means 2ⁿ. Each extra bit doubles the work. A
  Caesar cipher has 26 keys, about 4.7 bits; AES-128 has 2¹²⁸ keys, exactly 128 bits.
- **Crib** — a piece of plaintext the code breaker believes is in the message, because of its context:
  WETTERBERICHT ("weather report") at the start of a German weather report, DEAR at the start of a
  letter. Bletchley Park slang, now standard.
- **Cryptography** is making ciphers; **cryptanalysis** is breaking them. Together they are
  **cryptology**.
- **Code** and **cipher** mean different things — see the next section.

---

## Codes and ciphers: converting both ways

In everyday speech "code" and "cipher" are used interchangeably. To a code breaker they are different
animals, and the difference decides how you attack them.

A **cipher** transforms a message letter by letter (or bit by bit) using a rule and a secret **key**.
Learn the rule and the key and every message opens. Every cipher in this library implements `ICipher`
and is an immutable object built from its key:

```csharp
ICipher caesar = new CaesarCipher(3);
string secret = caesar.Encrypt("Attack at dawn!");   // "Dwwdfn dw gdzq!"
string clear  = caesar.Decrypt(secret);              // "Attack at dawn!"
```

The letter-for-letter ciphers (Caesar, affine, substitution, Vigenère and friends) leave spaces,
punctuation and capitalisation alone so the result still looks like a sentence. Those that work on
blocks or move letters about (transpositions, Playfair, Hill, Enigma) work on the bare letters, in
capitals — `Letters.Normalise("Attack at dawn!")` is `"ATTACKATDAWN"`, and `Letters.Group(...)` writes
it in the traditional blocks of five, `ATTAC KATDA WN`, so that word lengths give nothing away.

A **code** replaces whole symbols, words or phrases with others, using a table. Codes implement
`ICode`, with `Encode` and `Decode`:

| Code | What it does | Example |
|---|---|---|
| `MorseCodeConverter` | Dots and dashes for telegraph and radio | `SOS` → `... --- ...` |
| `NatoPhoneticCode` | A word for each letter, for crackly voice lines | `BP` → `BRAVO PAPA` |
| `A1Z26Code` | Letters as their position in the alphabet | `HELLO` → `8-5-12-12-15` |
| `BaconianCode` | Francis Bacon's 1605 five-symbol A/B code, made for hiding in innocent text | `AB` → `AAAAA AAAAB` |
| `TapCode` | Row and column taps on a 5×5 grid, used by prisoners of war | `H` → `..,...` |
| `BinaryCode`, `HexCode`, `Base64Code` | Bytes as bits, hex digits or 64 safe characters | `Hi` → `SGk=` |
| `Ita2Code` | The 5-bit Baudot–Murray teleprinter code — the code inside every Lorenz (Tunny) message | `HI` → `10100 00110` |
| `Codebook` | Words and phrases as number groups, in the style of a 1917 diplomatic code | `ATTACK AT DAWN` → `4071 0005 2216` |

The public codes — Morse, ASCII, Base64 — hide nothing; they exist to *carry* text. (Base64 is often
mistaken for encryption. It is not.) Secret codes keep the table private. There is no key to search:
the book *is* the secret, and a code is broken by rebuilding the book from context, one group at a
time — which is exactly how the Admiralty's Room 40 read the Zimmermann telegram in 1917.

For anything that needs to convert generically (the demo site's converter page, a command line), two
registries list everything:

```csharp
foreach (ICode code in CodeCatalogue.All) { ... code.Encode(text) ... }

CipherDefinition def = CipherRegistry.Named("Playfair");   // name, key format, example key
ICipher playfair = def.Create("MONARCHY");                 // build it from a typed key
```

---

## How difficulty is measured

### The key space

Every cipher's difficulty starts with one number: how many keys it could have been set up with. The
library holds it as a `KeySpace` — an exact count (a `BigInteger`, so even 2²⁵⁶ is held exactly), a
description of how it was worked out, and the same number in bits:

```csharp
CaesarCipher.KeySpace          // 26 keys (4.7 bits) — 26 shifts
SubstitutionCipher.KeySpace    // ≈ 4.03 × 10^26 keys (88.4 bits) — 26! orderings of the alphabet
EnigmaMachine.KeySpaceFor()    // ≈ 1.07 × 10^23 keys (76.5 bits) — 60 rotor orders × 26³ start
                               //   positions × 26² ring settings × plugboard with 10 cables
```

Most key spaces are built from two shapes of counting, and it is worth seeing them once:

- **Independent choices multiply.** A five-letter Vigenère key is five choices of 26: 26 × 26 × 26 ×
  26 × 26 = 26⁵ = 11,881,376. `KeySpace.Power(26, 5)`.
- **Orderings are factorials.** A substitution alphabet is one ordering of the 26 letters: 26 choices
  for what A becomes, then 25 left for B, then 24… That is 26 × 25 × 24 × … × 1, written 26! ("26
  factorial"). `KeySpace.Factorial(26)`.
- **Choices made together multiply again.** `KeySpace.Times(other)`: 60 rotor orders × 17,576 start
  positions.

Factorials outgrow powers astonishingly quickly — 26! is about 4 × 10²⁶ — and this is the first
lesson of the library: **a big key space is easy to build, and on its own it guarantees nothing.**
Simple substitution has more keys than DES and is solved over breakfast.

> The library computes `log₂(n!)` for very large *n* through the log-gamma function
> (`KeySpace.Log2Factorial`), because ln n! = ln Γ(n + 1). That function comes from the vendored
> statistics code — see [code reused from other projects](#code-reused-from-other-projects-and-keeping-it-in-sync).

### From keys to time

A key space becomes a *time* once you know who is searching. An `Attacker` is a name and a rate in
keys per second, and the presets span eighty years of code breaking:

| `Attacker` | Keys per second | Where the figure comes from |
|---|---:|---|
| `PencilAndPaper` | 0.1 | one trial decryption every ten seconds or so |
| `Bombe` | 15 | a Bletchley bombe ran through the 17,576 positions of one wheel order in about 20 minutes |
| `Laptop` | 10⁷ | one core running a cheap trial decryption of a few dozen letters |
| `GraphicsCard` | 10¹⁰ | thousands of cores testing a simple hash or cipher |
| `DeepCrack` | 9 × 10¹⁰ | the EFF's 1998 DES-cracking machine |
| `WholeBitcoinNetwork` | 10²¹ | every Bitcoin miner on Earth — the largest search humanity has ever built |

The arithmetic is simply **time = keys ÷ rate**. `KeySpace.ExhaustiveSearch(attacker)` gives the time
to try every key; `ExpectedSearch` gives half that, since on average you find the key halfway through.
Each answer is a `WorkEstimate`, which describes itself in words and gives a verdict:

```csharp
SubstitutionCipher.KeySpace.ExhaustiveSearch(Attacker.Laptop)
// 1.28 × 10^12 years (92 times the age of the universe) — Feasibility.Infeasible
```

The verdicts (`Feasibility`) are **Instant** (under a second), **FewMinutes** (up to five minutes —
the line this library cares about), **Hours** (up to a day), **Months** (up to a year), **Lifetime**
(up to a century) and **Infeasible**.

### Why the key space is not the difficulty

If the key space were the whole story, simple substitution would be stronger than DES. It is not,
because nobody searches 26! keys one at a time. Each piece of insight a code breaker has — a property
of the language, a property of the cipher, a word they expect — cuts the search, and the real
difficulty is what is left. The library records that story as a `Difficulty`: the starting key
space, then each `Reduction` with what was learned, how many keys remained and why.

Here is the funnel the Vigenère attack produces for the [indéchiffrable example](#famous-cases):

```
Start: every key of 1–12 letters                    ≈ 9.92 × 10^16 keys (56.5 bits)
  → Key length is 7                                 8,031,810,176 keys (32.9 bits)
  → Each column is a Caesar cipher                  182 keys (7.5 bits)
```

The last line is the point. Once the key length is known, each of the seven key letters affects only
its own column of the message, so each can be found **on its own** by trying 26 shifts. The work is
not 26⁷ but 7 × 26 = 182. **A product has become a sum.** Most of code breaking, from al-Kindi to
Turing, is finding a way to make that happen: to split one enormous search into many small ones that
can be done separately.

---

## The difficulty ladder: what falls in a few minutes?

`CipherCatalogue.All` puts every cipher in the library (and some modern ones for scale) on one ladder.
For each it gives the full key space, the *effective work* — the number of trials the best attack
actually needs on a message of a few hundred letters — and the time that takes. These are the
library's own figures for one laptop core:

| Cipher | Key space | Effective work | Best attack | Laptop, best attack | Laptop, brute force |
|---|---:|---:|---|---|---|
| Atbash | 0 bits | 0 bits | recognise it | instant | instant |
| Caesar | 4.7 | 4.7 | try all 26 shifts | instant | instant |
| Rail fence | 7.6 | 7.6 | try every rail count | instant | instant |
| Affine | 8.3 | 8.3 | try all 312 keys | instant | instant |
| Hill (2×2) | 17.3 | 10.4 | search each matrix row alone | instant | 16 ms |
| Vigenère (key ≤ 12) | 56.5 | 10.9 | find the period, break each column | instant | 314 years |
| Autokey (primer ≤ 12) | 56.5 | 11.9 | find each primer letter's chain | instant | 314 years |
| Repeating-key XOR (16 bytes) | 128 | 14.0 | Hamming distance, then byte by byte | instant | 10²⁴ years |
| Simple substitution | 88.4 | 14.9 | hill climb on quadgrams | instant | 92 × the age of the universe |
| Polybius square | 83.7 | 14.9 | relabel, then as substitution | instant | 3 × the age of the universe |
| Columnar transposition (8 columns) | 15.3 | 15.3 | try every column order | instant | instant |
| Columnar transposition (15 columns) | 40.3 | 17.6 | hill climb on column moves | 20 ms\* | 36 hours |
| Playfair | 79.0 | ≈ 20 | simulated annealing | 100 ms\* | 2 billion years |
| **Enigma with a crib** | 76.5 | 24.7 | **Turing–Welchman bombe** | **2.7 seconds** | 340 million years |
| ADFGVX | 199 | ≈ 40 | needs several messages on one key | 28 hours\* | beyond counting |
| DES | 56 | 55 | brute force | 114 years | 228 years |
| Enigma *without* a crib | 76.5 | 75.5 | brute force | 170 million years | 340 million years |
| RSA-2048 | — | ≈ 117 | number field sieve | 10²⁰ years | — |
| AES-128 | 128 | 127 | brute force | 5 × 10²³ years | 10²⁴ years |
| AES-256 | 256 | 255 | brute force | 10⁶² years | 10⁶² years |
| One-time pad | grows with the message | — | **none possible** | never | never |

\* The effective-work figures are deliberately round, and the times assume the `Laptop` rate of a *cheap*
trial key. The hill climbs score the whole message for every trial, so in practice a 15-column
transposition or a Playfair grid takes a few seconds, not milliseconds — still comfortably inside the
line. ADFGVX has no automatic attack here at all; its figure is an estimate of the work a good one would
need.

Three things stand out.

1. **Everything classical falls in a few minutes, and almost everything falls in under a second** —
   despite key spaces up to 88 bits. In every case there is a way to split the search (letters one at a
   time, columns one at a time, rows one at a time) or to climb towards the answer gradually.
2. **Enigma falls in seconds *with a crib* and never without one.** Same machine, same key space; the
   difference is entirely the few words of plaintext the code breakers expected. That one line is the
   story of Bletchley Park. (The 2.7 seconds is not just an estimate from the table: the library's
   bombe searched all 60 rotor orders for the [weather report](#breaking-enigma-the-bombe) in 2.7
   seconds.)
3. **Modern ciphers have no funnel.** For DES and AES the effective work is the key space (less one bit,
   for finding it halfway through on average). They are *designed* so that no insight splits the
   search. DES fell only because 56 bits was too few; AES-128 would outlast the universe even against
   every Bitcoin miner on Earth.

```csharp
foreach (CipherProfile p in CipherCatalogue.BreakableInFewMinutes(Attacker.Laptop))
    Console.WriteLine($"{p.Name}: {p.BestAttack(Attacker.Laptop)}");
```

Change the attacker and the line moves. Against `Attacker.PencilAndPaper`, Vigenère and substitution
take hours of patient work — which is about right historically — and the bombe's search would take a
person longer than a lifetime, which is why Bletchley built machines.

---

## How known information shrinks the search

The ladder above assumes the code breaker knows only the ciphertext and the language. In practice
they nearly always know more, and every scrap shrinks the search. These are the kinds of information
that matter, roughly in order of how often they are available:

### 1. The language itself — always available

The plaintext is (probably) English, or German, or whatever the context says. That alone gives letter
frequencies, common pairs and fours of letters, and a word list. It is what makes "does this decryption
look right?" a question a computer can answer, and it is behind every attack in the library. See
[teaching a computer what English looks like](#teaching-a-computer-what-english-looks-like).

### 2. The kind of cipher — usually deducible

Statistics alone usually say what *kind* of cipher you face (see the
[first look](#the-first-look-what-kind-of-cipher-is-it)), and that alone throws away every other
kind's keys.

### 3. Cribs — words the context suggests

A weather station sends a weather report; an army unit reports "nothing special to report" (KEINE
BESONDEREN EREIGNISSE) every quiet night; a letter starts DEAR; a signal mentions the name of the
place it concerns. Give the attacks those words through `AttackOptions.Cribs` and they use them to
discard keys:

```csharp
var options = new AttackOptions { Cribs = ["BRIDGE"] };
BreakResult r = CaesarAttack.Break(new CaesarCipher(11).Encrypt("Meet me by the old bridge"), options);
```

```
Start: 26 shifts                                    26 keys (4.7 bits)
  → Plaintext must contain BRIDGE                   1 keys (0.0 bits)
```

For a Caesar cipher that is trivial. For a simple substitution it is a big deal: a crib can only sit
where the ciphertext's letters repeat in the same pattern as the crib's (LETTER is `ABCCBD` — the third
and fourth letters match, and the second matches the fifth). In the Babington example LETTER fits at
only 3 of 369 positions, and once placed it pins its letters in the key:

```
Start: 26! orderings of the alphabet                ≈ 4.03 × 10^26 keys (88.4 bits)
  → Cribs fix 7 letters of the key                  ≈ 2.43 × 10^17 keys (57.8 bits)
  → Hill climb: 8 restart(s) × ~642 trial keys      1 keys (0.0 bits)
```

Thirty bits gone from two short words. For Enigma, cribs are not a help but the *whole method* —
without one, the bombe has nothing to reason from.

### 4. Known plaintext — a stretch of message and its ciphertext together

Stronger than a crib, because you know *where* it goes. For the Hill cipher four letters of known
plaintext give the key outright by algebra (`HillAttack.FromKnownPlaintext`). For a one-time pad,
known plaintext reveals that stretch of pad, which is useless — unless the pad was used again.

### 5. Depth — two messages on the same key

The most valuable mistake of all. Two messages enciphered with the same key let the key be cancelled
out entirely. It broke the Lorenz cipher in 1941 (an operator re-sent a long message with the same
settings) and Soviet one-time pads in VENONA. See [the pad used twice](#the-one-time-pad-and-the-pad-used-twice).

### 6. Habits and procedures — the human factor

Enigma operators chose start positions like AAA or keyboard runs like QWE ("cillies"); a ring setting
could be guessed from the first message of the day (the "Herivel tip"); the RAF laid mines in
particular places so that German warnings would contain known place names ("gardening"). None of this
is mathematics, and all of it shrinks the search. The modern equivalent is the [password a person
chose](#passwords-and-pins-the-key-not-the-cipher).

---

## When no amount of computing will do: unicity distance

The ladder answers "how long?". There is a deeper question: is there enough ciphertext for a unique
answer to *exist*?

Take a Caesar cipher of the single letter `K`. All 26 shifts give a letter, and every one is equally
plausible. Two letters (`KH`) still allow several real words (HE, BE, OR…). Somewhere around a
sentence, only one key gives anything readable. Claude Shannon worked out (in 1949) roughly where:

> **unicity distance ≈ key-space bits ÷ redundancy of the language**

**Redundancy** is the part of each letter that is predictable. A letter could carry log₂ 26 ≈ 4.7 bits,
but real English carries only about 1.5 — the rest is spelling, grammar and sense — so English has
about 3.2 bits of redundancy per letter (`KeySpace.EnglishRedundancy`). Each letter of ciphertext gives
the code breaker about 3.2 bits of evidence against wrong keys.

```csharp
CaesarCipher.KeySpace.UnicityDistance()          // ≈ 1.5 letters
SubstitutionCipher.KeySpace.UnicityDistance()    // ≈ 28 letters
EnigmaMachine.KeySpaceFor().UnicityDistance()    // ≈ 24 letters
```

This is the honest boundary on what can be broken:

- **Below the unicity distance, no computer can help.** A 15-letter substitution cipher has many
  readable solutions; choosing between them is guessing.
- **Above it, the answer is unique** and only the work stands in the way — which is where the ladder
  takes over. In practice the attacks here want several times the unicity distance: the hill climbs
  are reliable at a hundred or two letters, not 28.
- **The one-time pad never gets there.** Its key grows with the message, so there are always enough
  keys to explain any ciphertext as any message of the same length. That is Shannon's proof that it is
  unbreakable.

---

## Teaching a computer what English looks like

Every automatic attack needs to answer one question millions of times: *does this decryption look
like English?* `LanguageModel` is the answer, made into numbers. `LanguageModel.English` is counted
from about two million letters of public-domain novels (Conan Doyle, Austen, Dickens, Wells and
Buchan, via Project Gutenberg); `LanguageModel.German` from about a million letters of Goethe, Kafka,
Schiller and Fontane, with umlauts written AE, OE, UE and ß as SS, as Enigma operators keyed them.
The tables are built by `tools/build_language_data.py` and embedded in the assembly.

### Letters

The share of each letter, `LetterFrequencies`. From the English corpus, as percentages:

```
E 12.5  T 9.0  A 8.1  O 7.6  N 7.0  I 6.9  H 6.5  S 6.2  R 5.9  D 4.5  L 3.9  U 2.8  M 2.7
C 2.4  W 2.4  F 2.2  Y 2.1  G 2.0  P 1.6  B 1.5  V 1.0  K 0.8  J 0.1  X 0.1  Q 0.1  Z 0.1
```

Single letters are what a person uses first, and what the chi-squared test and the index of
coincidence measure. But they are a weak judge: "EEETTTAAA" has wonderful letter frequencies.

### Quadgrams

The workhorse is the **quadgram**: a run of four letters. To score a candidate decryption, slide a
four-letter window along it and add up how likely each window is in English. `THER` is the commonest
(log₁₀ probability −2.44); `QZJX` has never been seen and gets the "floor" of −8.30.

Two details matter:

- **Logarithms, added.** The likelihood of a whole text is the *product* of thousands of small
  probabilities, which underflows to zero in a computer. Adding their logarithms gives the same ranking
  without the underflow.
- **A floor, not zero.** A quadgram never seen in the corpus is rare, not impossible. Giving it a tiny
  probability (0.01 occurrences) rather than zero means one unusual name doesn't wreck the score of an
  otherwise perfect decryption.

The beauty of quadgram scoring is that it is **graded**. Correct English scores about −4.3 per window;
the Caesar ciphertext above scores −7.9. And a decryption that is *nearly* right — most letters
correct — scores nearly as well as the right answer. That is what allows a search to *climb* towards
the answer, one small improvement at a time, instead of having to hit it exactly. Every hill climb in
the library depends on it.

```csharp
LanguageModel en = LanguageModel.English;
en.QuadgramScorePerLetter("THE ENEMY IS GATHERING NEAR THE RIVER");   // ≈ -4.3: English
en.QuadgramScorePerLetter("WKH HQHPB LV JDWKHULQJ QHDU WKH ULYHU");   // ≈ -7.9: not
```

`BreakResult.EnglishLikeness` reports this per-window score for every attack, and
`BreakResult.LooksLikeEnglish` is true above −5.1.

### Words

`Words` is the corpus's vocabulary, commonest first, and `Segment` uses it to put the spaces back into
a decryption, choosing the most probable split by dynamic programming:

```csharp
en.Segment("ATTACKATDAWN")   // "ATTACK AT DAWN"
```

It is purely cosmetic, but it makes a result far easier to read.

### Why the language matters

An English model judges a correct German decryption as gibberish. When the bombe below recovered a
German weather report, the plugboard completion only worked once it was given `LanguageModel.German`.
Set `AttackOptions.Language` to match the traffic.

---

## The first look: what kind of cipher is it?

Before any key is guessed, statistics usually reveal what kind of cipher you are facing. The library
gathers the classic measurements in a few static classes.

### Counting letters — `FrequencyAnalysis`

The oldest tool there is, described by the Arab polymath **al-Kindi** in the ninth century. A cipher
that swaps each letter for another (a *monoalphabetic substitution*) changes the letters' names but not
how often they turn up, so the commonest ciphertext letter is probably E.

- `Counts`, `Frequencies`, `Ranked` — the tally, and the table a code breaker writes first.
- `ChiSquared` — Σ (observed − expected)² ÷ expected against English letter frequencies. Zero is a
  perfect match. This breaks a Caesar cipher in one line: try 26 shifts, keep the lowest.
- `ChiSquaredPValue` — the same as a probability, via the chi-squared distribution: near 1 is "very
  English", near 0 is "certainly not plain English letters".
- `Entropy` — bits per letter. Random letters 4.70; English letter frequencies about 4.17.
- `NGrams` — the commonest pairs and triples (TH, HE, IN; THE, AND, ING).
- `FrequencyMatchedKey` — a first-guess substitution key by frequency rank alone.

### The index of coincidence

William Friedman's great discovery (1922). The **index of coincidence** (IoC) is the chance that two
letters picked at random from a text are the same letter:

> IoC = Σ nᵢ(nᵢ − 1) ÷ N(N − 1)

where nᵢ is how often letter *i* appears and N is the total. English is lumpy — lots of E, few Z — so
its IoC is about **0.066** (0.0655 in our corpus; German 0.074). Perfectly random letters give **1/26 ≈
0.0385**. And the key property: **any cipher that only relabels letters leaves the IoC unchanged**,
because the IoC doesn't care which letter is which, only how lumpy the counts are. So:

- IoC near 0.066 → one alphabet: a substitution, or a transposition (letters merely moved).
- IoC near 0.04 → many alphabets (Vigenère, Enigma) or none at all (a one-time pad).

### The key length — `Periodicity`

For a Vigenère-type cipher the next question is the length of the key, and there are two classic
methods.

**Kasiski (1863).** When a common word lines up with the same part of the key twice, it encrypts the
same way twice, and the distance between the repeats is a multiple of the key length. `Repeats` finds
them. In the indéchiffrable example (key length 7) they include:

```
RKTFAUTA  105 apart  = 7 × 15
ZLFKFZ     49 apart  = 7 × 7
YXIE       98 and 126 apart = 7 × 14, 7 × 18
UHJ        28 apart  = 7 × 4
GCF        81 apart  — a coincidence (not a multiple of 7)
```

The coincidences are why Kasiski is a *vote* (`KasiskiVotes`), not a proof: 7 collects 13 votes, more
than any other length.

**The index of coincidence by column.** Guess a length L, cut the ciphertext into L columns (every
L-th letter), and measure each column's IoC. With the right L each column is a Caesar cipher of
English and scores like English; with the wrong L the columns are a mixture and score near random.
For the same example:

```
period  2: 0.0506    period  6: 0.0487    period 10: 0.0480
period  3: 0.0490    period  7: 0.0758 ←  period 12: 0.0522
period  4: 0.0516    period  8: 0.0524    period 14: 0.0802  (a multiple of 7)
period  5: 0.0494    period  9: 0.0486
```

Multiples of the true period score just as well (their columns are also single shifts), so
`RankPeriods` prefers the *smallest* period whose IoC is close to the best — otherwise the attack would
cheerfully choose a key twice as long as necessary.

**Friedman's estimate** (`FriedmanEstimate`) gets a length from the overall IoC alone, with no guessing.
It is famously rough, and this example shows why: it says about 2.4 for a key of 7, because this
particular plaintext happens to be lumpier than average English. Use it as a hint and the column test
as the evidence.

### Letter patterns — `WordPatterns`

A substitution cipher changes every letter but cannot change a word's *pattern*: THAT is `ABCA`, and
whatever it encrypts to will have matching first and last letters. `Pattern("WETTERBERICHT")` is
`ABCCBDEBDFGHC`; `Matching("XQPX")` lists the English words that fit; `PlacementsForSubstitution` finds
where a crib can sit. This is how pattern-word dictionaries solve newspaper cryptograms.

### Putting it together — `CipherIdentifier`

`CipherIdentifier.Identify` makes the first look automatic and explains itself:

```mermaid
graph TD
    S["Ciphertext"] --> D{"Only digits 1–5?"}
    D -->|yes| P["Polybius square"]
    D -->|no| A{"Only A D F G V X?"}
    A -->|yes| X["ADFGVX"]
    A -->|no| I{"Index of coincidence<br/>near English (≈0.066)?"}
    I -->|yes| C{"Letter frequencies<br/>already English?"}
    C -->|yes| T["Transposition<br/>(letters moved, not changed)"]
    C -->|"no, but one shift fixes them"| K["Caesar / affine"]
    C -->|no| M["Simple substitution"]
    I -->|"no (flatter)"| R{"Some period gives<br/>English-like columns?"}
    R -->|yes| V["Vigenère-type"]
    R -->|"no, IoC ≈ 0.04–0.05"| H["Hill / Playfair / Bifid<br/>or autokey"]
    R -->|"no, IoC ≈ 0.038"| O["Random-looking: one-time pad,<br/>Enigma or modern cipher"]
```

A Playfair check runs alongside: even length, no J, and no pair that is a double letter is exactly
what Playfair produces. `AutoBreaker.BreakAsync` goes one step further — identify, run the matching
attacks, return the result that reads most like English.

---

## The ciphers, one by one

Each section follows the same shape: **how it works**, **the key space**, **how it is broken**, **can
it be broken in a few minutes?**, and **in code**. Times are for one laptop core.

### Caesar, ROT13 and Atbash

**How it works.** Shift every letter a fixed number of places along the alphabet, wrapping Z round to
A. With a shift of 3, A → D, B → E, X → A. Treat letters as numbers (A = 0 … Z = 25) and it is
c = (p + k) mod 26 — "mod 26" meaning the remainder after dividing by 26, the arithmetic of a clock with
26 hours. Suetonius tells us Julius Caesar used a shift of 3. **ROT13** is a shift of 13, its own
inverse, used on Usenet to hide punchlines. **Atbash** reverses the alphabet (A ↔ Z) and appears in the
Hebrew Bible.

**Key space.** 26 shifts (Atbash: 1).

**How it is broken.** Try all 26 and keep the one that reads (`CaesarAttack.Break`), or — the way you
would by hand — keep the shift whose letter frequencies best match English
(`CaesarAttack.ShiftByFrequency`).

**A few minutes?** Under a millisecond.

```csharp
BreakResult r = CaesarAttack.Break("Wkh ohjlrqv zloo furvv wkh ulyhu dw iluvw oljkw.");
// r.Key == "shift 3 (A→D)", r.Plaintext == "The legions will cross the river at first light."
```

### Affine

**How it works.** Multiply, then shift: c = (a·p + b) mod 26. Caesar is the case a = 1.

**Key space.** Decryption must undo the multiplication, which needs the *inverse* of a modulo 26 — a
number that multiplied by a gives 1 more than a multiple of 26 (3 × 9 = 27, so 9 undoes 3). It exists
only when a shares no factor with 26 = 2 × 13, which leaves 12 multipliers. 12 × 26 = 312 keys.
`NumberTheory.ModInverse` finds inverses with the extended Euclidean algorithm.

**How it is broken / a few minutes?** Try all 312 (`AffineAttack`). Instant.

### Simple substitution

**How it works.** Any rearrangement of the alphabet. `SubstitutionCipher.FromKeyword("ZEBRAS")` uses
the classic keyword alphabet `ZEBRASCDFGHIJKLMNOPQTUVWXY`.

**Key space.** 26! ≈ 4.03 × 10²⁶ (88.4 bits). At ten million keys a second, trying them all would take
92 times the age of the universe.

**How it is broken — hill climbing.** The key isn't a single lock that is either open or shut. A key
with twenty letters right produces text that is *mostly* readable, and the quadgram score shows it. So
`SubstitutionAttack` does what you would do with a pencil:

1. Start from a guess — first the frequency-matched key (commonest ciphertext letter = E, and so on).
2. Swap two letters of the key. If the decryption scores better, keep the swap; if not, undo it.
3. Repeat until no swap helps.

```
score
  ▲                    ╭──╮ the answer
  │        ╭─╮        ╭╯  ╰╮
  │   ╭──╮╭╯ ╰╮  ╭───╯     ╰─╮
  │ ╭─╯  ╰╯   ╰──╯           ╰──     each step: keep a swap only if it climbs
  └──────────────────────────────▶ keys
       ↑ a "foothill": no single swap improves it, but it isn't the answer
```

A climb can stop on a foothill, so the attack **restarts** several times from random keys and keeps the
best. On the 374-letter Babington message it tried 13,333 keys — about one in 3 × 10²² of the key space —
in 69 milliseconds, and read it perfectly. With cribs the letters they pin are never swapped (see
[known information](#3-cribs--words-the-context-suggests)).

**A few minutes?** Well under a second, given a hundred or two letters. Below about 28 letters — the
[unicity distance](#when-no-amount-of-computing-will-do-unicity-distance) — no method can be sure.

### Polybius square

**How it works.** Each letter becomes its row and column in a 5 × 5 grid (I and J share a cell): H in
the plain grid is `23`. Polybius described it for signalling with torches.

**Key space.** 25! grids (83.7 bits).

**How it is broken.** It is a simple substitution wearing digits. Relabel each digit pair as a letter
(11 → A, 12 → B…) and hand it to the substitution attack (`PolybiusAttack`). A lovely demonstration that
**changing the symbols adds no security**. Its value is as a building block for Bifid and ADFGVX, which
split the coordinates up.

### Vigenère, Beaufort and the keyed Vigenère

**How it works.** A Caesar shift that changes with every letter, following a repeating keyword. With
the key LEMON, `ATTACKATDAWN` encrypts to `LXFOPVEFRNHR`: the 1st, 6th, 11th… letters are shifted by
L, the 2nd, 7th, 12th… by E, and so on. Described by Giovan Battista Bellaso in 1553 (and named after
Blaise de Vigenère, who described a stronger relative), it was called *le chiffre indéchiffrable* for
three hundred years, because it flattens the letter frequencies that betray a substitution.

- **Beaufort** subtracts instead, c = k − p, so encryption and decryption are the same operation — one
  table or one machine does both, which is why the US Army's M-209 used it.
- **The keyed Vigenère (Quagga)** builds the table on a mixed alphabet. Jim Sanborn's Kryptos sculpture
  at the CIA uses the KRYPTOS alphabet: `new VigenereCipher("PALIMPSEST", "KRYPTOS")` decrypts the
  real K1 panel to BETWEEN SUBTLE SHADING AND THE ABSENCE OF LIGHT LIES THE NUANCE OF IQLUSION (the
  misspelling is Sanborn's).

**Key space.** 26ᴸ for a key of length L; every key up to 12 letters is 9.92 × 10¹⁶ (56.5 bits).

**How it is broken.** Charles Babbage in the 1850s and Friedrich Kasiski in 1863: find the key length
([Kasiski or the IoC](#the-key-length--periodicity)), cut the text into that many columns, and break
each column as a Caesar cipher. `VigenereAttack` does exactly that, then polishes each key letter
against the full-text quadgram score (which rescues short texts where a column has too few letters for
frequencies to be reliable). It is the clearest example of the [product becoming a
sum](#why-the-key-space-is-not-the-difficulty): 26⁷ becomes 7 × 26.

**Crib dragging.** Subtract a known word from the ciphertext and you get the key letters underneath it.
Keys were often words themselves, so sliding a crib along and reading the fragments (`DragCrib`) shows
the right position by eye. With a crib, the attack tries every period and keeps the one where the crib
gives a self-consistent key — enough to break a 42-letter message whose statistics alone say nothing.

**A few minutes?** About 30 milliseconds for the 327-letter example.

### Autokey

**How it works.** A short primer starts the key, and then the *message itself* continues it. With
primer QUEEN, `ATTACKATDAWN` is enciphered with the key `QUEENATTACKA`. The key never repeats, so
Kasiski and the column IoC find nothing.

**How it is broken.** Only the primer is secret. With a primer of length L, plaintext letter i + L is
decrypted using plaintext letter i, so primer letter *j* alone decides plaintext letters j, j + L,
j + 2L… — a chain. Each chain can be scored on its own by letter frequencies, so an L-letter primer costs
26 × L trials. `AutokeyAttack` tries each length and keeps the best. The same "make it a sum" trick as
Vigenère, reached by a different road. Instant.

### Rail fence

**How it works.** Write the message in a zigzag across a number of rails and read each rail off. With
three rails `WE ARE DISCOVERED FLEE AT ONCE` becomes `WECRLTEERDSOEEFEAOCAIVDEN`. A
**transposition**: letters are moved, not changed — so the letter frequencies are exactly English,
which is how you recognise one.

**How it is broken.** The key is one small number; try them all (`RailFenceAttack`). Instant.

### Columnar transposition

**How it works.** Write the message in rows under a keyword and read the columns off in the keyword's
alphabetical order. ZEBRAS reads the column under A first, then B, E, R, S and Z
(`OrderFromKeyword("ZEBRAS")` is `[4, 2, 1, 3, 5, 0]`). The last row is left short when the message
doesn't fill it, which is harder to attack than padding.

**Key space.** k! orders for k columns: 720 for six, 40,320 for eight, 1.3 × 10¹² for fifteen.

**How it is broken.** `ColumnarAttack` tries every order for up to eight columns. Beyond that it hill
climbs, with two kinds of move — swapping two columns, and lifting one column out and putting it
somewhere else (so a run of columns already in the right order can slide into place together) — and
an **iterated local search**: when the climb stops, it "kicks" the best order (a couple of random
changes) and climbs again, keeping the kick only if it leads higher. Plain restarts throw away a nearly
right order; kicks keep most of it. A sixteen-column key falls in a few seconds.

**A few minutes?** Yes, for keys into the teens. Double transposition (two in a row, as SOE agents used)
is far harder and not attempted here.

### Playfair

**How it works.** Encrypt *pairs* of letters using a 5 × 5 keyword grid (Charles Wheatstone, 1854,
promoted by Lord Playfair; used in the Boer War, the First World War, and by Australian coastwatchers in
the Second). For each pair: same row → take the letters to the right; same column → take the letters
below; otherwise the pair marks opposite corners of a rectangle, so take the other two corners.
Doubled letters are split with an X (BALLOON → BA LX LO ON).

**Key space.** 25! grids, but each grid can be rotated through its rows and columns without changing the
cipher, so 24! ≈ 6.2 × 10²³ (79 bits).

**How it is broken — simulated annealing.** Playfair's landscape is rougher than a substitution's:
swapping two letters in the grid changes every pair that touches either of them, and a nearly right
grid can still produce nonsense. A hill climb gets stuck almost every time. `PlayfairAttack` uses
**simulated annealing**, borrowed from metalwork: a hill climb that is sometimes allowed to step
*downhill*. A change that worsens the score by d is accepted with probability e^(−d/T), where T is a
"temperature" that falls steadily. Hot, the search roams widely and can escape dead ends; cool, it
settles into the deepest valley it has found. Moves are those that suit a grid: mostly swapping two
letters, sometimes swapping rows or columns or reversing the grid.

**A few minutes?** Yes, but this is the hardest classical cipher in the library. On a 327-letter
message it needs roughly 0.5–1.5 million trial grids — a few seconds — and some runs still end in a dead
end, which is why it restarts. Short messages (the 100-letter PT-109 example) are much harder.

### Hill

**How it works.** Lester Hill, 1929: treat each block of n letters as a column of numbers and multiply
it by an n × n key matrix, modulo 26. With key [[3, 3], [2, 5]], HE = (7, 4) becomes (3·7 + 3·4,
2·7 + 5·4) = (33, 34) ≡ (7, 8) = HI. Decryption multiplies by the inverse matrix modulo 26
(`NumberTheory.MatrixInverse`), which exists only when the determinant shares no factor with 26.

**Key space.** Of the 26⁴ = 456,976 possible 2 × 2 matrices only 157,248 are invertible (17.3 bits).
(The test suite checks that figure by counting.)

**How it is broken.** Two ways, both short:

- **Known plaintext — algebra.** C = K·P, so K = C·P⁻¹. Four letters of crib give a 2 × 2 key outright
  (`HillAttack.FromKnownPlaintext`). Being *linear* is the Hill cipher's downfall.
- **Ciphertext only — one row at a time.** In P = D·C, the first row of the decryption matrix D makes
  the first letter of every pair and nothing else. So each row is found separately by trying its 26² =
  676 values and asking which gives English letter frequencies; then the best rows are paired up and
  scored on quadgrams. 2 × 676 = 1,352 trials, not 157,248 — the product-into-sum trick once more.

Instant either way.

### Bifid and ADFGVX

These two are here to show where the automatic methods stop.

**Bifid** (Félix Delastelle, 1895) writes each letter's row and column from a Polybius grid one above
the other, reads the digits off row-wise in blocks, and turns them back into letters. Each ciphertext
letter then depends on *two* plaintext letters — *fractionation* — which smears the statistics far more
than a substitution.

**ADFGVX** (Fritz Nebel, German army, 1918) puts letters and digits in a 6 × 6 square labelled A D F G V
X — letters chosen because they sound unlike in Morse — then scrambles the resulting letter pairs with a
columnar transposition. Substitution *and* transposition, so each ciphertext letter is half a symbol,
moved away from its partner, and no single statistic gives a hill to climb.

Both can be made and decrypted here (`BifidCipher`, `AdfgvxCipher`) but neither has an automatic attack.
Georges Painvin broke ADFGVX in June 1918 by finding two messages with similar beginnings on the same
key — [depth](#5-depth--two-messages-on-the-same-key) again — at a cost of days of work and much of his
health. The catalogue estimates the ciphertext-only work at around 2⁴⁰: a day or more for a laptop with
a good method, and a good method is a research project. See [future
considerations](#future-considerations).

### The one-time pad, and the pad used twice

**How it works.** A Vigenère cipher whose key is truly random letters, as long as the message, and
never used again (`OneTimePad.Generate` uses a cryptographic random generator).

**Key space.** 26ⁿ for an n-letter message — and, uniquely, **no way to tell the right key from the
wrong ones**. Every plaintext of the right length is produced by some pad, so trying keys gives you
every possible message and no reason to prefer one. Shannon proved it in 1949: the one-time pad is
perfectly secure. Its unicity distance is never reached.

**Unless it is used twice.** If c₁ = p₁ + k and c₂ = p₂ + k with the same pad k, then c₁ − c₂ = p₁ − p₂.
The pad has vanished; what is left is two English texts tangled together. Guess a word in one
(a crib), subtract it, and the other message's letters at that position fall out — English if the guess
was right, gibberish if not. `TwoTimePadAttack.DragCrib` slides the crib along and ranks the
positions. This is how **VENONA** worked: Soviet pads duplicated under wartime pressure were read for
decades.

### Repeating-key XOR

**How it works.** The computer-age Vigenère: combine each byte with the next byte of a short repeating
key using exclusive-or (XOR, "one or the other but not both"). XOR undoes itself, so the same operation
decrypts.

**How it is broken.** Exactly as Vigenère, moved from letters to bytes (`XorAttack`). The key length is
found by **Hamming distance**: XOR two blocks one key-length apart and the key cancels, leaving
plaintext ⊕ plaintext, which differs in fewer bits than random bytes do. Then every L-th byte shares a
key byte, found by trying all 256 and keeping the one that produces the most English-like characters.
Multiples of the key length look just as good on Hamming distance, so the attack prefers the shortest
key whose decryption scores within a whisker of the best. Instant — `"SECRET"` falls in 13 ms.

### Enigma

**How it works.** The German Enigma machine (`EnigmaMachine`, the army and air force's three-rotor
Enigma I). Pressing a key sends a current through:

```
key → plugboard → right rotor → middle → left → reflector
                                                   ↓
lamp ← plugboard ← right rotor ← middle ← left ←───┘
```

- **Rotors** (`EnigmaRotor.I` … `VIII`, with their historical wirings): discs of 26 contacts each side,
  wired in a scrambled order. The right rotor steps on **every** key press, so every letter is
  enciphered by a different alphabet.
- **Stepping**: when the right rotor passes its notch it kicks the middle rotor on; when the middle rotor
  reaches its own notch, it moves again on the next press *and takes the left rotor with it* — the
  famous **double step**. From ADU the windows read ADV, AEW, BFX (`PositionsAfter` shows it; the test
  suite checks it).
- **Ring settings** turn the alphabet ring relative to the wiring, which changes when the notch acts.
- **The reflector** (`EnigmaReflector.B`) sends the current back by a different path. It makes the
  machine its own inverse — the same settings decrypt — and it means **no letter ever encrypts to
  itself**. That small flaw is at the heart of everything below.
- **The plugboard** (Steckerbrett) swaps letters in pairs before and after the rotors.

```csharp
var settings = EnigmaSettings.Parse("I II III", "B", rings: "AAA", positions: "AAA");
new EnigmaMachine(settings).Encrypt("AAAAA");   // "BDZGO" — the standard test vector
```

**Key space.** `EnigmaMachine.KeySpaceFor()` multiplies it out:

| Part | Choices |
|---|---:|
| Rotor order: 3 of 5 rotors, in order | 60 |
| Start positions | 26³ = 17,576 |
| Ring settings (the left ring changes nothing a start position couldn't) | 26² = 676 |
| Plugboard, 10 cables: 26! ÷ (6! · 10! · 2¹⁰) | 150,738,274,937,250 |
| **Total** | **≈ 1.07 × 10²³ (76.5 bits)** |

The navy's eight-rotor version (`KeySpaceFor(8)`) has 336 rotor orders, ≈ 6 × 10²³. Notice where the
size lives: **almost all of it is the plugboard**. The rotors and positions are "only" about a million
combinations. That observation is where the attack begins.

---

## Breaking Enigma: the bombe

The Polish Cipher Bureau — Marian Rejewski, Jerzy Różycki and Henryk Zygalski — broke Enigma first, in
1932, and in July 1939 handed everything they knew to the British and French. When the Germans tightened
their procedures, Alan Turing designed a new machine around a different idea, and Gordon Welchman made
it decisive. The British bombe (named in honour of the Polish *bomba*) went into service in 1940; by the
end of the war over two hundred were running.

`EnigmaBombe` is that machine, in software.

### Step 1 — place the crib

You need a crib: plaintext you are confident is in the message, and roughly where. German weather
stations sent reports every morning beginning WETTERBERICHT. Because **Enigma never encrypts a letter to
itself**, the crib can only sit where none of its letters lies over the same ciphertext letter:

```
ciphertext  M I C I D E T T U B T S W I K V ...
crib        W E T T E R B E R I C H T          ← no letter matches: possible
```

`PossibleCribPositions` applies the rule. For the [weather report example](#famous-cases) it rules out
28 of the 82 places the crib could go, leaving 54.

### Step 2 — build the menu

Lay the crib against the ciphertext and read it as a graph: each crib letter is joined to the
ciphertext letter under it, labelled with its position. `BuildMenu` does this and counts its **loops**
— chains of letters that come back to where they started:

```
W—M(0)  E—I(1)  T—C(2)  T—I(3)  E—D(4)  R—E(5)  B—T(6)  E—T(7)  R—U(8)  I—B(9)  C—T(10)  H—S(11)  T—W(12)
```

Drawn as a graph (the demo site draws it for any crib):

```
              M
              │ 0
              W
              │ 12
    C ══ 2,10 ══ T ─── 6 ─── B
                ╱ ╲          │
             7 ╱   ╲ 3       │ 9
              ╱     ╲        │
             E ── 1 ── I ────┘
            ╱ ╲
         5 ╱   ╲ 4
          R     D                H ── 11 ── S
          │ 8
          U
```

There are three **loops** — chains of links that come back to where they started: E–I–T (positions 1,
3, 7), T–C–T (2 and 10, the same two letters linked twice) and T–B–I–T (6, 9, 3). **Loops are what make
the bombe work**, as the next step shows. This menu has 12 letters and 3 loops.

### Step 3 — reason instead of searching

Write S for the plugboard and Eᵢ for the rotors and reflector at crib position *i*. A crib letter p
enciphering to c at position *i* means c = S(Eᵢ(S(p))) — and because the plugboard swaps in pairs,
that is S(c) = Eᵢ(S(p)).

Now **suppose** the plugboard partner of the commonest menu letter, T. Say S(T) = A. Every link touching
T now *tells you* another partner: T—C at position 2 says S(C) = E₂(A). That tells you more, and so on
around the menu. If the guess was wrong, the chain soon demands that some letter has two different
partners — a **contradiction** — and the guess dies. Try all 26 guesses for S(T). If every one
contradicts itself, **this rotor position is impossible — for every one of the 150 million million
plugboard settings at once**. The loops make the contradictions come fast.

Welchman's **diagonal board** added the observation that plugging is symmetric: if S(T) = A then S(A) =
T. That doubles the deductions and slashes the false stops. `EnigmaBombe.Deduce` applies it on every
step.

So the bombe never searches the plugboard at all. It searches only rotor orders and positions — and
because it tests the rotors' *cores*, the ring settings drop out too:

```
Start: 60 rotor orders × 26³ × 26² × plugboard        ≈ 1.07 × 10^23 keys (76.5 bits)
  → Plugboard handled by deduction, rings drop out     1,054,560 keys (20.0 bits)
  → Bombe stops                                        1 keys
  → Checking machine                                   1 keys
```

Fifty-six bits removed by logic.

### Step 4 — the checking machine

Where some guess survives, the bombe **stops**. A stop is a candidate rotor order and position, plus the
plug pairs the surviving guess implied. Bletchley's checkers then tried it on a real Enigma. The software
version tries the 26 settings of the right-hand ring (which decide when the middle rotor steps), keeps
one under which the whole crib enciphers correctly, and completes the plugboard by hill climbing — adding
whichever remaining cable makes the full decryption most like the language.

### The result

```csharp
FamousCase c = FamousCases.WeatherReport;
BombeResult r = await EnigmaBombe.RunAsync(c.Ciphertext, "WETTERBERICHT", position: 0,
    options: new AttackOptions { Language = LanguageModel.German });
```

The library's own run, all five rotors:

```
No letter enciphers to itself: the crib WETTERBERICHT can sit at 54 of 82 positions.
Menu WETTERBERICHT over MICIDETTUBTSW at 0: 12 letters, 3 loop(s), test letter T.
1 stop(s) from 1,054,560 rotor positions.
Stop II IV I at FEW: AT BS DE FM IR OW checks out with right ring F, start FEA

II IV I / UKW-B / rings AAF / start FEA / plugs AT BS DE FM IR KN LZ OW PV XY
WETTERBERICHTFUERDIENORDSEEXWINDAUSWESTSUEDWESTSTAERKEFUENFXSICHTGUTXREGENSCHAUERAMNACHMITTAGX
```

*Wetterbericht für die Nordsee: Wind aus Westsüdwest, Stärke fünf; Sicht gut; Regenschauer am
Nachmittag.* A million rotor positions, one stop, 2.7 seconds. The bombe found six of the ten cables by
pure deduction; the checker found the other four.

### What can go wrong — and did

- **Few loops, many stops.** Without loops, a wrong guess can only be caught by the symmetry of the
  plugboard, so many survive. The same weather report started from DQX instead of FEA gives a menu with
  *no* loops: 56 stops instead of one, each of which the checker must work through (it still finds the
  answer, in a second). Shorter and loop-free is hopeless: the half-crib WETTER on its own made 130,004
  stops from 105,456 positions — more stops than positions, because several guesses survive at once.
  Bletchley's menu-makers chose cribs and positions for their loops, and so should you; the bombe logs a
  warning when a menu has none.
- **The middle rotor turning mid-crib.** The bombe assumes the middle rotor stays put while the crib is
  being enciphered. With a 13-letter crib that fails about half the time. If no stop survives checking,
  the library reruns each half of the crib separately (a crib shorter than 26 letters can contain at most
  one turnover, so one half is always clean) — but only halves with loops are worth running.
- **A wrong crib.** If WETTERBERICHT isn't where you put it, nothing checks out. Try the next possible
  position.

### Why it worked — and why it wouldn't now

Notice what the bombe exploited: a **property of the machine** (no letter to itself; plugging is
symmetric) and a **habit of the users** (routine messages in routine words). Neither is a matter of key
size. Enigma's 76 bits were never brute-forced; they were *reasoned around*. Modern ciphers are designed
so that no such reasoning exists, and modern procedures (random padding, no fixed formats) deny the cribs.

---

## Modern cryptography: where the shortcuts run out

Every classical attack above works by **splitting** the search — letters, columns, rows or rotor
positions found one at a time — or by **climbing** towards the answer because partly-right keys give
partly-right text. Modern ciphers are designed so that neither is possible: change one bit of the key
and every bit of the output changes, unpredictably, so a key that is 99% right decrypts to pure noise.
Then the key space *is* the work, and the only question is whether it is big enough.

### DES and AES — the key space is the work

DES (1977) had 56-bit keys: 7 × 10¹⁶. In 1998 the EFF's Deep Crack, built for $250,000, found a DES key
in 56 hours (`Attacker.DeepCrack`: the whole space in about nine days). That was the end of DES.

AES-128 has 2¹²⁸ ≈ 3.4 × 10³⁸ keys. At the rate of every Bitcoin miner on Earth combined,
`KeySpace.FromBits(128).ExhaustiveSearch(Attacker.WholeBitcoinNetwork)` is about ten billion years —
most of the age of the universe, for one key. AES-256 squares that. (A large quantum computer running
Grover's algorithm would, in principle, halve the bits: AES-256 would still be 128-bit strong.)

### RSA — the difficulty is factoring

`ToyRsa` is textbook RSA with deliberately small keys. Pick two primes p and q, publish n = p·q; anyone
can encrypt, but decrypting needs a number computed from p and q. So **the private key is exactly as safe
as n is hard to factor** — and `Factoring` shows how that difficulty grows:

| Method | Work for an n of b bits | 64-bit n | 100-bit n | 2048-bit n |
|---|---|---|---|---|
| `TrialDivision` (try every divisor up to √n) | 2^(b/2) | instant | years | never |
| `PollardRho` (a random walk that collides after about √p steps) | 2^(b/4) | instant | seconds | never |
| `Fermat` (only when p and q are close together) | tiny | instant | instant | instant — if the key is badly made |
| Number field sieve (the record-holder; estimate only) | ≈ e^(1.92 (ln n)^⅓ (ln ln n)^⅔) | — | — | ≈ 2¹¹⁷ |

An 80-bit modulus falls to Pollard's rho in about half a second (788,607 steps). The record factorisations
used the number field sieve: RSA-129 in 1994 ("The Magic Words are Squeamish Ossifrage"), the 512-bit
RSA-155 in 1999. `Factoring.EstimateNumberFieldSieveOperations(2048)` gives about 2¹¹⁷ — the reason
2048-bit keys are used today. Fermat's method is here as a cautionary tale: keys generated with p and q
too close together have been found in real devices, and fall instantly.

### Passwords and PINs — the key, not the cipher

`PasswordCracking` makes the most important point in modern code breaking. SHA-256 is unbroken: 2²⁵⁶
possible outputs, no known shortcut. And it does not save a four-digit PIN:

```csharp
CrackResult pin = PasswordCracking.CrackPin(PasswordCracking.Sha256Hex("4071"), digits: 4);
// "4071", after 4,072 guesses, in 2 milliseconds
```

The work is not the size of the hash; it is the size of the set **the secret was chosen from**. 10,000
PINs. A few thousand common words with a capital letter and a digit or two on the end
(`CrackWord`): five thousand words, three capitalisations and a hundred and two endings is about one and
a half million guesses — a fraction of a second. This is the modern crib — knowing
how *people* choose keys shrinks the search exactly as knowing a message says WETTERBERICHT did — and it
is why real systems use slow, salted password hashes (bcrypt, scrypt, Argon2) and limit guesses.

---

## Famous cases

`FamousCases.All` holds a message for each of these, to break with the library or on the demo site.
Where the historical ciphertext is public and short (Kryptos K1) it is used as it stands; otherwise the
message is new text in the spirit of the original, enciphered here, so it can be broken with the same
method that broke the real thing.

| Case | When | Cipher | How it fell | In the library |
|---|---|---|---|---|
| **Caesar's dispatch** | c. 55 BC | Caesar shift 3 | Try all 26 | `CaesarAttack`, instant |
| **Mary, Queen of Scots** — the Babington plot | 1586 | Nomenclator (substitution) | Thomas Phelippes, by letter frequencies; Mary was executed | `SubstitutionAttack`, 69 ms |
| **Le chiffre indéchiffrable** | 1553–1863 | Vigenère | Babbage and Kasiski: find the period | `VigenereAttack`, 32 ms |
| **The radiogram of victory** | 1918 | ADFGVX | Painvin, from two messages on one key | encode/decode only |
| **The Zimmermann telegram** | 1917 | Diplomatic codebook | Room 40 rebuilt the book from context | `Codebook` |
| **WETTERBERICHT** | 1940–45 | Enigma | The bombe, with a crib | `EnigmaBombe`, 0.6 s (rotors I, II, IV) / 2.7 s (all five) |
| **PT-109** | 1943 | Playfair | (sent, not broken — field-grade security) | `PlayfairAttack`, hard: only 100 letters |
| **VENONA** | 1943–80 | One-time pad, reused | Crib dragging across messages in depth | `TwoTimePadAttack` |
| **Kryptos K1** (real ciphertext) | 1990 | Keyed Vigenère | James Gillogly, 1999 (NSA in 1992) | `VigenereCipher("PALIMPSEST", "KRYPTOS")` |
| **Home-made XOR** | today | Repeating-key XOR | Hamming distance, then byte by byte | `XorAttack`, 13 ms |

A few more that the library tells the story of without a demo:

- **Lorenz (Tunny) and Colossus.** Hitler's high command used the Lorenz SZ40/42 teleprinter cipher,
  which added a stream of pseudo-random bits to each 5-bit ITA2 character (`Ita2Code` is that code). On 30
  August 1941 an operator re-sent a 4,000-character message with the same settings — depth. John Tiltman
  recovered the key stream; Bill Tutte deduced the entire machine from it without ever seeing one; and
  Tommy Flowers built Colossus, the first programmable electronic computer, to find the wheel settings.
- **The Zodiac Killer's ciphers** (1969–70) used *homophonic* substitution — several symbols per common
  letter, to flatten the frequencies. The 408-symbol cipher was solved by hand in a week; the
  340-symbol one held out until 2020, when a team broke it with a modern solver.
- **Naval Enigma** added rotors VI–VIII, then a fourth rotor in February 1942, blacking out U-boat traffic
  for ten months until captured material and cribs reopened it.

---

## Common situations, and what to do

**"I have a ciphertext and nothing else."** Run `CipherIdentifier.Identify` and read its reasons, or let
`AutoBreaker.BreakAsync` choose and run the attack. Check the length against the [unicity
distance](#when-no-amount-of-computing-will-do-unicity-distance): if you have fewer than a few dozen
letters, expect several plausible answers.

**"I think I know a word in it."** Put it in `AttackOptions.Cribs`. Every attack uses cribs to discard
keys; for Vigenère and Hill a crib can solve messages the statistics can't; for Enigma it is essential.
Choose long cribs with repeated letters — they fit in fewer places.

**"I have two messages that might share a key."** That is depth, the strongest position there is.
Subtract them (`TwoTimePadAttack.DragCrib`) and drag likely words.

**"It's short."** Short texts starve frequency analysis. Lean on cribs, raise `Restarts`, and accept that
below the unicity distance the answer may not be unique.

**"It isn't English."** Set `AttackOptions.Language` (English and German are built in; another language
needs its own tables from `tools/build_language_data.py`).

**"The attack returned rubbish."** Look at `BreakResult.Log` and `EnglishLikeness`. Below about −5.1 per
window the attack has not found English: try a different cipher family, a larger `MaxKeyLength`, more
`Restarts` (the climbs are randomised; another `Seed` may succeed), or a crib.

**"It's a modern cipher."** Don't attack the cipher. Attack the key: how was it chosen, and from how
large a set? That is where real systems fail.

---

## Using the library

```csharp
using RP.CodeBreaker;
using RP.CodeBreaker.Attacks;
using RP.CodeBreaker.Ciphers;

// Make some ciphertext.
string secret = new VigenereCipher("WINTER").Encrypt("It was the best of times, it was the worst of times…");

// What is it?
foreach (CipherGuess g in CipherIdentifier.Identify(secret))
    Console.WriteLine($"{g.Family} ({g.Confidence:P0}): {g.Reason}");

// Break it, with a word we expect.
BreakResult r = VigenereAttack.Break(secret, new AttackOptions { Cribs = ["TIMES"] });
Console.WriteLine(r.Key);                                   // WINTER
Console.WriteLine(LanguageModel.English.Segment(r.Plaintext));
Console.WriteLine(r.Difficulty);                            // the funnel
Console.WriteLine(r.Difficulty.RemainingWork(Attacker.PencilAndPaper));

// The long searches are async, and can report progress and be cancelled.
var options = new AttackOptions
{
    Progress = new Progress<AttackProgress>(p => Console.WriteLine($"{p.Fraction:P0} {p.Message}")),
    Cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token,
};
BreakResult p = await PlayfairAttack.BreakAsync(playfairText, options);
```

### The namespaces at a glance

| Namespace | What lives there |
|---|---|
| `RP.CodeBreaker` | `Letters`, `KeySpace`, `Attacker`, `WorkEstimate`, `Difficulty`, `ICipher`, `BreakResult`, `AttackOptions`, `LanguageModel`, `FrequencyAnalysis`, `Periodicity`, `WordPatterns`, `CipherIdentifier`, `NumberTheory`, `CipherCatalogue` |
| `RP.CodeBreaker.Ciphers` | every cipher, `EnigmaMachine` and its parts, `CipherRegistry`, `MorseCode` |
| `RP.CodeBreaker.Codes` | `ICode` and every code, `CodeCatalogue` |
| `RP.CodeBreaker.Attacks` | one class per attack, `BruteForce`, `AutoBreaker`, `EnigmaBombe` |
| `RP.CodeBreaker.Modern` | `ToyRsa`, `Factoring`, `PasswordCracking` |
| `RP.CodeBreaker.Examples` | `FamousCases` |
| `RP.CodeBreaker.Statistics` | vendored special functions and the chi-squared distribution |

**Synchronous and asynchronous.** The slow searches (`SubstitutionAttack`, `ColumnarAttack`,
`PlayfairAttack`, `PolybiusAttack`, `EnigmaBombe`) have a `…Async` form that calls
`AttackOptions.Breathe` now and again, for hosts that must share one thread with a user interface (Blazor
WebAssembly, say: pass `async () => { StateHasChanged(); await Task.Delay(1); }`). The showcase doesn't
need it — it runs the synchronous `Break` in a Web Worker, where blocking harms nothing.
`AttackOptions.Progress` is called from inside the search, so give it a reporter that acts at once (the
standard `Progress<T>` defers to a synchronisation context, which a busy single thread never reaches).

**Randomness.** The randomised searches take `AttackOptions.Seed`, so a run can be repeated exactly.

---

## The demo website

The showcase is built the same way as RP.Sound's: a **Svelte** front end, and the C# library behind a
small JSON API that can run in two places.

```mermaid
graph LR
    UI["Svelte client<br/>(showcase-client)"]
    API["ShowcaseApi<br/>JSON in, JSON out<br/>(RP.CodeBreaker.Showcase.Catalog)"]
    W["Web Worker<br/>.NET runtime in WebAssembly<br/>(RP.CodeBreaker.Showcase.Browser)"]
    S["ASP.NET server<br/>native .NET<br/>(RP.CodeBreaker.Showcase)"]
    L["RP.CodeBreaker"]
    UI -- "GitHub Pages build" --> W
    UI -- "local / development" --> S
    W --> API
    S --> API
    API --> L
```

- **On GitHub Pages** there is no server. The library and `ShowcaseApi` are compiled to WebAssembly
  and run inside a **Web Worker** — a background thread of the browser — so a long search (the bombe,
  Playfair's annealing) never freezes the page. Progress reports come back from C# through a
  `[JSImport]` as the search runs, and **Cancel** stops the worker outright (a busy WebAssembly thread
  cannot read a "stop" message); the next run starts a fresh one.
- **Locally**, `RP.CodeBreaker.Showcase` serves the same client and maps the same API onto
  `POST /api/{operation}`, running the attacks as native .NET — the timings this document quotes.

Both answer every request identically, because both call the one `ShowcaseApi`. The pages are the
difficulty ladder (with the "few minutes" line for any attacker), a two-way converter for every code and
cipher, an analysis workbench (frequency charts, IoC, period finding, Kasiski, cipher identification), the
code breaker (every attack, with cribs, progress, cancel and the difficulty funnel), an Enigma simulator
and bombe (with the menu drawn as a graph), the famous cases, and the modern section (RSA factoring, PIN
and dictionary cracking, a key-space calculator).

WebAssembly in the browser is interpreted, so the heavy searches are several times slower than native
.NET — though less than you might fear. Measured in Chrome: the Babington substitution solves in about
70 ms, and the bombe over rotors I, II and IV in about 5 seconds (0.6 s natively); over all five rotors,
allow a minute or so. Hashing is much slower in the browser, so the dictionary attack there uses a short
word list.

## Building, testing and deploying

```
dotnet build CodeBreaker.sln
dotnet test RP.CodeBreaker.Tests

# the showcase served by ASP.NET (native speed): build the client into its wwwroot, then run it
cd showcase-client && npm install && npm run build && cd ..
dotnet run --project RP.CodeBreaker.Showcase            # http://localhost:5231

# or live-reload development: the server in one terminal, Vite (proxying /api) in another
cd showcase-client && npm run dev

# the static GitHub Pages build, exactly as deployed
dotnet publish RP.CodeBreaker.Showcase.Browser -c Release -o publish/browser
cd showcase-client && npm run build:pages && cp -r ../publish/browser/wwwroot/_framework dist/
```

The library targets .NET 10 and depends on nothing beyond the base class library; the showcase needs
Node.js for the Svelte build. The tests (MSTest)
check every cipher against published test vectors (the Enigma `BDZGO` vector and double step, the
Playfair and Hill textbook examples, Kryptos K1), round-trip every cipher and code, and break every
famous case.

The site deploys to GitHub Pages from `.github/workflows/pages.yml` on every push to `main`: it runs the
tests, publishes the WebAssembly build, builds the Svelte client in its `pages` mode (relative URLs, so it
works under the repository's Pages path; routes live after a `#`, so deep links need no server rewrites)
and copies the .NET runtime's `_framework` folder in beside it. One-off setup: **Settings → Pages → Build and deployment → Source: GitHub
Actions**. `.github/workflows/ci.yml` builds, tests and packs the library on every push and pull request.

### Project layout

```
RP.CodeBreaker/
  Core/         Letters, KeySpace, WorkEstimate, Difficulty, ICipher, BreakResult, NumberTheory, CipherCatalogue
  Language/     LanguageModel and its embedded English and German tables (Data/*.gz)
  Analysis/     FrequencyAnalysis, Periodicity, WordPatterns, CipherIdentifier
  Ciphers/      the ciphers, Enigma, CipherRegistry
  Codes/        the codes, Codebook, CodeCatalogue
  Attacks/      one attack per cipher family, AutoBreaker, EnigmaBombe
  Modern/       ToyRsa, Factoring, PasswordCracking
  Examples/     FamousCases
  Vendored/     RADD.Statistics (see below)
RP.CodeBreaker.Tests/                MSTest
RP.CodeBreaker.Showcase.Catalog/     ShowcaseApi: the showcase's JSON interface to the library
RP.CodeBreaker.Showcase.Browser/     ShowcaseApi compiled to WebAssembly, exported to JavaScript
RP.CodeBreaker.Showcase/             ASP.NET host: the client plus /api/, at native speed
showcase-client/                     the Svelte + Vite front end, and the Web Worker that hosts .NET
tools/build_language_data.py
```

---

## Code reused from other projects, and keeping it in sync

Before writing this library I looked through my other projects for anything worth reusing rather than
rewriting.

- **RADD.Statistics** (in the RADD Desktop Suite) supplied the special functions and the chi-squared
  distribution: `LogFactorial` for factorial key spaces, and the upper-tail probability behind
  `FrequencyAnalysis.ChiSquaredPValue`. Five files are **vendored** — copied unchanged except for the
  namespace (`RADD.Statistics` → `RP.CodeBreaker.Statistics`) — into `RP.CodeBreaker/Vendored/RADD.Statistics`,
  whose `UPSTREAM.md` records the source, the file list and the synced commit.
- **RP.Math** (and its twin RADD.Math) is geometry and floating-point numerics; nothing in it is needed to
  break ciphers, so nothing is taken. The number theory this library needs (`NumberTheory`) lives here.
- **The showcase and its deployment workflow** follow RP.Sound's: a Svelte client, the library behind a
  shared API exported from a plain .NET WebAssembly build into a Web Worker, an ASP.NET host for local
  use, and the same Pages workflow. The look (palette, navigation, panels) follows RP.Math's visualizer.

Vendored code is kept in step **both ways**, using the same protocol as RADD.Math and RP.Math: on any
commit touching the vendored files, changes upstream are brought in, changes made here are ported back
(on a branch — never onto someone's work in progress), the namespace difference is ignored, and the
synced commit in `UPSTREAM.md` is updated. The full protocol is in `CLAUDE.md`. New *generic* statistics
belong upstream first; cryptography-specific code stays here.

> **Copyright note.** The five vendored RADD.Statistics files are RADD Solutions Limited's code, published
> here with RADD Solutions' permission. Each keeps its original copyright header so its ownership stays on
> record.

---

## Points of interest

- Simon Singh, *The Code Book* (1999) — the best popular history, and the inspiration for much of the
  famous-cases section.
- David Kahn, *The Codebreakers* (1967, revised 1996) — the comprehensive history.
- Claude Shannon, "Communication Theory of Secrecy Systems" (1949) — perfect secrecy and unicity distance.
- William Friedman, *The Index of Coincidence and Its Applications in Cryptography* (1922).
- Gordon Welchman, *The Hut Six Story* (1982) — the bombe and the diagonal board, from the man who added it.
- James Lyons, *Practical Cryptography* (practicalcryptography.com) — quadgram scoring and simulated
  annealing for classical ciphers, which shaped the attacks here.
- Bletchley Park, and The National Museum of Computing next door, where a working bombe and a rebuilt
  Colossus can be seen running.

---

## Future considerations

Things deliberately not yet built, recorded so the reasoning isn't lost:

- **Lorenz and Colossus.** An SZ42 simulator over `Ita2Code`, and Tutte's statistical method
  ("1+2 break-in") that Colossus mechanised — the natural sequel to the bombe.
- **Homophonic substitution** (Zodiac, the Beale ciphers): a hill climb over symbol-to-letter
  assignments, with the key space and unicity distance that many-to-one mappings imply.
- **Attacks on Bifid and ADFGVX.** Bifid by simulated annealing over the grid with the period known;
  ADFGVX from messages in depth, as Painvin did.
- **Naval Enigma** with the fourth rotor, and **Banburismus**, Turing's statistical method for cutting
  down the rotor orders before the bombe ran.
- **Double transposition** and running-key ciphers.
- **Faster in the browser.** WebAssembly ahead-of-time compilation (the `wasm-tools` workload) would make
  the heavy searches much quicker on the site, at the cost of a larger download and a longer build.
  Splitting the bombe's rotor orders across several workers would use more than one core.

---

## Status and history

New, and complete for what it sets out to do: sixteen ciphers and the Enigma machine, ten codes, attacks
that break every cipher in the catalogue that has a practical attack (on realistic messages), a bombe
that recovers full Enigma settings from a crib, English and German language models, a difficulty model
behind all of it, a Svelte showcase that runs the library in a Web Worker, and fifty tests. It targets
.NET 10. It was started in November 2026, shortly after a visit
to Bletchley Park.

---

*Licensed under the [Code Project Open License (CPOL) 1.02](LICENSE.txt), like RP.Math's original
article — except the vendored statistics files, which remain © RADD Solutions Limited and are published
with its permission (see the copyright note above).*
