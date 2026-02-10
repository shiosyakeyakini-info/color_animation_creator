# Color Animation Creator - 実装計画

## プロジェクト概要

このプロジェクトは、VRChat向けのModular Avatar (MA) Menu Itemとして動作するコンポーネントを作成し、Radial MenuでliltoonシェーダーのsColorパラメータの色相を変更できるようにするものです。

## 技術スタック

- **NDMF (Non-Destructive Modular Framework)**: 非破壊的なビルドシステム
- **Modular Avatar**: メニューシステムとアバター編集フレームワーク
- **liltoon**: 色相変更対象のシェーダー
- **Unity Animator**: アニメーション生成とパラメータ制御

## 主要な発見事項

### 1. 色相変更は可能だが実装方法に制約あり

#### liltoonのHSVGパラメータの制限
- **_MainTexHSVG**: メインテクスチャの色調整に対応（`_Color`プロパティ）
- **_OutlineTexHSVG**: アウトラインテクスチャの色調整に対応

**重要**: `_EmissionColor`、`_MatCapColor`、`_OutlineColor`、`_RimColor`、`_BacklightColor`などの個別色プロパティには、専用のHSVGパラメータが存在しません。

#### 実装アプローチ

任意の色プロパティの色相を変更するには、以下の方法を採用:

1. **ビルド時にRGB→HSV変換を実行**
   - ベースとなる色を取得（ユーザー指定のマテリアルプロパティから）
   - `Color.RGBToHSV()`でHSV値に変換
   - 彩度(S)と明度(V)を固定

2. **色相環をサンプリング**
   - 0°～360°の範囲を均等に分割（例: 10度刻みで36サンプル）
   - 各サンプルポイントでHSV→RGB変換を実行
   - RGB値を3つのアニメーションカーブ（R、G、B）に記録

3. **Motion Timeで制御**
   - 1つのアニメーションクリップに全サンプルポイントを含める
   - Animator LayerでMotion Time機能を有効化
   - Radial PuppetのFloatパラメータをMotion Time Parameterに指定

### 2. RGB直接アニメーションの技術的課題

**Animation Curveの制約:**
- Unity標準のAnimation Curveは、RGBAチャンネルを個別にアニメーション
- 色相(Hue)の概念はRGB色空間では線形ではない
- キーフレーム間の補間は線形RGB空間で行われる

**解決策:**
- 十分な数のキーフレームを配置して色相環を近似
- サンプル数が多いほど滑らかな色相変化が可能（推奨: 24～36サンプル）

### 3. NDMFによる自動化

NDMFのビルドフェーズを使用して以下を自動化:
- **Generating**: アニメーションクリップの生成
- **Transforming**: AnimatorControllerへの追加、メニュー・パラメータの設定

## アーキテクチャ設計

### コンポーネント構造

```
ColorHueAnimator (MonoBehaviour, ExecuteInEditMode)
├── targetRenderer: Renderer (色相変更対象)
├── materialIndex: int (対象マテリアルのインデックス)
├── colorPropertyName: string (対象の色プロパティ名、例: "_EmissionColor")
├── hueSteps: int (色相サンプル数、デフォルト: 36)
├── menuName: string (メニュー表示名)
├── parameterName: string (Animatorパラメータ名)
├── saved: bool (パラメータを保存するか)
├── synced: bool (パラメータを同期するか)
└── menuIcon: Texture2D (メニューアイコン)
```

**注意**: `colorPropertyName`には、liltoonの任意の色プロパティを指定可能:
- `_Color` (メインカラー)
- `_EmissionColor` (エミッション)
- `_MatCapColor` (MatCap)
- `_OutlineColor` (アウトライン)
- `_RimColor` (リムライト)
- `_BacklightColor` (バックライト)
- など

### NDMFプラグイン構造

```
ColorAnimationCreatorPlugin : Plugin<ColorAnimationCreatorPlugin>
├── Generating Phase
│   └── GenerateHueAnimationClips
│       - VirtualClipで色相アニメーションを作成
│       - _MainTexHSVG.xを0→1に線形変化
│       - AssetContainerに保存
│
└── Transforming Phase
    └── SetupAnimatorAndMenu
        - VirtualControllerContextでFXレイヤーを取得
        - Motion Time対応のステートを追加
        - Expression Parametersに追加
        - Expression Menuに追加
```

