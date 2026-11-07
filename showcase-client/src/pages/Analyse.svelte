<script>
  // The first look at a ciphertext: letter frequencies, index of coincidence, period finding, Kasiski
  // repeats and a guess at the cipher — all before any key is known.
  import { ask } from '../lib/api.js';
  import { shared, takeHandoff } from '../lib/state.svelte.js';
  import { num } from '../lib/format.js';
  import Guide from '../lib/Guide.svelte';
  import Explain from '../lib/Explain.svelte';

  const cases = shared.meta.cases.filter((c) => !['Repeating-key XOR', 'Codebook', 'One-time pad (reused)'].includes(c.cipher));
  const indechiffrable = shared.meta.cases.find((c) => c.cipher === 'Vigenère');

  let text = $state(takeHandoff() ?? indechiffrable?.ciphertext ?? '');

  /** Hand the text on to the Break page. */
  function breakIt() {
    shared.handoff = text;
  }
  let a = $state(null);
  let error = $state(null);

  // Recompute as you type, debounced: in the browser every call goes to the worker.
  let timer;
  $effect(() => {
    const request = { text };
    clearTimeout(timer);
    timer = setTimeout(async () => {
      try {
        a = await ask('analyse', request);
        error = null;
      } catch (e) {
        error = e.message;
      }
    }, 250);
    return () => clearTimeout(timer);
  });

  const max = $derived(a ? Math.max(...a.letters.map((l) => Math.max(l.share, l.expected)), 1e-9) : 1);
  const top = $derived(a?.periods ? Math.max(0.08, ...a.periods.map((p) => p.ioc)) : 0.08);
  const y = (v) => 165 - (v / top) * 150;
  const friedman = $derived(!a || !isFinite(a.friedman) || a.friedman > 100 || a.friedman <= 0 ? '∞' : a.friedman.toFixed(1));
  const pValue = (p) => (p === 0 ? '0' : p < 0.001 ? p.toExponential(2) : p.toFixed(3));
</script>

<header class="masthead">
  <h1>What kind of <span>cipher</span> is this?</h1>
  <p>
    Before you know a single letter of the key, the statistics tell you a great deal. Count the letters and compare them
    with English; measure the <b>index of coincidence</b> (the chance two letters picked at random are the same — 0.066
    for English, 0.038 for random letters); cut the text into columns and see whether some period makes them look English
    again; look for repeated sequences. Paste a ciphertext, or load one.
  </p>
</header>

<Guide id="analyse">
  <ol>
    <li><b>Put a ciphertext in the box</b>: paste one, bring one from the Convert page, or press one of the buttons under
      the box to load a famous case. Everything below updates as you type.</li>
    <li><b>Read the five numbers</b> across the top. The most telling is the <b>index of coincidence</b>: near 0.066 means
      the letters were swapped one-for-one (or only moved about); near 0.038 means many alphabets, or a machine.</li>
    <li><b>Look at the bar chart</b> of letter counts against English (the gold line). Its shape says which family of
      cipher you have — the note under it says how to tell.</li>
    <li>If there is a second chart, <b>find the tall green bar</b>: that is the most likely length of a repeating key.</li>
    <li>Read <b>"What is it likely to be?"</b> on the right, then press <b>Try to break it →</b>.</li>
  </ol>
  <p class="look">None of this needs the key. That is the first lesson of code breaking: a cipher leaks the statistics of
    the language underneath it, and the leak tells you where to start.</p>
</Guide>

