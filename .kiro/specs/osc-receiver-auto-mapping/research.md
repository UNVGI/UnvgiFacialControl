# Research & Design Decisions — osc-receiver-auto-mapping

---
**Purpose**: requirements.md / gap-analysis.md で決まった 10 個の設計判断と、design.md に乗せ切れない代替案・トレードオフ・参照情報を記録する。
**スコープ**: 受信側 `OscReceiverAdapterBinding` の auto mapping 経路 + sentinel 形式 heartbeat 拡張 (送信側最小改修) + `IInputSourceRegistry.Replace` 追加 + `AddressPresetKind.Custom` 追加。
---

## Summary

- **Feature**: `osc-receiver-auto-mapping`
- **Discovery Scope**: Extension（既存 OSC binding 群への機能追加 + 隣接プロトコル拡張）
- **Discovery Process**: `design-discovery-light.md`（既存実装の Grep / 既存テストの SkipMask 参照点列挙 / heartbeat 既存経路の調査）。新規外部ライブラリは導入しないため WebSearch は省略。
- **Key Findings**:
  - 既存 `OnStart` の `!hasBlendShapeMappings && !hasGazeMappings` 早期 return が空 Mappings 経路と直接矛盾しており、3 フェーズ分割が必須。
  - `OscInputSource._mappingIndexToMeshIndex` / `_skipMask` / `_contributeMask` は全て `readonly` で in-place 拡張不可。`Replace API + 新インスタンス差し替え` 戦略が最も影響範囲が小さい。
  - heartbeat 既存経路は `string` 値のみ集約するため、sentinel `"__preset__"` を「BlendShape 名らしき string」として旧 receiver に渡しても mesh 不一致で自然に skip され、後方互換が保たれる。

---

## Research Log

### Topic 1: 既存 `OscReceiverAdapterBinding.OnStart` の早期 return 経路と Phase 分割可能性

- **Context**: 空 Mappings で heartbeat 待ちに入る Phase 1 を成立させるには、現状の「mapping 0 件で何もせず終了」する短絡パス（`OscReceiverAdapterBinding.cs` の `!hasBlendShapeMappings && !hasGazeMappings` 分岐）を除去する必要がある。
- **Sources Consulted**:
  - `FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/AdapterBindings/OscReceiverAdapterBinding.cs`（`OnStart`, `OnFixedTick`, `HandleIncomingOscMessage`, `HandleHeartbeatMessage`）
  - `FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/OSC/OscReceiverHost.cs`（`Configure` が `OscMapping[]` 必須）
- **Findings**:
  - `OscReceiverHost.Configure` は `mappings != null` を assert するが、`mappings.Length == 0` でも `OscReceiver.Initialize` 自体は通る（受信は dispatch されないだけ）。
  - `OscReceiver.SetMessageFilter(HandleIncomingOscMessage)` 経路は mapping 注入と独立しており、heartbeat 受信のみを取り出す経路として再利用可能。
  - 既存の Gaze 経路 (`HasGazeMappings`) は本 spec の対象外であり、Phase 1 で先行起動できる構造に整える必要がある。
- **Implications**:
  - **OnStart を 3 Phase に分割する**（design.md の「OnStart 3 Phase 分割」参照）。Phase 1 で socket + filter + Gaze、Phase 2 で mapping 確定（手入力 ∪ heartbeat 差分）、Phase 3 で `OscInputSource` 構築 + `Registry.Replace`。
  - Phase 2/3 は OnStart 内とは別に「heartbeat 受信時に再実行する Coordinator method」として抽出する。

### Topic 2: `OscInputSource` の runtime 差し替え戦略

- **Context**: `_mappingIndexToMeshIndex` / `_skipMask` / `_contributeMask` が `readonly` であり in-place 拡張不可。heartbeat 駆動で件数が増えるたびに新インスタンスへ差し替える必要がある。
- **Sources Consulted**:
  - `FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/InputSources/OscInputSource.cs`
  - `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/InputSources/InputSourceRegistry.cs`（duplicate 時 LogError + 後勝ち上書き）
