# Vendored from RADD.Statistics

These files are **vendored** — a tracked copy of part of a separately developed library, not code
authored in this repository. Keep divergence to the namespace rename only.

| | |
|---|---|
| **Upstream library** | `RADD.Statistics` |
| **Local clone** | `C:\Source\radd_azrdevops\RADD.DesktopSuite\libs\RADD.Statistics` |
| **Upstream repository** | RADD.DesktopSuite (Azure DevOps, RADD Solutions) |
| **Synced baseline commit** | `6f620825a677cf1cc27c36c0f6414bb60081149c` (last commit touching `libs/RADD.Statistics`) |
| **Imported on** | 2026-11-07 |
| **Last synced** | 2026-11-11 |

## What was taken, and why

| File here | Upstream file | Used for |
|---|---|---|
| `SpecialFunctions.cs` | `Special/SpecialFunctions.cs` | `LogFactorial` for the size of factorial key spaces (`KeySpace.Log2Factorial`); the incomplete gamma behind chi-squared p-values |
| `ChiSquaredDistribution.cs` | `Distributions/ChiSquaredDistribution.cs` | `FrequencyAnalysis.ChiSquaredPValue` — "how plausibly English are these letters?" |
| `IContinuousDistribution.cs` | `Distributions/IContinuousDistribution.cs` | the interface (and argument guards) `ChiSquaredDistribution` implements |
| `NormalDistribution.cs` | `Distributions/NormalDistribution.cs` | needed by `SpecialFunctions.InverseErf` |
| `Solve.cs` | `Special/Solve.cs` | quantile inversion used by the distributions |

Only the files needed were taken; the rest of RADD.Statistics (tests, regression, resampling…) is not
copied.

## The only intended difference: namespacing

The copy differs from upstream **solely** by the reversible token substitution

```
RADD.Statistics  ⟷  RP.CodeBreaker.Statistics
```

applied to every `*.cs` here. The RADD Solutions copyright line at the top of each file is kept as it
is upstream. **No source logic is changed on import.** Normalised by reversing the substitution, every
file should be identical to its upstream counterpart (bar line endings and byte-order marks).

## Copyright and permission

These files are RADD Solutions Limited's code. **RADD Solutions Limited has allowed them to be
published in this repository** (November 2026). Each file keeps its original copyright header,
`// Copyright (c) 2026 RADD Solutions Limited. …`, so their ownership stays on record; the header must be
kept on every sync and never removed or rewritten. The rest of the repository is under the MIT licence (`LICENSE.txt`).

The two-way sync protocol is in the repository-root `CLAUDE.md`.
