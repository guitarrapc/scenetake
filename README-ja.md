[![Build](https://github.com/guitarrapc/scenario2cast/actions/workflows/build.yaml/badge.svg)](https://github.com/guitarrapc/scenario2cast/actions/workflows/build.yaml)

# scenario2cast

[English](README.md) | 日本語

YAMLシナリオファイルから [asciinema v3 cast](https://docs.asciinema.org/manual/asciicast/v3/) ファイルを生成するツールです。`asciinema`をインストール・起動する必要はありません。step を並べた YAML を書くだけで、実際にコマンドを実行してその出力を使った cast ファイルを生成します。

サンプルシナリオ`samples/basic.yaml`で生成したcastをgif/svgに変換すると...!タイプを頑張る必要もないし、実際にコマンドも走るので、リアルなデモが簡単に作れます。

```yaml
title: "Basic Demo"
width: 80
height: 24
shell: bash

settings:
  prompt: "$ "
  typing-speed: 0.02 # average seconds per keystroke
  typing-jitter: 0.01 # random variance (±) per keystroke
  pre-delay: 0.4 # pause before typing starts
  post-delay: 1.0 # pause after output before next step

steps:
  - echo "Hello, World!"
  - name: Name of the step
    run: echo "Current directory:"
  - name: Coloring stdout
    run: curl -I https://google.com
    highlight:
      - color: green
        at:
          - "1"
      - color: gray
        at:
          - "2-12"
  - echo "Wait for 2 seconds..."
  - run: sleep 2
    execution-duration: 1.0
  - echo "stderr output is red by default" 1>&2
  - echo "Done!"
```

| GIF | SVG |
| --- | --- |
| ![](samples/basic.gif) | ![](samples/basic.svg) |

**動機**

作りたいのはasciinemaのcastファイルであって、コマンドのレコードを頑張りたくない、scenario2castはこんな動機で作ったツールです。asciinemaの周辺に様々なツールがありますが、シェルスクリプトに寄っていたり、asciinema 本体を前提にしていたり、実行パスがcastに出力されたり、実行せずにそれっぽい出力だけを作るものだったりして、どれも欲しい形とはずれています。欲しいのは、シナリオを素直に書けて、列挙したコマンドを実際に実行し、その結果から cast ファイルを直接生成できるものです。

最終的にcastファイルがあればいいのでasciinema介さず、シナリオから直接castを生成するクロスプラットフォームツールです。

1. 実行するコマンドをシナリオに書く
2. コマンドを一定の間隔で入力しているように cast イベントを生成する
3. コマンドを実際に実行し、その出力を cast に書き出す

## クイックスタート

GitHub の Releases ページから利用 OS 向けアセットをダウンロードし、`scenario2cast`（Windows は `scenario2cast.exe`）を任意の場所に配置します。

```bash
# macOS/Linux は必要に応じて実行権限を付与
chmod +x ./scenario2cast

# 現在のディレクトリにスターターシナリオを作成
scenario2cast init

# シナリオを実行して cast ファイルを生成
scenario2cast scenario.yaml

# cast とアニメーション SVG を一度に生成
scenario2cast --format svg scenario.yaml

# SVG 出力のスタイルを細かく指定
scenario2cast --format svg scenario.yaml --font-size 20 --font-family "'Noto Sans Mono', ui-monospace"

# 既存の cast ファイルを SVG に変換
scenario2cast svg scenario.cast

# cast 生成時に正常な pre/post 実行ログも表示
scenario2cast --verbose scenario.yaml

# asciinemaで再生
asciinema play scenario.cast

# agg で gif に変換 (Linux/macOS) — v3 cast には agg 1.6.0 以降が必要。フォントサイズは --font-size で調整
docker run --rm -v "${PWD}:/data" ghcr.io/asciinema/agg /data/scenario.cast /data/scenario.gif --font-size 20

# agg で gif に変換 (Windows PowerShell)
docker run --rm -v "$($PWD.Path):/data" ghcr.io/asciinema/agg /data/scenario.cast /data/scenario.gif --font-size 20
```

**Usage**

```bash
# 新しいシナリオファイルを初期化
scenario2cast init [scenario.yaml]

# シナリオを実行して cast を生成
scenario2cast [--verbose] [--format cast|svg] scenario.yaml [output]

# 既存の cast ファイルを SVG に変換
scenario2cast svg <input.cast> [output.svg]
```

**Notes**

- `shell`:
  - Linux/macOS の既定シェルは `$SHELL`、なければ `bash`
  - Windows の既定シェルは `pwsh`、なければ `powershell`、Windows で `shell: bash` を指定した場合は Git Bash / MSYS の `bash` を使います
- `settings` でpromptとtiming の既定値を設定できます
- `render` は cast ヘッダーの表示メタデータ（`s2c:font-size` タグ、`term.theme`）と SVG 出力を制御します。詳細は [.github/docs/spec_cast.md](.github/docs/spec_cast.md) と [.github/docs/spec_svg.md](.github/docs/spec_svg.md)
- `pre` / `post` は録画フロー外で setup / teardown コマンドを実行します。stdout/stderr は CLI に表示されますが、cast ファイルには一切書き込まれません。
- `steps`:
  - 実際に実行されるため、ファイル変更や外部システムを操作するような副作用のあるコマンドは慎重に使ってください
  - `vim` や `htop` のような対話的コマンドは避けてください
  - 実行時間が長いコマンドは `execution-duration` で見やすく調整できます

## シナリオファイル形式

```yaml
title: "Demo Title"     # cast のタイトル（任意）
width: 120              # ターミナル幅（デフォルト: 120）
height: 24              # ターミナル高さ（デフォルト: 24）
cwd: /path/to/dir       # step を実行するディレクトリ（任意）
shell: bash             # 実行シェルを指定（任意）

settings:
  prompt: "$ "
  typing-speed: 0.05       # 1文字あたりの平均秒数
  typing-jitter: 0.015     # ジッター幅 ±秒
  pre-delay: 0.8           # 次の step のタイピング開始前の停止時間
  post-delay: 1.5          # プロンプト表示後・次の step 入力までの停止時間
  execution-duration: 0.1  # 任意。各コマンド実行の cast 上待機時間
  stderr-color: red        # stderr に ANSI SGR がない場合の既定色（デフォルト: red）

pre:
  - dotnet build

steps:
  # コマンドをリスト形式で書くと、settings の既定値が適用
  - echo "Hello, World!"
  - ls -la

  # コマンドをマッピング形式で書くと、コマンドごとに設定を上書き可能
  - run: git log --oneline -10
    post-delay: 3.0

  - run: git status
    typing-speed: 0.10
    pre-delay: 1.5
    post-delay: 2.0

  - run: sleep 2
    execution-duration: 0.4

  # run highlight
  - run: git log --oneline -3
    run-highlight: bright-cyan

  # stdout highlight
  - run: git status
    highlight:
      - color: yellow
        at: "4"                   # 4行目
      - color: red
        at: "6-7:3-"             # 複数行カラム帯。6-7行目、3列目から行末
      - color: bright-cyan
        at: "8-"                  # 8行目から出力末尾まで

  # stderr highlight（stderr に ANSI SGR がない場合）
  - run: echo "plain stderr" 1>&2

  # この step の stderr 既定色を上書き
  - run: echo "stderr override" 1>&2
    stderr-color: bright-yellow

post:
  - git clean -fd
```

### Pre/Post コマンド

`pre` と `post` は setup / teardown コマンドを書く top-level の文字列配列です。`steps` と同じ `shell` と `cwd` を使い、各配列要素は 1 つのコマンド文字列として shell に渡されます。空の要素は無視されます。

`pre` は `steps` の前に実行されます。fail-fast です。いずれかの `pre` コマンドが非 0 で終了した場合、後続の `pre` はスキップされ、`steps` は実行されず、cast ファイルは書き込まれず、`post` も実行されません。scenario2cast は失敗したコマンドの exit code で終了します。

`post` は `steps` の実行後、cast ファイルを書き込んだ後に実行されます。こちらも fail-fast です。いずれかの `post` コマンドが非 0 で終了した場合、後続の `post` はスキップされ、すでに書き込まれた cast ファイルはそのまま残り、scenario2cast は失敗したコマンドの exit code で終了します。

記録対象の `steps` は成功しても失敗しても、その結果が記録されます。step の exit code は scenario2cast の exit code を決めません。`pre` と `post` は録画フロー外です。stdout/stderr は元のストリームを保って CLI に表示されますが、コマンド文字列も出力も cast ファイルには一切書き込まれません。

`--verbose` を使うと、正常に実行された `pre` / `post` のコマンドラベルとフェーズマーカーを表示します。既存の `steps` の `running:` ログは常に表示されます。失敗した `pre` / `post` は、`--verbose` の有無に関係なく、完全なコマンド文字列と exit code を常に表示します。

- `highlight` は step のみ対応です（map-form の `run` step）。
- `run-highlight` は step のみ対応です（map-form の `run` step）。
- `stderr-color` は両対応です（`settings.stderr-color` の既定値 + step の `stderr-color` 上書き）。
- stderr 側に既存 ANSI がある場合はそれを保持し、`stderr-color` は ANSI がない stderr にのみ適用されます。

### スタイル指定（bold/underline/background/intensity）

`highlight.color`、`run-highlight`、`stderr-color` には、単純なカラー名に加えてスタイル文字列も指定できます。

- カラー名: `red`、`bright-cyan`
- スタイルトークン: `bold`、`underline`、`bright`
- 前景/背景プレフィックス: `fg:bright-white`、`bg:blue`、`fg:196`、`bg:235`
- 生 SGR リテラル: `1;31`、`38;5;196`、`48;5;235`、`\e[1;31m`、`\x1b[1;31m`

```yaml
steps:
  - run: git log --oneline -3
    run-highlight: "bold bright-cyan"

  - run: printf 'line1\nline2\n'
    highlight:
      - color: "underline fg:196 bg:235"
        at: "2"

  - run: echo "plain stderr" 1>&2
    stderr-color: "\\e[1;93m"
```

**カラー名とANSIコード**

| 名前 | ANSI SGR |
|------|----------|
| `black` | `30` |
| `red` | `31` |
| `green` | `32` |
| `yellow` | `33` |
| `blue` | `34` |
| `magenta` | `35` |
| `cyan` | `36` |
| `white` | `37` |
| `bright-black` (`gray`, `grey`) | `90` |
| `bright-red` | `91` |
| `bright-green` | `92` |
| `bright-yellow` | `93` |
| `bright-blue` | `94` |
| `bright-magenta` | `95` |
| `bright-cyan` | `96` |
| `bright-white` | `97` |

ANSI 256色パレットを使う場合は、`fg:<0-255>` / `bg:<0-255>`、または生SGRの `38;5;n` / `48;5;n` を指定します。

### コマンド設定一覧

| キー | 説明 | デフォルト |
|------|------|-----------|
| `run` | 実行するコマンド | 必須 |
| `typing-speed` | 1文字あたりの平均秒数 | `settings.typing-speed` |
| `typing-jitter` | ジッター幅 | `settings.typing-jitter` |
| `pre-delay` | タイピング前の停止時間 | `settings.pre-delay` |
| `post-delay` | プロンプト表示後の停止時間 | `settings.post-delay` |
| `execution-duration` | このコマンド実行の cast 上待機時間を上書き | `settings.execution-duration` |
| `run-highlight` | タイピングされるコマンド文字列に色を付ける | なし（stepのみ） |
| `stderr-color` | stderr に ANSI SGR がない場合の既定色 | `settings.stderr-color` |

### settings 対応 / step 対応メモ

- `highlight` はこのバージョンでは `settings` 既定値を持ちません。
- `run-highlight` はこのバージョンでは `settings` 既定値を持ちません。
- `stderr-color` は `settings` と step の両方で設定できます。

## Development

`dotnet` を使ってローカル実行・ビルドする場合は以下を利用します。

- .NET 10 SDK（C# file-based app 実行のため）

```bash
# ローカル実行
dotnet run scenario2cast.cs -- <scenario.yaml> [output.cast]
dotnet run scenario2cast.cs -- svg <scenario.cast> [output.svg]

# ビルド
dotnet publish scenario2cast.cs --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:DebugType=None
```
