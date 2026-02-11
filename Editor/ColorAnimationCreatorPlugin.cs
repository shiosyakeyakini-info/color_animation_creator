using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using ShioShakeYakiNi.ColorAnimationCreator.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

[assembly: ExportsPlugin(typeof(ShioShakeYakiNi.ColorAnimationCreator.Editor.ColorAnimationCreatorPlugin))]

namespace ShioShakeYakiNi.ColorAnimationCreator.Editor
{
    public class ColorAnimationCreatorPlugin : Plugin<ColorAnimationCreatorPlugin>
    {
        public override string QualifiedName => "com.shiosyakeyakini.coloranimationcreator";
        public override string DisplayName => "Color Animation Creator";

        private struct ResolvedTarget
        {
            public string path;
            public int materialIndex;
            public string colorPropertyName;
            public bool isHSVG;
            // Color モード用
            public float baseH, baseS, baseV;
            // HSVG モード用
            public Vector4 baseHSVG;
        }

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("net.rs64.tex-trans-tool")
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Generate Color HSV Animations", GenerateColorAnimations);
        }

        private void GenerateColorAnimations(BuildContext ctx)
        {
            var animators = ctx.AvatarRootObject.GetComponentsInChildren<ColorHSVAnimator>(true);

            Debug.Log($"[ColorAnimationCreator] Found {animators.Length} ColorHSVAnimator component(s)");

            foreach (var animator in animators)
            {
                if (!animator.Validate())
                {
                    Debug.LogWarning(
                        $"[ColorAnimationCreator] ColorHSVAnimator on '{animator.gameObject.name}' has invalid settings. Error: {animator.GetValidationError()}",
                        animator);
                    continue;
                }

                ProcessAnimator(ctx, animator);

                UnityEngine.Object.DestroyImmediate(animator);
            }
        }

        private void ProcessAnimator(BuildContext ctx, ColorHSVAnimator animator)
        {
            // 全ターゲットを解決し、Color/HSVGに分類
            var colorTargets = new List<ResolvedTarget>();
            var hsvgTargets = new List<ResolvedTarget>();

            foreach (var target in animator.targets)
            {
                var material = target.renderer.sharedMaterials[target.materialIndex];
                string path = GetRelativePath(ctx.AvatarRootTransform, target.renderer.transform);
                bool isHSVG = target.IsHSVG;

                var rt = new ResolvedTarget
                {
                    path = path,
                    materialIndex = target.materialIndex,
                    colorPropertyName = target.colorPropertyName,
                    isHSVG = isHSVG
                };

                if (isHSVG)
                {
                    rt.baseHSVG = material.GetVector(target.colorPropertyName);
                    hsvgTargets.Add(rt);
                }
                else
                {
                    Color baseColor = material.GetColor(target.colorPropertyName);
                    Color.RGBToHSV(baseColor, out rt.baseH, out rt.baseS, out rt.baseV);
                    colorTargets.Add(rt);
                }
            }

            // レイヤーを構築
            var layers = new List<AnimatorControllerLayer>();

            // Color ターゲット用: ネストされた Blend Tree → 1レイヤー
            if (colorTargets.Count > 0)
            {
                Motion colorMotion = BuildMotionTree(ctx, animator, colorTargets);
                layers.Add(CreateLayer(ctx, $"ColorHSV_{animator.gameObject.name}", colorMotion));
            }

            // HSVG ターゲット用: 軸ごとに独立レイヤー
            if (hsvgTargets.Count > 0)
            {
                var hsvgLayerMotions = BuildHSVGLayerMotions(ctx, animator, hsvgTargets);
                foreach (var (paramName, motion) in hsvgLayerMotions)
                {
                    layers.Add(CreateLayer(ctx, $"HSVG_{paramName}", motion));
                }
            }

            // AnimatorController を作成
            var controller = CreateAnimatorControllerFromLayers(ctx, animator, layers);

            // デフォルトパラメータ値: 全軸 0.5（中央）= 変化なし
            // Color: 3-child BT で param 0.5 = 元の色を再現
            // HSVG: param 0.5 → Hue=0, Sat=1.0, Val=1.0 = 変化なし
            float defaultHue = 0.5f;
            float defaultSat = 0.5f;
            float defaultVal = 0.5f;

            // MA コンポーネントを追加
            SetupModularAvatarComponents(animator, controller, defaultHue, defaultSat, defaultVal);

            int totalTargets = colorTargets.Count + hsvgTargets.Count;
            string modeDesc = colorTargets.Count > 0 && hsvgTargets.Count > 0 ? "Mixed"
                : hsvgTargets.Count > 0 ? "HSVG" : "Color";
            Debug.Log($"[ColorAnimationCreator] Setup complete for '{animator.gameObject.name}' ({modeDesc} mode) with {totalTargets} target(s), {layers.Count} layer(s)");
        }

