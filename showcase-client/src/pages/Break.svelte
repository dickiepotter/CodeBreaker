<script>
  // Break a cipher, and see how much work it took: any attack (or Auto), cribs, the language, and the
  // difficulty funnel drawn from the full key space down to the work actually done.
  import { shared, route, takeHandoff } from '../lib/state.svelte.js';
  import { Runner } from '../lib/Runner.svelte.js';
  import { cribs as splitCribs, ms, num, thousands } from '../lib/format.js';
  import { inBrowser } from '../lib/api.js';
  import AttackerSelect from '../lib/AttackerSelect.svelte';
  import Funnel from '../lib/Funnel.svelte';
  import Progress from '../lib/Progress.svelte';
  import Guide from '../lib/Guide.svelte';
  import Explain from '../lib/Explain.svelte';

  /** One line on how each attack works, shown under the choice. */
  const howItWorks = {
    Auto: 'Measures the text first (as on the Analyse page), guesses the cipher family, then runs the attack that suits it.',
    Caesar: 'Tries all 25 shifts and keeps the one whose text reads most like the language. Pure brute force — there are so few keys.',
    Affine: 'Tries all 312 multiplier-and-shift pairs. Still small enough for brute force.',
    Substitution: 'Hill climbing: starts from a guess based on letter counts, swaps two letters of the key at a time, and keeps any swap that makes the text read more like the language.',
    'Vigenère': 'Finds the key length from repeats and column statistics, then solves each column as its own Caesar cipher. A product of possibilities becomes a sum.',
    Beaufort: 'The same as Vigenère (Beaufort subtracts instead of adds): find the period, then each column separately.',
    Autokey: 'The message becomes the key after a short primer, so there is no period to find; it guesses the primer a letter at a time.',
    'Rail fence': 'Tries every number of rails. Letters are only moved, never changed, so there are few keys.',
    Columnar: 'Guesses the number of columns, then hill-climbs the column order, keeping changes that make more English four-letter runs.',
    Playfair: 'Simulated annealing: like hill climbing, but sometimes accepts a worse 5×5 grid so it can escape dead ends. The slowest attack here.',
    Hill: 'The Hill cipher mixes letters in pairs with a 2×2 matrix; each row of the matrix can be found on its own, so 26⁴ becomes 2 × 26².',
    Polybius: 'Turns each pair of digits back into a letter symbol, then solves it as a substitution.',
    XOR: 'Finds the key length from the bytes, then each key byte separately: the one that makes its column look most like text.',
  };
  const attacks = shared.meta.attacks;
  const cases = shared.meta.cases;

  /** The attack that suits a famous case, or null if this page has none for it. */
  const attackFor = (c) =>
    ({
      Caesar: 'Caesar',
      'Simple substitution': 'Substitution',
      Vigenère: 'Vigenère',
      'Keyed Vigenère (Quagga)': 'Vigenère',
      Playfair: 'Playfair',
      'Repeating-key XOR': 'XOR',
    })[c.cipher] ?? null;

  let ciphertext = $state(cases[1].ciphertext);
  let attack = $state('Substitution');
  let cribText = $state('');
  let language = $state('English');
  let restarts = $state(inBrowser ? 4 : 8);
  let maxKeyLength = $state(12);
  let iterations = $state(inBrowser ? 150_000 : 400_000);
  let result = $state(null);
  let notice = $state(null);
  const runner = new Runner();

  function load(index) {
    const c = cases[index];
    ciphertext = c.ciphertext;
    cribText = c.cribs.join(', ');
    attack = attackFor(c) ?? 'Auto';
    if (attack === 'Playfair' && inBrowser) {
      restarts = 2;
      iterations = 150_000;
    }
    result = null;
    notice =
      c.cipher === 'Keyed Vigenère (Quagga)'
        ? 'Kryptos K1 uses a keyed (mixed) alphabet, which this Vigenère attack does not search; it will find the period (10) but not the text. A lesson in itself: one extra twist defeats a textbook attack.'
        : null;
  }

  const requested = Number(route.query.get('case'));
  if (route.query.has('case') && requested >= 0 && requested < cases.length) load(requested);
  const handed = takeHandoff();
  if (handed) {
    ciphertext = handed;
    cribText = '';
    attack = 'Auto';
  }

  async function run() {
    result = null;
    result = await runner.run('break', {
      attack,
      ciphertext,
      cribs: splitCribs(cribText),
      language,
      restarts,
      maxKeyLength,
      iterations,
    });
  }
