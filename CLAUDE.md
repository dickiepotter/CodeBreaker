# CodeBreaker — notes for Claude

A teaching library (`RP.CodeBreaker`, .NET 10), its tests (`RP.CodeBreaker.Tests`, MSTest) and a
showcase built the RP.Sound way (`C:\Source\personal-github\Sound`): a Svelte + Vite client
(`showcase-client/`) over a JSON API (`RP.CodeBreaker.Showcase.Catalog/ShowcaseApi.cs`) that runs either
in a Web Worker from a plain .NET WebAssembly build (`RP.CodeBreaker.Showcase.Browser`, the GitHub Pages
build) or behind ASP.NET at native speed (`RP.CodeBreaker.Showcase`, local). Deployed by
`.github/workflows/pages.yml`. A sibling of RP.Math and Vector, whose README conventions it follows.

## Conventions

- **The README is the tutorial.** It is written in Richard's voice: first person, British spelling,
  plain language, no unexplained jargon, "verbose on purpose". Every cipher section follows the same
  shape — how it works, the key space, how it is broken, whether it falls in a few minutes, in code. Keep
  new material in that shape and keep the tables of contents current.
- **Difficulty is the theme.** Every attack returns a `Difficulty` funnel (`KeySpace` → each insight →
  remaining work). A new attack must record its reductions with honest numbers, and a new cipher needs a
  `KeySpace` and an entry in `CipherCatalogue`.
- **Answers are checked, never just trusted.** `BreakCheck` judges every result (cribs present, readable,
  message long enough) and the Break page shows its verdict. A new attack needs a rough reliable length
  in `BreakCheck.ReliableLength`, keyed by its `CipherName`. When an attack gets something wrong, add a
  test that reproduces it (see `BreakCheckTests`) before fixing it.
- **The site explains itself.** Every page has a `Guide` ("How to use this page") and `Explain` notes on
  its panels; the README's "Finding your way around the site" mirrors them. Change one, change the other.
- **Ciphers are immutable objects built from their key** and implement `ICipher`; codes implement
  `ICode`. Register new ones in `CipherRegistry` / `CodeCatalogue` so the site's converter picks them up.
- **The showcase talks only through `ShowcaseApi`** (JSON built with `JsonNode`, no reflection, so the
  trimmed WebAssembly build keeps working). A new page feature means a new or extended operation there,
  never C# in the client. Progress from long searches reaches the worker through the `[JSImport]`
  `codeBreakerProgress`; cancelling terminates the worker.
- **English and German statistics are data**, rebuilt reproducibly by `tools/build_language_data.py`
  from the public-domain Gutenberg texts listed in that script. Don't hand-edit the `.gz` tables.
- Run `dotnet test RP.CodeBreaker.Tests` before committing; the Pages workflow refuses to deploy a
  failing build.

## Vendored library: RADD.Statistics (upstream sync — run on every commit)

`RP.CodeBreaker/Vendored/RADD.Statistics/` holds files **vendored** from
`C:\Source\radd_azrdevops\RADD.DesktopSuite\libs\RADD.Statistics` (see its `UPSTREAM.md` for the file
list and the synced baseline commit). The **only** intended difference is the reversible namespace rename
`RADD.Statistics` ⟷ `RP.CodeBreaker.Statistics`. The files are © RADD Solutions Limited, published here
with RADD Solutions' permission: **keep each file's copyright header exactly as upstream has it.**

**Whenever a commit touches `RP.CodeBreaker/Vendored/RADD.Statistics/**`, or when asked to sync, run
this two-way reconciliation, ignoring namespacing:**

1. **Read upstream:** `git -C "C:\Source\radd_azrdevops\RADD.DesktopSuite" log -1 --format=%H -- libs/RADD.Statistics`
   and compare with the baseline in `UPSTREAM.md`.
2. **Upstream → here:** if it moved, take
   `git -C "…\RADD.DesktopSuite" diff <baseline>..HEAD -- <each vendored upstream file>` and apply it
   here, then re-apply the `RADD.Statistics` → `RP.CodeBreaker.Statistics` substitution. If upstream
   added a dependency the vendored files now need, vendor that file too and list it in `UPSTREAM.md`.
3. **Here → upstream:** normalise each vendored file (substitute `RP.CodeBreaker.Statistics` →
   `RADD.Statistics`) and `diff --strip-trailing-cr` it against upstream. Anything left is a real edit
   made here: port it to RADD.Statistics. The DesktopSuite is a working repository with its own work in
   progress — **never commit onto someone's feature branch**; create a branch from its main line for
   the port (or leave the change staged and tell Richard), and follow the DesktopSuite `CLAUDE.md`.
4. **Ignore** as expected: the namespace rename, line endings and byte-order marks.
5. **Verify:** `dotnet test RP.CodeBreaker.Tests` here, and `dotnet test tests/RADD.Statistics.Tests` in
   the DesktopSuite after porting.
6. **Record:** update the baseline commit and dates in `UPSTREAM.md`.
7. If the same lines changed on both sides, **stop and ask** rather than guessing.

**Generic maths belongs upstream.** If CodeBreaker needs a new statistical function (a distribution,
a test), add it to the vendored file *and* port it upstream in the same piece of work, so the two never
drift. Cryptography-specific code (n-gram scoring, key spaces, attacks) stays here.

## Why nothing is vendored from RP.Math

RP.Math (and its vendored twin RADD.Math) is geometry and floating-point numerics; nothing in it is
needed for cryptanalysis. The number theory CodeBreaker needs (`NumberTheory`: gcd, modular inverse,
primality, matrix inverse mod m) lives here. If it is ever wanted in RP.Math, move it there first and
vendor it back, following the protocol above with `RP.Math` as upstream (and remember RADD.Math then
picks it up through the DesktopSuite's own sync protocol).
