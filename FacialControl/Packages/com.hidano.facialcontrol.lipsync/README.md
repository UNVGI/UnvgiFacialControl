# com.hidano.facialcontrol.lipsync

`com.hidano.facialcontrol.lipsync` は、FacialControl コアパッケージと `com.hidano.ulipsync-asio` を接続する uLipSync 連携アダプタパッケージです。uLipSync の解析結果を FacialControl のリップシンク入力ソースへ変換し、Character Prefab に uLipSync 系コンポーネントを事前追加しない構成で利用します。

## 対応範囲

- Unity 6000.3.2f1 以降
- Windows Editor / Windows Standalone
- `com.hidano.facialcontrol` 0.1.0-preview.2
- `com.hidano.ulipsync-asio` 3.1.5-custom.0 互換

本パッケージは Windows 限定です。`package.json` の `keywords` とこの README の両方で Windows-only として扱います。Preview 期間中のため、公開 API、SerializedProperty 名、サンプル構成、プロファイル形式は破壊的に変更される可能性があります。

## インストール

`Packages/manifest.json` の `dependencies` に本パッケージを追加します。`com.hidano.facialcontrol` と `com.hidano.ulipsync-asio` は本パッケージの依存関係として解決されます。

```json
{
    "dependencies": {
        "com.hidano.facialcontrol.lipsync": "0.1.0-preview.1"
    }
}
```

`com.hidano.ulipsync-asio` が導入されていない環境では、Package Manager の依存解決エラーまたはコンパイルエラーとして明示的に検出されます。

## 最小サンプル

1. Unity の Package Manager で **FacialControl uLipSync Adapter** を選択します。
2. **Samples** から **MicLipSyncDemo** を Import します。
3. `Assets/Samples/com.hidano.facialcontrol.lipsync/MicLipSyncDemo/Scenes/MicLipSyncDemo.unity` を開きます。
4. `Character` GameObject の下へ自分のモデルを配置します。
5. `Profiles/MicLipSyncDemoProfile.asset` の uLipSync binding で、使用するマイク名と A / I / U / E / O の BlendShape 名を環境に合わせます。
6. Play して、再生時に `ULipSyncAdapterBinding` が uLipSync 系コンポーネントを動的追加することを確認します。

`Character` GameObject は、再生前に `uLipSync.uLipSync`、`uLipSyncMicrophone`、`uLipSyncAsioInput`、`AudioSource` を持たない前提です。これにより、FacialControl の Adapter Binding 契約と同じく Character Prefab を汚さない運用を維持します。

## Hot Path GC 方針

`ULipSyncProvider` は、uLipSync event の受信、音素スナップショット蓄積、`GetLipSyncValues` による値書き込みのホットパスで GC アロケーション 0 byte を維持する方針です。毎フレーム実行される経路では LINQ、`string.Format`、`new T[]`、`new List<T>()`、`new Dictionary<,>()` などの暗黙アロケーションを避けます。

AnimationClip 形式エントリの time-0 サンプリングや内部バッファ構築は `OnStart` 時の初期化コストとして扱い、ホットパスから除外します。

## Samples の二重管理

`Samples~/` は UPM 配布用の正本です。開発プロジェクトで実際に Scene を開いて検証するため、同じ内容を `FacialControl/Assets/Samples/com.hidano.facialcontrol.lipsync/` にミラーします。

- 配布用: `FacialControl/Packages/com.hidano.facialcontrol.lipsync/Samples~/MicLipSyncDemo/`
- 開発検証用: `FacialControl/Assets/Samples/com.hidano.facialcontrol.lipsync/MicLipSyncDemo/`

どちらかを編集した場合は、Scene、Profile、README、`.meta` を含めて必ずもう一方へ同期します。同期漏れがあると、Package Manager から Import したユーザーのサンプルと開発プロジェクトで確認したサンプルの挙動がずれます。

## パッケージ内容

- `Runtime/`
- `Editor/`
- `Tests/EditMode/`
- `Tests/PlayMode/`
- `Tests/Shared/`
- `Samples~/MicLipSyncDemo/`
- `Documentation~/`

## ライセンス

[MIT License](LICENSE.md)
