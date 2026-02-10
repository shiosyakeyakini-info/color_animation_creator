# Modular Avatar メニューシステム詳細ガイド

## 概要

Modular Avatarは、UnityのヒエラルキーからVRChatのExpression Menuアイテムを定義できるシステムを提供します。従来のVRC Expressions Menu アセットを使うよりも便利で、直感的な編集が可能です。

## MA Menu Item

### 基本構造

MA Menu Itemは、以下の特徴を持つコンポーネントです:

**階層ベースの編集:**
- メニューアイテムの名前はGameObjectの名前から自動的に取得
- ヒエラルキーで直接ドラッグ&ドロップやリネームが可能
- リッチテキスト（太字、斜体、色など）もサポート

**自動パラメータ生成:**
- 未宣言のパラメータ名を指定すると、自動的にパラメータが作成される
- Synced（同期）とSaved（保存）の設定をチェックボックスで制御

**自動メニュー分割:**
- サブメニューのアイテム数がVRCメニューの最大数（8個）を超えると、自動的に「next」アイテムが作成されてメニューが分割される

### メニューバインディング（3つの方法）

Menu Itemを機能させるには、以下のいずれかの方法でバインドする必要があります:

#### 1. 親子関係

サブメニューモードに設定された別のMenu Itemの子として配置:

```
Parent Menu Item (Type: Sub Menu)
├── Child Menu Item 1
├── Child Menu Item 2
└── Child Menu Item 3
```

#### 2. Menu Installerとの同一GameObject

```
GameObject
├── MA Menu Installer
└── MA Menu Item
```

#### 3. Menu Groupの子

```
GameObject
├── MA Menu Installer
└── MA Menu Group
    ├── Menu Item 1
    ├── Menu Item 2
    └── Menu Item 3
```

**重要**: バインドされていないMenu Itemは効果がありません。

## コントロールタイプ

### Button（ボタン）

```csharp
Parameter Type: Bool, Float, Int
動作: ボタンを押している間、パラメータをTrue（Bool）または指定した値（Float/Int）に設定
      離すとFalse（Bool）または0に戻る
      短押しの場合は最低0.2秒間有効
```

**用途**: 一時的なアクション、ジェスチャーオーバーライド

### Toggle（トグル）

```csharp
Parameter Type: Bool, Float, Int
動作: 有効時にTrue（Bool）または指定値に設定、無効時にFalseまたは0に設定
```

**用途**: 最も一般的に使用されるコントロール、衣装の表示/非表示など

### Sub Menu（サブメニュー）

```csharp
Parameter Type: (Optional) Bool
動作: 別のメニューを開く
      パラメータを指定すると、サブメニュー内にいる間はToggleのように動作
```

**用途**: メニューの階層化、関連アイテムのグループ化

### RadialPuppet（ラジアルパペット）

```csharp
Parameter Type: Float
動作: 円形コントロールで、0から1までの任意の値を設定可能
      ユーザーがコントロールを閉じたときに値が保存される
```

**一般的な用途**: Hueシフト、ブレンドシェイプの細かい調整

**実装例:**
```
Control Type: Radial Puppet
Parameter Rotation: HueRotation (Float)
Label: 色相変更
Icon: (お好みのアイコン)
```

### TwoAxisPuppet（2軸パペット）

```csharp
Parameter Type: 2つのFloat (水平、垂直)
動作: 2Dコントロールで、水平・垂直位置に基づいて2つのパラメータを設定
      上下左右のスティック操作で-1から1の値を設定
```

**用途**: 2次元のコントロール、表情の細かい調整

### FourAxisPuppet（4軸パペット）

```csharp
Parameter Type: 4つのFloat (上、右、下、左)
動作: 2Dコントロールで、上下左右の位置に基づいて4つのパラメータを設定
```

**用途**: 複雑な2次元コントロール

## パラメータ管理

### パラメータタイプ

