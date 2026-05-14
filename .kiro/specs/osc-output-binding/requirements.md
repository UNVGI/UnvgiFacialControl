# Requirements Document

## Project Description (Input)
FacialControl の OSC 送信機能を AdapterBinding として正式統合する spec。

## 背景
現状 com.hidano.facialcontrol.osc には OscSender / OscSenderHost クラスが実装済みで、uOSC クライアントをラップした実 UDP 送信機構（SendAll / SendSingle）と PlayMode ループバックテストは存在する。一方、これらは AdapterBinding として結線されておらず、Inspector の Add ドロップダウンから選択できず、FacialController の合成後 BlendShape 値や Gaze 入力 Vector2 を吸い出す経路も無いため、プロダクション統合は未完了の状態にある。

## ユースケース
- VTuber 配信などで「ひとつの表情システム → 複数のキャラ描画システム」へリアルタイム配信し、描画側を冗長構成にする
- Unity でキャプチャ／編集した表情を UnrealEngine など Unity 外プラットフォームへ転送し、クロスエンジンでの表情同期を実現する

## スコープ（実装すべきもの）
1. **Domain 層**: FacialController の合成後 BlendShape 値、および各 GazeBindingConfig に対応する Gaze 入力 Vector2 を観察できる仕組み（`IFacialOutputObserver` / `FacialOutputBus` 的なもの）を Domain 純度を保ったまま新設。GC-free（`ReadOnlySpan<float>` および軽量構造体ベース受け渡し）。
2. **OscSenderAdapterBinding**: `[Serializable]` + `[FacialAdapterBinding(displayName: "OSC Sender")]` 具象。OnStart で `OscSenderHost` を AddComponent + Configure、Domain bus を購読し OnLateTick 等で BlendShape 配列と Gaze Vector2 群を SendAll。
3. **複数送信先**: 1 binding で複数 endpoint への同報送信に対応（`List<EndpointConfig>` 形式）。BlendShape も Gaze も同じ endpoint 群へ流す。
4. **アドレス形式プリセット（案 B）**: mapping エントリは BlendShape 名 / Gaze expressionId のみを保持し、送信時に endpoint のプリセット enum と組み合わせて完全な OSC アドレス文字列を組み立てる。BlendShape は `/avatar/parameters/{name}`（VRChat 互換）/ `/ARKit/{name}`（ARKit 互換）等、Gaze Vector2 は各プリセットに対応した別アドレス規約で送信する。
5. **ループ防止**: 同一プロセス内で OSC 受信 binding と送信 binding が同居した場合に無限ループしないための loopback 抑制ポリシー（**既定 ON / 明示無効化可能**）。
6. **JSON 永続化**: `OscSenderOptionsDto` + 既存 System.Text.Json 経路への統合、JSON スキーマドキュメント追記。Gaze 送信構成も DTO に含める。
7. **Editor**: `OscSenderAdapterBindingDrawer`（UI Toolkit / PropertyDrawer）で endpoint 一覧、BlendShape mapping、Gaze 送信構成を編集。
8. **テスト**: EditMode（DTO 往復、Domain bus 単体、BlendShape / Gaze 双方の通知）、PlayMode（FacialController → OscSender → OscReceiver E2E、複数 endpoint 同報、loopback 抑制、Gaze Vector2 送信検証）。
9. **サンプル**: `Samples~/OscOutputDemo`（および `Assets/Samples` 二重管理）と README / CHANGELOG / docs/work-procedure.md 更新。

## Non-Goals
- VRM 対応（別マイルストーン）
- UDP multicast / broadcast の独自実装（unicast 複数送信先で代替）
- 音声ストリーム / リップシンク音声の OSC 送出（リップシンク値のみ対象）
- OSC bundle / timetag の高度な対応（個別メッセージ送信で十分）
- 既存 OscReceiver / OscAdapterBinding（受信側）の仕様変更
- **Gaze Vector2 の OSC 受信側 binding の実装は本 spec の対象外**。送信フォーマット仕様は本 spec で確定するが、Vector2 を受け取って `GazeBindingConfig` を駆動する受信 binding は後続 spec で別途実装する
- ボーンの世界回転 / 位置値そのものの OSC 送出（Gaze は入力 Vector2 ベースのみ。受け側はその Vector2 から自モデルのボーン姿勢を再合成する責務を持つ）

