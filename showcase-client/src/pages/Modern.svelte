<script>
  // Modern ciphers: the key is the work. Toy RSA (breaking it is factoring it), a strong hash with a weak
  // secret, and a key-space calculator.
  import { ask, inBrowser } from '../lib/api.js';
  import { Runner } from '../lib/Runner.svelte.js';
  import Progress from '../lib/Progress.svelte';
  import { bits as fmtBits, ms, thousands, verdict } from '../lib/format.js';

  // ---- toy RSA ----
  let rsaBits = $state(48);
  let seed = $state(Math.floor(Math.random() * 1e9));
  let key = $state(null); // the rsa response that made the key (it also carries the "best quick method" result)
  let factor = $state(null); // the rsa response whose factoring is on show
  const rsaRunner = new Runner();

  let close = $state(false);

  // Making a key is instant (method 'none'); factoring it is the slow part, run on demand.
  async function generate(careless = false) {
    seed = Math.floor(Math.random() * 1e9);
    close = careless;
    factor = null;
    key = await rsaRunner.run('rsa', { bits: rsaBits, seed, close, method: 'none' });
  }

  async function factorWith(method) {
    if (!key) return;
    factor = null;
    factor = await rsaRunner.run('rsa', { bits: rsaBits, seed, close, method });
  }

  generate();

  // A duration in words, from log₁₀ of the seconds (so 2^117 operations don't overflow).
  function describe(log10Seconds) {
    if (log10Seconds < -3) return 'under a millisecond';
    const s = 10 ** log10Seconds;
    if (s < 1) return `${Math.round(s * 1000)} milliseconds`;
    if (s < 120) return `${s.toFixed(1)} seconds`;
    if (s < 7200) return `${Math.round(s / 60)} minutes`;
    if (s < 172800) return `${Math.round(s / 3600)} hours`;
    const log10Years = log10Seconds - Math.log10(365.25 * 86400);
    if (log10Years < 0) return `${Math.round(s / 86400)} days`;
    if (log10Years < 6) return `${thousands(Math.round(10 ** log10Years))} years`;
    const e = Math.floor(log10Years);
    return `${(10 ** (log10Years - e)).toFixed(2)} × 10^${e} years`;
  }
  const onBitcoin = (b) => describe(b * Math.log10(2) - 21);

  // ---- a strong hash and a weak secret ----
  let pin = $state('4071');
  let pinHash = $state('');
  let pinDigits = $state(0);
  let pinResult = $state(null);
  let password = $state('House7');
  let wordResult = $state(null);
  const crackRunner = new Runner();
  const maxWords = inBrowser ? 300 : 5000;

  async function sha256(text) {
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
    return Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, '0')).join('');
  }

  $effect(() => {
    const p = pin;
    sha256(p ?? '').then((h) => (pinHash = h)).catch(() => (pinHash = ''));
  });

  async function crackPin() {
    const digits = (pin ?? '').replace(/\D/g, '');
    if (digits.length < 1 || digits.length > 7) return;
    pin = digits;
    pinDigits = digits.length;
    pinResult = null;
    pinResult = await crackRunner.run('crack', { kind: 'pin', secret: digits, digits: digits.length });
  }

  async function crackWord() {
    wordResult = null;
    wordResult = await crackRunner.run('crack', { kind: 'word', secret: password ?? '', maxWords });
  }

  // ---- key-space calculator ----
  let calcKind = $state('power');
  let calcA = $state(94);
  let calcB = $state(12);
  let calc = $state(null);
  let calcError = $state(null);

  // Recompute as you type, debounced: in the browser every call goes to the worker.
  let timer;
  $effect(() => {
    const request =
      calcKind === 'power'
        ? { kind: 'power', a: Number(calcA), b: Number(calcB) }
        : { kind: calcKind, a: Number(calcB) };
    clearTimeout(timer);
    timer = setTimeout(async () => {
      try {
        calc = await ask('keyspace', request);
        calcError = null;
      } catch (e) {
        calcError = e.message;
      }
    }, 250);
    return () => clearTimeout(timer);
  });
</script>

