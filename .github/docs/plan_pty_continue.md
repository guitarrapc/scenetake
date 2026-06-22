# PTY Continue — Implementation Plan

Status: **Proposed**

## Summary

シナリオ録画で `pty: true` の step を通常 step の直後に置くと、PTY セッション起動時にシェルが送る画面クリア等の ANSI シーケンスにより、それまで積み上がったターミナル状態が消える。本機能は step 単位の opt-in キー `pty-continue: true` を追加し、PTY キャプチャ先頭の **シェル初期化用シーケンスのみ** を除去して、前 step からの描画を継続できるようにする。

実装後は [spec_scenario.md](spec_scenario.md) と [spec_pty.md](spec_pty.md) を更新し、本 plan はアーカイブまたは削除する。

## Problem

### 現状の挙動

1. 通常 step（`pty: false`）はタイピング演出とパイプ出力で cast `o` イベントを生成し、仮想ターミナル（`ScreenBuffer` + `AnsiParser`）上に状態が蓄積される。
2. `pty: true` の step は **新しい PTY 子プロセス**を起動し、キャプチャした生バイト列をそのまま cast に追加する（[spec_pty.md](spec_pty.md) → Raw byte stream）。
3. シェル起動時、多くの環境で次のようなシーケンスが先頭付近に含まれる。

   | シーケンス | 効果 |
   |---|---|
   | `ESC [ ? 25 l` | カーソル非表示 |
   | `ESC [ 2 J` | 画面全体クリア |
   | `ESC [ m` | SGR リセット |
   | `ESC [ H` | カーソルを左上へ |
   | `ESC ] 0 ; title BEL` | ウィンドウタイトル（描画への影響は小） |
   | `ESC [ ? 25 h` | カーソル表示 |
   | Windows 固有: `ESC [ ? 9001 h/l`, `ESC [ ? 1004 h/l` | ConPTY 周辺のモード切替 |

4. 再生時に `2J` は `ClearDisplay(2)` として解釈されるため、PTY step の直前までの画面内容が消える。

### 再現例

[samples/scenario_format.cast](../samples/scenario_format.cast) の `pty demo` step（116 行付近）:

```
ESC[?25l ESC[2J ESC[m ESC[H …実際の echo 出力… ESC[?25h
```

[samples/pty.cast](../samples/pty.cast) の `matrix` step でも同様に `2J` が先に来た後、`?1049h` で alternate screen に入る。

### ユーザーが期待すること

- 通常 step → 軽い PTY step（`echo` など）→ 通常 step、という混在シナリオで、PTY 出力が **前のプロンプト行の続き** として見えること。
- `matrix` や `vim` のようなフルスクリーン TUI は、これまで通りクリアや alternate screen を含む生ストリームのまま録画できること。

## Goals

| # | Goal |
|---|---|
| G1 | `pty-continue: true` 指定時、PTY キャプチャ先頭のシェル初期化 ANSI を除去し、前 step のターミナル状態を維持したまま PTY 出力を追記する |
| G2 | デフォルト（`pty-continue: false` または省略）は **現状と同一** — TUI デモや `samples/pty.yaml` の挙動を変えない |
| G3 | 除去は **記録時（cast 生成時）** に行い、cast ファイルにはフィルタ後のバイト列を書く（再生側・SVG 側の変更は不要） |
| G4 | チャンク境界をまたぐ ANSI シーケンスでも正しく処理する（PTY は chunk 分割で届く） |

## Non-Goals

| # | Non-Goal | 理由 |
|---|---|---|
| N1 | `pty: true` でもタイピング演出（プロンプト + 1 文字入力 + Enter）を復活させる | 別機能。本 plan のスコープ外 |
| N2 | PTY 実行中・実行後の意図的な `clear` / `cls` / TUI の画面操作を抑制する | コマンド本体の挙動であり、初期化フィルタでは判別不能 |
| N3 | シェル起動方式の変更（`-lc` → `-c` など）をデフォルトで行う | 副作用（プロファイル未読込、環境変数差分）が大きい |
| N4 | 外部 `.cast` の `svg` 変換時にフィルタを適用する | シナリオ録画パスのみが対象 |
| N5 | カーソル位置を「前 step の末尾」へ明示的に再配置する | 初期化シーケンス除去でカーソルは自然に前位置を維持する想定。不足があれば follow-up |

