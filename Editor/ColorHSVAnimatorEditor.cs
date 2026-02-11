using System;
using System.Linq;
using ShioShakeYakiNi.ColorAnimationCreator.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ShioShakeYakiNi.ColorAnimationCreator.Editor
{
    [CustomEditor(typeof(ColorHSVAnimator))]
    public class ColorHSVAnimatorEditor : UnityEditor.Editor
    {
        private SerializedProperty targetsProp;
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

        private ReorderableList targetsList;

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
            ("_MainTexHSVG",      "メインテクスチャ HSVG"),
            ("_OutlineTexHSVG",   "アウトラインテクスチャ HSVG"),
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
            MigrateIfNeeded();

            targetsProp = serializedObject.FindProperty("targets");
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

            targetsList = new ReorderableList(serializedObject, targetsProp, true, true, true, true);
            targetsList.drawHeaderCallback = DrawTargetsHeader;
            targetsList.drawElementCallback = DrawTargetElement;
            targetsList.elementHeightCallback = GetTargetElementHeight;
            targetsList.onAddCallback = OnAddTarget;
        }

        /// <summary>
        /// 旧単一ターゲットフィールドから新リスト形式へのマイグレーション
        /// </summary>
        private void MigrateIfNeeded()
        {
            var migratedProp = serializedObject.FindProperty("_migrated");
            var targetRendererProp = serializedObject.FindProperty("targetRenderer");
            var oldTargetsProp = serializedObject.FindProperty("targets");

            if (migratedProp == null || migratedProp.boolValue)
                return;

            if (targetRendererProp.objectReferenceValue == null)
            {
                migratedProp.boolValue = true;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            if (oldTargetsProp.arraySize == 0)
            {
                oldTargetsProp.InsertArrayElementAtIndex(0);
                var element = oldTargetsProp.GetArrayElementAtIndex(0);
                element.FindPropertyRelative("renderer").objectReferenceValue = targetRendererProp.objectReferenceValue;
                element.FindPropertyRelative("materialIndex").intValue = serializedObject.FindProperty("materialIndex").intValue;
                element.FindPropertyRelative("colorPropertyName").stringValue = serializedObject.FindProperty("colorPropertyName").stringValue;
            }

            targetRendererProp.objectReferenceValue = null;
            migratedProp.boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
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

            // ターゲットリスト
            targetsList.DoLayoutList();

            // HSVG モード表示
            if (animator.IsHSVGMode)
            {
                EditorGUILayout.HelpBox(
                    "HSVGモード: 各軸が独立したレイヤーで制御されます。\n" +
                    "色相: -0.5~0.5 (0.0=変化なし) / 彩度・明度: 0.0~2.0 (1.0=変化なし)",
                    MessageType.Info);
            }

            EditorGUILayout.Space();

            // 色相
            DrawAxisSection(
                "色相 (Hue)",
                enableHue,
                () =>
                {
                    if (!animator.IsHSVGMode)
                    {
                        EditorGUILayout.PropertyField(hueSteps, new GUIContent("サンプル数"));
                        EditorGUILayout.HelpBox(
                            "色相のサンプル数。数値が大きいほど滑らか（推奨: 24～36）",
                            MessageType.None);
                    }
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

        // ===== ReorderableList callbacks =====

        private void DrawTargetsHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "対象マテリアル");
        }

        private float GetTargetElementHeight(int index)
        {
            if (index < 0 || index >= targetsProp.arraySize)
                return EditorGUIUtility.singleLineHeight;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;
            float padding = 4f;

            // 4 lines: renderer, material slot, color property, validation
            int lineCount = 4;

            var element = targetsProp.GetArrayElementAtIndex(index);
            var colorPropName = element.FindPropertyRelative("colorPropertyName").stringValue;
            bool isCustomProp = Array.IndexOf(DropdownPropertyNames, colorPropName) < 0;
            if (isCustomProp) lineCount = 5;

            return lineCount * (lineHeight + spacing) + padding * 2;
        }

        private void DrawTargetElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = targetsProp.GetArrayElementAtIndex(index);
            var rendererProp = element.FindPropertyRelative("renderer");
            var matIndexProp = element.FindPropertyRelative("materialIndex");
            var colorPropNameProp = element.FindPropertyRelative("colorPropertyName");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;
            float padding = 4f;

            rect.y += padding;
            rect.height = lineHeight;

            // Line 1: Renderer
            EditorGUI.PropertyField(rect, rendererProp, new GUIContent("Renderer"));
            rect.y += lineHeight + spacing;

            // Line 2: Material slot popup
            DrawMaterialIndexPopup(rect, rendererProp, matIndexProp);
            rect.y += lineHeight + spacing;

            // Line 3: Color property dropdown
            DrawColorPropertyDropdownForElement(rect, colorPropNameProp);
            rect.y += lineHeight + spacing;

            // Line 3.5: Custom property name (if custom)
            bool isCustomProp = Array.IndexOf(DropdownPropertyNames, colorPropNameProp.stringValue) < 0;
            if (isCustomProp)
            {
                EditorGUI.PropertyField(rect, colorPropNameProp, new GUIContent("    プロパティ名"));
                rect.y += lineHeight + spacing;
            }

            // Line 4: Per-target validation
            DrawElementValidation(rect, rendererProp, matIndexProp, colorPropNameProp);
        }

        private void OnAddTarget(ReorderableList list)
        {
            int newIndex = targetsProp.arraySize;
            targetsProp.InsertArrayElementAtIndex(newIndex);
            var element = targetsProp.GetArrayElementAtIndex(newIndex);
            element.FindPropertyRelative("renderer").objectReferenceValue = null;
            element.FindPropertyRelative("materialIndex").intValue = 0;
            element.FindPropertyRelative("colorPropertyName").stringValue = "_EmissionColor";
        }

        // ===== Per-element drawing helpers =====

        private void DrawMaterialIndexPopup(Rect rect, SerializedProperty rendererProp, SerializedProperty matIndexProp)
        {
            if (rendererProp.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(rect, matIndexProp, new GUIContent("マテリアルスロット"));
                return;
            }

            var renderer = (Renderer)rendererProp.objectReferenceValue;
            var materials = renderer.sharedMaterials;

            var displayNames = new string[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                string matName = materials[i] != null ? materials[i].name : "<None>";
                displayNames[i] = $"[{i}] {matName}";
            }

            int currentIndex = matIndexProp.intValue;
            if (currentIndex < 0 || currentIndex >= materials.Length)
            {
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUI.Popup(rect, "マテリアルスロット", -1, displayNames);
                if (EditorGUI.EndChangeCheck() && newIndex >= 0)
                    matIndexProp.intValue = newIndex;
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUI.Popup(rect, "マテリアルスロット", currentIndex, displayNames);
                if (EditorGUI.EndChangeCheck())
                    matIndexProp.intValue = newIndex;
            }
        }

        private void DrawColorPropertyDropdownForElement(Rect rect, SerializedProperty colorPropNameProp)
        {
            int currentIndex = Array.IndexOf(DropdownPropertyNames, colorPropNameProp.stringValue);
            bool isCustom = currentIndex < 0;
            int selectedIndex = isCustom ? DropdownLabels.Length - 1 : currentIndex;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(rect, "色プロパティ", selectedIndex, DropdownLabels);
            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex < DropdownPropertyNames.Length)
                    colorPropNameProp.stringValue = DropdownPropertyNames[newIndex];
            }
        }

        private void DrawElementValidation(Rect rect, SerializedProperty rendererProp,
            SerializedProperty matIndexProp, SerializedProperty colorPropNameProp)
        {
            if (rendererProp.objectReferenceValue == null)
            {
                EditorGUI.HelpBox(rect, "Rendererが未設定です", MessageType.Warning);
                return;
            }

            var renderer = (Renderer)rendererProp.objectReferenceValue;
            if (matIndexProp.intValue < 0 || matIndexProp.intValue >= renderer.sharedMaterials.Length)
            {
                EditorGUI.HelpBox(rect, "マテリアルインデックスが範囲外です", MessageType.Warning);
                return;
            }

            var material = renderer.sharedMaterials[matIndexProp.intValue];
            if (material == null)
            {
                EditorGUI.HelpBox(rect, "マテリアルがnullです", MessageType.Error);
                return;
            }

            if (!material.HasProperty(colorPropNameProp.stringValue))
            {
                EditorGUI.HelpBox(rect, $"プロパティ '{colorPropNameProp.stringValue}' が存在しません", MessageType.Error);
                return;
            }

            int propIndex = material.shader.FindPropertyIndex(colorPropNameProp.stringValue);
            if (propIndex != -1)
            {
                var propType = material.shader.GetPropertyType(propIndex);
                bool isHSVG = MaterialColorTarget.IsHSVGProperty(colorPropNameProp.stringValue);

                if (isHSVG)
                {
                    if (propType != UnityEngine.Rendering.ShaderPropertyType.Vector)
                    {
                        EditorGUI.HelpBox(rect, $"プロパティ '{colorPropNameProp.stringValue}' はVector型ではありません", MessageType.Error);
                        return;
                    }
                }
                else
                {
                    if (propType != UnityEngine.Rendering.ShaderPropertyType.Color)
                    {
                        EditorGUI.HelpBox(rect, $"プロパティ '{colorPropNameProp.stringValue}' はColor型ではありません", MessageType.Error);
                        return;
                    }
                }
            }

            EditorGUI.HelpBox(rect, $"OK: {material.name}", MessageType.None);
        }

        // ===== Shared drawing helpers =====

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
                int targetCount = animator.targets.Count;
                string menuInfo = axisCount == 1
                    ? "RadialPuppet を1つ生成します。"
                    : $"サブメニュー内に RadialPuppet を{axisCount}つ生成します。";
                EditorGUILayout.HelpBox($"設定は正常です。{targetCount}個のマテリアルに対して{menuInfo}", MessageType.Info);
            }
        }
    }
}
