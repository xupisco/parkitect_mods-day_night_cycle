using System.Reflection;
using UnityEngine;

namespace day_night_cycle
{
    // Takes over the light from the game's LightMoodController while enabled. That controller keeps writing its own
    // values in Update, so this writes over them in LateUpdate, without needing to patch it.
    internal sealed class sky_controller : MonoBehaviour
    {
        private const float sunrise_hour = 5f;
        private const float sunset_hour = 21f;
        // Below this elevation the light fades into night, and it starts turning warm below warm_elevation.
        private const float twilight_elevation = 12f;
        private const float warm_elevation = 28f;
        private const float lights_on_below_daylight = 0.35f;
        private const float night_skybox_exposure = 0.025f;
        // The procedural sky turns orange by itself with a low sun, and a thicker atmosphere makes that stronger.
        private const float sunset_atmosphere_thickness = 1.6f;
        // The same as the game: rain dims the sun, and a builder window makes the night easier to see.
        private const float rain_key_intensity = 0.33f;
        private const float builder_daylight = 0.3f;
        private const float fade_speed = 0.5f;
        private const float seconds_per_week = 75f;
        private const float seconds_between_searches = 1f;

        private static readonly Color warm_color = new Color(1f, 0.52f, 0.28f);
        private static readonly Color moon_color = new Color(0.62f, 0.72f, 1f);

        private const BindingFlags private_field = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo default_key_intensity = typeof(LightMoodController).GetField("defaultKeyLightIntensity", private_field);
        private static readonly FieldInfo default_key_shadow = typeof(LightMoodController).GetField("defaultKeyLightShadowStrength", private_field);
        private static readonly FieldInfo default_fill_intensity = typeof(LightMoodController).GetField("defaultFillLightIntensity", private_field);
        private static readonly FieldInfo default_ambient = typeof(LightMoodController).GetField("defaultAmbientColor", private_field);
        private static readonly FieldInfo default_exposure = typeof(LightMoodController).GetField("defaultSkyboxExposure", private_field);

        private int night_mode_id;
        private int exposure_id;
        private int atmosphere_id;

        public cycle_settings settings;

        private bool is_active;
        private LightMoodController mood;
        private Quaternion original_key_rotation;
        private Color original_key_color;
        // A shared material, so a change here outlives the park unless it's undone.
        private Material atmosphere_material;
        private float original_atmosphere;
        private bool previous_day_night_auto;
        private float next_search_time;
        private float rain_amount;
        private float builder_amount;

        private void Awake()
        {
            night_mode_id = Shader.PropertyToID("_NightMode");
            exposure_id = Shader.PropertyToID("_Exposure");
            atmosphere_id = Shader.PropertyToID("_AtmosphereThickness");
        }

        // The hour the park's time points to, before the stopped clock is taken into account.
        private float get_cycle_hour()
        {
            float cycle_seconds = Mathf.Max(1f, settings.cycle_weeks) * seconds_per_week;
            float progress = (float)(ParkInfo.ParkTimeDouble % cycle_seconds) / cycle_seconds;
            return progress * 24f;
        }

        public float get_hour()
        {
            if (settings.is_time_stopped)
            {
                return settings.fixed_hour;
            }
            return Mathf.Repeat(get_cycle_hour() + settings.hour_offset, 24f);
        }

        // Keeps the clock running from the chosen hour, unless it's stopped.
        public void set_hour(float hour)
        {
            settings.fixed_hour = hour;
            settings.hour_offset = Mathf.Repeat(hour - get_cycle_hour(), 24f);
        }

        private bool try_begin()
        {
            if (Time.unscaledTime < next_search_time || GameController.Instance == null || GameController.Instance.park == null)
            {
                return false;
            }
            next_search_time = Time.unscaledTime + seconds_between_searches;
            mood = FindObjectOfType<LightMoodController>();
            // While a park loads, it exists for a moment before its weather does.
            WeatherController weather = GameController.Instance.park.weatherController;
            if (mood == null || weather == null)
            {
                return false;
            }

            original_key_rotation = mood.keyLight.transform.rotation;
            original_key_color = mood.keyLight.color;
            atmosphere_material = RenderSettings.skybox != null && RenderSettings.skybox.HasProperty(atmosphere_id) ? RenderSettings.skybox : null;
            if (atmosphere_material != null)
            {
                original_atmosphere = atmosphere_material.GetFloat(atmosphere_id);
            }
            // Both would fight over IsNight otherwise.
            previous_day_night_auto = weather.IsDayNightAuto;
            weather.IsDayNightAuto = false;
            is_active = true;
            return true;
        }