## 制約
- 既存パッケージ `com.hidano.facialcontrol.osc` に追加実装する形を取る（新パッケージは切らない）
- クリーンアーキテクチャ（Domain / Application / Adapters）の依存方向を維持。Domain は Unity / uOSC 非依存
- 毎フレームのヒープ確保ゼロを目標
- preview.2 以降の機能として位置付ける（preview.1 ターゲットには含めない）

## 参考ファイル
- 既存送信機構: `FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/OSC/OscSender.cs`, `OscSenderHost.cs`
- 既存受信 binding（実装ひな型として参考）: `FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/AdapterBindings/OscAdapterBinding.cs`
- AdapterBinding 基底: `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Domain/Adapters/AdapterBindingBase.cs`
- Gaze 仕様: `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/ScriptableObject/GazeBindingConfig.cs`, `Runtime/Adapters/Bone/GazeBoneBinding.cs`
- 既存ループバックテスト: `FacialControl/Packages/com.hidano.facialcontrol.osc/Tests/PlayMode/Integration/OscSendReceiveTests.cs`

## Requirements

### Requirement 1: Domain 層の表情出力バス（BlendShape 値 + Gaze Vector2）
**Objective:** As a FacialControl コアの利用者（OSC 送信 binding 実装者）, I want FacialController の合成後 BlendShape 値および各 `GazeBindingConfig` に対応する Vector2 入力値を Domain 純度を保ったまま観察できる仕組み, so that Adapters 層から Unity / uOSC 非依存な契約で表情の全状態（BlendShape + 視線）を毎フレーム読み取れる

#### Acceptance Criteria
1. The Facial Output Bus shall be defined in `Hidano.FacialControl.Domain` namespace and shall not reference any UnityEngine, uOSC, or Adapters-layer type.
2. When FacialController が 1 フレームのレイヤー合成を完了した直後, the Facial Output Bus shall 全 BlendShape の合成後値を `ReadOnlySpan<float>` として登録済みオブザーバへ通知する。
3. When FacialController が 1 フレームの Gaze 入力評価を完了した直後, the Facial Output Bus shall 各 `GazeBindingConfig` の `expressionId` と対応する Vector2 (x, y) のスナップショットを登録済みオブザーバへ通知する。
4. The Facial Output Bus shall Gaze Vector2 を Domain 表現の軽量構造体（例: `(float x, float y)` ペア or Domain 専用 `GazeSnapshot` 構造体）として表現し、`UnityEngine.Vector2` を Domain 層へ持ち込まない。
5. The Facial Output Bus shall BlendShape 値配列および Gaze スナップショットコレクションを内部バッファとして再利用し、毎フレームのヒープ確保をゼロにする。
6. When 利用者がオブザーバ（`IFacialOutputObserver` 相当）を登録/解除する API を呼び出した時, the Facial Output Bus shall 当該フレームの通知タイミング前後で安全に追加/削除を反映し、列挙中の並行変更例外を発生させない。
7. While オブザーバが 1 件も登録されていない, the Facial Output Bus shall 通知処理を skip し、合成後値配列のコピーや Gaze スナップショット構築、列挙コストを発生させない。
8. If 通知対象のオブザーバが例外をスローした場合, the Facial Output Bus shall その例外を Unity 標準ログ（`Debug.LogException` 相当の Adapters 層フック）に渡したうえで他オブザーバへの通知を継続する。
9. The Facial Output Bus shall BlendShape インデックスの並び順を FacialController の内部順序と一致させ、`OscMapping` 配列とインデックス対応で参照可能にする。
10. The Facial Output Bus shall Gaze Vector2 スナップショットを `expressionId`（`GazeBindingConfig.expressionId` と同値）でキー付けし、Adapters 層が任意の `GazeBindingConfig` 集合と join できるよう構造化する。
11. The Facial Output Bus shall Gaze 入力源が未接続のフレームでは当該 `expressionId` のスナップショットを通知対象から除外する（または「未更新」フラグを付与する）方針を一貫して採用する。

