# クイックスタートガイド

FacialControl を使って 3D キャラクターの表情をリアルタイム制御するまでの手順を解説します。本ガイドは GUI ベースの導線を優先しており、上から順に読み進めれば JSON を手書きせずに動作確認まで完遂できます。

## 動作要件

- Unity 6000.3 以降
- Animator コンポーネントが設定された 3D キャラクターモデル（FBX / VRM など）
- BlendShape（シェイプキー）を持つ SkinnedMeshRenderer

## 推奨モデル

BlendShape を多数持つモデルであれば何でも動作しますが、以下のいずれかを推奨します。

- **VRoid Studio で出力した VRM モデル**（UniVRM / VRM パッケージで Unity にインポート。BlendShape 名: `Fcl_ALL_Joy` など VRM 標準）
- **ARKit 52 / PerfectSync 対応モデル**（BlendShape 名: `browInnerUp`, `eyeBlinkLeft`, `jawOpen` など）
- **ニコニ立体ちゃん（Unity-Chan 等）**（キャラクター独自命名）

本ガイドでは VRM 命名を例にします。ARKit 命名の場合は後述の **9. ARKit / PerfectSync の自動検出** を使うとプロファイルを自動生成できます。

## 1. パッケージのインストール

FacialControl は機能別に 3 パッケージで提供されます。コアパッケージは表情制御本体のみで、OSC 配信・InputSystem 入力は用途に応じて任意で追加します。

| パッケージ | 役割 | 必須依存 |
|---|---|---|
| `com.hidano.facialcontrol` | コア（表情遷移・JSON プロファイル・Editor 拡張） | Unity 6000.3+ |
| `com.hidano.facialcontrol.osc` | OSC 送受信アダプタ（VRChat / ARKit 互換） | コア + `com.hidano.uosc` |
| `com.hidano.facialcontrol.inputsystem` | InputSystem 経由のキーバインド・コントローラ入力 | コア + `com.unity.inputsystem` |

### npm レジストリ経由（推奨）

`Packages/manifest.json` に以下を追加してください（必要なサブパッケージのみ追加 OK）。

```json
{
    "scopedRegistries": [
        {
            "name": "npmjs",
            "url": "https://registry.npmjs.org",
            "scopes": [
                "com.hidano"
            ]
        }
    ],
    "dependencies": {
        "com.hidano.facialcontrol": "0.1.0-preview.1",
        "com.hidano.facialcontrol.osc": "0.1.0-preview.1",
        "com.hidano.facialcontrol.inputsystem": "0.1.0-preview.1"
    }
}
```

### Git URL 経由

npm レジストリを経由せず、GitHub から直接インポートする場合は `Packages/manifest.json` の `dependencies` に Git URL を指定します。

```json
{
    "dependencies": {
        "com.hidano.facialcontrol": "https://github.com/NHidano/FacialControl.git?path=FacialControl/Packages/com.hidano.facialcontrol"
    }
}
```

特定のバージョン（タグ）やブランチを指定する場合は URL の末尾に `#<tag-or-branch>` を付加します。

```json
{
    "dependencies": {
        "com.hidano.facialcontrol": "https://github.com/NHidano/FacialControl.git?path=FacialControl/Packages/com.hidano.facialcontrol#v0.1.0-preview.1"
    }
}
```

## 2. プロファイルの作成（GUI）

メニュー **FacialControl** → **新規プロファイル作成** からダイアログを開き、GUI だけでプロファイルを生成できます。

1. ダイアログで以下を入力:
   - **プロファイル名**: 任意の名前（例: `MyProfile`）
   - **レイヤー定義**: デフォルトで `emotion` / `lipsync` / `eye` の 3 レイヤーが設定済み。必要に応じて追加・編集
   - **命名規則プリセット**: 使用モデルの BlendShape 命名規則を選択
     - `VRM`: VRoid などの `Fcl_ALL_Joy` 系命名
     - `ARKit`: ARKit 52 / PerfectSync の `mouthSmile_L` 系命名
     - `None`: 雛形 Expression を生成しない（自分で追加する）
   - **雛形 Expression を追加**: チェック ON で `smile` / `angry` / `blink` の 3 つを自動生成
