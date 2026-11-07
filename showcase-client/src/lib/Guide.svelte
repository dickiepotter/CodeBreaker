<script>
  // "How to use this page": a short numbered walk-through at the top of a page. Open until the visitor
  // closes it; that choice is remembered per page (a convenience only — it works without storage).
  let { id, title = 'How to use this page', children } = $props();

  const storageKey = () => `codebreaker.guide.${id}`;
  let open = $state(true);
  try {
    open = localStorage.getItem(storageKey()) !== 'closed';
  } catch {
    /* no storage: always open */
  }

  function toggled(e) {
    open = e.currentTarget.open;
    try {
      localStorage.setItem(storageKey(), open ? 'open' : 'closed');
    } catch {
      /* ignore */
    }
  }
</script>

<details class="guide" {open} ontoggle={toggled}>
  <summary>{title}</summary>
  <div class="guide__body">
    {@render children()}
  </div>
</details>
