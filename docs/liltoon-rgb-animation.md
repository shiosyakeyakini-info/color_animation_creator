# liltoon 任意の色プロパティの色相変更ガイド

## 概要

liltoonシェーダーでは、`_MainTexHSVG`と`_OutlineTexHSVG`以外の色プロパティ（EmissionColor、MatCapColor、OutlineColorなど）には専用のHSVG調整パラメータが存在しません。

このガイドでは、**RGB値を直接アニメーション**して任意の色プロパティの色相を変更する方法を解説します。

## liltoonのHSVGパラメータの制限

### HSVGパラメータが存在するプロパティ

- **`_MainTexHSVG`** - メインテクスチャの色調整用
- **`_OutlineTexHSVG`** - アウトラインテクスチャの色調整用

### HSVGパラメータが存在しない色プロパティ

以下の色プロパティには専用のHSVG調整パラメータがありません:

| プロパティ名 | 説明 |
|------------|------|
| `_EmissionColor` | エミッション色 |
| `_Emission2ndColor` | セカンダリエミッション色 |
| `_MatCapColor` | MatCap色 |
| `_MatCap2ndColor` | セカンダリMatCap色 |
| `_RimColor` | リムライト色 |
| `_RimIndirColor` | 間接リムライト色 |
| `_BacklightColor` | バックライト色 |
| `_OutlineColor` | アウトライン色（※テクスチャではなく色自体） |
| `_OutlineLitColor` | ライト付きアウトライン色 |
| `_ShadowColor` | シャドウ色 |
| `_Shadow2ndColor` | セカンダリシャドウ色 |
| `_Shadow3rdColor` | ターシャリシャドウ色 |

## RGB直接アニメーションの実装方法

### アプローチ概要

1. **ビルド時にベース色を取得**
2. **RGB→HSV変換**を実行
3. **色相環をサンプリング**（0°～360°を均等分割）
4. **各サンプルでHSV→RGB変換**
5. **R、G、B各チャンネルのアニメーションカーブを生成**
6. **Motion Timeで制御**

### 詳細な実装手順

#### ステップ1: ベース色の取得とHSV変換

```csharp
// ベース色を取得
Material material = renderer.sharedMaterials[materialIndex];
Color baseColor = material.GetColor("_EmissionColor");

// RGB→HSV変換
float h, s, v;
Color.RGBToHSV(baseColor, out h, out s, out v);
```

**重要**: 彩度(S)と明度(V)は固定し、色相(H)のみを変化させます。

#### ステップ2: 色相環のサンプリング

```csharp
int hueSteps = 36; // 10度刻み（360° ÷ 36 = 10°）
float animationDuration = 1.0f; // アニメーションの長さ（秒）

// RGB値を格納する配列
Keyframe[] redKeys = new Keyframe[hueSteps];
Keyframe[] greenKeys = new Keyframe[hueSteps];
Keyframe[] blueKeys = new Keyframe[hueSteps];

for (int i = 0; i < hueSteps; i++)
{
    // 色相を計算（0.0～1.0の範囲で循環）
    float hueOffset = (float)i / hueSteps;
    float currentHue = (h + hueOffset) % 1.0f;

    // HSV→RGB変換
    Color sampledColor = Color.HSVToRGB(currentHue, s, v, true); // HDR色の場合はtrue

    // アニメーション時間を計算
    float time = (float)i / hueSteps * animationDuration;

    // キーフレームを作成（TangentをLinearに設定）
    redKeys[i] = new Keyframe(time, sampledColor.r, 0f, 0f); // inTangent=0, outTangent=0でLinear
    greenKeys[i] = new Keyframe(time, sampledColor.g, 0f, 0f);
    blueKeys[i] = new Keyframe(time, sampledColor.b, 0f, 0f);

    // TangentModeをLinearに設定
    AnimationUtility.SetKeyLeftTangentMode(redKeys, i, AnimationUtility.TangentMode.Linear);
    AnimationUtility.SetKeyRightTangentMode(redKeys, i, AnimationUtility.TangentMode.Linear);
    AnimationUtility.SetKeyLeftTangentMode(greenKeys, i, AnimationUtility.TangentMode.Linear);
    AnimationUtility.SetKeyRightTangentMode(greenKeys, i, AnimationUtility.TangentMode.Linear);
    AnimationUtility.SetKeyLeftTangentMode(blueKeys, i, AnimationUtility.TangentMode.Linear);
    AnimationUtility.SetKeyRightTangentMode(blueKeys, i, AnimationUtility.TangentMode.Linear);
}
```

#### ステップ3: アニメーションカーブの作成

```csharp
// AnimationCurveを作成
AnimationCurve redCurve = new AnimationCurve(redKeys);
AnimationCurve greenCurve = new AnimationCurve(greenKeys);
AnimationCurve blueCurve = new AnimationCurve(blueKeys);

// カーブをスムーズに設定（オプション、Linearの場合は不要）
// redCurve.SmoothTangents(0, 0f);
```

