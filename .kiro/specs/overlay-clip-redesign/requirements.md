# Requirements Document

## Project Description (Input)
overlay-clip-redesign

Overlay 機能をスキーマレベルで刷新する preview.2 段階の破壊的変更。

【ドメイン側】
- FacialProfile に Slots（List<string>）を追加。Adapter Bindings の overlaySlot / Expression.Overlays / FacialProfile.DefaultOverlays が参照する slot 識別子の単一の真実とする。
- OverlaySlotBinding を旧 (slot + expressionId) から新 (slot + suppress + ExpressionSnapshot) へ刷新。3 状態 (default fallback / 明示 suppress / 個別 snapshot override) を表現する。Expression と FacialProfile.DefaultOverlays の双方が新型を保持する。
- OverlayInputSource は「expressionId 引き」から「Expression 内部の slot snapshot 引き」へ書き換える。default fallback は profile.DefaultOverlays から解決する。

【JSON】
- ProfileSnapshotDto に slots フィールド追加。
- OverlaySlotBindingDto を {slot, suppress, snapshot: ExpressionSnapshotDto} へ刷新。
- SystemTextJsonParser / JsonSchemaDefinition / FacialCharacterProfileConverter を新スキーマで読み書き。

【SO Serializable】
- OverlaySlotBindingSerializable に AnimationClip + cachedSnapshot + suppress フラグを追加。
- FacialCharacterProfileSO に _slots（List<string>）を追加。
- FacialCharacterProfileExporter で overlay clip もサンプリング対象にし、cachedSnapshot を更新。

【Inspector UI】
- 「表情」タブを「表情ライブラリ」「レイヤー」「ベース表情」「目線」「Adapter Bindings」「Debug」に再編。レイヤー定義タブと表情ライブラリタブを完全に分離する。
- 表情ライブラリタブに Slots 宣言セクション、Default Overlays セクション、Expression リストを配置。
- 各 Expression row に Layer プルダウンと Overlays セクションを追加。Overlays セクションは slot ごとに 3 状態 (Default / Suppress / 個別 AnimationClip) のラジオ + 個別時の AnimationClip フィールドを表示。
- Default Overlays UI は slot プルダウン × AnimationClip フィールドのリスト形式。

【Sample データ移行】
- Assets/Samples/.../MultiSourceBlendDemoCharacter.asset、Assets/StreamingAssets/.../profile.json、Packages/.../Samples~/.../profile.json を新スキーマに同期更新。blink_overlay は smile / smile_closed_eye の overlays に snapshot として焼き直す。

【テスト】
- OverlaySlotBindingTests / OverlayInputSourceTests / LayerUseCaseOverlayLayerTests / LayerUseCaseAnalogOverlayTests / LayerUseCaseAnalogExpressionAdditionTests / ExpressionUseCaseActiveProviderTests / SystemTextJsonParserOverlaysTests を新スキーマで TDD 書き換え。Inspector UI 用の Edit Mode テストも追加 (3 状態切替、AnimationClip 設定、Slots 宣言の整合性検証)。

【非機能要件】
- preview.2 段階のため後方互換は不要。旧 expressionId フィールドを Parser が読んだ場合は明確なエラーで弾く。
- OverlayInputSource の per-frame GC アロケーションはゼロ目標を維持。
- ARKit / 既存 input system / OSC 経路への影響を最小化する (overlaySlot 参照箇所のみ更新)。

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