- **Findings**:
  - 既存 `Register` の重複時 `LogError` 仕様を heartbeat 駆動の通常運用で踏むと「予期される再登録 vs 異常な重複」を分離できない。
  - `Unregister` → `Register` の 2 ステップにすると `RegisteredIds` の挿入順が崩れる（既存テストが順序を assert している）。
- **Implications**:
  - **`IInputSourceRegistry.Replace(string id, IInputSource source)` を新規追加**。挙動: 既存登録があれば置換（挿入順を保持）、なければ新規登録。ログレベルは `Debug.Log`（LogDebug 相当）で、`LogError` は出さない。
  - 既存 `Register(slug, source)` の duplicate-LogError 仕様は本 spec で変更しない。`Replace` は「意図的差し替え」を明示する API として導入。

### Topic 3: heartbeat payload 拡張（Option A/B/C の選定）

- **Context**: receiver 側で preset 種別を確定するには (a) 名前ベース推定のみ / (b) sentinel + preset 文字列 / (c) 別 OSC 型タグ の 3 案があった。
- **Sources Consulted**:
  - `FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/OSC/OscBundleBuilder.cs:300-380`（`AddHeartbeatMessages`）
  - `FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/AdapterBindings/OscReceiverAdapterBinding.cs:778-795`（`HandleHeartbeatMessage` の `is string` フィルタ）
- **Findings**:
  - Option A（名前ベースのみ）は ARKit / VRChat の判別に sentinel 不要だが、custom prefix を payload に乗せられない。
  - Option C（別 OSC 型タグ）は旧 receiver の `is string` フィルタが暗黙 skip してくれるが、`OscBundleBuilder.AddHeartbeatMessages` が string 専用 packing に最適化されているため builder 側の改修が大きい。
  - Option B（sentinel `"__preset__"` + preset 名）は string 配列 1 本で完結し、旧 receiver には mesh 不一致名として渡って自然に warning だけで済む。
- **Implications**:
  - **Option B を採用**。送信側 `OscBundleBuilder.AddHeartbeatMessages` と `OscSenderAdapterBinding` の差分は「heartbeat 名配列の末尾に sentinel + preset 名 + (custom 時のみ) custom prefix を追加する」だけで済む。
  - 送信側 Inspector / JSON に `preset 出力 ON/OFF オプション`（デフォルト ON）を追加し、preset 出力を急がない既存ユーザーへの逃げ道を残す。

### Topic 4: heartbeat 内容変化検出のハッシュ戦略

- **Context**: heartbeat は 5 秒間隔で何度も到着する。毎回 mapping を再構築すると GC スパイクが発生するため、内容変化のみで再構築する必要がある。`string.Join` や `GetHashCode` の安易な利用は GC アロケーションを生む。
- **Sources Consulted**:
  - 既存 `HeartbeatConsistencyChecker.ComputeMismatchHash`（`string.GetHashCode` を全 BlendShape に対して回している。order-stable な FNV-1a を新規実装するのが性能・移植性ともに優位）
  - FNV-1a 参考: [Wikipedia: Fowler–Noll–Vo hash function](https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function)
- **Findings**:
  - 32-bit FNV-1a は 1 文字あたり XOR + 32-bit 乗算のみで GC ゼロ。`string` 全体を 1 byte ずつ走査するだけで、別 buffer 確保が不要。
  - 名前順依存（順序が変わるとハッシュも変わる）であり、heartbeat の sentinel 配置順を保ったまま差分検出に使える。
- **Implications**:
  - **FNV-1a を採用**（design.md 「Heartbeat 内容変化検出 (FNV-1a)」参照）。
  - 擬似コード（C# 風）:
    ```
    const uint OffsetBasis = 2166136261u;
    const uint Prime = 16777619u;
    uint hash = OffsetBasis;
    for (int n = 0; n < names.Count; n++) {
        string s = names[n];
        for (int i = 0; i < s.Length; i++) {
            char c = s[i];
            hash ^= (byte)(c & 0xFF); hash *= Prime;
            hash ^= (byte)((c >> 8) & 0xFF); hash *= Prime;
        }
        hash ^= 0x00; hash *= Prime; // 名前間区切り
    }
    return hash;
    ```

