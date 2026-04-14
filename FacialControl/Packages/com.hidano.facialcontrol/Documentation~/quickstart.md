# クイックスタートガイド

FacialControl を使って 3D キャラクターの表情をリアルタイム制御するまでの手順を解説します。

## 動作要件

- Unity 6000.3 以降
- Animator コンポーネントが設定された 3D キャラクターモデル（FBX）
- BlendShape（シェイプキー）を持つ SkinnedMeshRenderer

## 1. パッケージのインストール

### OpenUPM 経由（推奨）

`Packages/manifest.json` に以下を追加してください。

```json
{
    "scopedRegistries": [
        {
            "name": "OpenUPM",
            "url": "https://package.openupm.com",
            "scopes": [
                "com.hidano"
            ]
        }
    ],
    "dependencies": {
        "com.hidano.facialcontrol": "0.1.0-preview.1"
    }
}
```

### ローカルインストール

リポジトリをクローンして `Packages/` ディレクトリに配置する場合:

```json
{
    "dependencies": {
        "com.hidano.facialcontrol": "file:com.hidano.facialcontrol"
    }
}
```

## 2. プロファイル JSON の作成

表情プロファイルは JSON ファイルで管理します。`StreamingAssets/FacialControl/` ディレクトリに JSON ファイルを配置してください。

パッケージ同梱のテンプレート `Templates/default_profile.json` をコピーして編集するのが簡単です。

### 最小構成の例

```json
{
    "schemaVersion": "1.0",
    "layers": [
        {"name": "emotion", "priority": 0, "exclusionMode": "lastWins"},
        {"name": "lipsync", "priority": 1, "exclusionMode": "blend"},
        {"name": "eye", "priority": 2, "exclusionMode": "lastWins"}
    ],
    "expressions": [
        {
            "id": "550e8400-e29b-41d4-a716-446655440000",
            "name": "smile",
            "layer": "emotion",
            "transitionDuration": 0.25,
            "transitionCurve": {"type": "linear"},
            "blendShapeValues": [
                {"name": "Fcl_ALL_Joy", "value": 1.0}
            ],
            "layerSlots": []
        },
        {
            "id": "661f9511-f30c-52e5-b827-557766551111",
            "name": "angry",
            "layer": "emotion",
            "transitionDuration": 0.25,
            "transitionCurve": {"type": "easeInOut"},
            "blendShapeValues": [
                {"name": "Fcl_ALL_Angry", "value": 1.0}
            ],
            "layerSlots": []
        }
    ]
}
```

### レイヤー構成

FacialControl はレイヤーベースで表情を管理します。デフォルトは 3 レイヤー構成です。

| レイヤー | 優先度 | 排他モード | 用途 |
|---------|--------|-----------|------|
| emotion | 0 | LastWins | 感情表情（笑顔、怒りなど） |
| lipsync | 1 | Blend | リップシンク（口の動き） |
| eye | 2 | LastWins | 目の制御（まばたきなど） |

**排他モード**:
- `lastWins`: 同レイヤー内で最後にアクティブ化された Expression のみ有効（クロスフェード遷移）
- `blend`: 同レイヤー内の複数 Expression をブレンド合成（加算、0〜1 クランプ）

### BlendShape 値

`blendShapeValues` にはモデルの BlendShape 名と値（0.0〜1.0）を指定します。BlendShape 名はモデルに定義されている名前と完全一致させてください。

## 3. FacialProfileSO の作成

Unity Editor で ScriptableObject を作成し、JSON ファイルへの参照を設定します。

1. Project ウィンドウで右クリック → **Create** → **FacialControl** → **Facial Profile**
2. 作成された FacialProfileSO アセットを選択
3. Inspector の **Json File Path** に JSON ファイルのパスを入力（`StreamingAssets/` からの相対パス）
   - 例: `FacialControl/my_character_profile.json`

## 4. FacialController のセットアップ

1. キャラクターの GameObject を選択（Animator コンポーネントが必要）
2. **Add Component** → **FacialControl** → **Facial Controller**
3. Inspector で以下を設定:
   - **Profile SO**: 手順 3 で作成した FacialProfileSO アセットをドラッグ＆ドロップ
   - **Skinned Mesh Renderers**: 空のままにすると子オブジェクトから自動検索。手動で指定する場合はここにドラッグ

FacialController は `OnEnable` 時に ProfileSO が設定されていれば自動で初期化されます。

## 5. スクリプトから表情を切り替える

