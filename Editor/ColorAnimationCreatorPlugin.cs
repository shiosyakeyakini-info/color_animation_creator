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

                Object.DestroyImmediate(animator);
            }
        }

        private void ProcessAnimator(BuildContext ctx, ColorHSVAnimator animator)
        {
            // ベース色を取得して HSV に分解
            var material = animator.targetRenderer.sharedMaterials[animator.materialIndex];
            Color baseColor = material.GetColor(animator.colorPropertyName);
            Color.RGBToHSV(baseColor, out float baseH, out float baseS, out float baseV);

            string path = GetRelativePath(ctx.AvatarRootTransform, animator.targetRenderer.transform);

            // clipS, clipV: 外側レイヤーが担当する軸は 1.0 にする
            float clipS = animator.enableSaturation ? 1.0f : baseS;
            float clipV = animator.enableValue ? 1.0f : baseV;

            // Blend Tree を内側から構築
            Motion rootMotion = BuildMotionTree(ctx, animator, path, baseH, baseS, baseV, clipS, clipV);

            // AnimatorController を作成
            var controller = CreateAnimatorController(ctx, animator, rootMotion);

            // MA コンポーネントを追加
            SetupModularAvatarComponents(animator, controller, baseS, baseV);

            Debug.Log($"[ColorAnimationCreator] Setup complete for '{animator.gameObject.name}'");
        }

        /// <summary>
        /// ネストされた Blend Tree（または単一クリップ）を内側から構築する
        /// </summary>
        private Motion BuildMotionTree(
            BuildContext ctx, ColorHSVAnimator animator, string path,
            float baseH, float baseS, float baseV, float clipS, float clipV)
        {
            Motion innerMotion;

            // Step 1: 最内 Motion — 色相 Blend Tree またはベース色クリップ
            if (animator.enableHue)
            {
                innerMotion = CreateHueBlendTree(ctx, animator, path, baseH, clipS, clipV);
            }
            else
            {
                // 色相固定: ベース色のクリップ
                Color fixedColor = Color.HSVToRGB(baseH, clipS, clipV, true);
                innerMotion = CreateColorClip(ctx, path, animator.materialIndex, animator.colorPropertyName, fixedColor,
                    $"{animator.gameObject.name}_BaseColor");
            }

            // Step 2: 彩度レイヤーで包む
            if (animator.enableSaturation)
            {
                float grayValue = animator.enableValue ? 1.0f : baseV;
                Color grayColor = new Color(grayValue, grayValue, grayValue);
                var grayClip = CreateColorClip(ctx, path, animator.materialIndex, animator.colorPropertyName, grayColor,
                    $"{animator.gameObject.name}_Gray");

                var satTree = new BlendTree
                {
                    name = $"Saturation_{animator.saturationParameterName}",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = animator.saturationParameterName,
                    useAutomaticThresholds = false
                };
                satTree.AddChild(grayClip, 0f);
                satTree.AddChild(innerMotion, 1f);
                ctx.AssetSaver.SaveAsset(satTree);

                innerMotion = satTree;
            }

            // Step 3: 明度レイヤーで包む
            if (animator.enableValue)
            {
                var blackClip = CreateColorClip(ctx, path, animator.materialIndex, animator.colorPropertyName, Color.black,
                    $"{animator.gameObject.name}_Black");

                var valTree = new BlendTree
                {
                    name = $"Value_{animator.valueParameterName}",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = animator.valueParameterName,
                    useAutomaticThresholds = false
                };
                valTree.AddChild(blackClip, 0f);
                valTree.AddChild(innerMotion, 1f);
                ctx.AssetSaver.SaveAsset(valTree);

                innerMotion = valTree;
            }

            return innerMotion;
        }

        /// <summary>
        /// 色相の 1D Blend Tree を作成する
        /// </summary>
        private BlendTree CreateHueBlendTree(
            BuildContext ctx, ColorHSVAnimator animator, string path,
            float baseH, float clipS, float clipV)
        {
            int steps = animator.hueSteps;

            var hueTree = new BlendTree
            {
                name = $"Hue_{animator.hueParameterName}",
                blendType = BlendTreeType.Simple1D,
                blendParameter = animator.hueParameterName,
                useAutomaticThresholds = false
            };

            for (int i = 0; i <= steps; i++)
            {
                float hueOffset = (float)i / steps;
                float currentHue = (baseH + hueOffset) % 1.0f;
                Color color = Color.HSVToRGB(currentHue, clipS, clipV, true);

                var clip = CreateColorClip(ctx, path, animator.materialIndex, animator.colorPropertyName, color,
                    $"{animator.gameObject.name}_Hue_{i}");

                hueTree.AddChild(clip, hueOffset);
            }

            ctx.AssetSaver.SaveAsset(hueTree);
            return hueTree;
        }

        /// <summary>
        /// 単一キーフレームのポーズクリップを作成する
        /// </summary>
        private AnimationClip CreateColorClip(
            BuildContext ctx, string path, int materialIndex, string colorPropertyName, Color color, string clipName)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f
            };

            // materialIndex に応じたプロパティプレフィックスを決定
            string propertyPrefix = materialIndex == 0
                ? $"material.{colorPropertyName}"
                : $"material[{materialIndex}].{colorPropertyName}";

            // ポーズクリップに 2 キーフレームを設定し、非ゼロ長を保証
            float duration = 1f / clip.frameRate;
            string[] channels = { ".r", ".g", ".b" };
            float[] values = { color.r, color.g, color.b };

            for (int ch = 0; ch < 3; ch++)
            {
                var binding = new EditorCurveBinding
                {
                    path = path,
                    type = typeof(Renderer),
                    propertyName = propertyPrefix + channels[ch]
                };
                var curve = new AnimationCurve(
                    new Keyframe(0f, values[ch]),
                    new Keyframe(duration, values[ch])
                );
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            ctx.AssetSaver.SaveAsset(clip);
            return clip;
        }

        /// <summary>
        /// AnimatorController を作成する（Blend Tree を motion に割り当て）
        /// </summary>
        private AnimatorController CreateAnimatorController(
            BuildContext ctx, ColorHSVAnimator animator, Motion rootMotion)
        {
            var controller = new AnimatorController
            {
                name = $"ColorHSV_{animator.gameObject.name}"
            };

            // 有効な軸のパラメータを追加
            if (animator.enableHue)
                controller.AddParameter(animator.hueParameterName, AnimatorControllerParameterType.Float);
            if (animator.enableSaturation)
                controller.AddParameter(animator.saturationParameterName, AnimatorControllerParameterType.Float);
            if (animator.enableValue)
                controller.AddParameter(animator.valueParameterName, AnimatorControllerParameterType.Float);

            var stateMachine = new AnimatorStateMachine
            {
                name = $"ColorHSV_{animator.gameObject.name}",
                hideFlags = HideFlags.HideInHierarchy
            };

            var state = stateMachine.AddState("ColorControl");
            state.motion = rootMotion;
            state.writeDefaultValues = false;

            // defaultState を明示的に設定（AddState が設定しないケースへの対策）
            stateMachine.defaultState = state;

            var layer = new AnimatorControllerLayer
            {
                name = $"ColorHSV_{animator.gameObject.name}",
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

            controller.layers = new[] { layer };

            // 依存される側から先に保存（参照の整合性を保証）
            ctx.AssetSaver.SaveAsset(state);
            ctx.AssetSaver.SaveAsset(stateMachine);
            ctx.AssetSaver.SaveAsset(controller);

            return controller;
        }

        /// <summary>
        /// MA コンポーネントを追加する（メニュー分岐含む）
        /// </summary>
        private void SetupModularAvatarComponents(
            ColorHSVAnimator animator, AnimatorController controller, float baseS, float baseV)
        {
            var gameObject = animator.gameObject;

            // MenuInstaller（メニューをアバターのExpression Menuにバインドするために必要）
            gameObject.AddComponent<ModularAvatarMenuInstaller>();

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
                    nameOrPrefix = animator.hueParameterName,
                    syncType = ParameterSyncType.Float,
                    defaultValue = 0f,
                    saved = animator.saved,
                    localOnly = !animator.synced
                });
            }

            if (animator.enableSaturation)
            {
                maParameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = animator.saturationParameterName,
                    syncType = ParameterSyncType.Float,
                    defaultValue = baseS,
                    saved = animator.saved,
                    localOnly = !animator.synced
                });
            }

            if (animator.enableValue)
            {
                maParameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = animator.valueParameterName,
                    syncType = ParameterSyncType.Float,
                    defaultValue = baseV,
                    saved = animator.saved,
                    localOnly = !animator.synced
                });
            }

            // Menu Items
            int enabledCount = animator.EnabledAxisCount;

            if (enabledCount == 1)
            {
                // 単一軸: 親に直接 RadialPuppet
                string paramName = animator.enableHue ? animator.hueParameterName
                    : animator.enableSaturation ? animator.saturationParameterName
                    : animator.valueParameterName;

                var menuItem = gameObject.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control = new VRCExpressionsMenu.Control
                {
                    name = animator.gameObject.name,
                    icon = animator.menuIcon,
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
                    icon = animator.menuIcon,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu
                };
                subMenu.MenuSource = SubmenuSource.Children;

                if (animator.enableHue)
                    CreateChildMenuItem(gameObject, "色相", animator.hueParameterName);
                if (animator.enableSaturation)
                    CreateChildMenuItem(gameObject, "彩度", animator.saturationParameterName);
                if (animator.enableValue)
                    CreateChildMenuItem(gameObject, "明度", animator.valueParameterName);
            }
        }

        private void CreateChildMenuItem(GameObject parent, string displayName, string parameterName)
        {
            var child = new GameObject(displayName);
            child.transform.SetParent(parent.transform);

            var menuItem = child.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                name = displayName,
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
