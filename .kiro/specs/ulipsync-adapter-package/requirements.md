# Requirements Document

## Project Description (Input)
com.hidano.facialcontrol.lipsync (uLipSync 連携アダプタパッケージ)

## Introduction

本仕様は、FacialControl コアパッケージ (`com.hidano.facialcontrol`) と uLipSync (ASIO Fork)
パッケージ (`com.hidano.ulipsync-asio` 3.1.5-custom.0) を橋渡しする新規 UPM 姉妹パッケージ
`com.hidano.facialcontrol.lipsync` の要件を定義する。

コアパッケージは既に受け口となる契約 (`Hidano.FacialControl.Domain.Interfaces.ILipSyncProvider`、
`Hidano.FacialControl.Adapters.InputSources.LipSyncInputSource`、`lipsync` レイヤー) を
公開しているため、本パッケージは **既存契約を再定義せず、それを実装する** 立場で uLipSync の
解析結果 (`uLipSync.LipSyncInfo.phonemeRatios`) を BlendShape 値ベクトルへマッピングし、
`AdapterBinding` パターン (`OscAdapterBinding` を参考とする) に従って Inspector から
追加できる形で配布する。

対象プラットフォームはプレリリース時点では Windows PC のみ。ASIO Fork が Windows 限定の
ためであり、将来の音声入力ソース差し替えに備えた設計余地は確保しつつも、本仕様では
クロスプラットフォーム対応を行わない。

## Boundary Context

- **In scope**:
  - `com.hidano.facialcontrol.lipsync` パッケージ本体 (`package.json`, asmdef, README,
    CHANGELOG, Documentation~).
  - `ULipSyncProvider` (`ILipSyncProvider` 実装、`uLipSync.uLipSync.onLipSyncUpdate` 購読、
    `phonemeRatios` の固定長 BlendShape 配列へのマッピング、GC フリーのホットパス)。
  - `PhonemeBlendShapeMapSO` (uLipSync Profile の音素名 → BlendShape 名のペアを保持する
    ScriptableObject)。
  - `ULipSyncAdapterBinding : AdapterBindingBase` (`OnStart` で provider と
    `LipSyncInputSource` を `ctx.InputSourceRegistry` に登録、`OnFixedTick` 無処理、
    `Dispose` で購読解除と Helper の取り外し、`[FacialAdapterBinding(displayName: "uLipSync")]`
    属性付与、引数なしコンストラクタ可能)。
  - Editor 用 `PropertyDrawer`（OSC binding と整合する描画）。
  - Samples~: `MicLipSyncDemo` (Mic 入力)、必要に応じ `AsioLipSyncDemo` (ASIO 入力)。
    各サンプルは FacialCharacterProfileSO・mapping SO・Scene を同梱し、ユーザーは
    自前モデルを `Character` 配下に置くだけで動作する。
  - EditMode 単体テスト (`ULipSyncProvider`)、PlayMode ライフサイクルテスト
    (`ULipSyncAdapterBinding`)。

- **Out of scope**:
  - 音声解析そのもの（uLipSync 側の責務）。
  - `ILipSyncProvider`・`LipSyncInputSource`・`lipsync` レイヤーの再定義／変更。
  - macOS / Linux / モバイル / WebGL 対応。
  - リップシンクのキャリブレーション UI、音素機械学習、ベイクパイプライン
    (uLipSync 側の Editor ウィンドウが提供)。
  - 新規通信プロトコル、新規入力ソース種別の追加。

- **Adjacent expectations**:
  - コアパッケージの `LipSyncInputSource` (silence threshold `1e-4f`、`false` 時に
    `output` 非変更) を変更しない。本パッケージは provider 側で
    `phonemeRatio[i] * normalizedVolume` 形式の値を出力し、無音時に自然に
    閾値未満となる契約を満たす。
  - `OscAdapterBinding` (`com.hidano.facialcontrol.osc`) のライフサイクル・登録手順・
    `PropertyDrawer` 構造に準拠する。
  - 既存の `Hidano.FacialControl.Adapters.Json.Dto.LipSyncOptionsDto` (現状空) を
    forward-compat のため触らない。本仕様では JSON 設定項目を追加しない。

## Requirements

### Requirement 1: パッケージ構成と配布

**Objective:** Unity エンジニアとして、uLipSync 連携を独立した UPM パッケージとして導入し、
コアと OSC と同じインストールフローでプロジェクトに追加できるようにしたい。それにより
リップシンクが不要なプロジェクトには本機能を入れずに済むようにしたい。