```csharp
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

public class MyExpressionController : MonoBehaviour
{
    [SerializeField]
    private FacialController _facialController;

    private Expression? _smileExpression;

    private void Start()
    {
        // プロファイルから Expression を取得
        if (_facialController.IsInitialized && _facialController.CurrentProfile.HasValue)
        {
            var profile = _facialController.CurrentProfile.Value;
            _smileExpression = profile.FindExpressionById("550e8400-e29b-41d4-a716-446655440000");
        }
    }

    private void Update()
    {
        if (_smileExpression == null) return;

        // スペースキーで笑顔を切り替え
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _facialController.Activate(_smileExpression.Value);
        }

        if (Input.GetKeyUp(KeyCode.Space))
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

## 6. キーコンフィグの設定

InputAction と Expression の紐付けを ScriptableObject として永続化し、コードを書かずにキーボード／コントローラからの表情切り替えを実現できます。

### 6.1. InputBindingProfileSO の作成

1. Project ウィンドウで右クリック → **Create** → **FacialControl** → **Input Binding Profile**
2. 作成された `InputBindingProfileSO` アセットを選択
3. Inspector で以下を設定:
   - **Action Asset**: 使用する `InputActionAsset`（同梱の `FacialControlDefaultActions.inputactions` を指定するか、後述の手順で複製したカスタム Asset を指定）
   - **Action Map Name**: 対象の ActionMap 名（デフォルト: `Expression`）
   - **Facial Profile (Editor Only)**: Expression 候補を列挙するために参照する `FacialProfileSO`（この参照は Inspector 上の補助用で Asset 本体には保存されません）
4. **＋** ボタンでバインディング行を追加し、各行で **Action** ドロップダウンと **Expression** ドロップダウンを選択します
5. JSON を外部エディタで編集した場合は **Refresh** ボタンで Expression 一覧を再取得できます

### 6.2. FacialInputBinder の配置と割り当て

1. キャラクターの GameObject（`FacialController` コンポーネントが付与されている GameObject）を選択
2. **Add Component** → **FacialControl** → **Facial Input Binder**
3. Inspector で以下を設定:
   - **Facial Controller**: 対象の `FacialController` コンポーネントをドラッグ＆ドロップ
   - **Binding Profile**: 手順 6.1 で作成した `InputBindingProfileSO` アセットをドラッグ＆ドロップ

`FacialInputBinder` は `OnEnable` 時に `InputBindingProfileSO` を読み込んで内部の `InputSystemAdapter` にバインディングを登録し、`OnDisable` 時に全バインディングを解除します。

### 6.3. Trigger1〜Trigger12 汎用スロット

同梱の `FacialControlDefaultActions.inputactions` には、`Expression` ActionMap 内に `Trigger1` 〜 `Trigger12` の 12 個の汎用 Action スロットが用意されています。

| Action 名 | デフォルトバインド |
|-----------|-------------------|
| Trigger1 | キーボード `1` / ゲームパッド South（A ボタン） |
| Trigger2 | キーボード `2` / ゲームパッド East（B ボタン） |
| ... | ... |
| Trigger12 | キーボード `=` ほか |

任意の Expression を任意の `TriggerN` に割り当てることで、キーコンフィグを自由に構成できます。各 Trigger はプレス／リリースに応じて Expression のアクティブ化／非アクティブ化を行います。

### 6.4. キーバインドのカスタマイズ

デフォルトのキーバインドを変更したい場合は `FacialControlDefaultActions.inputactions` を複製してカスタマイズします。

1. Project ウィンドウで `Packages/com.hidano.facialcontrol/Runtime/Adapters/Input/FacialControlDefaultActions.inputactions` を右クリック → **Copy**（または `Ctrl+D` で複製）
2. 複製した Asset を任意の場所（例: `Assets/InputActions/MyFacialActions.inputactions`）へ配置
3. Asset をダブルクリックして Input Actions エディタを開き、`Trigger1〜Trigger12` のバインドを編集
4. `InputBindingProfileSO` の **Action Asset** フィールドに複製した Asset を割り当てる

この方式によりパッケージ同梱の Asset を書き換えることなく、プロジェクト固有のキーコンフィグを維持できます。

## 7. OSC 通信の設定（オプション）

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

## 8. ARKit / PerfectSync の自動検出（オプション）

モデルが ARKit 52 または PerfectSync の BlendShape 命名規則に準拠している場合、自動検出機能を使って Expression と OSC マッピングを自動生成できます。

1. メニュー **FacialControl** → **ARKit Detector** を開く
2. 対象の SkinnedMeshRenderer を指定
3. **検出実行** ボタンをクリック
4. 検出結果を確認し、Expression の自動生成を実行

## 次のステップ

- **JSON スキーマの詳細**: [json-schema.md](json-schema.md) を参照
- **Expression 作成ツール**: メニュー **FacialControl** → **Expression Creator** で BlendShape スライダーを操作しながら Expression を作成可能
- **プロファイル管理**: メニュー **FacialControl** → **Profile Manager** でプロファイル内の Expression を一覧管理
