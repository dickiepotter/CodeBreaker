<script>
  // Codes and ciphers, both ways: every code and every cipher in the library, converted through the API.
  import { ask } from '../lib/api.js';
  import { shared } from '../lib/state.svelte.js';
  import { bits } from '../lib/format.js';

  const codes = shared.meta.codes;
  const ciphers = shared.meta.ciphers;

  let codeIndex = $state(0);
  let codePlain = $state('SOS SEND HELP');
  let codeCoded = $state('');
  let codeError = $state(null);

  let cipherIndex = $state(0);
  let cipherKey = $state(ciphers[0].exampleKey);
  let cipherPlain = $state('Attack at dawn. The enemy is weakest just before first light.');
  let cipherText = $state('');
  let cipherError = $state(null);
  let keySpace = $state(null); // { text, bits }

  const code = $derived(codes[codeIndex]);
  const definition = $derived(ciphers[cipherIndex]);

  async function convertCode(direction) {
    try {
      const r = await ask('convert', {
        kind: 'code',
        name: code.name,
        direction,
        text: direction === 'encode' ? codePlain : codeCoded,
      });
      if (direction === 'encode') codeCoded = r.output;
      else codePlain = r.output;
      codeError = null;
    } catch (e) {
      codeError = e.message;
    }
  }

  async function convertCipher(direction) {
    try {
      const r = await ask('convert', {
        kind: 'cipher',
        name: definition.name,
        key: cipherKey,
        direction,
        text: direction === 'encrypt' ? cipherPlain : cipherText,
      });
      if (direction === 'encrypt') cipherText = r.output;
      else cipherPlain = r.output;
      keySpace = r.keySpace ? { text: r.keySpace, bits: r.keySpaceBits } : null;
      cipherError = null;
    } catch (e) {
      cipherError = e.message;
    }
  }

  // Key space as soon as the cipher, key or text changes (debounced: in the browser each call goes to the worker).
  let timer;
  $effect(() => {
    const request = { kind: 'cipher', name: definition.name, key: cipherKey, direction: 'encrypt', text: cipherPlain };
    clearTimeout(timer);
    timer = setTimeout(async () => {
      try {
        const r = await ask('convert', request);
        keySpace = r.keySpace ? { text: r.keySpace, bits: r.keySpaceBits } : null;
      } catch {
        keySpace = null;
      }
    }, 250);
    return () => clearTimeout(timer);
  });

  const letters = (s) => (s ?? '').replace(/[^A-Za-z]/g, '').length;

  function cipherChanged(e) {
    cipherIndex = Number(e.target.value);
    cipherKey = definition.name === 'One-time pad' ? randomLetters(Math.max(26, letters(cipherPlain))) : definition.exampleKey;
    cipherError = null;
  }

  // ---- random keys (just choosing letters and numbers; no cryptography needed here) ----
  const rnd = (n) => Math.floor(Math.random() * n);
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
  const word = (n) => Array.from({ length: n }, () => alphabet[rnd(26)]).join('');
  const shuffle = (items) => {
    const a = [...items];
    for (let i = a.length - 1; i > 0; i--) {
      const j = rnd(i + 1);
      [a[i], a[j]] = [a[j], a[i]];
    }
    return a;
  };
  function randomLetters(n) {
    const bytes = crypto.getRandomValues(new Uint32Array(n));
    return Array.from(bytes, (b) => alphabet[b % 26]).join('');
  }
  const gcd = (a, b) => (b === 0 ? Math.abs(a) : gcd(b, a % b));
  function randomHill() {
    for (;;) {
      const n = [rnd(26), rnd(26), rnd(26), rnd(26)];
      const det = (((n[0] * n[3] - n[1] * n[2]) % 26) + 26) % 26;
      if (gcd(det, 26) === 1) return n.join(' ');
    }
  }
  function randomEnigma() {
    const rotors = shuffle(['I', 'II', 'III', 'IV', 'V']).slice(0, 3).join(' ');
    const l = () => alphabet[rnd(26)];
    const plugLetters = shuffle(alphabet.split('')).slice(0, 20);
    const plugs = Array.from({ length: 10 }, (_, i) => plugLetters[2 * i] + plugLetters[2 * i + 1]).join(' ');
    return `${rotors} | B | ${l()}${l()}${l()} | ${l()}${l()}${l()} | ${plugs}`;
  }
  const affineMultipliers = [1, 3, 5, 7, 9, 11, 15, 17, 19, 21, 23, 25];

  function randomise() {
    const make =
      {
        Caesar: () => String(1 + rnd(25)),
        Affine: () => `${affineMultipliers[rnd(12)]}, ${rnd(26)}`,
        'Simple substitution': () => shuffle(alphabet.split('')).join(''),
        'Rail fence': () => String(2 + rnd(6)),
        'Hill (2×2)': randomHill,
        'One-time pad': () => randomLetters(Math.max(26, letters(cipherPlain))),
        Bifid: () => `${word(8)}, 5`,
        'Keyed Vigenère (Quagga)': () => `${word(7)}, ${word(7)}`,
        ADFGVX: () => `${word(8)}, ${word(6)}`,
        Enigma: randomEnigma,
        ROT13: () => '',
        Atbash: () => '',
      }[definition.name] ?? (() => word(5 + rnd(4)));
    cipherKey = make();
  }

  // Brute force on a laptop (10^7 keys a second), from the key space's bits.
  function laptopTime(b) {
    const log10Seconds = b * Math.log10(2) - 7;
    if (log10Seconds < -3) return 'under a millisecond';
    const s = 10 ** log10Seconds;
    if (s < 1) return `${Math.round(s * 1000)} milliseconds`;
    if (s < 120) return `${s.toFixed(1)} seconds`;
    if (s < 7200) return `${Math.round(s / 60)} minutes`;
    if (s < 172800) return `${Math.round(s / 3600)} hours`;
    const log10Years = log10Seconds - Math.log10(365.25 * 86400);
    if (log10Years < 0) return `${Math.round(s / 86400)} days`;
    if (log10Years < 6) return `${Math.round(10 ** log10Years).toLocaleString('en-GB')} years`;
    const e = Math.floor(log10Years);
    return `${(10 ** (log10Years - e)).toFixed(2)} × 10^${e} years`;
  }