## Proposed Design

### YAML キー

[spec_scenario.md](spec_scenario.md) の map-form step キーに追加:

| Key | Default | Requires | Description |
|---|---|---|---|
| `pty-continue` | `false` | `pty: true` | PTY キャプチャ先頭のシェル初期化 ANSI を除去し、前 step からの画面継続を試みる |

```yaml
steps:
  - echo "before pty"
  - name: "pty demo"
    run: echo "continues from previous screen"
    pty: true
    pty-continue: true
  - printf 'after pty\n'
```

### バリデーション

| 条件 | 挙動 |
|---|---|
| `pty-continue: true` かつ `pty: false`（または省略） | 警告を stderr に出し、`pty-continue` を無視する |
| `pty-continue` が最初の step で `true` | 許可。除去対象がなくても害はない（フィルタは no-op） |
| `pty-continue` の値が bool 以外 | 既存の step キーと同様にパースエラー |

### フィルタ適用タイミング

`GenerateAsync` 内で PTY チャンクを cast イベントに変換する **直前**（[scenetake.cs](../../scenetake.cs) の `execution.IsPty` 分岐）。`PtyCapture` 自体は変更しない。

```
PTY child bytes
    → (pty-continue) leading-init filter  ← 新規
    → CastEvent.OutputUtf8 × N
    → cast file
```

### 除去対象（Leading Init Phase）

**Leading Init Phase** を定義する: PTY 出力ストリームの先頭から、**最初の「意味のあるユーザー出力」が現れるまで**の区間。

この区間内で、次に該当するシーケンスを **除去** する（出現順不同、繰り返し可）:

#### CSI — 画面・カーソル

| シーケンス | 除去理由 |
|---|---|
| `ESC [ 2 J` | 画面全体クリア（主因） |
| `ESC [ 1 J` | カーソルから画面端まで消去 |
| `ESC [ 0 J` / `ESC [ J` | カーソルから行末まで消去 |
| `ESC [ H` / `ESC [ row ; col H` / `ESC [ row ; col f` | カーソルを左上等へ移動（`2J` 除去だけでは上書きになる） |
| `ESC [ ? 25 l` / `ESC [ ? 25 h` | カーソル表示切替（見た目のみ、除去して問題ない） |

#### CSI — モード（プラットフォーム固有）

| シーケンス | 除去理由 |
|---|---|
| `ESC [ ? 9001 h/l` | Windows ConPTY |
| `ESC [ ? 1004 h/l` | Windows focus reporting |
| `ESC [ ? 2004 h/l` | bracketed paste（出現時） |

#### SGR リセット（初期化文脈のみ）

| シーケンス | 除去理由 |
|---|---|
| `ESC [ m` / `ESC [ 0 m` | シェル起動時の属性リセット |

#### OSC（任意）

| シーケンス | 方針 |
|---|---|
| `ESC ] 0 ; … BEL`（タイトル） | **除去してよい**（描画に影響しない） |
| その他 OSC | Leading Init Phase 内なら除去してよい |

#### 除去しないもの（Leading Init Phase 内でも）

| シーケンス | 理由 |
|---|---|
| `ESC [ ? 1049 h/l` / `ESC [ ? 47 h/l` / `ESC [ ? 1047 h/l` | alternate screen — TUI の意図的操作。`pty-continue` でも残す |
| `ESC [ 2 K` 等の行消去 | 初期化というよりコマンド出力の一部になりうる |
| 印刷可能文字・改行 | Phase 終了トリガー |

### Leading Init Phase の終了条件

次のいずれかで Phase を終了し、**以降のバイトは一切フィルタしない**:

1. **印刷可能なユーザー出力** — UTF-8 として解釈可能な非制御文字（改行 `\r`/`\n` を除く制御文字以外）が 1 バイト以上出現
2. **alternate screen 入場** — `?1049h` 等の検出
3. **バッファ上限** — 先頭 **4096 バイト** を超えても Phase が終了しない場合はフィルタを打ち切り、残りは生のまま通す（安全弁）

