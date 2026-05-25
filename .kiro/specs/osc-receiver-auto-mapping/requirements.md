# Requirements Document

## Project Description (Input)
OscReceiverAdapterBinding の Mappings 列挙を、送信側 heartbeat (/_facialcontrol/blendshape_names) と受信側モデルの BlendShape 一覧から自動生成する。現状 Mappings は Inspector で手入力必須（mode=blendShape, expressionId, addressPattern を 1 件ずつ）で、200 個の BlendShape を持つモデルでは現実的に運用できない。本 spec では (a) heartbeat 受信時に runtime mapping を動的拡張する経路を追加し、(b) Mappings が空 / 部分入力 / 完全手入力の 3 ケースを統一的に扱い、(c) アドレスプリセット（VRChat 形式 /avatar/parameters/{name} と ARKit 形式 /ARKit/{name}）の推定方法を確立する。スコープ: OscReceiverAdapterBinding、OscInputSource、HeartbeatConsistencyChecker、heartbeat メッセージフォーマット（preset 情報の追加可否含む）、Gaze は対象外（heartbeat に Gaze id を含まないため別途検討）。非機能: runtime 中の mapping 拡張で GC スパイクを起こさない、既存の手動 Mappings との後方互換維持。受け入れ条件は OscReceiverDemo の Mappings を空にしても OscOutputDemo からの送信が受信側モデルの BlendShape へ反映されること。

## Introduction
本 spec は `OscReceiverAdapterBinding` の `Mappings` 列挙を、送信側 heartbeat (`/_facialcontrol/blendshape_names`) と受信側モデルの BlendShape 名一覧から自動生成する仕組みを定義する。現状の手入力運用は 200 シェイプ規模のモデルでは破綻するため、heartbeat 受信を起点として runtime に mapping を動的拡張する経路を追加する。同時にアドレスプリセット (VRChat 形式 `/avatar/parameters/{name}` と ARKit 形式 `/ARKit/{name}`) の推定方法を確立し、`Mappings` が空 / 部分入力 / 完全手入力の 3 ケースを統一フローで扱う。Gaze は heartbeat に id を含まないため対象外とし、既存の手動 mapping との後方互換を維持する。

## Boundary Context

- **In scope**:
  - `OscReceiverAdapterBinding` の Normal_BlendShape mapping 自動生成 (heartbeat 駆動)
  - `HeartbeatConsistencyChecker` の receiver side 構築 / 拡張経路 (mesh BlendShape 名 ↔ sender heartbeat 名の照合)
  - `OscInputSource` の mapping 拡張対応 (runtime 中の mapping 件数増加と内部バッファ・mask 再構築)
  - アドレスプリセット推定 (VRChat `/avatar/parameters/{name}` / ARKit `/ARKit/{name}` / カスタム)
  - heartbeat メッセージフォーマットへの preset 情報追加可否の決定 (preset 識別子の payload 追加 or アドレス形状からの推定)
  - `OscReceiverDemo` / `OscOutputDemo` サンプルの空 Mappings での疎通確認
- **Out of scope**:
  - Gaze (`Gaze_VRChat_XY` / `Gaze_ARKit_8BS`) の自動 mapping (heartbeat に Gaze id を含まないため別 spec)
  - 送信側 (`OscOutputAdapterBinding`) の自動構成 (今 spec は受信側に閉じる)
  - heartbeat 以外の経路 (例: シーン外 JSON プリセット配信) からの mapping 投入
  - 表情 (Expression) id 体系の改廃
  - `OscRuntimeSettingsSO` のスキーマ破壊的変更
- **Adjacent expectations**:
  - 送信側 heartbeat は `BlendShapeNamesAddress` (`/_facialcontrol/blendshape_names`) を継続して送出する (preset 情報を追加する場合も既存 `string[]` payload を破壊しない)
  - `IInputSourceRegistry` への登録 slug `osc` は予約のまま維持し、本 spec で新 slug を導入しない
  - Domain 層は Unity 非依存契約を維持し、自動 mapping ロジックは Adapters 層に閉じ込める
  - 既存の手動 `OscMappingEntry` (mode=`Normal_BlendShape`) を含む `FacialAdapterBindingCollectionSO` の構成は無改修で動作する (後方互換)

## Requirements

### Requirement 1: 空 Mappings での自動 BlendShape mapping 生成
**Objective:** As a Unity エンジニア (受信側統合担当), I want `OscReceiverAdapterBinding.Mappings` を空にしたまま受信を開始できるようにしたい, so that 200 シェイプ規模のモデルでも Inspector への手入力を避けて素早く疎通させられる。

