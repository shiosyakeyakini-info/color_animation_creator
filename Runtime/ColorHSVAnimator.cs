using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace ShioShakeYakiNi.ColorAnimationCreator.Runtime
{
    [Serializable]
    public class MaterialColorTarget
    {
        [Tooltip("色変更対象のRenderer")]
        public Renderer renderer;

        [Tooltip("対象マテリアルのインデックス")]
        public int materialIndex;

        [Tooltip("対象の色プロパティ名（例: _EmissionColor, _MatCapColor, _OutlineColor）")]
        public string colorPropertyName = "_EmissionColor";

        /// <summary>
        /// プロパティ名がHSVGモードかを判定
        /// </summary>
        public static bool IsHSVGProperty(string propertyName)
        {
            return propertyName != null && propertyName.EndsWith("HSVG");
        }

        /// <summary>
        /// このターゲットがHSVGモードかを判定
        /// </summary>
        public bool IsHSVG => IsHSVGProperty(colorPropertyName);

        /// <summary>
        /// このターゲットが有効かどうかを検証します
        /// </summary>
        public bool Validate()
        {
            if (renderer == null)
                return false;

            if (materialIndex < 0 || materialIndex >= renderer.sharedMaterials.Length)
                return false;

            var material = renderer.sharedMaterials[materialIndex];
            if (material == null)
                return false;

            if (material.shader == null)
                return false;

            if (!material.HasProperty(colorPropertyName))
                return false;

            int propertyIndex = material.shader.FindPropertyIndex(colorPropertyName);
            if (propertyIndex == -1)
                return false;

            var propertyType = material.shader.GetPropertyType(propertyIndex);
            if (IsHSVG)
            {
                if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Vector)
                    return false;
            }
            else
            {
                if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Color)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 検証エラーメッセージを取得します
        /// </summary>
        public string GetValidationError()
        {
            if (renderer == null)
                return "Renderer is not set.";

            if (materialIndex < 0 || materialIndex >= renderer.sharedMaterials.Length)
                return $"Material Index {materialIndex} is out of range (0-{renderer.sharedMaterials.Length - 1}).";

            var material = renderer.sharedMaterials[materialIndex];
            if (material == null)
                return $"Material at index {materialIndex} is null.";

            if (material.shader == null)
                return "Material's shader is null.";

            if (!material.HasProperty(colorPropertyName))
                return $"Material does not have property '{colorPropertyName}'.";

            int propertyIndex = material.shader.FindPropertyIndex(colorPropertyName);
            if (propertyIndex == -1)
                return $"Shader property '{colorPropertyName}' not found.";

            var propertyType = material.shader.GetPropertyType(propertyIndex);
            if (IsHSVG)
            {
                if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Vector)
                    return $"Property '{colorPropertyName}' is not a Vector type (found: {propertyType}).";
            }
            else
            {
                if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Color)
                    return $"Property '{colorPropertyName}' is not a Color type (found: {propertyType}).";
            }

            return null;
        }

        /// <summary>
        /// ベース色を取得します
        /// </summary>
        public Color GetBaseColor()
        {
            if (!Validate())
                return Color.white;

            var material = renderer.sharedMaterials[materialIndex];
            return material.GetColor(colorPropertyName);
        }

        /// <summary>
        /// HSVGのベースVector4値を取得します
        /// </summary>
        public Vector4 GetBaseVector()
        {
            if (!Validate())
                return new Vector4(0f, 1f, 1f, 1f);

            var material = renderer.sharedMaterials[materialIndex];
            return material.GetVector(colorPropertyName);
        }
    }

    [AddComponentMenu("Color Animation Creator/Color HSV Animator")]
    [DisallowMultipleComponent]
    public class ColorHSVAnimator : MonoBehaviour, IEditorOnly
    {
        // --- Legacy fields for migration (hidden from inspector) ---
        [SerializeField, HideInInspector] private Renderer targetRenderer;
        [SerializeField, HideInInspector] private int materialIndex = 0;
        [SerializeField, HideInInspector] private string colorPropertyName = "_EmissionColor";
        [SerializeField, HideInInspector] private bool _migrated = false;

        [Tooltip("色変更対象のマテリアルリスト")]
        public List<MaterialColorTarget> targets = new List<MaterialColorTarget>();

        [Tooltip("色相制御を有効化")]
        public bool enableHue = true;

        [Tooltip("色相のサンプル数（多いほど滑らか、推奨: 24～36）")]
        [Range(8, 72)]
        public int hueSteps = 36;

        [Tooltip("色相パラメータ名（空の場合は自動生成）")]
        public string hueParameterName = "";

        [Tooltip("色相パラメータを保存するか")]
        public bool hueSaved = true;

        [Tooltip("色相パラメータを同期するか")]
        public bool hueSynced = true;

        [Tooltip("色相メニューアイコン（オプション）")]
        public Texture2D hueMenuIcon;

        [Tooltip("彩度制御を有効化")]
        public bool enableSaturation = false;

        [Tooltip("彩度パラメータ名（空の場合は自動生成）")]
        public string saturationParameterName = "";

        [Tooltip("彩度パラメータを保存するか")]
        public bool saturationSaved = true;

        [Tooltip("彩度パラメータを同期するか")]
        public bool saturationSynced = true;

        [Tooltip("彩度メニューアイコン（オプション）")]
        public Texture2D saturationMenuIcon;

        [Tooltip("明度制御を有効化")]
        public bool enableValue = false;

        [Tooltip("明度パラメータ名（空の場合は自動生成）")]
        public string valueParameterName = "";

        [Tooltip("明度パラメータを保存するか")]
        public bool valueSaved = true;

        [Tooltip("明度パラメータを同期するか")]
        public bool valueSynced = true;

        [Tooltip("明度メニューアイコン（オプション）")]
        public Texture2D valueMenuIcon;

        [Tooltip("親メニューアイコン（複数軸有効時のサブメニュー用、オプション）")]
        public Texture2D parentMenuIcon;

        /// <summary>
        /// 全ターゲットがHSVGプロパティかどうか
        /// </summary>
        public bool IsHSVGMode
        {
            get
            {
                if (targets == null || targets.Count == 0) return false;
                return targets[0].IsHSVG;
            }
        }

        /// <summary>
        /// 有効な軸の数を返します
        /// </summary>
        public int EnabledAxisCount
        {
            get
            {
                int count = 0;
                if (enableHue) count++;
                if (enableSaturation) count++;
                if (enableValue) count++;
                return count;
            }
        }

        /// <summary>
        /// コンポーネントの設定が有効かどうかを検証します
        /// </summary>
        public bool Validate()
        {
            if (targets == null || targets.Count == 0)
                return false;

            foreach (var t in targets)
            {
                if (!t.Validate())
                    return false;
            }

            if (EnabledAxisCount == 0)
                return false;

            return true;
        }

        /// <summary>
        /// 最初のターゲットのベース色を取得します（パラメータデフォルト値用）
        /// </summary>
        public Color GetPrimaryBaseColor()
        {
            if (targets != null && targets.Count > 0 && targets[0].Validate())
                return targets[0].GetBaseColor();
            return Color.white;
        }

        /// <summary>
        /// 最初のターゲットのベースVector4値を取得します（HSVGモード用）
        /// </summary>
        public Vector4 GetPrimaryBaseVector()
        {
            if (targets != null && targets.Count > 0 && targets[0].Validate())
                return targets[0].GetBaseVector();
            return new Vector4(0f, 1f, 1f, 1f);
        }

        /// <summary>
        /// 検証エラーメッセージを取得します
        /// </summary>
        public string GetValidationError()
        {
            if (targets == null || targets.Count == 0)
                return "No material targets are configured.";

            for (int i = 0; i < targets.Count; i++)
            {
                string err = targets[i].GetValidationError();
                if (err != null)
                    return $"Target [{i}]: {err}";
            }

            if (EnabledAxisCount == 0)
                return "At least one axis (Hue, Saturation, or Value) must be enabled.";

            return null;
        }

        /// <summary>
        /// パラメータ名が空の場合、自動生成された名前を返します
        /// </summary>
        public string GetEffectiveParameterName(string paramName, string axisName)
        {
            if (string.IsNullOrWhiteSpace(paramName))
            {
                return $"{gameObject.name}_{axisName}";
            }
            return paramName;
        }

        /// <summary>
        /// 実際に使用される色相パラメータ名を取得します
        /// </summary>
        public string GetEffectiveHueParameterName() => GetEffectiveParameterName(hueParameterName, "Hue");

        /// <summary>
        /// 実際に使用される彩度パラメータ名を取得します
        /// </summary>
        public string GetEffectiveSaturationParameterName() => GetEffectiveParameterName(saturationParameterName, "Saturation");

        /// <summary>
        /// 実際に使用される明度パラメータ名を取得します
        /// </summary>
        public string GetEffectiveValueParameterName() => GetEffectiveParameterName(valueParameterName, "Value");
    }
}