> **設計意図:** `echo` のような軽い PTY step では、シェル初期化の直後にすぐ本文が来る。`matrix` で `pty-continue: true` を誤指定した場合でも、`?1049h` または本文出現で Phase が終わるため、TUI 本体への影響を最小化する（ただし先頭の `2J` は除去される — 誤指定時は画面はクリアされない点に注意。デフォルト `false` で回避）。

### チャンク境界

フィルタは **PTY 全チャンクを時系列順に連結した論理ストリーム** に対して状態機械を適用し、除去後に元のチャンク境界（タイムスタンプ）を可能な限り維持する。

- 空になったチャンクは cast イベントとして emit しない
- 1 チャンクが分割される場合は、先頭部分のみ当該 chunk の timestamp を使い、分割後の追加分は同一 timestamp でよい（既存 PTY も chunk 単位で同 timestamp がありうる）

### デフォルトとの互換性

| 設定 | 挙動 |
|---|---|
| `pty: true`（現状） | 変更なし。生バイト列をそのまま記録 |
| `pty: true` + `pty-continue: true` | 先頭 Leading Init Phase のみフィルタ後に記録 |

## Expected User-Visible Behavior

### Before（現状）

```
$ echo "before"
before
# pty demo
[画面がクリアされる]
This command runs in a PTY...
$ printf 'after'
```

### After（`pty-continue: true`）

```
$ echo "before"
before
# pty demo
This command runs in a PTY...
$ printf 'after'
```

- `# pty demo` コメント行（`name` キー）は従来通り PTY 実行前に emit される
- PTY 出力は `before` の次行以降に追記される（カーソルは前 step 終了位置を維持）

### `samples/pty.yaml`（`matrix`）— デフォルト維持

`pty-continue` 未指定のため、現状どおりシェル `2J` → `matrix` → alternate screen の流れを保持。

## Interactions

| 機能 | 影響 |
|---|---|
| `name` コメント行 | 変更なし。PTY 前に出力 |
| `highlight` / `stderr-color` | PTY step では引き続き無効（[spec_highlight.md](spec_highlight.md)） |
| `execution-duration` | 変更なし。PTY は chunk タイムスタンプと `execution-duration` の max |
| SVG 再生 `TrimTrailingBlankRestore` | 変更なし。フィルタ後の cast を入力するため |
| 決定性（deterministic seed） | `pty-continue` は YAML に含める。フィルタは決定的であること |
| 外部 cast / `scenetake svg` | 対象外 |

## Risks and Limitations

| Risk | Mitigation |
|---|---|
| シェルや OS ごとに初期化シーケンスが異なる | 代表パターンを fixture + integration test で固定。未知シーケンスは安全弁（4096 バイト）で打ち切り |
| `pty-continue: true` を TUI step に誤用 | ドキュメントで用途を明示。デフォルト `false` |
| フィルタがコマンド出力の ANSI を誤除去 | Phase は先頭区間のみ。印刷可能文字出現後はフィルタ停止 |
| `2J` 除去後に `H` が残ると左上から上書き | `H` / `f` もセットで除去（本 plan の要件） |
| 前 step 終了時カーソルが行末でなく画面下部 | 自然な継続は「その位置から追記」。スクロールは既存ターミナルエミュレータに委ねる |

## Verification Plan

### Unit tests（新規: `tests/pty_continue_test.cs` または `pty_test.cs` へ統合）

| Case | Assert |
|---|---|
| `2J` + `H` + テキスト | テキストのみ残る |
| チャンク分割: `[ESC[2` + `Jtext]` | 正しく結合除去 |
| Phase 終了後の `2J` | 除去されない |
| `?1049h` 含むストリーム | alternate screen シーケンスは残る |
| 空ストリーム / 初期化のみ | 空または OSC のみ除去 |
| `pty-continue` 無効時 | 入力 = 出力（パススルー） |

### Integration tests（`SCENETAKE_BIN`）

