using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EFT;
using EFT.Animations;
using EFT.InputSystem;
using HarmonyLib;
using UnityEngine;

namespace StableGame
{
    internal static class Temporal
    {
        internal static bool Local(object owner)
        {
            Player player = Plugin.LocalPlayer;
            if (player == null || !Plugin.TemporalBlend.Value) return false;
            var state = owner as MovementState;
            return ReferenceEquals(owner, player) || ReferenceEquals(owner, player.ProceduralWeaponAnimation)
                || ReferenceEquals(owner, player.MovementContext) || ReferenceEquals(owner, player.PlayerBones)
                || (state != null && ReferenceEquals(state.MovementContext, player.MovementContext));
        }

        private static float Response(object owner, float alpha, float step)
        {
            if (!Local(owner)) return alpha;
            float result = VisualTimingPatch.Response(owner, alpha, step);
            Diagnostics.Blend(alpha, result);
            return result;
        }

        public static float Scalar(float a, float b, float alpha, object owner, float step)
        {
            return Mathf.Lerp(a, b, Response(owner, alpha, step));
        }

        public static float RenderScalar(float a, float b, float alpha, object owner)
        {
            return Scalar(a, b, alpha, owner, Time.deltaTime);
        }

        public static Vector2 Vector(Vector2 a, Vector2 b, float alpha, object owner, float step)
        {
            return Vector2.Lerp(a, b, Response(owner, alpha, step));
        }

        public static Quaternion Rotation(Quaternion a, Quaternion b, float alpha, object owner, float step)
        {
            if (Local(owner)) return Quaternion.Slerp(a, b, Response(owner, alpha, step));
            return Quaternion.Lerp(a, b, alpha);
        }

        public static Vector3 Position(Vector3 a, Vector3 b, float alpha, object owner, float step)
        {
            return Vector3.Lerp(a, b, Response(owner, alpha, step));
        }

        public static Vector3 RenderPosition(Vector3 a, Vector3 b, float alpha, object owner)
        {
            bool local = Local(owner);
            float result = local ? TimingMath.Blend(alpha, Time.deltaTime, Tuning.Reference(Fix.TemporalBlend)) : alpha;
            if (local) Diagnostics.Blend(alpha, result);
            return Vector3.Lerp(a, b, result);
        }

        public static Vector2 Pitch(Vector2 a, Vector2 b, float alpha, object owner, float step)
        {
            bool local = Local(owner);
            float result = local ? TimingMath.Blend(alpha, step, Tuning.Reference(Fix.TemporalBlend)) : alpha;
            if (local) Diagnostics.Blend(alpha, result);
            return Vector2.Lerp(a, b, result);
        }
    }

    [HarmonyPatch]
    internal static class TemporalPatch
    {
        internal sealed class Spec
        {
            internal Type Owner;
            internal string Method;
            internal int Scalars, Vectors, Rotations, Positions, OwnerArg, StepArg;
            internal bool Constant;
            internal Spec(Type owner, string method, int scalars, int vectors, int rotations, int ownerArg = 0, int stepArg = 1, bool constant = false, int positions = 0)
            { Owner = owner; Method = method; Scalars = scalars; Vectors = vectors; Rotations = rotations; Positions = positions; OwnerArg = ownerArg; StepArg = stepArg; Constant = constant; }
        }

        // This allowlist distinguishes accumulated response from pose weights.
        // In particular LerpCamera's animation-weight Quaternion.Lerp and
        // MountedWeaponCamera's aiming-weight Vector3.Lerp MUST remain unchanged.
        internal static readonly Spec[] Specs = new[]
        {
            new Spec(typeof(ProceduralWeaponAnimation), "UpdateAimWeight", 2, 0, 0),
            new Spec(typeof(ProceduralWeaponAnimation), "ApplyAimingAlignment", 1, 0, 0),
            new Spec(typeof(ProceduralWeaponAnimation), "ApplyComplexRotation", 0, 0, 1),
            new Spec(typeof(ProceduralWeaponAnimation), "ApplySimpleRotation", 0, 0, 1),
            new Spec(typeof(ProceduralWeaponAnimation), "LerpCamera", 1, 1, 0),
            new Spec(typeof(ProceduralWeaponAnimation), "MountedWeaponCamera", 1, 1, 0),
            new Spec(typeof(ProceduralWeaponAnimation), "StationaryCamera", 1, 1, 0),
            new Spec(typeof(Player), "Look", 2, 0, 0, 0, -1),
            new Spec(typeof(Player), "ManualUpdate", 0, 1, 0, 0, 1, true),
            new Spec(typeof(Player), "HeightInterpolation", 1, 0, 0),
            new Spec(typeof(MovementContext), "SmoothPoseLevel", 1, 0, 0),
            new Spec(typeof(MovementContext), "SmoothPitchLimitations", 0, 1, 0),
            new Spec(typeof(MovementContext), "method_14", 1, 0, 0, 0, 2),
            new Spec(typeof(MovementState), "ProcessUpperbodyRotation", 0, 0, 1),
            new Spec(typeof(MovementState), "SetTilt", 1, 0, 0, 0, -1),
            new Spec(typeof(Player), "method_23", 1, 0, 0, 0, -1),
            new Spec(typeof(GClass909), "ApplyFovAdjustments", 1, 0, 0, 1, -1),
            new Spec(typeof(GClass912), "ApplyFovAdjustments", 1, 0, 0, 1, -1),
            new Spec(typeof(MovementContext), "AlignToSurface", 0, 0, 0, positions: 1),
            new Spec(typeof(MovementContext), "OnePointAlignment", 0, 0, 0, stepArg: 2, positions: 1),
            new Spec(typeof(MovementContext), "TwoPointAlignment", 0, 0, 0, stepArg: 3, positions: 1),
            new Spec(typeof(MovementContext), "RestoreDefaultAlignment", 0, 0, 0, stepArg: -1, positions: 2)
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var spec in Specs) yield return spec.Owner.GetMethod(spec.Method);
        }

