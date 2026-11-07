// Small display helpers shared by the pages.

export const bits = (b) => `${Number(b).toFixed(1)} bits`;

export const num = (v, digits = 4) => Number(v).toFixed(digits);

export const ms = (t) => (t < 1000 ? `${Math.round(t)} ms` : `${(t / 1000).toFixed(1)} s`);

export const thousands = (n) => Number(n).toLocaleString('en-GB');

/** The words for a Feasibility verdict. */
export const verdict = (f) =>
  ({ Instant: 'instant', FewMinutes: 'a few minutes', Hours: 'hours', Months: 'days to months', Lifetime: 'a lifetime' })[f] ??
  'never';

/** The colour for a Feasibility verdict (matches the .f-* classes in app.css). */
export const colour = (f) =>
  ({ Instant: '#4be0a8', FewMinutes: '#9be04b', Hours: '#ffce47', Months: '#ffa04b', Lifetime: '#ff7a7a' })[f] ?? '#c06bff';

/** 10^7 for big rates, 0.1 for small. */
export const rate = (r) => (r >= 1000 ? `10^${Number(Math.log10(r).toFixed(1))}` : `${r}`);

/** Split a comma-separated list of cribs. */
export const cribs = (text) =>
  (text ?? '')
    .split(',')
    .map((c) => c.trim())
    .filter((c) => c.length > 0);