### Requirement 2: OscSenderAdapterBinding の AdapterBinding lifecycle 結線
**Objective:** As a FacialControl 利用者, I want OSC 送信機能を Inspector の Add ドロップダウンから他の binding と同じ手順で追加・除去できるよう, so that 既存 `OscAdapterBinding`（受信側）と対称な UX で OSC 送信を有効化できる

#### Acceptance Criteria
1. The OscSenderAdapterBinding shall `[Serializable]` 属性および `[FacialAdapterBinding(displayName: "OSC Sender")]` 属性を付与した `AdapterBindingBase` の sealed 具象として実装される。
2. The OscSenderAdapterBinding shall パラメータレスコンストラクタを公開し、Inspector の Add ドロップダウンから `Activator.CreateInstance` 経由で生成可能にする。
3. When `OnStart(in AdapterBuildContext ctx)` が呼び出された時, the OscSenderAdapterBinding shall `ctx.HostGameObject` に `OscSenderHost` を `AddComponent` し、`Configure(endpoint, port, mappings)` を呼び出して送信機構を起動する。
4. When `OnStart` 後に Domain 層の Facial Output Bus が利用可能な状態である時, the OscSenderAdapterBinding shall 自身を `IFacialOutputObserver` として登録し、合成後 BlendShape 値および Gaze Vector2 の双方の購読を開始する。
5. If `ctx.HostGameObject` が null、または BlendShape mapping と Gaze 送信構成が両方とも空の場合, the OscSenderAdapterBinding shall 送信を開始せず Unity 標準ログに警告を出力し、`OnStart` を no-op として安全に終了する。
6. When `OnLateTick(deltaTime)` が呼び出された時, the OscSenderAdapterBinding shall 直近フレームで受信した post-blend BlendShape 値配列を `OscSenderHost.SendAll(...)` で全送信先に同報送信する。
7. When `OnLateTick(deltaTime)` が呼び出された時, the OscSenderAdapterBinding shall 直近フレームで受信した Gaze Vector2 群を、各 endpoint のアドレス形式プリセットに従って構築した OSC アドレスに同報送信する。
8. When `Dispose()` が呼び出された時, the OscSenderAdapterBinding shall Facial Output Bus からのオブザーバ登録を解除し、`OscSenderHost` を `Object.Destroy` で破棄したうえで内部状態を初期化済みフラグごとリセットする。
9. The OscSenderAdapterBinding shall `Slug` を `AdapterBindingBase.Slug` 規約に従って保持し、空 / 規約違反の場合は警告ログを出力したうえで起動を断念する。

### Requirement 3: 複数送信先（multi-endpoint unicast）の同報送信
**Objective:** As a VTuber 配信向け冗長構成を構築する Unity エンジニア, I want 1 つの OSC 送信 binding に複数の送信先（IP/port）を登録して BlendShape 値と Gaze Vector2 を同時に配れる仕組み, so that マルチキャスト / ブロードキャスト機構を使わずに複数描画システムへリアルタイム配信できる