VRChatは3つの主要なパラメータタイプをサポート:

| タイプ | 範囲 | メモリコスト |
|--------|------|-------------|
| Bool | True/False | 1ビット |
| Int | 0～255 | 8ビット |
| Float | -1.0～1.0 | 8ビット |

### 同期タイプ

#### Playable Sync
```
更新頻度: 0.1～1秒ごと（必要に応じて）
用途: Button/Toggleなど、長時間続くアニメーション状態
特徴: オンデマンド更新で、精密な同期が不要なもの
```

#### IK Sync
```
更新頻度: 継続的に0.1秒ごと（10回/秒）
用途: Puppetコントロール、頻繁に変化する値
特徴: Float値をローカルで補間、高速/素早い動きに適している
```

#### Local (Unsynchronized)
```
更新頻度: なし
用途: ローカル設定、他のプレイヤーに見せる必要のない設定
特徴: メモリコストを消費しない
```

### パラメータ設定オプション

Menu Itemで自動作成されたパラメータには、以下の設定が可能:

- **Is Synced**: ネットワーク経由で他のプレイヤーと同期するか
- **Is Saved**: アバター切り替え時に値を保存・復元するか
- **Is Default**: このMenu Itemの値をパラメータのデフォルト値として使用するか

**注意**: 複数のMenu Itemを"Is Default"に設定すると、結果は未定義になります。

### パラメータメモリ管理

```
利用可能なExpression Parameters: 32～256ビット
- Bool: 1ビット
- Int: 8ビット
- Float: 8ビット
- Unsyncedパラメータ: メモリ制限にカウントされない
```

## プログラマティック実装

### NDMFでのメニュー追加

```csharp
InPhase(BuildPhase.Transforming).Run("Add Menu Items", ctx =>
{
    var descriptor = ctx.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
    if (descriptor == null) return;

    // Expression Menuを取得または作成
    var menu = descriptor.expressionsMenu;
    if (menu == null)
    {
        menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        menu.name = "GeneratedMenu";
        descriptor.expressionsMenu = menu;
        ctx.AssetSaver.SaveAsset(menu);
    }

    // Radial Puppetコントロールを追加
    var control = new VRCExpressionsMenu.Control
    {
        name = "色相変更",
        icon = iconTexture,
        type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
        subParameters = new[]
        {
            new VRCExpressionsMenu.Control.Parameter { name = "HueRotation" }
        }
    };

    var controls = menu.controls.ToList();
    controls.Add(control);
    menu.controls = controls;
});
```

### Expression Parametersの追加

```csharp
var parameters = descriptor.expressionParameters;
if (parameters == null)
{
    parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
    parameters.name = "GeneratedParameters";
    descriptor.expressionParameters = parameters;
    ctx.AssetSaver.SaveAsset(parameters);
}

var paramList = parameters.parameters.ToList();
if (!paramList.Any(p => p.name == "HueRotation"))
{
    paramList.Add(new VRCExpressionParameters.Parameter
    {
        name = "HueRotation",
        valueType = VRCExpressionParameters.ValueType.Float,
        defaultValue = 0f,
        saved = true,
        networkSynced = true
    });
    parameters.parameters = paramList.ToArray();
}
```

### サブメニューの作成

```csharp
// 親メニューにサブメニューコントロールを追加
var subMenuControl = new VRCExpressionsMenu.Control
{
    name = "カラー設定",
    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
    subMenu = subMenu  // 別途作成したVRCExpressionsMenu
};

parentMenu.controls.Add(subMenuControl);
```

## ベストプラクティス

### 1. 基本セットアップ

**単一コントロール/サブメニューの追加:**
```
GameObject
├── MA Menu Installer
└── MA Menu Item
```

**複数のコントロールをグループ化せずに追加:**
```
GameObject
├── MA Menu Installer
└── MA Menu Group
    ├── Menu Item 1
    ├── Menu Item 2
    └── Menu Item 3
```

