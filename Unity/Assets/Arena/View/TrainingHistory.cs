using System;
using System.IO;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>One scalar time series exported from a training run.</summary>
    [Serializable]
    public sealed class TrainingSeries
    {
        public string tag;
        public long[] steps;
        public float[] values;
    }

    /// <summary>Compact training history written by Trainer/training_history.py.</summary>
    [Serializable]
    public sealed class TrainingHistory
    {
        public const string FileName = "training_history.json";

        public string run_id;
        public string behavior;
        public int rules_version;
        public double updated_unix;
        public long last_step;
        public TrainingSeries[] series;

        /// <summary>Parses a history document, returning null for empty or malformed input.</summary>
        public static TrainingHistory Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                TrainingHistory history = JsonUtility.FromJson<TrainingHistory>(json);
                if (history == null || string.IsNullOrEmpty(history.run_id) || history.series == null)
                {
                    return null;
                }

                return history;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Finds a series by its exact TensorBoard tag.</summary>
        public TrainingSeries Find(string tag)
        {
            if (series == null || string.IsNullOrEmpty(tag))
            {
                return null;
            }

            for (int i = 0; i < series.Length; i++)
            {
                if (series[i] != null && series[i].tag == tag)
                {
                    return series[i];
                }
            }

            return null;
        }

        /// <summary>Loads training_history.json from a run directory, or null when unavailable.</summary>
        public static TrainingHistory Load(string runDirectory)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                return null;
            }

            try
            {
                string path = Path.Combine(runDirectory, FileName);
                if (!File.Exists(path))
                {
                    return null;
                }

                return Parse(File.ReadAllText(path));
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Returns a trailing moving average with the same length as the input.</summary>
        public static float[] Smooth(float[] values, int window)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<float>();
            }

            int width = Math.Max(1, window);
            float[] smoothed = new float[values.Length];
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
                if (i >= width)
                {
                    sum -= values[i - width];
                }

                smoothed[i] = (float)(sum / Math.Min(i + 1, width));
            }

            return smoothed;
        }
    }
}
