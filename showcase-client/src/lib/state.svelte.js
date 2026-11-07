// State shared by every page: the route, who is searching, and the library's metadata (the cipher
// catalogue, the famous cases, the lists of codes, ciphers and rotors), loaded once.

import { ask } from './api.js';

/** Hash routing ("#/break?case=1"), so the static site needs no server-side rewrites. */
function parse() {
  const hash = location.hash.replace(/^#\/?/, '');
  const [path, query = ''] = hash.split('?');
  return { path: path || '', query: new URLSearchParams(query) };
}

export const route = $state(parse());
window.addEventListener('hashchange', () => Object.assign(route, parse()));

/** Go to a page: navigate('break', { case: 1 }). */
export function navigate(path, query = {}) {
  const q = new URLSearchParams(query).toString();
  location.hash = `#/${path}${q ? `?${q}` : ''}`;
}

/** Everything the pages share. `attacker` is an index into meta.attackers (2 = a laptop). */
export const shared = $state({ meta: null, error: null, attacker: 2 });

export const metaLoaded = ask('meta')
  .then((m) => (shared.meta = m))
  .catch((e) => (shared.error = e.message));