        /// <summary>
        /// ネストされた Blend Tree を構築する（全軸 param 0.5 = 元の色）
        /// HSVToRGB(H, S, V) = V × lerp((1,1,1), PureHue(H), S) は S, V に対して双線形。
        /// 3-child BT の区分線形補間で正確に再現できる。
        /// </summary>
        private Motion BuildMotionTree(
            BuildContext ctx, ColorHSVAnimator animator, List<ResolvedTarget> targets)
        {
            bool eSat = animator.enableSaturation;
            bool eVal = animator.enableValue;
            string name = animator.gameObject.name;

            if (eSat && eVal)
            {
                // 4つの内部 Motion（S×V の組み合わせ）
                var m_bS_bV = CreateInnerHueOrBaseClip(ctx, animator, targets, true, true, "bSbV");
                var m_fS_bV = CreateInnerHueOrBaseClip(ctx, animator, targets, false, true, "fSbV");
                var m_bS_fV = CreateInnerHueOrBaseClip(ctx, animator, targets, true, false, "bSfV");
                var m_fS_fV = CreateInnerHueOrBaseClip(ctx, animator, targets, false, false, "fSfV");

                // グレー/白/黒クリップ
                var grayBaseV = CreateMultiTargetColorClip(ctx, targets,
                    t => { float v = t.baseV; return new Color(v, v, v); },
                    $"{name}_GrayBaseV");
                var white = CreateMultiTargetColorClip(ctx, targets,
                    t => Color.white,
                    $"{name}_White");
                var black = CreateMultiTargetColorClip(ctx, targets,
                    t => Color.black,
                    $"{name}_Black");

                // 彩度 BT × 2（明度レベル別）
                var satBT_bV = CreateCentered1DBT(ctx, animator.GetEffectiveSaturationParameterName(),
                    grayBaseV, m_bS_bV, m_fS_bV, $"Sat_{animator.GetEffectiveSaturationParameterName()}_baseV");
                var satBT_fV = CreateCentered1DBT(ctx, animator.GetEffectiveSaturationParameterName(),
                    white, m_bS_fV, m_fS_fV, $"Sat_{animator.GetEffectiveSaturationParameterName()}_fullV");

                // 明度 BT
                return CreateCentered1DBT(ctx, animator.GetEffectiveValueParameterName(),
                    black, satBT_bV, satBT_fV, $"Value_{animator.GetEffectiveValueParameterName()}");
            }
            else if (eSat)
            {
                var m_bS = CreateInnerHueOrBaseClip(ctx, animator, targets, true, true, "bS");
                var m_fS = CreateInnerHueOrBaseClip(ctx, animator, targets, false, true, "fS");
                var gray = CreateMultiTargetColorClip(ctx, targets,
                    t => { float v = t.baseV; return new Color(v, v, v); },
                    $"{name}_Gray");

                return CreateCentered1DBT(ctx, animator.GetEffectiveSaturationParameterName(),
                    gray, m_bS, m_fS, $"Sat_{animator.GetEffectiveSaturationParameterName()}");
            }
            else if (eVal)
            {
                var m_bV = CreateInnerHueOrBaseClip(ctx, animator, targets, true, true, "bV");
                var m_fV = CreateInnerHueOrBaseClip(ctx, animator, targets, true, false, "fV");
                var black = CreateMultiTargetColorClip(ctx, targets,
                    t => Color.black,
                    $"{name}_Black");

                return CreateCentered1DBT(ctx, animator.GetEffectiveValueParameterName(),
                    black, m_bV, m_fV, $"Value_{animator.GetEffectiveValueParameterName()}");
            }
            else
            {
                // 色相のみ or 全軸無効
                return CreateInnerHueOrBaseClip(ctx, animator, targets, true, true, "base");
            }
        }

