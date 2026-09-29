using System;
using PersonalArena.Core;

namespace PersonalArena.Core.Survivor
{
    public static class SurvivorActionMask
    {
        public static void WriteMask(SurvivorSim sim, bool[] move, bool[] skill, bool[] pick)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (move == null || move.Length < SurvivorInput.MoveBranchSize || skill == null || skill.Length < SurvivorInput.SkillBranchSize || pick == null || pick.Length < SurvivorInput.PickBranchSize) throw new ArgumentException("Mask buffer is too small.");
            Array.Clear(move, 0, SurvivorInput.MoveBranchSize);
            Array.Clear(skill, 0, SurvivorInput.SkillBranchSize);
            Array.Clear(pick, 0, SurvivorInput.PickBranchSize);
            if (sim.IsAwaitingPick)
            {
                move[0] = true; skill[0] = true;
                for (int i = 1; i <= sim.OfferCount; i++) pick[i] = true;
                return;
            }
            for (int i = 0; i < move.Length && i < SurvivorInput.MoveBranchSize; i++) move[i] = true;
            skill[0] = true; pick[0] = true;
            if (sim.Hero.Dashing || sim.Hero.StunRemaining > 0f) return;
            for (int slot = 0; slot < 4; slot++)
            {
                SkillDef def = sim.Config.ClassDef.ActiveSkills[slot];
                if (def == null || def.Kind == SkillKind.None) continue;
                bool energy = sim.Hero.Energy >= def.EnergyCost;
                bool ready = sim.Hero.SkillCooldowns[slot] <= 0f;
                if (def.Kind == SkillKind.Block && sim.Hero.Blocking) energy = true;
                skill[slot + 1] = energy && ready;
            }
        }
    }
}
