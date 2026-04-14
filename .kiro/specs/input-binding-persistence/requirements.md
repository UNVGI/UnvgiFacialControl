# Requirements Document

## Introduction

本機能は、InputAction と Expression の紐付けを ScriptableObject（`InputBindingProfileSO`）として Unity プロジェクト内に永続化し、Unity Editor の Inspector から GUI でキーコンフィグを設定できるようにするものである。
また、`FacialInputBinder` MonoBehaviour がプロファイルを読み込んで内部の `InputSystemAdapter` にバインディングを登録することで、コードを一切書かずに表情切り替えのキーコンフィグを完結できる仕組みを提供する。
さらに、デバッグ用コード直書きのサンプル `TestExpressionToggle.cs` を `FacialInputBinder` + `SampleInputBinding.asset` 方式へ置き換え、preview.1 リリースに含める。

## Boundary Context

- **In scope**: `InputBinding` ドメインモデルの定義、`InputBindingProfileSO` ScriptableObject の新設、`FacialInputBinder` MonoBehaviour の新設、Inspector カスタマイズ（`InputBindingProfileSOEditor`）、サンプルシーンの更新、ドキュメント更新
- **Out of scope**: ランタイム Rebinding UI の提供、InputActions Asset の自動生成・変更、`InputSystemAdapter` の変更、音声解析・リップシンク、VRM / タイムライン統合
- **Adjacent expectations**: 既存の `InputSystemAdapter.BindExpression(InputAction, Expression)` API を内部で利用する。`FacialControlDefaultActions.inputactions` の `Trigger1〜Trigger12` 汎用スロット方式は維持する。

## Requirements

### 1. InputBinding ドメインモデル

**Objective:** As a Unity エンジニア, I want InputAction 名と Expression ID の紐付けを Unity 非依存の値型として表現できる, so that ドメイン層がプラットフォーム依存なく型安全にバインディング情報を扱える。

#### Acceptance Criteria

1. The FacialControl shall provide an `InputBinding` struct with `ActionName` (string) and `ExpressionId` (string) read-only fields.
2. When `ActionName` または `ExpressionId` が null または空文字でコンストラクタが呼ばれた場合, the FacialControl shall `ArgumentException` をスローする。
3. The FacialControl shall `InputBinding` に `IEquatable<InputBinding>` を実装し、等価性判定を提供する。
4. The FacialControl shall `InputBinding` を Unity 非依存のイミュータブル値型として定義し、Unity 依存の名前空間を含まない。

---

### 2. InputBindingProfileSO — バインディング設定の永続化

**Objective:** As a Unity エンジニア, I want InputAction と Expression の紐付けを ScriptableObject Asset として保存・管理できる, so that コードを書かずにキーコンフィグをプロジェクト内に永続化できる。

#### Acceptance Criteria

1. The FacialControl shall `InputBindingProfileSO` ScriptableObject を提供し、`[CreateAssetMenu(menuName = "FacialControl/Input Binding Profile")]` で Project ウィンドウから Asset を作成できる。
2. The `InputBindingProfileSO` shall `InputActionAsset` 参照フィールド（`_actionAsset`）を持ち、使用する InputActions Asset を指定できる。
3. The `InputBindingProfileSO` shall ActionMap 名フィールド（`_actionMapName`）を持ち、デフォルト値として `"Expression"` を設定する。
4. The `InputBindingProfileSO` shall ActionName と ExpressionId のペアリストをシリアライズフィールドとして保持する。
5. When バインディング一覧の取得が要求された場合, the `InputBindingProfileSO` shall シリアライズデータを Domain モデル `IReadOnlyList<InputBinding>` に変換して返す。
6. If `_actionAsset` が null の場合, the `InputBindingProfileSO` shall 空のバインディングリストを返す。

---

### 3. FacialInputBinder — バインディングの自動登録

**Objective:** As a Unity エンジニア, I want MonoBehaviour を GameObject にアタッチするだけで `InputBindingProfileSO` の設定に従い表情切り替えが動作する, so that ランタイムコードなしにキーコンフィグを有効にできる。

#### Acceptance Criteria

