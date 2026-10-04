using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>Turns the sim's events of one tick into effects, damage numbers and sounds.</summary>
    public sealed partial class SurvivorRenderer
    {
        // ------------------------------------------------------------------ events

        private void ReadEvents()
        {
            IReadOnlyList<SurvivorEvent> events = sim.Events;
            Vector3 heroPosition = heroCurrent;
            BeginM9Events();
            for (int i = 0; i < events.Count; i++)
            {
                SurvivorEvent e = events[i];
                switch (e.Type)
                {
                    case SurvivorEventType.WeaponFired:
                        OnWeaponFired(e, heroPosition);
                        break;
                    case SurvivorEventType.DamageDealt:
                    {
                        bool crit = i > 0 && events[i - 1].Type == SurvivorEventType.Crit && events[i - 1].Id == e.Id;
                        OnDamageDealt(e, crit);
                        break;
                    }
                    case SurvivorEventType.EnemyKilled:
                        OnEnemyKilled(e);
                        break;
                    case SurvivorEventType.EliteKilled:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, EliteColor, 3.2f, 0.6f);
                        if (!StoreArea(StoreFx.PoisonExplode, point + Vector3.up * 0.6f, 2.2f, 1.8f))
                        {
                            effects.Sparkle(point, EliteColor, 18, 0.8f, 2.2f, 0.26f);
                        }
                        break;
                    }
                    case SurvivorEventType.BossKilled:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, GoldColor, 9f, 1.1f);
                        effects.Flash(point + Vector3.up, Color.white, 6f, 0.4f);
                        if (!StoreArea(StoreFx.MagicCircleExplode, point + Vector3.up * 0.3f, 5f, 2.6f))
                        {
                            effects.Sparkle(point, GoldColor, 40, 2f, 3f, 0.35f);
                        }
                        effects.Text(point + Vector3.up * 3.5f, "TRÙM ĐÃ GỤC!", GoldColor, 2f, 2f);
                        break;
                    }
                    case SurvivorEventType.EliteSpawned:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, EliteColor, 3f, 0.7f);
                        if (!StoreArea(StoreFx.SummonCircle2, point + Vector3.up * 0.1f, 2f, 2f))
                        {
                            effects.Puff(point, new Color(0.45f, 0.3f, 0.6f, 0.6f), 10, 1f, 1.8f, 0.9f, 0.8f);
                        }
                        break;
                    }
                    case SurvivorEventType.BossSpawned:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        effects.Shockwave(point, BossColor, 8f, 1.2f);
                        if (!StoreArea(StoreFx.SummonCircle2, point + Vector3.up * 0.1f, 4.5f, 2.6f))
                        {
                            effects.Puff(point, new Color(0.35f, 0.1f, 0.12f, 0.7f), 24, 1.6f, 3.5f, 1.4f, 1f);
                        }
                        effects.Text(heroPosition + Vector3.up * 3f, "TRÙM XUẤT HIỆN!", BossColor, 1.8f, 2.2f);
                        break;
                    }
                    case SurvivorEventType.HeroDamaged:
                        heroFlashRemaining = 0.14f;
                        if (numbersLeft > 0 && e.Value >= 1f)
                        {
                            numbersLeft--;
                            effects.Text(heroPosition + Vector3.up * 2.1f, "-" + NumberText(Mathf.RoundToInt(e.Value)), HurtColor, 1.05f, 0.8f);
                        }
                        break;
                    case SurvivorEventType.HeroDied:
                        if (!StoreArea(StoreFx.SmokeBlast, heroPosition, 3f, 2f))
                        {
                            effects.Puff(heroPosition, new Color(0.3f, 0.22f, 0.3f, 0.7f), 16, 1.2f, 2f, 1.2f, 0.6f);
                        }
                        break;
                    case SurvivorEventType.Blocked:
                        FlashGuard(heroHasBubble ? BubbleColor : BlockColor);
                        effects.Sparks(heroPosition + Vector3.up * 0.9f + heroRoot.forward * 0.6f, heroRoot.forward, heroHasBubble ? BubbleColor : BlockColor, 6, 5f, 0.9f);
                        break;
                    case SurvivorEventType.Parry:
                        FlashGuard(ParryColor);
                        effects.Flash(heroPosition + Vector3.up * 0.9f + heroRoot.forward * 0.6f, ParryColor, 2f, 0.25f);
                        effects.Text(heroPosition + Vector3.up * 2.4f, "ĐỠ ĐÒN!", ParryColor, 1.2f, 0.9f);
                        break;
                    case SurvivorEventType.SkillUsed:
                        OnSkillUsed(e, heroPosition);
                        break;
                    case SurvivorEventType.Healed:
                        effects.Sparkle(heroPosition, HealColor, 14, 0.7f, 2f, 0.24f);
                        PulseHealAura();
                        effects.Text(heroPosition + Vector3.up * 2.2f, "+" + NumberText(Mathf.RoundToInt(e.Value)), HealColor, 1.1f, 1f);
                        break;
                    case SurvivorEventType.LevelUp:
                        effects.Shockwave(heroPosition, LevelUpColor, 4.5f, 0.7f);
                        if (!StoreAt(StoreFx.StarAura, heroPosition, 2.2f, 2f))
                        {
                            effects.Sparkle(heroPosition, LevelUpColor, 22, 0.9f, 2.6f, 0.28f);
                        }
                        effects.Text(heroPosition + Vector3.up * 2.6f, "LÊN CẤP!", LevelUpColor, 1.4f, 1.3f);
                        break;
                    case SurvivorEventType.ItemPicked:
                    {
                        Color color = SurvivorViewLogic.ItemColor(e.Id);
                        effects.Shockwave(heroPosition, color, 3f, 0.5f);
                        if (!StoreAt(StoreFx.StarHit, heroPosition + Vector3.up * 1f, 2.2f, 1.2f))
                        {
                            effects.Sparkle(heroPosition, color, 16, 0.7f, 2.4f, 0.25f);
                        }
                        break;
                    }
                    case SurvivorEventType.ChestOpened:
                        OnChestOpened(e);
                        break;
                    case SurvivorEventType.MagnetPicked:
                        OnMagnetPicked(heroPosition);
                        break;
                    case SurvivorEventType.BuffStarted:
                        OnBuffStarted(e, heroPosition);
                        break;
                    case SurvivorEventType.BombExploded:
                        OnBombPickup(e);
                        break;
                    case SurvivorEventType.ManaRestored:
                        OnManaRestored(e, heroPosition);
                        break;
                    case SurvivorEventType.GoldCollected:
                        if (e.Id < 0 && e.Point.X * e.Point.X + e.Point.Y * e.Point.Y > 0f && sparksLeft > 0)
                        {
                            sparksLeft--;
                            effects.Sparkle(ArenaSpace.ToWorld(e.Point, 0.3f), GoldColor, 5, 0.3f, 1.2f, 0.18f);
                        }
                        break;
                    case SurvivorEventType.StrikeLanded:
                        OnStrikeLanded(e, events, i, heroPosition);
                        break;
                    case SurvivorEventType.EnemyExploded:
                        OnEnemyExploded(e);
                        break;
                    case SurvivorEventType.EnemyCharged:
                        OnEnemyCharged(e);
                        break;
                    case SurvivorEventType.EnemySplit:
                        OnEnemySplit(e);
                        break;
                    case SurvivorEventType.EnemyHealed:
                        OnEnemyHealed(e);
                        break;
                    case SurvivorEventType.GoldenSpawned:
                        OnGoldenSpawned(e);
                        break;
                    case SurvivorEventType.GoldenKilled:
                        OnGoldenKilled(e);
                        break;
                    case SurvivorEventType.EnemySummoned:
                    {
                        Vector3 point = ArenaSpace.ToWorld(e.Point);
                        if (enemiesById.TryGetValue(e.Id, out EnemyView summoner))
                        {
                            summoner.Animator?.PlayOneShot(WalkerThrowState, 1.2f, false);
                        }
                        effects.Shockwave(point, SummonColor, 3.5f * RingQuadPerRadius * 0.5f, 0.8f);
                        effects.Flash(point + Vector3.up * 1.2f, SummonColor, 2.2f, 0.3f);
                        StoreArea(StoreFx.RuneOfMagic, point + Vector3.up * 0.1f, 1.8f, 1.6f);
                        if (puffsLeft > 0)
                        {
                            puffsLeft--;
                            effects.Puff(point, new Color(0.35f, 0.15f, 0.5f, 0.6f), 10, 1f, 2.6f, 1f, 0.7f);
                        }
                        break;
                    }
                    case SurvivorEventType.WeaponEvolved:
                    {
                        Color gold = SurvivorViewLogic.IsEvolution(e.Id) ? EvolutionColor(e.Id) : SurvivorViewLogic.EvolutionGold;
                        effects.Shockwave(heroPosition, gold, 6f, 0.8f);
                        effects.Flash(heroPosition + Vector3.up, gold, 4f, 0.35f);
                        effects.Sparkle(heroPosition, gold, 32, 1.2f, 3f, 0.3f);
                        effects.Text(heroPosition + Vector3.up * 3f, "TIẾN HÓA!", gold, 1.6f, 1.6f);
                        WeaponEvolved?.Invoke(e.Id);
                        break;
                    }
                }
            }
        }

        private void OnWeaponFired(SurvivorEvent e, Vector3 heroPosition)
        {
            Vector3 direction = new Vector3(e.Point.X, 0f, e.Point.Y);
            if (direction.sqrMagnitude < 1e-6f)
            {
                direction = heroRoot.forward;
            }
            direction.Normalize();
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            WeaponVisual visual = SurvivorViewLogic.WeaponVisualOf(e.Id);
            switch (visual)
            {
                case WeaponVisual.Sweep:
                case WeaponVisual.Dagger:
                {
                    // Dagger and its evolution: a crescent the size of the real hit arc.
                    bool dagger = visual == WeaponVisual.Dagger;
                    int level = Mathf.Max(1, sim.Inventory.Level(e.Id));
                    float reach = SurvivorViewLogic.SweepRange(e.Id, level, sim.DerivedStats.AreaMul) / 1.25f;
                    bool mirror = (strikeCount & 1) == 1;
                    PlayHeroOneShot(mirror ? HeroStrikeB : HeroStrikeA, dagger ? 2.3f : 1.7f, false);
                    strikeCount++;
                    if (!dagger)
                    {
                        // The sword throws a flying wave (a projectile draws it): nothing is painted on the hero.
                        break;
                    }
                    Color color = SurvivorViewLogic.IsEvolution(e.Id) ? EvolutionColor(e.Id) : DaggerSlashColor;
                    float life = dagger ? 0.16f : 0.22f;
                    Vector3 origin = heroPosition + Vector3.up * 0.85f;
                    effects.Slash(origin, yaw, color, mirror, reach, life);
                    if (SurvivorViewLogic.SweepHitsBehind(e.Id, level))
                    {
                        effects.Slash(origin, yaw + 180f, color, !mirror, reach, life);
                    }
                    if (SurvivorViewLogic.IsEvolution(e.Id))
                    {
                        EvolutionMark(e.Id, heroPosition + direction * reach * 0.65f, reach * 0.45f);
                    }
                    break;
                }
                case WeaponVisual.FlameCone:
                {
                    PlayHeroOneShot(HeroCast, 2.1f, false);
                    Vector3 origin = heroPosition + Vector3.up * 0.8f;
                    Color color = FxColor(e.Id, FireballColor);
                    // A spray of fire filling the real cone: flames grow with the distance from the hero.
                    int level = Mathf.Max(1, sim.Inventory.Level(e.Id));
                    float range = SurvivorViewLogic.SweepRange(e.Id, level, sim.DerivedStats.AreaMul);
                    ItemDef cone = SurvivorCatalog.Get(e.Id);
                    float halfArc = (cone != null ? cone.ArcDegrees : 60f) * 0.5f * 0.85f;
                    const int Rows = 5;
                    for (int row = 0; row < Rows; row++)
                    {
                        float along = range * (row + 1f) / Rows;
                        int across = 1 + row / 2;
                        for (int k = 0; k < across; k++)
                        {
                            float t = across == 1 ? 0f : k / (across - 1f) * 2f - 1f;
                            Vector3 spray = Quaternion.AngleAxis(t * halfArc + Random.Range(-5f, 5f), Vector3.up) * direction;
                            effects.Flame(origin + spray * along, color, 0.7f + 0.9f * (row + 1f) / Rows, 0.45f, false);
                        }
                    }
                    if (SurvivorViewLogic.IsEvolution(e.Id))
                    {
                        // Hellfire leaves the ground burnt along the cone.
                        effects.Decal(heroPosition + direction * range * 0.6f, ScorchColor, range * 0.9f, 2.5f);
                        effects.Smoke(heroPosition + direction * range * 0.8f + Vector3.up * 0.8f, new Color(0.12f, 0.08f, 0.08f, 0.45f), 2, 1.1f, 0.9f);
                    }
                    break;
                }
                case WeaponVisual.Combo:
                {
                    bool finisher = e.Extra >= 3f;
                    bool mirror = ((int)e.Extra & 1) == 0;
                    PlayHeroOneShot(mirror ? HeroStrikeB : HeroStrikeA, finisher ? 2.2f : 2.5f, false);
                    effects.Slash(heroPosition + Vector3.up * 0.85f, yaw, FxColor(e.Id, StrikeColor), mirror,
                        finisher ? 3f : 2.2f, finisher ? 0.28f : 0.16f);
                    if (finisher && SurvivorViewLogic.IsEvolution(e.Id))
                    {
                        EvolutionMark(e.Id, heroPosition + direction * 1.8f, 1.4f);
                    }
                    break;
                }
                case WeaponVisual.Hammer:
                case WeaponVisual.Arrow:
                case WeaponVisual.Bomb:
                case WeaponVisual.Bounce:
                case WeaponVisual.Momentum:
                case WeaponVisual.Trio:
                case WeaponVisual.Quad:
                    PlayHeroOneShot(HeroThrow, 1.9f, false);
                    break;
                case WeaponVisual.MagicBolt:
                case WeaponVisual.MultiShot:
                    PlayHeroOneShot(HeroCast, 1.9f, false);
                    break;
                default:
                    OnNewWeaponFired(e, heroPosition, direction, visual);
                    break;
            }
        }

        private void OnSkillUsed(SurvivorEvent e, Vector3 heroPosition)
        {
            int slot = (int)e.Value;
            SkillDef[] skills = sim.Config.ClassDef.ActiveSkills;
            SkillDef skill = slot >= 0 && slot < skills.Length ? skills[slot] : null;
            SkillKind kind = skill != null ? skill.Kind : SkillKind.None;
            Vector3 forward = heroRoot.forward;
            switch (kind)
            {
                case SkillKind.Kick:
                    PlayHeroOneShot(HeroKick, 1.5f, true);
                    effects.Shockwave(heroPosition + forward * 1f, KickColor, 2.6f, 0.35f);
                    effects.Sparks(heroPosition + Vector3.up * 0.6f + forward * 0.9f, forward, KickColor, 6, 7f, 0.9f);
                    break;
                case SkillKind.Dash:
                    PlayHeroOneShot(skill.DashBackward ? HeroDodgeBack : HeroDash, 1.6f, true);
                    effects.Puff(heroPosition, DustColor, 6, 0.7f, 1.2f, 0.6f, 0.3f);
                    break;
                case SkillKind.Block:
                    FlashGuard(heroHasBubble ? BubbleColor : BlockColor);
                    break;
                case SkillKind.Projectile:
                {
                    // Fireball for the mage, power shot for the archer.
                    bool fire = SurvivorViewLogic.ProjectileLookOf(-1 - slot, sim.Config.ClassDef) == ProjectileLook.Fireball;
                    PlayHeroOneShot(fire ? HeroCast : HeroThrow, 1.8f, true);
                    break;
                }
                case SkillKind.AreaBurst:
                    // The ice ring itself is drawn from the StrikeLanded event of the burst.
                    PlayHeroOneShot(HeroCast, 2f, true);
                    break;
                case SkillKind.Teleport:
                {
                    // Core moved the hero before announcing the skill: flash at both ends and snap the model.
                    Vector3 from = heroPrevious;
                    Vector3 to = heroCurrent;
                    effects.Afterimage(heroRenderers, BlinkColor, 0.45f);
                    effects.Flash(from + Vector3.up * 0.9f, BlinkColor, 2.6f, 0.3f);
                    effects.Flash(to + Vector3.up * 0.9f, BlinkColor, 2.6f, 0.3f);
                    effects.Sparkle(from, BlinkColor, 12, 0.6f, 1.6f, 0.22f);
                    effects.Sparkle(to, BlinkColor, 12, 0.6f, 1.6f, 0.22f);
                    effects.Shockwave(to, BlinkColor, 2.4f, 0.35f);
                    if (!StoreAt(StoreFx.Portal, from + Vector3.up * 0.9f, 1.8f, 1.2f))
                    {
                        effects.Rune(from, BlinkColor, 3f, 0.6f);
                    }
                    if (!StoreAt(StoreFx.Portal, to + Vector3.up * 0.9f, 2.2f, 1.2f))
                    {
                        effects.Rune(to, BlinkColor, 3.6f, 0.8f);
                    }
                    heroSnap = true;
                    break;
                }
                case SkillKind.Leap:
                    PlayHeroOneShot(HeroDash, 1.8f, true);
                    effects.Afterimage(heroRenderers, WhirlColor, 0.4f);
                    effects.Puff(heroPosition, DustColor, 8, 0.8f, 1.5f, 0.65f, 0.25f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Whirlwind:
                    PlayHeroOneShot(HeroStrikeA, 2.4f, true);
                    effects.Shockwave(heroPosition, WhirlColor, 5.5f, 0.35f);
                    effects.Twirl(heroPosition + Vector3.up * 0.7f, WhirlColor, 5.5f, 0.5f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Trap:
                    PlayHeroOneShot(HeroThrow, 2f, true);
                    effects.Flash(heroPosition + forward * 0.8f + Vector3.up * 0.4f, TrapColor, 1.2f, 0.18f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Barrage:
                    // The arrows themselves are the whole effect.
                    PlayHeroOneShot(HeroThrow, 2.2f, true);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Wall:
                    PlayHeroOneShot(HeroCast, 2f, true);
                    effects.Flash(heroPosition + forward * 1.2f + Vector3.up * 0.5f, WallColor, 2f, 0.2f);
                    RememberM9Skill(kind);
                    break;
                case SkillKind.Chain:
                    PlayHeroOneShot(HeroCast, 2.2f, true);
                    RememberM9Skill(kind);
                    break;
            }
        }

        private void OnEnemyExploded(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            float radius = Mathf.Max(0.5f, e.Value);
            // Only the explosion, sized to the real radius: extra sparks and smoke made the screen noisy.
            Blast(point, ExplodeColor, radius, StoreFx.PoisonExplode);
            // The exploder is gone at once (no death animation, no EnemyKilled follows).
            if (enemiesById.TryGetValue(e.Id, out EnemyView view))
            {
                ReleaseEnemy(view);
            }
        }

        /// <summary>
        /// Effect colour of a weapon: the renderer's own colour for the warrior's weapons, the item colour for the
        /// mage and archer weapons, the element colour for an evolution.
        /// </summary>
        private static Color FxColor(int catalogIndex, Color original)
        {
            if (catalogIndex < 0)
            {
                return original;
            }
            int baseIndex = SurvivorViewLogic.BaseWeapon(catalogIndex);
            Color color = baseIndex <= 5 ? original : SurvivorViewLogic.ItemColor(baseIndex);
            // An evolution has its own colour (its element), not a gold copy of the base weapon's.
            return SurvivorViewLogic.IsEvolution(catalogIndex) ? EvolutionColor(catalogIndex) : color;
        }

        private void OnDamageDealt(SurvivorEvent e, bool crit)
        {
            if (enemiesById.TryGetValue(e.Id, out EnemyView view))
            {
                view.Flash = 0.09f;
            }
            if (numbersLeft <= 0)
            {
                return;
            }
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            if ((point - heroCurrent).sqrMagnitude > 900f)
            {
                return;
            }
            if (view != null)
            {
                QueueElementFx(point, 1.5f * view.HeightScale, false, view.Chilled);
            }
            numbersLeft--;
            float height = view != null ? 1.5f * view.HeightScale : 1.6f;
            effects.Text(point + Vector3.up * height, NumberText(Mathf.Max(1, Mathf.RoundToInt(e.Value))),
                crit ? CritColor : DamageColor, crit ? 1.3f : 0.9f, crit ? 0.9f : 0.7f);
            if (crit && sparksLeft > 0)
            {
                sparksLeft--;
                effects.Sparks(point + Vector3.up * 0.9f, Vector3.up, CritColor, 6, 5f, 1f);
            }
        }

        private void OnEnemyKilled(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            if (enemiesById.TryGetValue(e.Id, out EnemyView view))
            {
                if ((point - heroCurrent).sqrMagnitude < 900f)
                {
                    QueueElementFx(point, 1.5f * view.HeightScale, true, view.Chilled);
                }
                StartDying(view);
            }
            if (puffsLeft > 0 && (point - heroCurrent).sqrMagnitude < 900f)
            {
                puffsLeft--;
                effects.Puff(point, BoneDustColor, 5, 0.7f, 1.2f, 0.7f, 0.5f);
            }
        }

        private static string NumberText(int value)
        {
            if (numberCache == null)
            {
                numberCache = new string[2048];
            }
            if (value < 0 || value >= numberCache.Length)
            {
                return value.ToString();
            }
            return numberCache[value] ?? (numberCache[value] = value.ToString());
        }
    }
}
