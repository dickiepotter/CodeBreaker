<script>
  // The progress bar, elapsed time, cancel button and error for a Runner.
  import { inBrowser } from './api.js';
  import { ms } from './format.js';

  let { runner } = $props();
</script>

{#if runner.busy}
  <div class="progress"><div style="width:{Math.round(100 * (runner.progress?.fraction ?? 0))}%"></div></div>
  <div class="row" style="justify-content:space-between;align-items:center">
    <span class="note">{runner.progress?.message ?? 'Working…'} · {ms(runner.elapsed)}</span>
    <button onclick={() => runner.cancel()}>Cancel</button>
  </div>
  {#if runner.progress?.best}
    <pre class="output">{runner.progress.best}</pre>
  {/if}
  {#if inBrowser}
    <p class="note">Running in a Web Worker, so the page stays responsive. Cancelling stops the worker; the next run starts a fresh one.</p>
  {/if}
{/if}
{#if runner.error}
  <div class="error">{runner.error}</div>
{/if}
