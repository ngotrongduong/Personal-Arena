using System;
using NUnit.Framework;

namespace PersonalArena.View.Tests
{
    public sealed class PickHighlightTests
    {
        private static PickHighlight OfferOfThree()
        {
            PickHighlight highlight = new PickHighlight();
            highlight.ShowOffer(3, new[] { 0, 3, 6 }, new[] { 2, 1, 1 });
            return highlight;
        }

        [Test]
        public void StartsIdle()
        {
            PickHighlight highlight = new PickHighlight();
            Assert.That(highlight.Current, Is.EqualTo(PickHighlight.Phase.None));
            Assert.That(highlight.BlocksSim, Is.False);
            Assert.That(highlight.ReadyToPick, Is.False);
            Assert.That(highlight.Item(0), Is.EqualTo(-1));
        }

        [Test]
        public void Offer_BlocksTheSimAndWaitsForTheReveal()
        {
            PickHighlight highlight = OfferOfThree();
            Assert.That(highlight.Current, Is.EqualTo(PickHighlight.Phase.Offer));
            Assert.That(highlight.BlocksSim, Is.True);
            Assert.That(highlight.Count, Is.EqualTo(3));
            Assert.That(highlight.ReadyToPick, Is.False);

            highlight.Tick(PickHighlight.RevealSeconds * 0.5f);
            Assert.That(highlight.ReadyToPick, Is.False);
            highlight.Tick(PickHighlight.RevealSeconds * 0.6f);
            Assert.That(highlight.ReadyToPick, Is.True);
            Assert.That(highlight.Current, Is.EqualTo(PickHighlight.Phase.Offer), "an offer never closes by itself");
        }

        [Test]
        public void Offer_CopiesTheArrays()
        {
            int[] items = { 0, 3, 6 };
            int[] levels = { 2, 1, 1 };
            PickHighlight highlight = new PickHighlight();
            highlight.ShowOffer(3, items, levels);
            items[1] = 99;
            levels[1] = 7;
            Assert.That(highlight.Item(1), Is.EqualTo(3));
            Assert.That(highlight.ItemLevel(1), Is.EqualTo(1));
            Assert.That(highlight.Item(3), Is.EqualTo(-1), "outside the offer");
        }

        [Test]
        public void Offer_ClampsTheCount()
        {
            PickHighlight highlight = new PickHighlight();
            highlight.ShowOffer(9, new[] { 0, 3, 6, 7, 9 }, new[] { 1, 1, 1, 1, 1 });
            Assert.That(highlight.Count, Is.EqualTo(PickHighlight.MaximumOffers));
            highlight.ShowOffer(3, new[] { 0 }, new[] { 1 });
            Assert.That(highlight.Count, Is.EqualTo(1), "never more than the arrays hold");
            highlight.ShowOffer(0, new int[0], new int[0]);
            Assert.That(highlight.Current, Is.EqualTo(PickHighlight.Phase.None), "an empty offer shows nothing");
            Assert.Throws<ArgumentNullException>(() => highlight.ShowOffer(1, null, new[] { 1 }));
            Assert.Throws<ArgumentNullException>(() => highlight.ShowOffer(1, new[] { 1 }, null));
        }

        [Test]
        public void Choose_RejectsInvalidSlots()
        {
            PickHighlight highlight = OfferOfThree();
            Assert.That(highlight.Choose(-1), Is.False);
            Assert.That(highlight.Choose(3), Is.False);
            Assert.That(highlight.Current, Is.EqualTo(PickHighlight.Phase.Offer));
            Assert.That(new PickHighlight().Choose(0), Is.False, "nothing to choose without an offer");
        }

        [Test]
        public void Choose_HighlightsForTheSetTimeThenReleasesTheSim()
        {
            PickHighlight highlight = OfferOfThree();
            highlight.Tick(PickHighlight.RevealSeconds);
            Assert.That(highlight.Choose(1), Is.True);
            Assert.That(highlight.Current, Is.EqualTo(PickHighlight.Phase.Highlight));
            Assert.That(highlight.ChosenSlot, Is.EqualTo(1));
            Assert.That(highlight.Item(highlight.ChosenSlot), Is.EqualTo(3));
            Assert.That(highlight.BlocksSim, Is.True);
            Assert.That(highlight.Choose(2), Is.False, "only one choice per offer");

            highlight.Tick(PickHighlight.HighlightSeconds * 0.5f);
            Assert.That(highlight.Progress, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(highlight.BlocksSim, Is.True);

            highlight.Tick(PickHighlight.HighlightSeconds * 0.51f);
            Assert.That(highlight.Current, Is.EqualTo(PickHighlight.Phase.None));
            Assert.That(highlight.BlocksSim, Is.False);
            Assert.That(highlight.ChosenSlot, Is.EqualTo(-1));
        }

        [Test]
        public void Tick_IgnoresInvalidTime()
        {
            PickHighlight highlight = OfferOfThree();
            highlight.Tick(float.NaN);
            highlight.Tick(-5f);
            highlight.Tick(0f);
            Assert.That(highlight.ReadyToPick, Is.False);
        }

        [Test]
        public void Clear_EndsAnyPhase()
        {
            PickHighlight highlight = OfferOfThree();
            highlight.Clear();
            Assert.That(highlight.BlocksSim, Is.False);
            Assert.That(highlight.Count, Is.Zero);
        }
    }
}
