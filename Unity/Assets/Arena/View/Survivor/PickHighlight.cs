using System;

namespace PersonalArena.View
{
    /// <summary>
    /// Pure state machine of the level-up panel in the viewer: the offer is shown briefly, the AI picks,
    /// then the chosen card stays highlighted while the viewer holds the simulation.
    /// </summary>
    public sealed class PickHighlight
    {
        public const int MaximumOffers = 4;
        /// <summary>Seconds the offer is visible before the AI is allowed to pick, so the cards can be read.</summary>
        public const float RevealSeconds = 0.6f;
        /// <summary>Seconds the chosen card stays highlighted before the run continues.</summary>
        public const float HighlightSeconds = 1.2f;

        public enum Phase
        {
            None,
            Offer,
            Highlight
        }

        private readonly int[] items = new int[MaximumOffers];
        private readonly int[] levels = new int[MaximumOffers];
        private float elapsed;

        public Phase Current { get; private set; }
        public int Count { get; private set; }
        public int ChosenSlot { get; private set; } = -1;

        /// <summary>True once the offer has been visible long enough for the AI to pick.</summary>
        public bool ReadyToPick => Current == Phase.Offer && elapsed >= RevealSeconds;

        /// <summary>True while the viewer should not step the simulation (offer shown or choice highlighted).</summary>
        public bool BlocksSim => Current != Phase.None;

        /// <summary>0..1 progress of the highlight; 0 outside the highlight phase.</summary>
        public float Progress => Current == Phase.Highlight ? Math.Min(1f, elapsed / HighlightSeconds) : 0f;

        public int Item(int slot) => slot >= 0 && slot < Count ? items[slot] : -1;
        public int ItemLevel(int slot) => slot >= 0 && slot < Count ? levels[slot] : 0;

        /// <summary>Starts showing an offer; the arrays are copied (up to <see cref="MaximumOffers"/> entries).</summary>
        public void ShowOffer(int count, int[] offerItems, int[] offerLevels)
        {
            if (offerItems == null) throw new ArgumentNullException(nameof(offerItems));
            if (offerLevels == null) throw new ArgumentNullException(nameof(offerLevels));
            Count = Math.Max(0, Math.Min(Math.Min(count, MaximumOffers), Math.Min(offerItems.Length, offerLevels.Length)));
            for (int i = 0; i < MaximumOffers; i++)
            {
                items[i] = i < Count ? offerItems[i] : -1;
                levels[i] = i < Count ? offerLevels[i] : 0;
            }
            ChosenSlot = -1;
            elapsed = 0f;
            Current = Count > 0 ? Phase.Offer : Phase.None;
        }

        /// <summary>Marks the chosen card (0-based) and starts the highlight; false when no offer is open or the slot is invalid.</summary>
        public bool Choose(int slot)
        {
            if (Current != Phase.Offer || slot < 0 || slot >= Count)
            {
                return false;
            }
            ChosenSlot = slot;
            elapsed = 0f;
            Current = Phase.Highlight;
            return true;
        }

        /// <summary>Advances time; the highlight ends by itself after <see cref="HighlightSeconds"/>.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Current == Phase.None || float.IsNaN(deltaSeconds) || deltaSeconds <= 0f)
            {
                return;
            }
            elapsed += deltaSeconds;
            if (Current == Phase.Highlight && elapsed >= HighlightSeconds)
            {
                Clear();
            }
        }

        public void Clear()
        {
            Current = Phase.None;
            Count = 0;
            ChosenSlot = -1;
            elapsed = 0f;
        }
    }
}