### ビルドフロー

```
1. ユーザーがColorHueAnimatorコンポーネントを配置
   - 対象Renderer、マテリアルインデックス、色プロパティ名を指定
   ↓
2. [Generating] NDMFがアニメーションクリップを生成
   a. ベース色を取得: material.GetColor(colorPropertyName)
   b. RGB→HSV変換: Color.RGBToHSV(baseColor, out h, out s, out v)
   c. 色相環をサンプリング (0° ～ 360°を均等分割)
   d. 各サンプルでHSV→RGB変換: Color.HSVToRGB((h + i/steps) % 1.0, s, v)
   e. RGBカーブを生成:
      - material.{colorPropertyName}.r: キーフレーム配列
      - material.{colorPropertyName}.g: キーフレーム配列
      - material.{colorPropertyName}.b: キーフレーム配列
   f. アニメーションクリップを保存
   ↓
3. [Transforming] FX AnimatorControllerを設定
   - 新レイヤー追加 (Weight: 1.0)
   - ステート作成 (Motion Time有効)
   - パラメータ追加 (Float型)
   ↓
4. [Transforming] Expression Menu/Parametersを設定
   - Radial Puppet追加
   - Floatパラメータ登録
   ↓
5. ビルド完了
```

## 実装計画

### Phase 1: 基本構造の実装

**ファイル構成:**
```
ColorAnimationCreator/
├── Runtime/
│   ├── ColorHueAnimator.cs           # ユーザー向けコンポーネント
│   └── ColorAnimationCreator.Runtime.asmdef
├── Editor/
│   ├── ColorAnimationCreatorPlugin.cs # NDMFプラグイン
│   ├── ColorHueAnimatorEditor.cs     # カスタムインスペクター
│   └── ColorAnimationCreator.Editor.asmdef
└── CLAUDE.md
```

**アセンブリ定義:**
- **Runtime.asmdef**: VRCSDKBase、Modular Avatar Runtime参照
- **Editor.asmdef**: NDMF、Modular Avatar Editor、VRCSDKBase Editor参照

### Phase 2: コンポーネント実装

**ColorHueAnimator.cs (Runtime):**
```csharp
[AddComponentMenu("Modular Avatar/Color Hue Animator")]
[DisallowMultipleComponent]
public class ColorHueAnimator : MonoBehaviour
{
    public Renderer targetRenderer;
    public int materialIndex = 0;

    [Tooltip("対象の色プロパティ名（例: _EmissionColor, _MatCapColor, _OutlineColor）")]
    public string colorPropertyName = "_EmissionColor";

    [Tooltip("色相のサンプル数（多いほど滑らか、推奨: 24～36）")]
    [Range(8, 72)]
    public int hueSteps = 36;

    public string menuName = "色相変更";
    public string parameterName = "HueRotation";
    public bool saved = true;
    public bool synced = true;
    public Texture2D menuIcon;

    // 検証メソッド
    public bool Validate()
    {
        if (targetRenderer == null) return false;
        if (materialIndex >= targetRenderer.sharedMaterials.Length) return false;
        var mat = targetRenderer.sharedMaterials[materialIndex];
        if (mat == null) return false;

        // 色プロパティの存在確認
        if (!mat.HasProperty(colorPropertyName)) return false;

        // プロパティがColor型かチェック
        var propertyType = mat.shader.GetPropertyType(
            mat.shader.FindPropertyIndex(colorPropertyName)
        );
        return propertyType == UnityEngine.Rendering.ShaderPropertyType.Color;
    }

    // ベース色を取得
    public Color GetBaseColor()
    {
        if (!Validate()) return Color.white;
        var mat = targetRenderer.sharedMaterials[materialIndex];
        return mat.GetColor(colorPropertyName);
    }
}
```

### Phase 3: NDMFプラグイン実装

**ColorAnimationCreatorPlugin.cs (Editor):**
```csharp
[assembly: ExportsPlugin(typeof(ColorAnimationCreatorPlugin))]

public class ColorAnimationCreatorPlugin : Plugin<ColorAnimationCreatorPlugin>
{
    public override string QualifiedName =>
        "com.shiosyakeyakini.coloranimationcreator";
    public override string DisplayName =>
        "Color Animation Creator";

    protected override void Configure()
    {
        InPhase(BuildPhase.Generating)
            .Run("Generate Hue Animation Clips", GenerateHueAnimationClips);

        InPhase(BuildPhase.Transforming)
            .AfterPlugin("nadena.dev.modular-avatar")
            .Run("Setup Animator and Menu", SetupAnimatorAndMenu);
    }

    private void GenerateHueAnimationClips(BuildContext ctx) { }
    private void SetupAnimatorAndMenu(BuildContext ctx) { }
}
```