1. The FacialControl shall `FacialInputBinder` MonoBehaviour を提供し、`[AddComponentMenu("FacialControl/Facial Input Binder")]` で Component メニューから追加できる。
2. The `FacialInputBinder` shall `FacialController` 参照フィールド（`_facialController`）と `InputBindingProfileSO` 参照フィールド（`_bindingProfile`）を Inspector に公開する。
3. When `FacialInputBinder` が `OnEnable` された場合, the `FacialInputBinder` shall `_bindingProfile` の `_actionAsset` をインスタンス化し、対象 ActionMap を有効化した上で、プロファイルの全バインディングを `InputSystemAdapter` を通じて `BindExpression` で登録する。
4. When `_bindingProfile` の `ExpressionId` が `FacialController` の現在のプロファイルに存在しない場合, the `FacialInputBinder` shall 警告ログを出力し、該当バインディングのみスキップして残りのバインディングは継続登録する。
5. If `_bindingProfile` が null の場合, the `FacialInputBinder` shall 警告ログを出力し、バインディング登録を行わない（例外はスローしない）。
6. When `FacialInputBinder` が `OnDisable` された場合, the `FacialInputBinder` shall 全バインディングを解除し（`UnbindAll`）、ActionMap を無効化する。
7. While `FacialController` が未初期化の状態で InputAction がトリガーされた場合, the `FacialInputBinder` shall 表情操作を行わず安全に無視する（`InputSystemAdapter` の既存動作に従う）。

---

### 4. InputBindingProfileSOEditor — Inspector GUI

**Objective:** As a Unity エンジニア, I want `InputBindingProfileSO` の Inspector から ActionMap・Action・Expression をドロップダウンで選択してバインディングを設定できる, so that JSON や コードを手動編集せずにキーコンフィグを GUI で完結できる。

#### Acceptance Criteria

1. The `InputBindingProfileSOEditor` shall UI Toolkit で実装し、`InputBindingProfileSO` の Inspector をカスタマイズする。
2. The `InputBindingProfileSOEditor` shall `InputActionAsset` 参照を設定する ObjectField を表示する。
3. The `InputBindingProfileSOEditor` shall 参照用 `FacialProfileSO` を指定する ObjectField を表示する（エディタ専用・SO 本体には保存しない）。
4. When `InputActionAsset` が設定された場合, the `InputBindingProfileSOEditor` shall その Asset 内の ActionMap 名を ActionMap ドロップダウンに自動列挙する。
5. When ActionMap が選択された場合, the `InputBindingProfileSOEditor` shall その ActionMap 内の Action 名を各バインディング行の Action ドロップダウンに自動列挙する。
6. When `FacialProfileSO` が設定された場合, the `InputBindingProfileSOEditor` shall そのプロファイル内の Expression 名を各バインディング行の Expression ドロップダウンに自動列挙する（保存時は ExpressionId で記録する）。
7. The `InputBindingProfileSOEditor` shall バインディング一覧に「追加」ボタンと各行の「削除」ボタンを提供する。
8. When `InputActionAsset` または ActionMap が変更された場合, the `InputBindingProfileSOEditor` shall バインディング一覧の表示を自動的に再構築する。

---

### 5. サンプルシーンの更新

**Objective:** As a Unity エンジニア, I want サンプルシーンが `FacialInputBinder` + `SampleInputBinding.asset` ベースで動作する, so that コード直書きなしのキーコンフィグの実用例を参照できる。

#### Acceptance Criteria

1. When preview.1 リリース時, the FacialControl shall `TestExpressionToggle.cs` と対応する `.meta` ファイルを削除する。
2. The FacialControl shall `SampleInputBinding.asset`（`InputBindingProfileSO`）を `FacialControl/Assets/Samples/` 配下に提供し、`FacialControlDefaultActions.inputactions` を参照した状態で `Trigger1` がまばたき Expression にバインドされていること。
3. When サンプルシーンが実行された場合, the FacialControl shall キーボード `1` キー押下でまばたき表情がトグル動作することを確認できる。
4. The FacialControl shall サンプルシーンの対象 GameObject から `TestExpressionToggle` コンポーネントを削除し、`FacialInputBinder` コンポーネントを設定した状態とする。

---

### 6. ドキュメント更新

**Objective:** As a Unity エンジニア, I want クイックスタートガイドにキーコンフィグ設定手順が記載されている, so that `FacialInputBinder` を使ったキーコンフィグを自力でセットアップできる。

#### Acceptance Criteria

1. The FacialControl shall `quickstart.md` に「キーコンフィグの設定」セクションを追加し、`InputBindingProfileSO` の作成手順・`FacialInputBinder` の配置と割り当て・`Trigger1〜Trigger12` スロットの説明・カスタマイズ手順（InputActions Asset の複製方法）を含める。
2. The FacialControl shall `README.md` の主要機能一覧に「キーコンフィグの永続化（`InputBindingProfileSO` + `FacialInputBinder`）」を追記する。
3. The FacialControl shall `CHANGELOG.md` の preview.1 エントリに P23 の変更内容を記載する。