### 2. 名前付けの推奨事項

- GameObjectの名前がそのままメニューアイテムの名前になる
- ヒエラルキーで直接リネーム可能
- リッチテキストを使用する場合は、カスタムラベルフィールドに自動切り替え

### 3. パラメータ管理

**パラメータの検索:**
- パラメータフィールドの横の矢印をクリックして名前で検索
- 親オブジェクトのMA Parametersコンポーネントも考慮される

**パラメータの再利用:**
- 既存のパラメータを複数のMenu Itemで共有可能
- 競合は自動的に解決される

**メモリ最適化:**
- Unsyncedパラメータはメモリ制限にカウントされない
- MA Parametersで同期ビットカウントを確認して最適化

### 4. プレハブ配布のアドバイス

**衣装クリエイター向け:**
- MA Menu Installer + MA Menu Itemを使用して、スタンドアロンアセットを作成
- ユーザーが簡単にアバターに追加できる
- 自動メニューインストール機能を活用

### 5. デバッグとテスト

- Unity Editorで「Default」チェックボックスをクリックしてプレビュー
- パラメータの同期・保存設定を確認
- メモリ使用量を監視（特にSynced parameters）

### 6. 避けるべき事項

- 複数のMenu Itemで「Is Default」を設定しない（結果が未定義）
- パラメータメモリ制限（32～256ビット）を超えないよう注意
- バインドされていないMenu Itemは効果がないため、必ずMenu Installer、Menu Group、または親Menu Itemと関連付ける

## Modular Avatar As Code

プログラマティックにメニューを構築するライブラリ:

```csharp
// Menu Itemの作成例
ma.EditMenuItem(menuItem)
    .Name("My Toggle")
    .Toggle(boolParameter)
    .WithIcon(iconTexture)
    .WithDefaultValue(true)
    .NotSaved();

// Radial Puppet
ma.EditMenuItem(menuItem)
    .Name("Radial Control")
    .RadialPuppet(floatParameter)
    .WithIcon(iconTexture);
```

## 一般的なパターン

### シンプルなオブジェクトトグル

1. アバターを右クリック → `Modular Avatar -> Create Toggle`
2. 自動的に3つのコンポーネントが作成:
   - Menu Item
   - Menu Installer
   - Object Toggle
3. Object Toggleでオブジェクトを追加
4. Menu Itemで"Default"ボックスをクリックしてプレビュー

### カスタムアニメーション付きトグル

1. Merge Animatorコンポーネントを追加
2. カスタムアニメーションクリップを設定
3. Menu Itemでパラメータを指定

### Multi-Toggle with Radial Puppet

1つのRadial Puppetで複数の相互排他的なオプションを切り替える:
- Float値の範囲で異なる状態を制御
- 例: 5種類の異なるトップスの切り替え

## 参考資料

### 公式ドキュメント
- [Menu Item | Modular Avatar](https://modular-avatar.nadena.dev/docs/reference/menu-item)
- [Edit menus | Modular Avatar](https://modular-avatar.nadena.dev/docs/tutorials/menu)
- [Menu Installer | Modular Avatar](https://modular-avatar.nadena.dev/docs/reference/menu-installer)
- [Menu Group | Modular Avatar](https://modular-avatar.nadena.dev/docs/reference/menu-group)
- [Parameters | Modular Avatar](https://modular-avatar.nadena.dev/docs/reference/parameters)

### VRChat公式ドキュメント
- [Expressions Menu and Controls | VRChat Creation](https://creators.vrchat.com/avatars/expression-menu-and-controls/)
- [Animator Parameters | VRChat Creation](https://creators.vrchat.com/avatars/animator-parameters/)

### GitHub
- [GitHub - bdunderscore/modular-avatar](https://github.com/bdunderscore/modular-avatar)
- [GitHub - hai-vr/modular-avatar-as-code](https://github.com/hai-vr/modular-avatar-as-code)