#### Acceptance Criteria
1. The OscSenderAdapterBinding shall 送信先を `List<EndpointConfig>`（または同等の `[Serializable]` コレクション）として保持し、任意件数の unicast endpoint を構成可能にする。
2. The EndpointConfig shall 少なくとも IP/host 文字列、UDP ポート番号、有効/無効フラグ、アドレス形式プリセット（VRChat 形式 / ARKit 形式）を保持する。
3. When 利用者が複数の endpoint を有効化した状態で `OnLateTick` が呼び出された時, the OscSenderAdapterBinding shall 各 endpoint に対して同一フレームの合成後 BlendShape 値および Gaze Vector2 を unicast で送信する。
4. The OscSenderAdapterBinding shall endpoint ごとに独立した `OscSenderHost` インスタンス（または同等の送信スロット）を確保し、1 endpoint の起動失敗が他 endpoint の送信を阻害しないようにする。
5. If 同一 endpoint（IP+port の組）が複数登録された場合, the OscSenderAdapterBinding shall 1 回の警告ログを出力したうえで重複を 1 つに正規化して送信する。
6. Where endpoint が無効化フラグで disabled に設定されている場合, the OscSenderAdapterBinding shall その endpoint への送信を skip し、他 endpoint への送信は継続する。
7. The OscSenderAdapterBinding shall 送信先件数が 0 件の場合に Unity 標準ログで警告を出力し、`OnStart` を no-op として終了する。
8. The OscSenderAdapterBinding shall UDP multicast / broadcast アドレスを独自に解決・送信する処理を実装しない（unicast での複数 endpoint 構成のみで要求を満たす）。

### Requirement 4: アドレス形式プリセット（案 B: プリセット enum + 名前から組み立て）
**Objective:** As a 既存 VRChat / ARKit エコシステムと相互運用する Unity エンジニア, I want endpoint ごとに OSC アドレス形式（VRChat / ARKit）をプリセットで切り替えると BlendShape も Gaze も全自動で対応するアドレス文字列が組み立てられる仕組み, so that 「endpoint A は VRChat 互換、endpoint B は ARKit 互換」を 1 クリックで切り替えられる

#### Acceptance Criteria
1. The OscSenderAdapterBinding shall mapping 1 件あたり完全な OSC アドレスを保存せず、BlendShape 名（および Gaze の場合は `expressionId`）のみを保存し、送信時に endpoint のプリセット enum と組み合わせて完全なアドレス文字列を組み立てる（案 B）。
2. The OscSenderAdapterBinding shall endpoint ごとに「VRChat 形式」「ARKit 形式」を選択可能なプリセット enum を提供する。
3. When endpoint のプリセットが VRChat 形式に設定されている時, the OscSenderAdapterBinding shall その endpoint への BlendShape 送信アドレスを `/avatar/parameters/{BlendShape 名}` の形式で構築する。
4. When endpoint のプリセットが ARKit 形式に設定されている時, the OscSenderAdapterBinding shall その endpoint への BlendShape 送信アドレスを `/ARKit/{BlendShape 名}` の形式で構築する。
5. When endpoint のプリセットが VRChat 形式に設定されている時, the OscSenderAdapterBinding shall Gaze Vector2 を VRChat 互換のアドレス規約（x 成分と y 成分の 2 メッセージ、`expressionId` をパラメータ名へ反映する）で送信する。具体的なアドレス文字列規約は Design フェーズで確定する。
6. When endpoint のプリセットが ARKit 形式に設定されている時, the OscSenderAdapterBinding shall Gaze Vector2 を ARKit 互換のアドレス規約（x 成分と y 成分の 2 メッセージ、ARKit 系統のパス階層に揃える）で送信する。具体的なアドレス文字列規約は Design フェーズで確定する。
7. The OscSenderAdapterBinding shall BlendShape 名および `expressionId` にマルチバイト文字（日本語等）および記号（`_`, `.` 等）が含まれてもアドレス文字列を正しく構築できる（ASCII 限定の正規化を強制しない）。
8. The OscSenderAdapterBinding shall プリセット切り替え時に BlendShape および Gaze の両系統でアドレス文字列を再構築し、`OscSenderHost.Configure(...)` へ渡す送信テーブルへ反映する。
9. Where 将来的に追加プリセットが必要になった場合, the OscSenderAdapterBinding shall 新規プリセットを enum 値の追加と分岐 1 か所の修正（BlendShape 用 + Gaze 用）で拡張可能な構造を維持する。

### Requirement 5: 同一プロセス内の OSC 受信 → 送信ループバック抑制
**Objective:** As a 同じ Unity プロセスで OSC 受信 binding と送信 binding を同居させる利用者, I want 受信した値をそのまま同じ endpoint に送り返して無限ループする事故を防ぐ仕組み, so that ローカル配信検証やループバックテストでも暴走を起こさず安心して結線できる

