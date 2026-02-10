using UnityEngine;
using VRC.SDKBase;

namespace ShioShakeYakiNi.ColorAnimationCreator.Runtime
{
    [AddComponentMenu("Color Animation Creator/Color Hue Animator")]
    [DisallowMultipleComponent]
    public class ColorHueAnimator : MonoBehaviour, IEditorOnly
    {
        [Header("Target Settings")]
        [Tooltip("色相変更対象のRenderer")]
        public Renderer targetRenderer;

        [Tooltip("対象マテリアルのインデックス")]
        public int materialIndex = 0;

        [Header("Color Settings")]
        [Tooltip("対象の色プロパティ名（例: _EmissionColor, _MatCapColor, _OutlineColor）")]
        public string colorPropertyName = "_EmissionColor";

        [Tooltip("色相のサンプル数（多いほど滑らか、推奨: 24～36）")]
        [Range(8, 72)]
        public int hueSteps = 36;

        [Header("Menu Settings")]
        [Tooltip("Animatorパラメータ名")]
        public string parameterName = "HueRotation";

        [Tooltip("パラメータを保存するか")]
        public bool saved = true;

        [Tooltip("パラメータを同期するか")]
        public bool synced = true;

        [Tooltip("メニューアイコン（オプション）")]
        public Texture2D menuIcon;

        /// <summary>
        /// コンポーネントの設定が有効かどうかを検証します
        /// </summary>
        /// <returns>設定が有効ならtrue、無効ならfalse</returns>
        public bool Validate()
        {
            // Rendererが設定されているか
            if (targetRenderer == null)
            {
                return false;
            }

            // マテリアルインデックスが範囲内か
            if (materialIndex < 0 || materialIndex >= targetRenderer.sharedMaterials.Length)
            {
                return false;
            }

            // マテリアルが取得できるか
            var material = targetRenderer.sharedMaterials[materialIndex];
            if (material == null)
            {
                return false;
            }

            // シェーダーが設定されているか
            if (material.shader == null)
            {
                return false;
            }

            // 色プロパティが存在するか
            if (!material.HasProperty(colorPropertyName))
            {
                return false;
            }

            // プロパティがColor型かチェック
            int propertyIndex = material.shader.FindPropertyIndex(colorPropertyName);
            if (propertyIndex == -1)
            {
                return false;
            }

            var propertyType = material.shader.GetPropertyType(propertyIndex);
            if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Color)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 対象マテリアルのベース色を取得します
        /// </summary>
        /// <returns>ベース色。検証失敗時はColor.white</returns>
        public Color GetBaseColor()
        {
            if (!Validate())
            {
                return Color.white;
            }

            var material = targetRenderer.sharedMaterials[materialIndex];
            return material.GetColor(colorPropertyName);
        }

        /// <summary>
        /// 検証エラーメッセージを取得します（デバッグ用）
        /// </summary>
        /// <returns>エラーメッセージ。問題なければnull</returns>
        public string GetValidationError()
        {
            if (targetRenderer == null)
            {
                return "Target Renderer is not set.";
            }

            if (materialIndex < 0 || materialIndex >= targetRenderer.sharedMaterials.Length)
            {
                return $"Material Index {materialIndex} is out of range (0-{targetRenderer.sharedMaterials.Length - 1}).";
            }

            var material = targetRenderer.sharedMaterials[materialIndex];
            if (material == null)
            {
                return $"Material at index {materialIndex} is null.";
            }

            if (material.shader == null)
            {
                return "Material's shader is null.";
            }

            if (!material.HasProperty(colorPropertyName))
            {
                return $"Material does not have property '{colorPropertyName}'.";
            }

            int propertyIndex = material.shader.FindPropertyIndex(colorPropertyName);
            if (propertyIndex == -1)
            {
                return $"Shader property '{colorPropertyName}' not found.";
            }

            var propertyType = material.shader.GetPropertyType(propertyIndex);
            if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Color)
            {
                return $"Property '{colorPropertyName}' is not a Color type (found: {propertyType}).";
            }

            return null;
        }
    }
}
