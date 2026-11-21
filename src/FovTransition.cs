using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace StableGame
{
    [HarmonyPatch(typeof(CameraClass), "method_5")]
    internal static class FovPatch
    {
        private static void Postfix(CameraClass __instance, float x, float time, ref IEnumerator __result)
        {
            if (Plugin.LocalPlayer != null && Plugin.FovTiming.Value && ReferenceEquals(__instance, CameraClass.Instance))
            {
                Diagnostics.Hit(Metric.FovTransitions);
                __result = Transition(__instance, x, time);
            }
        }

        internal static IEnumerator Transition(CameraClass camera, float target, float duration)
        {
            if (camera.Camera == null) { camera.Coroutine_0 = null; yield break; }
            float initial = camera.Camera.fieldOfView;
            double started = Time.time;
            double curveStarted = started;
            float curveDuration = duration;
            int reference = Tuning.Reference(Fix.FovTiming);
            var sampler = new FovSampler();
            bool wasCorrected = Plugin.LocalPlayer != null && Plugin.FovTiming.Value;
            while (TimingMath.FinitePositive(duration) && Time.time - started < duration && camera.Camera != null)
            {
                bool corrected = Plugin.LocalPlayer != null && Plugin.FovTiming.Value;
                if (reference != Tuning.Reference(Fix.FovTiming) || corrected != wasCorrected)
                {
                    initial = camera.Camera.fieldOfView;
                    curveDuration = Math.Max(0f, duration - (float)(Time.time - started));
                    curveStarted = Time.time;
                    reference = Tuning.Reference(Fix.FovTiming);
                    wasCorrected = corrected;
                }
                float remaining = corrected ? sampler.Sample(Time.time - curveStarted, curveDuration, reference)
                    : Mathf.Clamp01(1f - (float)(Time.time - started) / duration);
                float value = Mathf.Lerp(target, corrected ? initial : camera.Camera.fieldOfView, remaining);
                camera.Camera.fieldOfView = value;
                camera.Fov = value; // Preserve LOD bias and FOV event notifications.
                yield return null;
            }
            if (camera.Camera != null)
            {
                camera.Camera.fieldOfView = target;
                camera.Fov = target;
            }
            camera.Coroutine_0 = null;
        }
    }
}
