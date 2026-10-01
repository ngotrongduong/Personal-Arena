using System;

namespace PersonalArena.View
{
    /// <summary>
    /// Menu sounds raised by the shared button factories and panels (click, purchase, refused), heard through the
    /// viewer's <see cref="SurvivorAudio"/> when one is listening. The viewer has a single UI, so a static event is
    /// enough; training arenas have no UI and never raise it. Nothing happens when no one listens.
    /// </summary>
    public static class UiSounds
    {
        public static event Action<SoundCue> Requested;

        /// <summary>Soft click of a button.</summary>
        public static void Click() => Raise(SoundCue.UiClick);

        /// <summary>Coin sound of a successful purchase.</summary>
        public static void Purchase() => Raise(SoundCue.UiCoin);

        /// <summary>Soft error of a refused action (not enough gold, nothing to do).</summary>
        public static void Refused() => Raise(SoundCue.UiError);

        public static void Raise(SoundCue cue)
        {
            Requested?.Invoke(cue);
        }
    }
}