### Topic 5: `OscDoubleBuffer.Resize` 中の受信スレッド取りこぼし防止

- **Context**: 受信スレッドが Write 中に Resize が走ると、書き込み先 NativeArray が Dispose されて crash する可能性がある。lock を長く保持すると受信スレッドが停止する。
- **Sources Consulted**:
  - `FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/OSC/OscDoubleBuffer.cs`（`_writeIndex` の Interlocked.Exchange と `Write` が同期されていない既存実装）
- **Findings**:
  - 既存 `Write` は lock を持たないため、Resize 中の Dispose と競合する。
  - 検討案: (a) 全 lock 化（受信スレッド停止リスク）、(b) Atomic swap pattern（pending list に切り替え、新 buffer 構築後に再投入）、(c) Resize を frame 境界に固定し受信 Pause（受信を一時止める）。
- **Implications**:
  - **Pending list 方式を採用**（design.md 「OscDoubleBuffer Resize 擬似コード」参照）。
  - lock 保持時間は「pending list への参照 swap 1 命令」のみに圧縮。new buffer 確保とコピーは lock 外で実施。

### Topic 6: `HeartbeatConsistencyChecker` のスリム化と SkipMask 廃止

- **Context**: 既存 `HeartbeatConsistencyChecker` は (a) sender/receiver 名前差分の warning、(b) `SkipMask`（mismatch BlendShape の出力抑止）、(c) `ContributeMask`（mapping 有効範囲）の 3 役を兼ねている。auto mapping 経路では heartbeat 駆動で mapping が常に最新化されるため、`SkipMask` の出番がなくなる。
- **Sources Consulted**:
  - `HeartbeatConsistencyChecker.cs:18-19, 92-167`
  - `OscInputSourceMaskTests.cs`, `HeartbeatConsistencyCheckerTests.cs`, `OscHeartbeatConsistencyTests.cs`（SkipMask を直接 assert している既存テスト）
- **Findings**:
  - `SkipMask` の存在意義は「heartbeat に含まれない BlendShape を mapping 経由で出力しない」こと。auto mapping ではそもそも mapping が heartbeat に基づいて生成されるため、本機能は意味を失う。
  - `ContributeMask` は `OscInputSource` の `BlendShapeCount` を決めるために必要だが、Checker から切り離して binding 側が runtime mapping 配列から直接計算可能。
- **Implications**:
  - **`SkipMask` 完全廃止**。Checker は sender/receiver 名前差分の mismatch warning 専用に縮退。
  - **ContributeMask は `OscReceiverAdapterBinding` が runtime mapping 配列から生成**し、`OscInputSource` の constructor に渡す。
  - 影響テスト（再構成 or 削除）: `HeartbeatConsistencyCheckerTests`（SkipMask assertion を削除）、`OscInputSourceMaskTests`（SkipMask 引数経路を削除）、`OscHeartbeatConsistencyTests`（SkipMask 検証パートを削除し ContributeMask 検証に置換）。

### Topic 7: `AddressPresetKind.Custom` 追加と `OscAddressFormatter` 拡張

- **Context**: 既存 enum は VRChat / ARKit の 2 値のみ。`GetBlendShapePrefix` の switch は default で `NotSupportedException` を投げる。
- **Sources Consulted**:
  - `AddressPresetKind.cs`（2 値定義）
  - `OscAddressFormatter.cs:138-149`（switch default → throw）
- **Findings**:
  - enum 値追加は破壊変更ではない（既存呼び出しは VRChat/ARKit のみで完結）。
  - Custom prefix は runtime に変動する可能性があるため、enum 値とは別に prefix 文字列を引数として受け取る overload が必要。
