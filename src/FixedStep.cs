using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EFT.Animations;
using HarmonyLib;
using UnityEngine;

namespace StableGame
{
    internal static class FixedStep
    {
        private static readonly FieldInfo Direction = typeof(ProceduralWeaponAnimation).GetField("_aimSwayDirection", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Blender = typeof(ProceduralWeaponAnimation).GetField("_aimSwayBlender", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Strategy = typeof(ProceduralWeaponAnimation).GetField("_strategy", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Func<ProceduralWeaponAnimation, Vector3> ReadDirection = FieldReader.Create<ProceduralWeaponAnimation, Vector3>(Direction);
        private static readonly Func<ProceduralWeaponAnimation, EFT.Player.ValueBlender> ReadBlender = FieldReader.Create<ProceduralWeaponAnimation, EFT.Player.ValueBlender>(Blender);
        private static readonly Func<ProceduralWeaponAnimation, GInterface38> ReadStrategy = FieldReader.Create<ProceduralWeaponAnimation, GInterface38>(Strategy);
        private static ProceduralWeaponAnimation animation;
        private static double cursor;
        private static int batchSteps;
        private static bool sampled;
        private static Vector2 rotationPerStep;

        internal static void Validate()
        {
            if (Direction == null || Blender == null || Strategy == null)
                throw new MissingFieldException("StableGame fixed-step integration: required PWA fields changed.");
        }

        internal static bool Supports(ProceduralWeaponAnimation pwa)
        {
            object strategy = ReadStrategy(pwa);
            if (strategy == null) return false;
            Type type = strategy.GetType();
            return type == typeof(GClass909) || type == typeof(GClass911) || type == typeof(GClass912);
        }

        internal static string StrategyName(ProceduralWeaponAnimation pwa)
        {
            object strategy = ReadStrategy(pwa);
            return strategy == null ? "none" : strategy.GetType().FullName;
        }

        internal static void Begin(ProceduralWeaponAnimation pwa, float seconds, int steps)
        {
            animation = Plugin.IsLocal(pwa) && steps > 0 && TimingMath.FinitePositive(seconds) ? pwa : null;
            cursor = (double)Time.fixedTime - seconds;
            batchSteps = steps;
            sampled = false;
        }

        internal static void End(ProceduralWeaponAnimation pwa)
        {
            if (ReferenceEquals(animation, pwa)) animation = null;
        }

        public static Vector2 RotationSample(MotionEffector effector)
        {
            if (animation == null || !Plugin.MotionTiming.Value || !Plugin.IsLocal(animation) || !ReferenceEquals(animation.MotionReact, effector))
                return effector.method_0();
            if (!sampled)
            {
                rotationPerStep = effector.method_0() / batchSteps;
                sampled = true;
            }
            return rotationPerStep;
        }

        public static void SpringStep(BetterSpring spring, float seconds, ProceduralWeaponAnimation pwa)
        {
            if (ReferenceEquals(animation, pwa))
            {
                double from = cursor;
                cursor += seconds;
                if (Plugin.IsLocal(pwa) && Plugin.AimSway.Value && pwa.IsAiming && pwa.AimSwayStrength > 0)
                {
                    var blender = ReadBlender(pwa);
                    var direction = ReadDirection(pwa);
                    float area = TimingMath.BlenderArea(from, cursor, blender.StartTime, blender.StartValue, blender.Speed, blender.Target);
                    if (area > 0) Diagnostics.Hit(Metric.SwayFixedImpulse);
                    spring.ApplyVelocity(direction * (Tuning.Reference(Fix.AimSway) * area));
                }
            }
            spring.Process(seconds);
        }
    }

    [HarmonyPatch]
    internal static class FixedBatchPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return typeof(GClass909).GetMethod("ProcessEffectors");
            yield return typeof(GClass911).GetMethod("ProcessEffectors");
            yield return typeof(GClass912).GetMethod("ProcessEffectors");
        }

        private static void Prefix(ProceduralWeaponAnimation __0, float __1, int __2) { FixedStep.Begin(__0, __1, __2); }
        private static void Postfix(ProceduralWeaponAnimation __0) { FixedStep.End(__0); }
        private static Exception Finalizer(ProceduralWeaponAnimation __0, Exception __exception)
        {
            FixedStep.End(__0);
            return __exception;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var output = new List<CodeInstruction>();
            int count = 0;
            foreach (var instruction in instructions)
            {
                if (PatchTools.Call(instruction, typeof(BetterSpring), "Process", 1))
                {
                    var first = new CodeInstruction(OpCodes.Ldarg_1);
                    first.labels.AddRange(instruction.labels);
                    first.blocks.AddRange(instruction.blocks);
                    output.Add(first);
                    output.Add(new CodeInstruction(OpCodes.Call, typeof(FixedStep).GetMethod(nameof(FixedStep.SpringStep))));
                    count++;
                }
                else output.Add(instruction);
            }
            PatchTools.Require(count, 1, "Fixed strategy SwaySpring.Process");
            return output;
        }
    }

