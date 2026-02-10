using System;
using System.Linq;
using ShioShakeYakiNi.ColorAnimationCreator.Runtime;
using UnityEditor;
using UnityEngine;

namespace ShioShakeYakiNi.ColorAnimationCreator.Editor
{
    [CustomEditor(typeof(ColorHSVAnimator))]
    public class ColorHSVAnimatorEditor : UnityEditor.Editor
    {
        private SerializedProperty targetRenderer;
        private SerializedProperty materialIndex;
        private SerializedProperty colorPropertyName;
        private SerializedProperty enableHue;
        private SerializedProperty hueSteps;
        private SerializedProperty hueParameterName;
        private SerializedProperty enableSaturation;
        private SerializedProperty saturationParameterName;
        private SerializedProperty enableValue;
        private SerializedProperty valueParameterName;
        private SerializedProperty saved;
        private SerializedProperty synced;
        private SerializedProperty menuIcon;

        /// <summary>
        /// liltoon シェーダーの主要な Color プロパティ一覧
        /// </summary>
        private static readonly (string propertyName, string label)[] LiltoonColorProperties =
        {
            ("_Color",            "メインカラー"),
            ("_Color2nd",         "メインカラー 2nd"),
            ("_Color3rd",         "メインカラー 3rd"),
            ("_EmissionColor",    "エミッション"),
            ("_Emission2ndColor", "エミッション 2nd"),
            ("_MatCapColor",      "マットキャップ"),
            ("_MatCap2ndColor",   "マットキャップ 2nd"),
            ("_RimColor",         "リムライト"),
            ("_RimIndirColor",    "リムライト (逆光)"),
            ("_RimShadeColor",    "リムシェード"),
            ("_BacklightColor",   "バックライト"),
            ("_ShadowColor",      "影色 1st"),
            ("_Shadow2ndColor",   "影色 2nd"),
            ("_Shadow3rdColor",   "影色 3rd"),
            ("_OutlineColor",     "アウトライン"),
            ("_OutlineLitColor",  "アウトライン (ライト)"),
            ("_GlitterColor",     "ラメ"),
            ("_ReflectionColor",  "反射"),
        };

        private static readonly string[] DropdownLabels;
        private static readonly string[] DropdownPropertyNames;

        static ColorHSVAnimatorEditor()
        {
            DropdownLabels = LiltoonColorProperties
                .Select(p => $"{p.label} ({p.propertyName})")
                .Append("カスタム")
                .ToArray();

            DropdownPropertyNames = LiltoonColorProperties
                .Select(p => p.propertyName)
                .ToArray();
        }

        private void OnEnable()
        {
            targetRenderer = serializedObject.FindProperty("targetRenderer");
            materialIndex = serializedObject.FindProperty("materialIndex");
            colorPropertyName = serializedObject.FindProperty("colorPropertyName");
            enableHue = serializedObject.FindProperty("enableHue");
            hueSteps = serializedObject.FindProperty("hueSteps");
            hueParameterName = serializedObject.FindProperty("hueParameterName");
            enableSaturation = serializedObject.FindProperty("enableSaturation");
            saturationParameterName = serializedObject.FindProperty("saturationParameterName");
            enableValue = serializedObject.FindProperty("enableValue");
            valueParameterName = serializedObject.FindProperty("valueParameterName");
            saved = serializedObject.FindProperty("saved");
            synced = serializedObject.FindProperty("synced");
            menuIcon = serializedObject.FindProperty("menuIcon");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var animator = (ColorHSVAnimator)target;

            // ヘッダー
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Color HSV Animator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "色プロパティの色相・彩度・明度をRadial Menuで独立制御します。\n" +
                "ビルド時にネストされたBlend Treeを自動生成し、AnimatorとMenuに追加します。",
                MessageType.Info);
            EditorGUILayout.Space();

