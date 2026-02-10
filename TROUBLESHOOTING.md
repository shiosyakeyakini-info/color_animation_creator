# トラブルシューティングガイド

## アニメーションが反映されない場合の確認手順

### ステップ1: コンポーネントの設定確認

1. **ColorHueAnimatorコンポーネントの配置**
   - アバターのヒエラルキー内のGameObjectに配置されているか確認
   - インスペクターで以下を確認:
     - ✅ Target Renderer: 設定されているか
     - ✅ Material Index: 範囲内の値か（0から始まる）
     - ✅ Color Property Name: 正しいプロパティ名か（例: `_EmissionColor`）
     - ✅ Parameter Name: ユニークな名前か
     - ✅ インスペクター下部に「✓ 設定は正常です。」と表示されているか

2. **検証エラーのチェック**
   - インスペクター下部にエラーメッセージが表示されていないか確認
   - エラーがある場合は、メッセージに従って修正

### ステップ2: NDMFビルドの実行

Color Animation CreatorはNDMFプラグインなので、**NDMFビルドが実行されないと何も生成されません**。

#### 方法1: Manual Bake（推奨）

1. Unity上部メニュー → `Tools` → `NDM Framework` → `Manual Bake Avatar`
2. ヒエラルキーでアバターのルートオブジェクトを選択
3. 実行後、Consoleでエラーがないか確認

#### 方法2: アバターアップロード時の自動ビルド

1. VRChat SDK Control Panel → `Build & Publish`
2. ビルド時に自動的にNDMFが実行される
3. アップロードせずにビルドのみ実行してテスト可能

### ステップ3: 生成物の確認

NDMFビルド後、以下が生成されているはずです:

#### 3-1. アニメーションクリップの確認

1. **場所**: `Assets/NDMF Generated/` または `Assets/_GeneratedAssets/` フォルダ内
2. **ファイル名**: `{GameObjectName}_HueRotation.anim`
3. **確認方法**:
   - Project ウィンドウで検索: `HueRotation`
   - クリップを選択してAnimationウィンドウで中身を確認
   - R、G、B のカーブが存在するか確認

#### 3-2. AnimatorControllerの確認

1. アバターのVRCAvatarDescriptorを選択
2. `Playable Layers` → `FX` のAnimatorControllerを開く
3. **確認ポイント**:
   - Layersタブに `HueShift_{パラメータ名}` というレイヤーが追加されているか
   - Parametersタブに Float型の `{パラメータ名}` が追加されているか
   - レイヤー内に `HueRotation` というステートがあるか
   - ステートのMotionに生成されたアニメーションクリップが設定されているか
   - ステートのMotion Timeが有効になっているか（Inspector確認）

#### 3-3. Expression Parameters/Menuの確認

1. **Expression Parameters**:
   - VRCAvatarDescriptor → `Expressions` → `Parameters`
   - Float型のパラメータが追加されているか

2. **Expression Menu**:
   - VRCAvatarDescriptor → `Expressions` → `Menu`
   - Radial Puppet タイプのコントロールが追加されているか
   - コントロール名とアイコンが正しいか

### ステップ4: Consoleログの確認

Unity Console（Ctrl+Shift+C）で以下を確認:

#### エラーメッセージ例

```
[ColorAnimationCreator] ColorHueAnimator on 'XXX' has invalid settings.
→ コンポーネント設定に問題あり

[ColorAnimationCreator] VRCAvatarDescriptor not found on avatar root.
→ コンポーネントがアバタールート外に配置されている

[ColorAnimationCreator] No generated clip found for 'XXX'.
→ Generatingフェーズでクリップ生成に失敗
```

#### 成功時のログ

特にエラーがなければ、NDMFビルドは成功しています。

### ステップ5: Play Modeでのテスト

1. Unity Play Modeに入る
2. アバターを選択
3. Animator ウィンドウを開く（Window → Animation → Animator）
4. **手動テスト**:
   - Parametersタブで `{パラメータ名}` の値を 0.0 ～ 1.0 の範囲で変更
   - Scene ビューでマテリアルの色が変化するか確認
   - 0.0 = 元の色相、0.5 = 補色、1.0 = 元の色相に戻る

### ステップ6: よくある問題と解決策

#### 問題1: NDMFビルドが実行されない

**原因**: NDMFがインストールされていない、またはバージョンが古い

**解決策**:
1. VCC（VRChat Creator Companion）でNDMFを最新版に更新
2. Unityを再起動
3. Manual Bake を再実行

#### 問題2: アニメーションクリップが生成されない

**原因**: Generatingフェーズでエラーが発生

**確認**:
1. Console で `[ColorAnimationCreator]` のエラーログを確認
2. ColorHueAnimatorの設定を再確認
3. マテリアルのプロパティ名が正しいか確認（大文字小文字も一致）

#### 問題3: AnimatorControllerにレイヤーが追加されない

**原因**: FX AnimatorControllerが見つからない、またはTransformingフェーズでエラー

**解決策**:
1. VRCAvatarDescriptorのFXレイヤーに空のAnimatorControllerを設定
2. または、VRChat SDKのデフォルトFXコントローラーをセット
3. Manual Bake を再実行

#### 問題4: 色が変化しない

**原因**: プロパティ名の間違い、またはHDR色の問題

**確認**:
1. マテリアルのInspectorで正確なプロパティ名を確認
   - 表示名と内部名は異なる場合がある
   - 例: 「Emission Color」→ 内部名 `_EmissionColor`

2. liltoonのマテリアルロックを確認
   - マテリアルがロックされているか
   - `Animated (when locked)` に設定されているか

3. アニメーションパスの確認
   - Animationウィンドウでカーブを確認
   - プロパティパス: `material.{プロパティ名}.r/g/b`

#### 問題5: VRChatにアップロード後、動作しない

**原因**: Expression ParametersまたはMenuが正しく設定されていない

**確認**:
1. Expression Parameters のメモリ制限（256ビット）を超えていないか
2. Expression Menu が満杯（8個制限）になっていないか
3. パラメータがSyncedに設定されているか

### デバッグモードの有効化

より詳細なログを確認したい場合:

1. ColorAnimationCreatorPlugin.cs を開く
2. 各メソッドの先頭に以下を追加:
   ```csharp
   Debug.Log($"[ColorAnimationCreator] {メソッド名} started");
   ```
3. 再コンパイル後、Manual Bake を実行
4. Consoleで詳細なログを確認

### まだ解決しない場合

以下の情報を確認してください:

1. **Unity バージョン**: `Help → About Unity`
2. **VRChat SDK バージョン**: Package Manager で確認
3. **NDMF バージョン**: Package Manager で確認
4. **Modular Avatar バージョン**: Package Manager で確認
5. **liltoon バージョン**: シェーダーファイルで確認

**Console の完全なエラーログ**をコピーして、問題を特定します。

---

## クイックチェックリスト

- [ ] ColorHueAnimatorコンポーネントが配置されている
- [ ] インスペクターで「✓ 設定は正常です。」と表示
- [ ] NDMFビルド（Manual Bake）を実行した
- [ ] Consoleにエラーがない
- [ ] アニメーションクリップが生成されている
- [ ] AnimatorControllerにレイヤーが追加されている
- [ ] Expression ParametersにFloatパラメータが追加されている
- [ ] Expression MenuにRadial Puppetが追加されている
- [ ] Play Modeで手動テストして色が変化する

すべてチェックできれば、VRChatでも正常に動作するはずです。