    [HarmonyPatch(typeof(ProceduralWeaponAnimation), nameof(ProceduralWeaponAnimation.ProcessEffectors))]
    internal static class VisualTimingPatch
    {
        private static ProceduralWeaponAnimation active;

        private static void Prefix(ProceduralWeaponAnimation __instance, float __0, int __1)
        {
            Diagnostics.Visual(__instance, __0, __1);
        }

        private static void Postfix(ProceduralWeaponAnimation __instance) { Diagnostics.VisualAfter(__instance); }

        internal static float Elapsed(object owner, float fallback)
        {
            return ReferenceEquals(owner, active) && active != null ? Time.deltaTime : fallback;
        }

        internal static float Response(object owner, float alpha, float step)
        {
            if (ReferenceEquals(owner, active) && active != null && TimingMath.FinitePositive(step))
                return TimingMath.MixedRateBlend(alpha / step, Time.deltaTime, Tuning.Reference(Fix.TemporalBlend), Time.fixedDeltaTime);
            return TimingMath.RateBlend(alpha, step, step, Tuning.Reference(Fix.TemporalBlend));
        }

        public static void Transform(GInterface38 strategy, ProceduralWeaponAnimation pwa, float seconds)
        {
            if (!Plugin.IsLocal(pwa) || !Plugin.VisualTiming.Value) { strategy.ApplyTransformations(pwa, seconds); return; }
            Diagnostics.Hit(Metric.VisualTransform);
            var previous = active;
            try
            {
                active = pwa;
                strategy.ApplyTransformations(pwa, Time.deltaTime * TimingMath.ReferenceVisualScale(Tuning.Reference(Fix.VisualTiming), Time.fixedDeltaTime));
            }
            finally { active = previous; }
        }

        public static void CameraTransform(GInterface38 strategy, ProceduralWeaponAnimation pwa, float seconds)
        {
            if (!Plugin.IsLocal(pwa) || !Plugin.VisualTiming.Value) { strategy.ApplyCameraTransformations(pwa, seconds); return; }
            Diagnostics.Hit(Metric.VisualCamera);
            var previous = active;
            try
            {
                active = pwa;
                strategy.ApplyCameraTransformations(pwa, Time.deltaTime * TimingMath.ReferenceVisualScale(Tuning.Reference(Fix.VisualTiming), Time.fixedDeltaTime));
            }
            finally { active = previous; }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var output = new List<CodeInstruction>();
            int count = 0;
            foreach (var instruction in instructions)
            {
                string helper = null;
                if (PatchTools.Call(instruction, typeof(GInterface38), "ApplyTransformations", 2)) helper = nameof(Transform);
                if (PatchTools.Call(instruction, typeof(GInterface38), "ApplyCameraTransformations", 2)) helper = nameof(CameraTransform);
                if (helper != null)
                {
                    var replacement = new CodeInstruction(instruction);
                    replacement.opcode = OpCodes.Call;
                    replacement.operand = typeof(VisualTimingPatch).GetMethod(helper);
                    output.Add(replacement);
                    count++;
                }
                else output.Add(instruction);
            }
            PatchTools.Require(count, 2, "PWA visual callbacks");
            return output;
        }
    }
}