            // ターゲット設定
            EditorGUILayout.LabelField("ターゲット設定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetRenderer, new GUIContent("対象Renderer"));
            EditorGUILayout.PropertyField(materialIndex, new GUIContent("マテリアルインデックス"));

            DrawMaterialInfo();

            EditorGUILayout.Space();

            // 色設定
            EditorGUILayout.LabelField("色設定", EditorStyles.boldLabel);
            DrawColorPropertyDropdown();
            DrawPropertyValidation(animator);

            EditorGUILayout.Space();

            // 色相
            DrawAxisSection(
                "色相 (Hue)",
                enableHue,
                () =>
                {
                    EditorGUILayout.PropertyField(hueSteps, new GUIContent("サンプル数"));
                    EditorGUILayout.HelpBox(
                        "色相のサンプル数。数値が大きいほど滑らか（推奨: 24～36）",
                        MessageType.None);
                    EditorGUILayout.PropertyField(hueParameterName, new GUIContent("パラメータ名"));
                });

            // 彩度
            DrawAxisSection(
                "彩度 (Saturation)",
                enableSaturation,
                () =>
                {
                    EditorGUILayout.PropertyField(saturationParameterName, new GUIContent("パラメータ名"));
                });

            // 明度
            DrawAxisSection(
                "明度 (Value)",
                enableValue,
                () =>
                {
                    EditorGUILayout.PropertyField(valueParameterName, new GUIContent("パラメータ名"));
                });

            EditorGUILayout.Space();

            // 共通設定
            EditorGUILayout.LabelField("共通設定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(saved, new GUIContent("パラメータを保存"));
            EditorGUILayout.PropertyField(synced, new GUIContent("パラメータを同期"));
            EditorGUILayout.PropertyField(menuIcon, new GUIContent("メニューアイコン"));

            EditorGUILayout.Space();

            // 検証結果
            DrawValidationResult(animator);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAxisSection(string label, SerializedProperty enableProp, Action drawDetails)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(enableProp, new GUIContent("有効化"));

            if (enableProp.boolValue)
            {
                EditorGUI.indentLevel++;
                drawDetails();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMaterialInfo()
        {
            if (targetRenderer.objectReferenceValue == null)
                return;

            var renderer = (Renderer)targetRenderer.objectReferenceValue;
            int maxIndex = renderer.sharedMaterials.Length - 1;

            if (materialIndex.intValue < 0 || materialIndex.intValue > maxIndex)
            {
                EditorGUILayout.HelpBox(
                    $"マテリアルインデックスは 0 ～ {maxIndex} の範囲で指定してください。",
                    MessageType.Warning);
            }
            else
            {
                var material = renderer.sharedMaterials[materialIndex.intValue];
                if (material != null)
                {
                    EditorGUILayout.HelpBox($"対象マテリアル: {material.name}", MessageType.None);
                }
            }
        }

        private void DrawPropertyValidation(ColorHSVAnimator animator)
        {
            if (targetRenderer.objectReferenceValue == null)
                return;

            var renderer = (Renderer)targetRenderer.objectReferenceValue;
            if (materialIndex.intValue < 0 || materialIndex.intValue >= renderer.sharedMaterials.Length)
                return;

            var material = renderer.sharedMaterials[materialIndex.intValue];
            if (material == null)
                return;

            if (!material.HasProperty(colorPropertyName.stringValue))
            {
                EditorGUILayout.HelpBox(
                    $"マテリアル '{material.name}' にプロパティ '{colorPropertyName.stringValue}' が存在しません。",
                    MessageType.Error);
            }
            else
            {
                int propIndex = material.shader.FindPropertyIndex(colorPropertyName.stringValue);
                if (propIndex != -1)
                {
                    var propType = material.shader.GetPropertyType(propIndex);
                    if (propType != UnityEngine.Rendering.ShaderPropertyType.Color)
                    {
                        EditorGUILayout.HelpBox(
                            $"プロパティ '{colorPropertyName.stringValue}' はColor型ではありません（型: {propType}）。",
                            MessageType.Error);
                    }
                }
            }
        }

        private void DrawValidationResult(ColorHSVAnimator animator)
        {
            string validationError = animator.GetValidationError();
            if (!string.IsNullOrEmpty(validationError))
            {
                EditorGUILayout.HelpBox("検証エラー: " + validationError, MessageType.Error);
            }
            else
            {
                int axisCount = animator.EnabledAxisCount;
                string menuInfo = axisCount == 1
                    ? "RadialPuppet を1つ生成します。"
                    : $"サブメニュー内に RadialPuppet を{axisCount}つ生成します。";
                EditorGUILayout.HelpBox($"設定は正常です。{menuInfo}", MessageType.Info);
            }
        }

        private void DrawColorPropertyDropdown()
        {
            int currentIndex = Array.IndexOf(DropdownPropertyNames, colorPropertyName.stringValue);
            bool isCustom = currentIndex < 0;
            int selectedIndex = isCustom ? DropdownLabels.Length - 1 : currentIndex;

            int newIndex = EditorGUILayout.Popup("色プロパティ", selectedIndex, DropdownLabels);

            if (newIndex != selectedIndex)
            {
                if (newIndex < DropdownPropertyNames.Length)
                {
                    colorPropertyName.stringValue = DropdownPropertyNames[newIndex];
                }
            }

            if (newIndex >= DropdownPropertyNames.Length)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(colorPropertyName, new GUIContent("プロパティ名"));
                EditorGUI.indentLevel--;
            }
        }
    }
}
