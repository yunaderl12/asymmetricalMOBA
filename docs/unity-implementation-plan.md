# Unity実装手順（ゼロから作る場合）

> Googleドキュメント「Asymmetrical MOBA（仮）企画書」のタブ2「Unity実装手順」を書き出したもの（2026-10-30時点）。
> 仕様の根拠は `game-design.md`。既存プロジェクトにはすでに実装済みの部分が多いため、実作業では「未実装の部分だけ」を拾って進める。

企画書タブの最新仕様をUnityでゼロから実装する場合の手順。推奨環境は Unity 6 + URP。手順は依存関係の順に並べており、各フェーズ末尾の「完了の目安」を満たしてから次へ進む。未定の数値は ScriptableObject に仮値で入れ、後から調整する。

## Phase 0. 環境構築

- Unity Hub から Unity 6（LTS）を入れ、3D（URP）テンプレートでプロジェクトを作成する。
- Package Manager で次を導入する: Input System、AI Navigation（NavMesh）、Cinemachine、TextMeshPro、Netcode for GameObjects（Phase 12で使用）。
- Git を設定し、Unity用の .gitignore（Library / Temp / Logs / UserSettings を除外）を置く。GitHub（private）に初回コミットする。
- フォルダ構成を作る: Scripts（Core / Hero / Demon / Monster / Spawner / Match / UI / Bot / Debug）、ScriptableObjects、Prefabs、Scenes、Materials。
- Tags（Player、Monster、StartTown_Core、DemonCastle_Core、Spawner、PowerItem）と Layers（Ground、Wall、Hero、Monster）を登録する。

完了の目安: 空のシーンが再生でき、リポジトリにプロジェクトが上がっている。

## Phase 1. データ定義（ScriptableObject）

- ClassData を作る: HP、SP、Atk、mAtk、Df、mDf、Spd、レベル毎の成長値（クラス別に統一）、スキル4種の参照。剣士・魔法使い・僧侶・重戦士の4アセットを作成する。
- SkillData を作る: 消費SP、クールタイム、射程、効果値の基礎値、スキルLv成長（基礎値 × (1 + (Lv-1) × 0.2)）、付与する状態異常。数値は未定のため仮値で入れる。
- MonsterData（HP、Atk、Spd、Cost、SpwnCool、MaxSpwn、Exp、飛行の有無）、SpawnerData（HP、Atk、射撃間隔、射程、検知範囲、MaxCost、MaxConcurrent）を作る。SpawnInterval は廃止済みのため作らない。
- DemonKingData（HP、MP、Spd、レベル毎の強化値、スキル）と、全体設定の BalanceConfig（ダメージ式の定数 K=20、魔王Lv間隔100秒、最大Lv16、勇者最大Lv20、試合時間30分、準備時間1分など）を作る。

完了の目安: インスペクターで全数値を編集でき、コードに数値の直書きが残っていない。

## Phase 2. 共通基盤システム

- DamageCalculator を作る: ダメージ = 攻撃値 × K / (K + 防御値)。物理は Atk と Df、魔法は mAtk と mDf を使う。コアや魔物の防御値にも同じ式を使う。最低ダメージとクリティカルの有無は未定のため、差し込み口だけ用意する。
- HealthComponent を作る: CurrentHP、MaxHP、TakeDamage、Heal、OnDeath イベント、ラストヒット／アシストの記録（直近10秒の寄与者）。
- StatusEffectController を作る: スタン、スロー、継続ダメージ、沈黙の4種。同種は上書き（残り時間は長い方・効果量は強い方）、スタンが解けた後1.5秒はスタン無効（仮値）。持続時間はスキルごとの仮値で実装する。
- AutoRegen を作る: 5秒間攻撃も被弾もなければ毎秒回復し、回復量は MaxXxx × (0.005 × 1.1^step)（切り上げ）。攻撃・スキル・被弾でリセットする。勇者は HP と SP、魔王は HP と MP が対象。
- チーム判定（勇者側／魔物・魔王側）を共通化し、ダメージ対象の判定に使う。

完了の目安: テスト用の的にダメージを与えて、計算式どおりの値が出る。状態異常と自動回復が単体で動く。

## Phase 3. MainStage の構築

- 200×120 の地面を作り、壁（z=±20）で3レーンに分割する。全レーン同一構造で、壁の切れ目でレーンが合流する。切れ目の位置・個数・幅は未定のため、まず中央付近と左右に1か所ずつ置く。
- StartTown（-80, 1, 0）と DemonCastle（80, 1, 0）を作る。柱4本、各コア（HP3000、Df仮30）、RegenTrigger（6×3×6）、CoreTrigger（5×3×5）を置く。
- スポナー5基を配置する: NW(-40,0,40)、NE(40,0,40)、Center(0,0,0)、SW(-40,0,-40)、SE(40,0,-40)。NE と SE はマップの縁を通る中継ウェイポイントを置く。
- 岩10個を置く。NavMeshSurface で 200×120 全域に NavMesh を焼き、岩・柱・コアを除外する。
- 勇者用の TPS カメラと、魔王用の BirdsEyeCamera（俯瞰・orthographic）を用意する。