#### Acceptance Criteria

1. The `com.hidano.facialcontrol.lipsync` package shall provide a `package.json` declaring
   dependencies on `com.hidano.facialcontrol` (コア) and `com.hidano.ulipsync-asio`
   (3.1.5-custom.0 互換) and on Unity 6000.3.2f1 以降のランタイム。
2. The package shall ship with `Runtime/`, `Editor/`, `Tests/EditMode/`, `Tests/PlayMode/`,
   `Samples~/`, `Documentation~/`, `README.md`, `CHANGELOG.md`, `LICENSE.md` を含む
   標準 UPM レイアウトで構成される。
3. The package shall expose all public types under namespaces rooted at
   `Hidano.FacialControl.LipSync` (`Hidano.FacialControl.LipSync.Adapters` /
   `Hidano.FacialControl.LipSync.Editor` / `Hidano.FacialControl.LipSync.Tests` 等)。
4. The Runtime asmdef shall reference `com.hidano.facialcontrol` (Domain / Application /
   Adapters), `com.hidano.ulipsync-asio` の Runtime asmdef、および `Unity.Animation` 等
   必要最小限のエンジン assembly のみであり、Editor 専用機能を Runtime asmdef へ含めて
   はならない。
5. Where `com.hidano.ulipsync-asio` がプロジェクトに導入されていない、the package shall
   コンパイルエラーまたは Package Manager の依存解決エラーで明示的に未導入を通知する
   (silent failure を避ける)。
6. The package shall be marked Windows-only in its supported platform metadata
   (例: `package.json` の `keywords` / `documentationUrl` および README で明記)。

### Requirement 2: ULipSyncProvider による ILipSyncProvider 実装

**Objective:** uLipSync の解析結果 (`LipSyncInfo.phonemeRatios`) を、コア側
`ILipSyncProvider` の固定長 `Span<float>` 出力契約に橋渡ししたい。

#### Acceptance Criteria

1. The `ULipSyncProvider` class shall implement
   `Hidano.FacialControl.Domain.Interfaces.ILipSyncProvider`.
2. When `uLipSync.uLipSync.onLipSyncUpdate` が発火する、the `ULipSyncProvider` shall
   `LipSyncInfo.phonemeRatios` を内部固定長バッファに反映する。
3. When `GetLipSyncValues(Span<float> output)` が呼ばれる、the `ULipSyncProvider` shall
   内部バッファ内容を `output` の長さ分（不足時は overlap のみ）へコピーし、
   `output` を超える要素は破棄する。
4. The `ULipSyncProvider.BlendShapeNames` shall マッピング SO に登録された BlendShape 名の
   `ReadOnlySpan<string>` を、構築時に確定した順序で返す。
5. While `ULipSyncProvider` がアクティブ、the provider shall `phonemeRatio[i] *
   normalizedVolume` 形式（または等価に無音時に総和が `LipSyncInputSource.SilenceThreshold`
   = `1e-4f` 未満となる形式）で値を出力する。
6. If マッピング SO に存在しない音素キーが `phonemeRatios` に含まれる、then the
   `ULipSyncProvider` shall そのキーを警告ログ無しでスキップする
   (uLipSync 側のプロファイル差異を許容するため)。
7. If マッピング SO に登録された音素キーが当該フレームの `phonemeRatios` に存在しない、
   then the `ULipSyncProvider` shall その BlendShape 値を `0` として扱う。
8. The `ULipSyncProvider` shall provide a constructor that accepts at least
   `(uLipSync.uLipSync source, PhonemeBlendShapeMapSO mapping)` 形式のシグネチャを公開し、
   引数 null 時は `ArgumentNullException` を送出する。
9. When `ULipSyncProvider.Dispose()` が呼ばれる、the provider shall
   `onLipSyncUpdate` の購読を解除し、以降のイベント着信を内部に反映しない。

### Requirement 3: 毎フレーム GC フリーのホットパス

**Objective:** プロジェクト全体の性能要件「毎フレームのヒープ確保ゼロ」を本パッケージでも
保つこと。GC スパイクで配信のフレーム落ちを起こしたくない。

#### Acceptance Criteria