        private void end()
        {
            is_active = false;
            if (mood != null)
            {
                mood.keyLight.transform.rotation = original_key_rotation;
                mood.keyLight.color = original_key_color;
            }
            restore_atmosphere();
            WeatherController weather = GameController.Instance?.park?.weatherController;
            if (weather != null)
            {
                weather.IsDayNightAuto = previous_day_night_auto;
            }
        }

        private void restore_atmosphere()
        {
            if (atmosphere_material != null)
            {
                atmosphere_material.SetFloat(atmosphere_id, original_atmosphere);
            }
        }

        private void LateUpdate()
        {
            if (settings.is_enabled != is_active)
            {
                if (is_active)
                {
                    end();
                }
                else if (!try_begin())
                {
                    return;
                }
            }
            if (!is_active)
            {
                return;
            }
            if (mood == null)
            {
                // The park is gone, and with it the lights and the weather that end() would restore.
                is_active = false;
                restore_atmosphere();
                return;
            }
            apply(get_hour());
        }

        private void apply(float hour)
        {
            WeatherController weather = GameController.Instance.park.weatherController;
            float base_elevation = original_key_rotation.eulerAngles.x;
            float base_azimuth = original_key_rotation.eulerAngles.y;

            // Runs from 0 at sunrise to 1 at sunset, and outside that range the sun is below the horizon.
            float sun_progress = (hour - sunrise_hour) / (sunset_hour - sunrise_hour);
            bool sun_is_up = sun_progress > 0f && sun_progress < 1f;
            float elevation = sun_is_up ? Mathf.Sin(sun_progress * Mathf.PI) * base_elevation : 0f;
            float daylight = Mathf.SmoothStep(0f, 1f, elevation / twilight_elevation);
            float warm = sun_is_up ? (1f - Mathf.SmoothStep(0f, 1f, elevation / warm_elevation)) * settings.warmth : 0f;

            builder_amount = Mathf.Lerp(builder_amount, GameController.Instance.hasActiveBuilderWindow() ? builder_daylight : 0f, Time.deltaTime * fade_speed);
            rain_amount = Mathf.Lerp(rain_amount, weather.IsRaining ? 1f : 0f, Time.deltaTime * fade_speed);
            float visible_daylight = Mathf.Max(daylight, builder_amount);

            Light key = mood.keyLight;
            if (sun_is_up)
            {
                // The arc passes through the game's own sun direction at noon.
                key.transform.rotation = Quaternion.Euler(Mathf.Max(elevation, 1f), base_azimuth + Mathf.Lerp(-90f, 90f, sun_progress), 0f);
                key.color = Color.Lerp(original_key_color, warm_color, warm);
            }
            else
            {
                // The moon: the same direction all night, because a jump would show when it takes over from the sun,
                // and that happens while the light is at its lowest.
                key.transform.rotation = Quaternion.Euler(base_elevation, base_azimuth + 180f, 0f);
                key.color = Color.Lerp(original_key_color, moon_color, 0.6f);
            }

            float night_mode = 1f - visible_daylight;
            key.intensity = Mathf.Lerp(mood.keyLightIntensityNight, (float)default_key_intensity.GetValue(mood), visible_daylight) * Mathf.Lerp(1f, rain_key_intensity, rain_amount);
            key.shadowStrength = Mathf.Lerp(0f, (float)default_key_shadow.GetValue(mood), daylight) * (1f - rain_amount);
            key.shadows = key.shadowStrength > 0.08f ? LightShadows.Soft : LightShadows.None;
            mood.fillLight.intensity = Mathf.Lerp(mood.fillLightIntensityNight, (float)default_fill_intensity.GetValue(mood), visible_daylight);
            // During rain the game's own value stays, because its lightning flashes go through the ambient light.
            if (!weather.IsRaining)
            {
                RenderSettings.ambientLight = Color.Lerp(mood.ambientColorNight, (Color)default_ambient.GetValue(mood), visible_daylight);
            }

            Shader.SetGlobalFloat(night_mode_id, night_mode);
            if (RenderSettings.skybox != null)
            {
                RenderSettings.skybox.SetFloat(exposure_id, Mathf.Lerp((float)default_exposure.GetValue(mood), night_skybox_exposure, night_mode));
            }
            if (atmosphere_material != null)
            {
                atmosphere_material.SetFloat(atmosphere_id, Mathf.Lerp(original_atmosphere, sunset_atmosphere_thickness, warm));
            }

            // Turns the park's lights on and off, and is what the rest of the game knows about the time of day.
            // Also overrides the game's day and night button while the cycle is on.
            bool should_be_night = daylight < lights_on_below_daylight;
            if (weather.IsNight != should_be_night)
            {
                weather.IsNight = should_be_night;
            }
        }

        private void OnDestroy()
        {
            if (is_active)
            {
                end();
            }
        }
    }
}