| Fixture | Assert |
|---|---|
| `tests/fixtures/pty-continue.yaml`（新規） | 通常 step → `pty-continue` PTY echo → 通常 step。生成 cast に `2J` が含まれないこと |
| 既存 `pty.yaml` / `pty-unix.yaml` 等 | **回帰なし**（バイト列同一） |

### Manual / sample update

- [samples/scenario_format.yaml](../samples/scenario_format.yaml) の `pty demo` に `pty-continue: true` を追加し、[samples/scenario_format.cast](../samples/scenario_format.cast) を再生成
- SVG/GIF があれば同様に更新

## Documentation Updates（実装後）

| Document | Change |
|---|---|
| [spec_scenario.md](spec_scenario.md) | `pty-continue` キー行を `steps` テーブルに追加 |
| [spec_pty.md](spec_pty.md) | Leading init filter の節を追加。`pty-continue` 時は raw ではなくフィルタ後を記録する旨 |
| [spec_index.md](spec_index.md) | 「PTY 後も画面を継続」ナビゲーション行（任意） |
| [README.md](../README.md) / [README-ja.md](../README-ja.md) | 混在シナリオの短い例（任意） |
| 本 plan | Status を **Implemented** に更新するか、内容を spec に移して削除 |

## Implementation Outline

実装の詳細手順は spec に書かない方針（[document-spec-policy](https://github.com/guitarrapc/scenetake/blob/main/.github/docs/docs_authoring_guidelines.md)）に従い、ここでは担当境界のみ示す。

| Component | Responsibility |
|---|---|
| `PtyLeadingInitFilter`（新規、配置は実装時判断） | バイトストリームへの状態機械フィルタ |
| `GenerateAsync` | `pty-continue` 読み取り、PTY チャンクへのフィルタ適用 |
| Step パース | `GetBool(extra, false, "pty-continue")` と `pty` 併用チェック |
| Tests | 上記 Verification Plan |

## Open Questions

| # | Question | 提案（デフォルト案） |
|---|---|---|
| Q1 | キー名は `pty-continue` で確定か？ | `pty-continue` を推奨（`pty` と対になる kebab-case） |
| Q2 | `pty-continue: true` 時もタイピング演出を付けるか？ | v1 は **付けない**（N1）。需要があれば `pty-typing: true` 等で follow-up |
| Q3 | verbose ログに除去バイト数を出すか？ | `scenetake --verbose` 時のみ `pty-continue: stripped N bytes` を出す |
| Q4 | settings レベルのデフォルト override は要るか？ | v1 は **step のみ**。全 step PTY 継続は稀 |

## Alternatives Considered

| Alternative | 却下理由 |
|---|---|
| デフォルトで常に `2J` を除去 | TUI デモ（`matrix`）や意図的 `clear` との互換を壊す |
| 再生時（`TerminalReplay`）で除去 | cast ファイルが asciinema 互換として生バイトを保持しない。外部ツールでも直らない |
| シェルを `--noprofile` / `-NoProfile` のみで対処 | `2J` は Profile 無しでも出る（実測: Git Bash, pwsh） |
| PTY を別ターミナルセッションとして合成描画 | 実装コスト・asciinema モデルとの乖離が大きい |
| `TERM=dumb` で PTY 起動 | 色・TUI が壊れ、`pty` の目的に反する |

## Related Documents

- [spec_pty.md](spec_pty.md) — 現行 PTY 録画仕様
- [spec_scenario.md](spec_scenario.md) — step キー定義
- [spec_svg.md](spec_svg.md) — `OutputContainsAnsiBlankIndicator`（トレーリング空白トリム。本機能とは独立）
- [samples/scenario_format.cast](../samples/scenario_format.cast) — 問題の再現サンプル

## Lessons Learned（調査時点）

- PTY 起動時の `2J` はシェルプロファイルだけでなく、ConPTY / 対話シェル初期化でも発生する。
- `matrix` は `2J` の後に `?1049h` で alternate screen に入るため、「メイン画面の復元」では前 step の内容は戻らない（alternate screen の仕様）。
- `2J` と `H` はセットで扱わないと、クリアは防げても左上からの上書きが残る。
