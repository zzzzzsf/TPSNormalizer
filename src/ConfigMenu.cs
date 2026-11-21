using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace StableGame
{
    // ConfigurationManager's documented tag contract; no runtime dependency on its DLL.
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? Browsable, HideDefaultButton, HideSettingName;
        public int? Order;
        public string Category, DispName, Description;
        public Action<ConfigEntryBase> CustomDrawer;
    }

    internal static class ConfigMenu
    {
        internal static ConfigEntry<string> Language;
        internal const float GroupSpacing = 40f;
        private static readonly List<Binding> bindings = new List<Binding>();
        private static readonly Dictionary<ConfigEntry<int>, NumberDraft> drafts = new Dictionary<ConfigEntry<int>, NumberDraft>();
        private static bool refresh;
        private static GUIStyle wrapped;
        private static readonly GUILayoutOption[] NoOptions = new GUILayoutOption[0];
        private static readonly GUILayoutOption[] FieldWidth = { GUILayout.Width(80f) };
        private static readonly GUILayoutOption[] Expand = { GUILayout.ExpandWidth(true) };
        internal static bool Chinese { get { return Language == null || Language.Value == "中文"; } }
        internal static string Text(string english, string chinese) { return Chinese ? chinese : english; }

        private sealed class Binding
        {
            internal ConfigurationManagerAttributes Attributes;
            internal string CategoryEn, CategoryZh, NameEn, NameZh, DescriptionEn, DescriptionZh;
            internal void Translate()
            {
                Attributes.Category = Text(CategoryEn, CategoryZh);
                Attributes.DispName = Text(NameEn, NameZh);
                Attributes.Description = Text(DescriptionEn, DescriptionZh);
            }
        }

        internal static ConfigDescription Description(string categoryEn, string categoryZh, string nameEn, string nameZh,
            string descriptionEn, string descriptionZh, int order, Action<ConfigEntryBase> drawer, AcceptableValueBase range = null)
        {
            var binding = new Binding
            {
                CategoryEn = categoryEn, CategoryZh = categoryZh, NameEn = nameEn, NameZh = nameZh,
                DescriptionEn = descriptionEn, DescriptionZh = descriptionZh,
                Attributes = new ConfigurationManagerAttributes { Order = order, HideDefaultButton = true, HideSettingName = true, CustomDrawer = drawer }
            };
            bindings.Add(binding); binding.Translate();
            return new ConfigDescription(descriptionEn, range, binding.Attributes);
        }

        internal static void BindLanguage(ConfigFile config)
        {
            bindings.Clear(); drafts.Clear(); wrapped = null;
            Language = config.Bind("01 General", "Language", "中文",
                Description("01 General", "01 常规", "Language", "语言", "Switch the TPS Normalizer menu language immediately.", "即时切换 TPS Normalizer 菜单的语言。", 300,
                DrawLanguage, new AcceptableValueList<string>("中文", "Eng")));
            Language.SettingChanged += (sender, args) => Translate();
            Translate();
        }

        internal static void Translate()
        {
            foreach (var binding in bindings) binding.Translate();
            refresh = true;
        }

        // Rebuild between OnGUI passes, never mutate the manager's list during drawing.
        internal static void Tick()
        {
            if (!refresh) return;
            refresh = false;
            BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.bepis.bepinex.configurationmanager", out info) || info.Instance == null) return;
            MethodInfo rebuild = info.Instance.GetType().GetMethod("BuildSettingList", BindingFlags.Instance | BindingFlags.Public);
            if (rebuild != null) rebuild.Invoke(info.Instance, null);
        }

        private static void Label(string text)
        {
            if (wrapped == null) wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(text, wrapped, Expand);
        }

        private static void DrawLanguage(ConfigEntryBase entry)
        {
            GUILayout.BeginVertical(Expand);
            Label(Text("Language", "语言"));
            GUILayout.BeginHorizontal(NoOptions);
            if (GUILayout.Toggle(Language.Value == "Eng", "Eng", NoOptions)) Language.Value = "Eng";
            if (GUILayout.Toggle(Language.Value == "中文", "中文", NoOptions)) Language.Value = "中文";
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        internal static void DrawEnabled(ConfigEntryBase entry)
        {
            GUILayout.BeginVertical(Expand);
            Plugin.Enabled.Value = GUILayout.Toggle(Plugin.Enabled.Value, Text("Enable TPS Normalizer", "启用 TPS Normalizer"), NoOptions);
            if (GUILayout.Button(Text("Reset", "重置"), NoOptions)) Plugin.Enabled.Value = true;
            GUILayout.EndVertical();
        }

        internal static void DrawGlobal(ConfigEntryBase entry)
        {
            GUILayout.BeginVertical(Expand);
            Label(Text("Global reference FPS", "全局基准帧率"));
            Number(Plugin.ReferenceFps);
            Label(Text("Controls covered responses; does not limit actual rendering FPS.", "控制已覆盖逻辑的响应，不限制实际画面帧率。"));
            GUILayout.EndVertical();
        }

        internal static void DrawDiagnostic(ConfigEntryBase entry)
        {
            GUILayout.BeginVertical(Expand);
            Plugin.DiagnosticLogging.Value = GUILayout.Toggle(Plugin.DiagnosticLogging.Value, Text("Diagnostic logging", "诊断日志"), NoOptions);
            Label(Text("Logs actual FPS and correction counters every 10 seconds. Leave off for normal play.", "每10秒记录实际帧率和修正计数。平时建议关闭。"));
            if (GUILayout.Button(Text("Reset", "重置"), NoOptions)) Plugin.DiagnosticLogging.Value = false;
            GUILayout.EndVertical();
        }

        internal static readonly string[] NamesEn = {
            "Rotation speed limit", "Aim sway impulse", "Camera recoil blend", "Motion tracking timestep", "Visual update timestep",
            "ADS and look smoothing", "FOV transition", "Alternate mouse axis rate", "Lean inertia reference ticks", "Rotation sound averaging"
        };
        internal static readonly string[] NamesZh = {
            "转向速度上限", "瞄准摆动输入", "镜头后坐力平滑", "运动追踪时间步", "视觉更新时间步",
            "开镜与视线平滑", "视野过渡", "替代鼠标轴速率", "侧倾惯性参考步", "转向声音平均窗口"
        };
        internal static readonly string[] HelpEn = {
            "Converts the local player's per-frame rotation cap to a time-based cap. Ordinary mouse displacement stays unchanged.",
            "Integrates aiming sway in fixed steps, including mounted weapons. Higher reference FPS increases the accumulated sway input.",
            "Normalizes camera recoil interpolation. Changes camera response, not weapon stats or ballistics.",
            "Keeps the true fixed timestep and distributes batched rotation samples. Reference FPS adjusts tracking smoothing time: original smoothTime × 120 / reference FPS.",
            "Samples the reference FPS mixed visual clock using real elapsed time. Best used together with ADS and look smoothing.",
            "Normalizes ADS, alignment, free-look return, pitch and posture smoothing. Pose blend weights remain native.",
            "Samples the original FOV transition at the chosen reference FPS. Does not cover every animation zoom path.",
            "Only affects directional MousePosition turn/look input. Default raw mouse input usually shows no change.",
            "Runs normal lean inertia at the reference cadence. Higher rendering FPS still presents discrete reference ticks.",
            "Averages turn speed over the latest 10 / reference FPS seconds for turn and bush sound response."
        };
        internal static readonly string[] HelpZh = {
            "将本地玩家每帧转向角度上限换算为时间上限，不缩放普通鼠标位移。",
            "在固定步中积分瞄准摆动，包含普通与架枪状态。基准帧率越高，累计摆动输入越大。",
            "修正镜头后坐力的逐帧插值，只改变镜头响应，不修改枪械属性或弹道。",
            "保留真实固定时间步并分配同批旋转采样。基准帧率控制追踪滤波响应：原平滑时间 × 120 / 基准帧率。",
            "按真实时间重现基准帧率的视觉混合时钟。建议与“开镜与视线平滑”同时开启。",
            "补偿开镜、镜头对齐、自由观察回正、俯仰和部分姿势平滑。姿态混合权重保留原样。",
            "按所选基准帧率采样原始视野过渡曲线，不覆盖所有动画变焦路径。",
            "仅补偿 MousePosition 方向型转向与观察轴。默认原始鼠标输入通常没有可见变化。",
            "按基准频率更新普通侧倾惯性，高画面帧率下仍会呈现参考步的阶梯变化。",
            "取最近 10 / 基准帧率 秒的转向速度平均值，影响转身声和灌木声响应。"
        };

        internal static Action<ConfigEntryBase> FixDrawer(int index) { return entry => DrawFix(index); }

        internal static void DrawFix(int index)
        {
            FixProfile profile = Tuning.Profiles[index];
            GUILayout.BeginVertical(Expand);
            Label(Text(NamesEn[index], NamesZh[index]));
            Label(Text(HelpEn[index], HelpZh[index]));
            profile.Enabled.Value = GUILayout.Toggle(profile.Enabled.Value, Text("Enable fix", "开启修复"), NoOptions);
            profile.Separate.Value = GUILayout.Toggle(profile.Separate.Value, Text("Use separate reference FPS", "独立基准帧率"), NoOptions);
            if (profile.Separate.Value) Number(profile.Fps);
            else
            {
                NumberDraft hiddenDraft;
                if (drafts.TryGetValue(profile.Fps, out hiddenDraft)) { hiddenDraft.Cancel(); hiddenDraft.Focused = false; }
                Label(Text("Using global reference FPS: ", "跟随全局基准帧率：") + Plugin.ReferenceFps.Value);
            }
            if (GUILayout.Button(Text("Reset this fix", "重置本项"), NoOptions))
            {
                profile.Enabled.Value = true; profile.Separate.Value = false; profile.Fps.Value = 120;
                NumberDraft draft;
                if (drafts.TryGetValue(profile.Fps, out draft)) draft.Cancel();
            }
            GUILayout.Space(GroupSpacing); // About two ordinary menu lines between fix groups.
            GUILayout.EndVertical();
        }

        private static void Number(ConfigEntry<int> entry)
        {
            NumberDraft draft;
            if (!drafts.TryGetValue(entry, out draft)) { draft = new NumberDraft(); drafts.Add(entry, draft); }
            string control = "TPS-" + entry.Definition.Key;
            GUILayout.BeginHorizontal(NoOptions);
            int slider = (int)Math.Round(GUILayout.HorizontalSlider(entry.Value, 30, 360, Expand));
            if (slider != entry.Value) { entry.Value = slider; draft.Cancel(); }
            bool wasFocused = GUI.GetNameOfFocusedControl() == control;
            Event evt = Event.current;
            bool keyPressed = wasFocused && evt.type == EventType.KeyDown;
            KeyCode key = evt.keyCode;
            if (wasFocused) draft.Begin(entry.Value);
            GUI.SetNextControlName(control);
            string input = GUILayout.TextField(draft.Editing ? draft.Text : entry.Value.ToString(), FieldWidth);
            if (input != (draft.Editing ? draft.Text : entry.Value.ToString())) { draft.Begin(entry.Value); draft.Text = input; }
            bool focused = GUI.GetNameOfFocusedControl() == control;
            if (focused) draft.Begin(entry.Value);
            bool commit = GUILayout.Button(Text("Apply", "应用"), NoOptions);
            bool reset = GUILayout.Button(Text("Reset", "重置"), NoOptions);
            // TextField may consume Return/Escape; remember the event before drawing it.
            if (keyPressed)
            {
                if (key == KeyCode.Return || key == KeyCode.KeypadEnter) { commit = true; evt.Use(); GUI.FocusControl(null); }
                else if (key == KeyCode.Escape) { draft.Cancel(); evt.Use(); GUI.FocusControl(null); }
            }
            if (reset) { entry.Value = 120; draft.Cancel(); GUI.FocusControl(null); }
            else if (draft.Editing && (commit || (draft.Focused && !focused)))
            {
                int value;
                if (draft.Commit(out value)) entry.Value = value;
            }
            draft.Focused = focused;
            GUILayout.EndHorizontal();
            Label(draft.Invalid ? Text("Invalid integer; kept the previous value.", "请输入整数，已保留之前的数值。")
                : Text("30–360. Finish typing, then press Enter / Apply or leave the field. Esc cancels.", "范围30–360。输入完成后按回车/应用或离开输入框；Esc取消。"));
        }
    }
}
