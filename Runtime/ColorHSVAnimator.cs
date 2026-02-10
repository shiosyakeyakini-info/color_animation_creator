using UnityEngine;
using VRC.SDKBase;

namespace ShioShakeYakiNi.ColorAnimationCreator.Runtime
{
    [AddComponentMenu("Color Animation Creator/Color HSV Animator")]
    [DisallowMultipleComponent]
    public class ColorHSVAnimator : MonoBehaviour, IEditorOnly
    {
        [Header("Target Settings")]
        [Tooltip("色変更対象のRenderer")]
        public Renderer targetRenderer;

        [Tooltip("対象マテリアルのインデックス")]
        public int materialIndex = 0;

        [Header("Color Settings")]
        [Tooltip("対象の色プロパティ名（例: _EmissionColor, _MatCapColor, _OutlineColor）")]
        public string colorPropertyName = "_EmissionColor";

        [Header("Hue")]
        [Tooltip("色相制御を有効化")]
        public bool enableHue = true;

        [Tooltip("色相のサンプル数（多いほど滑らか、推奨: 24～36）")]
        [Range(8, 72)]
        public int hueSteps = 36;

        [Tooltip("色相パラメータ名")]
        public string hueParameterName = "HueRotation";

        [Header("Saturation")]
        [Tooltip("彩度制御を有効化")]
        public bool enableSaturation = false;

        [Tooltip("彩度パラメータ名")]
        public string saturationParameterName = "SaturationControl";

        [Header("Value (Brightness)")]
        [Tooltip("明度制御を有効化")]
        public bool enableValue = false;

        [Tooltip("明度パラメータ名")]
        public string valueParameterName = "ValueControl";

        [Header("Menu Settings")]
        [Tooltip("パラメータを保存するか")]
        public bool saved = true;

        [Tooltip("パラメータを同期するか")]
        public bool synced = true;

        [Tooltip("メニューアイコン（オプション）")]
        public Texture2D menuIcon;

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
            if (targetRenderer == null)
                return false;

            if (materialIndex < 0 || materialIndex >= targetRenderer.sharedMaterials.Length)
                return false;

            var material = targetRenderer.sharedMaterials[materialIndex];
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
            if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Color)
                return false;

            if (EnabledAxisCount == 0)
                return false;

            return true;
        }

        /// <summary>
        /// 対象マテリアルのベース色を取得します
        /// </summary>
        public Color GetBaseColor()
        {
            if (!Validate())
                return Color.white;

            var material = targetRenderer.sharedMaterials[materialIndex];
            return material.GetColor(colorPropertyName);
        }

        /// <summary>
        /// 検証エラーメッセージを取得します
        /// </summary>
        public string GetValidationError()
        {
            if (targetRenderer == null)
                return "Target Renderer is not set.";

            if (materialIndex < 0 || materialIndex >= targetRenderer.sharedMaterials.Length)
                return $"Material Index {materialIndex} is out of range (0-{targetRenderer.sharedMaterials.Length - 1}).";

            var material = targetRenderer.sharedMaterials[materialIndex];
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
            if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Color)
                return $"Property '{colorPropertyName}' is not a Color type (found: {propertyType}).";

            if (EnabledAxisCount == 0)
                return "At least one axis (Hue, Saturation, or Value) must be enabled.";

            return null;
        }
    }
}
