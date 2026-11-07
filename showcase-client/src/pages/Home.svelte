<script>
  // The difficulty ladder: every cipher's full key space against the work its best attack needs, with
  // the "few minutes" line for whoever is searching.
  import { shared } from '../lib/state.svelte.js';
  import { bits, colour, verdict } from '../lib/format.js';
  import AttackerSelect from '../lib/AttackerSelect.svelte';
  import Guide from '../lib/Guide.svelte';
  import Explain from '../lib/Explain.svelte';

  const LabelWidth = 250;
  const RowHeight = 24;
  const Top = 26;
  const MaxBits = 256;
  const feasibilities = ['Instant', 'FewMinutes', 'Hours', 'Months', 'Lifetime', 'Infeasible'];

  const rows = $derived(shared.meta.catalogue);
  const minutesBits = $derived(shared.meta.attackers[shared.attacker].fewMinutesBits);
  const height = $derived(Top + rows.length * RowHeight + 30);
  const x = (b) => LabelWidth + (Math.min(Math.max(b, 0), MaxBits) / MaxBits) * (980 - LabelWidth);
  const gridlines = Array.from({ length: MaxBits / 32 + 1 }, (_, i) => i * 32);
</script>

<header class="masthead">
  <h1>The <span>difficulty ladder</span></h1>
  <p>
    Every cipher has a key space — the number of keys it could have been set up with — and for years people mistook that
    number for its strength. It isn't. A simple substitution has more keys than DES, yet you can break one over breakfast,
    because the letters can be found one at a time. What matters is the work the <i>best attack</i> has to do, and whether
    that fits in the time you have.
  </p>
  <p>
    The grey bar is the naive brute force: every key, tried one after another. The coloured bar is the work that is really
    needed once the code breaker is clever — or has been told something, like a word that must be in the message. The
    dashed line is "a few minutes" (five, to be exact) for whoever you choose below. Anything whose coloured bar stops left
    of the line can be broken while you wait. The
    <a href="https://github.com/dickiepotter/CodeBreaker#readme" target="_blank" rel="noopener">tutorial</a>
    explains how every number here is derived.
  </p>
</header>

<Guide id="home" title="New here? Start here">
  <p>
    This site is a hands-on companion to the <a href="https://github.com/dickiepotter/CodeBreaker#readme" target="_blank" rel="noopener">tutorial</a>.
    Each page along the top does one job. If you have ten minutes, take them in this order:
  </p>
  <ol class="tour">
    <li><a href="#/convert"><b>Convert</b><span>Encrypt a sentence of your own with a cipher, and see how many keys it could have used.</span></a></li>
    <li><a href="#/analyse"><b>Analyse</b><span>Look at a ciphertext the way a code breaker does first: count letters and spot patterns, before knowing any key.</span></a></li>
    <li><a href="#/break"><b>Break</b><span>Run a real attack, and watch the number of keys left to try shrink at each step.</span></a></li>
    <li><a href="#/enigma"><b>Enigma &amp; bombe</b><span>Type on a simulated Enigma, then recover its settings from one guessed word, as Bletchley Park did.</span></a></li>
    <li><a href="#/famous"><b>Famous cases</b><span>Mary, Queen of Scots; the Zimmermann telegram; VENONA; Kryptos — each with a message to break.</span></a></li>
    <li><a href="#/modern"><b>Modern</b><span>Why today's ciphers don't fall like this — and why a weak password still does.</span></a></li>
  </ol>
  <p>
    On every page a green box like this one explains how to use it, and the <span style="color:var(--accent)">▸ What am I
    looking at?</span> links inside the panels explain each chart and number. Close this box and it stays closed.
  </p>
</Guide>

