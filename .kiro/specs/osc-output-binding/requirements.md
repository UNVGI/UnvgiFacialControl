# Requirements Document

## Project Description (Input)
FacialControl の OSC 送信機能を AdapterBinding として正式統合する spec。

## 背景
現状 com.hidano.facialcontrol.osc には OscSender / OscSenderHost クラスが実装済みで、uOSC クライアントをラップした実 UDP 送信機構（SendAll / SendSingle）と PlayMode ループバックテストは存在する。一方、これらは AdapterBinding として結線されておらず、Inspector の Add ドロップダウンから選択できず、FacialController の合成後 BlendShape 値を吸い出す経路も無いため、プロダクション統合は未完了の状態にある。

## ユースケース
- VTuber 配信などで「ひとつの表情システム → 複数のキャラ描画システム」へリアルタイム配信し、描画側を冗長構成にする
- Unity でキャプチャ／編集した表情を UnrealEngine など Unity 外プラットフォームへ転送し、クロスエンジンでの表情同期を実現する

## スコープ（実装すべきもの）
1. **Domain 層**: FacialController の合成後 BlendShape 値を観察できる仕組み（IFacialOutputObserver / BlendShapeOutputBus 的なもの）を Domain 純度を保ったまま新設。GC-free（ReadOnlySpan<float> 受け渡し）。
2. **OscSenderAdapterBinding**: `[Serializable]` + `[FacialAdapterBinding(displayName: "OSC Sender")]` 具象。OnStart で OscSenderHost を AddComponent + Configure、Domain bus を購読し OnLateTick 等で SendAll。
3. **複数送信先**: 1 binding で複数 endpoint への同報送信に対応（List<EndpointConfig> 形式）。VRChat 互換アドレス形式（/avatar/parameters/{name}）と ARKit 互換（/ARKit/{name}）を選択可能に。
4. **ループ防止**: 同一プロセス内で OSC 受信 binding と送信 binding が同居した場合に無限ループしないための loopback 抑制ポリシー（同 endpoint への送信抑制 or 送信元タグ付け）。
5. **JSON 永続化**: OscSenderOptionsDto + System.Text.Json 往復、JSON スキーマドキュメント追記。
6. **Editor**: OscSenderAdapterBindingDrawer（UI Toolkit / PropertyDrawer）で endpoint 一覧と mapping を編集。
7. **テスト**: EditMode（DTO 往復、Domain bus 単体）、PlayMode（FacialController → OscSender → OscReceiver E2E、複数 endpoint 同報、loopback 抑制）。
8. **サンプル**: Samples~/OscOutputDemo（および Assets/Samples 二重管理）と README / CHANGELOG / docs/work-procedure.md 更新。

## Non-Goals
- VRM 対応（別マイルストーン）
- UDP multicast / broadcast の独自実装（unicast 複数送信先で代替）
- 音声ストリーム / リップシンク音声の OSC 送出（リップシンク値のみ対象）
- OSC bundle / timetag の高度な対応（個別メッセージ送信で十分）
- 既存 OscReceiver / OscAdapterBinding（受信側）の仕様変更

## 制約
- 既存パッケージ com.hidano.facialcontrol.osc に追加実装する形を取る（新パッケージは切らない）
- クリーンアーキテクチャ（Domain / Application / Adapters）の依存方向を維持。Domain は Unity / uOSC 非依存
- 毎フレームのヒープ確保ゼロを目標
- preview.2 以降の機能として位置付ける（preview.1 ターゲットには含めない）

## 参考ファイル
- 既存送信機構: FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/OSC/OscSender.cs, OscSenderHost.cs
- 既存受信 binding（実装ひな型として参考）: FacialControl/Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/AdapterBindings/OscAdapterBinding.cs
- AdapterBinding 基底: FacialControl/Packages/com.hidano.facialcontrol/Runtime/Domain/Adapters/AdapterBindingBase.cs
- 既存ループバックテスト: FacialControl/Packages/com.hidano.facialcontrol.osc/Tests/PlayMode/Integration/OscSendReceiveTests.cs

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
