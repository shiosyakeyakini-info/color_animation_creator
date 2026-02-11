# Color Animation Creator - 実装ガイド

## プロジェクト概要

VRChat向けのNDMFプラグイン。liltoonシェーダーの任意の色プロパティ（Color型）およびHSVGプロパティ（Vector4型: `_MainTexHSVG`, `_OutlineTexHSVG`）に対して、色相・彩度・明度をRadial Menuで独立制御するアニメーションをビルド時に自動生成する。

## 技術スタック

- **NDMF (Non-Destructive Modular Framework)**: 非破壊的なビルドシステム
- **Modular Avatar**: メニューシステムとアバター編集フレームワーク
- **liltoon**: 色変更対象のシェーダー
- **Unity Animator / Blend Tree**: ネストされた1D Blend Treeによるパラメータ制御

## アーキテクチャ

### データ構造

```
MaterialColorTarget (Serializable)
├── renderer: Renderer
├── materialIndex: int
└── colorPropertyName: string (例: "_EmissionColor")

ColorHSVAnimator (MonoBehaviour, IEditorOnly)
├── targets: List<MaterialColorTarget>  ← 複数マテリアルを同時制御
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

### 複数マテリアル対応

- 1つのコンポーネントで複数の Renderer/マテリアルスロット/色プロパティを同時に制御
- Color型とHSVG型のターゲットを同一コンポーネント内で混在可能
- 全ターゲットが同じ HSV パラメータで連動して色変更される
- 各ターゲットは独自の baseH/baseS/baseV を持ち、色相オフセットは各自の基準から適用
- デフォルトパラメータ値: 全軸 0.5 で統一（Color: 3-child centered BT, HSVG: 線形マッピング中央）

### 旧データからのマイグレーション

旧フィールド（`targetRenderer`, `materialIndex`, `colorPropertyName`）は `[SerializeField, HideInInspector]` として保持。
エディタの `OnEnable()` で `_migrated` フラグを確認し、未移行の場合は `targets` リストへ自動移行。

### HSV→RGB の数学的分解

HSV→RGB変換は以下のように線形分解可能:

```
HSVToRGB(H, S, V) = V × lerp( (1,1,1), PureHue(H), S )
```

HSVToRGB は S, V に対して双線形（bilinear）であるため、3-child の区分線形 BlendTree で正確に再現できる。

全軸 **param 0.5 = 元の色（変化なし）** で統一。Color/HSVG 混在時もパラメータを共有可能:

```
1D BT (Value param, 0→1) — 3-child centered
  ├── threshold 0:   黒クリップ (0,0,0)
  ├── threshold 0.5: 1D BT (Saturation, baseV レベル) — 3-child centered
  │    ├── threshold 0:   グレー (baseV, baseV, baseV)
  │    ├── threshold 0.5: Hue BT (baseS, baseV) ← 元の色
  │    └── threshold 1:   Hue BT (fullS=1, baseV)
  └── threshold 1:   1D BT (Saturation, fullV レベル) — 3-child centered
       ├── threshold 0:   白 (1,1,1)
       ├── threshold 0.5: Hue BT (baseS, fullV=1)
       └── threshold 1:   Hue BT (fullS=1, fullV=1)

Hue BT (中央寄せ, param 0→1):
  ├── threshold 0/N:   HSVToRGB(baseH - 180°, clipS, clipV)
  ├── threshold 0.5:   HSVToRGB(baseH + 0°, clipS, clipV) ← 元の色相
  │   ...
  └── threshold N/N:   HSVToRGB(baseH + 180°, clipS, clipV)
```

近似ではなく厳密解。色相サンプリング間のRGB補間のみが近似要素。
Sat/Val 有効時はクリップ数が最大 4×(hueSteps+1) + 3 になる。

### HSVGプロパティの独立レイヤー方式

`_MainTexHSVG` / `_OutlineTexHSVG` はVector4型で、各コンポーネント（.x=Hue, .y=Saturation, .z=Value, .w=Gamma）が独立している。RGB用のネスト構造ではクロス補間問題が発生するため、軸ごとに独立したAnimatorレイヤーを使用:

```
AnimatorController
├── Layer "HSVG_{hueParam}"  ← Hue (.x): -0.5~0.5
│   └── 1D BT (2 children: clip(.x=-0.5), clip(.x=0.5))
├── Layer "HSVG_{satParam}"  ← Saturation (.y): 0.0~2.0
│   └── 1D BT (2 children: clip(.y=0), clip(.y=2))
└── Layer "HSVG_{valParam}"  ← Value (.z): 0.0~2.0
    └── 1D BT (2 children: clip(.z=0), clip(.z=2))
