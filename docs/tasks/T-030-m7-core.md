# T-030: M7 core — buyable classes, weapon evolutions, new enemies

- **Milestone:** M7 (D-038)
- **Owner:** Claude (PC)
- **Status:** done

## Scope (Core + CoreTests only)

- `SurvivorDefaults.Mage()` / `Archer()` / `ForClass(id)`: own HP, speed, weapon pool, starting weapon and
  4 active skills. New skill kinds: `Projectile` (optional pierce or explosion), `Teleport`, `AreaBurst`;
  `Dash.DashBackward`; `Block.BlockAllDirections`.
- Catalog 14–19 (Mage) and 20–25 (Archer) weapons; patterns `Strike` and `Fan`.
- Evolutions 40–57: `SurvivorCatalog.EvolutionOf`, chest evolves the first max-level weapon whose paired
  passive is owned (`WeaponEvolved` event); offers skip evolved-away bases.
- Enemies 5 Exploder, 6 Ghost, 7 Necromancer (minute 5+, never elite). `DeathCause.Explosion`.
- Observation v4 unchanged in size (2264); inventory slot = level / MaxLevel.
- `ProfileRules.TryBuyClass` / `TrySelectClass` / `OwnsClass`; `SurvivorEnvFactory.CreateClass` uses
  the class kit.

## Acceptance

- CoreTests green (232), including `SurvivorM7Tests` and the M5 goldens: the first 300 s of the
  invulnerable run match the pre-M7 code bit for bit; the full run is re-pinned on M7.