2. **作成** ボタンを押すと:
   - `Assets/{プロファイル名}_Profile.asset` に `FacialProfileSO` が生成される
   - `StreamingAssets/FacialControl/{プロファイル名}.json` に JSON が自動出力される

生成される JSON は Unity に読み込まれる表情定義の正規データですが、GUI フローで完結するため**このガイドでは JSON を直接編集する必要はありません**。必要になった場合は **11. 高度な使い方** を参照してください。

### 雛形 Expression の BlendShape 名について

`VRM` / `ARKit` プリセットの雛形は一般的な命名規則に準拠していますが、モデル個別の BlendShape 名と完全一致するとは限りません。後述の **6. Expression の微調整** で実際のモデルに合わせて調整できます。

## 3. FacialController のセットアップ

1. シーンにキャラクターを配置（Animator コンポーネントが必要）
2. キャラクターの GameObject を選択
3. **Add Component** → **FacialControl** → **Facial Controller**
4. Inspector で以下を設定:
   - **Profile SO**: 手順 2 で作成した `FacialProfileSO` アセットをドラッグ＆ドロップ
   - **Skinned Mesh Renderers**: 空のままにすると子オブジェクトから自動検索。手動で指定する場合はここにドラッグ

FacialController は `OnEnable` 時に ProfileSO が設定されていれば自動で初期化されます。

## 4. キーコンフィグの設定

雛形 Expression をキーボード／コントローラから発火するために、InputAction と Expression のバインディングを設定します。

### 4.1. InputBindingProfileSO の作成

1. Project ウィンドウで右クリック → **Create** → **FacialControl** → **Input Binding Profile**
2. 作成された `InputBindingProfileSO` アセットを選択
3. Inspector で以下を設定:
   - **Action Asset**: `Packages/com.hidano.facialcontrol/Runtime/Adapters/Input/FacialControlDefaultActions.inputactions`（同梱）を指定するか、後述の **4.4** の手順で複製したカスタム Asset を指定
   - **Action Map Name**: 対象の ActionMap 名（デフォルト: `Expression`）
   - **Facial Profile (Editor Only)**: Expression 候補を列挙するために参照する `FacialProfileSO`（手順 2 で作成したもの。この参照は Inspector 上の補助用で Asset 本体には保存されません）
4. **＋** ボタンでバインディング行を追加し、`Trigger1` → `smile`、`Trigger2` → `angry`、`Trigger3` → `blink` を割り当てます

### 4.2. FacialInputBinder の配置と割り当て

1. 手順 3 で `FacialController` を付与したキャラクターの GameObject を選択
2. **Add Component** → **FacialControl** → **Facial Input Binder**
3. Inspector で以下を設定:
   - **Facial Controller**: 手順 3 の `FacialController` コンポーネントをドラッグ＆ドロップ
   - **Binding Profile**: 手順 4.1 で作成した `InputBindingProfileSO` アセットをドラッグ＆ドロップ

`FacialInputBinder` は `OnEnable` 時に `InputBindingProfileSO` を読み込んで内部の `InputSystemAdapter` にバインディングを登録し、`OnDisable` 時に全バインディングを解除します。

### 4.3. Trigger1〜Trigger12 汎用スロット

同梱の `FacialControlDefaultActions.inputactions` には、`Expression` ActionMap 内に `Trigger1` 〜 `Trigger12` の 12 個の汎用 Action スロットが用意されています。

| Action 名 | デフォルトバインド |
|-----------|-------------------|
| Trigger1 | キーボード `1` / ゲームパッド South（A ボタン） |
| Trigger2 | キーボード `2` / ゲームパッド East（B ボタン） |
| ... | ... |
| Trigger12 | キーボード `=` ほか |

任意の Expression を任意の `TriggerN` に割り当てることで、キーコンフィグを自由に構成できます。各 Trigger はプレス／リリースに応じて Expression のアクティブ化／非アクティブ化を行います。

