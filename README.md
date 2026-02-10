# Color Animation Creator

VRChat向けのModular Avatarコンポーネント。Radial Menuでliltoonシェーダーの任意の色プロパティの色相を変更できます。

## 特徴

- **任意の色プロパティに対応**: `_EmissionColor`、`_MatCapColor`、`_OutlineColor`など、liltoonの任意のColorプロパティの色相を変更可能
- **非破壊的**: NDMFを使用した非破壊的なビルドシステム
- **自動化**: アニメーションクリップの生成、Animatorへの追加、メニュー設定を全自動化
- **カスタマイズ可能**: サンプル数、パラメータ名、メニュー名などを自由に設定可能

## 必要な依存関係

- **Unity**: 2019.4以上（VRChat SDKの要件に準拠）
- **VRChat SDK3 - Avatars**: 最新版
- **NDMF (Non-Destructive Modular Framework)**: 1.4.0以上
- **Modular Avatar**: 1.9.0以上
- **liltoon**: 1.3.0以上（色相変更対象のシェーダー）

## インストール

1. 上記の依存関係をすべてインストール
2. このパッケージをUnityプロジェクトにインポート
3. `Assets/shiosyakeyakini/ColorAnimationCreator` フォルダが作成されます

## 使用方法

### 基本的な使い方

1. **コンポーネントの追加**
   - アバターのヒエラルキー内の任意のGameObjectを選択
   - `Add Component` → `Modular Avatar` → `Color Hue Animator` を追加

2. **ターゲット設定**
   - `Target Renderer`: 色相を変更したいRendererを指定
   - `Material Index`: 対象マテリアルのインデックス（0から始まる）

3. **色プロパティ設定**
   - `Color Property Name`: 変更したい色プロパティ名を入力
     - 例: `_EmissionColor`、`_MatCapColor`、`_OutlineColor`、`_RimColor`など
   - `Hue Steps`: 色相のサンプル数（デフォルト: 36）
     - 多いほど滑らかですが、ファイルサイズが増加します
     - 推奨値: 24～36

4. **メニュー設定**
   - `Menu Name`: VRChatメニューに表示される名前
   - `Parameter Name`: Animatorパラメータ名（ユニークな名前を推奨）
   - `Saved`: パラメータをワールド間で保存するか
   - `Synced`: パラメータを他プレイヤーと同期するか
   - `Menu Icon`: メニューアイコン（オプション）

5. **ビルド**
   - NDMFビルドを実行（アバターをアップロード時に自動実行されます）
   - アニメーションクリップが自動生成され、AnimatorとMenuに追加されます

### よくある使用例

#### エミッションカラーの色相変更

```
Component Settings:
├── Target Renderer: Body (SkinnedMeshRenderer)
├── Material Index: 0
├── Color Property Name: _EmissionColor
├── Hue Steps: 36
├── Menu Name: エミッション色相
├── Parameter Name: EmissionHue
├── Saved: ✓
└── Synced: ✓
```

#### MatCapカラーの色相変更

```
Component Settings:
├── Target Renderer: Hair (MeshRenderer)
├── Material Index: 1
├── Color Property Name: _MatCapColor
├── Hue Steps: 24
├── Menu Name: MatCap色
├── Parameter Name: MatCapHue
├── Saved: ✓
└── Synced: ✓
```

## トラブルシューティング

### エラー: "Material does not have property 'XXX'"

**原因**: 指定したプロパティ名がマテリアルに存在しません。

**解決策**:
1. マテリアルのInspectorで、プロパティ名を確認
2. liltoonの内部プロパティ名は、表示名と異なる場合があります
   - 例: 表示名「Emission Color」→ 内部名 `_EmissionColor`
3. シェーダーのソースコードで正確なプロパティ名を確認

### エラー: "Property 'XXX' is not a Color type"

**原因**: 指定したプロパティがColor型ではありません。

