using PersonalArena.Core.Survivor;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>
    /// M11: moving auras on the ground under the hero, one look per kind of buff (damage, defence, area, speed,
    /// healing). Each aura is two textured quads turning against each other; it fades in and out.
    /// </summary>
    public sealed partial class SurvivorRenderer
    {
        private enum BuffAura
        {
            Damage,
            Defense,
            Area,
            Speed,
            Heal
        }

        private const int BuffAuraCount = 5;

        private sealed class BuffAuraView
        {
            public Transform Root;
            public Transform Inner;
            public Transform Outer;
            public Material InnerMaterial;
            public Material OuterMaterial;
            public Color Color;
            public float Size;
            public float Spin;
            public float Alpha;
            public bool On;
            public GameObject Store;
            public float StoreScale;
        }

        private readonly BuffAuraView[] buffAuras = new BuffAuraView[BuffAuraCount];
        private Transform buffAuraRoot;
        private float healAuraRemaining;
        private bool buffAuraDemo;

        private void BuildBuffAuras()
        {
            buffAuraRoot = CreateChild("Buff Auras", actorsRoot);
            buffAuras[(int)BuffAura.Damage] = BuildBuffAura("Damage", "magic_03", "twirl_02", new Color(1f, 0.38f, 0.12f), 3.4f, 70f);
            buffAuras[(int)BuffAura.Defense] = BuildBuffAura("Defense", "circle_03", "magic_02", new Color(0.35f, 0.75f, 1f), 2.9f, 40f);
            buffAuras[(int)BuffAura.Area] = BuildBuffAura("Area", "light_03", "magic_01", new Color(1f, 0.88f, 0.4f), 4f, 28f);
            buffAuras[(int)BuffAura.Speed] = BuildBuffAura("Speed", "twirl_03", "twirl_02", new Color(0.45f, 1f, 0.55f), 2.6f, 220f);
            buffAuras[(int)BuffAura.Heal] = BuildBuffAura("Heal", "circle_01", "star_06", new Color(0.4f, 1f, 0.5f), 3.2f, 55f);
            // M12: the store packs' looping auras replace the two quads where the packs are installed.
            UseStoreAura(BuffAura.Damage, StoreFx.BuffAura, 1.6f, 0f);
            UseStoreAura(BuffAura.Defense, StoreFx.ShieldBlue, 0.85f, 0.9f);
            UseStoreAura(BuffAura.Speed, StoreFx.LightningAura, 1.6f, 0f);
            UseStoreAura(BuffAura.Heal, StoreFx.HealAura, 1.6f, 0f);
        }

        private void UseStoreAura(BuffAura aura, StoreFx slot, float scale, float height)
        {
            BuffAuraView view = buffAuras[(int)aura];
            view.Store = effects.StoreAttach(slot, view.Root, scale, height);
            if (view.Store == null)
            {
                return;
            }
            view.StoreScale = scale;
            view.Store.SetActive(true);
            view.Inner.gameObject.SetActive(false);
            view.Outer.gameObject.SetActive(false);
        }

        private BuffAuraView BuildBuffAura(string label, string innerTexture, string outerTexture, Color color, float size, float spin)
        {
            BuffAuraView view = new BuffAuraView { Color = color, Size = size, Spin = spin };
            view.Root = CreateChild(label + " Aura", buffAuraRoot);
            view.InnerMaterial = Own(FxAssets.Create(label + " Aura Inner", VfxLibrary.Texture(innerTexture), true));
            view.OuterMaterial = Own(FxAssets.Create(label + " Aura Outer", VfxLibrary.Texture(outerTexture), true));
            view.Inner = CreateFlatQuad("Inner", view.Root, view.InnerMaterial);
            view.Outer = CreateFlatQuad("Outer", view.Root, view.OuterMaterial);
            view.Root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>Called on a Healed event: the healing aura shows for a moment.</summary>
        private void PulseHealAura()
        {
            healAuraRemaining = 1.4f;
        }

        private void PresentBuffAuras(float realDelta)
        {
            if (buffAuraRoot == null || sim == null)
            {
                return;
            }
            bool alive = sim.Hero.Alive;
            healAuraRemaining = Mathf.Max(0f, healAuraRemaining - realDelta);
            buffAuras[(int)BuffAura.Damage].On = alive && (buffAuraDemo || HeroHasDamageBuff());
            buffAuras[(int)BuffAura.Defense].On = alive && (buffAuraDemo || sim.BarrierCharges > 0 || sim.Hero.Blocking || HeroHasDefenseBuff());
            buffAuras[(int)BuffAura.Area].On = alive && (buffAuraDemo || sim.AuraRadius > 0f);
            buffAuras[(int)BuffAura.Speed].On = alive && (buffAuraDemo || sim.Whirling || HeroHasSpeedBuff());
            buffAuras[(int)BuffAura.Heal].On = alive && (buffAuraDemo || healAuraRemaining > 0f);

            float time = Time.unscaledTime;
            float areaSize = sim.AuraRadius > 0f ? Mathf.Max(3f, sim.AuraRadius * 2.1f) : buffAuras[(int)BuffAura.Area].Size;
            Vector3 center = heroDisplayPosition + Vector3.up * 0.07f;
            for (int i = 0; i < buffAuras.Length; i++)
            {
                BuffAuraView view = buffAuras[i];
                view.Alpha = Mathf.MoveTowards(view.Alpha, view.On ? 1f : 0f, realDelta * 3f);
                bool show = view.Alpha > 0.01f;
                if (view.Root.gameObject.activeSelf != show)
                {
                    view.Root.gameObject.SetActive(show);
                }
                if (!show)
                {
                    continue;
                }
                float size = (i == (int)BuffAura.Area ? areaSize : view.Size) * (1f + 0.06f * Mathf.Sin(time * 4f + i));
                // Stacked a little apart so two auras never z-fight.
                view.Root.position = center + Vector3.up * (0.012f * i)
                    + (buffAuraDemo ? new Vector3((i - 2) * 4.2f, 0f, -7.5f) : Vector3.zero);
                view.Inner.localRotation = Quaternion.Euler(0f, (time * view.Spin) % 360f, 0f);
                view.Outer.localRotation = Quaternion.Euler(0f, (-time * view.Spin * 0.6f) % 360f, 0f);
                view.Inner.localScale = new Vector3(size, 1f, size);
                view.Outer.localScale = new Vector3(size * 1.25f, 1f, size * 1.25f);
                float glow = 0.75f + 0.25f * Mathf.Sin(time * 6f + i * 1.7f);
                view.InnerMaterial.color = new Color(view.Color.r, view.Color.g, view.Color.b, 0.85f * view.Alpha * glow);
                view.OuterMaterial.color = new Color(view.Color.r, view.Color.g, view.Color.b, 0.55f * view.Alpha);
            }
        }

        private bool HeroHasDamageBuff() => sim.RageRemaining > 0f;
        private bool HeroHasDefenseBuff() => sim.ShieldRemaining > 0f;
        private bool HeroHasSpeedBuff() => sim.HasteRemaining > 0f;

        private static readonly Color RageColor = new Color(1f, 0.35f, 0.12f);
        private static readonly Color ShieldBuffColor = new Color(0.4f, 0.75f, 1f);
        private static readonly Color HasteColor = new Color(0.45f, 1f, 0.55f);
        private static readonly Color ManaColor = new Color(0.35f, 0.6f, 1f);

        private void OnBuffStarted(SurvivorEvent e, Vector3 heroPosition)
        {
            Color color;
            string label;
            switch ((BuffKind)e.Id)
            {
                case BuffKind.Rage: color = RageColor; label = "CUỒNG NỘ!"; break;
                case BuffKind.Shield: color = ShieldBuffColor; label = "KHIÊN!"; break;
                default: color = HasteColor; label = "TỐC ĐỘ!"; break;
            }
            effects.Shockwave(heroPosition, color, 9f, 0.6f);
            effects.Rune(heroPosition, color, 6f, 0.8f);
            effects.Flash(heroPosition + Vector3.up, color, 4f, 0.25f);
            StoreFx burst = (BuffKind)e.Id == BuffKind.Rage ? StoreFx.RedBlast
                : (BuffKind)e.Id == BuffKind.Shield ? StoreFx.Explode5
                : StoreFx.LightningBall;
            if (!StoreArea(burst, heroPosition + Vector3.up * 0.3f, 2.8f, 1.5f))
            {
                effects.Sparkle(heroPosition, color, 22, 1.2f, 2.4f, 0.26f);
            }
            effects.Text(heroPosition + Vector3.up * 2.5f, label, color, 1.3f, 1.2f);
        }

        private void OnBombPickup(SurvivorEvent e)
        {
            Vector3 point = ArenaSpace.ToWorld(e.Point);
            float radius = Mathf.Max(1f, e.Value);
            effects.Shockwave(point, FireballColor, radius * RingQuadPerRadius, 0.6f);
            effects.Shockwave(point, Color.white, radius * RingQuadPerRadius * 0.6f, 0.4f);
            Blast(point, FireballColor, radius);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                effects.Explosion(point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * 0.6f), FireballColor, radius * 0.4f, false);
            }
            effects.Text(point + Vector3.up * 2.5f, "BOM!", FireballColor, 1.4f, 1.1f);
        }

        private void OnManaRestored(SurvivorEvent e, Vector3 heroPosition)
        {
            effects.Shockwave(heroPosition, ManaColor, 4f, 0.5f);
            if (!StoreAt(StoreFx.MagicCircle, heroPosition, 1.6f, 1.5f))
            {
                effects.Sparkle(heroPosition, ManaColor, 18, 0.8f, 2.2f, 0.24f);
            }
            effects.Text(heroPosition + Vector3.up * 2.2f, "+" + NumberText(Mathf.RoundToInt(e.Value)) + " năng lượng", ManaColor, 1.1f, 1f);
        }
    }
}