### 4.4. キーバインドのカスタマイズ

デフォルトのキーバインドを変更したい場合は `FacialControlDefaultActions.inputactions` を複製してカスタマイズします。

1. Project ウィンドウで `Packages/com.hidano.facialcontrol/Runtime/Adapters/Input/FacialControlDefaultActions.inputactions` を右クリック → **Copy**（または `Ctrl+D` で複製）
2. 複製した Asset を任意の場所（例: `Assets/InputActions/MyFacialActions.inputactions`）へ配置
3. Asset をダブルクリックして Input Actions エディタを開き、`Trigger1〜Trigger12` のバインドを編集
4. `InputBindingProfileSO` の **Action Asset** フィールドに複製した Asset を割り当てる

この方式によりパッケージ同梱の Asset を書き換えることなく、プロジェクト固有のキーコンフィグを維持できます。

## 5. 動作確認

ここまでの手順が完了したら Unity Editor で Play モードに入ります。キーボードの **1 / 2 / 3** キーを押すと、それぞれ `smile` / `angry` / `blink` が発動します。キーを離すと元に戻ります。

モデルの BlendShape 名が雛形と一致していれば即座に動作します。反応しない場合は **トラブルシューティング** の「表情がまったく変化しない」を参照してください。

## 6. Expression の微調整

雛形 Expression の BlendShape 値をモデルに合わせて調整する場合、または新しい Expression を追加する場合は、専用の GUI ツールを使います。

### 6.1. FacialProfileSO Inspector で調整

1. 手順 2 で作成した `FacialProfileSO` アセットを選択
2. Inspector の **使用モデル** セクションで、対象モデル（シーン上のキャラクターまたは Prefab）を指定
   - これにより Expression 編集時の BlendShape 名がドロップダウンから選択できるようになります
3. **Expression 一覧** セクションで既存 Expression を展開
4. BlendShape 行の **＋** ボタンで新規追加、既存行のドロップダウンから BlendShape 名を選択

Expression や BlendShape を編集すると、JSON は自動で上書き保存されます。

### 6.2. Expression 作成ウィンドウで新規追加

より視覚的に Expression を作成したい場合は、メニュー **FacialControl** → **Expression 作成** を開きます。

1. 上部のフィールドで編集対象の `FacialProfileSO` を選択
2. モデル欄が自動的に設定されます（未設定の場合は手動指定）
3. スライダーを動かしながら表情を確認
4. Expression 名・レイヤー・遷移時間を設定して保存

BlendShape 名候補は参照モデルから自動取得されるため、タイポによる「動かない」問題を防げます。

## 7. スクリプトから表情を切り替える（オプション）

キーボードやコントローラから Expression を発火したいだけの場合は **4. キーコンフィグの設定** で完結します。以下は独自のトリガー条件（タイマー・ネットワークイベント等）から切り替える場合の例です。

```csharp
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;
using UnityEngine.InputSystem;

public class MyExpressionController : MonoBehaviour
{
    [SerializeField]
    private FacialController _facialController;

    private Expression? _smileExpression;

    private void Start()
    {
        // プロファイルから名前で Expression を取得
        if (_facialController.IsInitialized && _facialController.CurrentProfile.HasValue)
        {
            var profile = _facialController.CurrentProfile.Value;
            var expressions = profile.Expressions.Span;
            for (int i = 0; i < expressions.Length; i++)
            {
                if (expressions[i].Name == "smile")
                {
                    _smileExpression = expressions[i];
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (_smileExpression == null) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // スペースキーで笑顔を切り替え
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            _facialController.Activate(_smileExpression.Value);
        }

        if (keyboard.spaceKey.wasReleasedThisFrame)
        {
            _facialController.Deactivate(_smileExpression.Value);
        }
    }
}
```

### 主要 API