        private static OpCode Argument(int index)
        {
            switch (index)
            {
                case 0: return OpCodes.Ldarg_0;
                case 1: return OpCodes.Ldarg_1;
                case 2: return OpCodes.Ldarg_2;
                case 3: return OpCodes.Ldarg_3;
                default: throw new InvalidOperationException("Unexpected temporal argument index.");
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            Spec spec = Array.Find(Specs, s => s.Owner == __originalMethod.DeclaringType && s.Method == __originalMethod.Name);
            if (spec == null) throw new InvalidOperationException("Unreviewed temporal method.");
            var output = new List<CodeInstruction>();
            int scalars = 0, vectors = 0, rotations = 0, positions = 0;
            foreach (var instruction in instructions)
            {
                string helper = null;
                if (spec.Scalars > 0 && PatchTools.Call(instruction, typeof(Mathf), "Lerp", 3))
                { helper = spec.StepArg < 0 ? nameof(Temporal.RenderScalar) : nameof(Temporal.Scalar); scalars++; }
                if (spec.Vectors > 0 && PatchTools.Call(instruction, typeof(Vector2), "Lerp", 3))
                { helper = spec.Constant ? nameof(Temporal.Pitch) : nameof(Temporal.Vector); vectors++; }
                if (spec.Rotations > 0 && PatchTools.Call(instruction, typeof(Quaternion), "Lerp", 3))
                { helper = nameof(Temporal.Rotation); rotations++; }
                if (spec.Positions > 0 && PatchTools.Call(instruction, typeof(Vector3), "Lerp", 3))
                { helper = spec.StepArg < 0 ? nameof(Temporal.RenderPosition) : nameof(Temporal.Position); positions++; }
                if (helper == null) { output.Add(instruction); continue; }
                var first = new CodeInstruction(Argument(spec.OwnerArg));
                first.labels.AddRange(instruction.labels);
                first.blocks.AddRange(instruction.blocks);
                output.Add(first);
                if (spec.StepArg >= 0) output.Add(new CodeInstruction(Argument(spec.StepArg)));
                output.Add(new CodeInstruction(OpCodes.Call, typeof(Temporal).GetMethod(helper)));
            }
            PatchTools.Require(scalars, spec.Scalars, spec.Method + " scalar responses");
            PatchTools.Require(vectors, spec.Vectors, spec.Method + " vector responses");
            PatchTools.Require(rotations, spec.Rotations, spec.Method + " rotation responses");
            PatchTools.Require(positions, spec.Positions, spec.Method + " position responses");
            return output;
        }
    }

    [HarmonyPatch(typeof(PlayerBones), "ShiftWeaponRoot")]
    internal static class LeftStancePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var output = new List<CodeInstruction>();
            int positions = 0, rotations = 0;
            foreach (var instruction in instructions)
            {
                string helper = null;
                if (PatchTools.Call(instruction, typeof(Vector3), "Lerp", 3) && ++positions <= 2) helper = nameof(Temporal.Position);
                if (PatchTools.Call(instruction, typeof(Quaternion), "Lerp", 3) && ++rotations <= 2) helper = nameof(Temporal.Rotation);
                if (helper == null) { output.Add(instruction); continue; }
                var first = new CodeInstruction(OpCodes.Ldarg_0);
                first.labels.AddRange(instruction.labels);
                first.blocks.AddRange(instruction.blocks);
                output.Add(first);
                output.Add(new CodeInstruction(OpCodes.Ldarg_1));
                output.Add(new CodeInstruction(OpCodes.Call, typeof(Temporal).GetMethod(helper)));
            }
            PatchTools.Require(positions, 6, "PlayerBones.ShiftWeaponRoot position blend layout");
            PatchTools.Require(rotations, 6, "PlayerBones.ShiftWeaponRoot rotation blend layout");
            return output;
        }
    }

    [HarmonyPatch(typeof(GClass2409), "GetValue")]
    internal static class AlternateInputPatch
    {
        private static void Postfix(GClass2409 __instance, ref float __result)
        {
            if (Plugin.LocalPlayer == null || !Plugin.AlternateInput.Value || __result == 0) return;
            EAxis axis = __instance.GameAxis;
            if (axis != EAxis.TurnX && axis != EAxis.TurnY && axis != EAxis.LookX && axis != EAxis.LookY) return;
            // Apply AFTER command/key detection; scaling GClass2414.GetValue
            // itself would break GClass2407's exact +/-1 key-state comparisons.
            foreach (var side in __instance.KeyValuePair_0)
            {
                if (side.Value == null || (side.Key == GClass2409.EUseSide.Positive) != (__result > 0)) continue;
                foreach (var source in side.Value)
                {
                    var mouse = source as GClass2407;
                    if (mouse != null && mouse.Ginterface241_0 is GClass2414 && mouse.GetValue() > 0)
                    {
                        __result *= TimingMath.FrameScale(Time.deltaTime, Tuning.Reference(Fix.AlternateInput));
                        Diagnostics.Hit(Metric.AlternateInput);
                        return;
                    }
                }
            }
        }
    }
}