#### Acceptance Criteria
1. The OscSenderAdapterBinding shall 既存 `OscAdapterBinding`（受信側）の仕様や API を変更せずに、送信側のみでループバック抑制ポリシーを実装する。
2. The OscSenderAdapterBinding shall ループバック抑制ポリシーを `[Serializable]` フィールドとして保持し、その既定値を「有効（抑制 ON）」とする。
3. While 同一プロセス内に `OscAdapterBinding`（受信）と `OscSenderAdapterBinding`（送信）が同時に有効、かつ抑制が ON, the OscSenderAdapterBinding shall 各送信 endpoint が受信側 binding の listen endpoint（IP+port）と一致するかを起動時に検査する。
4. If 送信先 endpoint が受信 binding の listen endpoint と一致した場合（抑制 ON 時）, the OscSenderAdapterBinding shall その endpoint への送信を抑制し、Unity 標準ログで警告を出力する。
5. The OscSenderAdapterBinding shall 抑制ポリシーを明示的に無効化（OFF）するオプションを `[Serializable]` フィールドおよび Drawer UI で提供し、E2E テストや意図的なローカル配信検証で抑制を解除できるようにする。
6. When ループバック抑制が有効な状態で全 endpoint が抑制対象になった場合, the OscSenderAdapterBinding shall 送信を行わずに Unity 標準ログで警告を出力し、binding 自体は live 状態を維持する。
7. The OscSenderAdapterBinding shall 抑制判定を毎フレームではなく `OnStart` および endpoint 構成変更時にのみ行い、`OnLateTick` のホットパスに比較処理を入れない。

### Requirement 6: JSON 永続化（OscSenderOptionsDto）とスキーマドキュメント
**Objective:** As a JSON ファースト永続化を採用するプロジェクトの利用者, I want OSC 送信 binding の設定（endpoint 一覧 / BlendShape mapping / Gaze 送信構成 / 抑制ポリシー）を JSON で往復シリアライズできる仕組み, so that ビルド後でも JSON で送信設定を差し替え、スキーマドキュメントで仕様を共有できる

#### Acceptance Criteria
1. The OscSenderOptionsDto shall `OscSenderAdapterBinding` が保持する設定（endpoint 一覧、各 endpoint の IP/port/プリセット/有効フラグ、ループバック抑制ポリシー、BlendShape mapping、Gaze 送信構成）を漏れなく表現する `[Serializable]` 型として定義される。
2. The OscSenderOptionsDto shall Gaze 送信構成として、送信対象 `expressionId` の集合と送信有効フラグを最低限保持する（具体的フィールド設計は Design で確定）。
3. When OscSenderOptionsDto が JSON へシリアライズされた時, the JSON 出力 shall human-readable で diff フレンドリーな形式（インデント / 安定キー順）であること。
4. When 同等内容の JSON が OscSenderOptionsDto に往復デシリアライズされた時, the round-trip 結果 shall 元の OscSenderOptionsDto と値レベルで等価である。
5. If JSON 中に未知のキーが含まれている場合, the OscSenderOptionsDto デシリアライザ shall 当該キーを無視し、既知キーのみで OscSenderOptionsDto を構築する。
6. If JSON 中に必須キーの欠落 / 型不一致がある場合, the OscSenderOptionsDto デシリアライザ shall Unity 標準ログで警告を出力し、当該フィールドは既定値で補ったうえで構築を継続する。
7. The OscSenderOptionsDto shall パッケージ内 `Documentation~/` 配下に JSON スキーマ仕様（フィールド一覧、型、既定値、サンプル JSON）を Markdown で提供し、Gaze 送信構成も例示する。
8. The OscSenderOptionsDto shall 既存 `com.hidano.facialcontrol.osc` パッケージ内に追加実装され、新パッケージを切らずに JSON ロード／セーブ経路へ統合する。
9. The OscSenderOptionsDto shall 既存 JSON 経路で採用されている `SystemTextJsonParser`（`System.Text.Json` ベース）と整合する形で実装され、`JsonUtility` を新規に持ち込まない。

