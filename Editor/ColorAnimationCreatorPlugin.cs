using System.Collections.Generic;
using System.Linq;
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
                .Run("Generate Color Hue Animations", GenerateColorHueAnimations);
        }

        private void GenerateColorHueAnimations(BuildContext ctx)
        {
            var animators = ctx.AvatarRootObject.GetComponentsInChildren<ColorHueAnimator>(true);

            Debug.Log($"[ColorAnimationCreator] Found {animators.Length} ColorHueAnimator component(s)");

            foreach (var animator in animators)
            {
                if (!animator.Validate())
                {
                    Debug.LogWarning($"[ColorAnimationCreator] ColorHueAnimator on '{animator.gameObject.name}' has invalid settings. Error: {animator.GetValidationError()}", animator);
                    continue;
                }

                // 1. アニメーションクリップを生成
                var clip = GenerateHueAnimationClip(ctx, animator);
                if (clip == null)
                {
                    Debug.LogError($"[ColorAnimationCreator] Failed to generate clip for '{animator.gameObject.name}'", animator);
                    continue;
                }

                Debug.Log($"[ColorAnimationCreator] Generated animation clip '{clip.name}' for '{animator.gameObject.name}'");

                // 2. AnimatorController を作成
                var controller = CreateAnimatorController(ctx, animator, clip);

                // 3. Modular Avatar コンポーネントを追加
                SetupModularAvatarComponents(animator, controller);

                Debug.Log($"[ColorAnimationCreator] Setup complete for '{animator.gameObject.name}' (parameter: {animator.parameterName})");

                // 4. ColorHueAnimator コンポーネントを削除（MA が後で処理するため不要）
                Object.DestroyImmediate(animator);
            }
        }

        private AnimationClip GenerateHueAnimationClip(BuildContext ctx, ColorHueAnimator animator)
        {
            // ベース色を取得（TTT MaterialModifier 処理後のマテリアルから直接読み取る）
            var material = animator.targetRenderer.sharedMaterials[animator.materialIndex];
            Color baseColor = material.GetColor(animator.colorPropertyName);

            // RGB → HSV 変換
            float h, s, v;
            Color.RGBToHSV(baseColor, out h, out s, out v);

            // アニメーションクリップを作成
            var clip = new AnimationClip
            {
                name = $"{animator.gameObject.name}_HueRotation",
                frameRate = 60f
            };

            // 対象オブジェクトの相対パスを取得
            string path = GetRelativePath(ctx.AvatarRootTransform, animator.targetRenderer.transform);

            // R、G、B 各チャンネルのキーフレーム配列を準備
            int steps = animator.hueSteps;
            Keyframe[] redKeys = new Keyframe[steps + 1];
            Keyframe[] greenKeys = new Keyframe[steps + 1];
            Keyframe[] blueKeys = new Keyframe[steps + 1];

            // 色相環をサンプリング
            for (int i = 0; i <= steps; i++)
            {
                float hueOffset = (float)i / steps;
                float currentHue = (h + hueOffset) % 1.0f;

                Color sampledColor = Color.HSVToRGB(currentHue, s, v, true);

                float time = hueOffset;

                redKeys[i] = new Keyframe(time, sampledColor.r);
                greenKeys[i] = new Keyframe(time, sampledColor.g);
                blueKeys[i] = new Keyframe(time, sampledColor.b);
            }

            // Tangent を Linear に設定
            SetLinearTangents(redKeys);
            SetLinearTangents(greenKeys);
            SetLinearTangents(blueKeys);

            // AnimationCurve を作成
            AnimationCurve redCurve = new AnimationCurve(redKeys);
            AnimationCurve greenCurve = new AnimationCurve(greenKeys);
            AnimationCurve blueCurve = new AnimationCurve(blueKeys);

            // プロパティパス（常に material.{propertyName} 形式を使用）
            string propertyBase = $"material.{animator.colorPropertyName}";

            Debug.Log($"[ColorAnimationCreator] Using property path: {propertyBase}.r/g/b on '{path}'");

            clip.SetCurve(path, typeof(Renderer), $"{propertyBase}.r", redCurve);
            clip.SetCurve(path, typeof(Renderer), $"{propertyBase}.g", greenCurve);
            clip.SetCurve(path, typeof(Renderer), $"{propertyBase}.b", blueCurve);

            ctx.AssetSaver.SaveAsset(clip);

            return clip;
        }

        private AnimatorController CreateAnimatorController(BuildContext ctx, ColorHueAnimator animator, AnimationClip clip)
        {
            var controller = new AnimatorController
            {
                name = $"HueShift_{animator.parameterName}"
            };

            // Float パラメータを追加
            controller.AddParameter(animator.parameterName, AnimatorControllerParameterType.Float);

            // レイヤーを作成
            var stateMachine = new AnimatorStateMachine
            {
                name = $"HueShift_{animator.parameterName}",
                hideFlags = HideFlags.HideInHierarchy
            };

            var state = stateMachine.AddState("HueRotation");
            state.motion = clip;
            state.writeDefaultValues = false;
            state.timeParameterActive = true;
            state.timeParameter = animator.parameterName;

            var layer = new AnimatorControllerLayer
            {
                name = $"HueShift_{animator.parameterName}",
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

            controller.layers = new[] { layer };

            // 全オブジェクトをアセットとして保存
            ctx.AssetSaver.SaveAsset(controller);
            ctx.AssetSaver.SaveAsset(stateMachine);
            ctx.AssetSaver.SaveAsset(state);

            return controller;
        }

        private void SetupModularAvatarComponents(ColorHueAnimator animator, AnimatorController controller)
        {
            var gameObject = animator.gameObject;

            // ModularAvatarMergeAnimator を追加
            var mergeAnimator = gameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;

            // ModularAvatarParameters を追加
            var maParameters = gameObject.GetComponent<ModularAvatarParameters>();
            if (maParameters == null)
            {
                maParameters = gameObject.AddComponent<ModularAvatarParameters>();
            }

            maParameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = animator.parameterName,
                syncType = ParameterSyncType.Float,
                defaultValue = 0f,
                saved = animator.saved,
                localOnly = !animator.synced
            });

            // ModularAvatarMenuItem を追加（RadialPuppet）
            var menuItem = gameObject.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                name = animator.gameObject.name,
                icon = animator.menuIcon,
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                subParameters = new[]
                {
                    new VRCExpressionsMenu.Control.Parameter { name = animator.parameterName }
                }
            };
        }

        private void SetLinearTangents(Keyframe[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                float inTangent = 0f;
                float outTangent = 0f;

                if (i > 0)
                {
                    float deltaTime = keys[i].time - keys[i - 1].time;
                    float deltaValue = keys[i].value - keys[i - 1].value;
                    inTangent = deltaValue / deltaTime;
                }

                if (i < keys.Length - 1)
                {
                    float deltaTime = keys[i + 1].time - keys[i].time;
                    float deltaValue = keys[i + 1].value - keys[i].value;
                    outTangent = deltaValue / deltaTime;
                }

                keys[i].inTangent = inTangent;
                keys[i].outTangent = outTangent;
                keys[i].weightedMode = WeightedMode.None;
            }
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return "";
            }

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
