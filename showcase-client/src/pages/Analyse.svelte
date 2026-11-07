<script>
  // The first look at a ciphertext: letter frequencies, index of coincidence, period finding, Kasiski
  // repeats and a guess at the cipher — all before any key is known.
  import { ask } from '../lib/api.js';
  import { shared } from '../lib/state.svelte.js';
  import { num } from '../lib/format.js';

  const cases = shared.meta.cases.filter((c) => !['Repeating-key XOR', 'Codebook', 'One-time pad (reused)'].includes(c.cipher));
  const indechiffrable = shared.meta.cases.find((c) => c.cipher === 'Vigenère');

  let text = $state(indechiffrable?.ciphertext ?? '');
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
        <a class="button primary" href="#/break">Try to break it →</a>
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
          <p class="note">The gaps between repeats are multiples of a repeating key's length; the common factors point to it.</p>
        {/if}
      </section>

      <section class="panel">
        <h3>Commonest pairs and triples</h3>
        <div class="desc mono">{a.bigrams.map((g) => `${g.gram} ${g.count}`).join('  ')}</div>
        <div class="desc mono">{a.trigrams.slice(0, 10).map((g) => `${g.gram} ${g.count}`).join('  ')}</div>
        <p class="note">English: TH HE IN ER AN RE… and THE AND ING ION…</p>
      </section>
    </div>
  </div>
{/if}
