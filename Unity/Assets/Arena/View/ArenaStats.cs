using System.Collections.Generic;
using PersonalArena.Core;

namespace PersonalArena.View
{
    /// <summary>Per-episode counters derived only from simulation events.</summary>
    public sealed class ArenaStats
    {
        public int Kills { get; private set; }
        public int Backstabs { get; private set; }
        public int Parries { get; private set; }
        public int KicksLanded { get; private set; }
        public float DamageDealt { get; private set; }
        public float DamageTaken { get; private set; }
        public float TimeSurvived { get; private set; }

        public void Reset()
        {
            Kills = 0;
            Backstabs = 0;
            Parries = 0;
            KicksLanded = 0;
            DamageDealt = 0f;
            DamageTaken = 0f;
            TimeSurvived = 0f;
        }

        public void RecordTick(ArenaSim sim)
        {
            RecordEvents(sim.Events, sim.Time);
        }

        public void RecordEvents(IReadOnlyList<SimEvent> events, float timeSurvived)
        {
            TimeSurvived = timeSurvived;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent simEvent = events[i];
                switch (simEvent.Type)
                {
                    case SimEventType.ZombieKilled:
                        Kills++;
                        break;
                    case SimEventType.Backstab:
                        Backstabs++;
                        break;
                    case SimEventType.Parry:
                        Parries++;
                        break;
                    case SimEventType.Kick:
                        KicksLanded++;
                        break;
                    case SimEventType.HeroDealtDamage:
                        DamageDealt += simEvent.Value;
                        break;
                    case SimEventType.HeroDamaged:
                        DamageTaken += simEvent.Value;
                        break;
                }
            }
        }
    }
}
