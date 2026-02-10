# liltoon 色相変更詳細ガイド

## 概要

liltoonシェーダーは、VRChat向けの高機能シェーダーで、色相(Hue)、彩度(Saturation)、明度(Value)、ガンマ(Gamma)を調整する機能を提供します。このガイドでは、特に色相をRadial Puppetで制御する方法を解説します。

## liltoonの色調整パラメータ

### 基本カラー

```csharp
プロパティ名: _Color (内部名) / sColor (Inspector表示名)
型: Color (HDR対応)
デフォルト値: (1, 1, 1, 1)
```

### HSVGパラメータ

```csharp
プロパティ名: _MainTexHSVG
型: Vector (Vector4)
構成要素:
  X: Hue (色相)          範囲: 0.0 ~ 1.0 (0° ~ 360°)
  Y: Saturation (彩度)   範囲: 0.0 ~ 2.0
  Z: Value (明度)        範囲: 0.0 ~ 2.0
  W: Gamma (ガンマ)      範囲: 0.0 ~ 2.0
```

**重要**: liltoonでは、`_Color`が基本色を制御し、`_MainTexHSVG`がHSVGによる色調整を行います。色相のアニメーションには主に`_MainTexHSVG`のX成分を使用します。

## 色相変更の実装方法

### Method 1: Motion Time（推奨）

Motion Timeを使用すると、1つのアニメーションクリップでFloatパラメータの値に応じて任意のフレームを再生できます。

#### ステップ1: アニメーションクリップの作成

1. **新しいアニメーションクリップを作成**
   - プロジェクトウィンドウで右クリック → Create → Animation

2. **キーフレームを追加**
   - Animation Windowを開く
   - 対象のマテリアルを持つオブジェクトを選択
   - 録画ボタンをクリック

   **キーフレーム設定:**
   | 時間 | プロパティ | 値 |
   |------|-----------|-----|
   | 0:00 | material._MainTexHSVG.x | 0.0 |
   | 0:01 | material._MainTexHSVG.x | 1.0 |

3. **Tangentを設定**
   - Dope Sheetビューに切り替え
   - 両方のキーフレームを選択
   - 右クリック → Both Tangents → Linear

**重要**: TangentをLinearに設定しないと、カーブが曲線になり、色相が均等に変化しません。

#### ステップ2: マテリアルの準備

1. liltoonのInspectorでマテリアルをロック（鍵アイコン）
2. アニメーション対象のプロパティ（`_MainTexHSVG`）を右クリック
3. `Animated (when locked)`を選択
   - 複数マテリアルの場合は`Renamed (when locked)`を使用

#### ステップ3: Animator Controllerの設定

1. **FXレイヤーに新しいレイヤーを追加**
   ```
   名前: HueShift
   Weight: 1.0
   Blending: Override (または Additive)
   ```

2. **新しいステートを作成**
   ```
   名前: HueRotation
   Motion: 上記で作成したアニメーションクリップ
   Speed: 1
   Write Defaults: OFF (推奨)
   ```

3. **Motion Timeを有効化**
   ```
   ステートのインスペクターで:
   ☑ Motion Time
   Motion Time Parameter: HueRotation (Floatパラメータ)
   ```

#### ステップ4: パラメータの作成

**Animator Controller:**
```
名前: HueRotation
タイプ: Float
デフォルト値: 0
```

**VRC Expression Parameters:**
```
名前: HueRotation
タイプ: Float
デフォルト値: 0.0
Saved: はい
Synced: はい
```

#### ステップ5: Expression Menuの設定

```
Control Type: Radial Puppet
Parameter Rotation: HueRotation
Label: 色相変更
Icon: (お好みのアイコン)
```

### Method 2: ブレンドツリー

複数の色相プリセットを用意する場合に有効:

1. **複数のアニメーションクリップを作成**
   - Clip 1: `_MainTexHSVG.x = 0.0` (元の色)
   - Clip 2: `_MainTexHSVG.x = 0.25` (90度回転)
   - Clip 3: `_MainTexHSVG.x = 0.5` (180度回転)
   - Clip 4: `_MainTexHSVG.x = 0.75` (270度回転)

