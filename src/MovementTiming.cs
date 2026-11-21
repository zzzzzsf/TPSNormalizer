using System;
using EFT;
using HarmonyLib;

namespace StableGame
{
    [HarmonyPatch(typeof(MovementContext), "method_15")]
    internal static class LeanPatch
    {
        private static MovementContext context;
        private static bool executing;
        private static double carry;

        private static bool Prefix(MovementContext __instance, float deltaTime)
        {
            if (executing) return true;
            Player player = Plugin.LocalPlayer;
            if (player == null || !Plugin.LeanTiming.Value || !ReferenceEquals(player.MovementContext, __instance))
            {
                if (ReferenceEquals(context, __instance)) { context = null; carry = 0; }
                return true;
            }
            if (!ReferenceEquals(context, __instance)) { context = __instance; carry = 0; }
            int ticks = TimingMath.ReferenceTicks(ref carry, deltaTime, Tuning.Reference(Fix.LeanTiming));
            Diagnostics.Hit(Metric.LeanCalls);
            try
            {
                executing = true;
                float step = 1f / Tuning.Reference(Fix.LeanTiming);
                for (int i = 0; i < ticks; i++) { Diagnostics.Hit(Metric.LeanTicks); __instance.method_15(__instance.TiltDiff, step); }
            }
            finally { executing = false; }
            return false;
        }
    }

    internal static class RotationHistory
    {
        internal static RollingMedian Buffer;
        internal static DurationAverage History = new DurationAverage();
        internal static void Clear()
        {
            if (Buffer != null) { Buffer = null; History.Clear(); }
        }

        internal static void Capture(MovementContext context, float value, float seconds)
        {
            Player player = Plugin.LocalPlayer;
            if (player == null) { Clear(); return; }
            if (!ReferenceEquals(context, player.MovementContext)) return;
            if (!Plugin.RotationWindow.Value) { Buffer = null; return; }
            if (!ReferenceEquals(Buffer, context.AverageRotationSpeed))
            {
                Buffer = context.AverageRotationSpeed;
                History.Clear();
            }
            History.Add(value, seconds);
        }
    }

    [HarmonyPatch(typeof(MovementState), "UpdateRotationSpeed")]
    internal static class RotationCapturePatch
    {
        private static void Postfix(MovementState __instance, float deltaTime)
        {
            var context = __instance.MovementContext;
            Player player = Plugin.LocalPlayer;
            if (player == null) { RotationHistory.Clear(); return; }
            if (!ReferenceEquals(player.MovementContext, context)) return;
            var buffer = context.AverageRotationSpeed;
            if (buffer != null && buffer.Buffer != null && buffer.Counter >= 0 && buffer.Counter < buffer.Buffer.Length)
                RotationHistory.Capture(context, buffer.Buffer[buffer.Counter], deltaTime);
        }
    }

    [HarmonyPatch(typeof(RollingMedian), "get_Avarage")]
    internal static class RotationAveragePatch
    {
        private static void Postfix(RollingMedian __instance, ref float __result)
        {
            Player player = Plugin.LocalPlayer;
            if (player == null) { RotationHistory.Clear(); return; }
            if (player != null && Plugin.RotationWindow.Value && ReferenceEquals(__instance, player.MovementContext.AverageRotationSpeed)
                && ReferenceEquals(__instance, RotationHistory.Buffer) && RotationHistory.History.HasSamples)
                __result = RotationHistory.History.Get(10.0 / Tuning.Reference(Fix.RotationWindow));
        }
    }
}