        /// <summary>
        /// 色相 BT（中央寄せ）またはベース色クリップを作成する
        /// satAsBase: true → t.baseS を使用（元の彩度）、false → 1.0（全彩度）
        /// valAsBase: true → t.baseV を使用（元の明度）、false → 1.0（全明度）
        /// </summary>
        private Motion CreateInnerHueOrBaseClip(
            BuildContext ctx, ColorHSVAnimator animator, List<ResolvedTarget> targets,
            bool satAsBase, bool valAsBase, string suffix)
        {
            if (animator.enableHue)
            {
                return CreateCenteredHueBlendTree(ctx, animator, targets, satAsBase, valAsBase, suffix);
            }
            else
            {
                return CreateMultiTargetColorClip(ctx, targets,
                    t =>
                    {
                        float s = satAsBase ? t.baseS : 1.0f;
                        float v = valAsBase ? t.baseV : 1.0f;
                        return Color.HSVToRGB(t.baseH, s, v, true);
                    },
                    $"{animator.gameObject.name}_Base_{suffix}");
            }
        }

        /// <summary>
        /// 色相の中央寄せ 1D Blend Tree（param 0.5 = ベース色相、オフセット -180°〜+180°）
        /// </summary>
        private BlendTree CreateCenteredHueBlendTree(
            BuildContext ctx, ColorHSVAnimator animator, List<ResolvedTarget> targets,
            bool satAsBase, bool valAsBase, string suffix)
        {
            int steps = animator.hueSteps;

            var hueTree = new BlendTree
            {
                name = $"Hue_{animator.GetEffectiveHueParameterName()}_{suffix}",
                blendType = BlendTreeType.Simple1D,
                blendParameter = animator.GetEffectiveHueParameterName(),
                useAutomaticThresholds = false
            };

            for (int i = 0; i <= steps; i++)
            {
                float threshold = (float)i / steps;
                float hueShift = threshold - 0.5f; // -0.5 to +0.5

                // ラムダ内でキャプチャされる変数をローカルにコピー
                float capturedShift = hueShift;
                bool capSatBase = satAsBase;
                bool capValBase = valAsBase;

                var clip = CreateMultiTargetColorClip(ctx, targets,
                    (t) =>
                    {
                        float s = capSatBase ? t.baseS : 1.0f;
                        float v = capValBase ? t.baseV : 1.0f;
                        float hue = ((t.baseH + capturedShift) % 1.0f + 1.0f) % 1.0f;
                        return Color.HSVToRGB(hue, s, v, true);
                    },
                    $"{animator.gameObject.name}_Hue_{i}_{suffix}");

                hueTree.AddChild(clip, threshold);
            }

            ctx.AssetSaver.SaveAsset(hueTree);
            return hueTree;
        }