### Requirement 7: Editor 拡張（OscSenderAdapterBindingDrawer / UI Toolkit）
**Objective:** As a Inspector から OSC 送信を構成する Unity エンジニア, I want endpoint 一覧、BlendShape mapping、Gaze 送信構成を視覚的に編集できる UI Toolkit ベースの PropertyDrawer, so that 設定ファイルを直接編集せずに送信先の追加・並べ替え・削除・プリセット切替・Gaze 送信切替を行える

#### Acceptance Criteria
1. The OscSenderAdapterBindingDrawer shall UI Toolkit ベースで実装され、IMGUI を新規 UI コードに混入させない。
2. The OscSenderAdapterBindingDrawer shall endpoint 一覧を ReorderableList 相当の UI で表示し、追加 / 削除 / 並べ替え操作を提供する。
3. The OscSenderAdapterBindingDrawer shall 各 endpoint について IP/host、ポート、プリセット（VRChat / ARKit）、有効/無効を編集できるフォーム要素を提供する。
4. The OscSenderAdapterBindingDrawer shall ループバック抑制ポリシー（有効 / 無効、既定: 有効）の編集 UI を提供する。
5. The OscSenderAdapterBindingDrawer shall BlendShape mapping 編集 UI（BlendShape 名一覧、追加 / 削除）を提供する。送信時のアドレス文字列はプリセットから自動生成されるためインライン編集は不要とする。
6. The OscSenderAdapterBindingDrawer shall Gaze 送信構成 UI（送信対象 `expressionId` 一覧、送信有効フラグの切替）を提供する。
7. If 入力された IP / ポートが明らかに不正（空文字 / 範囲外ポート）な場合, the OscSenderAdapterBindingDrawer shall インラインで警告メッセージを表示する。
8. The OscSenderAdapterBindingDrawer shall Editor 専用 asmdef 配下に実装され、Runtime asmdef へ Editor 参照を漏らさない。

### Requirement 8: テスト（EditMode 単体 + PlayMode E2E）
**Objective:** As a TDD で実装する開発者, I want EditMode / PlayMode の役割分担と検証観点を仕様として明文化した状態, so that Red-Green-Refactor サイクルで安全に OSC 送信 binding を構築できる

#### Acceptance Criteria
1. The osc-output-binding spec shall EditMode テストで OscSenderOptionsDto の JSON 往復（BlendShape mapping + Gaze 送信構成を含む）、未知キー無視、必須キー欠落時の既定値補完を検証する。
2. The osc-output-binding spec shall EditMode テストで Domain 層 Facial Output Bus のオブザーバ登録 / 解除、列挙中変更、空オブザーバ時 skip、例外伝播のログ化を BlendShape 通知と Gaze 通知の両方で検証する。
3. The osc-output-binding spec shall PlayMode テストで `FacialController` → Facial Output Bus → `OscSenderAdapterBinding` → 実 UDP → `OscReceiver` の E2E 経路で post-blend BlendShape 値が到達することを検証する。
4. The osc-output-binding spec shall PlayMode テストで Gaze 入力 Vector2 が同一の E2E 経路で受信側に到達し、x / y 成分が誤差範囲で再現されることを検証する。
5. The osc-output-binding spec shall PlayMode テストで 1 つの `OscSenderAdapterBinding` から複数 endpoint へ unicast 同報送信した結果が 2 系統の `OscReceiver` で受信されることを BlendShape と Gaze の両方で検証する。
6. The osc-output-binding spec shall PlayMode テストで 同一プロセス内の `OscAdapterBinding`（受信）と `OscSenderAdapterBinding`（送信）が同居した状態でループバック抑制ポリシーが期待通り（既定 ON 時は送信されず、明示 OFF 時は送信される）に動作することを検証する。
7. The osc-output-binding spec shall PlayMode テストで `OnLateTick` のホットパスに GC アロケーションが発生しないことを `Profiler.GetTotalAllocatedMemoryLong` 差分等で監視する（BlendShape のみ送信ケースと Gaze 同送ケースの両方）。
8. The osc-output-binding spec shall テスト命名を `{Target}Tests` / `{Method}_{Condition}_{Expected}` 規約に従って配置する。

