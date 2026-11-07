<script>
  // Choose who is doing the searching; every time on the page is quoted for them.
  import { shared } from './state.svelte.js';
  import { rate } from './format.js';
</script>

{#if shared.meta}
  <label class="field">
    <span>Who is searching?</span>
    <select bind:value={shared.attacker}>
      {#each shared.meta.attackers as a, i}
        <option value={i}>{a.name} — {rate(a.rate)}/s</option>
      {/each}
    </select>
  </label>
{/if}