<div class="panel">
  <div class="row">
    <AttackerSelect />
    <div class="field">
      <span>"A few minutes" for them is</span>
      <div class="result__num">{bits(minutesBits)}</div>
    </div>
  </div>

  <svg class="chart" viewBox="0 0 1000 {height}" role="img" aria-label="Key space and effective work for each cipher, in bits">
    {#each gridlines as b}
      <line class="grid" x1={x(b)} x2={x(b)} y1={Top} y2={Top + rows.length * RowHeight} />
      <text x={x(b)} y={Top - 8} text-anchor="middle">{b}</text>
    {/each}
    <text x={LabelWidth - 8} y={Top - 8} text-anchor="end">bits →</text>

    {#each rows as p, i}
      {@const y = Top + i * RowHeight}
      {@const f = p.breakable ? p.times[shared.attacker].best.feasibility : 'Infeasible'}
      <text class="label" x={LabelWidth - 8} y={y + 15} text-anchor="end">{p.name}</text>
      <rect class="full" x={LabelWidth} y={y + 4} width={Math.max(1, x(p.keySpaceBits) - LabelWidth)} height="16" rx="3" />
      {#if p.keySpaceBits > MaxBits}
        <text x={x(MaxBits) - 4} y={y + 16} text-anchor="end">→ {bits(p.keySpaceBits)}</text>
      {/if}
      {#if p.breakable}
        <rect x={LabelWidth} y={y + 8} width={Math.max(2, x(p.effectiveBits) - LabelWidth)} height="8" rx="3" fill={colour(f)} />
      {:else}
        <text x={LabelWidth + 6} y={y + 16} style="fill:#c06bff">no attack exists: every message is equally possible</text>
      {/if}
    {/each}

    <line class="line" x1={x(minutesBits)} x2={x(minutesBits)} y1={Top - 4} y2={Top + rows.length * RowHeight + 4} />
    <text x={x(minutesBits) + 4} y={Top + rows.length * RowHeight + 18} style="fill:#ffce47">a few minutes</text>
  </svg>

  <div class="legend">
    <span><i style="background:#2f3a58"></i>full key space</span>
    {#each feasibilities as f}
      <span><i style="background:{colour(f)}"></i>best attack: {verdict(f)}</span>
    {/each}
  </div>

  <Explain title="How do I read this chart?">
    <p>One row per cipher, oldest and easiest at the top. Read it left to right: further right means more work.</p>
    <ul>
      <li>
        <b>The scale is in bits.</b> A key space of <i>n</i> bits means 2<sup><i>n</i></sup> keys, so each step of one bit
        to the right <b>doubles</b> the work; ten bits is a thousand times more; 77 bits (Enigma) is about 10<sup>23</sup>.
        Bits let a ruler hold both Caesar's 25 keys and AES's 10<sup>38</sup>.
      </li>
      <li><b>The grey bar</b> is how many keys the cipher could have been set up with — the work of trying them all.</li>
      <li>
        <b>The coloured bar</b> is the work the best known attack really needs. Its colour is the verdict for the searcher
        you picked: green means while you wait, red means a lifetime, purple means never.
      </li>
      <li>
        <b>The gold dashed line</b> is how much one searcher can do in five minutes. Change "Who is searching?" and watch
        it slide: a person with pencil and paper is far to the left, the whole Bitcoin network far to the right.
      </li>
    </ul>
    <p>
      The gap between the grey and the coloured bar is the point of the whole site: it is what cleverness — and a
      guessed word — buys you.
    </p>
  </Explain>
</div>

<h2>The ladder in numbers</h2>
<div class="panel">
  <p class="desc" style="margin-bottom:.6rem">
    The same ciphers in figures. Times are for the searcher chosen above; change it and they all update.
  </p>
  <table class="ladder">
    <thead>
      <tr>
        <th>Cipher</th><th>Key space</th><th>Brute force</th><th>Best attack, and why it needs so much less</th><th>Time, best attack</th>
      </tr>
    </thead>
    <tbody>
      {#each rows as p}
        {@const t = p.times[shared.attacker]}
        <tr>
          <td><b>{p.name}</b><div class="note">{p.era} · {p.famous}</div></td>
          <td class="n nowrap" data-label="Key space">{bits(p.keySpaceBits)}</td>
          <td class="n" data-label="Brute force">{t.brute.text}</td>
          <td data-label="Best attack">
            {p.attack}{#if p.breakable}<span class="note"> — {bits(p.effectiveBits)} of work</span>{/if}
            <div class="note">{p.explanation}</div>
          </td>
          <td data-label="Time, best attack">
            {#if p.breakable}
              <span class="chip f-{t.best.feasibility}">{t.best.text}</span>
            {:else}
              <span class="chip never">impossible</span>
            {/if}
          </td>
        </tr>
      {/each}
    </tbody>
  </table>
  <Explain title="What do the columns mean?">
    <ul>
      <li><b>Key space</b> — how many keys there are, in bits (each extra bit doubles it).</li>
      <li><b>Brute force</b> — how long it takes to try every key, one at a time, for the chosen searcher.</li>
      <li>
        <b>Best attack</b> — the cleverest method known, how much work it needs (again in bits), and in one sentence why it
        needs so much less than brute force.
      </li>
      <li><b>Time, best attack</b> — how long that takes, coloured by the verdict: green is "while you wait".</li>
    </ul>
  </Explain>
</div>

<h2>Reading the ladder</h2>
<div class="workspace even">
  <div class="panel">
    <h3>Below the line: classical ciphers</h3>
    <p class="desc">
      Every hand cipher sits left of the line on a laptop, but look how differently they get there. Caesar and affine are
      small enough to try every key. Substitution, Playfair and long transpositions have astronomical key spaces, yet fall
      to a <b>hill climb</b> that improves a key a little at a time, because a nearly-right key gives nearly-readable text.
      Vigenère and Hill fall because their keys split into pieces that can be found <b>separately</b> — a product of
      possibilities becomes a sum.
    </p>
  </div>
  <div class="panel">
    <h3>Above it: modern ciphers, and Enigma without help</h3>
    <p class="desc">
      Modern ciphers are designed so that no such shortcut exists: the key space <i>is</i> the work. Change the searcher to
      the whole Bitcoin network — the largest search humanity has built — and AES-128 still takes longer than the age of the
      universe. Enigma is the bridge between the two worlds: hopeless by brute force, but given a <b>crib</b> (a word known
      to be in the message), the bombe's deductions strip out the plugboard and leave about a million rotor positions.
    </p>
  </div>
</div>

<h2>The words used on this site</h2>
<div class="panel">
  <dl class="glossary">
    <div><dt>Plaintext / ciphertext</dt><dd>The message before and after encryption.</dd></div>
    <div><dt>Code</dt><dd>Swaps whole words or symbols using a fixed table (Morse, a codebook). There is no key; the table is the secret, if anything is.</dd></div>
    <div><dt>Cipher</dt><dd>Changes the message letter by letter with a rule and a secret <b>key</b>. Knowing the rule is no help without the key.</dd></div>
    <div><dt>Key space</dt><dd>Every key the cipher could have been set up with. Caesar has 25; a simple substitution has 26! ≈ 4 × 10<sup>26</sup>.</dd></div>
    <div><dt>Bits</dt><dd>A way to write huge numbers: <i>n</i> bits means 2<sup><i>n</i></sup>. 10 bits ≈ a thousand, 20 ≈ a million, 40 ≈ a trillion. One more bit doubles the work.</dd></div>
    <div><dt>Brute force</dt><dd>Trying every key, one after another, until the text reads. Always works, given time — often more time than the universe has.</dd></div>
    <div><dt>Crib</dt><dd>A word you are confident is in the message: the sender's name, "weather report", "Heil Hitler". Bletchley's word for it. It rules out most keys at a stroke.</dd></div>
    <div><dt>Searcher</dt><dd>Who (or what) is trying keys, and how fast: a clerk with pencil and paper, a wartime bombe, a laptop, a graphics card, the Bitcoin network.</dd></div>
    <div><dt>A few minutes</dt><dd>Five, exactly. The question on every page is whether the work left fits in five minutes for your chosen searcher.</dd></div>
    <div><dt>Frequency analysis</dt><dd>Counting letters. In English E, T and A are common and Z rare; a cipher that keeps that unevenness gives itself away.</dd></div>
    <div><dt>Index of coincidence</dt><dd>The chance two letters picked at random from the text are the same: 0.066 for English, 0.038 for random letters. Tells you whether a cipher used one alphabet or many.</dd></div>
    <div><dt>Hill climbing</dt><dd>Start with a random key, change it a little, keep the change if the text reads more like English. Repeat. Works when a nearly-right key gives nearly-right text.</dd></div>
    <div><dt>Quadgram score</dt><dd>How English a text looks, from how common its four-letter runs (TION, THER, …) are in real English. The "fitness" every hill climb here tries to raise.</dd></div>
    <div><dt>Unicity distance</dt><dd>Roughly how many letters of ciphertext you need before only one key gives sensible text. Shorter messages can decrypt to several plausible answers.</dd></div>
  </dl>
</div>
