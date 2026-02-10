# Color Animation Creator - 実装ガイド

## プロジェクト概要

VRChat向けのNDMFプラグイン。liltoonシェーダーの任意の色プロパティ（Color型）に対して、色相・彩度・明度をRadial Menuで独立制御するアニメーションをビルド時に自動生成する。

## 技術スタック

- **NDMF (Non-Destructive Modular Framework)**: 非破壊的なビルドシステム
- **Modular Avatar**: メニューシステムとアバター編集フレームワーク
- **liltoon**: 色変更対象のシェーダー
- **Unity Animator / Blend Tree**: ネストされた1D Blend Treeによるパラメータ制御

## アーキテクチャ

### コンポーネント: ColorHSVAnimator

```
ColorHSVAnimator (MonoBehaviour, IEditorOnly)
├── targetRenderer: Renderer
├── materialIndex: int
├── colorPropertyName: string (例: "_EmissionColor")
│
├── enableHue: bool (default: true)
├── hueSteps: int (8-72, default: 36)
├── hueParameterName: string
│
├── enableSaturation: bool (default: false)
├── saturationParameterName: string
│
├── enableValue: bool (default: false)
├── valueParameterName: string
│
├── saved: bool
├── synced: bool
└── menuIcon: Texture2D
```

### HSV→RGB の数学的分解

HSV→RGB変換は以下のように線形分解可能:

```
HSVToRGB(H, S, V) = V × lerp( (1,1,1), PureHue(H), S )
```

これをネストされた1D Blend Treeで厳密に再現:

```
1D BT (Value param, 0→1)
  ├── threshold 0: 黒クリップ (0,0,0)
  └── threshold 1: 1D BT (Saturation param, 0→1)
       ├── threshold 0: 白クリップ (1,1,1)
       └── threshold 1: 1D BT (Hue param, 0→1)
            ├── threshold 0/N: PureHue(baseH + 0°)
            ├── threshold 1/N: PureHue(baseH + 10°)
            │   ...
            └── threshold N/N: PureHue(baseH + 360°)
```

近似ではなく厳密解。色相サンプリング間のRGB補間のみが近似要素。

### クリップ内の色の決定ルール

外側レイヤーが担当する軸は内側クリップで1.0に固定:
- `clipS` = enableSaturation ? 1.0 : baseS
- `clipV` = enableValue ? 1.0 : baseV
- グレークリップ = enableValue ? (1,1,1) : (baseV, baseV, baseV)

### デフォルトパラメータ値

- Hue: 0 (オフセットなし → ベース色相)
- Saturation: baseS (ベース色の彩度 → 元の色を再現)
- Value: baseV (ベース色の明度 → 元の色を再現)

### メニュー構造の分岐

- 有効軸 1つ: 親GameObjectに直接 RadialPuppet
- 有効軸 2つ以上: 親をSubMenu化、子GameObjectに各RadialPuppet

### NDMFプラグイン実行順序

```
Transforming Phase:
  AfterPlugin("net.rs64.tex-trans-tool")   ← TTTのマテリアル変更後に色を取得
  BeforePlugin("nadena.dev.modular-avatar") ← MAがメニュー/パラメータを処理する前
```

## ファイル構成

```
ColorAnimationCreator/
├── Runtime/
│   ├── ColorHSVAnimator.cs                # ユーザー向けコンポーネント
│   └── ColorAnimationCreator.Runtime.asmdef
├── Editor/
│   ├── ColorAnimationCreatorPlugin.cs     # NDMFプラグイン（Blend Tree生成）
│   ├── ColorHSVAnimatorEditor.cs          # カスタムインスペクター
│   └── ColorAnimationCreator.Editor.asmdef
├── docs/
│   ├── NDMF.md
│   ├── ModularAvatar.md
│   ├── liltoon.md
│   └── liltoon-rgb-animation.md
└── CLAUDE.md
```

## 技術的な注意点

### liltoonシェーダーの制約

- `_MainTexHSVG`/`_OutlineTexHSVG`のみHSVG調整対応
- `_EmissionColor`等にはHSVGパラメータがないためRGB直接アニメーションが必要
- 任意のColor型プロパティに対応

### VRChatの制約

- **パラメータメモリ**: Float = 8ビット（HSV全有効時 = 24ビット）
- **メニュー容量**: 最大8アイテム/メニュー
- **同期頻度**: Synced Floatは0.1秒ごと

### Blend Tree の注意点

- 各クリップは単一キーフレーム（ポーズクリップ）
- `useAutomaticThresholds = false` を必ず設定
- Blend Tree もアセットとして `AssetSaver.SaveAsset()` が必要

## 参考資料

- [docs/NDMF.md](docs/NDMF.md) - NDMF詳細ガイド
- [docs/ModularAvatar.md](docs/ModularAvatar.md) - MA Menu システムガイド
- [docs/liltoon.md](docs/liltoon.md) - liltoon色相変更ガイド

---

最終更新: 2026-02-11
