using System;
using System.Reflection;
using Parkitect.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace day_night_cycle
{
    // The game's windows need a UIWindow as their content, and the class itself has nothing to add.
    internal sealed class cycle_window_content : UIWindow
    {
    }

    // Reads the hotkey and builds the controls as a native game window, from the same parts as a ride's settings.
    internal sealed class cycle_window : MonoBehaviour
    {
        private const string window_tag = "day_night_cycle";
        // Also the name the game remembers the window's position under.
        private const string window_name = "DayNightCycleWindow";
        private const float window_width = 420f;

        private const BindingFlags private_field = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo slider_field = typeof(AttractionSettingNumberSliderUI).GetField("numberSlider", private_field);
        private static readonly FieldInfo number_text_field = typeof(AttractionSettingNumberSliderUI).GetField("numberText", private_field);
        private static readonly FieldInfo toggle_field = typeof(AttractionSettingToggleUI).GetField("toggle", private_field);

        public sky_controller controller;
        public cycle_settings settings;

        private UIWindowFrame frame;
        private GameObject parts_holder;
        private Slider time_slider;
        private TextMeshProUGUI time_number;

        private void Update()
        {
            if (InputManager.getKeyDown(day_night_cycle_mod.window_hotkey_id) && !UIUtility.isInputFieldFocused())
            {
                if (frame != null)
                {
                    frame.close();
                }
                else
                {
                    open();
                }
            }

            // Follows the clock while it runs, except while the slider is being dragged.
            if (frame != null && !Input.GetMouseButton(0))
            {
                float hour = controller.get_hour();
                time_slider.SetValueWithoutNotify(hour);
                time_number.text = format_hour(hour);
            }
        }

        private void open()
        {
            // Parts are prepared in here, because a game object that was never active doesn't run OnDestroy.
            parts_holder = new GameObject("day_night_cycle_parts");
            parts_holder.SetActive(false);
            parts_holder.transform.SetParent(transform, false);

            GameObject content_object = new GameObject(window_name, typeof(RectTransform));
            VerticalLayoutGroup layout = content_object.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            UIWindowSettings window_settings = content_object.AddComponent<UIWindowSettings>();
            window_settings.title = "Day Night Cycle";
            window_settings.resizeability = 0;
            window_settings.pinnable = false;
            window_settings.contentDeterminesWindowSize = true;

            GameObject spacer = new GameObject("width", typeof(RectTransform));
            spacer.transform.SetParent(content_object.transform, false);
            spacer.AddComponent<LayoutElement>().minWidth = window_width;

            add_toggle_row(content_object.transform, "Day and night cycle", settings.is_enabled, value => settings.is_enabled = value);
            add_toggle_row(content_object.transform, "Stop the clock", settings.is_time_stopped, on_stop_clock_changed);
            add_slider_row(content_object.transform, "Time of day", 0f, 24f, controller.get_hour(), format_hour, value => controller.set_hour(value), true);
            add_slider_row(content_object.transform, "Day length (weeks)", 1f, 24f, settings.cycle_weeks, value => value.ToString("0"), on_day_length_changed, false);
            add_slider_row(content_object.transform, "Sunset color", 0f, 1f, settings.warmth, value => value.ToString("0.00"), value => settings.warmth = value, false);

            cycle_window_content content = content_object.AddComponent<cycle_window_content>();
            frame = UIWindowsController.Instance.spawnWindow(content, window_tag);
            frame.OnClose += on_frame_closed;

            Destroy(parts_holder);
        }

        private void on_stop_clock_changed(bool stopped)
        {
            // Stops at the hour on screen, and picks up from it when the clock starts again.
            controller.set_hour(controller.get_hour());
            settings.is_time_stopped = stopped;
        }

        private void on_day_length_changed(float weeks)
        {
            // The hour comes from the length of the cycle, so without this the sun would jump.
            float hour = controller.get_hour();
            settings.cycle_weeks = Mathf.Round(weeks);
            controller.set_hour(hour);
        }

        private void on_frame_closed(UIWindowFrame closed_frame)
        {
            frame = null;
            settings.save();
        }

        private void add_toggle_row(Transform parent, string label, bool value, Action<bool> write)
        {
            AttractionSettingToggleUI row = Instantiate(ScriptableSingleton<UIAssetManager>.Instance.attractionSettingToggleUIGO, parts_holder.transform);
            row.setLabelText(label);
            Toggle toggle = (Toggle)toggle_field.GetValue(row);
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(new_value => write(new_value));
            take_part(row, parent);
        }

        private void add_slider_row(Transform parent, string label, float minimum, float maximum, float value, Func<float, string> format, Action<float> write, bool is_time)
        {
            AttractionSettingNumberSliderUI row = Instantiate(ScriptableSingleton<UIAssetManager>.Instance.attractionSettingNumberSliderUIGO, parts_holder.transform);
            row.setLabelText(label);
            Slider slider = (Slider)slider_field.GetValue(row);
            TextMeshProUGUI number = (TextMeshProUGUI)number_text_field.GetValue(row);
            slider.onValueChanged = new Slider.SliderEvent();
            slider.wholeNumbers = false;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.SetValueWithoutNotify(value);
            number.text = format(value);
            slider.onValueChanged.AddListener(new_value =>
            {
                write(new_value);
                number.text = format(new_value);
            });
            if (is_time)
            {
                time_slider = slider;
                time_number = number;
            }
            take_part(row, parent);
        }

        private static string format_hour(float hour)
        {
            int minutes = Mathf.FloorToInt(hour * 60f) % (24 * 60);
            return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }

        // The part's own script expects a ride's setting, and would throw without one both when used and when destroyed.
        // Removing it while it has never been active means its OnDestroy doesn't run.
        private static void take_part(Component row_script, Transform parent)
        {
            Transform row = row_script.transform;
            DestroyImmediate(row_script);
            row.SetParent(parent, false);
        }

        private void OnDestroy()
        {
            if (frame != null)
            {
                frame.OnClose -= on_frame_closed;
                frame.close();
            }
        }
    }
}
