# Requirements Document

## Project Description (Input)
gaze-config-promotion: GazeConfig を FacialCharacterProfileSO ルートへ昇格し、Inspector セクションを再編する。

【背景】
adapter-binding-architecture spec の改修で _gazeConfigs は InputSystemAdapterBinding 内部に格納される構造になったが、以下の問題を抱えている:
1. 目線ボーンパス・可動範囲・LookXxxClip はキャラ/モデル固有データであり、入力源（InputSystem/OSC/ARKit）と独立であるべき。現状は InputSystem に密結合
2. FacialCharacterProfileSOInspector に gaze 関連のデッドコード（_gazeConfigsProperty 周辺、AppendGazeConfigForExpression 等）が残置されている
3. GazeConfig の opt-in 追加 UX が無く、現状は InputSystemAdapterBindingDrawer の素の PropertyField でリスト編集のみ
4. 参照モデルを再割当てしても GazeConfigs が自動更新されない
5. Inspector セクション順が直感に反している（AdapterBindings → Layers → ReferenceModel → Debug）

【目的】
- GazeConfig を SO ルート直下に昇格し、入力源非依存のキャラ設定として正しく位置づける
- Inspector を「参照モデル → レイヤーと表情 → GazeConfigs → AdapterBindings → Debug」の順で再構成
- GazeConfig は Analog Expression の中から明示的に opt-in で紐づける UX に変更（自動連動はしない）
- 参照モデル割当て時、bone path が空の GazeConfig を自動補完。手動値は保持
- 「全 GazeConfig を参照モデルから再解決」一括ボタンを GazeConfigs セクションに追加
- Expression 行から ID 表示を撤去し Debug セクションへ集約
- ExpressionKind に Vector2 kind は新設しない（Analog のままで sidecar として GazeConfig を opt-in）

【スコープ】
- com.hidano.facialcontrol コア: FacialCharacterProfileSO / GazeBindingConfig / IFacialCharacterProfile / FacialCharacterProfileConverter / FacialCharacterProfileExporter
- com.hidano.facialcontrol.inputsystem: InputSystemAdapterBinding / GazeExpressionConfig / InputSystemAdapterBindingDrawer
- profile.json スキーマ: gaze_configs[] を root 直下へ。schemaVersion bump（破壊的変更を許容、preview 段階のため migration 不要）
- Sample asset: MultiSourceBlendDemoCharacter.asset / profile.json を新スキーマへ手術
- Editor Inspector: FacialCharacterProfileSOInspector セクション再編・dead code 削除
- 既存テスト: InputSystemAdapterBindingTests / IntegrationTests / RoundTrip 系を更新

【スコープ外】
- ExpressionKind に Vector2 kind を新設すること
- OSC/ARKit native での gaze 駆動の実装（IBonePoseSource インターフェースは将来拡張可能な状態に保つが実装は別 spec）
- BlendShape-only gaze の挙動変更
- 自動まばたき・視線追従ターゲット指定（preview.2 以降の別 spec）
- bone path の相対化（backlog S-1 と一緒に対処する選択肢はあるが本 spec では対象外）

【preview リリース方針】
preview 段階のため破壊的変更（JSON schema bump / Sample asset 移植）を許容。migration コードは書かない。CHANGELOG / migration-guide には変更内容を記録する。

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