| メソッド | 説明 |
|---------|------|
| `Activate(Expression)` | Expression をアクティブ化する。レイヤーの排他モードに基づいて処理 |
| `Deactivate(Expression)` | Expression を非アクティブ化する |
| `LoadProfile(FacialProfileSO)` | プロファイルを切り替える（PlayableGraph を再構築） |
| `ReloadProfile()` | 現在のプロファイルを再読み込みする |
| `GetActiveExpressions()` | 現在アクティブな Expression のリストを取得 |

### 主要プロパティ

| プロパティ | 型 | 説明 |
|-----------|------|------|
| `IsInitialized` | `bool` | 初期化済みかどうか |
| `CurrentProfile` | `FacialProfile?` | 現在のプロファイル |
| `ProfileSO` | `FacialProfileSO` | ProfileSO の参照 |
| `OscSendPort` | `int` | OSC 送信ポート（デフォルト: 9000） |
| `OscReceivePort` | `int` | OSC 受信ポート（デフォルト: 9001） |

## 8. OSC 通信の設定（オプション）

VTuber 配信でフェイシャルキャプチャを連動する場合、OSC 通信を設定します。

`StreamingAssets/FacialControl/config.json` を配置してください。テンプレートは `Templates/default_config.json` にあります。

```json
{
    "schemaVersion": "1.0",
    "osc": {
        "sendPort": 9000,
        "receivePort": 9001,
        "preset": "vrchat",
        "mapping": [
            {
                "oscAddress": "/avatar/parameters/Fcl_ALL_Joy",
                "blendShapeName": "Fcl_ALL_Joy",
                "layer": "emotion"
            }
        ]
    },
    "cache": {
        "animationClipLruSize": 16
    }
}
```

OSC アドレス形式は VRChat 互換（`/avatar/parameters/{name}`）です。

## 9. ARKit / PerfectSync の自動検出（オプション）

モデルが ARKit 52 または PerfectSync の BlendShape 命名規則に準拠している場合、自動検出機能を使って Expression と OSC マッピングを自動生成できます。

1. メニュー **FacialControl** → **ARKit 検出ツール** を開く
2. 対象の SkinnedMeshRenderer を指定
3. **検出実行** ボタンをクリック
4. 検出結果を確認し、**出力先** を選択:
   - **新規 JSON として保存**: 新しいプロファイル JSON を作成
   - **既存 FacialProfileSO の JSON に追記**: 既存プロファイルに Expression と OSC マッピングをマージ（ID 衝突時は自動で新 UUID を発行）
5. Expression の自動生成を実行

既存プロファイルにマージする場合は、マージ先の `FacialProfileSO` を指定してください。

## 10. レイヤー構成と排他モード

FacialControl はレイヤーベースで表情を管理します。デフォルトは 3 レイヤー構成です。

| レイヤー | 優先度 | 排他モード | 用途 |
|---------|--------|-----------|------|
| emotion | 0 | LastWins | 感情表情（笑顔、怒りなど） |
| lipsync | 1 | Blend | リップシンク（口の動き） |
| eye | 2 | LastWins | 目の制御（まばたきなど） |

**排他モード**:
- `lastWins`: 同レイヤー内で最後にアクティブ化された Expression のみ有効（クロスフェード遷移）
- `blend`: 同レイヤー内の複数 Expression をブレンド合成（加算、0〜1 クランプ）

レイヤー構成は プロファイル作成ダイアログおよび `FacialProfileSO` Inspector から変更できます。

## 11. 高度な使い方: JSON を直接編集する

通常の運用では GUI フローで完結しますが、以下のようなケースでは JSON を直接編集できます。

- ビルド後のランタイムで表情定義を差し替えたい
- 外部ツールから FacialControl 用プロファイルを生成したい
- バージョン管理での差分を細かく追跡したい

### JSON の所在

`StreamingAssets/FacialControl/{プロファイル名}.json` に配置されます。`FacialProfileSO` は `JsonFilePath` プロパティでこの相対パスを保持しており、ランタイムで読み込まれます。

### スキーマリファレンス

JSON の詳細なスキーマは [json-schema.md](json-schema.md) を参照してください。

