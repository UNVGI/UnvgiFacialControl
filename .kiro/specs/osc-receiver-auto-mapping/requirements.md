# Requirements Document

## Project Description (Input)
OscReceiverAdapterBinding の Mappings 列挙を、送信側 heartbeat (/_facialcontrol/blendshape_names) と受信側モデルの BlendShape 一覧から自動生成する。現状 Mappings は Inspector で手入力必須（mode=blendShape, expressionId, addressPattern を 1 件ずつ）で、200 個の BlendShape を持つモデルでは現実的に運用できない。本 spec では (a) heartbeat 受信時に runtime mapping を動的拡張する経路を追加し、(b) Mappings が空 / 部分入力 / 完全手入力の 3 ケースを統一的に扱い、(c) アドレスプリセット（VRChat 形式 /avatar/parameters/{name} と ARKit 形式 /ARKit/{name}）の推定方法を確立する。スコープ: OscReceiverAdapterBinding、OscInputSource、HeartbeatConsistencyChecker、heartbeat メッセージフォーマット（preset 情報の追加可否含む）、Gaze は対象外（heartbeat に Gaze id を含まないため別途検討）。非機能: runtime 中の mapping 拡張で GC スパイクを起こさない、既存の手動 Mappings との後方互換維持。受け入れ条件は OscReceiverDemo の Mappings を空にしても OscOutputDemo からの送信が受信側モデルの BlendShape へ反映されること。

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