1. While `ULipSyncProvider` がイベント受信および `GetLipSyncValues` 呼び出しを処理する、
   the provider shall ヒープ確保ゼロで動作する（内部バッファ・辞書ルックアップ用配列等は
   構築時にのみ確保する）。
2. While `ULipSyncAdapterBinding` の `OnFixedTick` / `OnUpdate` 等が呼ばれる、the binding
   shall ヒープ確保ゼロで動作する。
3. The EditMode test suite shall `ULipSyncProvider.GetLipSyncValues` を 10000 回呼び出す
   ベンチマークケースを含み、`Unity.PerformanceTesting` または `GC.GetTotalAllocatedBytes`
   差分で 0 byte であることをアサートする。
4. The package shall LINQ・`string.Format`・`new T[]`・`new List<T>()`・`new
   Dictionary<,>()` 等のホットパスでの暗黙アロケーションを避ける実装方針を README または
   Documentation~ に明記する。

### Requirement 4: PhonemeBlendShapeMapSO

**Objective:** uLipSync Profile が出力する音素識別子を BlendShape 名にマッピングする
データを、Editor で編集可能な ScriptableObject として保持したい。Expression 名でなく
音素ベースで設計したい。

#### Acceptance Criteria

1. The `PhonemeBlendShapeMapSO` shall be a `ScriptableObject` 派生クラスで、
   `[CreateAssetMenu]` 属性によりプロジェクトメニューから生成可能であること。
2. The `PhonemeBlendShapeMapSO` shall 「音素名 (string)」と「BlendShape 名 (string)」の
   ペアの一覧を直列化可能な形式で保持し、Inspector から追加・削除・並べ替えできる。
3. The mapping shall uLipSync Profile の音素 ID (例: `A` / `I` / `U` / `E` / `O` / `S`)
   をキーとし、FacialControl の Expression 名はキーとして用いない。
4. If 同一音素キーが重複登録される、then the SO shall 後勝ち（最後に登録された
   BlendShape 名を採用）の動作を行い、Editor で警告アイコンを表示する。
5. The `PhonemeBlendShapeMapSO` shall BlendShape 名に 2 バイト文字・特殊記号
   （プロジェクトの BlendShape 命名規則を固定しない契約）を許容する。
6. When `ULipSyncProvider` が構築される、the provider shall `PhonemeBlendShapeMapSO` の
   登録順序を確定的な BlendShape インデックス順序として採用し、`BlendShapeNames` も
   同順序で返す。

### Requirement 5: ULipSyncAdapterBinding ライフサイクル

**Objective:** Inspector の Add ドロップダウンから「uLipSync」を選んで追加するだけで、
provider・`LipSyncInputSource`・購読の取り回しが自動で完了するようにしたい。

#### Acceptance Criteria

1. The `ULipSyncAdapterBinding` shall be a `[FacialAdapterBinding(displayName: "uLipSync")]`
   属性付きの `AdapterBindingBase` 派生 sealed class で、引数なしコンストラクタを持つ。
2. When `OnStart(BindingContext ctx)` が呼ばれる、the binding shall:
   (a) シーンまたは `ctx.HostGameObject` から `uLipSync.uLipSync` MonoBehaviour を取得または
   生成し、(b) `ULipSyncProvider` と `LipSyncInputSource` を構築し、(c) AdapterSlug を
   キーに `ctx.InputSourceRegistry` に `LipSyncInputSource` を登録する。
3. The `ULipSyncAdapterBinding.OnFixedTick` shall no-op であり、provider はイベント駆動で
   バッファを更新する (uLipSync が自身でフレーム駆動するため)。
4. When `Dispose()` が呼ばれる、the binding shall (a) provider の購読を解除し、
   (b) `ctx.InputSourceRegistry` から登録解除し、(c) `OnStart` で生成した Helper
   MonoBehaviour を `ctx.HostGameObject` から取り外す。
5. If `OnStart` 中に `uLipSync.uLipSync` の参照が解決できない、then the binding shall
   `Debug.LogError` で原因を記録し、ctor / `OnStart` から復帰可能な状態（破壊的副作用
   なし）で終了する。
6. The `ULipSyncAdapterBinding` shall シリアライズ可能なフィールドとして少なくとも
   `PhonemeBlendShapeMapSO` 参照と、`uLipSync.uLipSync` の参照取得方式
   (例: 既存参照 / 自動生成 / 子オブジェクト名指定) の選択値を持ち、Inspector から
   設定可能であること。
