using UnityEngine;

namespace day_night_cycle
{
    // Entry point. The game discovers this type through GetExportedTypes, so it is the only public type in the assembly.
    public class day_night_cycle_mod : AbstractMod
    {
        private const string identifier = "xupisco@DayNightCycle";
        internal const string window_hotkey_id = identifier + "/window";

        private KeyGroup key_group;
        private KeyMapping key_mapping;
        private GameObject controller_object;
        private cycle_settings settings;

        public override string getName()
        {
            return "Day Night Cycle";
        }

        public override string getDescription()
        {
            return "Moves the sun across the sky, with sunrises, sunsets and a gradual night.";
        }

        public override string getVersionNumber()
        {
            return "0.2.0";
        }

        public override string getIdentifier()
        {
            return identifier;
        }

        // Stays off until it has been tested with two clients.
        public override bool isMultiplayerModeCompatible()
        {
            return false;
        }

        public override void onEnabled()
        {
            // The group is registered explicitly, because registering one that already exists makes the game throw,
            // which the implicit per-mod group would hit when the mod is enabled again.
            InputManager input = ScriptableSingleton<InputManager>.Instance;
            key_group = new KeyGroup(identifier);
            key_group.keyGroupName = getName();
            input.registerKeyGroup(key_group);

            key_mapping = new KeyMapping(window_hotkey_id, KeyCode.H, KeyCode.None);
            key_mapping.keyName = "Open Day Night Cycle";
            key_mapping.keyDescription = "Open the window with the day and night cycle's settings.";
            key_mapping.keyGroupIdentifier = identifier;
            input.registerKeyMapping(key_mapping);

            settings = cycle_settings.load();
            controller_object = new GameObject("day_night_cycle");
            sky_controller controller = controller_object.AddComponent<sky_controller>();
            controller.settings = settings;
            cycle_window window = controller_object.AddComponent<cycle_window>();
            window.controller = controller;
            window.settings = settings;
        }

        public override void onDisabled()
        {
            if (controller_object != null)
            {
                // Immediate, so that the lights are back to normal before anything else happens.
                Object.DestroyImmediate(controller_object);
            }
            InputManager input = ScriptableSingleton<InputManager>.Instance;
            input.unregisterKeyMapping(key_mapping);
            input.unregisterKeyGroup(key_group);
            settings.save();
        }
    }
}