- **Implications**:
  - `AddressPresetKind` に **`Custom` 値を追加**。
  - **`OscAddressFormatter.FormatBlendShapeAddress(string customPrefix, string name)` overload を追加**（既存の `(preset, name)` API は維持）。
  - `FormatBlendShapeAddressUtf8` / `GetOrAddBlendShapeAddressUtf8` も custom prefix 対応の overload を追加。Pool key は `(name, customPrefix)` を含む形に変更。
  - 既存 `GetBlendShapePrefix(AddressPresetKind preset)` の switch は Custom 以外で動作させ、Custom は呼ばないように経路を分ける。

### Topic 8: Inspector 出自表示の最小実装

- **Context**: Req 2.5 は「手入力 / heartbeat 由来 entry の出自を区別する runtime 内部状態」を要求する。Inspector UI への badge 表示は preview 段階では負荷が高い。
- **Sources Consulted**:
  - `OscReceiverAdapterBindingDrawer.cs`（既存 Inspector は ListView ベースで手入力 entry を編集）
  - `docs/backlog.md`（preview.2 以降の Backlog 候補項目を集約する場所）
- **Findings**:
  - Inspector の Manual/Auto badge は新規 USS + bindingPath カスタマイズが必要で、本 spec の主目的（疎通）と独立して進められる。
  - 出自識別は診断ログ + runtime API（`OscReceiverAdapterBinding.GetMappingOrigin(int index)` 等）のみで Req 2.5 を満たせる。
- **Implications**:
  - **runtime 内部のみで出自識別**。Adapters 専用 wrapper struct or parallel `bool[] isAutoMapping` で保持。
  - **Inspector の Manual/Auto badge UI 改修は backlog 行き**。`docs/backlog.md` に追記する（タスク phase で対応）。

### Topic 9: PlayMode テストの heartbeat 注入方式

- **Context**: heartbeat 5 秒間隔の実 wait は CI で 1 テスト 10 秒を平気で食う。テストの決定論性を保つ必要がある。
- **Sources Consulted**:
  - `OscHeartbeatConsistencyTests.cs`（`HandleHeartbeat(params string[] names)` で binding 内部経路を直接呼ぶパターン）
- **Findings**:
  - 既存 `OscHeartbeatConsistencyTests` は `HandleHeartbeat` ヘルパで `_binding` の `HandleIncomingOscMessage` を直接呼んでおり、UDP loopback を経由しない。
- **Implications**:
  - **同方式を採用**。`OscReceiverAdapterBindingAutoMappingIntegrationTests` も `HandleHeartbeat(message)` を直接呼ぶ。
  - UDP loopback 経路の E2E は別 spec で `OscReceiverAdapterBindingAutoMappingE2ETests` 等を追加する候補（backlog）。

### Topic 10: `OscReceiverDemoProfile.asset` の運用変更方針

- **Context**: 現状 5 件 mapping を含む asset を「空 Mappings に書き換える」か「空 variant を別ファイルとして追加」かを決める。
- **Sources Consulted**:
  - `OscReceiverDemoProfile.asset`（4 件 Normal_BlendShape + 1 件 Gaze_VRChat_XY）
  - `OscReceiverDemo/README.md`（既に「`Mappings` 自動生成は別 spec で計画」と明記）
- **Findings**:
  - 既存 asset を空に書き換える方が「auto mapping がデフォルト経路」というメッセージが強く伝わる。
  - 手入力サンプルを残したいニーズは backlog として別 spec / 別サンプルで対応可能。
- **Implications**:
  - **既存 asset を空 Mappings に書き換える**。Gaze entry も削除する（heartbeat に Gaze id が含まれないため Gaze は別 spec 課題）。
  - README は「auto mapping がデフォルト経路。手入力サンプルは backlog で再提供」を 1 段落以上で説明。
  - 「Gaze entry 無しで heartbeat 駆動の BlendShape mapping が生成されること」を確認するテストを Req 7.5 の integration test に含める。