2. **1D Blend Treeを作成**
   - Parameter: HueRotation
   - 各クリップを0.0, 0.25, 0.5, 0.75の位置に配置

## VRChatでのマテリアルプロパティアニメーション

### プロパティパスの形式

```
material._PropertyName
material._PropertyName.x  (Vector成分)
material._PropertyName.y
material._PropertyName.z
material._PropertyName.w
```

### Animation Windowでの録画

1. **Window > Animation > Animation**を開く
2. 対象オブジェクトを選択
3. 録画ボタン（赤い丸）をクリック
4. マテリアルプロパティの値を変更
5. 自動的にキーフレームが追加される

### 手動でのプロパティ追加

1. Animation Windowで「Add Property」をクリック
2. 対象のRenderer（SkinnedMeshRenderer等）を展開
3. `Material._MainTexHSVG.x`を選択

### 複数マテリアルの対応

同じメッシュ上の複数マテリアルを同時にアニメーション:

```
SkinnedMeshRenderer.material[0]._MainTexHSVG.x
SkinnedMeshRenderer.material[1]._MainTexHSVG.x
SkinnedMeshRenderer.material[2]._MainTexHSVG.x
```

## RGB⇔HSV変換

### liltoonの場合: 変換不要

liltoonシェーダーは内部で自動的にRGB⇔HSV変換を処理します:
- `_MainTexHSVG`パラメータがシェーダー内で自動的にRGBカラーにHSV調整を適用
- アニメーションでは`_MainTexHSVG`の値を直接変更するだけ
- シェーダーコード内でHSV変換処理が実装済み

### カスタムシェーダーでの変換

一般的なUnityシェーダーでHSV変換が必要な場合:

**RGB to HSV (HLSL):**
```hlsl
float3 RGBtoHSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}
```

**HSV to RGB (HLSL):**
```hlsl
float3 HSVtoRGB(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}
```

**Unity C# (Color.HSVToRGB):**
```csharp
// H: 0.0 ~ 1.0
// S: 0.0 ~ 1.0
// V: 0.0 ~ 1.0
Color color = Color.HSVToRGB(h, s, v);
```

## Radial Puppetの値マッピング

### 基本マッピング

```
Radial Puppetの回転: 0.0 ～ 1.0
対応する色相: 0° ～ 360°

値0.0（12時方向開始）→ 元の色 (0°)
値0.25 → 90度回転 (黄色→青など)
値0.5 → 180度回転 (補色)
値0.75 → 270度回転
値1.0 → 360度 (元の色に戻る)
```

### 色相環

```
0.0 (0°)   → Red
0.083 (30°) → Orange
0.167 (60°) → Yellow
0.25 (90°)  → Yellow-Green
0.333 (120°) → Green
0.417 (150°) → Cyan-Green
0.5 (180°)  → Cyan
0.583 (210°) → Blue
0.667 (240°) → Purple
0.75 (270°)  → Magenta
0.833 (300°) → Red-Purple
1.0 (360°)  → Red (戻る)
```

## NDMFでの実装例

### アニメーションクリップ生成

```csharp
InPhase(BuildPhase.Generating).Run("Generate Hue Animation", ctx =>
{
    var component = ctx.AvatarRootObject.GetComponentInChildren<ColorHueAnimator>();
    if (component == null) return;

    // VirtualClipを作成
    var clip = VirtualClip.Create($"{component.name}_HueShift");
    clip.FrameRate = 60f;

    // 相対パスを取得
    var path = GetRelativePath(ctx.AvatarRootTransform, component.targetRenderer.transform);

    // EditorCurveBindingを作成
    var binding = new EditorCurveBinding
    {
        path = path,
        type = typeof(Renderer),
        propertyName = "material._MainTexHSVG.x"
    };

    // 線形カーブを作成
    var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    // Tangentを明示的にLinearに設定
    var keys = curve.keys;
    for (int i = 0; i < keys.Length; i++)
    {
        keys[i].inTangent = 1f;
        keys[i].outTangent = 1f;
        keys[i].weightedMode = WeightedMode.None;
    }
    curve.keys = keys;

    // カーブを設定
    clip.SetFloatCurve(binding, curve);

    // アセットとして保存
    ctx.AssetSaver.SaveAsset(clip.Clip);

    // コンポーネントに保存
    component.generatedClip = clip.Clip;
});
```

