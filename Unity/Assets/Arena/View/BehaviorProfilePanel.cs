using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>
    /// Full-screen "AI profile": the trainer's best brain (champion), its last 100-seed evaluation,
    /// why it dies and its play style compared with the previous best brain. Built at runtime.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class BehaviorProfilePanel : MonoBehaviour
    {
        private const float ReloadSeconds = 5f;
        private const float CardWidth = 1500f;
        private const float BarWidth = 380f;
        private const float DeathBarWidth = 190f;

        private static readonly Color Good = new Color(0.36f, 0.88f, 0.5f, 1f);
        private static readonly Color Warn = new Color(1f, 0.72f, 0.28f, 1f);
        private static readonly Color Bad = new Color(1f, 0.42f, 0.42f, 1f);
        private static readonly Color Muted = new Color(0.66f, 0.72f, 0.81f, 1f);
        private static readonly Color Panel = new Color(0.085f, 0.1f, 0.135f, 1f);
        private static readonly Color Track = new Color(0.15f, 0.17f, 0.22f, 1f);

        private static readonly Trait[] Traits =
        {
            new Trait("Offense", "With enemies near: share of time spent charging into the pack",
                new Color(1f, 0.45f, 0.35f), b => b.Aggression),
            new Trait("Cautious", "Below 35% HP with enemies close: share of time spent backing away",
                new Color(0.35f, 0.75f, 1f), b => b.Caution),
            new Trait("Looter", "Share of EXP gems, gold and drops picked up",
                new Color(1f, 0.83f, 0.28f), b => b.Greed),
            new Trait("Explorer", "Share of the map visited in a run",
                new Color(0.45f, 0.9f, 0.6f), b => b.Exploration),
            new Trait("Crowd control", "Share of kicks that hit 3 or more enemies",
                new Color(0.75f, 0.5f, 1f), b => b.CrowdControl),
            new Trait("Boss hunter", "With the boss near: share of time spent moving toward it",
                new Color(1f, 0.5f, 0.75f), b => b.BossHunting),
            new Trait("Skill use", "Share of kicks, blocks and dashes that had an effect",
                new Color(0.4f, 0.9f, 0.95f), b => b.SkillDiscipline)
        };

        private static readonly string[] DeathOrder = { "Surrounded", "Boss", "Brute", "Contact", "Projectile" };

        private readonly TraitRow[] traitRows = new TraitRow[7];
        private readonly DeathRow[] deathRows = new DeathRow[5];
        private GameObject overlay;
        private GameObject content;
        private GameObject emptyState;
        private Text subtitle;
        private Text badgeLabel;
        private Image badge;
        private Text identity;
        private readonly Tile[] tiles = new Tile[4];
        private Text lastEvalResult;
        private Text lastEvalDetail;
        private Text deathHeader;
        private Text deathNone;
        private Text styleCompare;
        private Text styleDetail;
        private Font font;
        private string runsDirectory;
        private string behavior;
        private bool built;
        private bool isOpen;
        private float nextReloadTime;
        private int consumedEscapeFrame = -1;

        /// <summary>Raised after an open panel closes.</summary>
        public event Action Closed;

        /// <summary>Whether the overlay is currently visible.</summary>
        public bool IsOpen => isOpen;

        /// <summary>Whether this panel handled Escape during the current frame.</summary>
        public bool ConsumedEscapeThisFrame => consumedEscapeFrame == Time.frameCount;

        /// <summary>Builds the overlay under an existing HUD canvas.</summary>
        public void Build(Transform canvasRoot, Font font)
        {
            if (built || canvasRoot == null)
            {
                return;
            }

            built = true;
            this.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();

            overlay = CreateUiObject("Behavior Profile Overlay", canvasRoot);
            Image backdrop = overlay.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.025f, 0.04f, 0.92f);
            backdrop.raycastTarget = true;
            SetStretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform card = CreateImage("Behavior Profile Card", overlay.transform,
                new Color(0.055f, 0.065f, 0.09f, 1f), UiSprites.RoundedSprite(), Image.Type.Sliced);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(CardWidth, 860f), new Vector2(0.5f, 0.5f));

            Text title = CreateText("Title", card, 34, TextAnchor.UpperLeft, Color.white);
            title.text = "AI PROFILE — best brain and play style";
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, 34f, -22f, 1250f, 44f);

            subtitle = CreateText("Subtitle", card, 17, TextAnchor.UpperLeft, Muted);
            subtitle.text = "Every 2M steps the AI is scored over 100 runs; the best brain is always kept";
            Place(subtitle.rectTransform, 36f, -68f, 1320f, 28f);

            RectTransform closeRect = CreateImage("Close", card, new Color(0.14f, 0.16f, 0.21f, 1f),
                UiSprites.Circle(), Image.Type.Simple);
            SetRect(closeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f),
                new Vector2(48f, 48f), new Vector2(1f, 1f));
            Button closeButton = closeRect.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeRect.GetComponent<Image>();
            closeButton.onClick.AddListener(UiSounds.Click);
            closeButton.onClick.AddListener(() => SetOpen(false));
            Text closeLabel = CreateText("Label", closeRect, 25, TextAnchor.MiddleCenter, Color.white);
            closeLabel.text = "X";
            closeLabel.fontStyle = FontStyle.Bold;
            SetStretch(closeLabel.rectTransform, 0f, 0f, 0f, 0f);

            content = CreateUiObject("Content", card);
            SetStretch(content.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            BuildChampionCard(content.transform);
            BuildLastEvaluationCard(content.transform);
            BuildDeathCard(content.transform);
            BuildStyleCard(content.transform);

            emptyState = CreateUiObject("Empty State", card);
            RectTransform emptyRect = emptyState.GetComponent<RectTransform>();
            Place(emptyRect, 110f, -225f, 1280f, 420f);
            Text emptyText = CreateText("Message", emptyRect, 27, TextAnchor.MiddleCenter,
                new Color(0.82f, 0.86f, 0.93f, 1f));
            emptyText.text = "The profile appears after 2M training steps.\n" +
                "The AI is then scored over 100 runs and the best brain is kept.\n" +
                "Press TRAIN AI to start training.";
            emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetStretch(emptyText.rectTransform, 30f, 30f, 30f, 30f);

            Apply(null);
            overlay.SetActive(isOpen);
            if (isOpen)
            {
                Reload();
            }
        }

        /// <summary>Where to read Trainer/runs/champions/&lt;behavior&gt; from.</summary>
        public void SetSource(string runsDirectory, string behavior)
        {
            if (this.runsDirectory == runsDirectory && this.behavior == behavior)
            {
                return;
            }

            this.runsDirectory = runsDirectory;
            this.behavior = behavior;
            if (built && isOpen)
            {
                Reload();
            }
        }

        /// <summary>Shows or hides the overlay.</summary>
        public void SetOpen(bool open)
        {
            if (isOpen == open)
            {
                return;
            }

            isOpen = open;
            if (overlay != null)
            {
                overlay.SetActive(open);
            }

            if (open)
            {
                nextReloadTime = Time.unscaledTime + ReloadSeconds;
                Reload();
            }
            else
            {
                Closed?.Invoke();
            }
        }

        /// <summary>Toggles the overlay.</summary>
        public void Toggle()
        {
            SetOpen(!isOpen);
        }

        private void Update()
        {
            if (!isOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                consumedEscapeFrame = Time.frameCount;
                SetOpen(false);
                return;
            }

            if (Time.unscaledTime >= nextReloadTime)
            {
                nextReloadTime = Time.unscaledTime + ReloadSeconds;
                Reload();
            }
        }

        private void Reload()
        {
            if (!built)
            {
                return;
            }

            Apply(ChampionInfo.Load(runsDirectory, behavior));
        }

        private void BuildChampionCard(Transform parent)
        {
            RectTransform card = CreateImage("Champion Card", parent, Panel, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(card, 34f, -112f, 560f, 300f);
            Header(card, "BEST BRAIN");

            badge = CreateImage("M4A Badge", card, Good, UiSprites.RoundedSprite(), Image.Type.Sliced).GetComponent<Image>();
            SetRect(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -12f),
                new Vector2(190f, 34f), new Vector2(1f, 1f));
            badgeLabel = CreateText("Label", badge.transform, 16, TextAnchor.MiddleCenter, new Color(0.05f, 0.06f, 0.08f, 1f));
            badgeLabel.fontStyle = FontStyle.Bold;
            SetStretch(badgeLabel.rectTransform, 0f, 0f, 0f, 0f);

            identity = CreateText("Identity", card, 16, TextAnchor.UpperLeft, Muted);
            Place(identity.rectTransform, 18f, -50f, 524f, 24f);

            for (int i = 0; i < tiles.Length; i++)
            {
                float x = 18f + (i % 2) * 270f;
                float y = -84f - (i / 2) * 104f;
                RectTransform tile = CreateImage("Tile " + (i + 1), card, new Color(0.11f, 0.13f, 0.17f, 1f),
                    UiSprites.RoundedSprite(), Image.Type.Sliced);
                Place(tile, x, y, 254f, 92f);
                tiles[i].Label = CreateText("Label", tile, 15, TextAnchor.UpperLeft, Muted);
                Place(tiles[i].Label.rectTransform, 14f, -10f, 230f, 20f);
                tiles[i].Value = CreateText("Value", tile, 30, TextAnchor.UpperLeft, Color.white);
                tiles[i].Value.fontStyle = FontStyle.Bold;
                Place(tiles[i].Value.rectTransform, 14f, -30f, 230f, 36f);
                tiles[i].Note = CreateText("Note", tile, 13, TextAnchor.UpperLeft, Muted);
                Place(tiles[i].Note.rectTransform, 14f, -68f, 230f, 18f);
            }
        }

        private void BuildLastEvaluationCard(Transform parent)
        {
            RectTransform card = CreateImage("Last Evaluation Card", parent, Panel, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(card, 34f, -428f, 560f, 140f);
            Header(card, "LATEST EVALUATION");
            lastEvalResult = CreateText("Result", card, 19, TextAnchor.UpperLeft, Color.white);
            lastEvalResult.fontStyle = FontStyle.Bold;
            Place(lastEvalResult.rectTransform, 18f, -52f, 524f, 26f);
            lastEvalDetail = CreateText("Detail", card, 16, TextAnchor.UpperLeft, Muted);
            lastEvalDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(lastEvalDetail.rectTransform, 18f, -84f, 524f, 48f);
        }

        private void BuildDeathCard(Transform parent)
        {
            RectTransform card = CreateImage("Death Card", parent, Panel, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(card, 34f, -584f, 560f, 248f);
            deathHeader = Header(card, "CAUSES OF DEATH");
            deathNone = CreateText("None", card, 17, TextAnchor.UpperLeft, Good);
            Place(deathNone.rectTransform, 18f, -56f, 524f, 26f);

            for (int i = 0; i < deathRows.Length; i++)
            {
                float y = -54f - i * 38f;
                deathRows[i].Name = CreateText("Name", card, 17, TextAnchor.UpperLeft, Color.white);
                Place(deathRows[i].Name.rectTransform, 18f, y, 170f, 24f);
                RectTransform track = CreateImage("Track", card, Track, UiSprites.RoundedSprite(), Image.Type.Sliced);
                Place(track, 196f, y - 4f, DeathBarWidth, 16f);
                deathRows[i].Fill = CreateImage("Fill", track, Bad, UiSprites.RoundedSprite(), Image.Type.Sliced).GetComponent<Image>();
                Place(deathRows[i].Fill.rectTransform, 0f, 0f, 0f, 16f);
                deathRows[i].Count = CreateText("Count", card, 17, TextAnchor.UpperRight, Color.white);
                Place(deathRows[i].Count.rectTransform, 396f, y, 146f, 24f);
                deathRows[i].Root = new[] { deathRows[i].Name.gameObject, track.gameObject, deathRows[i].Count.gameObject };
            }
        }

        private void BuildStyleCard(Transform parent)
        {
            RectTransform card = CreateImage("Style Card", parent, Panel, UiSprites.RoundedSprite(), Image.Type.Sliced);
            Place(card, 618f, -112f, 848f, 720f);
            Header(card, "PLAY STYLE  (0–100%)");
            styleCompare = CreateText("Compare", card, 15, TextAnchor.UpperRight, Muted);
            SetRect(styleCompare.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f),
                new Vector2(430f, 22f), new Vector2(1f, 1f));

            for (int i = 0; i < traitRows.Length; i++)
            {
                float y = -58f - i * 70f;
                Trait trait = Traits[i];
                Text label = CreateText("Label", card, 19, TextAnchor.UpperLeft, Color.white);
                label.text = trait.Name;
                label.fontStyle = FontStyle.Bold;
                Place(label.rectTransform, 22f, y, 270f, 26f);
                Text hint = CreateText("Hint", card, 14, TextAnchor.UpperLeft, Muted);
                hint.text = trait.Hint;
                Place(hint.rectTransform, 22f, y - 28f, 800f, 20f);
                RectTransform track = CreateImage("Track", card, Track, UiSprites.RoundedSprite(), Image.Type.Sliced);
                Place(track, 300f, y - 4f, BarWidth, 18f);
                traitRows[i].Fill = CreateImage("Fill", track, trait.Color, UiSprites.RoundedSprite(), Image.Type.Sliced).GetComponent<Image>();
                Place(traitRows[i].Fill.rectTransform, 0f, 0f, 0f, 18f);
                traitRows[i].Value = CreateText("Value", card, 19, TextAnchor.UpperRight, Color.white);
                traitRows[i].Value.fontStyle = FontStyle.Bold;
                Place(traitRows[i].Value.rectTransform, 684f, y, 76f, 26f);
                traitRows[i].Delta = CreateText("Delta", card, 17, TextAnchor.UpperRight, Muted);
                traitRows[i].Delta.fontStyle = FontStyle.Bold;
                Place(traitRows[i].Delta.rectTransform, 762f, y + 1f, 66f, 26f);
            }

            RectTransform separator = CreateImage("Separator", card, Track, null, Image.Type.Simple);
            Place(separator, 22f, -552f, 804f, 2f);
            styleDetail = CreateText("Detail", card, 16, TextAnchor.UpperLeft, new Color(0.82f, 0.86f, 0.93f, 1f));
            styleDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            styleDetail.lineSpacing = 1.25f;
            Place(styleDetail.rectTransform, 22f, -568f, 804f, 140f);
        }

        private void Apply(ChampionInfo info)
        {
            bool has = info != null && info.HasChampion;
            content.SetActive(has);
            emptyState.SetActive(!has);
            if (!has)
            {
                return;
            }

            ApplyChampion(info.Champion);
            ApplyLastEvaluation(info.LastEvaluation);
            ApplyDeaths(info.Champion);
            ApplyStyle(info.Champion, info.PreviousChampion);
        }

        private void ApplyChampion(ChampionRecord champion)
        {
            ChampionSummary summary = champion.summary;
            badge.color = champion.passes_m4a ? Good : Warn;
            badgeLabel.text = champion.passes_m4a ? "M4A TARGET MET" : "M4A NOT MET";
            identity.text = champion.run_id + "  •  " + FormatSteps(champion.step) + " steps  •  scored at " +
                FormatTime(champion.When);

            int runs = Math.Max(1, summary.Runs);
            int wins = Mathf.RoundToInt(summary.WinRate * runs);
            SetTile(0, "Survival (median run)", FormatSeconds(summary.MedianSurvivedSeconds),
                "target " + FormatSeconds(ChampionInfo.TargetMedianSeconds),
                summary.MedianSurvivedSeconds >= ChampionInfo.TargetMedianSeconds ? Good : Warn);
            SetTile(1, "Survival (worst 10%)", FormatSeconds(summary.P10SurvivedSeconds),
                "target " + FormatSeconds(ChampionInfo.TargetP10Seconds),
                summary.P10SurvivedSeconds >= ChampionInfo.TargetP10Seconds ? Good : Warn);
            SetTile(2, "Boss kills (wins)", Percent(summary.WinRate),
                wins + " / " + summary.Runs + " runs", wins > 0 ? Good : Color.white);
            SetTile(3, "Early deaths (before 3:00)", summary.CatastrophicCount.ToString(CultureInfo.InvariantCulture),
                "target 0 runs", summary.CatastrophicCount == 0 ? Good : Bad);
        }

        private void ApplyLastEvaluation(ChampionRecord last)
        {
            if (last == null || last.summary == null)
            {
                lastEvalResult.text = "No evaluation yet";
                lastEvalResult.color = Muted;
                lastEvalDetail.text = string.Empty;
                return;
            }

            string when = FormatSteps(last.step) + " steps: ";
            if (last.restored)
            {
                lastEvalResult.text = when + "old brain restored";
                lastEvalResult.color = Warn;
            }
            else if (last.won)
            {
                lastEvalResult.text = when + "WON — now the best brain";
                lastEvalResult.color = Good;
            }
            else
            {
                lastEvalResult.text = when + "not better — best brain kept";
                lastEvalResult.color = Warn;
            }

            lastEvalDetail.text = "Median survival " + FormatSeconds(last.summary.MedianSurvivedSeconds) +
                "  •  worst 10% " + FormatSeconds(last.summary.P10SurvivedSeconds) +
                "  •  wins " + Percent(last.summary.WinRate) + "\nscored at " + FormatTime(last.When);
        }

        private void ApplyDeaths(ChampionRecord champion)
        {
            int runs = Math.Max(1, champion.summary.Runs);
            deathHeader.text = "CAUSES OF DEATH  (" + champion.summary.Runs + " scored runs)";
            List<KeyValuePair<string, int>> causes = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < DeathOrder.Length; i++)
            {
                if (champion.DeathCauses.TryGetValue(DeathOrder[i], out int count) && count > 0)
                {
                    causes.Add(new KeyValuePair<string, int>(DeathOrder[i], count));
                }
            }

            causes.Sort((a, b) => b.Value.CompareTo(a.Value));
            deathNone.gameObject.SetActive(causes.Count == 0);
            deathNone.text = "No deaths — survived to the end or killed the boss";
            for (int i = 0; i < deathRows.Length; i++)
            {
                bool visible = i < causes.Count;
                for (int part = 0; part < deathRows[i].Root.Length; part++)
                {
                    deathRows[i].Root[part].SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                deathRows[i].Name.text = DeathCauseName(causes[i].Key);
                deathRows[i].Count.text = causes[i].Value + " runs";
                deathRows[i].Fill.rectTransform.sizeDelta =
                    new Vector2(DeathBarWidth * Mathf.Clamp01(causes[i].Value / (float)runs), 16f);
            }
        }

        private void ApplyStyle(ChampionRecord champion, ChampionRecord previous)
        {
            ChampionBehavior now = champion.behavior;
            ChampionBehavior before = previous?.behavior;
            styleCompare.text = previous != null
                ? "vs the previous best brain (" + FormatSteps(previous.step) + " steps)"
                : "no previous best brain to compare with";

            for (int i = 0; i < traitRows.Length; i++)
            {
                float value = Traits[i].Read(now);
                TraitRow row = traitRows[i];
                if (value < 0f)
                {
                    row.Value.text = "—";
                    row.Value.color = Muted;
                    row.Fill.rectTransform.sizeDelta = new Vector2(0f, 18f);
                    row.Delta.text = "none yet";
                    row.Delta.color = Muted;
                    continue;
                }

                int percent = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
                row.Value.text = percent + "%";
                row.Value.color = Color.white;
                row.Fill.rectTransform.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(value), 18f);

                float old = before != null ? Traits[i].Read(before) : -1f;
                if (old < 0f)
                {
                    row.Delta.text = string.Empty;
                    continue;
                }

                int delta = percent - Mathf.RoundToInt(Mathf.Clamp01(old) * 100f);
                row.Delta.text = delta > 0 ? "+" + delta : delta < 0 ? delta.ToString(CultureInfo.InvariantCulture) : "=";
                row.Delta.color = delta > 0 ? Good : delta < 0 ? Bad : Muted;
            }

            int runs = Math.Max(1, champion.summary.Runs);
            string distance = "Distance to the nearest enemy: typically " + Metres(now.PreferredRange) +
                ", average " + Metres(now.KeepDistance);
            string skills = "Skills per run:  " +
                SkillLine("Kick", now.KickUses, now.EffectiveKicks, runs) + "   •   " +
                SkillLine("Block", now.BlockUses, now.EffectiveBlocks, runs) + "   •   " +
                SkillLine("Dash", now.DashUses, now.EffectiveDashes, runs);
            string economy = "Per minute: " + champion.summary.XpPerMinute.ToString("0", CultureInfo.InvariantCulture) +
                " EXP, " + champion.summary.GoldPerMinute.ToString("0.#", CultureInfo.InvariantCulture) +
                " gold  •  average level " + champion.summary.MeanLevel.ToString("0.#", CultureInfo.InvariantCulture);
            styleDetail.text = distance + "\n" + skills + "\n" + economy;
        }

        private void SetTile(int index, string label, string value, string note, Color color)
        {
            tiles[index].Label.text = label;
            tiles[index].Value.text = value;
            tiles[index].Value.color = color;
            tiles[index].Note.text = note;
        }

        private struct Tile
        {
            public Text Label;
            public Text Value;
            public Text Note;
        }

        private struct TraitRow
        {
            public Image Fill;
            public Text Value;
            public Text Delta;
        }

        private struct DeathRow
        {
            public Text Name;
            public Image Fill;
            public Text Count;
            public GameObject[] Root;
        }

        private readonly struct Trait
        {
            public readonly string Name;
            public readonly string Hint;
            public readonly Color Color;
            private readonly Func<ChampionBehavior, float> read;

            public Trait(string name, string hint, Color color, Func<ChampionBehavior, float> read)
            {
                Name = name;
                Hint = hint;
                Color = color;
                this.read = read;
            }

            public float Read(ChampionBehavior behavior)
            {
                return behavior == null ? -1f : read(behavior);
            }
        }
    }
}