完了の目安: NavMeshAgent を置いて、各スポナー位置から StartTown まで経路が引ける。

## Phase 4. 勇者（単体動作）

- 移動（WASD＋マウス視点）を Input System で作る。TPS カメラは Cinemachine を使い、移動速度は ClassData から読む。
- 通常攻撃（左クリック）を作る: 剣士は近接単体、重戦士は近距離の範囲、魔法使いは遠距離弾、僧侶は中距離の光弾。射程と攻撃間隔は仮値。
- スキル5スロットを作る: 1〜4はクラス専用、5は自由枠（他クラス12個＋共通3個＋ワープ地点設定スキル）。右クリックで発動、Shift長押しでスキルホイール。同じスキルはパーティ内で重複選択不可。
- レベルと経験値を作る: 必要 Exp = Lv × 100、上限 Lv20、レベルアップでクラス別成長値とスキルポイント+1。1〜5キーでスキルアップ（最大Lv5）。
- 経験値分配を作る: ラストヒット100%、アシスト50%、さらに戦闘に参加した近くの味方（攻撃または回復・バフ）へ少量共有。共有は半径15m以内、経験値の25%（仮値）。
- 死亡とリスポーンを作る: 待機 = 10 + 死亡回数 × 5 秒（上限は未定）、入力ロック、Body非表示、StartTown固定で復活。Y < -10 の落下時は Backspace で復帰。
- ワープ地点設定スキルを作る: 破壊済みスポナーより自陣側の範囲に限って地点を設定でき、そこへ移動できる。発動条件・詠唱・クールタイムは未定。

完了の目安: テストシーンで1人の勇者がクラスを切り替えながら戦え、レベルアップ・死亡・復活・ワープが動く。

## Phase 5. 魔物とスポナー

- 魔物プレハブを作る（BasicMob、TankMob、FlyingMob）。NavMeshAgent で、スポナーのウェイポイントに沿って StartTown へ進軍する。FlyingMob は高さ5mを飛行する。
- ターゲット選択を作る: 半径10mの勇者のうち最も近い勇者を追い、10m超で経路に復帰する。ヘイト値は一部の賢い魔物だけが持ち、Taunt はそのヘイトを加算する（対象魔物・増減条件は未定）。
- 接触ダメージを作る: 同じ魔物から1秒に1回。対象は Player と StartTown_Core。StartTown 到達後は停止して削り続ける。FlyingMob は接触範囲6m。
- BasicSpawner を作る: inv の魔物を魔物ごとの SpwnCool で順に湧かせる、同時生存数はMaxConcurrent、半径15m内の勇者を検知して射撃（LineRenderer）する。HP0で機能停止してグレー化し、復活はしない。破壊で大経験値と序盤向けボーナス（内容は未定）を与える。
- 魔物のレベル強化（魔王Lvごとに HP と Atk を上昇、仮値 +5%/Lv）と、レベルによる魔物の解禁（仮値: TankMob Lv3 / FlyingMob Lv6。解禁前は初期配置にも置けない）を、SpawnerData と連動して実装する。

完了の目安: スポナーから湧いた魔物が StartTown まで進軍してコアを削る。勇者を追い、スポナーを壊すと湧きが止まる。

## Phase 6. 魔王

- 魔王の移動と視点操作（ピッチ／ヨー）、HP、MP を作る。城内（RegenTrigger）で MP が毎秒+10 回復、城外で毎秒-5 のドレイン（Lv15で無効）。
- 城外滞在デバフを作る: 城外にいる時間に応じて移動速度と攻撃力のデバフが蓄積する。60秒で最大 -30% まで蓄積し、城内では同じ速さで徐々に回復する（仮値）。
- 魔王レベルを作る: 100秒ごとに自動レベルアップ（最大Lv16、25:00に到達）。レベルで魔物の強化・解禁・コスト上限増加・魔王本体の強化を行い、空の色を6段階で切り替える。Lv16は雷フラッシュを5〜12秒間隔で出す。
- 指揮官オーラを作る: 半径10m内の魔物に Atk×1.1、Spd×1.1。
- 俯瞰モードを作る: Core 接触後に V キーで切り替え、マップ全体を真上から見る。移動ロック、スポナーHPと魔物位置を表示する。コスト制限内で魔物を追加（削除不可）。勇者の把握は、スポナー検知範囲に入った勇者の表示、MP消費の探知スキル（仮値: MP40 / 5秒 / CT30秒）、城内侵入の警告（城内にいる間はマップにも表示）の3つ。
- 魔王スキル4系統を作る: 近接・範囲攻撃、魔物の強化・支援、勇者への妨害・設置、探知。具体的なスキル、MP消費、クールタイムは未定。キャラ別の差別化は後のフェーズで拡張する。