</script>

<header class="masthead">
  <h1>Codes and ciphers, <span>both ways</span></h1>
  <p>
    A <b>code</b> swaps whole symbols using a table — Morse, ASCII, a diplomatic codebook — and has no key: the table is
    the whole secret, or no secret at all. A <b>cipher</b> applies a rule with a secret key, letter by letter. Type into
    either box and convert in either direction; every code and every cipher in the library is here.
  </p>
</header>

<div class="workspace even">
  <section class="panel">
    <h3>Codes</h3>
    <label class="field">
      <span>Code</span>
      <select bind:value={codeIndex} onchange={() => (codeError = null)}>
        {#each codes as c, i}
          <option value={i}>{c.name}</option>
        {/each}
      </select>
      <small>{code.description}</small>
    </label>
    <label class="field">
      <span>Plain text</span>
      <textarea bind:value={codePlain}></textarea>
    </label>
    <div class="buttons">
      <button class="primary" onclick={() => convertCode('encode')}>Encode ↓</button>
      <button onclick={() => convertCode('decode')}>↑ Decode</button>
    </div>
    <label class="field">
      <span>Coded</span>
      <textarea bind:value={codeCoded}></textarea>
    </label>
    {#if codeError}
      <div class="error">{codeError}</div>
    {/if}
    {#if code.phrases}
      <div class="field">
        <span>The book (anything not in it is spelled out letter by letter as 9xx groups)</span>
        <div class="windows">
          {#each code.phrases as p}<span>{p.phrase} = {p.group}</span>{/each}
        </div>
      </div>
    {/if}
  </section>

  <section class="panel">
    <h3>Ciphers</h3>
    <label class="field">
      <span>Cipher</span>
      <select value={cipherIndex} onchange={cipherChanged}>
        {#each ciphers as c, i}
          <option value={i}>{c.name}</option>
        {/each}
      </select>
    </label>
    <label class="field">
      <span>Key</span>
      <input type="text" bind:value={cipherKey} />
      <small>{definition.keyFormat}</small>
    </label>
    <label class="field">
      <span>Plaintext</span>
      <textarea bind:value={cipherPlain}></textarea>
    </label>
    <div class="buttons">
      <button class="primary" onclick={() => convertCipher('encrypt')}>Encrypt ↓</button>
      <button onclick={() => convertCipher('decrypt')}>↑ Decrypt</button>
      <button onclick={randomise} title="A random key of a sensible kind">Random key</button>
    </div>
    <label class="field">
      <span>Ciphertext</span>
      <textarea bind:value={cipherText}></textarea>
    </label>
    {#if cipherError}
      <div class="error">{cipherError}</div>
    {/if}
    {#if keySpace}
      <div class="result">
        <span class="result__label">How many keys could it have been?</span>
        <div class="result__num">{bits(keySpace.bits)}</div>
        <div class="result__sub">{keySpace.text}. Brute force on a laptop: {laptopTime(keySpace.bits)}.</div>
      </div>
    {/if}
    <p class="note">
      Letter-for-letter ciphers (Caesar, Vigenère, substitution…) keep your spaces and punctuation; the others work on the
      letters alone, as the clerks did, and send them in a single run.
    </p>
  </section>
</div>
