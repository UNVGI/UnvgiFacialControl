# Changelog

すべての変更は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) の形式に準拠し、[セマンティックバージョニング](https://semver.org/lang/ja/) に従います。

## [0.1.0-preview.1] - 2026-05-07

初回プレリリース。`com.hidano.facialcontrol` と `com.hidano.ulipsync-asio` を接続する Windows 向け uLipSync 連携アダプタとして提供。

### Added

- `com.hidano.facialcontrol.lipsync` の UPM パッケージ足場を追加し、`package.json`、標準 UPM ディレクトリ、Runtime / Editor / Tests asmdef、README、CHANGELOG、LICENSE を配置。
- `ULipSyncAdapterBinding` を追加し、Character Prefab に uLipSync 系コンポーネントを事前付与せず、再生時に `AudioSource`、`uLipSync.uLipSync`、Mic / ASIO 入力コンポーネントを動的に構築できるようにした。
- uLipSync の `LipSyncInfo.phonemeRatios` と `volume` を FacialControl の `lipsync` 入力ソースへ変換する `ULipSyncProvider` と音素エントリを追加。
- Mic / ASIO デバイス名の自動判定と `DeviceDescriptor` による同名デバイスの識別に対応。
- 実行中に入力デバイスを切り替える hot-swap API を追加し、切替時にゼロ値 settle を挟んで既存の Provider / InputSource 登録を維持できるようにした。
- 複数キャラクターがそれぞれ独立した `ULipSyncAdapterBinding` とデバイス設定を保持できる構成を追加。
- `ULipSyncProvider` のイベント受信、スナップショット蓄積、`GetLipSyncValues` のホットパスで GC アロケーション 0 byte を維持する方針と検証テストを追加。
- `ULipSyncAdapterBinding` 用の UI Toolkit PropertyDrawer を追加し、デバイス設定、Analyzer Profile、BlendShape / AnimationClip 形式の音素エントリを Inspector から編集できるようにした。
- `MicLipSyncDemo` sample を追加し、Package Manager から Import してマイク入力の最小構成を確認できるようにした。
- `Runtime/Resources/FacialControl/LipSync/Default uLipSync Profile.asset` を同梱し、`ULipSyncAdapterBinding._analyzerProfile` 未指定時に `Resources.Load` 経由でフォールバックされるようにした。
