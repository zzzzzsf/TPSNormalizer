using System;
using BepInEx.Configuration;

namespace StableGame
{
    internal enum Fix { Rotation, AimSway, Recoil, MotionTiming, VisualTiming, TemporalBlend, FovTiming, AlternateInput, LeanTiming, RotationWindow }

    internal sealed class FixProfile
    {
        internal ConfigEntry<bool> Enabled, Separate;
        internal ConfigEntry<int> Fps;
    }

    internal static class Tuning
    {
        internal static readonly FixProfile[] Profiles = new FixProfile[10];
        internal static int Revision;

        internal static int Reference(Fix fix)
        {
            FixProfile profile = Profiles[(int)fix];
            return profile != null && profile.Separate.Value ? profile.Fps.Value : Plugin.ReferenceFps.Value;
        }

        internal static void Bind(ConfigFile config, ConfigEntry<bool>[] switches)
        {
            for (int i = 0; i < Profiles.Length; i++)
            {
                string name = ((Fix)i).ToString();
                Profiles[i] = new FixProfile
                {
                    Enabled = switches[i],
                    Separate = config.Bind("02 Fixes", name + " separate reference", false,
                        new ConfigDescription("Use a separate reference FPS for this fix.", null, new ConfigurationManagerAttributes { Browsable = false })),
                    Fps = config.Bind("02 Fixes", name + " reference FPS", 120,
                        new ConfigDescription("Separate reference FPS (30-360).", new AcceptableValueRange<int>(30, 360), new ConfigurationManagerAttributes { Browsable = false }))
                };
                Watch(Profiles[i].Enabled); Watch(Profiles[i].Separate); Watch(Profiles[i].Fps);
            }
            Watch(Plugin.Enabled); Watch(Plugin.ReferenceFps); Watch(Plugin.DiagnosticLogging);
            Revision++;
        }

        private static void Watch<T>(ConfigEntry<T> entry) { entry.SettingChanged += (sender, args) => Revision++; }
    }

    // Edit text first; apply numeric limits only when the user commits.
    internal sealed class NumberDraft
    {
        internal string Text;
        internal bool Editing, Invalid, Focused;
        private int original;

        internal void Begin(int value)
        {
            if (Editing) return;
            original = value; Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Editing = true; Invalid = false;
        }

        internal bool Commit(out int value)
        {
            Editing = false;
            int parsed;
            if (!int.TryParse(Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out parsed))
            { value = original; Invalid = true; Text = original.ToString(); return false; }
            value = Math.Max(30, Math.Min(360, parsed));
            Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture); Invalid = false;
            return true;
        }

        internal void Cancel() { Editing = Invalid = false; Text = original.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    }
}
