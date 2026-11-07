using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MiniJSON;
using UnityEngine;

namespace day_night_cycle
{
    // Kept next to the game's settings rather than in the mod folder, which Workshop updates can replace.
    internal sealed class cycle_settings
    {
        private static string file_path => FilePaths.getFolderPath("day_night_cycle.settings.json");

        public bool is_enabled = true;
        // Stops the clock at fixed_hour instead of following the park's time.
        public bool is_time_stopped;
        public float fixed_hour = 12f;
        public float hour_offset;
        // Six of the game's weeks, the same as its own automatic day and night.
        public float cycle_weeks = 6f;
        public float warmth = 1f;

        public static cycle_settings load()
        {
            cycle_settings settings = new cycle_settings();
            try
            {
                if (File.Exists(file_path) && Json.Deserialize(File.ReadAllText(file_path)) is Dictionary<string, object> values)
                {
                    settings.is_enabled = read_bool(values, "is_enabled", settings.is_enabled);
                    settings.is_time_stopped = read_bool(values, "is_time_stopped", settings.is_time_stopped);
                    settings.fixed_hour = read_float(values, "fixed_hour", settings.fixed_hour);
                    settings.hour_offset = read_float(values, "hour_offset", settings.hour_offset);
                    settings.cycle_weeks = read_float(values, "cycle_weeks", settings.cycle_weeks);
                    settings.warmth = read_float(values, "warmth", settings.warmth);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[day_night_cycle] Couldn't read " + file_path + ": " + exception);
            }
            return settings;
        }

        public void save()
        {
            try
            {
                Dictionary<string, object> values = new Dictionary<string, object>
                {
                    { "is_enabled", is_enabled },
                    { "is_time_stopped", is_time_stopped },
                    { "fixed_hour", fixed_hour },
                    { "hour_offset", hour_offset },
                    { "cycle_weeks", cycle_weeks },
                    { "warmth", warmth }
                };
                File.WriteAllText(file_path, Json.Serialize(values));
            }
            catch (Exception exception)
            {
                Debug.LogError("[day_night_cycle] Couldn't write " + file_path + ": " + exception);
            }
        }

        private static float read_float(Dictionary<string, object> values, string key, float fallback)
        {
            return values.TryGetValue(key, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        }

        private static bool read_bool(Dictionary<string, object> values, string key, bool fallback)
        {
            return values.TryGetValue(key, out object value) && value is bool flag ? flag : fallback;
        }
    }
}
