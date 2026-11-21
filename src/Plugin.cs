using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Animations;
using HarmonyLib;
using UnityEngine;

namespace StableGame
{
    [BepInPlugin(Id, "Zenice-TPSNormalizer", "0.3.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.stablegame.fpsnormalizer";
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> ReferenceFps;
        internal static ConfigEntry<bool> Rotation, AimSway, Recoil, MotionTiming;
        internal static ConfigEntry<bool> VisualTiming, TemporalBlend, FovTiming, AlternateInput;
        internal static ConfigEntry<bool> LeanTiming, RotationWindow;
        internal static ConfigEntry<bool> DiagnosticLogging;
        private Harmony harmony;

        private void Awake()
        {
            BindSettings();
            Diagnostics.Initialize(message => Logger.LogInfo(message));
            harmony = new Harmony(Id);
            try
            {
                FixedStep.Validate();
                harmony.PatchAll(typeof(Plugin).Assembly);
                Logger.LogInfo("TPS Normalizer 0.3.1: patches installed (SPT 4.0.13 / EFT 0.16.9). Reference FPS=" + ReferenceFps.Value + ". Local player only. Diagnostic logging=" + DiagnosticLogging.Value);
            }
            catch (Exception ex)
            {
                // Do not leave a partly installed set after a changed method shape.
                harmony.UnpatchSelf();
                Logger.LogError("TPS Normalizer: incompatible method shape or patch conflict; all TPS Normalizer patches removed. " + ex);
            }
        }

        internal void BindSettings()
        {
            bool save = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try { BindSettingsCore(); }
            finally { Config.SaveOnConfigSet = save; }
            if (save) Config.Save();
        }

        private void BindSettingsCore()
        {
            ConfigMenu.BindLanguage(Config);
            Enabled = Config.Bind("01 General", "Enabled", true,
                ConfigMenu.Description("01 General", "01 常规", "Enabled", "启用插件", "Master switch; applies immediately.", "修正总开关，即时生效。", 200, ConfigMenu.DrawEnabled));
            ReferenceFps = Config.Bind("01 General", "Reference FPS", 120,
                ConfigMenu.Description("01 General", "01 常规", "Global reference FPS", "全局基准帧率", "Reference response; does not cap rendering FPS.", "控制基准响应，不限制画面帧率。", 100, ConfigMenu.DrawGlobal, new AcceptableValueRange<int>(30, 360)));
            ConfigEntry<bool>[] switches = new ConfigEntry<bool>[10];
            for (int i = 0; i < switches.Length; i++)
                switches[i] = Config.Bind("02 Fixes", ConfigMenu.NamesEn[i], true,
                    ConfigMenu.Description("02 Fixes", "02 修复", ConfigMenu.NamesEn[i], ConfigMenu.NamesZh[i], ConfigMenu.HelpEn[i], ConfigMenu.HelpZh[i], 100 - i, ConfigMenu.FixDrawer(i)));
            Rotation = switches[0]; AimSway = switches[1]; Recoil = switches[2]; MotionTiming = switches[3]; VisualTiming = switches[4];
            TemporalBlend = switches[5]; FovTiming = switches[6]; AlternateInput = switches[7]; LeanTiming = switches[8]; RotationWindow = switches[9];
            DiagnosticLogging = Config.Bind("03 Diagnostics", "Diagnostic logging", false,
                ConfigMenu.Description("03 Diagnostics", "03 诊断", "Diagnostic logging", "诊断日志", "Passive diagnostic logging every 10 seconds. Off by default.", "每10秒记录被动诊断信息，默认关闭。", 0, ConfigMenu.DrawDiagnostic));
            Tuning.Bind(Config, switches);
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
        }

        private void Update() { ConfigMenu.Tick(); Diagnostics.Tick(); }

        // Observation must still identify the player when the gameplay master
        // switch is off, otherwise an A/B log would falsely report no player.
        internal static Player CurrentPlayer
        {
            get
            {
                if (!Singleton<GameWorld>.Instantiated) return null;
                Player player = Singleton<GameWorld>.Instance.MainPlayer;
                return player != null && player.IsYourPlayer ? player : null;
            }
        }

        internal static Player LocalPlayer
        {
            get
            {
                return Enabled != null && Enabled.Value ? CurrentPlayer : null;
            }
        }

        internal static bool IsLocal(ProceduralWeaponAnimation animation)
        {
            Player player = LocalPlayer;
            return player != null && ReferenceEquals(player.ProceduralWeaponAnimation, animation);
        }

        public static float RotationLimit(float limit, MovementState state)
        {
            Player player = LocalPlayer;
            if (player == null || !Rotation.Value || !ReferenceEquals(player.MovementContext, state.MovementContext)) return limit;
            return limit * TimingMath.FrameScale(Time.deltaTime, Tuning.Reference(Fix.Rotation));
        }

        public static void ApplyAimVelocity(BetterSpring spring, Vector3 velocity, ProceduralWeaponAnimation animation)
        {
            if (IsLocal(animation) && AimSway.Value && FixedStep.Supports(animation))
            {
                Diagnostics.Hit(Metric.SwayRenderRemoved);
                return; // Input now belongs to the physics step, not the render pass.
            }
            spring.ApplyVelocity(velocity);
        }

        public static Quaternion CameraBlend(Quaternion from, Quaternion to, float alpha, ProceduralWeaponAnimation animation, float step)
        {
            if (IsLocal(animation) && Recoil.Value)
            {
                Diagnostics.Hit(Metric.CameraRecoil);
                return Quaternion.Slerp(from, to, TimingMath.Blend(alpha, VisualTimingPatch.Elapsed(animation, step), Tuning.Reference(Fix.Recoil)));
            }
            return Quaternion.Lerp(from, to, alpha);
        }

        public static Vector3 TrackingSmooth(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime, float step, MotionEffector effector)
        {
            Player player = LocalPlayer;
            if (player != null && MotionTiming.Value && player.ProceduralWeaponAnimation != null && ReferenceEquals(player.ProceduralWeaponAnimation.MotionReact, effector))
            {
                Diagnostics.Hit(Metric.TrackingSmooth);
                return Vector3.SmoothDamp(current, target, ref velocity, smoothTime * (120f / Tuning.Reference(Fix.MotionTiming)), float.PositiveInfinity, step);
            }
            return Vector3.SmoothDamp(current, target, ref velocity, smoothTime);
        }
    }

    internal static class PatchTools
    {
        internal static bool Call(CodeInstruction instruction, Type type, string name, int parameters)
        {
            var method = instruction.operand as MethodInfo;
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && method != null
                && method.DeclaringType == type && method.Name == name && method.GetParameters().Length == parameters;
        }

        internal static MethodInfo Helper(string name) { return typeof(Plugin).GetMethod(name, BindingFlags.Public | BindingFlags.Static); }

        // Keep branch labels and exception boundaries at the first inserted instruction.
        internal static void ReplaceCall(List<CodeInstruction> output, CodeInstruction original, string helper, params OpCode[] loads)
        {
            var first = new CodeInstruction(loads[0]);
            first.labels.AddRange(original.labels);
            first.blocks.AddRange(original.blocks);
            output.Add(first);
            for (int i = 1; i < loads.Length; i++) output.Add(new CodeInstruction(loads[i]));
            output.Add(new CodeInstruction(OpCodes.Call, Helper(helper)));
        }

        internal static void Require(int actual, int expected, string name)
        {
            if (actual != expected) throw new InvalidOperationException(name + ": expected " + expected + " IL sites, found " + actual);
        }
    }

    [HarmonyPatch(typeof(MovementState), nameof(MovementState.ClampRotation))]
    internal static class RotationPatch
    {
        private static void Postfix(MovementState __instance, Vector3 __result)
        {
            Diagnostics.Rotation(__instance, __result);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var output = new List<CodeInstruction>();
            int count = 0;
            foreach (var instruction in instructions)
            {
                output.Add(instruction);
                var field = instruction.operand as FieldInfo;
                if (instruction.opcode == OpCodes.Ldfld && field != null && field.Name == "RotationSpeedClamp" && field.DeclaringType == typeof(BaseMovementState))
                {
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    output.Add(new CodeInstruction(OpCodes.Call, PatchTools.Helper(nameof(Plugin.RotationLimit))));
                    count++;
                }
            }
            PatchTools.Require(count, 3, "MovementState.ClampRotation");
            return output;
        }
    }

    [HarmonyPatch]
    internal static class AimSwayPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return typeof(ProceduralWeaponAnimation).GetMethod(nameof(ProceduralWeaponAnimation.LerpCamera));
            yield return typeof(ProceduralWeaponAnimation).GetMethod(nameof(ProceduralWeaponAnimation.MountedWeaponCamera));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var output = new List<CodeInstruction>();
            int count = 0;
            foreach (var instruction in instructions)
            {
                if (PatchTools.Call(instruction, typeof(BetterSpring), "ApplyVelocity", 1))
                {
                    PatchTools.ReplaceCall(output, instruction, nameof(Plugin.ApplyAimVelocity), OpCodes.Ldarg_0);
                    count++;
                }
                else output.Add(instruction);
            }
            PatchTools.Require(count, 1, "ProceduralWeaponAnimation camera aim sway");
            return output;
        }
    }

    [HarmonyPatch(typeof(ProceduralWeaponAnimation), "method_19")]
    internal static class RecoilPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var output = new List<CodeInstruction>();
            int count = 0;
            foreach (var instruction in instructions)
            {
                if (PatchTools.Call(instruction, typeof(Quaternion), "Lerp", 3))
                {
                    PatchTools.ReplaceCall(output, instruction, nameof(Plugin.CameraBlend), OpCodes.Ldarg_0, OpCodes.Ldarg_1);
                    count++;
                }
                else output.Add(instruction);
            }
            PatchTools.Require(count, 4, "ProceduralWeaponAnimation.method_19");
            return output;
        }
    }

    [HarmonyPatch(typeof(MotionEffector), nameof(MotionEffector.FixedTracking))]
    internal static class MotionTimingPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var output = new List<CodeInstruction>();
            int count = 0, samples = 0;
            foreach (var instruction in instructions)
            {
                if (PatchTools.Call(instruction, typeof(Vector3), "SmoothDamp", 4))
                {
                    PatchTools.ReplaceCall(output, instruction, nameof(Plugin.TrackingSmooth), OpCodes.Ldarg_1, OpCodes.Ldarg_0);
                    count++;
                }
                else if (PatchTools.Call(instruction, typeof(MotionEffector), "method_0", 0))
                {
                    var replacement = new CodeInstruction(instruction);
                    replacement.opcode = OpCodes.Call;
                    replacement.operand = typeof(FixedStep).GetMethod(nameof(FixedStep.RotationSample));
                    output.Add(replacement);
                    samples++;
                }
                else output.Add(instruction);
            }
            PatchTools.Require(count, 2, "MotionEffector.FixedTracking");
            PatchTools.Require(samples, 1, "MotionEffector rotation sampling");
            return output;
        }
    }
}