**解決策**:
1. プロパティ名が正しいか確認
2. Color型のプロパティのみ指定可能です（Float、Vector4などは非対応）

### 色相が正しく変化しない

**原因**: サンプル数が少なすぎる、または元の色が無彩色（グレー）です。

**解決策**:
1. `Hue Steps` を増やす（推奨: 36）
2. 元の色に彩度があることを確認（S > 0）
3. 白や黒、グレーは色相変更の効果が見えません

### メニューに表示されない

**原因**: Expression Menuが満杯、またはパラメータ名が重複しています。

**解決策**:
1. Expression Menuの容量を確認（最大8個）
2. パラメータ名が他のコンポーネントと重複していないか確認
3. Console ログでエラーメッセージを確認

## 技術的な制限事項

### liltoonシェーダーの制約

- **HSVGパラメータの制限**: `_MainTexHSVG`と`_OutlineTexHSVG`のみHSVG調整に対応
- **個別色プロパティ**: `_EmissionColor`、`_MatCapColor`などにはHSVGパラメータが存在しないため、RGB直接アニメーション方式を使用

### RGB補間の制約

- UnityのAnimation CurveはRGB色空間で線形補間します
- 色相環は円形なので、RGB補間では完全に正確な色相回転にはなりません
- **対策**: 十分な数のキーフレーム（24～36）で近似

### VRChatの制約

- **パラメータメモリ**: Float = 8ビット
- **メニュー容量**: 最大8アイテム/メニュー
- **同期頻度**: Synced Floatは0.1秒ごとに同期

## 高度な使い方

### 複数のプロパティを同時に変更

1つのアバターに複数の `ColorHueAnimator` を配置できます。それぞれ異なるプロパティを変更可能です。

```
Avatar Root
├── Emitter (ColorHueAnimator)
│   └── Color Property: _EmissionColor
└── MatCap (ColorHueAnimator)
    └── Color Property: _MatCapColor
```

### サンプル数の調整

| サンプル数 | 品質 | ファイルサイズ | 推奨用途 |
|-----------|------|---------------|---------|
| 12 | 低 | 小 | テスト用 |
| 24 | 中 | 中 | 一般的な使用 |
| 36 | 高 | 中 | 推奨（デフォルト） |
| 72 | 最高 | 大 | 高品質が必要な場合 |

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## 謝辞

このプロジェクトは以下のツール・フレームワークを使用しています:

- **NDMF**: [bd_](https://github.com/bdunderscore)氏による非破壊的ビルドフレームワーク
- **Modular Avatar**: [bd_](https://github.com/bdunderscore)氏によるアバター編集フレームワーク
- **liltoon**: [lilxyzw](https://github.com/lilxyzw)氏による高機能シェーダー
- **VRChat SDK**: VRChat Inc.

## 技術資料

詳細な技術情報については、以下のドキュメントを参照してください:

- [CLAUDE.md](CLAUDE.md) - 実装計画と設計ドキュメント
- [docs/NDMF.md](docs/NDMF.md) - NDMFビルドシステム詳細ガイド
- [docs/ModularAvatar.md](docs/ModularAvatar.md) - Modular Avatarメニューシステムガイド
- [docs/liltoon.md](docs/liltoon.md) - liltoon色相変更ガイド
- [docs/liltoon-rgb-animation.md](docs/liltoon-rgb-animation.md) - RGB直接アニメーション実装ガイド

## サポート

問題が発生した場合は、以下を確認してください:

1. Unity Consoleでエラーメッセージを確認
2. コンポーネントのインスペクターで検証エラーを確認
3. 依存関係が正しくインストールされているか確認

## 更新履歴

### Version 1.0.0 (2026-02-05)

- 初回リリース
- 基本的な色相変更機能の実装
- RGB直接アニメーション方式による任意の色プロパティ対応
- カスタムインスペクターによる検証とエラー表示
- 自動メニュー・パラメータ追加

---

Developed by ShioShakeYakiNi