### Requirement 9: サンプル（Samples~/OscOutputDemo）とドキュメント更新
**Objective:** As a UPM 経由でパッケージを Import する利用者, I want OSC 送信 binding を 1 シーンで動かせる完結したサンプル（BlendShape + Gaze）, so that Inspector 設定例と JSON 例を見ながら自分のプロジェクトに導入できる

#### Acceptance Criteria
1. The osc-output-binding spec shall `com.hidano.facialcontrol.osc` の `Samples~/OscOutputDemo` に Scene / FacialProfileSO / OscSenderOptions JSON / README をまとめて配置する。
2. The osc-output-binding spec shall OscOutputDemo サンプルで BlendShape 送信と Gaze Vector2 送信の双方を含む構成例を提示する。
3. The osc-output-binding spec shall 同名のサンプル一式を `FacialControl/Assets/Samples/` 配下にも二重管理として配置し、dev プロジェクトで Scene 結線して動作確認できる状態にする。
4. When `Samples~` または `Assets/Samples` のいずれかが編集された時, the osc-output-binding spec の作業手順 shall もう一方を同期コピーする手順を `docs/work-procedure.md` に明記する。
5. The osc-output-binding spec shall `com.hidano.facialcontrol.osc` の `package.json` の `samples` 配列に OscOutputDemo を登録する。
6. The osc-output-binding spec shall `com.hidano.facialcontrol.osc` の `CHANGELOG.md` および `README.md` に OSC 送信 binding 機能（BlendShape + Gaze Vector2）の追加を記載する。
7. The osc-output-binding spec shall `docs/work-procedure.md` にこの spec の作業手順を追加し、`docs/backlog.md` から OSC 送信関連の積み残しを引き上げて整理する。

### Requirement 10: パフォーマンス・アーキテクチャ制約
**Objective:** As a 性能設計指針（毎フレーム GC ゼロ、クリーンアーキテクチャ、preview.2 以降）に従う必要のあるプロジェクト, I want OSC 送信 binding が全レイヤーの規約を破らないこと, so that 既存 contract に違反せず preview.2 以降にマージできる

#### Acceptance Criteria
1. The OscSenderAdapterBinding shall `OnLateTick` のホットパス（毎フレーム実行経路、BlendShape と Gaze の両系統）でヒープ確保を行わず、配列・バッファは `OnStart` 時点で確保し再利用する。
2. The OscSenderAdapterBinding shall Adapters 層（`Hidano.FacialControl.Adapters.OSC` 名前空間相当）に配置され、Domain / Application 層へ Unity / uOSC 参照を漏らさない。
3. The Facial Output Bus shall `Hidano.FacialControl.Domain` 配下に配置され、`Unity.Collections` 以外の Engine 参照を持たない。Gaze Vector2 の通知データ型も Domain 純度を保つ（`UnityEngine.Vector2` を Domain に持ち込まず、`(float x, float y)` などの Domain 表現を使う）。
4. The osc-output-binding 実装 shall 新規パッケージを切らず、既存 `com.hidano.facialcontrol.osc` パッケージへの追加実装として完結する。
5. The osc-output-binding 実装 shall preview.2 以降の機能として位置付け、preview.1 ターゲットの破壊的変更を発生させない。
6. The osc-output-binding 実装 shall Windows PC 環境を主たる検証対象とし、モバイル / WebGL / VR 固有のコードパスを必須にしない（将来拡張の余地は契約として残す）。
7. The osc-output-binding 実装 shall エラーハンドリングを Unity 標準ログ（`Debug.Log/Warning/Error/LogException`）のみで行い、カスタム例外型を新規追加しない。