<header class="masthead">
  <h1>Modern ciphers: <span>the key is the work</span></h1>
  <p>
    Modern ciphers are built so that no attack does much better than trying keys — so the whole question is how many keys
    there are, and how they were chosen. RSA's private key is exactly as safe as its modulus is hard to factor. SHA-256
    cannot be run backwards, but if the secret came from a small set — 10,000 PINs, or the words people actually use — you
    simply try them all.
    {#if inBrowser}
      This page runs in your browser, which is much slower than native .NET, so the sizes are kept small.
    {/if}
  </p>
</header>

<div class="workspace even">
  <section class="panel">
    <h3>Toy RSA: breaking it is factoring it</h3>
    <label class="field slider">
      <span>Modulus size: <b>{rsaBits} bits</b></span>
      <input type="range" min="16" max="96" step="4" bind:value={rsaBits} />
    </label>
    <div class="buttons">
      <button class="primary" onclick={() => generate(false)} disabled={rsaRunner.busy}>Make a key</button>
      <button onclick={() => generate(true)} disabled={rsaRunner.busy}>Make a careless key (p ≈ q)</button>
    </div>
    {#if key}
      <div class="result">
        <span class="result__label">Public key</span>
        <div class="output">n = {key.n}
e = {key.e}</div>
        <div class="result__sub">
          {key.bits}-bit modulus. The secret is p = {key.p}, q = {key.q}.
          {#if key.careless}p and q are neighbouring primes, so n is nearly a perfect square: try Fermat.{/if}
        </div>
      </div>
      <div class="buttons">
        <button onclick={() => factorWith('auto')} disabled={rsaRunner.busy}>Best quick method</button>
        <button onclick={() => factorWith('trial')} disabled={rsaRunner.busy}>Trial division</button>
        <button onclick={() => factorWith('fermat')} disabled={rsaRunner.busy}>Fermat</button>
        <button onclick={() => factorWith('rho')} disabled={rsaRunner.busy}>Pollard's rho</button>
      </div>
    {/if}
    <Progress runner={rsaRunner} />
    {#if factor}
      <div class="result">
        <span class="result__label">{factor.method}</span>
        {#if factor.success}
          <div class="result__num">{factor.factor} × {factor.cofactor}</div>
          <div class="result__sub">
            {thousands(factor.steps)} steps in {ms(factor.elapsedMs)}. With p and q, d = e⁻¹ mod (p−1)(q−1) = {factor.d} — the
            private key. Decrypting {factor.ciphertext} with it gives back {factor.recovered}.
          </div>
        {:else}
          <div class="result__sub">
            Gave up after {thousands(factor.steps)} steps ({ms(factor.elapsedMs)}). That is the point: the work grows with the
            size of n.
          </div>
        {/if}
      </div>
    {/if}

    {#if key}
      <h3>How the work grows (log₂ of operations)</h3>
      <div class="tablewrap">
        <table>
          <thead><tr><th>Modulus</th><th>Trial division</th><th>Pollard's rho</th><th>Number field sieve</th><th>NFS on the Bitcoin network</th></tr></thead>
          <tbody>
            {#each key.work as w}
              <tr>
                <td class="n">{w.bits} bits</td>
                <td class="n">{w.trial.toFixed(0)}</td>
                <td class="n">{w.rho.toFixed(0)}</td>
                <td class="n">{w.nfs.toFixed(0)}</td>
                <td class="n">{onBitcoin(w.nfs)}</td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
      <p class="note">
        Rough (constant factors ignored), but the shape is right: 512 bits fell in 1999; 2048 is far out of reach of any
        classical computer.
      </p>
    {/if}
  </section>

  <section class="panel">
    <h3>A strong hash and a weak secret</h3>
    <label class="field">
      <span>A PIN</span>
      <input type="text" bind:value={pin} maxlength="7" />
      <small>SHA-256: <span class="mono">{pinHash}</span></small>
    </label>
    <div class="buttons"><button class="primary" onclick={crackPin} disabled={crackRunner.busy}>Crack it from the hash</button></div>
    {#if pinResult}
      <div class="result">
        <span class="result__label">Brute force over every {pinDigits}-digit PIN</span>
        <div class="result__num">{pinResult.secret ?? 'not found'}</div>
        <div class="result__sub">
          {thousands(pinResult.tried)} of {pinResult.space} tried in {ms(pinResult.elapsedMs)} ({thousands(Math.round(pinResult.rate))}
          a second here). The 2²⁵⁶ outputs of SHA-256 did not matter; the 10^{pinDigits} inputs did.
        </div>
      </div>
    {/if}

    <label class="field">
      <span>A password made the way people make them (a common word, maybe a capital, maybe a number)</span>
      <input type="text" bind:value={password} />
    </label>
    <div class="buttons">
      <button class="primary" onclick={crackWord} disabled={crackRunner.busy}>Dictionary attack (top {thousands(maxWords)} words)</button>
    </div>
    <Progress runner={crackRunner} />
    {#if wordResult}
      <div class="result">
        <span class="result__label">Dictionary attack</span>
        <div class="result__num">{wordResult.secret ?? 'not in this small dictionary'}</div>
        <div class="result__sub">
          {thousands(wordResult.tried)} guesses of {wordResult.space} in {ms(wordResult.elapsedMs)}.
          {#if inBrowser}
            The browser hashes far more slowly than native code, so this list is tiny;
          {:else}
            Even this list is small;
          {/if}
          a real cracker uses millions of words and rules on a graphics card at 10¹⁰ a second.
        </div>
      </div>
    {/if}

    <h3>Key-space calculator</h3>
    <div class="row">
      <label class="field">
        <span>Kind of key</span>
        <select bind:value={calcKind}>
          <option value="power">symbols ^ length</option>
          <option value="factorial">n! (orderings)</option>
          <option value="bits">2 ^ bits</option>
        </select>
      </label>
      {#if calcKind === 'power'}
        <label class="field"><span>Alphabet size</span><input type="number" min="2" max="1000" bind:value={calcA} /></label>
        <label class="field"><span>Length</span><input type="number" min="1" max="200" bind:value={calcB} /></label>
      {:else}
        <label class="field"><span>{calcKind === 'bits' ? 'Bits' : 'n'}</span><input type="number" min="1" max="1000" bind:value={calcB} /></label>
      {/if}
    </div>
    {#if calcError}
      <div class="error">{calcError}</div>
    {/if}
    {#if calc}
      <div class="result">
        <span class="result__label">{calc.description}</span>
        <div class="result__num">{fmtBits(calc.bits)}</div>
        <div class="result__sub">{calc.size} keys · unicity distance ≈ {calc.unicity.toFixed(1)} letters of English</div>
      </div>
      <table>
        <thead><tr><th>Searcher</th><th>Every key</th><th>Verdict</th></tr></thead>
        <tbody>
          {#each calc.times as t}
            <tr>
              <td>{t.attacker}</td>
              <td class="n">{t.all.text}</td>
              <td><span class="chip f-{t.all.feasibility}">{verdict(t.all.feasibility)}</span></td>
            </tr>
          {/each}
        </tbody>
      </table>
      <p class="note">Try 94 symbols × 12 (a random 12-character password), 26! (a substitution alphabet), and 128 bits (AES).</p>
    {/if}
  </section>
</div>
