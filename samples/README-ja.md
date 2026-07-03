# Sample scenarios

[English](README.md) | 日本語

scenetake のサンプル集です。各シナリオの**目的**と**生成物**（GIF / SVG）を一覧で確認できます。

## 一覧

| シナリオ | 目的 | 入力 | 出力 |
|----------|------|------|------|
| [basic](#basic) | 入門・タイピング・ハイライト・sleep | [basic.yaml](basic.yaml) | [cast](basic.cast) · [gif](basic.gif) · [svg](basic.svg) |
| [scenario-format](#scenario-format) | シナリオ形式の全体リファレンス（README 埋め込み） | [scenario_format.yaml](scenario_format.yaml) | [cast](scenario_format.cast) · [svg](scenario_format.svg) |
| [demo](#demo) | README 用の一連のワークフロー | [demo.yaml](demo.yaml) | [cast](demo.cast) · [gif](demo.gif) · [svg](demo.svg) |
| [git](#git) | 実コマンドの git デモ | [git.yaml](git.yaml) | [cast](git.cast) · [gif](git.gif) · [svg](git.svg) |
| [highlight](#highlight) | コメント・stdout / stderr の色指定 | [highlight.yaml](highlight.yaml) | [cast](highlight.cast) · [gif](highlight.gif) · [svg](highlight.svg) |
| [pty](#pty) | `pty: true` で TTY 依存コマンド（`matrix`）を録画 | [pty.yaml](pty.yaml) | [cast](pty.cast) · [gif](pty.gif) · [svg](pty.svg) |
| [interactive-pty](#interactive-pty) | ホスト端末での対話録画（`interactive: true`） | [interactive-pty.yaml](interactive-pty.yaml) | 手動のみ |
| [theme](#theme) | light テーマ・16 / 256 色 | [theme.yaml](theme.yaml) | [cast](theme.cast) · [gif](theme.gif) · [svg](theme.svg) |
| [theme-macos](#theme-macos) | macOS ウィンドウ chrome | [theme-macos.yaml](theme-macos.yaml) | [cast](theme-macos.cast) · [gif](theme-macos.gif) · [svg](theme-macos.svg) |
| [theme-windows](#theme-windows) | Windows ウィンドウ chrome | [theme-windows.yaml](theme-windows.yaml) | [cast](theme-windows.cast) · [gif](theme-windows.gif) · [svg](theme-windows.svg) |
| [resize](#resize) | ターミナル resize イベント（cast のみ） | [resize.cast](resize.cast) | [gif](resize.gif) · [svg](resize.svg) |

## 再生成

リポジトリルートで実行します。

```bash
# すべての *.yaml から .cast と .svg を再生成
dotnet run samples/regenerate.cs

# GIF は agg が必要（Docker 例。v3 cast は ghcr.io/asciinema/agg を使用）
docker run --rm -v "$PWD:/data" ghcr.io/asciinema/agg \
  /data/samples/basic.cast /data/samples/basic.gif --last-frame-duration 0
```

---

## basic

**目的:** scenetake の基本機能を一通り見る入門サンプル。

- ステップ名コメント、タイピング演出、`curl` 出力の行ハイライト
- `sleep` による待ち、`stderr` のデフォルト色（赤）

| GIF | SVG |
|-----|-----|
| ![](basic.gif) | ![](basic.svg) |

```bash
scenetake --format svg samples/basic.yaml
```

---

## scenario-format

**目的:** メイン README に埋め込んでいるシナリオ YAML 形式の実行可能リファレンス。

- top-level 各セクション（メタデータ、`settings`、`render`、無害な `pre`/`post`、文字列 / マップ形式の `steps`）
- 固定 `printf` 出力での `run-highlight` / `highlight` / `stderr-color`、および依存なしの最小 `pty: true` echo 例（git やファイル変更なし）
- README プレビュー用に `render.window: macos`

| SVG |
|-----|
| ![](scenario_format.svg) |

```bash
scenetake --format svg samples/scenario_format.yaml
```

---

## demo

**目的:** README に載せる「YAML → cast → GIF」までの流れを再現するデモ。

- `cat` でシナリオを表示 → `scenetake` で cast 生成 → `agg` で GIF 化（Docker）

| GIF | SVG |
|-----|-----|
| ![](demo.gif) | ![](demo.svg) |

```bash
scenetake --format svg samples/demo.yaml
```

---

## git

**目的:** 実際の `git` コマンド出力を記録した、リポジトリ向けデモ。

- `git status` / `log` / `branch` / `diff --stat`
- ステップごとの `post-delay`・`typing-speed` の調整例

| GIF | SVG |
|-----|-----|
| ![](git.gif) | ![](git.svg) |

```bash
scenetake --format svg samples/git.yaml
```

---

## highlight

**目的:** scenetake 固有の色付け（cast イベントへの SGR 注入）の総合サンプル。

- コメント行の色（`name` の `[style]`）
- `run-highlight`（入力コマンドの色）
- `highlight`（stdout の行・範囲指定）
- `stderr-color`（名前付き色・SGR リテラル）
- 256 色インデックス・true color

| GIF | SVG |
|-----|-----|
| ![](highlight.gif) | ![](highlight.svg) |

```bash
scenetake --format svg samples/highlight.yaml
```

---

## pty

**目的:** 疑似端末（`pty: true`）で TTY 依存コマンドを録画するサンプル。

- 導入 step はタイピング演出あり。その後 `matrix 5` を `pty: true` で実行（コマンドのタイピング演出のあと PTY キャプチャ）
- [`matrix`](https://github.com/guitarrapc/matrix) の出力で SVG の **Matrix rain contextual tint**（緑の隣の白ハイライトを明るい緑で描画）も確認できる
- 再生成時は `matrix` CLI が `PATH` に必要
- 追加依存なしの最小 `pty: true` 例は [scenario-format の pty demo step](#scenario-format) を参照

| GIF | SVG |
|-----|-----|
| ![](pty.gif) | ![](pty.svg) |

```bash
scenetake --format svg samples/pty.yaml
```

---

## interactive-pty

**目的:** ホスト端末で TUI を操作しながら録画する（`pty: true` + `interactive: true`）。

- セッション開始前に `run` のタイピング演出を cast に記録
- 操作中の PTY 出力を実時間でバッファし、子プロセス終了で step 終了（`:wq` など）
- **手動のみ** — 実ターミナル（stdin/stdout TTY）と `vim` 等が必要。事前生成 cast/SVG なし

```bash
scenetake --format svg samples/interactive-pty.yaml out/interactive-pty
```

---

## theme

**目的:** SVG / cast ヘッダーの **`render.theme`**（light プリセット）と ANSI 色の見え方。

- `render.font-size` / `theme.preset: light`
- 16 色・256 色の printf 出力

| GIF | SVG |
|-----|-----|
| ![](theme.gif) | ![](theme.svg) |

```bash
scenetake --format svg samples/theme.yaml
```

---

## theme-macos

**目的:** **`render.window: macos`** ウィンドウ chrome + dark テーマ。

| GIF | SVG |
|-----|-----|
| ![](theme-macos.gif) | ![](theme-macos.svg) |

```bash
scenetake --format svg samples/theme-macos.yaml
```

---

## theme-windows

**目的:** **`render.window: windows`** ウィンドウ chrome + dark テーマ。

| GIF | SVG |
|-----|-----|
| ![](theme-windows.gif) | ![](theme-windows.svg) |

```bash
scenetake --format svg samples/theme-windows.yaml
```

---

## resize

**目的:** cast の **`r`（resize）イベント** を SVG がどう扱うかの確認用。

- YAML はなく、手編集の [resize.cast](resize.cast) のみ（40×8 → 80×12 → 50×6）
- 縮小時の viewport クリップと chrome 連動の確認向け

| GIF | SVG |
|-----|-----|
| ![](resize.gif) | ![](resize.svg) |

```bash
scenetake svg samples/resize.cast
```