        /// <summary>
        /// 3-child 中央寄せ 1D BlendTree（param 0 = min, 0.5 = 元の値, 1 = max）
        /// </summary>
        private BlendTree CreateCentered1DBT(
            BuildContext ctx, string paramName,
            Motion child0, Motion child05, Motion child1, string name)
        {
            var bt = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = paramName,
                useAutomaticThresholds = false
            };
            bt.AddChild(child0, 0f);
            bt.AddChild(child05, 0.5f);
            bt.AddChild(child1, 1f);
            ctx.AssetSaver.SaveAsset(bt);
            return bt;
        }

        /// <summary>
        /// HSVG用: 各有効軸に対して3-child 1D BlendTreeを構築する（param 0.5 = baseHSVG）
        /// </summary>
        private List<(string paramName, BlendTree motion)> BuildHSVGLayerMotions(
            BuildContext ctx, ColorHSVAnimator animator, List<ResolvedTarget> targets)
        {
            var result = new List<(string, BlendTree)>();

            if (animator.enableHue)
            {
                // Hue: param 0→(baseH-0.5), param 0.5→baseH, param 1→(baseH+0.5)
                var clipMin = CreateHSVGComponentClipAdditive(ctx, targets, "x", -0.5f,
                    $"{animator.gameObject.name}_HSVG_Hue_min", animator);
                var clipBase = CreateHSVGComponentClipAdditive(ctx, targets, "x", 0.0f,
                    $"{animator.gameObject.name}_HSVG_Hue_base", animator);
                var clipMax = CreateHSVGComponentClipAdditive(ctx, targets, "x", 0.5f,
                    $"{animator.gameObject.name}_HSVG_Hue_max", animator);

                var tree = CreateCentered1DBT(ctx, animator.GetEffectiveHueParameterName(),
                    clipMin, clipBase, clipMax, $"HSVG_Hue_{animator.GetEffectiveHueParameterName()}");

                result.Add((animator.GetEffectiveHueParameterName(), tree));
            }

            if (animator.enableSaturation)
            {
                // Saturation: param 0→0.0, param 0.5→baseS, param 1→2.0
                var clipMin = CreateHSVGComponentClipAbsolute(ctx, targets, "y", 0.0f,
                    $"{animator.gameObject.name}_HSVG_Sat_0", animator);
                var clipBase = CreateHSVGComponentClipBase(ctx, targets, "y",
                    $"{animator.gameObject.name}_HSVG_Sat_base", animator);
                var clipMax = CreateHSVGComponentClipAbsolute(ctx, targets, "y", 2.0f,
                    $"{animator.gameObject.name}_HSVG_Sat_2", animator);

                var tree = CreateCentered1DBT(ctx, animator.GetEffectiveSaturationParameterName(),
                    clipMin, clipBase, clipMax, $"HSVG_Sat_{animator.GetEffectiveSaturationParameterName()}");

                result.Add((animator.GetEffectiveSaturationParameterName(), tree));
            }

            if (animator.enableValue)
            {
                // Value: param 0→0.0, param 0.5→baseV, param 1→2.0
                var clipMin = CreateHSVGComponentClipAbsolute(ctx, targets, "z", 0.0f,
                    $"{animator.gameObject.name}_HSVG_Val_0", animator);
                var clipBase = CreateHSVGComponentClipBase(ctx, targets, "z",
                    $"{animator.gameObject.name}_HSVG_Val_base", animator);
                var clipMax = CreateHSVGComponentClipAbsolute(ctx, targets, "z", 2.0f,
                    $"{animator.gameObject.name}_HSVG_Val_2", animator);

                var tree = CreateCentered1DBT(ctx, animator.GetEffectiveValueParameterName(),
                    clipMin, clipBase, clipMax, $"HSVG_Val_{animator.GetEffectiveValueParameterName()}");

                result.Add((animator.GetEffectiveValueParameterName(), tree));
            }

            return result;
        }

        /// <summary>
        /// HSVG用: 加算型（Hue用） - 各ターゲットの baseHSVG.component + offset
        /// </summary>
        private AnimationClip CreateHSVGComponentClipAdditive(
            BuildContext ctx, List<ResolvedTarget> targets,
            string component, float offset, string clipName, ColorHSVAnimator animator)
        {
            return CreateHSVGComponentClipFunc(ctx, targets, component, clipName,
                (baseVal) => baseVal + offset, animator);
        }

        /// <summary>
        /// HSVG用: 絶対値型（Sat/Val の端点用） - 全ターゲットに同じ絶対値
        /// </summary>
        private AnimationClip CreateHSVGComponentClipAbsolute(
            BuildContext ctx, List<ResolvedTarget> targets,
            string component, float value, string clipName, ColorHSVAnimator animator)
        {
            return CreateHSVGComponentClipFunc(ctx, targets, component, clipName,
                (baseVal) => value, animator);
        }

        /// <summary>
        /// HSVG用: ベース値型（Sat/Val の中央用） - 各ターゲットの baseHSVG.component そのまま
        /// </summary>
        private AnimationClip CreateHSVGComponentClipBase(
            BuildContext ctx, List<ResolvedTarget> targets,
            string component, string clipName, ColorHSVAnimator animator)
        {
            return CreateHSVGComponentClipFunc(ctx, targets, component, clipName,
                (baseVal) => baseVal, animator);
        }

        /// <summary>
        /// HSVG用: 共通関数 - valueFunc で各ターゲットの値を計算
        /// 独立レイヤー方式:
        /// - 制御対象の軸: valueFunc で計算
        /// - 他の有効レイヤーが制御する軸: 含めない
        /// - 無効な軸: baseHSVG 値を保持
        /// - Gamma(.w): 常に1.0（WD ON環境でのリセット防止）
        /// </summary>
        private AnimationClip CreateHSVGComponentClipFunc(
            BuildContext ctx, List<ResolvedTarget> targets,
            string component, string clipName,
            Func<float, float> valueFunc,
            ColorHSVAnimator animator)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f
            };

            float duration = 1f / clip.frameRate;

            foreach (var target in targets)
            {
                string propertyPrefix = target.materialIndex == 0
                    ? $"material.{target.colorPropertyName}"
                    : $"material[{target.materialIndex}].{target.colorPropertyName}";

                // baseHSVG から該当コンポーネントを取得
                float baseValue = component switch
                {
                    "x" => target.baseHSVG.x,
                    "y" => target.baseHSVG.y,
                    "z" => target.baseHSVG.z,
                    _ => 1.0f
                };

                float finalValue = valueFunc(baseValue);

                // 対象コンポーネントの値をセット
                SetConstantCurve(clip, target.path, $"{propertyPrefix}.{component}", finalValue, duration);

                // 無効な軸は baseHSVG 値で保持（有効な軸は各レイヤーに任せる）
                if (component == "x") // Hue レイヤー
                {
                    if (!animator.enableSaturation)
                        SetConstantCurve(clip, target.path, $"{propertyPrefix}.y", target.baseHSVG.y, duration);
                    if (!animator.enableValue)
                        SetConstantCurve(clip, target.path, $"{propertyPrefix}.z", target.baseHSVG.z, duration);
                }
                else if (component == "y") // Saturation レイヤー
                {
                    if (!animator.enableHue)
                        SetConstantCurve(clip, target.path, $"{propertyPrefix}.x", target.baseHSVG.x, duration);
                    if (!animator.enableValue)
                        SetConstantCurve(clip, target.path, $"{propertyPrefix}.z", target.baseHSVG.z, duration);
                }
                else if (component == "z") // Value レイヤー
                {
                    if (!animator.enableHue)
                        SetConstantCurve(clip, target.path, $"{propertyPrefix}.x", target.baseHSVG.x, duration);
                    if (!animator.enableSaturation)
                        SetConstantCurve(clip, target.path, $"{propertyPrefix}.y", target.baseHSVG.y, duration);
                }

                // Gamma(.w)を常に1.0でセット（WD ON時に0にリセットされるのを防止）
                SetConstantCurve(clip, target.path, $"{propertyPrefix}.w", 1.0f, duration);
            }

            ctx.AssetSaver.SaveAsset(clip);
            return clip;
        }

        private static void SetConstantCurve(
            AnimationClip clip, string path, string propertyName, float value, float duration)
        {
            var binding = new EditorCurveBinding
            {
                path = path,
                type = typeof(Renderer),
                propertyName = propertyName
            };
            var curve = new AnimationCurve(
                new Keyframe(0f, value),
                new Keyframe(duration, value)
            );
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        /// <summary>
        /// 全ターゲットのカーブを含むポーズクリップを作成する
        /// </summary>
        private AnimationClip CreateMultiTargetColorClip(
            BuildContext ctx,
            List<ResolvedTarget> targets,
            Func<ResolvedTarget, Color> colorFunc,
            string clipName)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f
            };

            float duration = 1f / clip.frameRate;
            string[] channels = { ".r", ".g", ".b" };

            foreach (var target in targets)
            {
                Color color = colorFunc(target);
                float[] values = { color.r, color.g, color.b };

                // materialIndex に応じたプロパティプレフィックスを決定
                string propertyPrefix = target.materialIndex == 0
                    ? $"material.{target.colorPropertyName}"
                    : $"material[{target.materialIndex}].{target.colorPropertyName}";

                for (int ch = 0; ch < 3; ch++)
                {
                    var binding = new EditorCurveBinding
                    {
                        path = target.path,
                        type = typeof(Renderer),
                        propertyName = propertyPrefix + channels[ch]
                    };
                    var curve = new AnimationCurve(
                        new Keyframe(0f, values[ch]),
                        new Keyframe(duration, values[ch])
                    );
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
            }

            ctx.AssetSaver.SaveAsset(clip);
            return clip;
        }

        /// <summary>
        /// Motion を1つのレイヤーにまとめる
        /// </summary>
        private AnimatorControllerLayer CreateLayer(BuildContext ctx, string layerName, Motion motion)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = layerName,
                hideFlags = HideFlags.HideInHierarchy
            };

            var state = stateMachine.AddState("ColorControl");
            state.motion = motion;
            state.writeDefaultValues = false;
            stateMachine.defaultState = state;

            ctx.AssetSaver.SaveAsset(state);
            ctx.AssetSaver.SaveAsset(stateMachine);

            return new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };
        }

        /// <summary>
        /// 複数レイヤーから AnimatorController を作成する
        /// </summary>
        private AnimatorController CreateAnimatorControllerFromLayers(
            BuildContext ctx, ColorHSVAnimator animator, List<AnimatorControllerLayer> layers)
        {
            var controller = new AnimatorController
            {
                name = $"ColorHSV_{animator.gameObject.name}"
            };

            // 有効な軸のパラメータを追加
            if (animator.enableHue)
                controller.AddParameter(animator.GetEffectiveHueParameterName(), AnimatorControllerParameterType.Float);
            if (animator.enableSaturation)
                controller.AddParameter(animator.GetEffectiveSaturationParameterName(), AnimatorControllerParameterType.Float);
            if (animator.enableValue)
                controller.AddParameter(animator.GetEffectiveValueParameterName(), AnimatorControllerParameterType.Float);

            controller.layers = layers.ToArray();

            ctx.AssetSaver.SaveAsset(controller);
            return controller;
        }

        /// <summary>
        /// MA コンポーネントを追加する（メニュー分岐含む）
        /// </summary>
        private void SetupModularAvatarComponents(
            ColorHSVAnimator animator, AnimatorController controller,
            float defaultHue, float defaultSat, float defaultVal)
        {
            var gameObject = animator.gameObject;

            // MergeAnimator
            var mergeAnimator = gameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;

            // Parameters
            var maParameters = gameObject.AddComponent<ModularAvatarParameters>();

            if (animator.enableHue)
            {
                maParameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = animator.GetEffectiveHueParameterName(),
                    syncType = ParameterSyncType.Float,
                    defaultValue = defaultHue,
                    saved = animator.hueSaved,
                    localOnly = !animator.hueSynced
                });
            }

            if (animator.enableSaturation)
            {
                maParameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = animator.GetEffectiveSaturationParameterName(),
                    syncType = ParameterSyncType.Float,
                    defaultValue = defaultSat,
                    saved = animator.saturationSaved,
                    localOnly = !animator.saturationSynced
                });
            }

            if (animator.enableValue)
            {
                maParameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = animator.GetEffectiveValueParameterName(),
                    syncType = ParameterSyncType.Float,
                    defaultValue = defaultVal,
                    saved = animator.valueSaved,
                    localOnly = !animator.valueSynced
                });
            }

            // Menu Items
            int enabledCount = animator.EnabledAxisCount;

            if (enabledCount == 1)
            {
                // 単一軸: 親に直接 RadialPuppet
                string paramName = animator.enableHue ? animator.GetEffectiveHueParameterName()
                    : animator.enableSaturation ? animator.GetEffectiveSaturationParameterName()
                    : animator.GetEffectiveValueParameterName();

                Texture2D icon = animator.enableHue ? animator.hueMenuIcon
                    : animator.enableSaturation ? animator.saturationMenuIcon
                    : animator.valueMenuIcon;

                var menuItem = gameObject.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control = new VRCExpressionsMenu.Control
                {
                    name = animator.gameObject.name,
                    icon = icon,
                    type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                    subParameters = new[]
                    {
                        new VRCExpressionsMenu.Control.Parameter { name = paramName }
                    }
                };
            }
            else
            {
                // 複数軸: 親を SubMenu 化して子にRadialPuppet
                var subMenu = gameObject.AddComponent<ModularAvatarMenuItem>();
                subMenu.Control = new VRCExpressionsMenu.Control
                {
                    name = animator.gameObject.name,
                    icon = animator.parentMenuIcon,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu
                };
                subMenu.MenuSource = SubmenuSource.Children;

                if (animator.enableHue)
                    CreateChildMenuItem(gameObject, "色相", animator.GetEffectiveHueParameterName(), animator.hueMenuIcon);
                if (animator.enableSaturation)
                    CreateChildMenuItem(gameObject, "彩度", animator.GetEffectiveSaturationParameterName(), animator.saturationMenuIcon);
                if (animator.enableValue)
                    CreateChildMenuItem(gameObject, "明度", animator.GetEffectiveValueParameterName(), animator.valueMenuIcon);
            }
        }

        private void CreateChildMenuItem(GameObject parent, string displayName, string parameterName, Texture2D icon)
        {
            var child = new GameObject(displayName);
            child.transform.SetParent(parent.transform);

            var menuItem = child.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                name = displayName,
                icon = icon,
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                subParameters = new[]
                {
                    new VRCExpressionsMenu.Control.Parameter { name = parameterName }
                }
            };
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
                return "";

            string path = target.name;
            Transform current = target.parent;

            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