```

- 各レイヤーは `writeDefaultValues = false` で、未制御コンポーネント（.w Gamma等）はマテリアルデフォルトを保持
- 線形補間が正確なため、色相も2クリップで十分（RGB方式の37+クリップ不要）
- デフォルト値: 全軸 0.5（Color/HSVG 共通。param 0.5 = 変化なし）
- ColorターゲットとHSVGターゲットは同一コンポーネント内で混在可能（別レイヤーで共存）
- EditorCurveBinding: Color型は `.r/.g/.b`、Vector4型は `.x/.y/.z/.w`

### クリップ内の色の決定ルール

3-child centered BT 方式により、各軸の param 0.5 = 元の色:
- 彩度 BT: threshold 0 (gray), 0.5 (baseS hue motion), 1.0 (fullS=1 hue motion)
- 明度 BT: threshold 0 (black), 0.5 (baseV sat motion), 1.0 (fullV=1 sat motion)
- 色相 BT: 中央寄せ（param 0.5 = baseH、-180°〜+180°の範囲でサンプリング）

外側の軸が無効な場合、内側クリップに baseS/baseV をベイク。有効な軸の組み合わせで内部 Motion 数が変わる:
- Hue のみ: 1 hue BT
- Sat のみ: 3-child BT (gray, baseClip, fullSatClip)
- Hue + Sat: 3-child BT 内に 2 hue BTs
- Hue + Sat + Val: 2 × 3-child BT 内に 4 hue BTs

各クリップは `CreateMultiTargetColorClip()` で生成され、全ターゲット分の AnimationCurve を1つのクリップに含める。

### デフォルトパラメータ値

全軸 **0.5** で統一（Color / HSVG 共通）:
- param 0.5 = 変化なし（元の色を再現）
- Color: 3-child BT の中央ポイントで元の baseH/baseS/baseV を再現
- HSVG: param 0.5 → Hue=0, Sat=1.0, Val=1.0（liltoon デフォルト）

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
│   ├── ColorHSVAnimator.cs                # MaterialColorTarget + ColorHSVAnimator
│   └── ColorAnimationCreator.Runtime.asmdef
├── Editor/
│   ├── ColorAnimationCreatorPlugin.cs     # NDMFプラグイン（複数ターゲット対応Blend Tree生成）
│   ├── ColorHSVAnimatorEditor.cs          # カスタムインスペクター（ReorderableList UI）
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

- `_MainTexHSVG`/`_OutlineTexHSVG`: Vector4型のHSVG調整プロパティ（独立レイヤー方式で対応）
  - X: Hue (-0.5 ~ 0.5 = -180° ~ +180°, 0.0 = 変化なし)
  - Y: Saturation (0.0 ~ 2.0, 1.0 = 変化なし)
  - Z: Value (0.0 ~ 2.0, 1.0 = 変化なし)
  - W: Gamma (0.0 ~ 2.0, 未制御)
- `_EmissionColor`等にはHSVGパラメータがないためRGB直接アニメーションが必要
- Color型とVector4(HSVG)型の両方に対応

### VRChatの制約

- **パラメータメモリ**: Float = 8ビット（HSV全有効時 = 24ビット）
- **メニュー容量**: 最大8アイテム/メニュー
- **同期頻度**: Synced Floatは0.1秒ごと

### Blend Tree の注意点

- 各クリップは単一キーフレーム（ポーズクリップ）
- `useAutomaticThresholds = false` を必ず設定
- Blend Tree もアセットとして `AssetSaver.SaveAsset()` が必要

### エディタ UI

- IMGUI ベースの `ReorderableList` を使用（MA Material Setter 風）
- 各エントリ: Renderer / マテリアルスロット(Popup) / 色プロパティ(liltoonプリセット) / バリデーション
- ドラッグによる並び替え、+/- ボタンで追加・削除

## 参考資料

- [docs/NDMF.md](docs/NDMF.md) - NDMF詳細ガイド
- [docs/ModularAvatar.md](docs/ModularAvatar.md) - MA Menu システムガイド
- [docs/liltoon.md](docs/liltoon.md) - liltoon色相変更ガイド

---

最終更新: 2026-02-11