完了の目安: 魔王が城内外を行き来してデバフが蓄積し、俯瞰モードで魔物を追加できる。100秒ごとにレベルが上がり、空が変わる。

## Phase 7. 試合進行とシーン

- シーンを作る: Title、HeroLobby、DemonLobby、MatchLobby、MainStage、Result。Build Settings に順番に登録する。開発用のテストシーンは別に残す。
- MatchManager を作る: WaitingForStart → DemonPrepPhase（1分、スポナー割当）→ InMatch（30分）→ Finished。勝敗は、魔王側がStartTown_CoreのHP0、勇者側がDemonCastle_CoreのHP0または魔王のHP0、時間切れは魔王勝利。
- 勇者4人固定とし、足りない人数はBOTで補う。魔王がいない場合はBOT魔王で開始する。
- ロビーを作る: HeroLobby（クラスとスキル設定、クラスの重複は可、スキルは重複不可）、DemonLobby（20種類から20スロットのデッキ編成、スポナー初期設定）、MatchLobby。

完了の目安: タイトルからリザルトまで、1人プレイ（BOT補完）で最初から最後まで通せる。

## Phase 8. BOT

- BotHero を作る: StartTown周辺を巡回し、12m内の魔物と交戦する。後でスキル使用を追加する。
- BotDemonKing を作る: まず城内に立つだけの代役を作り、その後に魔物追加・スキル使用・城内での戦闘を行う行動を追加する。
- 切断・離脱時に、勇者と魔王の両方をBOTが引き継ぐ処理を作る。再接続時の引き継ぎは未定。

完了の目安: 人間が勇者でも魔王でも、残りをBOTが埋めて1試合が成立する。

## Phase 9. UI

- 基本UIを作る: TopBarUI（両コアHPと時間）、HeroHUD、DemonKingHUD、SpawnerListUI、MatchTimerUI、MatchResultUI、RespawnCountdownUI、PowerItemNotifierUI、FallRespawnPrompt、SkillWheel、SkillLevelUpUI、BirdsEyeControlUI。
- 勇者側の追加UIを作る: 味方のHP／状態表示とピン、コアHP危険・スポナー破壊の通知、ダメージ数字と被弾表示、魔王Lvアップ時の通知。ミニマップは対象外。

完了の目安: プレイ中に必要な情報がUIで確認でき、ロールごとに正しいHUDが出る。

## Phase 10. デバッグ機能

- ConsoleUI を作る（/ でコマンド、Tab でチャット、Enter実行、Escで閉じる、入力中は操作を無効化）。
- /debug コマンドを実装する（help、addexp、winhero、losehero、spawnitem、respawn、skipprep、usemp、selfdamage、skiplevel、switch、vision、skillup）。新仕様に合わせて、状態異常付与やワープなどのコマンドも追加すると検証が楽になる。

完了の目安: コマンドだけで各状況を再現でき、通しプレイを何度も試せる。

## Phase 11. 試合ギミック

- PowerItem を作る: 10分後から複数回出現（間隔は未定）、拠点・スポナーから10m以上離れたNavMesh上、勇者取得で Atk×2 / Spd×1.5 / Df×1.5（30秒）、魔王が触れると破壊。
- 中立のボス魔物と環境ギミック（台風・落とし穴）を作る。種類・配置・出現タイミングは未定。
- 魔物を最終的に20種類まで増やす（後衛・遠距離、サポート、特殊、ボス級を含む）。

完了の目安: 序盤から終盤まで、各時間帯に勇者の目標となる出来事がある。

## Phase 12. ネットワーク化（Netcode for GameObjects）

- 方式は、まずホスト型で作り、将来は専用サーバーへ移行する。判定と魔物管理はサーバー権限で設計する。
- NetworkManager、プレイヤーとスポナーなどの NetworkObject 化、NetworkTransform による位置同期を行う。
- HealthComponent の CurrentHP を NetworkVariable にし、攻撃・スキル発動・魔物追加を ServerRpc / ClientRpc に変える。魔物は同時数十体になるため、同期負荷を計測する。
- Unity Lobby と Relay でマッチメイキングを作る。勇者3＋魔王1から始め、勇者4＋魔王1まで動作確認する。
- 切断・再接続とBOT引き継ぎを実装する。

完了の目安: 複数台で、勇者と魔王が1試合を最後まで遊べる。

## Phase 13. 仕上げとバランス調整

- ビジュアル（空、雷、アニメーション、VFX、カメラシェイク）とオーディオ（SE、BGM、音量設定）を入れる。
- 30分の通しプレイで数値を調整する: コア破壊時間、魔王撃破時間、スポナー破壊速度、レベル到達速度。パラメータはScriptableObjectで調整する。
- 設定画面（キーバインド、音量、画質）、チュートリアル、リザルトの本格化、ランク・アンロック・シーズンの土台を作る。

完了の目安: 想定どおりの試合時間で決着し、勇者側・魔王側の勝率に極端な偏りがない。
