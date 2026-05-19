# Requirements Document

## Project Description (Input)

## 目的
Adapter 層の設定値のうち「環境依存」「運用依存」「マシン依存」の項目を、現在の `FacialCharacterProfileSO` (キャラ SO) から切り離し、適切な置き場所に再配置する。キャラ SO は「キャラクター固有の表情データ」のみを保持する純粋な存在に戻す。

## 背景
現在、OSC ポート・IP・マイクデバイス名などの環境/マシン依存設定が `OscAdapterBinding` / `OscSenderAdapterBinding` / `ULipSyncAdapterBinding` の `[SerializeField]` としてキャラ SO に埋め込まれている。これにより以下の問題が発生している:
- 開発環境ごとに値を変更すると git diff が発生し、merge conflict が増える
- 本番環境とテスト環境の IP 混在リスク
- 同じキャラ SO を複数シーン/複数環境で使い回せない
- マイクデバイス名が個人マシン依存なのに git 管理下に置かれる

## 配置方針(合意済み)

### キャラ SO に残す (キャラ依存)
- `OscAdapterBinding._mappings` (OSC アドレス ↔ BlendShape)
- `ULipSyncAdapterBinding._analyzerProfile` (uLipSync.Profile)
- `ULipSyncAdapterBinding._phonemeEntries` (音素 ↔ BlendShape/Clip)
- `ULipSyncAdapterBinding._targetMeshHint`
- `ULipSyncAdapterBinding._maxWeightScale`

### 新規 Settings SO に切り出す (環境/運用依存)
- OSC Receiver: `_endpoint`, `_port`, `_stalenessSeconds`, `_failSafeMode`, `_bundleMode`, `_bundleAccumulationTimeoutMs`
- OSC Sender: `_endpoints` (リスト), `_heartbeatIntervalSeconds`, `_suppressLoopback`

### PlayerPrefs に移す (マシン依存)
- `ULipSyncAdapterBinding._deviceDescriptor` (DeviceName + DisambiguatorIndex)
- キー: `Hidano.FacialControl.LipSync.MicDevice.Name` / `Hidano.FacialControl.LipSync.MicDevice.Disambiguator`

## 採用する実装方式: 親 SO + Sub-asset SO 集約 (方式3)

```
AdapterRuntimeSettingsCollection.asset (Project ビューでは 1 ファイル)
  └─ OscRuntimeSettings (sub-asset)
       ├─ Receiver セクション
       └─ Sender セクション
```

- `AdapterRuntimeSettingsBase` (abstract ScriptableObject) を基底とする
- `AdapterRuntimeSettingsCollectionSO` が `List<AdapterRuntimeSettingsBase>` で sub-asset を束ねる
- `OscRuntimeSettingsSO` は Receiver/Sender を 1 つの SO に統合 (両 AdapterBinding が同じ SettingsSO を参照し、それぞれのセクションだけ読む)
- AdapterBinding (`OscAdapterBinding` / `OscSenderAdapterBinding`) は子 SO を直接 SerializeField で参照
- Collection SO の CustomEditor で sub-asset の追加/削除 UI を提供

## Adapter 追加/削除の想定レベル
- **対応レベル b**: Inspector でエントリ追加/削除、または新規 Adapter 種別を C# 型として追加することは安全に行える(既存パラメータが消失しない)
- **対応レベル c (型削除/リネーム)**: 今回はスコープ外。破壊的変更としてアナウンスする前提
- ただし将来の c 対応に向けて以下の拡張点を v0 から仕込んでおく:
  - `AdapterRuntimeSettingsBase._schemaVersion: int` フィールド
  - `ToJson()` / `FromJson()` API (各 RuntimeSettings サブクラスで override 可能)
  - `AdapterRuntimeSettingsCollectionSO.OnEnable()` でマイグレーション拡張点を予約(実装は空)

## 後方互換
- 既存 SO データとの後方互換性は **考慮不要**(プレリリース前、preview 段階)
- 既存テストは新構造に合わせて全面修正

## スコープ外
- Adapter 種別の C# 型削除/リネーム時の自動マイグレーション (将来別 spec)
- AdapterBinding (`OscAdapterBinding` / `OscSenderAdapterBinding`) の統合 (現状の 2 Binding 構造は維持)
- enabled フラグによる動作停止機構 (AdapterBindings リストからの除外で代替)
- ランタイムでの動的設定変更 UI

## 影響範囲
- `Packages/com.hidano.facialcontrol/Runtime/Adapters/AdapterBindings/` (Base, Collection の新規追加)
- `Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/AdapterBindings/OscAdapterBinding.cs` (フィールド削除 + Settings 参照追加)
- `Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/AdapterBindings/OscSenderAdapterBinding.cs` (同上)
- `Packages/com.hidano.facialcontrol.osc/Runtime/Settings/OscRuntimeSettingsSO.cs` (新規)
- `Packages/com.hidano.facialcontrol.lipsync/Runtime/Adapters/ULipSyncAdapterBinding.cs` (_deviceDescriptor 削除)
- `Packages/com.hidano.facialcontrol.lipsync/Runtime/Settings/LipSyncDeviceStore.cs` (新規, PlayerPrefs ラッパー)
- Editor: `AdapterRuntimeSettingsCollectionSO` の CustomEditor
- Tests: EditMode/PlayMode 両方で既存テストの修正と新規 SO 用テストの追加

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