7. Where `OscAdapterBinding` のライフサイクル順序 (OnStart → 各 Tick → Dispose) と
   InputSource 登録 API、the `ULipSyncAdapterBinding` shall 同一の呼び出し順・契約に
   準拠する。

### Requirement 6: 音声入力ソースの差し替え可能性

**Objective:** uLipSync は Mic / ASIO 等の `IAudioInputSource` を持つ。本パッケージは
特定の入力ソースに密結合せず、ユーザーが既に Scene に置いた `uLipSync.uLipSync`
インスタンスをそのまま利用できるようにしたい。

#### Acceptance Criteria

1. The `ULipSyncAdapterBinding` shall `uLipSyncMicrophone` と `uLipSyncAsioInput` の
   いずれかを `uLipSync.uLipSync` の入力源として接続済みのシーンで動作可能である。
2. The package shall 自前で音声解析ロジックを実装してはならない。すべての音声入力・
   解析は `com.hidano.ulipsync-asio` 側に委譲する。
3. Where 将来 ASIO 以外の音声入力 (例: VBAN / NDI) に差し替える、the
   `ULipSyncAdapterBinding` shall `uLipSync.uLipSync` のコンポーネント参照層のみで
   抽象化されており、本パッケージのソース変更なく利用者が `uLipSync.IAudioInputSource`
   実装を差し替えられる。

### Requirement 7: マルチキャラクター・複数バインディング動作

**Objective:** 同時に 10 体以上のキャラクターを制御するという全体性能目標を本パッケージ
でも崩さないこと。

#### Acceptance Criteria

1. While 10 体以上の `FacialCharacter` がそれぞれ `ULipSyncAdapterBinding` を持つ、
   the package shall フレーム落ち無く動作可能であり、provider 同士は静的状態を共有しない。
2. The `ULipSyncAdapterBinding` shall AdapterSlug によりインスタンスを識別し、
   `ctx.InputSourceRegistry` への登録キーが他キャラクターと衝突しないよう slug を
   名前空間化する。
3. If 同一 `FacialCharacter` に `ULipSyncAdapterBinding` が複数追加される、then the
   binding 第 2 個目以降は `OnStart` で `Debug.LogError` を出力し、登録をスキップする。

### Requirement 8: Editor PropertyDrawer

**Objective:** Inspector で `ULipSyncAdapterBinding` を編集する際、OSC binding と同等の
見た目・操作感で扱えるようにしたい。

#### Acceptance Criteria

1. The package shall Editor 専用 asmdef (`includePlatforms: ["Editor"]`) 配下に
   `ULipSyncAdapterBinding` 用の `PropertyDrawer` を提供する。
2. The PropertyDrawer shall UI Toolkit ベースで実装され、IMGUI を新規 UI に使わない。
3. The PropertyDrawer shall `OscAdapterBindingDrawer` のフィールド配置・折りたたみ
   挙動・Validation メッセージ表示と整合する見た目で、`PhonemeBlendShapeMapSO` 参照、
   `uLipSync.uLipSync` 参照、AdapterSlug を編集可能とする。
4. If `PhonemeBlendShapeMapSO` 参照が未設定、then the PropertyDrawer shall HelpBox に
   「マッピング未設定」の警告を表示し、ランタイム前に修正を促す。

### Requirement 9: Samples~ パッケージング

**Objective:** ユーザーがモデルを差し込むだけで動作確認できる canonical サンプルを、
UPM Package Manager から Import 可能な形で配布したい。

#### Acceptance Criteria

1. The package shall `Samples~/MicLipSyncDemo/` を含み、`package.json` の `samples` 配列
   から Import 可能であること。
2. The `MicLipSyncDemo` sample shall 以下を同梱する:
   (a) FacialCharacterProfileSO (`ULipSyncAdapterBinding` を Wired 済み)、
   (b) `PhonemeBlendShapeMapSO` の例（uLipSync 同梱の A/I/U/E/O/S プロファイル準拠）、
   (c) Scene。Scene は `Character` という空 GameObject を含み、ユーザーはそこへ自前
   モデル (FBX/VRM) をドロップするだけで動作する。
3. Where ASIO 入力デモが必要な利用者向け、the package shall 別途 `Samples~/AsioLipSyncDemo/`
   を提供してもよい (オプション)。提供する場合は `MicLipSyncDemo` と同等の構成とする。
