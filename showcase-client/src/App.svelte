<script>
  import { route, shared } from './lib/state.svelte.js';
  import { inBrowser } from './lib/api.js';
  import Home from './pages/Home.svelte';
  import Convert from './pages/Convert.svelte';
  import Analyse from './pages/Analyse.svelte';
  import Break from './pages/Break.svelte';
  import Enigma from './pages/Enigma.svelte';
  import Famous from './pages/Famous.svelte';
  import Modern from './pages/Modern.svelte';

  const pages = [
    { path: '', label: 'Difficulty ladder', component: Home },
    { path: 'convert', label: 'Convert', component: Convert },
    { path: 'analyse', label: 'Analyse', component: Analyse },
    { path: 'break', label: 'Break', component: Break },
    { path: 'enigma', label: 'Enigma & bombe', component: Enigma },
    { path: 'famous', label: 'Famous cases', component: Famous },
    { path: 'modern', label: 'Modern', component: Modern },
  ];

  const page = $derived(pages.find((p) => p.path === route.path) ?? pages[0]);

  $effect(() => {
    document.title = `RP.CodeBreaker — ${page.label.toLowerCase()}`;
  });
</script>

<nav class="topnav">
  <span class="topnav__brand">RP.<span>CodeBreaker</span></span>
  {#each pages as p}
    <a class="topnav__link" class:active={p === page} href="#/{p.path}">{p.label}</a>
  {/each}
  <a class="topnav__link topnav__ext" href="https://github.com/dickiepotter/CodeBreaker#readme" target="_blank" rel="noopener">Tutorial ↗</a>
</nav>

<main>
  {#if shared.error}
    <div class="error">The code-breaking engine could not start: {shared.error}</div>
  {:else if !shared.meta}
    <p class="loading">
      {inBrowser
        ? 'Loading the .NET runtime and RP.CodeBreaker into a Web Worker…'
        : 'Connecting to the RP.CodeBreaker server…'}
    </p>
  {:else}
    {#key route.path}
      <page.component />
    {/key}
  {/if}
  <p class="engine" style="margin-top:2.5rem">
    {#if inBrowser}
      Every answer on this site is worked out <b>in your browser</b>, by the C# library compiled to WebAssembly and
      running in a Web Worker — interpreted, so the heavy searches are slower here than the native timings the tutorial quotes.
    {:else}
      Answers are worked out <b>on the server</b>, by the C# library running as native .NET.
    {/if}
  </p>
</main>