#### Acceptance Criteria
1. When `OscReceiverAdapterBinding.OnStart` が実行されたとき、`Mappings` が空かつ `Mappings` 由来の Gaze entry も無い場合、The OscReceiverAdapterBinding shall heartbeat 待ちで OSC 受信を起動し、`OscInputSource` 登録を heartbeat 到着まで延期する。
2. When `OscReceiverAdapterBinding` が `BlendShapeNamesAddress` heartbeat メッセージを最初に受信したとき、The OscReceiverAdapterBinding shall heartbeat の BlendShape 名と `AdapterBuildContext.BlendShapeNames` (受信側モデル) の積集合から `Normal_BlendShape` runtime mapping を生成し、`OscInputSource` を `IInputSourceRegistry` に登録する。
3. When heartbeat 起点で runtime mapping が生成されたとき、The OscReceiverAdapterBinding shall 各 mapping の `addressPattern` を推定アドレスプリセット (Requirement 3) に従って組み立て、`expressionId` を BlendShape 名に一致させる。
4. While Mappings が空のまま heartbeat 未受信であるとき、The OscReceiverAdapterBinding shall `OscInputSource` を未登録のまま保持し、`Aggregator` に対して BlendShape 値を一切出力しない。
5. If 受信した heartbeat と受信側 mesh の BlendShape 名に共通要素が一つも存在しない場合, the OscReceiverAdapterBinding shall mapping を生成せず、`Debug.LogWarning` で「heartbeat と mesh BlendShape が一致しません」を 1 度だけ出力する。
6. The OscReceiverAdapterBinding shall heartbeat 駆動で生成された runtime mapping を、後続 heartbeat 受信時に再評価できる構造で保持する (Requirement 4 の差分更新前提)。

### Requirement 2: 手入力 Mappings / 部分入力 Mappings との統一フロー
**Objective:** As a Unity エンジニア, I want Mappings が空 / 部分入力 / 完全手入力のいずれでも同じ起動シーケンスで処理されるようにしたい, so that 既存プロジェクトを破壊せず段階的に自動化に移行できる。

#### Acceptance Criteria
1. When `OscReceiverAdapterBinding.OnStart` 時点で `Mappings` に `Normal_BlendShape` entry が 1 件以上含まれているとき、The OscReceiverAdapterBinding shall 手入力 entry を runtime mapping として優先採用し、`OscInputSource` を即時登録する (既存挙動を維持)。
2. When 手入力 `Normal_BlendShape` mapping が存在し、かつ後続で heartbeat が受信されたとき、The OscReceiverAdapterBinding shall heartbeat に含まれる BlendShape 名のうち手入力 entry でカバーされていないものを追加 runtime mapping として登録する (部分入力ケース)。
3. While 手入力 mapping と heartbeat 由来 mapping が共存しているとき、The OscReceiverAdapterBinding shall 同一 `expressionId` に対して手入力 entry の `addressPattern` を優先し、heartbeat 由来の推定値で上書きしない。
4. If `Mappings` に `Gaze_VRChat_XY` / `Gaze_ARKit_8BS` entry のみが含まれ `Normal_BlendShape` entry が含まれない場合, the OscReceiverAdapterBinding shall 既存の Gaze 結線を維持しつつ、heartbeat 受信時に自動 BlendShape mapping 生成 (Requirement 1) を実行する。
5. The OscReceiverAdapterBinding shall 手入力 entry / heartbeat 由来 entry を区別できる内部状態を保持し、Inspector 表示や診断ログから出自を識別可能にする (具体的な UI 表現は design phase で確定)。
6. The OscReceiverAdapterBinding shall `OscMappingEntry` のシリアライズ済みフィールドを破壊的変更せず、既存 `FacialAdapterBindingCollectionSO` アセットの再 import なしで起動可能とする。

### Requirement 3: アドレスプリセット推定 (VRChat / ARKit / カスタム)
**Objective:** As a Unity エンジニア, I want heartbeat 起点の mapping 生成時に VRChat / ARKit / カスタム形式のいずれかから OSC アドレスを推定したい, so that VRChat 互換クライアントや ARKit 系トラッカからの送信を Inspector 手入力なしで受信できる。