**主要な実装ポイント:**

1. **アニメーションクリップ生成 (Generating)**
   - VirtualClipを使用
   - EditorCurveBindingでプロパティパスを指定（R、G、B各チャンネル）
   - ベース色からHSV値を抽出
   - 色相を0°～360°の範囲でサンプリング
   - 各サンプルポイントでHSV→RGB変換を実行
   - RGB各チャンネルのキーフレーム配列を生成
   - TangentをLinearに設定して均等な補間を実現

2. **Animatorセットアップ (Transforming)**
   - VirtualControllerContextを有効化
   - FXレイヤーに新レイヤーを追加
   - Motion TimeをサポートするステートをWDオフで作成
   - AnimatorControllerParameterを追加 (Float型)

3. **メニュー/パラメータ追加 (Transforming)**
   - VRCExpressionParametersに追加 (Float, Synced設定)
   - VRCExpressionsMenuにRadial Puppet追加
   - パラメータ名の衝突チェック

### Phase 4: エディター拡張

**ColorHueAnimatorEditor.cs:**
- カスタムインスペクターUI
- マテリアル検証とエラー表示
- liltoonシェーダー検出
- プレビュー機能（可能であれば）

### Phase 5: テストとドキュメント

**テストケース:**
1. 単一マテリアルの色相変更
2. 複数のColorHueAnimatorコンポーネント共存
3. パラメータ名の重複処理
4. メニュー容量制限の確認

**ユーザードキュメント:**
- README.md: セットアップ手順
- 使用例とトラブルシューティング
- 制限事項の明記

## 技術的な注意点

### liltoonシェーダーの制約

- **HSVGパラメータの制限**:
  - `_MainTexHSVG`と`_OutlineTexHSVG`のみHSVG調整対応
  - `_EmissionColor`、`_MatCapColor`等の個別色プロパティにはHSVGパラメータなし
- **対応確認**: 指定された色プロパティの存在とColor型をチェック
- **RGB直接アニメーション**: 色プロパティ（Color型）のR、G、B各チャンネルを直接アニメーション

### VRChatの制約

- **パラメータメモリ**: Float = 8ビット
- **メニュー容量**: 最大8アイテム/メニュー（自動分割対応）
- **同期頻度**: Synced Floatは0.1秒ごと

### NDMFの注意点

- **VirtualControllerContext**: try-finallyで適切にDeactivate
- **アセット保存**: AssetSaverを使用して永続化
- **パス解決**: GetRelativePathでルートからの相対パスを取得
- **実行順序**: Modular Avatar後に実行（メニューシステム利用のため）

## 拡張可能性

### 将来的な機能追加

1. **彩度・明度の制御**
   - 色相と同様のアプローチでHSV変換を使用
   - 2軸パペットまたは追加のラジアルパペット
   - S（彩度）とV（明度）のパラメータ追加

2. **複数マテリアル対応**
   - 配列で複数のマテリアルを指定
   - 一括アニメーション生成

3. **プリセット機能**
   - よく使う色相をボタンで切り替え
   - トグルとラジアルの併用

4. **アニメーション合成**
   - 既存のアニメーションとのブレンド
   - レイヤーウェイト調整

## 参考資料

詳細な技術資料は以下のファイルを参照:
- [docs/NDMF.md](docs/NDMF.md) - NDMF詳細ガイド
- [docs/ModularAvatar.md](docs/ModularAvatar.md) - MA Menu システムガイド
- [docs/liltoon.md](docs/liltoon.md) - liltoon色相変更ガイド

## 実装スケジュール

1. **Phase 1-2** (基本構造): プロジェクトセットアップとコンポーネント実装
2. **Phase 3** (NDMFプラグイン): コア機能の実装
3. **Phase 4** (エディター拡張): ユーザビリティの向上
4. **Phase 5** (テスト): 動作検証とドキュメント作成

---

最終更新: 2026-02-05
