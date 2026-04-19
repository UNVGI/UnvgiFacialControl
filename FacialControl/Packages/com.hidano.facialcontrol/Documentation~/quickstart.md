# クイックスタートガイド

FacialControl を使って 3D キャラクターの表情をリアルタイム制御するまでの手順を解説します。

## 動作要件

- Unity 6000.3 以降
- Animator コンポーネントが設定された 3D キャラクターモデル（FBX / VRM など）
- BlendShape（シェイプキー）を持つ SkinnedMeshRenderer

## 最短手順で動作確認する（5 分コース）

まず動くところを確認したい方向けのチートシートです。詳細な解説は後続の各章を参照してください。

### 推奨モデル

BlendShape を多数持つモデルであれば何でも動作しますが、以下のいずれかを推奨します。

- **VRoid Studio で出力した VRM モデル**（UniVRM / VRM パッケージで Unity にインポート。BlendShape 名: `Fcl_ALL_Joy` など VRM 標準）
- **ARKit 52 / PerfectSync 対応モデル**（BlendShape 名: `browInnerUp`, `eyeBlinkLeft`, `jawOpen` など）
- **ニコニ立体ちゃん（Unity-Chan 等）**（キャラクター独自命名）

本ガイドでは VRM 命名を例にします。ARKit 命名の場合は後述の **8. ARKit / PerfectSync の自動検出** を使うとプロファイルを自動生成できます。

### ① パッケージのインストール

後述の **1. パッケージのインストール** の手順に従って `manifest.json` を編集。

### ② 動作確認用プロファイル JSON を配置

`Assets/StreamingAssets/FacialControl/my_profile.json` を新規作成して以下をコピペします。**BlendShape 名はご使用のモデルに合わせて変更**してください（VRoid の場合はそのままで動作します）。

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
            "id": "11111111-1111-1111-1111-111111111111",
            "name": "smile",
            "layer": "emotion",
            "transitionDuration": 0.25,
            "transitionCurve": {"type": "easeInOut"},
            "blendShapeValues": [
                {"name": "Fcl_ALL_Joy", "value": 1.0}
            ],
            "layerSlots": []
        },
        {
            "id": "22222222-2222-2222-2222-222222222222",
            "name": "angry",
            "layer": "emotion",
            "transitionDuration": 0.25,
            "transitionCurve": {"type": "easeInOut"},
            "blendShapeValues": [
                {"name": "Fcl_ALL_Angry", "value": 1.0}
            ],
            "layerSlots": []
        },
        {
            "id": "33333333-3333-3333-3333-333333333333",
            "name": "blink",
            "layer": "eye",
            "transitionDuration": 0.08,
            "transitionCurve": {"type": "linear"},
            "blendShapeValues": [
                {"name": "Fcl_ALL_Close", "value": 1.0}
            ],
            "layerSlots": []
        }
    ]
}
```

### ③ Unity Editor でのセットアップ

1. **FacialProfileSO の作成**
   - Project ウィンドウで右クリック → **Create** → **FacialControl** → **Facial Profile**
   - Inspector の **Json File Path** に `FacialControl/my_profile.json` を入力
2. **FacialController の追加**
   - シーンにキャラクター（Animator 付き）を配置
   - **Add Component** → **FacialControl** → **Facial Controller**
   - **Profile SO** フィールドに手順 1 で作成した `FacialProfileSO` をドラッグ＆ドロップ
3. **入力バインディングの作成（キーボード操作用）**
   - Project で右クリック → **Create** → **FacialControl** → **Input Binding Profile**
   - Inspector で以下を設定:
     - **Action Asset**: `Packages/FacialControl/Runtime/Adapters/Input/FacialControlDefaultActions.inputactions`（同梱）
     - **Action Map Name**: `Expression`
     - **Facial Profile (Editor Only)**: 手順 1 の `FacialProfileSO`
   - **＋** で行を追加し、`Trigger1` → `smile`、`Trigger2` → `angry`、`Trigger3` → `blink` を割り当て
4. **FacialInputBinder の追加**
   - 同じキャラクターに **Add Component** → **FacialControl** → **Facial Input Binder**
   - **Facial Controller**: 手順 2 の FacialController
   - **Binding Profile**: 手順 3 の InputBindingProfileSO

### ④ 動作確認

Play モードに入り、キーボードの **1 / 2 / 3** キーを押すと、それぞれ smile / angry / blink が発動します。離すと元に戻ります。

遷移しない場合は後述の **トラブルシューティング** を参照してください。

### ⑤ Expression の追加・調整

BlendShape スライダー操作で Expression を作成するには、メニュー **FacialControl** → **Expression 作成** を開きます。GUI で値を確認しながら JSON を更新できます。

---

以下、各セクションで詳細を解説します。

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

> **Note**: キーボードやコントローラから Expression を発火したいだけの場合は、**6. キーコンフィグの設定** の `InputBindingProfileSO` + `FacialInputBinder` を使う方が簡単です。以下は独自のトリガー条件（タイマー・ネットワークイベント等）から切り替える場合の例です。

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
        // プロファイルから Expression を取得
        if (_facialController.IsInitialized && _facialController.CurrentProfile.HasValue)
        {
            var profile = _facialController.CurrentProfile.Value;
            _smileExpression = profile.FindExpressionById("11111111-1111-1111-1111-111111111111");
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

## トラブルシューティング

### 表情がまったく変化しない

- **FacialProfileSO の JSON パスが間違っている**
  - `StreamingAssets/` からの相対パスで指定します（例: `FacialControl/my_profile.json`）
  - Console に `FacialController` からのエラーログが出ていないか確認してください
- **BlendShape 名がモデルと一致していない**
  - BlendShape 名は大文字小文字・記号まで完全一致が必要です
  - Inspector で対象 SkinnedMeshRenderer を選択し、BlendShapes の一覧と JSON の `blendShapeValues[].name` を突き合わせてください
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