---

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| A. OnStart 内 Phase 分割 | OnStart を Phase 1/2/3 に再構成し、heartbeat 受信時に Phase 2/3 を再実行 | 既存 1 ファイルに集約、lifecycle 直感的、後方互換が容易 | `OscReceiverAdapterBinding.cs` が 1400 行から更に膨らむ | **採用**（design.md "OnStart 3 Phase 分割" 参照） |
| B. AutoMappingCoordinator 新クラス抽出 | mapping 解決 + heartbeat 駆動拡張 + preset 推定を別クラスに分離 | 単一責任、テスト容易性、binding サイズ抑制 | 新クラスと binding の所有関係が複雑化、`OscDoubleBuffer` / Checker の owner が不明瞭 | 不採用（preview 段階で過剰分離） |
| C. ハイブリッド | preset 推定のみ helper class 化、lifecycle は binding に残す | 推定単体テストの testable 単位 | 影響範囲が広く、レビュー粒度大 | **小粒度の helper のみ採用**（`AddressPresetEstimator` 静的クラス + `RuntimeMappingResolver` 内部 helper） |

---

## Design Decisions

### Decision 1: heartbeat payload = Option B (sentinel + preset 名)

- **Context**: 受信側で preset 種別を確定し custom prefix を伝送する手段が必要。
- **Alternatives**: A 名前ベースのみ / B sentinel + preset 名 / C 別 OSC 型タグ
- **Selected**: **B**。配列末尾に `"__preset__"` + `"vrchat"|"arkit"|"custom"` + (custom 時のみ) prefix を追加。
- **Rationale**: string 配列 1 本で完結し、旧 receiver には mesh 不一致名として渡るため自然に skip される（最小破壊）。送信側改修は `OscBundleBuilder.AddHeartbeatMessages` の末尾 append のみ。
- **Trade-offs**: 旧 receiver で warning が 1 回出る可能性あり（`receiverOnly` 名一覧に `__preset__` が載る）。warning であり致命エラーではないため許容。
- **Follow-up**: 後方互換マトリクス（design.md 末尾参照）の「旧 receiver × 新 sender」セル動作確認。

### Decision 2: `OscInputSource` 差し替え = `IInputSourceRegistry.Replace` API 新設

- **Context**: 既存 `Register` 重複時 `LogError` を heartbeat 駆動の通常運用で踏みたくない。
- **Alternatives**: `Unregister` → `Register` / `Register` の LogError 抑止フラグ / 専用 `Replace` API
- **Selected**: **`Replace(string id, IInputSource source)` 新 API 追加**。
- **Rationale**: 「意図的差し替え」を呼び出し側で明示できる。挿入順保持の利点もある。
- **Trade-offs**: 新 API 追加によるインターフェース面積増加。テスト追加コスト。
- **Follow-up**: ログレベルは `Debug.Log`（LogDebug 相当）で「id={id} replaced (prev type={prevType}, new type={newType})」を出力。LogError は出さない。

### Decision 3: `OscDoubleBuffer.Resize` = Pending list 方式

- **Context**: Resize 中の受信スレッド取りこぼし防止。
- **Alternatives**: 全 lock 化 / Pending list 方式 / 受信 Pause
- **Selected**: **Pending list 方式**。Resize 開始直前に受信スレッドのストアを pending list (一時バッファ) に切り替え、メインスレッドで新 buffer 確保 → pending list の値を新 buffer に再投入 → 通常モードに戻す。
- **Rationale**: lock 保持時間が pending list 切り替えの atomic swap のみで極小。受信スレッドはほぼ stall しない。
- **Trade-offs**: 実装複雑度がやや上がる（pending list クラスの追加）。
- **Follow-up**: design.md に thread safety 擬似コードを掲載。実装時に既存 `OscDoubleBufferTests` に Resize 中受信ケースを追加。

### Decision 4: `HeartbeatConsistencyChecker` スリム化 + `SkipMask` 廃止

