using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Animations;
using HarmonyLib;
using UnityEngine;

namespace StableGame
{
    internal enum Metric { SwayRenderRemoved, SwayFixedImpulse, CameraRecoil, TrackingSmooth, VisualTransform, VisualCamera, TemporalBlend, AlternateInput, LeanCalls, LeanTicks, RotationCalls, RotationNearLimit, FovTransitions }
    // Passive telemetry only. No input injection, frame limiter, or profile edits.
    internal static class Diagnostics
    {
        private static Action<string> write;
        private static readonly long[] counters = new long[13];
        private static readonly string[] metricNames = { "swayRenderRemoved", "swayFixedImpulse", "cameraRecoil", "trackingSmooth", "visualTransform", "visualCamera", "temporalBlend", "alternateInput", "leanCalls", "leanTicks", "rotationCalls", "rotationNearLimit", "fovTransitions" };
        private static bool wasActive;
        private static int settingsRevision = -1;
        private static Player observed;
        private static string settings;
        private static double elapsed, scaled;
        private static int frames;
        private static float minStep, maxStep;
        private static double visualSeconds;
        private static int visualCalls, fixedTicks;
        private static float blendNative, blendCorrected, maxBlendDifference;
        private static float lastLimit;
        private static readonly FieldInfo AimWeight = typeof(ProceduralWeaponAnimation).GetField("_aimingWeight", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Func<ProceduralWeaponAnimation, float> ReadAimWeight = FieldReader.Create<ProceduralWeaponAnimation, float>(AimWeight);
        private static ProceduralWeaponAnimation aimAnimation;
        private static bool aimTarget, aimPending;
        private static float aimStarted;
        private static int aimFrames;
        private static string observedWorld;

        internal static bool Active { get { return write != null && Plugin.DiagnosticLogging != null && Plugin.DiagnosticLogging.Value; } }
        internal static void Initialize(Action<string> logger) { write = logger; Reset(); observed = null; settings = null; settingsRevision = -1; aimAnimation = null; aimPending = false; wasActive = false; }

        internal static void Hit(Metric name)
        {
            if (!Active) return;
            counters[(int)name]++;
        }

        internal static void Visual(ProceduralWeaponAnimation animation, float seconds, int ticks)
        {
            if (!Active) return;
            Player player = Plugin.CurrentPlayer;
            if (player == null || !ReferenceEquals(player.ProceduralWeaponAnimation, animation)) return;
            visualCalls++;
            visualSeconds += seconds;
            fixedTicks += Math.Max(0, ticks);
        }

        internal static void VisualAfter(ProceduralWeaponAnimation animation)
        {
            if (!Active || AimWeight == null) return;
            Player player = Plugin.CurrentPlayer;
            if (player == null || !ReferenceEquals(player.ProceduralWeaponAnimation, animation)) return;
            float weight = ReadAimWeight(animation);
            if (!ReferenceEquals(aimAnimation, animation) || aimTarget != animation.IsAiming)
            {
                bool firstObservation = !ReferenceEquals(aimAnimation, animation);
                aimAnimation = animation; aimTarget = animation.IsAiming;
                aimStarted = Time.unscaledTime; aimFrames = 0;
                aimPending = !firstObservation || (aimTarget ? weight < 0.95f : weight > 0.05f);
            }
            if (!aimPending) return;
            aimFrames++;
            if ((aimTarget && weight >= 0.95f) || (!aimTarget && weight <= 0.05f))
            {
                // Starts at the first observed visual pass, not at input delivery.
                write(string.Format(CultureInfo.InvariantCulture,
                    "[TPS ADS observed] aiming={0} master={1} reference={2} timeTo95pct={3:F4} visualPasses={4} weight={5:F5}",
                    aimTarget, Plugin.Enabled.Value, Tuning.Reference(Fix.TemporalBlend), Time.unscaledTime - aimStarted, aimFrames, weight));
                aimPending = false;
            }
        }

        internal static void Blend(float native, float corrected)
        {
            if (!Active) return;
            Hit(Metric.TemporalBlend);
            blendNative = native;
            blendCorrected = corrected;
            maxBlendDifference = Math.Max(maxBlendDifference, Math.Abs(native - corrected));
        }

        internal static void Rotation(MovementState state, Vector3 result)
        {
            if (!Active) return;
            Player player = Plugin.CurrentPlayer;
            if (player == null || !ReferenceEquals(player.MovementContext, state.MovementContext)) return;
            Hit(Metric.RotationCalls);
            lastLimit = state.RotationSpeedClamp;
            float limit = Plugin.Enabled.Value && Plugin.Rotation.Value ? lastLimit * TimingMath.FrameScale(Time.deltaTime, Tuning.Reference(Fix.Rotation)) : lastLimit;
            // This is a near-limit observation, not proof of an input being clipped.
            if (limit > 0 && Math.Abs(result.magnitude - limit) <= Math.Max(0.00001f, limit * 0.00001f)) Hit(Metric.RotationNearLimit);
        }

        private static void Reset()
        {
            Array.Clear(counters, 0, counters.Length); elapsed = scaled = visualSeconds = 0;
            frames = visualCalls = fixedTicks = 0;
            minStep = float.PositiveInfinity; maxStep = 0;
            blendNative = blendCorrected = maxBlendDifference = lastLimit = 0;
        }

        private static string Settings()
        {
            string values = Plugin.Enabled.Value + "/" + Plugin.ReferenceFps.Value + "/" +
                string.Join("", new[] { Plugin.Rotation.Value, Plugin.AimSway.Value, Plugin.Recoil.Value, Plugin.MotionTiming.Value,
                    Plugin.VisualTiming.Value, Plugin.TemporalBlend.Value, Plugin.FovTiming.Value, Plugin.AlternateInput.Value,
                    Plugin.LeanTiming.Value, Plugin.RotationWindow.Value }.Select(x => x ? "1" : "0").ToArray());
            for (int i = 0; i < Tuning.Profiles.Length; i++) values += "/" + Tuning.Reference((Fix)i);
            return values;
        }

        internal static void Tick()
        {
            if (!Active)
            {
                if (wasActive) { Reset(); observed = null; settings = null; aimAnimation = null; aimPending = false; }
                wasActive = false; return;
            }
            wasActive = true;
            Player player = Plugin.CurrentPlayer;
            if (!ReferenceEquals(player, observed) || settings == null || settingsRevision != Tuning.Revision)
            {
                bool playerChanged = !ReferenceEquals(player, observed);
                Flush("context-change");
                Reset(); observed = player; settings = Settings(); settingsRevision = Tuning.Revision;
                aimAnimation = null; aimPending = false;
                observedWorld = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.GetType().Name : "none";
                write("[TPS diagnostics] context player=" + (player == null ? "none" : player.GetType().FullName) +
                    " settings=" + settings + " (master/global/fix flags/effective FPS in F12 order)");
                if (player != null && playerChanged)
                {
                    foreach (var method in new Harmony(Plugin.Id).GetPatchedMethods())
                    {
                        var info = Harmony.GetPatchInfo(method);
                        write("[TPS patch owners] " + method.DeclaringType.FullName + "." + method.Name + "=" + string.Join(",", info.Owners.ToArray()));
                    }
                }
            }
            float dt = Time.unscaledDeltaTime;
            if (!TimingMath.FinitePositive(dt)) return;
            elapsed += dt; scaled += Time.deltaTime; frames++;
            minStep = Math.Min(minStep, dt); maxStep = Math.Max(maxStep, dt);
            if (elapsed >= 10) { Flush("window"); Reset(); }
        }

        private static void Flush(string reason)
        {
            if (frames == 0 || elapsed <= 0) return;
            Player player = observed;
            var pwa = player == null ? null : player.ProceduralWeaponAnimation;
            var hitRows = new List<string>();
            for (int i = 0; i < counters.Length; i++) if (counters[i] != 0) hitRows.Add(metricNames[i] + ":" + counters[i]);
            string hits = string.Join(",", hitRows.ToArray());
            write(string.Format(CultureInfo.InvariantCulture,
                "[TPS diagnostics] {0} player={1} world={2} settings={3} actualFPS={4:F1} frameMs={5:F2}..{6:F2} scaledSeconds={7:F3} fixedStep={8:F6} visualCalls={9} sourceVisualSeconds={10:F3} fixedTicks={11} strategy={12} aiming={13} aimSwayStrength={14:F4} rotationLimit={15:F4} lastAlpha={16:F6}->{17:F6} maxAlphaDelta={18:F6} hits=[{19}]",
                reason, player == null ? "none" : player.GetType().Name, observedWorld, settings, frames / elapsed,
                minStep * 1000, maxStep * 1000, scaled, Time.fixedDeltaTime, visualCalls, visualSeconds, fixedTicks,
                pwa == null ? "none" : FixedStep.StrategyName(pwa), pwa != null && pwa.IsAiming,
                pwa == null ? 0 : pwa.AimSwayStrength, lastLimit, blendNative, blendCorrected, maxBlendDifference, hits));
        }
    }
}
