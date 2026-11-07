<script>
  // A closed-by-default "what am I looking at?" note, placed inside the panel it explains.
  let { title = 'What am I looking at?', children } = $props();
</script>

<details class="explain">
  <summary>{title}</summary>
  <div class="explain__body">
    {@render children()}
  </div>
</details>
