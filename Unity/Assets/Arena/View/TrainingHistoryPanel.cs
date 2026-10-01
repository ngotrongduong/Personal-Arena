using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Full-screen training history dashboard built entirely at runtime.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TrainingHistoryPanel : MonoBehaviour
    {
        private const int ChartCount = 6;
        private const int TextureWidth = 640;
        private const int TextureHeight = 300;
        private const int SmoothWindow = 8;
        private const float ReloadSeconds = 5f;
        private static readonly DateTime UnixEpoch =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly ChartView[] charts = new ChartView[ChartCount];
        private GameObject overlay;
        private GameObject chartGrid;
        private GameObject emptyState;
        private Text subtitle;
        private Font font;
        private string runDirectory;
        private DateTime lastWriteUtc = DateTime.MinValue;
        private bool historyWasPresent;
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

            overlay = CreateUiObject("Training History Overlay", canvasRoot);
            Image backdrop = overlay.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.025f, 0.04f, 0.92f);
            backdrop.raycastTarget = true;
            SetStretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform card = CreateImage("Training History Card", overlay.transform,
                new Color(0.055f, 0.065f, 0.09f, 1f), UiSprites.RoundedSprite(), Image.Type.Sliced);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1500f, 860f), new Vector2(0.5f, 0.5f));

            Text title = CreateText("Title", card, 34, TextAnchor.UpperLeft, Color.white);
            title.text = "TRAINING DATA — how the AI is learning";
            title.fontStyle = FontStyle.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(34f, -22f), new Vector2(1250f, 44f), new Vector2(0f, 1f));

            subtitle = CreateText("Subtitle", card, 17, TextAnchor.UpperLeft,
                new Color(0.68f, 0.74f, 0.83f, 1f));
            subtitle.text = "Waiting for training data • updates every few seconds";
            SetRect(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -68f), new Vector2(1320f, 28f), new Vector2(0f, 1f));

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

            chartGrid = CreateUiObject("Chart Grid", card);
            RectTransform gridRect = chartGrid.GetComponent<RectTransform>();
            SetRect(gridRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -112f),
                new Vector2(1432f, 720f), new Vector2(0f, 1f));
            BuildCharts(gridRect);

            emptyState = CreateUiObject("Empty State", card);
            RectTransform emptyRect = emptyState.GetComponent<RectTransform>();
            SetRect(emptyRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -225f),
                new Vector2(1280f, 420f), new Vector2(0f, 1f));
            Text emptyText = CreateText("Message", emptyRect, 27, TextAnchor.MiddleCenter,
                new Color(0.82f, 0.86f, 0.93f, 1f));
            emptyText.text = "The training data screen fills in once the AI has trained for a minute or two.\n" +
                "Press TRAIN THE AI to start.";
            emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetStretch(emptyText.rectTransform, 30f, 30f, 30f, 30f);

            ShowEmptyState();
            overlay.SetActive(isOpen);
            if (isOpen)
            {
                ReloadIfChanged();
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
                ReloadIfChanged();
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

        /// <summary>Selects the run whose training_history.json should be displayed.</summary>
        public void SetRunDirectory(string runDirectory)
        {
            if (this.runDirectory == runDirectory)
            {
                return;
            }

            this.runDirectory = runDirectory;
            lastWriteUtc = DateTime.MinValue;
            historyWasPresent = false;
            if (built)
            {
                ShowEmptyState();
                if (isOpen)
                {
                    ReloadIfChanged();
                }
            }
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
                ReloadIfChanged();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < charts.Length; i++)
            {
                if (charts[i]?.Texture != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(charts[i].Texture);
                    }
                    else
                    {
                        DestroyImmediate(charts[i].Texture);
                    }
                }
            }
        }

        private void BuildCharts(Transform parent)
        {
            ChartDefinition[] definitions =
            {
                new ChartDefinition("Reward (overall score)", "Higher = the AI is doing better overall",
                    "Environment/Cumulative Reward", null, new Color(0.3f, 0.86f, 0.48f), false),
                new ChartDefinition("Zombies killed per round", "Higher = more zombies defeated each round",
                    "Arena/Kills", null, new Color(1f, 0.58f, 0.22f), false),
                new ChartDefinition("Seconds survived", "Higher = the AI stays alive longer",
                    "Arena/SurvivedSeconds", null, new Color(0.3f, 0.65f, 1f), false),
                new ChartDefinition("Died before the timer", "Lower = the AI dies less often",
                    "Arena/Died", null, new Color(1f, 0.34f, 0.34f), true),
                new ChartDefinition("Level reached per round", "Higher = more EXP collected and upgrades picked",
                    "Arena/Level", null, new Color(0.72f, 0.45f, 1f), false),
                new ChartDefinition("Round length in training (seconds)",
                    "Goes up as the AI passes each lesson", "Arena/RunSeconds",
                    "Environment/Lesson Number/run_seconds", new Color(1f, 0.83f, 0.28f), false)
            };

            const float columnGap = 18f;
            const float rowGap = 18f;
            float width = (1432f - columnGap * 2f) / 3f;
            float height = (720f - rowGap) / 2f;
            for (int i = 0; i < definitions.Length; i++)
            {
                int column = i % 3;
                int row = i / 3;
                RectTransform card = CreateImage("Chart " + (i + 1), parent,
                    new Color(0.085f, 0.1f, 0.135f, 1f), UiSprites.RoundedSprite(), Image.Type.Sliced);
                SetRect(card, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(column * (width + columnGap), -row * (height + rowGap)),
                    new Vector2(width, height), new Vector2(0f, 1f));
                charts[i] = CreateChart(card, definitions[i], width, height);
            }
        }

        private ChartView CreateChart(Transform parent, ChartDefinition definition, float width, float height)
        {
            ChartView chart = new ChartView(definition);
            Text title = CreateText("Title", parent, 19, TextAnchor.UpperLeft, Color.white);
            title.text = definition.Title;
            title.fontStyle = FontStyle.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -10f),
                new Vector2(width - 112f, 28f), new Vector2(0f, 1f));

            chart.Current = CreateText("Current", parent, 20, TextAnchor.UpperRight, definition.Color);
            chart.Current.fontStyle = FontStyle.Bold;
            SetRect(chart.Current.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -9f), new Vector2(96f, 30f), new Vector2(1f, 1f));

            Text hint = CreateText("Hint", parent, 14, TextAnchor.UpperLeft,
                new Color(0.66f, 0.72f, 0.81f, 1f));
            hint.text = definition.Hint;
            SetRect(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -39f),
                new Vector2(width - 32f, 22f), new Vector2(0f, 1f));

            GameObject graphObject = CreateUiObject("Graph", parent);
            chart.Image = graphObject.AddComponent<RawImage>();
            chart.Image.raycastTarget = false;
            SetRect(chart.Image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(52f, -70f), new Vector2(width - 68f, height - 112f), new Vector2(0f, 1f));

            chart.Texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                name = definition.Title + " Chart",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            chart.Pixels = new Color32[TextureWidth * TextureHeight];
            chart.Image.texture = chart.Texture;

            chart.Maximum = CreateText("Maximum", parent, 12, TextAnchor.UpperRight,
                new Color(0.63f, 0.69f, 0.78f, 1f));
            SetRect(chart.Maximum.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(4f, -70f), new Vector2(43f, 20f), new Vector2(0f, 1f));
            chart.Minimum = CreateText("Minimum", parent, 12, TextAnchor.LowerRight,
                new Color(0.63f, 0.69f, 0.78f, 1f));
            SetRect(chart.Minimum.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(4f, 25f), new Vector2(43f, 20f), new Vector2(0f, 0f));

            chart.StepStart = CreateText("Step Start", parent, 12, TextAnchor.UpperLeft,
                new Color(0.63f, 0.69f, 0.78f, 1f));
            SetRect(chart.StepStart.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(52f, 5f), new Vector2(90f, 18f), new Vector2(0f, 0f));
            chart.StepMiddle = CreateText("Step Middle", parent, 12, TextAnchor.UpperCenter,
                new Color(0.63f, 0.69f, 0.78f, 1f));
            SetRect(chart.StepMiddle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(18f, 5f), new Vector2(90f, 18f), new Vector2(0.5f, 0f));
            chart.StepEnd = CreateText("Step End", parent, 12, TextAnchor.UpperRight,
                new Color(0.63f, 0.69f, 0.78f, 1f));
            SetRect(chart.StepEnd.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f, 5f), new Vector2(90f, 18f), new Vector2(1f, 0f));

            chart.NoData = CreateText("No Data", parent, 19, TextAnchor.MiddleCenter,
                new Color(0.65f, 0.7f, 0.78f, 1f));
            chart.NoData.text = "No data yet";
            SetRect(chart.NoData.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(52f, -70f), new Vector2(width - 68f, height - 112f), new Vector2(0f, 1f));
            SetChartHasData(chart, false);
            return chart;
        }

        private void ReloadIfChanged()
        {
            if (!built || string.IsNullOrEmpty(runDirectory))
            {
                ShowEmptyState();
                return;
            }

            string path;
            DateTime written;
            try
            {
                path = Path.Combine(runDirectory, TrainingHistory.FileName);
                if (!File.Exists(path))
                {
                    if (historyWasPresent)
                    {
                        lastWriteUtc = DateTime.MinValue;
                        historyWasPresent = false;
                        ShowEmptyState();
                    }
                    return;
                }
                written = File.GetLastWriteTimeUtc(path);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (ArgumentException)
            {
                ShowEmptyState();
                return;
            }

            if (historyWasPresent && written == lastWriteUtc)
            {
                return;
            }

            TrainingHistory history = TrainingHistory.Load(runDirectory);
            if (history == null)
            {
                return;
            }

            lastWriteUtc = written;
            historyWasPresent = true;
            ApplyHistory(history);
        }

        private void ApplyHistory(TrainingHistory history)
        {
            chartGrid.SetActive(true);
            emptyState.SetActive(false);
            subtitle.text = HistorySubtitle(history);
            for (int i = 0; i < charts.Length; i++)
            {
                ChartView chart = charts[i];
                TrainingSeries series = history.Find(chart.Definition.Tag);
                if (series == null && chart.Definition.FallbackTag != null)
                {
                    series = history.Find(chart.Definition.FallbackTag);
                }
                RenderChart(chart, series);
            }
        }

        private void ShowEmptyState()
        {
            if (!built)
            {
                return;
            }

            chartGrid.SetActive(false);
            emptyState.SetActive(true);
            subtitle.text = "Waiting for training data • updates every few seconds";
        }

        private static void RenderChart(ChartView chart, TrainingSeries series)
        {
            int count = series == null || series.steps == null || series.values == null
                ? 0
                : Math.Min(series.steps.Length, series.values.Length);
            if (count == 0)
            {
                SetChartHasData(chart, false);
                return;
            }

            float multiplier = chart.Definition.Percent ? 100f : 1f;
            float[] values = new float[count];
            long[] steps = new long[count];
            bool hasFinite = false;
            float minimum = float.MaxValue;
            float maximum = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                steps[i] = series.steps[i];
                values[i] = series.values[i] * multiplier;
                if (!float.IsNaN(values[i]) && !float.IsInfinity(values[i]))
                {
                    minimum = Mathf.Min(minimum, values[i]);
                    maximum = Mathf.Max(maximum, values[i]);
                    hasFinite = true;
                }
            }

            if (!hasFinite)
            {
                SetChartHasData(chart, false);
                return;
            }

            float[] smooth = TrainingHistory.Smooth(values, SmoothWindow);
            float range = maximum - minimum;
            if (range < 1e-5f)
            {
                float padding = Mathf.Max(Mathf.Abs(maximum) * 0.1f, chart.Definition.Percent ? 1f : 0.5f);
                minimum -= padding;
                maximum += padding;
            }
            else
            {
                float padding = range * 0.08f;
                minimum -= padding;
                maximum += padding;
            }
            if (chart.Definition.Percent)
            {
                minimum = Mathf.Max(0f, minimum);
                maximum = Mathf.Min(100f, maximum);
                maximum = Mathf.Max(maximum, minimum + 1f);
            }

            long minimumStep = 0L;
            long maximumStep = 0L;
            for (int i = 0; i < count; i++)
            {
                maximumStep = Math.Max(maximumStep, steps[i]);
            }

            DrawGraph(chart, steps, values, smooth, count, minimumStep, maximumStep, minimum, maximum);
            chart.Current.text = FormatValue(values[count - 1], chart.Definition.Percent);
            chart.Minimum.text = FormatValue(minimum, chart.Definition.Percent);
            chart.Maximum.text = FormatValue(maximum, chart.Definition.Percent);
            chart.StepStart.text = FormatSteps(minimumStep);
            chart.StepMiddle.text = FormatSteps(minimumStep + (maximumStep - minimumStep) / 2);
            chart.StepEnd.text = FormatSteps(maximumStep);
            SetChartHasData(chart, true);
        }

        private static void DrawGraph(ChartView chart, long[] steps, float[] values, float[] smooth,
            int count, long minimumStep, long maximumStep, float minimum, float maximum)
        {
            Color32 background = new Color32(18, 22, 31, 255);
            for (int i = 0; i < chart.Pixels.Length; i++)
            {
                chart.Pixels[i] = background;
            }

            Color32 grid = new Color32(145, 158, 180, 34);
            for (int line = 0; line <= 4; line++)
            {
                int x = Mathf.RoundToInt(line * (TextureWidth - 1) / 4f);
                int y = Mathf.RoundToInt(line * (TextureHeight - 1) / 4f);
                DrawLine(chart.Pixels, x, 0, x, TextureHeight - 1, grid, 0.6f);
                DrawLine(chart.Pixels, 0, y, TextureWidth - 1, y, grid, 0.6f);
            }

            Color32 area = ColorWithAlpha(chart.Definition.Color, 0.12f);
            for (int i = 1; i < count; i++)
            {
                if (!IsFinite(smooth[i - 1]) || !IsFinite(smooth[i]))
                {
                    continue;
                }

                int x0 = MapX(steps[i - 1], i - 1, count, minimumStep, maximumStep);
                int x1 = MapX(steps[i], i, count, minimumStep, maximumStep);
                int y0 = MapY(smooth[i - 1], minimum, maximum);
                int y1 = MapY(smooth[i], minimum, maximum);
                FillArea(chart.Pixels, x0, y0, x1, y1, area);
            }

            Color32 raw = ColorWithAlpha(chart.Definition.Color, 0.32f);
            DrawSeries(chart.Pixels, steps, values, count, minimumStep, maximumStep, minimum, maximum,
                raw, 0.9f);
            Color32 bright = ColorWithAlpha(chart.Definition.Color, 0.98f);
            DrawSeries(chart.Pixels, steps, smooth, count, minimumStep, maximumStep, minimum, maximum,
                bright, 2.2f);
            chart.Texture.SetPixels32(chart.Pixels);
            chart.Texture.Apply(false, false);
        }

        private static void DrawSeries(Color32[] pixels, long[] steps, float[] values, int count,
            long minimumStep, long maximumStep, float minimum, float maximum, Color32 color, float radius)
        {
            if (count == 1 && IsFinite(values[0]))
            {
                DrawPoint(pixels, TextureWidth / 2, MapY(values[0], minimum, maximum), color, radius + 1f);
                return;
            }

            for (int i = 1; i < count; i++)
            {
                if (!IsFinite(values[i - 1]) || !IsFinite(values[i]))
                {
                    continue;
                }

                int x0 = MapX(steps[i - 1], i - 1, count, minimumStep, maximumStep);
                int x1 = MapX(steps[i], i, count, minimumStep, maximumStep);
                int y0 = MapY(values[i - 1], minimum, maximum);
                int y1 = MapY(values[i], minimum, maximum);
                DrawLine(pixels, x0, y0, x1, y1, color, radius);
            }
        }

        private static void FillArea(Color32[] pixels, int x0, int y0, int x1, int y1, Color32 color)
        {
            if (x1 < x0)
            {
                Swap(ref x0, ref x1);
                Swap(ref y0, ref y1);
            }

            int width = Math.Max(1, x1 - x0);
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(TextureWidth - 1, x1); x++)
            {
                float t = (x - x0) / (float)width;
                int top = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int y = 0; y <= Mathf.Clamp(top, 0, TextureHeight - 1); y++)
                {
                    Blend(pixels, x, y, color);
                }
            }
        }

        private static void DrawLine(Color32[] pixels, int x0, int y0, int x1, int y1,
            Color32 color, float radius)
        {
            int dx = x1 - x0;
            int dy = y1 - y0;
            int samples = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (samples == 0)
            {
                DrawPoint(pixels, x0, y0, color, radius);
                return;
            }

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                DrawPoint(pixels, Mathf.RoundToInt(x0 + dx * t), Mathf.RoundToInt(y0 + dy * t),
                    color, radius);
            }
        }

        private static void DrawPoint(Color32[] pixels, int centreX, int centreY, Color32 color, float radius)
        {
            int reach = Mathf.CeilToInt(radius + 0.5f);
            for (int y = centreY - reach; y <= centreY + reach; y++)
            {
                for (int x = centreX - reach; x <= centreX + reach; x++)
                {
                    if (x < 0 || x >= TextureWidth || y < 0 || y >= TextureHeight)
                    {
                        continue;
                    }

                    float dx = x - centreX;
                    float dy = y - centreY;
                    float coverage = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    if (coverage <= 0f)
                    {
                        continue;
                    }

                    Color32 covered = color;
                    covered.a = (byte)Mathf.RoundToInt(color.a * coverage);
                    Blend(pixels, x, y, covered);
                }
            }
        }

        private static void Blend(Color32[] pixels, int x, int y, Color32 source)
        {
            int index = y * TextureWidth + x;
            Color32 target = pixels[index];
            int alpha = source.a;
            int inverse = 255 - alpha;
            target.r = (byte)((source.r * alpha + target.r * inverse) / 255);
            target.g = (byte)((source.g * alpha + target.g * inverse) / 255);
            target.b = (byte)((source.b * alpha + target.b * inverse) / 255);
            target.a = 255;
            pixels[index] = target;
        }

        private static int MapX(long step, int index, int count, long minimum, long maximum)
        {
            if (maximum <= minimum)
            {
                return count <= 1 ? TextureWidth / 2 : Mathf.RoundToInt(index * (TextureWidth - 1f) / (count - 1));
            }

            double ratio = (step - minimum) / (double)(maximum - minimum);
            return Mathf.Clamp(Mathf.RoundToInt((float)ratio * (TextureWidth - 1)), 0, TextureWidth - 1);
        }

        private static int MapY(float value, float minimum, float maximum)
        {
            float ratio = Mathf.InverseLerp(minimum, maximum, value);
            return Mathf.Clamp(Mathf.RoundToInt(ratio * (TextureHeight - 1)), 0, TextureHeight - 1);
        }

        private static void SetChartHasData(ChartView chart, bool hasData)
        {
            chart.Image.gameObject.SetActive(hasData);
            chart.Minimum.gameObject.SetActive(hasData);
            chart.Maximum.gameObject.SetActive(hasData);
            chart.StepStart.gameObject.SetActive(hasData);
            chart.StepMiddle.gameObject.SetActive(hasData);
            chart.StepEnd.gameObject.SetActive(hasData);
            chart.NoData.gameObject.SetActive(!hasData);
            chart.Current.text = hasData ? chart.Current.text : "—";
        }

        private static string HistorySubtitle(TrainingHistory history)
        {
            string run = string.IsNullOrEmpty(history.run_id) ? "Training run" : history.run_id;
            string updated = FormatUpdateTime(history.updated_unix);
            return run + " • " + FormatSteps(history.last_step) + " steps • " + updated +
                " • updates every few seconds";
        }

        private static string FormatUpdateTime(double unixSeconds)
        {
            if (double.IsNaN(unixSeconds) || double.IsInfinity(unixSeconds))
            {
                return "update time unknown";
            }

            try
            {
                DateTime local = UnixEpoch.AddSeconds(unixSeconds).ToLocalTime();
                return "updated " + local.ToString("d MMM, h:mm tt");
            }
            catch (ArgumentOutOfRangeException)
            {
                return "update time unknown";
            }
        }

        private static string FormatSteps(long value)
        {
            long absolute = Math.Abs(value);
            if (absolute >= 1000000000L)
            {
                return (value / 1000000000f).ToString("0.#") + "B";
            }
            if (absolute >= 1000000L)
            {
                return (value / 1000000f).ToString("0.#") + "M";
            }
            if (absolute >= 1000L)
            {
                return (value / 1000f).ToString("0.#") + "K";
            }
            return value.ToString();
        }

        private static string FormatValue(float value, bool percent)
        {
            if (!IsFinite(value))
            {
                return "—";
            }

            if (percent)
            {
                return value.ToString("0.#") + "%";
            }
            if (Mathf.Abs(value) >= 1000f)
            {
                return FormatSteps(Mathf.RoundToInt(value));
            }
            return value.ToString(Mathf.Abs(value) < 10f ? "0.0" : "0");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Color32 ColorWithAlpha(Color color, float alpha)
        {
            Color adjusted = color;
            adjusted.a = alpha;
            return adjusted;
        }

        private static void Swap(ref int first, ref int second)
        {
            int value = first;
            first = second;
            second = value;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
        }

        private RectTransform CreateImage(string objectName, Transform parent, Color color,
            Sprite sprite, Image.Type type)
        {
            GameObject imageObject = CreateUiObject(objectName, parent);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = type;
            return image.rectTransform;
        }

        private Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment, Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private sealed class ChartView
        {
            public readonly ChartDefinition Definition;
            public RawImage Image;
            public Texture2D Texture;
            public Color32[] Pixels;
            public Text Current;
            public Text Minimum;
            public Text Maximum;
            public Text StepStart;
            public Text StepMiddle;
            public Text StepEnd;
            public Text NoData;

            public ChartView(ChartDefinition definition)
            {
                Definition = definition;
            }
        }

        private readonly struct ChartDefinition
        {
            public readonly string Title;
            public readonly string Hint;
            public readonly string Tag;
            public readonly string FallbackTag;
            public readonly Color Color;
            public readonly bool Percent;

            public ChartDefinition(string title, string hint, string tag, string fallbackTag,
                Color color, bool percent)
            {
                Title = title;
                Hint = hint;
                Tag = tag;
                FallbackTag = fallbackTag;
                Color = color;
                Percent = percent;
            }
        }
    }
}
