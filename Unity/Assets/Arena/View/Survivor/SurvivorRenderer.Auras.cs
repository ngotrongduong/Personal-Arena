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

        // Timed item buffs arrive with the M11 drops; until then only the standing states above show an aura.
        private bool HeroHasDamageBuff() => false;
        private bool HeroHasDefenseBuff() => false;
        private bool HeroHasSpeedBuff() => false;
    }
}