#### Acceptance Criteria
1. The OscReceiverAdapterBinding shall アドレスプリセットとして少なくとも以下の 3 種を識別する: VRChat 形式 (`/avatar/parameters/{name}`), ARKit 形式 (`/ARKit/{name}`), カスタム形式 (上記 2 種に該当しない任意プレフィックス)。
2. When heartbeat payload からプリセット種別を判定するとき、The OscReceiverAdapterBinding shall (a) heartbeat payload に preset 識別子が含まれていればそれを採用し、(b) 含まれていなければ heartbeat に含まれる名前のうち先頭エントリの命名規則 (例: ARKit 名一覧との一致率) から推定する。
3. When プリセット種別が VRChat と判定されたとき、The OscReceiverAdapterBinding shall 各 BlendShape 名 `{name}` に対して `/avatar/parameters/{name}` を `addressPattern` として割り当てる。
4. When プリセット種別が ARKit と判定されたとき、The OscReceiverAdapterBinding shall ARKit 52 / PerfectSync 標準名に一致する BlendShape へ `/ARKit/{name}` を割り当て、それ以外の名前は `/avatar/parameters/{name}` (VRChat フォールバック) を割り当てる。
5. When プリセット種別がカスタムと判定されたとき、The OscReceiverAdapterBinding shall heartbeat payload に含まれる `address prefix` 情報 (Requirement 5) を使ってアドレスを組み立て、prefix 情報が無ければ VRChat 形式へフォールバックし `Debug.LogWarning` で fallback 採用を通知する。
6. If 推定結果として同一 BlendShape 名に対して複数候補アドレスが生成された場合, the OscReceiverAdapterBinding shall (a) heartbeat payload に明示された preset を最優先し、(b) 明示が無い場合は VRChat 形式を採用し、衝突発生を `Debug.LogWarning` で 1 度だけ通知する。
7. The OscReceiverAdapterBinding shall ARKit 標準名一覧を Domain / Adapters の既存定義 (例: `PerfectSyncEyeLook` 等) と整合した単一情報源から取得する。

### Requirement 4: Runtime 中の mapping 拡張と GC スパイク回避
**Objective:** As a Unity エンジニア (パフォーマンス担当), I want heartbeat 受信時の mapping 動的拡張で毎フレーム GC アロケーションを増やさないようにしたい, so that FacialControl の「毎フレームのヒープ確保ゼロ目標」を破らずに自動 mapping を導入できる。

#### Acceptance Criteria
1. The OscReceiverAdapterBinding shall heartbeat 由来 mapping の生成・拡張・更新を毎フレームでは行わず、heartbeat 受信イベントもしくは内容変化を検出した時点でのみ実行する。
2. While heartbeat 内容が前回と同一であるとき、The OscReceiverAdapterBinding shall mapping 配列・mask BitArray・`OscDoubleBuffer` を新規確保せず、既存インスタンスを再利用する。
3. When heartbeat 内容が変化し runtime mapping の再構築が必要になったとき、The OscReceiverAdapterBinding shall mapping 配列確保・`OscDoubleBuffer` リサイズ・`OscInputSource` 差し替えをメインスレッドで実行し、受信スレッドの hot path でアロケーションを発生させない。
4. The OscInputSource shall `TryWriteValues` 1 回あたりの managed heap 確保 0 byte を維持する (mapping 拡張後も `_mappingIndexToMeshIndex` / mask 再構築は OnStart またはメインスレッドの再構築タイミングで完結させる)。
5. If heartbeat の BlendShape 名件数が `OscDoubleBuffer` の現在サイズを超える場合, the OscReceiverAdapterBinding shall バッファを単一のメインスレッド処理で再確保し、再確保中の受信メッセージを取りこぼさない (旧バッファに書込中の受信値を新バッファへ引き継ぐ手段を design phase で定義)。
6. The HeartbeatConsistencyChecker shall mapping 件数変化に追随して `SkipMask` / `ContributeMask` の長さを再構築できる経路を提供する (現状は constructor 渡しのため拡張点を新設する)。

### Requirement 5: Heartbeat メッセージフォーマット拡張可否の決定
**Objective:** As a プロトコル設計者, I want `/_facialcontrol/blendshape_names` heartbeat に preset 識別子を追加するか否かを本 spec で確定したい, so that 受信側のプリセット推定ロジックを安定して実装できる。

