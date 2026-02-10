# NDMF (Non-Destructive Modular Framework) 詳細ガイド

## 概要

**NDMF (なでもふ)** は、VRChatアバター向けの非破壊的なビルドプラグインを実行するためのフレームワークです。bd_氏によって開発され、Modular AvatarやAvatar Optimizerなどの主要なVRChatツールで使用されています。

## アーキテクチャ

### 3層構造

NDMFは3つの階層レベルで処理を構成します:

1. **Plugins (プラグイン)** - エンドユーザーに見える実行単位
2. **Sequences (シーケンス)** - ビルドフェーズ内のPassの順序付きコレクション
3. **Passes (パス)** - 特定のビルドポイントで実行される個別のコールバック

### 4つのビルドフェーズ

#### 1. Resolving (解決フェーズ)
- **目的**: コンポーネントやアバター状態の早期処理
- **用途**: オブジェクト参照の解決、アセットのクローン
- **例**: NDMFがEditorOnlyオブジェクトを削除、MAがアニメーションコントローラーをクローン

#### 2. Generating (生成フェーズ)
- **目的**: 下流のプラグインが使用するコンポーネントを作成
- **用途**: アセット生成、新しいコンポーネントの追加
- **例**: アニメーションクリップの生成、メッシュの生成

#### 3. Transforming (変換フェーズ)
- **目的**: 汎用的なアバター変更処理
- **用途**: メインのロジック実装
- **例**: Modular Avatarのロジックの大部分

#### 4. Optimizing (最適化フェーズ)
- **目的**: 後期段階の純粋な最適化処理
- **用途**: メッシュ結合、パフォーマンス改善
- **例**: Avatar Optimizerの処理

## プラグインの実装

### 基本構造

```csharp
using nadena.dev.ndmf;

[assembly: ExportsPlugin(typeof(MyPlugin))]

namespace YourNamespace
{
    public class MyPlugin : Plugin<MyPlugin>
    {
        public override string QualifiedName => "com.yourname.yourplugin";
        public override string DisplayName => "Your Plugin Name";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Pass Name", ctx =>
                {
                    // ビルド処理をここに記述
                });
        }
    }
}
```

### 実行順序の制約

#### BeforePlugin / AfterPlugin

他のプラグイン全体に対する実行順序を制御:

```csharp
protected override void Configure()
{
    Sequence seq = InPhase(BuildPhase.Transforming)
        .BeforePlugin("nadena.dev.modular-avatar");
    seq.Run(typeof(YourPass));
}
```

#### BeforePass / AfterPass

シーケンス内の単一Pass順序付け:

```csharp
seq
    .AfterPass(typeof(SomePriorPass))
    .Run(typeof(Pass1))
    .BeforePass(typeof(SomeOtherPass))
    .Then.Run(typeof(Pass2));
```

#### WaitFor

指定されたPassの直後に実行を試みる:

```csharp
seq
    .WaitFor(typeof(OtherPluginPass))
    .Run(typeof(YourPass));
```

## BuildContext

すべてのプラグインにビルド処理中に渡される中心的なインターフェース。

### 主要なプロパティ

```csharp
// アバター情報
ctx.AvatarRootObject        // ビルド中のルートGameObject
ctx.AvatarRootTransform     // ルートTransform
ctx.AvatarDescriptor        // アバターディスクリプタ

// アセット管理
ctx.AssetContainer          // 生成されたアセットを保存
ctx.AssetSaver             // アセットを永続化するインターフェース

// オブジェクト追跡
ctx.ObjectRegistry         // ビルド中のオブジェクト追跡
```

### 状態管理

```csharp
// ビルド間で状態を保持
var state = ctx.GetState<YourStateClass>();
```

## VirtualClip - アニメーション操作

**VirtualClip**は、Unity AnimationClipsの抽象化レイヤーで、低オーバーヘッドでアニメーションクリップを変更できます。

### 新規作成

```csharp
// 新しい空のクリップを作成
var clip = VirtualClip.Create("MyAnimationClip");
clip.FrameRate = 60f;
```

### 既存クリップのクローン

```csharp
// 独立したコピーを作成
var clonedClip = originalClip.Clone();
```