</script>

<header class="masthead">
  <h1>Break it, and see <span>how much work it took</span></h1>
  <p>
    Choose an attack (or let the statistics choose), add any words you expect in the message — a <b>crib</b>: DEAR at the
    top of a letter, WETTERBERICHT in a weather report — and run it. The funnel shows the key space shrinking at each
    insight, from the naive brute force down to the work the attack actually did.
    {#if inBrowser}
      It all runs in your browser, in a Web Worker: interpreted WebAssembly is slower than desktop .NET, so the heavier
      searches take seconds here rather than milliseconds — but the page never freezes, and you can cancel.
    {/if}
  </p>
</header>

<Guide id="break">
  <ol>
    <li><b>Choose a message.</b> One is loaded already; <i>Load a case</i> on the right offers more, or paste your own (the
      Convert page's <i>Try to break it →</i> button brings yours here).</li>
    <li><b>Choose an attack</b> — or leave it on <b>Auto</b> and let the statistics choose. The line under the menu says how
      the chosen attack works.</li>
    <li><b>Add a crib if you have one</b>: a word you're sure is in the message. It's optional, but it is often the
      difference between minutes and seconds — and the funnel will show you exactly how much it saved.</li>
    <li>Press <b>Break it</b>. A progress bar shows the best text so far; you can cancel at any time.</li>
    <li><b>Read the answer, then the funnel</b> on the right. The top bar is every possible key; each bar below is one
      thing the attack learned, and how much of the search it removed.</li>
  </ol>
  <p class="look">Leave <i>Restarts</i>, <i>Longest key</i> and <i>Iterations</i> alone unless an attack comes back with
    half-readable text; then raising them gives it more tries.</p>
</Guide>

<div class="workspace">
  <div class="stack">
    <section class="panel">
      <label class="field">
        <span>Ciphertext {attack === 'XOR' ? '(hexadecimal)' : ''}</span>
        <textarea bind:value={ciphertext} rows="6"></textarea>
      </label>
      <div class="row">
        <label class="field">
          <span>Attack</span>
          <select bind:value={attack}>
            {#each attacks as a}<option value={a}>{a}</option>{/each}
          </select>
          <small>{howItWorks[attack] ?? ''}</small>
        </label>
        <label class="field">
          <span>Language</span>
          <select bind:value={language}>
            <option>English</option>
            <option>German</option>
          </select>
        </label>
      </div>
      <label class="field">
        <span>Cribs — words you expect, comma separated (optional)</span>
        <input type="text" bind:value={cribText} placeholder="e.g. QUEEN, LETTER" />
        <small>Context gives cribs away: a letter starts DEAR, an order ends with a name, a weather report says WEATHER.</small>
      </label>
      <div class="row">
        <label class="field"><span>Restarts</span><input type="number" min="1" max="50" bind:value={restarts} /><small>fresh random starts for a hill climb</small></label>
        <label class="field"><span>Longest key</span><input type="number" min="2" max="30" bind:value={maxKeyLength} /><small>longest repeating key to look for</small></label>
        <label class="field"><span>Iterations (Playfair)</span><input type="number" min="1000" step="10000" bind:value={iterations} /><small>trial grids per restart</small></label>
      </div>
      <Explain title="What are restarts and iterations?">
        <p>
          The clever attacks here (substitution, columnar, Playfair) don't try every key. They <b>hill-climb</b>: start
          from a random key, make a small change, and keep it if the text reads more like the language. Like walking uphill
          in fog, they can end on a small hill rather than the summit — text that half reads.
        </p>
        <p>
          <b>Restarts</b> begins again from new random keys and keeps the best result, so more restarts means more chances
          of finding the summit. <b>Iterations</b> is how many small changes Playfair's search makes each time. Both cost
          time; the defaults are usually enough.
        </p>
      </Explain>
      {#if attack === 'Playfair'}
        <p class="note">
          Playfair is the hardest classical cipher here: simulated annealing needs hundreds of thousands of trial grids for a
          few hundred letters (about 3 s in desktop .NET{inBrowser ? ', far longer in the browser' : ''}), and a run can
          still end on a near-miss. If the text half-reads, run it again with another budget — or add a crib. Short
          messages, like PT-109's, may not yield at all.
        </p>
      {/if}
      {#if notice}<div class="error">{notice}</div>{/if}
      <div class="buttons">
        <button class="primary" onclick={run} disabled={runner.busy}>Break it</button>
      </div>
      <Progress {runner} />
    </section>

    {#if result}
      <section class="panel">
        <h3>{result.cipher} — key {result.key}</h3>
        <div class="stats">
          <div class="result">
            <span class="result__label">Reads as</span>
            <div class="result__num" style="color:{result.looksLikeEnglish ? '#4be0a8' : '#ff7a7a'}">
              {result.looksLikeEnglish ? 'language' : 'gibberish'}
            </div>
            <div class="result__sub">{num(result.englishLikeness, 2)} per letter (text ≈ −4.4, random ≲ −6)</div>
          </div>
          <div class="result"><span class="result__label">Keys tried</span><div class="result__num">{thousands(result.keysTried)}</div><div class="result__sub">compare with the funnel's first bar</div></div>
          <div class="result"><span class="result__label">Time {inBrowser ? 'here' : 'on the server'}</span><div class="result__num">{ms(result.elapsedMs)}</div></div>
        </div>
        <Explain title="How does it know it has the answer?">
          <p>
            The computer can't read, so it scores text by how common its four-letter runs are in real English (or German):
            TION and THER score well, QXZJ terribly. That score, averaged per letter, is the <b>Reads as</b> figure: about
            −4.4 for real text, −6 or lower for gibberish.
          </p>
          <p>
            "Keys tried" is the real work done. Set it against the first bar of the funnel — every key there is — to see how
            little of the key space a good attack needs to visit.
          </p>
        </Explain>
        <div class="result">
          <span class="result__label">Plaintext</span>
          <div class="output">{result.plaintext}</div>
        </div>
        {#if attack !== 'XOR' && result.plaintext.length > 0}
          <div class="result">
            <span class="result__label">With the spaces put back (the computer's best guess at the word breaks)</span>
            <div class="desc">{result.segmented}</div>
          </div>
        {/if}
      </section>

      {#if result.guesses}
        <section class="panel">
          <h3>What the statistics suggested</h3>
          {#each result.guesses as g}
            <p class="desc"><b>{g.family}</b> ({Math.round(g.confidence * 100)}%): {g.reason}</p>
          {/each}
        </section>
      {/if}
    {/if}
  </div>

  <div class="stack">
    <section class="panel">
      <AttackerSelect />
      {#if !result}
        <p class="desc">Run an attack to see its funnel: the full key space at the top, and below it each insight that cut the search down.</p>
        <h3>Load a case</h3>
        <div class="buttons">
          {#each cases as c, i}
            {#if attackFor(c)}
              <button onclick={() => load(i)}>{c.title}</button>
            {/if}
          {/each}
        </div>
      {:else}
        <h3>How the search shrank</h3>
        <Funnel difficulty={result.difficulty} />
      {/if}
    </section>

    {#if result}
      <section class="panel">
        <h3>Working</h3>
        <p class="note">The attack's own notes, step by step: what it measured, what it guessed, what it kept.</p>
        <ol class="log">
          {#each result.log as line}<li>{line}</li>{/each}
        </ol>
        {#if result.candidates.length > 1}
          <h3>Runners-up</h3>
          <p class="note">The next-best keys it found. When the winner is wrong, the right answer is often one of these.</p>
          <div class="tablewrap">
            <table>
              <thead><tr><th>Key</th><th>Decryption</th></tr></thead>
              <tbody>
                {#each result.candidates.slice(0, 6) as c}
                  <tr><td class="n">{c.key}</td><td class="n">{c.plaintext.length > 50 ? c.plaintext.slice(0, 50) + '…' : c.plaintext}</td></tr>
                {/each}
              </tbody>
            </table>
          </div>
        {/if}
      </section>
    {/if}
  </div>
</div>