4. The package shall `Assets/Samples/` 側 (dev project ミラー) との二重管理ルールを
   README または Documentation~ に明記し、編集時に両方を同期する手順を含む。
5. If サンプルを Import せずに利用者が自身でシーン構築する、then the README shall
   最小手順 (Add Binding → Map SO 紐付け → uLipSync 配置) を順序立てて記載する。

### Requirement 10: テスト網羅 (TDD)

**Objective:** プロジェクト全体の TDD 厳守方針を維持し、`OscAdapterBinding` と同等の
テスト面を確保したい。

#### Acceptance Criteria

1. The package shall EditMode 単体テスト `ULipSyncProviderTests` を提供し、以下を網羅する:
   (a) 構築時引数 null での `ArgumentNullException`、(b) `phonemeRatios` 辞書から
   固定長配列への正しいマッピング、(c) マッピング SO に無い音素キーの無視、
   (d) `phonemeRatios` に無い登録音素のゼロ埋め、(e) 無音時に
   `LipSyncInputSource.SilenceThreshold` 未満で総和が collapse することの確認、
   (f) GC アロケーション 0 byte。
2. The package shall PlayMode テスト `ULipSyncAdapterBindingTests` を提供し、
   `OnStart`／`OnFixedTick`／`Dispose` のライフサイクル順序、`InputSourceRegistry`
   への登録・登録解除、Helper MonoBehaviour の追加・取り外し、二重登録時のエラー
   ログ出力を検証する。
3. The PlayMode test surface shall `OscAdapterBindingTests` の検証項目（少なくとも
   ライフサイクル・Registry 操作・例外時の安全停止）を 1 対 1 で対応する形でカバーする。
4. While CI でテストが実行される、the package shall EditMode と PlayMode 両方のテストが
   失敗ゼロで完了することを必須要件とする。
5. The test naming shall プロジェクト命名規則 `{Method}_{Condition}_{Expected}` に従う
   (例: `GetLipSyncValues_SilentFrame_ProducesSubThresholdSum`)。

### Requirement 11: ドキュメントと移行手順

**Objective:** 既存の手動 `uLipSyncBlendShape` 配線で運用していた利用者が、
本パッケージへスムーズに移行できるようにしたい。

#### Acceptance Criteria

1. The package shall README.md を提供し、(a) インストール手順、(b) 最小サンプル手順、
   (c) Windows 限定スコープ、(d) 既存契約 (`ILipSyncProvider`、`LipSyncInputSource`、
   `lipsync` レイヤー) との関係を記載する。
2. The package shall CHANGELOG.md を提供し、Keep a Changelog 形式で初版エントリを
   記録する。
3. Where 利用者が従来 `uLipSyncBlendShape` で BlendShape を手結線していた、the package
   shall `Documentation~/migration-guide.md` を提供し、(a) 旧手結線の取り外し、
   (b) `ULipSyncAdapterBinding` 追加、(c) `PhonemeBlendShapeMapSO` 作成、
   (d) FacialCharacterProfileSO の `lipsync` レイヤー設定、の 4 ステップを順序立てて
   記載する。
4. The package documentation shall すべて日本語で記述する (プロジェクトの言語契約に従う)。
5. The package shall preview スコープでの破壊的変更可能性を README に明記する。

### Requirement 12: コーディング規約および非機能契約

**Objective:** プロジェクト共通の規約 (CLAUDE.md / steering) と整合し、コードレビューで
規約違反を理由とする差し戻しを発生させないこと。

#### Acceptance Criteria

1. The package source code shall 4 スペースインデント・改行時の中括弧・明示的アクセス修飾子
   (public / private)・`PascalCase` クラス／enum・`I` プレフィックスインターフェース・
   `_camelCase` プライベートフィールドの規約に従う。
2. The package shall コメント・XML ドキュメンテーション・README・CHANGELOG・
   `Documentation~/` を日本語で記述する。
3. The package shall エラーハンドリングを Unity 標準ログ (`Debug.Log` / `Debug.Warning` /
   `Debug.Error`) のみで行い、独自例外型を新設しない (標準例外
   `ArgumentNullException` 等は許容する)。
4. The package shall レンダーパイプライン非依存・シェーダー非依存・物理演算非依存の
   コア契約を維持する (uLipSync 経由の音声解析以外に外部実装に依存しない)。
5. The package shall すべての public 型を `Hidano.FacialControl.LipSync` 名前空間
   ルート配下に配置する。
