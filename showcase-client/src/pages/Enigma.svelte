<script>
  // The Enigma machine, and the Turing–Welchman bombe that recovers its settings from a crib.
  import { shared, route } from '../lib/state.svelte.js';
  import { ask, inBrowser } from '../lib/api.js';
  import { Runner } from '../lib/Runner.svelte.js';
  import { ms, thousands } from '../lib/format.js';
  import AttackerSelect from '../lib/AttackerSelect.svelte';
  import Funnel from '../lib/Funnel.svelte';
  import Progress from '../lib/Progress.svelte';

  const weather = shared.meta.cases.find((c) => c.cipher === 'Enigma');
  const army = ['I', 'II', 'III', 'IV', 'V'];
  const notches = { I: 'Q', II: 'E', III: 'V', IV: 'J', V: 'Z', VI: 'ZM', VII: 'ZM', VIII: 'ZM' };

  // ---- the machine
  let rotors = $state(['I', 'II', 'III']);
  let reflector = $state('B');
  let rings = $state('AAA');
  let start = $state('ADU');
  let plugs = $state('');
  let simInput = $state('HELLOBLETCHLEY');
  let sim = $state(null);
  let simError = $state(null);

  // Every call goes to the server or the worker, so wait for a pause in the typing.
  let simTimer;
  $effect(() => {
    const request = { rotors: rotors.join(' '), reflector, rings, positions: start, plugs, text: simInput };
    clearTimeout(simTimer);
    simTimer = setTimeout(async () => {
      try {
        sim = await ask('enigma', request);
        simError = null;
      } catch (e) {
        simError = e.message;
      }
    }, 250);
  });

  const doubleStepAt = (w, i) => i >= 1 && w[i][0] !== w[i - 1][0];

  // ---- the bombe
  let bombeCipher = $state(weather.ciphertext);
  let crib = $state('WETTERBERICHT');
  let cribPosition = $state(0);
  let bombeRotors = $state(['I', 'II', 'IV']);
  let language = $state('German');
  let positions = $state([]);
  let totalPlaces = $state(0);
  let menu = $state(null);
  let bombe = $state(null);
  const runner = new Runner();

  const letters = $derived(bombeCipher.toUpperCase().replace(/[^A-Z]/g, ''));
  const cribLength = $derived(crib.toUpperCase().replace(/[^A-Z]/g, '').length);
  const orders = $derived(bombeRotors.length < 3 ? 0 : bombeRotors.length * (bombeRotors.length - 1) * (bombeRotors.length - 2));

  let cribTimer;
  $effect(() => {
    const request = { ciphertext: bombeCipher, crib };
    clearTimeout(cribTimer);
    cribTimer = setTimeout(async () => {
      if (cribLength === 0) {
        positions = [];
        return;
      }
      try {
        const r = await ask('crib-positions', request);
        positions = r.positions;
        totalPlaces = r.total;
      } catch {
        positions = [];
      }
    }, 250);
  });

  $effect(() => {
    const request = { ciphertext: bombeCipher, crib, position: cribPosition };
    if (cribPosition === null || cribLength === 0 || cribPosition + cribLength > letters.length) {
      menu = null;
      return;
    }
    ask('menu', request)
      .then((m) => (menu = m))
      .catch(() => (menu = null));
  });

  function loadWeather() {
    bombeCipher = weather.ciphertext;
    crib = 'WETTERBERICHT';
    cribPosition = 0;
    language = 'German';
    bombe = null;
  }

  function useSimulator() {
    if (!sim) return;
    bombeCipher = sim.output;
    cribPosition = null;
    bombe = null;
  }

  function toggle(name, on) {
    bombeRotors = on ? [...bombeRotors, name] : bombeRotors.filter((r) => r !== name);
  }

  async function runBombe() {
    bombe = null;
    bombe = await runner.run('bombe', {
      ciphertext: bombeCipher,
      crib,
      position: cribPosition,
      rotors: army.filter((r) => bombeRotors.includes(r)),
      reflector,
      language,
    });
  }

  if (route.query.has('case')) loadWeather();

  // The menu drawn as a graph: letters round a circle, one line per crib position.
  const W = 420, H = 300, R = 115;
  const graph = $derived.by(() => {
    if (!menu) return null;
    const at = {};
    menu.letters.forEach((l, i) => {
      const a = (2 * Math.PI * i) / menu.letters.length - Math.PI / 2;
      at[l] = [W / 2 + R * Math.cos(a), H / 2 + R * Math.sin(a)];
    });
    const seen = {};
    const edges = menu.links.map((link) => {
      const [x1, y1] = at[link.plain];
      const [x2, y2] = at[link.cipher];
      const key = [link.plain, link.cipher].sort().join('');
      const n = (seen[key] = (seen[key] ?? -1) + 1);
      return { x1, y1, x2, y2, lx: (x1 + x2) / 2 + n * 12, ly: (y1 + y2) / 2 - 3 + n * 11, label: link.offset };
    });
    return { at, edges };
  });
