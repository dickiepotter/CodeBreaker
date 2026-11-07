<script>
  // The funnel: one bar per step from the full key space down to the work that was really needed.
  // Widths are proportional to bits (log₂ of the keys left), so each halving of the search is the same
  // step to the left wherever it happens.
  import { shared } from './state.svelte.js';
  import { bits, verdict } from './format.js';
  import Explain from './Explain.svelte';

  let { difficulty } = $props();

  const startBits = $derived(Math.max(1, difficulty.start.bits));
  const times = $derived(difficulty.times[shared.attacker]);
  const attacker = $derived(shared.meta?.attackers[shared.attacker]);
</script>

<div class="funnel">
  <div class="funnel__step">
    <div class="funnel__head">
      <b>Start: {difficulty.start.description}</b>
      <span>{difficulty.start.size} keys · {bits(difficulty.start.bits)} · {times.naive.text} ({verdict(times.naive.feasibility)})</span>
    </div>
    <div class="funnel__bar"><div style="width:100%"></div></div>
  </div>
  {#each difficulty.steps as step, i}
    <div class="funnel__step">
      <div class="funnel__head">
        <b>→ {step.insight}</b>
        <span>{step.size} keys · {bits(step.bits)} · saves {bits(step.bitsSaved)}</span>
      </div>
      <div class="funnel__bar"><div style="width:{Math.max(1.5, (100 * step.bits) / startBits)}%"></div></div>
      <div class="funnel__why">{step.why}</div>
    </div>
  {/each}
  <p class="note">
    In all, the insights saved <b>{bits(difficulty.bitsSaved)}</b> of work — the search shrank by a factor of
    2<sup>{Math.round(difficulty.bitsSaved)}</sup>. What is left takes <b>{times.remaining.text}</b>
    ({verdict(times.remaining.feasibility)}) for {attacker?.name ?? 'the chosen searcher'}, against
    {times.naive.text} for the naive brute force.
  </p>
  <Explain title="How do I read the funnel?">
    <p>
      The <b>top bar</b> is every key the cipher could have — the work of brute force. Each bar <b>below</b> is one thing the
      attack learned (from the statistics, or from your crib) and the number of keys still in play after it. The line under
      each says why that insight is worth what it is.
    </p>
    <p>
      Bar lengths are in <b>bits</b>, so halving the keys left shortens a bar by the same amount wherever it happens, and a
      bar half as long means not half the keys but the <i>square root</i> of them. "Saves 20 bits" means the search got
      about a million times smaller.
    </p>
    <p>Change "Who is searching?" above to see what the remaining work means for a person, a bombe or a graphics card.</p>
  </Explain>
</div>
