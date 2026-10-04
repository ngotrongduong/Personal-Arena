using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PersonalArena.View
{
    /// <summary>Draws the charts into textures: series, filled area, lines, points and the axis mapping.</summary>
    public sealed partial class TrainingHistoryPanel
    {
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
    }
}