### Motion Time対応ステートの追加

```csharp
InPhase(BuildPhase.Transforming).Run("Setup Motion Time State", ctx =>
{
    var vccContext = ctx.ActivateExtensionContext<VirtualControllerContext>();

    try
    {
        var fxController = vccContext[VRCAvatarDescriptor.AnimLayerType.FX];

        // 新しいレイヤーを追加
        var layers = fxController.Controller.layers.ToList();
        var newLayer = new AnimatorControllerLayer
        {
            name = "HueShift",
            defaultWeight = 1f,
            stateMachine = new AnimatorStateMachine()
        };
        layers.Add(newLayer);
        fxController.Controller.layers = layers.ToArray();

        // ステートを追加
        var stateMachine = newLayer.stateMachine;
        var state = stateMachine.AddState("HueRotation");
        state.motion = generatedClip;
        state.writeDefaultValues = false;
        state.timeParameterActive = true;  // Motion Time有効
        state.timeParameter = "HueRotation";
    }
    finally
    {
        ctx.DeactivateExtensionContext<VirtualControllerContext>();
    }
});
```

## パフォーマンス考慮

### liltoonバリアント

- **liltoonフルバージョン**: HSV調整機能あり
- **liltoon Lite**: HSV調整機能が削除されている場合あり
- **liltoon Multi**: 複数のバリエーション対応

**確認方法:**
```csharp
bool hasHSVG = material.HasProperty("_MainTexHSVG");
```

### Shader Settings

不要な場合はShader Settingsで機能を無効化してパフォーマンス向上:
1. liltoonのInspectorを開く
2. Shader Settingセクション
3. 不要な機能をオフにしてシェーダーを最適化

## トラブルシューティング

### 色相が変化しない

1. **マテリアルロック確認**
   - liltoonマテリアルがロックされているか
   - `Animated (when locked)`に設定されているか

2. **プロパティパス確認**
   - `material._MainTexHSVG.x`が正しいか
   - パスがアバタールートからの相対パスか

3. **Tangent確認**
   - キーフレームのTangentがLinearに設定されているか

### 色相が均等に変化しない

- **原因**: TangentがAutoやClampedになっている
- **解決**: Dope Sheetで両方のTangentをLinearに設定

### Motion Timeが機能しない

1. **ステート設定確認**
   - `timeParameterActive`がtrueか
   - `timeParameter`が正しいパラメータ名か

2. **パラメータタイプ確認**
   - AnimatorパラメータがFloat型か

## VRCFuryでの簡易実装

VRCFuryを使用すると、GUIベースで簡単に色相シフトを実装可能:

1. VRCFuryコンポーネントを追加
2. "Add Toggle"を選択
3. "Hue Shift"オプションを有効化
4. 自動的にアニメーションとメニューを生成

## 参考資料

### liltoon公式
- [lilToon GitHub](https://github.com/lilxyzw/lilToon)
- [lilToon公式ドキュメント](https://lilxyzw.github.io/lilToon/)

### VRChat
- [Radial Puppets (Hue Shifts and more) - VRC School](https://vrc.school/docs/Avatars/Radial-Puppets/)
- [VRChat Animator Parameters](https://creators.vrchat.com/avatars/animator-parameters/)
- [VRChat Expressions Menu and Controls](https://creators.vrchat.com/avatars/expression-menu-and-controls/)

### Unity
- [Unity Animation Curves](https://docs.unity3d.com/Manual/animeditor-AnimationCurves.html)
- [Unity Color.HSVToRGB](https://docs.unity3d.com/ScriptReference/Color.HSVToRGB.html)

### 実装例
- [GitHub - koturn/LilHueShift](https://github.com/koturn/LilHueShift)
- [Easy Avatar Hue Shift - VRCLibrary](https://vrclibrary.com/wiki/books/non-destructive-avatar-editing/page/easy-avatar-hue-shift-vrcfury)