- **Context**: auto mapping では mapping 自体が heartbeat に基づいて常に最新化されるため、SkipMask の出番がない。
- **Alternatives**: SkipMask 維持 / SkipMask 廃止
- **Selected**: **SkipMask 廃止**。Checker は sender/receiver 名前差分の mismatch warning 専用に縮退。
- **Rationale**: 責務が単純化し、ContributeMask は binding 側で生成する方が「mapping 配列を生成した側が長さも決める」自然な責務分担になる。
- **Trade-offs**: 既存 SkipMask 依存テスト 3 ファイルの再構成 / 削除が必要。
- **Follow-up**: タスク phase で影響テスト一覧（design.md 「既存 PlayMode 統合テストへの影響表」参照）を順番に修正。

### Decision 5: heartbeat 変化検出ハッシュ = FNV-1a

- **Context**: GC ゼロで heartbeat 変化を検出する。
- **Alternatives**: `string.Join` + `GetHashCode` / FNV-1a / xxHash
- **Selected**: **FNV-1a (32-bit)**。
- **Rationale**: 1 文字 XOR + 乗算で GC ゼロ。順序依存安定ハッシュとして本用途に十分。xxHash は高速だが実装複雑度が高く本ユースケースでは過剰。
- **Trade-offs**: 衝突確率は 2^-32 程度。heartbeat 5 秒間隔で同一内容を扱うため衝突実害は無視可能。
- **Follow-up**: design.md 擬似コード参照。

### Decision 6: `AddressPresetKind.Custom` 追加 + `OscAddressFormatter` overload

- **Context**: custom prefix（例: `/myapp/`）を runtime に受け取る必要がある。
- **Alternatives**: enum 値追加せず string 引数のみ / `Custom` enum 値追加 + overload
- **Selected**: **`Custom` enum 値追加 + `OscAddressFormatter.FormatBlendShapeAddress(string customPrefix, string name)` overload 追加**。
- **Rationale**: enum 値追加は後方互換（switch default が `NotSupportedException` を投げるため Custom 経路は別 overload に分離する設計が安全）。
- **Trade-offs**: enum + 別経路 overload の二重構造が必要。
- **Follow-up**: `FormatBlendShapeAddressUtf8` / `GetOrAddBlendShapeAddressUtf8` の custom prefix 対応 overload も合わせて追加。Pool key は `(name, customPrefix)` を含む。

### Decision 7: Inspector 出自表示 = runtime 内部のみ (UI 改修ゼロ)

- **Context**: Req 2.5 を「診断ログ + runtime API」で満たし、Inspector UI は backlog 行き。
- **Alternatives**: ListView 行 badge UI / runtime API のみ
- **Selected**: **runtime API のみ**。`OscReceiverAdapterBinding.GetMappingOrigin(int index)` 等の診断 API を提供し、Inspector UI 改修は本 spec のスコープ外。
- **Rationale**: preview 段階で疎通優先。UI badge は preview.2 以降の Inspector 統合 spec で対処。
- **Trade-offs**: ユーザーが Inspector 上で「これは auto / これは manual」を一目で判別できない。
- **Follow-up**: `docs/backlog.md` に「OscReceiverAdapterBindingDrawer の Manual/Auto badge 表示」を追加（タスク phase で実施）。

### Decision 8: PlayMode テスト heartbeat 注入 = HandleHeartbeat 直接呼び出し

- **Context**: heartbeat 5 秒待ちで PlayMode テストが遅延する。
- **Alternatives**: 実 5 秒待ち / `ManualTimeProvider` で時刻操作 / `HandleHeartbeat` 直接呼び出し
- **Selected**: **`HandleHeartbeat` 直接呼び出し**（既存 `OscHeartbeatConsistencyTests` と同方式）。
- **Rationale**: 既存パターンと一貫性。テスト時間が決定論的。
- **Trade-offs**: UDP loopback の真の経路カバレッジは別 spec の E2E テストに譲る。
- **Follow-up**: backlog に「OSC auto mapping E2E (UDP loopback)」を追加候補として記録。