### カーブの設定

```csharp
// Floatカーブの設定
clip.SetFloatCurve(
    "path/to/object",
    typeof(GameObject),
    "m_IsActive",
    AnimationCurve.Linear(0f, 1f, 1f, 0f)
);

// EditorCurveBindingを使用
var binding = new EditorCurveBinding
{
    path = "path/to/object",
    type = typeof(SkinnedMeshRenderer),
    propertyName = "blendShape.MyShape"
};
clip.SetFloatCurve(binding, yourCurve);
```

### マテリアルプロパティのアニメーション

```csharp
// マテリアルプロパティのカーブを設定
var binding = new EditorCurveBinding
{
    path = GetRelativePath(ctx.AvatarRootTransform, renderer.transform),
    type = typeof(Renderer),
    propertyName = "material._PropertyName"
};

var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
// Tangentをカスタマイズ
for (int i = 0; i < curve.keys.Length; i++)
{
    var key = curve.keys[i];
    key.inTangent = 1f;  // Linear
    key.outTangent = 1f; // Linear
    curve.MoveKey(i, key);
}

clip.SetFloatCurve(binding, curve);
```

### パスの編集

```csharp
// すべてのバインディングパスを変換
clip.EditPaths(oldPath =>
{
    if (oldPath.StartsWith("OldPrefix/"))
        return oldPath.Replace("OldPrefix/", "NewPrefix/");
    return oldPath; // nullを返すとバインディングが削除される
});
```

### アセットの保存

```csharp
// ビルドコンテキスト経由で保存
ctx.AssetSaver.SaveAsset(clip.Clip); // .Clipで元のAnimationClipを取得
```

## VirtualAnimatorController - コントローラー操作

**VirtualAnimatorController**は、NDMFによってインデックス化されたアニメーターコントローラーを表します。

### VirtualControllerContextの使用

```csharp
InPhase(BuildPhase.Transforming).Run("Modify Controllers", ctx =>
{
    // VirtualControllerContextを有効化
    var vccContext = ctx.ActivateExtensionContext<VirtualControllerContext>();

    try
    {
        // VRChatのFXレイヤーコントローラーを取得
        var fxController = vccContext[VRCAvatarDescriptor.AnimLayerType.FX];

        if (fxController != null)
        {
            // コントローラーを変更
            ModifyController(fxController);
        }
    }
    finally
    {
        // 必ずDeactivate
        ctx.DeactivateExtensionContext<VirtualControllerContext>();
    }
});
```

### 重要な制約

1. **コンテキストがアクティブな間**: 元のコントローラーやアニメーションを変更してはいけない
2. **コンテキスト非アクティブ後**: 仮想コントローラーを変更してはいけない

### パラメータの追加

```csharp
var parameters = fxController.Controller.parameters.ToList();
if (!parameters.Any(p => p.name == "MyParameter"))
{
    parameters.Add(new AnimatorControllerParameter
    {
        name = "MyParameter",
        type = AnimatorControllerParameterType.Float,
        defaultFloat = 0f
    });
    fxController.Controller.parameters = parameters.ToArray();
}
```

### レイヤーの追加

```csharp
var layers = fxController.Controller.layers.ToList();
var newLayer = new AnimatorControllerLayer
{
    name = "MyLayer",
    defaultWeight = 1f,
    stateMachine = new AnimatorStateMachine()
};
layers.Add(newLayer);
fxController.Controller.layers = layers.ToArray();
```

### ステートの追加（Motion Time対応）

```csharp
var layer = fxController.Controller.layers[layerIndex];
var stateMachine = layer.stateMachine;

// 新しいステートを作成
var state = stateMachine.AddState("MyState");
state.motion = myAnimationClip;
state.writeDefaultValues = false; // 推奨設定
state.timeParameterActive = true;  // Motion Time有効化
state.timeParameter = "MyFloatParameter"; // Motion Time Parameter
```

### トランジションの追加

```csharp
var defaultState = stateMachine.defaultState;
var targetState = stateMachine.states[1].state;

var transition = defaultState.AddTransition(targetState);
transition.hasExitTime = false;
transition.duration = 0.1f;
transition.AddCondition(AnimatorConditionMode.If, 0, "MyParameter");
```