#### ステップ4: VirtualClipへの適用

```csharp
var clip = VirtualClip.Create("HueAnimation_EmissionColor");
clip.FrameRate = 60f;

// 相対パスを取得
string relativePath = GetRelativePath(avatarRoot, renderer.transform);

// EditorCurveBindingを作成（R、G、B各チャンネル）
var bindingR = new EditorCurveBinding
{
    path = relativePath,
    type = typeof(Renderer),
    propertyName = "material._EmissionColor.r"
};

var bindingG = new EditorCurveBinding
{
    path = relativePath,
    type = typeof(Renderer),
    propertyName = "material._EmissionColor.g"
};

var bindingB = new EditorCurveBinding
{
    path = relativePath,
    type = typeof(Renderer),
    propertyName = "material._EmissionColor.b"
};

// カーブを設定
clip.SetFloatCurve(bindingR, redCurve);
clip.SetFloatCurve(bindingG, greenCurve);
clip.SetFloatCurve(bindingB, blueCurve);

// アセットとして保存
ctx.AssetSaver.SaveAsset(clip.Clip);
```

#### ステップ5: Motion Time設定

```csharp
// FXレイヤーに新しいレイヤーを追加
var layer = new AnimatorControllerLayer
{
    name = "EmissionHueShift",
    defaultWeight = 1.0f,
    stateMachine = new AnimatorStateMachine()
};

// ステートを作成
var state = layer.stateMachine.AddState("HueRotation");
state.motion = clip.Clip;
state.writeDefaultValues = false;
state.timeParameterActive = true; // Motion Time有効
state.timeParameter = "EmissionHueRotation"; // Floatパラメータ名
```

## サンプル数の選択

### 推奨値

| サンプル数 | 角度刻み | 用途 |
|-----------|---------|------|
| 12 | 30° | 低負荷、粗い色相変化 |
| 24 | 15° | バランス型 |
| 36 | 10° | **推奨** - 滑らかで自然な色相変化 |
| 72 | 5° | 非常に滑らか、ファイルサイズ増加 |

### パフォーマンス考慮

- サンプル数が多いほどアニメーションファイルのサイズが増加
- 36サンプル（10度刻み）が品質とサイズのバランスが良好
- VRChatのアバター容量制限に注意

## RGB補間の制約と対策

### 問題: RGB空間での線形補間

Animation Curveは、キーフレーム間をRGB色空間で線形補間します。色相環は円形なので、RGB空間の直線補間では正確な色相回転になりません。

**例**: 赤(255,0,0) → 緑(0,255,0)の補間
- 期待: 赤→オレンジ→黄色→緑
- 実際: 赤→茶色→緑（中間色が暗くなる）

### 対策: 高密度サンプリング

十分な数のキーフレームを配置することで、補間の影響を最小化:

```csharp
// 36サンプルの場合、各キーフレーム間は10度のみ
// RGB空間での10度の補間誤差は視覚的にほぼ無視できる
```

### TangentをLinearに設定する理由

```csharp
// AutoやClampedの場合、カーブが曲線になり、色相変化が不均等になる
// Linearに設定することで、各サンプル間が均等に補間される
AnimationUtility.SetKeyLeftTangentMode(keys, i, AnimationUtility.TangentMode.Linear);
AnimationUtility.SetKeyRightTangentMode(keys, i, AnimationUtility.TangentMode.Linear);
```

## 複数マテリアルの対応

同じRendererの複数マテリアルを同時にアニメーション:

```csharp
// マテリアルインデックスを指定
var binding = new EditorCurveBinding
{
    path = relativePath,
    type = typeof(SkinnedMeshRenderer),
    propertyName = "material[0]._EmissionColor.r" // 1つ目のマテリアル
};

// または
propertyName = "m_Materials.Array.data[0]._EmissionColor.r"
```

## HDR色の扱い

### HDR色とは

- 通常の色: RGB各チャンネル 0.0～1.0
- HDR色: RGB各チャンネル 0.0～任意の値（1.0以上も可能）

### HDR色のHSV変換

```csharp
// HDR色を正しく扱うには、第4引数をtrueに設定
Color sampledColor = Color.HSVToRGB(currentHue, s, v, true);

// または、手動でHDR係数を計算
float hdrMax = Mathf.Max(baseColor.r, baseColor.g, baseColor.b);
if (hdrMax > 1.0f)
{
    // HDR係数を保持
    Color.RGBToHSV(baseColor / hdrMax, out h, out s, out v);
    Color sampledColor = Color.HSVToRGB(currentHue, s, v) * hdrMax;
}
```

## トラブルシューティング

### 色相が変化しない

**原因1**: プロパティ名が間違っている
- 解決: `material.HasProperty("_EmissionColor")`で確認

**原因2**: プロパティパスが間違っている
- 解決: `material._EmissionColor.r`（ドット区切り）を使用

**原因3**: アニメーションカーブが設定されていない
- 解決: R、G、B全てのチャンネルにカーブを設定

### 色相が均等に変化しない

