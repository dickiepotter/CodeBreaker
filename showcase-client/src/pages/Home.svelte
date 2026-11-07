<script>
  // The difficulty ladder: every cipher's full key space against the work its best attack needs, with
  // the "few minutes" line for whoever is searching.
  import { shared } from '../lib/state.svelte.js';
  import { bits, colour, verdict } from '../lib/format.js';
  import AttackerSelect from '../lib/AttackerSelect.svelte';

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
</div>

<h2>The ladder in numbers</h2>
<div class="panel">
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