### 外部エディタで編集する場合の注意

- `FacialProfileSO` Inspector で編集している最中に外部エディタで書き換えると、Unity 側の変更で上書きされる可能性があります
- 外部エディタで編集した後は Unity Editor にフォーカスを戻し、`FacialProfileSO` Inspector の **Refresh** ボタン（もしくは再選択）で再読み込みしてください
- JSON 直接編集後にテンプレートから新規作成するには、メニュー **FacialControl** → **新規プロファイル作成** で `None` プリセットを選ぶと空のプロファイルを生成できます

## トラブルシューティング

### 表情がまったく変化しない

- **FacialProfileSO の JSON パスが間違っている**
  - `StreamingAssets/` からの相対パスで指定します（例: `FacialControl/MyProfile.json`）
  - Console に `FacialController` からのエラーログが出ていないか確認してください
- **BlendShape 名がモデルと一致していない**
  - 雛形 Expression の BlendShape 名がモデル側と異なる場合、**6.1. FacialProfileSO Inspector で調整** で参照モデルを設定してドロップダウンから正しい名前を選び直してください
  - 一致しない BlendShape は警告なしでスキップされます
- **SkinnedMeshRenderer が認識されていない**
  - `FacialController` の **Skinned Mesh Renderers** が空の場合、子オブジェクトを自動検索します
  - 階層が深い・別オブジェクトに分離されている場合は手動で明示的に割り当ててください
- **Animator が無い**
  - `FacialController` は `Animator` コンポーネントを前提とします。キャラクターのルートに Animator が付いていることを確認してください

### キー入力で切り替わらない

- **InputBindingProfileSO の Facial Profile が未設定**
  - Editor 上で Expression 候補を列挙するために `FacialProfileSO` の参照が必要です
  - 参照を設定して **Refresh** ボタンを押してください
- **FacialInputBinder の Facial Controller / Binding Profile が未設定**
  - 両方とも割り当てが必須です
- **ActionMap 名が一致しない**
  - デフォルトの `FacialControlDefaultActions.inputactions` は `Expression` ActionMap を持ちます。`InputBindingProfileSO` の **Action Map Name** も `Expression` にしてください
- **別の InputSystem 設定と競合している**
  - プロジェクトに独自の `PlayerInput` 等がある場合、同じキーが別 Action に割り当てられていると干渉する可能性があります

### 遷移がカクつく / 不自然

- **transitionDuration が 0 または極端に短い**
  - デフォルト 0.25 秒を推奨。0 秒にすると瞬時に切り替わります
- **同レイヤーで `exclusionMode: blend` を使っているのに 1 つしか Activate していない**
  - `blend` はレイヤー内で複数 Expression を合成するモードです。単一切り替えなら `lastWins` を使用してください

### OSC 通信が機能しない

- **`config.json` が `StreamingAssets/FacialControl/` に配置されていない**
- **ポートが他プロセスに占有されている**
  - 既定の送信 9000 / 受信 9001 が使われていないか確認
- **ファイアウォールで UDP がブロックされている**
  - Windows Defender ファイアウォールで Unity.exe の UDP 通信を許可
- **VRChat 互換のパラメータ名を使っていない**
  - OSC アドレスは `/avatar/parameters/{name}` 形式である必要があります

### ARKit 検出で Expression が生成されない

- **BlendShape 名が ARKit 命名規則に準拠していない**
  - `browInnerUp`, `eyeBlinkLeft`, `jawOpen` など、ARKit 52 の正規名が必要です
  - 先頭が大文字・スネークケース等の独自命名の場合は手動でプロファイルを作成してください

## 次のステップ

- **JSON スキーマの詳細**: [json-schema.md](json-schema.md) を参照
- **Expression 作成ツール**: メニュー **FacialControl** → **Expression 作成** で BlendShape スライダーを操作しながら Expression を作成可能
- **プロファイル管理**: `FacialProfileSO` の Inspector で Expression の追加・編集・削除、JSON のインポート/エクスポートが可能
