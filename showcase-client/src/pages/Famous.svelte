<script>
  // Two thousand years of codes made and broken, each with a message to work on.
  import { ask } from '../lib/api.js';
  import { shared, navigate } from '../lib/state.svelte.js';

  const cases = shared.meta.cases;

  // The ciphers the Break page has an attack for.
  const breakable = ['Caesar', 'Simple substitution', 'Vigenère', 'Playfair', 'Keyed Vigenère (Quagga)', 'Repeating-key XOR'];

  let revealed = $state({}); // index → decoded text (or an error)
  let venonaOpen = $state(false);
  let venonaCrib = $state('THE');
  let drags = $state([]);
  let dragError = $state(null);

  async function reveal(i) {
    const c = cases[i];
    try {
      const r =
        c.cipher === 'Codebook'
          ? await ask('convert', { kind: 'code', name: 'Codebook', direction: 'decode', text: c.ciphertext })
          : await ask('convert', { kind: 'cipher', name: c.cipher, key: c.key, direction: 'decrypt', text: c.ciphertext });
      revealed[i] = r.output;
    } catch (e) {
      revealed[i] = e.message;
    }
  }

  const venona = cases.find((c) => c.cipher === 'One-time pad (reused)');

  // Drag the crib as you type, debounced: in the browser every call goes to the worker.
  let timer;
  $effect(() => {
    if (!venonaOpen || !venona) return;
    const crib = venonaCrib;
    const [first, second] = venona.ciphertext.split('|');
    clearTimeout(timer);
    if (crib.replace(/[^A-Za-z]/g, '').length < 2) {
      drags = [];
      return;
    }
    timer = setTimeout(async () => {
      try {
        drags = (await ask('two-time-pad', { first, second, crib, top: 8 })).results;
        dragError = null;
      } catch (e) {
        dragError = e.message;
      }
    }, 250);
    return () => clearTimeout(timer);
  });
</script>

<header class="masthead">
  <h1>Famous <span>cases</span></h1>
  <p>
    Two thousand years of codes made and broken, each with a message to work on. Where the real ciphertext is public and
    short (Kryptos) it is used as it stands; otherwise the message is new text in the spirit of the original, enciphered
    here, so you can break it with the same method that broke the real thing. Notice how often the break came not from the
    cipher's mathematics but from a habit, a repeated key, or a word everybody knew would be there.
  </p>
</header>

<div class="cards">
  {#each cases as c, i}
    <article class="card">
      <div>
        <h3>{c.title} {#if c.historical}<span class="badge">historical ciphertext</span>{/if}</h3>
        <div class="year">{c.year} · {c.cipher}</div>
      </div>
      <p class="desc">{c.story}</p>
      <div class="ct">{c.ciphertext}</div>
      {#if c.cribs.length > 0}
        <p class="note">Cribs the context suggests: <b>{c.cribs.join(', ')}</b></p>
      {/if}
      <p class="note"><b>How it breaks:</b> {c.howToBreak}</p>
      <div class="buttons">
        {#if c.cipher === 'Enigma'}
          <button class="primary" onclick={() => navigate('enigma', { case: i })}>Run the bombe →</button>
        {:else if c.cipher === 'One-time pad (reused)'}
          <button class="primary" onclick={() => (venonaOpen = !venonaOpen)}>Drag a crib</button>
        {:else if breakable.includes(c.cipher)}
          <button class="primary" onclick={() => navigate('break', { case: i })}>Break it →</button>
        {:else}
          <button onclick={() => reveal(i)}>Decode with the known key</button>
        {/if}
      </div>
      {#if revealed[i] !== undefined}
        <div class="result">
          <span class="result__label">With the key ({c.key})</span>
          <div class="output">{revealed[i]}</div>
          <div class="result__sub">There is no automatic attack on this one here — which is itself the lesson.</div>
        </div>
      {/if}
      {#if c.cipher === 'One-time pad (reused)' && venonaOpen}
        <div class="result">
          <span class="result__label">Crib dragging across two messages on one pad</span>
          <label class="field">
            <span>A word you think is in message one</span>
            <input type="text" bind:value={venonaCrib} />
          </label>
          {#if dragError}
            <div class="error">{dragError}</div>
          {/if}
          {#if drags.length > 0}
            <table>
              <thead><tr><th>At</th><th>Message two there</th><th>Reads?</th></tr></thead>
              <tbody>
                {#each drags as d}
                  <tr><td class="n">{d.position}</td><td class="n">{d.other}</td><td class="n">{d.readability.toFixed(2)}</td></tr>
                {/each}
              </tbody>
            </table>
            <p class="note">
              c₁ − c₂ = p₁ − p₂: the pad has cancelled. Where your guess is right, message two reads as English. Try THE, then
              extend: THESOURCE, MEETTHE, COURIER…
            </p>
          {/if}
        </div>
      {/if}
    </article>
  {/each}
</div>