#### Acceptance Criteria
1. The 本 spec shall heartbeat payload を「BlendShape 名 `string[]` のみ (現状互換)」「BlendShape 名 + preset 識別子 (拡張案)」のいずれの方針を採用するかを design phase 完了までに決定する。
2. When 既存の送信実装が string 配列のみを送出している場合、The OscReceiverAdapterBinding shall preset 識別子が欠落した heartbeat を不正扱いせず、Requirement 3.2(b) の推定ロジックにフォールバックする。
3. Where heartbeat 拡張方針として preset 識別子を追加する設計が選択されたとき、The 送信側 (本 spec 範囲外実装) shall 既存 receiver が `string` 以外の payload を読み飛ばせるよう、追加 payload を後方互換性のあるエンコーディング (例: 末尾追加・別 OSC タイプタグ) で送出する。
4. The OscReceiverAdapterBinding shall preset 識別子として少なくとも `vrchat` / `arkit` / `custom` の 3 値を解釈できる。
5. If preset 識別子として未知の文字列を受信した場合, the OscReceiverAdapterBinding shall `Debug.LogWarning` で未知 preset を通知し、Requirement 3.2(b) の名前ベース推定にフォールバックする。
6. The OscReceiverAdapterBinding shall heartbeat payload のスキーマ判定結果を `OscRuntimeSettingsSO` を介さず runtime 内部状態として保持し、シリアライズ済みアセットの破壊的変更を伴わない。

### Requirement 6: 既存実装との後方互換と Gaze 結線維持
**Objective:** As a 既存 OSC サンプル / ユーザープロジェクトの保守担当, I want 自動 mapping 追加によって既存の手入力 mapping / Gaze 結線 / sender identity / staleness が壊れないことを保証したい, so that 段階的にバージョンアップしても運用が止まらない。

#### Acceptance Criteria
1. The OscReceiverAdapterBinding shall 既存の `OscMappingEntry` (mode=`Normal_BlendShape`) のみで構成された `Mappings` を持つ binding を、現行バージョンと同一の起動シーケンス・登録順序・mask 構成で動作させる。
2. The OscReceiverAdapterBinding shall 既存の Gaze entry (`Gaze_VRChat_XY` / `Gaze_ARKit_8BS`) の解釈・`GazeVector2InputSource` 登録・bundle accumulator 経路を改変しない。
3. The OscReceiverAdapterBinding shall `SenderIdentity` / `ZombieEvictionPolicy` / `OscBundleAccumulator` / `FailSafeMode` の挙動を本 spec の変更で退行させない (既存 PlayMode 統合テストが緑のまま維持されること)。
4. While heartbeat 由来の auto mapping が登録された状態であるとき、The HeartbeatConsistencyChecker shall sender / receiver の名前差分検出と `Debug.LogWarning` の 1 度だけログ機構を継続的に提供する。
5. If `OscRuntimeSettingsSO.ReceiverEnabled` が false の場合, the OscReceiverAdapterBinding shall heartbeat 受信も auto mapping 生成も実行せず、現行の「OSC Adapter は起動しません」warning を維持する。
6. The OscReceiverAdapterBinding shall `Dispose` 時に heartbeat 由来 runtime mapping / 拡張済み mask / 再確保したバッファを全て解放し、leak を残さない。

### Requirement 7: 受け入れ条件 — OscReceiverDemo の空 Mappings 疎通
**Objective:** As a プレリリース利用者 (サンプル動作確認担当), I want `OscReceiverDemo` の `Mappings` を空にしたまま `OscOutputDemo` からの送信が受信側モデルの BlendShape に反映されることを確認したい, so that 本 spec の主目的が達成されたことを誰でも再現確認できる。

#### Acceptance Criteria
1. When `OscReceiverDemo` Scene を `Mappings` 空の状態で起動し、`OscOutputDemo` Scene から VRChat 形式 (`/avatar/parameters/{name}`) で送信したとき、The OscReceiverDemo shall heartbeat 受信後に該当 BlendShape 名を持つ受信側 `SkinnedMeshRenderer` の weight を送信値で更新する。
2. When `OscReceiverDemo` Scene を `Mappings` 空の状態で起動し、`OscOutputDemo` 相当の ARKit 形式 (`/ARKit/{name}`) 送信を行ったとき、The OscReceiverDemo shall ARKit 標準名に一致する BlendShape weight を送信値で更新する。
3. While `OscReceiverDemo` が空 `Mappings` で受信中であるとき、The OscReceiverDemo shall PlayMode 内で `Debug.LogError` を出力せず、heartbeat 未受信フェーズでは BlendShape weight を初期値のまま保持する。
4. If 送信側が heartbeat を一度も送出しない場合, the OscReceiverDemo shall BlendShape weight を一切変更せず、`OscInputSource` を未登録のまま保持する。
5. The 本 spec shall 上記受け入れ条件を検証する PlayMode 統合テスト (例: `OscReceiverAdapterBindingAutoMappingIntegrationTests` 名称は design phase で確定) を提供する。
6. The OscReceiverDemo / OscOutputDemo サンプル README shall 空 `Mappings` 運用と auto mapping の動作条件 (heartbeat 受信が前提) を 1 段落以上で説明する。