## AnimatorServicesContext

オブジェクトのリネームを自動的に追跡し、既知のアニメーターに適用します。

```csharp
InPhase(BuildPhase.Transforming).Run("With Animator Services", ctx =>
{
    // AnimatorServicesContextを有効化（VirtualControllerContextも自動的に有効化）
    var animatorServices = ctx.ActivateExtensionContext<AnimatorServicesContext>();

    try
    {
        // オブジェクトをリネームまたは移動
        // パスは自動的に更新される
        someObject.name = "NewName";
        someObject.transform.SetParent(newParent);

        // 新しいオブジェクトを登録
        ctx.ObjectRegistry.RegisterReplacedObject(oldObject, newObject);
    }
    finally
    {
        ctx.DeactivateExtensionContext<AnimatorServicesContext>();
    }
});
```

## VRChat統合

### Expression Parametersの追加

```csharp
var descriptor = ctx.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
var parameters = descriptor.expressionParameters;

if (parameters == null)
{
    parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
    parameters.name = "GeneratedParameters";
    descriptor.expressionParameters = parameters;
    ctx.AssetSaver.SaveAsset(parameters);
}

var paramList = parameters.parameters.ToList();
if (!paramList.Any(p => p.name == "MyParameter"))
{
    paramList.Add(new VRCExpressionParameters.Parameter
    {
        name = "MyParameter",
        valueType = VRCExpressionParameters.ValueType.Float,
        defaultValue = 0f,
        saved = true,
        networkSynced = true
    });
    parameters.parameters = paramList.ToArray();
}
```

### Expression Menuの追加

```csharp
var menu = descriptor.expressionsMenu;
if (menu == null)
{
    menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
    menu.name = "GeneratedMenu";
    descriptor.expressionsMenu = menu;
    ctx.AssetSaver.SaveAsset(menu);
}

var controls = menu.controls.ToList();
controls.Add(new VRCExpressionsMenu.Control
{
    name = "My Control",
    type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
    subParameters = new[]
    {
        new VRCExpressionsMenu.Control.Parameter { name = "MyParameter" }
    }
});
menu.controls = controls;
```

## ベストプラクティス

### 1. フェーズの選択

- **Generating**: 他のプラグインが使用するアセットを生成
- **Transforming**: メインロジック、メニュー追加、コントローラー変更
- **Optimizing**: 純粋な最適化のみ

### 2. Extension Contextの管理

常にtry-finallyでDeactivate:

```csharp
var context = ctx.ActivateExtensionContext<SomeContext>();
try
{
    // 処理
}
finally
{
    ctx.DeactivateExtensionContext<SomeContext>();
}
```

### 3. 相対パスの取得

```csharp
private string GetRelativePath(Transform root, Transform target)
{
    if (target == root) return "";

    var path = target.name;
    var current = target.parent;

    while (current != null && current != root)
    {
        path = current.name + "/" + path;
        current = current.parent;
    }

    return path;
}
```

### 4. パラメータの重複チェック

```csharp
// Animator Parameters
var animParams = controller.Controller.parameters;
if (!animParams.Any(p => p.name == paramName))
{
    // 追加処理
}

// Expression Parameters
var exprParams = descriptor.expressionParameters.parameters;
if (!exprParams.Any(p => p.name == paramName))
{
    // 追加処理
}
```

### 5. Write Defaults設定

現代的な実装では`false`を推奨:

```csharp
state.writeDefaultValues = false;
```

## 参考資料

- [NDM Framework公式ドキュメント](https://ndmf.nadena.dev/)
- [Execution Model](https://ndmf.nadena.dev/execution-model.html)
- [GitHub - bdunderscore/ndmf](https://github.com/bdunderscore/ndmf)
- [VirtualClip API](https://ndmf.nadena.dev/api/nadena.dev.ndmf.animator.VirtualClip.html)
- [VirtualAnimatorController API](https://ndmf.nadena.dev/api/nadena.dev.ndmf.animator.VirtualAnimatorController.html)
- [BuildContext API](https://ndmf.nadena.dev/api/nadena.dev.ndmf.BuildContext.html)