<div class="panel">
  <label class="field">
    <span>Ciphertext</span>
    <textarea bind:value={text} rows="5"></textarea>
  </label>
  <div class="buttons">
    {#each cases as c}
      <button onclick={() => (text = c.ciphertext)}>{c.title}</button>
    {/each}
    <button onclick={() => (text = '')}>Clear</button>
  </div>
  <p class="note">The buttons load messages from the Famous cases page. Babbage's Vigenère message is loaded to start with.</p>
</div>

{#if error}
  <div class="error" style="margin-top:1rem">{error}</div>
{/if}

{#if !a || a.length < 2}
  <p class="note" style="margin-top:1rem">Paste at least a few dozen letters; statistics need something to count.</p>
{:else}
  <div class="stats" style="margin-top:1.25rem">
    <div class="result"><span class="result__label">Letters</span><div class="result__num">{a.length}</div></div>
    <div class="result">
      <span class="result__label">Index of coincidence</span>
      <div class="result__num">{num(a.ioc)}</div>
      <div class="result__sub">English {num(a.englishIoc)} · random 0.0385</div>
    </div>
    <div class="result">
      <span class="result__label">Entropy</span>
      <div class="result__num">{num(a.entropy, 2)}</div>
      <div class="result__sub">bits per letter · English ≈ 4.17, random 4.70</div>
    </div>
    <div class="result">
      <span class="result__label">Chi-squared vs English</span>
      <div class="result__num">{num(a.chiSquared, 0)}</div>
      <div class="result__sub">p = {pValue(a.pValue)} — near 1 means "letters as English"</div>
    </div>
    <div class="result">
      <span class="result__label">Friedman's key length</span>
      <div class="result__num">{friedman}</div>
      <div class="result__sub">a rough estimate from the IoC alone</div>
    </div>
  </div>
  <div class="panel" style="margin-top:.6rem;padding-top:.5rem;padding-bottom:.5rem">
    <Explain title="What do these five numbers mean?">
      <ul>
        <li><b>Letters</b> — how much there is to count. Below about a hundred, every statistic here gets unreliable.</li>
        <li>
          <b>Index of coincidence</b> — pick two letters at random from the text; how often are they the same? English
          repeats E, T and A a lot, so 0.066. Evenly spread random letters give 0.038. A one-for-one swap of letters
          (Caesar, substitution) or a reshuffle (transposition) keeps English's figure; a cipher that uses several
          alphabets in turn (Vigenère, Enigma) drags it down toward random.
        </li>
        <li>
          <b>Entropy</b> — how unpredictable each letter is, in bits. Again: low like English means one alphabet, high like
          random means many.
        </li>
        <li>
          <b>Chi-squared vs English</b> — how far these exact letter counts are from English's. Small, with p near 1, means
          the letters themselves are English letters in English proportions: the text has only been rearranged. Large
          means they were changed.
        </li>
        <li>
          <b>Friedman's key length</b> — a quick estimate, from the IoC alone, of how long a repeating key is. Rough: use the
          period chart below for the real answer.
        </li>
      </ul>
    </Explain>
  </div>

  <div class="workspace" style="margin-top:1.25rem">
    <div class="stack">
      <section class="panel">
        <h3>Letter frequencies</h3>
        <svg class="chart" viewBox="0 0 560 200" role="img" aria-label="Letter frequencies against English">
          {#each a.letters as l, i}
            {@const h = (l.share / max) * 160}
            <rect x={10 + i * 21} y={175 - h} width="15" height={h} rx="2" fill="#4be0a8"
              ><title>{l.letter}: {l.count} ({(l.share * 100).toFixed(1)}%)</title></rect>
            <text x={17.5 + i * 21} y="192" text-anchor="middle">{l.letter}</text>
          {/each}
          <polyline class="english" points={a.letters.map((l, i) => `${17.5 + i * 21},${175 - (l.expected / max) * 160}`).join(' ')} />
        </svg>
        <div class="legend"><span><i style="background:#4be0a8"></i>this text</span><span><i style="background:#ffce47"></i>English</span></div>
        <p class="note">
          Same shape, same letters → a transposition (letters moved, not changed). Same shape, shifted along → Caesar. Same
          lumpiness, different letters → substitution. Flat → many alphabets, or a machine.
        </p>
        <Explain>
          <p>
            Each green bar is how often that letter appears in this text; the gold line is how often it appears in ordinary
            English (E about 12%, Z almost never). Hover over a bar for the exact count.
          </p>
          <p>
            Compare the <b>shapes</b>, not the letters. If the bars are as uneven as the gold line but in the wrong places,
            each letter has been swapped for another — and the tallest bar is probably E. If they are flat, several
            alphabets have been mixed together and averaged the unevenness away.
          </p>
        </Explain>
      </section>

      {#if a.periods}
        <section class="panel">
          <h3>Does some period make the columns English?</h3>
          <svg class="chart" viewBox="0 0 560 190" role="img" aria-label="Average column index of coincidence for each key length">
            <line class="grid" x1="30" x2="555" y1={y(0.0667)} y2={y(0.0667)} style="stroke:#ffce47;stroke-dasharray:5 4" />
            <line class="grid" x1="30" x2="555" y1={y(0.0385)} y2={y(0.0385)} />
            <text x="2" y={y(0.0667) + 3}>EN</text>
            <text x="2" y={y(0.0385) + 3}>rnd</text>
            {#each a.periods as p}
              {@const h = (p.ioc / top) * 150}
              <rect x={34 + (p.period - 1) * 26} y={165 - h} width="18" height={h} rx="2" fill={p.ioc > 0.055 ? '#4be0a8' : '#4a5a82'}
                ><title>period {p.period}: {num(p.ioc)} · {p.votes} Kasiski votes</title></rect>
              <text x={43 + (p.period - 1) * 26} y="182" text-anchor="middle">{p.period}</text>
            {/each}
          </svg>
          <p class="note">
            For each key length, the text is cut into that many columns and each column's IoC averaged. The right length (and
            its multiples) jumps up to English's 0.066, because each column was shifted by one key letter only. Best guess:
            <b>{a.bestPeriod}</b>.
          </p>
          <Explain>
            <p>
              A Vigenère-style cipher repeats its key: with key BABBAGE, letters 1, 8, 15… are all shifted by B. If you guess
              the key length right and pull those letters out into a column, the column is a plain Caesar cipher — and
              looks like English again.
            </p>
            <p>
              So the chart tries every length from 1 upward. A bar's height is how English its columns look (the
              <b>EN</b> line) against random (<b>rnd</b>). Bars turn green when they get close to English. The first green
              bar is usually the key length; its multiples light up too, because they cut the text the same way. Hover
              over a bar for its Kasiski votes, a second, independent check.
            </p>
            <p>
              This is the step that turns one impossible search into several easy ones: a 7-letter key has 26<sup>7</sup> ≈
              8 billion settings, but seven separate Caesar ciphers have 7 × 26 = 182.
            </p>
          </Explain>
        </section>
      {/if}
    </div>

    <div class="stack">
      <section class="panel">
        <h3>What is it likely to be?</h3>
        {#each a.guesses as g}
          <div class="result">
            <span class="result__label">{Math.round(g.confidence * 100)}% · {g.family}</span>
            <div class="desc">{g.reason}</div>
          </div>
        {/each}
        <p class="note">
          A score from the statistics above, not a certainty. The percentage is how strongly each family fits.
        </p>
        <a class="button primary" href="#/break" onclick={breakIt}>Try to break it →</a>
      </section>

      <section class="panel">
        <h3>Kasiski repeats</h3>
        {#if !a.repeats || a.repeats.length === 0}
          <p class="note">No sequence of three or more letters repeats.</p>
        {:else}
          <div class="tablewrap">
            <table>
              <thead><tr><th>Sequence</th><th>At</th><th>Gaps</th></tr></thead>
              <tbody>
                {#each a.repeats.slice(0, 12) as r}
                  <tr><td class="n">{r.sequence}</td><td class="n">{r.positions.join(', ')}</td><td class="n">{r.spacings.join(', ')}</td></tr>
                {/each}
              </tbody>
            </table>
          </div>
          <p class="note">
            When the same word is enciphered by the same part of a repeating key, the same letters come out. So repeats of
            three or more letters are rarely chance, and the gaps between them are multiples of the key length: if the
            gaps are 14, 21 and 35, the key is probably 7 long. Friedrich Kasiski published this in 1863; Babbage had found
            it earlier and kept it quiet.
          </p>
        {/if}
      </section>

      <section class="panel">
        <h3>Commonest pairs and triples</h3>
        <div class="desc mono">{a.bigrams.map((g) => `${g.gram} ${g.count}`).join('  ')}</div>
        <div class="desc mono">{a.trigrams.slice(0, 10).map((g) => `${g.gram} ${g.count}`).join('  ')}</div>
        <p class="note">
          The most common two- and three-letter runs, with their counts. English's are TH HE IN ER AN RE… and THE AND ING
          ION…. In a substitution cipher the commonest triple is very often THE in disguise — a foothold for three letters
          of the key.
        </p>
      </section>
    </div>
  </div>
{/if}
