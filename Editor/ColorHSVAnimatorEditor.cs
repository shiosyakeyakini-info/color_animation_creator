using System;
using System.Linq;
using nadena.dev.modular_avatar.core;
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
        private SerializedProperty hueSaved;
        private SerializedProperty hueSynced;
        private SerializedProperty hueMenuIcon;
        private SerializedProperty enableSaturation;
        private SerializedProperty saturationParameterName;
        private SerializedProperty saturationSaved;
        private SerializedProperty saturationSynced;
        private SerializedProperty saturationMenuIcon;
        private SerializedProperty enableValue;
        private SerializedProperty valueParameterName;
        private SerializedProperty valueSaved;
        private SerializedProperty valueSynced;
        private SerializedProperty valueMenuIcon;
        private SerializedProperty parentMenuIcon;

        private ReorderableList targetsList;

        /// <summary>
        /// liltoon シェーダーの主要な Color プロパティ一覧
        /// </summary>
        private static readonly (string propertyName, string label)[] LiltoonColorProperties =
        {
            ("_Color",            "メインカラー"),
            ("_Color2nd",         "メインカラー 2nd"),
            ("_Color3rd",         "メインカラー 3rd"),
            ("_EmissionColor",    "発光色"),
            ("_Emission2ndColor", "発光色 2nd"),
            ("_MatCapColor",      "マットキャップ"),
            ("_MatCap2ndColor",   "マットキャップ 2nd"),
            ("_RimColor",         "リムライト"),
            ("_RimIndirColor",    "リムライト (逆光)"),
            ("_RimShadeColor",    "リムシェード"),
            ("_BacklightColor",   "バックライト"),
            ("_ShadowColor",      "影色1"),
            ("_Shadow2ndColor",   "影色2"),
            ("_Shadow3rdColor",   "影色3"),
            ("_OutlineColor",     "輪郭線"),
            ("_OutlineLitColor",  "輪郭線 (ライト)"),
            ("_GlitterColor",     "ラメ"),
            ("_ReflectionColor",  "反射"),
            ("_MainTexHSVG",      "メインテクスチャ HSVG"),
            ("_OutlineTexHSVG",   "輪郭線テクスチャ HSVG"),
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
            hueSaved = serializedObject.FindProperty("hueSaved");
            hueSynced = serializedObject.FindProperty("hueSynced");
            hueMenuIcon = serializedObject.FindProperty("hueMenuIcon");
            enableSaturation = serializedObject.FindProperty("enableSaturation");
            saturationParameterName = serializedObject.FindProperty("saturationParameterName");
            saturationSaved = serializedObject.FindProperty("saturationSaved");
            saturationSynced = serializedObject.FindProperty("saturationSynced");
            saturationMenuIcon = serializedObject.FindProperty("saturationMenuIcon");
            enableValue = serializedObject.FindProperty("enableValue");
            valueParameterName = serializedObject.FindProperty("valueParameterName");
            valueSaved = serializedObject.FindProperty("valueSaved");
            valueSynced = serializedObject.FindProperty("valueSynced");
            valueMenuIcon = serializedObject.FindProperty("valueMenuIcon");
            parentMenuIcon = serializedObject.FindProperty("parentMenuIcon");

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
                    EditorGUILayout.PropertyField(hueSaved, new GUIContent("パラメータを保存"));
                    EditorGUILayout.PropertyField(hueSynced, new GUIContent("パラメータを同期"));
                    EditorGUILayout.PropertyField(hueMenuIcon, new GUIContent("メニューアイコン"));
                });

            // 彩度
            DrawAxisSection(
                "彩度 (Saturation)",
                enableSaturation,
                () =>
                {
                    EditorGUILayout.PropertyField(saturationParameterName, new GUIContent("パラメータ名"));
                    EditorGUILayout.PropertyField(saturationSaved, new GUIContent("パラメータを保存"));
                    EditorGUILayout.PropertyField(saturationSynced, new GUIContent("パラメータを同期"));
                    EditorGUILayout.PropertyField(saturationMenuIcon, new GUIContent("メニューアイコン"));
                });

            // 明度
            DrawAxisSection(
                "明度 (Value)",
                enableValue,
                () =>
                {
                    EditorGUILayout.PropertyField(valueParameterName, new GUIContent("パラメータ名"));
                    EditorGUILayout.PropertyField(valueSaved, new GUIContent("パラメータを保存"));
                    EditorGUILayout.PropertyField(valueSynced, new GUIContent("パラメータを同期"));
                    EditorGUILayout.PropertyField(valueMenuIcon, new GUIContent("メニューアイコン"));
                });

            EditorGUILayout.Space();

            // 親メニュー設定（2つ以上の軸が有効な場合のみ表示）
            if (animator.EnabledAxisCount > 1)
            {
                EditorGUILayout.LabelField("親メニュー設定", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(parentMenuIcon, new GUIContent("親メニューアイコン"));
                EditorGUILayout.HelpBox("複数軸が有効な場合、サブメニューが作成されます。", MessageType.Info);
                EditorGUILayout.Space();
            }

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
                {
                    // プリセットが選択された場合
                    colorPropNameProp.stringValue = DropdownPropertyNames[newIndex];
                }
                else
                {
                    // カスタムが選択された場合
                    // 現在の値がプリセットにある場合のみ、空文字列に設定
                    if (currentIndex >= 0)
                    {
                        colorPropNameProp.stringValue = "";
                    }
                    // 既にカスタム値の場合は何もしない（現在の値を保持）
                }
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
            // MA Menu Installer チェック
            var menuInstaller = animator.GetComponentInParent<ModularAvatarMenuInstaller>();
            if (menuInstaller == null)
            {
                EditorGUILayout.HelpBox(
                    "警告: このGameObjectまたは親の階層にMA Menu Installerが見つかりません。\n" +
                    "メニューを表示するにはMA Menu Installerが必要です。",
                    MessageType.Warning);
            }

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