**原因**: TangentがLinearになっていない
- 解決: `AnimationUtility.SetKeyLeftTangentMode()`と`SetKeyRightTangentMode()`を使用

### 中間色が暗くなる

**原因**: サンプル数が少なすぎる
- 解決: サンプル数を24～36に増やす

### HDR色が正しく表示されない

**原因**: HSV変換時にHDRフラグを指定していない
- 解決: `Color.HSVToRGB(h, s, v, true)`の第4引数をtrueに設定

## Unity C# APIリファレンス

### Color.RGBToHSV

```csharp
public static void RGBToHSV(Color rgbColor, out float H, out float S, out float V);
```

**パラメータ**:
- `rgbColor`: 入力RGB色
- `H`: 出力色相（0.0～1.0、360度を0～1で表現）
- `S`: 出力彩度（0.0～1.0）
- `V`: 出力明度（0.0～1.0）

### Color.HSVToRGB

```csharp
public static Color HSVToRGB(float H, float S, float V);
public static Color HSVToRGB(float H, float S, float V, bool hdr);
```

**パラメータ**:
- `H`: 色相（0.0～1.0）
- `S`: 彩度（0.0～1.0）
- `V`: 明度（0.0～1.0）
- `hdr`: HDR色を許可するか（デフォルト: false）

**戻り値**: 変換されたRGB色

## 実装例: 完全なコード

```csharp
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;

public static class HueAnimationGenerator
{
    public static AnimationClip GenerateHueAnimation(
        Renderer renderer,
        int materialIndex,
        string colorPropertyName,
        int hueSteps,
        Transform avatarRoot)
    {
        // ベース色を取得
        var material = renderer.sharedMaterials[materialIndex];
        Color baseColor = material.GetColor(colorPropertyName);

        // RGB→HSV変換
        float h, s, v;
        Color.RGBToHSV(baseColor, out h, out s, out v);

        // HDR係数を取得
        float hdrIntensity = Mathf.Max(baseColor.r, baseColor.g, baseColor.b);
        bool isHDR = hdrIntensity > 1.0f;

        // キーフレーム配列を準備
        Keyframe[] redKeys = new Keyframe[hueSteps];
        Keyframe[] greenKeys = new Keyframe[hueSteps];
        Keyframe[] blueKeys = new Keyframe[hueSteps];

        float duration = 1.0f;

        // 色相環をサンプリング
        for (int i = 0; i < hueSteps; i++)
        {
            float hueOffset = (float)i / hueSteps;
            float currentHue = (h + hueOffset) % 1.0f;

            Color sampledColor = Color.HSVToRGB(currentHue, s, v, isHDR);

            float time = (float)i / hueSteps * duration;

            redKeys[i] = new Keyframe(time, sampledColor.r);
            greenKeys[i] = new Keyframe(time, sampledColor.g);
            blueKeys[i] = new Keyframe(time, sampledColor.b);

            // TangentをLinearに設定
            AnimationUtility.SetKeyLeftTangentMode(redKeys, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(redKeys, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyLeftTangentMode(greenKeys, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(greenKeys, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyLeftTangentMode(blueKeys, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(blueKeys, i, AnimationUtility.TangentMode.Linear);
        }

        // AnimationCurveを作成
        AnimationCurve redCurve = new AnimationCurve(redKeys);
        AnimationCurve greenCurve = new AnimationCurve(greenKeys);
        AnimationCurve blueCurve = new AnimationCurve(blueKeys);

        // AnimationClipを作成
        AnimationClip clip = new AnimationClip();
        clip.name = $"HueShift_{colorPropertyName}";
        clip.frameRate = 60f;

        // 相対パスを取得
        string relativePath = GetRelativePath(avatarRoot, renderer.transform);

        // カーブを設定
        clip.SetCurve(relativePath, typeof(Renderer), $"material.{colorPropertyName}.r", redCurve);
        clip.SetCurve(relativePath, typeof(Renderer), $"material.{colorPropertyName}.g", greenCurve);
        clip.SetCurve(relativePath, typeof(Renderer), $"material.{colorPropertyName}.b", blueCurve);

        return clip;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root) return "";

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
```

## 参考資料

### Unity公式
- [Unity - Scripting API: Color.RGBToHSV](https://docs.unity3d.com/ScriptReference/Color.RGBToHSV.html)
- [Unity - Scripting API: Color.HSVToRGB](https://docs.unity3d.com/ScriptReference/Color.HSVToRGB.html)
- [Unity - Manual: Animation Curves](https://docs.unity3d.com/Manual/animeditor-AnimationCurves.html)

### liltoon
- [GitHub - lilxyzw/lilToon](https://github.com/lilxyzw/lilToon)
- [lilToon公式ドキュメント](https://lilxyzw.github.io/lilToon/)

### VRChat
- [VRChat Animator Parameters](https://creators.vrchat.com/avatars/animator-parameters/)
- [VRChat Expressions Menu and Controls](https://creators.vrchat.com/avatars/expression-menu-and-controls/)