</script>

<header class="masthead">
  <h1>Enigma, and the <span>bombe</span> that broke it</h1>
  <p>
    A three-rotor Enigma has about 10<sup>23</sup> settings — 77 bits, far beyond any brute force in 1940 and well beyond a
    laptop today. Almost all of that is the plugboard. Turing and Welchman's insight was that, given a <b>crib</b>, you need
    never search the plugboard at all: suppose one plug, follow the consequences through the crib, and a wrong supposition
    contradicts itself. That leaves the 60 rotor orders × 17,576 positions — about a million — for a machine to grind through.
  </p>
</header>

<div class="workspace even">
  <section class="panel">
    <h3>The machine</h3>
    <div class="row">
      {#each ['Left', 'Middle', 'Right'] as label, k}
        <label class="field">
          <span>{label} rotor</span>
          <select bind:value={rotors[k]}>
            {#each shared.meta.rotors as r}<option value={r}>{r} (notch {notches[r]})</option>{/each}
          </select>
        </label>
      {/each}
      <label class="field">
        <span>Reflector</span>
        <select bind:value={reflector}><option>B</option><option>C</option></select>
      </label>
    </div>
    <div class="row">
      <label class="field"><span>Rings</span><input type="text" maxlength="3" bind:value={rings} /></label>
      <label class="field"><span>Start position</span><input type="text" maxlength="3" bind:value={start} /></label>
    </div>
    <label class="field">
      <span>Plugboard (pairs)</span>
      <input type="text" bind:value={plugs} placeholder="e.g. AB CD EF" />
    </label>
    <label class="field">
      <span>Type here (Enigma is its own inverse: type the ciphertext with the same settings to decrypt)</span>
      <textarea bind:value={simInput} rows="3"></textarea>
    </label>
    {#if simError}
      <div class="error">{simError}</div>
    {:else if sim}
      <div class="result">
        <span class="result__label">Lamps</span>
        <div class="output">{sim.grouped}</div>
      </div>
      <div class="field">
        <span>Window letters after each key press — gold where the double step carried the left rotor on (the middle rotor moves twice running)</span>
        <div class="windows">
          {#each sim.windows.slice(0, 120) as w, i}
            <span class:step2={doubleStepAt(sim.windows, i)}>{w}</span>
          {/each}
        </div>
      </div>
    {/if}
    <p class="note">
      Try rotors I II III, start ADU, and type a few letters: the windows go ADV, AEW, BFX — the middle rotor moves twice in a
      row and takes the left one with it. Notice too that no letter ever lights itself.
    </p>
  </section>

  <section class="panel">
    <h3>The bombe</h3>
    <div class="buttons">
      <button onclick={loadWeather}>Load the WETTERBERICHT case</button>
      <button onclick={useSimulator}>Use the machine's output</button>
    </div>
    <label class="field">
      <span>Intercepted ciphertext</span>
      <textarea bind:value={bombeCipher} rows="4"></textarea>
    </label>
    <label class="field">
      <span>Crib — a word you believe is in the message</span>
      <input type="text" bind:value={crib} />
    </label>
    <div class="field">
      <span>
        Where can it go? No letter enciphers to itself, so the crib fits at {positions.length} of {totalPlaces} places. Click one.
      </span>
      <div class="ctext">
        {#each letters as ch, k}
          {#if cribPosition !== null && k >= cribPosition && k < cribPosition + cribLength}
            <mark class="sel">{ch}</mark>
          {:else if positions.includes(k)}
            <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_noninteractive_element_interactions -->
            <mark class="pos" title="crib could start here" onclick={() => (cribPosition = k)}>{ch}</mark>
          {:else}
            <span>{ch}</span>
          {/if}
        {/each}
      </div>
    </div>

    {#if menu && graph}
      <div class="field">
        <span>The menu: crib letters joined by the position at which they encipher to each other. {menu.loops} loop(s); test letter {menu.testLetter}.</span>
        <svg class="chart menu-graph" viewBox="0 0 {W} {H}" role="img" aria-label="The bombe menu as a graph">
          {#each graph.edges as e}
            <line x1={e.x1} y1={e.y1} x2={e.x2} y2={e.y2} />
            <text class="edge" x={e.lx} y={e.ly}>{e.label}</text>
          {/each}
          {#each menu.letters as l}
            <circle class:test={l === menu.testLetter} cx={graph.at[l][0]} cy={graph.at[l][1]} r="14" />
            <text class="node" x={graph.at[l][0]} y={graph.at[l][1]}>{l}</text>
          {/each}
        </svg>
        {#if menu.loops < 2}
          <p class="note" style="color:#ffb27a">
            Few loops: wrong plug guesses rarely contradict themselves, so expect many false stops. Real menu-makers chose cribs
            and positions to get three or more.
          </p>
        {/if}
      </div>
    {/if}

    <div class="field">
      <span>Rotors that might be in the machine</span>
      <div class="buttons">
        {#each army as name}
          <label><input type="checkbox" checked={bombeRotors.includes(name)} onchange={(e) => toggle(name, e.currentTarget.checked)} /> {name}</label>
        {/each}
      </div>
      <small>
        {orders} orders × 17,576 positions = {thousands(orders * 17576)} positions. Native .NET does I, II, IV (6 orders) in about
        0.6 s and all five (60 orders) in about 3 s{inBrowser
          ? '; in the browser the 6-order search takes about 5 seconds, so allow a minute or so for all five'
          : ''}.
      </small>
    </div>
    <label class="field">
      <span>Language of the traffic</span>
      <select bind:value={language}><option>English</option><option>German</option></select>
    </label>
    <div class="buttons">
      <button class="primary" onclick={runBombe} disabled={runner.busy || cribPosition === null || orders === 0}>Run the bombe</button>
    </div>
    <Progress {runner} />
  </section>
</div>

{#if bombe}
  <div class="workspace" style="margin-top:1.25rem">
    <section class="panel">
      <h3>{bombe.solved ? 'Solved' : 'No solution'} — {thousands(bombe.positionsTested)} positions in {ms(bombe.elapsedMs)}</h3>
      {#if bombe.solved}
        <div class="result">
          <span class="result__label">Recovered settings</span>
          <div class="output">{bombe.settings}</div>
        </div>
        <div class="result">
          <span class="result__label">Plaintext</span>
          <div class="output">{bombe.plaintext}</div>
        </div>
      {/if}
      <h3>Stops ({bombe.stopCount})</h3>
      <ol class="log">
        {#each bombe.stops.slice(0, 20) as stop}<li>{stop}</li>{/each}
      </ol>
      <h3>Working</h3>
      <ol class="log">
        {#each bombe.log as line}<li>{line}</li>{/each}
      </ol>
    </section>
    <section class="panel">
      <AttackerSelect />
      <Funnel difficulty={bombe.difficulty} />
    </section>
  </div>
{/if}