### Decision 9: `OscReceiverDemoProfile.asset` = 既存 asset を空 Mappings に書き換え

- **Context**: 「auto mapping がデフォルト経路」を明確に伝える。
- **Alternatives**: 既存 asset を空に書き換え / 空 variant を別ファイル追加
- **Selected**: **既存 asset を書き換え**。
- **Rationale**: ユーザーが Import Sample した直後に空 Mappings 経路が動くことが、本 spec の主目的を最も強く伝える。
- **Trade-offs**: 手入力サンプルが失われる（backlog で独立サンプル化）。
- **Follow-up**: README は auto mapping がデフォルト経路である旨を 1 段落以上で説明。Gaze entry 無しでも heartbeat 駆動で BlendShape mapping が生成されることを確認するテストを追加。

### Decision 10: `Replace` API ログレベル = LogDebug 出力

- **Context**: heartbeat 駆動の `OscInputSource` 差し替えで毎回 LogError が出るとログがノイズだらけになる。
- **Alternatives**: ログ無し / `Debug.Log` / `Debug.LogWarning` / `Debug.LogError`
- **Selected**: **`Debug.Log`（LogDebug 相当）**。「id={id} replaced (prev type={prevType}, new type={newType})」を出力。
- **Rationale**: 通常運用ではノイズにならず、デバッグトレースで追える。LogError は heartbeat 起因の差し替えが意図的な操作であるため不適切。
- **Trade-offs**: Production で `Debug.Log` を抑制する場合は標準の Unity ログフィルタで対処可能。
- **Follow-up**: 既存 `Register` の重複時 LogError 仕様は変更しない（Replace は意図的差し替えの明示 API）。

---

## Risks & Mitigations

- **R1: heartbeat 内容変化検出ハッシュ衝突** — FNV-1a の 32-bit 衝突確率は実用上無視できるが、万一同一ハッシュで内容が異なるケースが発生すると mapping 再構築が起きない。**Mitigation**: 5 秒間隔の heartbeat で衝突継続するシナリオは現実的でない。万一発生しても次の差分到来で修正されるため許容。
- **R2: Replace API の他 binding への波及** — `Replace` API 追加で他 spec の binding（VMC, InputSystem 等）が誤って `Replace` を呼ぶリスクがある。**Mitigation**: API ドキュメントに「heartbeat 駆動の意図的差し替え専用」と明記。既存 binding は引き続き `Register` を使用。
- **R3: OscDoubleBuffer pending list の lock 漏れ** — pending list 切り替え時の atomic swap を Interlocked で行わず通常代入してしまうと receiver thread が古いポインタを掴む。**Mitigation**: design.md に thread safety 擬似コードを記載し、実装時に `Interlocked.Exchange` を必須化。
- **R4: 旧 receiver × 新 sender の warning 増加** — sentinel `"__preset__"` を含む heartbeat を旧 receiver が受信すると、receiverOnly 警告 1 回が出る。**Mitigation**: 後方互換マトリクス（design.md 末尾）で動作確認。warning 1 回は致命的でないため許容。送信側 `preset 出力 ON/OFF オプション` で OFF も選択可能（デフォルト ON）。
- **R5: 既存 SkipMask 依存テスト 3 ファイルの再構成漏れ** — 既存テストが SkipMask を assert している箇所を見落とすと CI 緑が崩れる。**Mitigation**: design.md 「既存 PlayMode 統合テストへの影響表」で全 SkipMask 参照箇所を列挙し、タスク phase で 1 ファイルずつ修正・削除する。

---

## References

- 既存実装ファイル一覧は `gap-analysis.md` 末尾「主要参照ファイル」セクション参照。
- [Fowler–Noll–Vo hash function (Wikipedia)](https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function) — FNV-1a の参照アルゴリズム。
- 隣接 spec `osc-output-binding/gap-analysis.md` — heartbeat 既存実装の調査と本 spec が直接連動する送信側拡張の方針整合性確認に使用。
