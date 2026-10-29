[![Build](https://github.com/guitarrapc/scenetake/actions/workflows/build.yaml/badge.svg)](https://github.com/guitarrapc/scenetake/actions/workflows/build.yaml)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/scenetake.svg?label=scenetake%20nuget)](https://www.nuget.org/packages/scenetake)
[![npm](https://img.shields.io/npm/v/scenetake.svg?label=scenetake%20npm)](https://www.npmjs.com/package/scenetake)

# scenetake

[English](README.md) | 日本語

YAML **scenario** を実行し、ターミナルの scene take（[asciinema v3 `.cast`](https://docs.asciinema.org/manual/asciicast/v3/)、アニメーション `.svg`）を記録するツールです。`asciinema`をインストール・起動する必要はありません。step を並べた YAML を書くだけで、実際にコマンドを実行し、その出力を `.cast`（任意で `.svg` も）として記録します。通常の step はタイピング演出付き、`pty: true` の step は端末ストリームをリアルタイムで記録します。

サンプルシナリオ `samples/basic.yaml` から `.cast` と `.svg` を生成すると、こんな見た目になります。タイプを頑張る必要もないし、実際にコマンドも走るので、リアルなターミナル記録が簡単に作れます。

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

SVG 出力ではフォント、ライト/ダークテーマ、ウィンドウ枠（`macos` / `windows`）を指定できます。

| macOS | Windows |
| --- | --- |
| ![](samples/theme-macos.svg) | ![](samples/theme-windows.svg) |

## モチベーション

「コマンドをキャプチャしたいけど、コマンドのタイピングを頑張りたくない」、scenetakeはこんな動機で作ったツールです。asciinemaの周辺に様々なツールがありますが、シェルスクリプトに寄っていたり、asciinema 本体を前提にしていたり、実行パスがcastに出力されたり、実行せずにそれっぽい出力だけを作るものだったりして、どれも欲しい形とはずれています。欲しいのは、シナリオを素直に書けて、コマンドを実際に実行し、その結果から cast ファイルを直接生成できるものです。

scenetake は、asciinema を介さずシナリオから `.cast`（任意で `.svg` も）を直接生成するクロスプラットフォームツールです。

1. 実行するコマンドをシナリオに書く
2. 通常の step は一定の間隔で入力しているように cast イベントを生成する。`pty: true` の step は端末ストリームをリアルタイムで記録する
3. コマンドを実際に実行し、その出力を cast に書き出す

## クイックスタート

GitHub の Releases ページから利用 OS 向けアセットをダウンロードし、`scenetake`（Windows は `scenetake.exe`）を任意の場所に配置します。

```sh
# .NET グローバルツール（インストールには .NET 10 SDK が必要。実行には .NET 10 ランタイムが必要）
dotnet tool install -g scenetake

# npm
npm install -g scenetake

# Windows (Scoop)
scoop bucket add guitarrapc https://github.com/guitarrapc/scoop-bucket
scoop install scenetake
```

![](samples/quickstart.svg)

```bash
# macOS/Linux は必要に応じて実行権限を付与
chmod +x ./scenetake

# 現在のディレクトリにスターターシナリオを作成
scenetake init

# シナリオを実行して cast ファイルを生成
scenetake scenario.yaml

# cast とアニメーション SVG を一度に生成
scenetake --format svg scenario.yaml

# SVG のスタイル: フォント、テーマ、ウィンドウ枠、フレーム上限
scenetake --format svg scenario.yaml --font-size 20 --font-family "'Noto Sans Mono', ui-monospace" --theme light --window macos

# 既存の cast ファイルを SVG に変換（v2 / v3 対応）
scenetake svg scenario.cast

# svg サブコマンドでも同じスタイルフラグが使える
scenetake svg scenario.cast --theme light --window windows --max-fps 30

# cast ファイルを mp4（H.264、ffmpeg が必要）に変換。全画面 TUI や共有に向く
scenetake mp4 scenario.cast

# X (Twitter) の動画アップロード制限に収める
scenetake mp4 scenario.cast --max-size 1280x1024

# cast と mp4 を一度に生成
scenetake --format mp4 scenario.yaml

# cast 生成時に正常な pre/post 実行ログも表示
scenetake --verbose scenario.yaml

# asciinemaで再生
asciinema play scenario.cast

# agg で gif に変換 (Linux/macOS) — v3 cast には agg 1.6.0 以降が必要
docker run --rm -v "${PWD}:/data" ghcr.io/asciinema/agg /data/scenario.cast /data/scenario.gif --font-size 20 --last-frame-duration 0

# agg で gif に変換 (Windows PowerShell)
# GIF はコンテナ内に書き出してから取り出す。Windows の Docker Desktop でバインドマウントに直接書くと GIF が壊れることがある
$id = docker create -v "$($PWD.Path):/data:ro" ghcr.io/asciinema/agg /data/scenario.cast /tmp/scenario.gif --font-size 20 --last-frame-duration 0
docker start -a $id
docker cp "${id}:/tmp/scenario.gif" scenario.gif
docker rm $id
```

**Usage**

```bash
# 新しいシナリオファイルを初期化
scenetake init [scenario.yaml]

# シナリオを実行して cast（既定）、cast + SVG、または cast + mp4 を生成
scenetake [--verbose] [--format cast|svg|mp4]
  [--font-size N] [--font-family FAMILIES] [--theme dark|light]
  [--window none|macos|windows] [--max-fps N]
  [--fps N] [--crf N] [--max-size WxH] [--font-file PATH]
  scenario.yaml [output]

# 既存の cast ファイルを SVG に変換
scenetake svg [--font-size N] [--font-family FAMILIES] [--theme dark|light]
  [--window none|macos|windows] [--max-fps N]
  <input.cast> [output.svg]

# 既存の cast ファイルを mp4 に変換
scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme dark|light]
  [--fps N] [--crf N] [--max-size WxH]
  <input.cast> [output.mp4]
```

`--max-fps` は SVG アニメーションのフレームレート上限です（`0` = オフ、cast のタイミングをそのまま使用）。`--format svg` 時と `svg` サブコマンドで有効です。

mp4 用のオプションは `--format mp4` 時と `mp4` サブコマンドで有効です。

- `--fps`: フレームレート（既定 `30`、最大 `60`）
- `--crf`: 画質（既定 `23`。大きいほど小さいファイル）
- `--max-size`: この範囲に収まるよう縮小（X 向けなら `1280x1024`）
- `--font-file`: 使うフォントファイル

**SVG と mp4 の使い分け**

タイピング中心や短い出力なら、たいてい SVG が最も小さく（mp4 の半分程度になることも多い）、README に埋め込めて、拡大しても文字がぼけません。毎フレーム画面全体を描き直す全画面 TUI（`matrix`、`btop` など）は、SVG や GIF だと非常に大きくなります。こうした録画や、X・Slack での共有には mp4 を使ってください。たとえば `matrix --fps 60` を 200×60 で 7.5 秒録ると、SVG は 19.7MB、mp4 は 8.0MB（`--max-size 1280x1024` なら 3.9MB）です。SVG が 10MB を超えると、scenetake は警告を表示します。

mp4 出力は、libx264 入りの [ffmpeg](https://ffmpeg.org/) を別プログラムとして呼び出します。インストール方法は次のとおりです。

- Windows: `winget install Gyan.FFmpeg` または `scoop install ffmpeg`
- macOS: `brew install ffmpeg`
- Debian/Ubuntu: `sudo apt install ffmpeg`

特定の ffmpeg を使うときは `SCENETAKE_FFMPEG` にそのパスを指定します。

mp4 は手元にインストールされたフォントで描画します。`font-family` の一覧を順に探し、見つからない文字は日本語などの CJK フォントや記号フォントで補います。フォントを固定したいときは `--font-file` を指定してください。`--window` を付けると、SVG と同じタイトルバーを描画します（外側の余白と影はありません）。

**Notes**

- `shell`:
  - Linux/macOS の既定シェルは `$SHELL`、なければ `bash`
  - Windows の既定シェルは `pwsh`、なければ `powershell`、Windows で `shell: bash` を指定した場合は Git Bash / MSYS の `bash` を使います
- `settings` でpromptとtiming の既定値を設定できます
- `render` は cast ヘッダーの表示メタデータ（`st:font-size` タグ、`term.theme`）と SVG 出力を制御します。詳細は [.github/docs/spec_cast.md](.github/docs/spec_cast.md) と [.github/docs/spec_svg.md](.github/docs/spec_svg.md)
- `pre` / `post` は録画フロー外で setup / teardown コマンドを実行します。stdout/stderr は CLI に表示されますが、cast ファイルには一切書き込まれません。
- `steps`:
  - 実際に実行されるため、ファイル変更や外部システムを操作するような副作用のあるコマンドは慎重に使ってください
  - ターミナルを必要とするコマンドは `pty: true` で pseudo-terminal capture を有効化できます。詳細は [PTY 録画](#pty-録画)。
  - 実行時間が長いコマンドは `execution-duration` で見やすく調整できます
  - 長い出力を自然にリプレイしたい場合（`docker build` や `sleep` を挟むステップなど）は、その step に `stream-output: true` と必要なら `max-duration` を指定します。デフォルトの pipe emit はタイピング演出のため一括表示のままです

**GIF で日本語などの CJK 文字を使う**

`ghcr.io/asciinema/agg` イメージには CJK フォントが入っていません。agg はどのフォントにもない文字を何も描画しないため、日本語・中国語・韓国語の文字（全角カタカナなど）は空白になります。`fonts/` などのフォルダに CJK フォントを置いて `--font-dir` で渡し、`--text-font-family` の欧文等幅フォントの後ろにそのファミリー名を追加してください。先頭のフォントがセル幅の基準になります。

```bash
# Linux/macOS
docker run --rm -v "${PWD}:/data" ghcr.io/asciinema/agg /data/scenario.cast /data/scenario.gif --font-size 20 --last-frame-duration 0 --font-dir /data/fonts --text-font-family "JetBrains Mono,BIZ UDGothic"

# Windows PowerShell
# フォントのフォルダはコンテナにコピーする。Windows の Docker Desktop ではバインドマウント上のフォントを読み込めない（Input/output error）
$id = docker create -v "$($PWD.Path):/data:ro" ghcr.io/asciinema/agg /data/scenario.cast /tmp/scenario.gif --font-size 20 --last-frame-duration 0 --font-dir /fonts --text-font-family "JetBrains Mono,BIZ UDGothic"
docker cp .\fonts "${id}:/fonts"
docker start -a $id
docker cp "${id}:/tmp/scenario.gif" scenario.gif
docker rm $id
```

`BIZ UDGothic`（OFL、Google Fonts で配布）は一例です。ほかの CJK フォントでも、そのファミリー名を指定すれば使えます。

## 出力モード

出力モードは、通常出力、ストリーム出力、TUI出力、対話型アプリケーション出力の4種類があります。モードはstepごとに切り替えられます。

### 通常出力

通常のstepは、コマンドの stdout/stderr をキャプチャしてタイピング演出付きで記録します。このモードは単発出力のコマンドや、TTYを必要としないコマンドに適しています。出力に色がついていなくても、`highlight` や `stderr-color` で色を付けることができます。出力は、実際の出力に関わらず、一瞬で表示されます。

```yaml
steps:
  - name: comment color is cyan
    run: echo hello
  - name: "[yellow] comment color override"
    run: echo world
  - name: run & stdout color default
    run: date
  - name: run color override
    run: date
    run-highlight: "bold underline green"
```

| GIF | SVG |
| --- | --- |
| ![](samples/highlight.gif) | ![](samples/highlight.svg) |

### ストリーム出力

出力が間欠的に発生するコマンドは、`stream-output: true` を指定することで、キャプチャしたタイミングをそのまま再生できます。`max-duration` を指定すると、step の出力タイムラインを制限し、長時間の idle gap を stream emit で圧縮できます。

```yaml
steps:
  - name: "stream-output (ping, staggered replay)"
    run: ping -n 4 127.0.0.1
    stream-output: true
```

| GIF | SVG |
| --- | --- |
| ![](samples/stream-output.gif) | ![](samples/stream-output.svg) |

### TUI 出力

ターミナル UI やリアルタイムアニメーション（`matrix`、プログレスバーなど）をキャプチャする場合、`pty: true` を指定します。PTY が有効になり、子プロセスの実行中に端末バイトストリームを記録します。PTY サイズはシナリオの `width` / `height` を用います。

```yaml
steps:
  - run: matrix --duration 5
    pty: true               # TTY依存コマンドの出力をリアルタイムでキャプチャ。対話しないならこれだけでOK
```

| GIF | SVG |
| --- | --- |
| ![](samples/pty.gif) | ![](samples/pty.svg) |

> [!WARNING] `pty: true` では stdout と stderr はバイトストリームに統合されるため、その step に `stderr-color` やパイプ向けの `highlight` は適用されません。[samples/pty.yaml](samples/pty.yaml) と [spec_pty.md](.github/docs/spec_pty.md) を参照してください。

scenetake はすべてのコマンドを `COLORTERM=truecolor` 付きで実行します。そのため環境変数から色数を決めるツールも、24bit カラーで記録されます。

### 対話型アプリケーション出力

対話型アプリケーション（`vim`、`copilot` など）をキャプチャする場合、`pty: true` と `interactive: true` を指定します。PTY が有効になり、子プロセスでの対話を可能にします。`normalize-typing: true` を指定すると、バナーやヘルプなどの出力は実時間のまま、ユーザー入力のタイピングだけを読みやすく再タイミングします。

```yaml
steps:
  - name: "Copilot CLI with banner"
    run: copilot --banner
    pty: true               # TTY依存コマンドの出力をリアルタイムでキャプチャ
    interactive: true       # 対話アプリケーション用
    normalize-typing: true  # バナーは実時間のまま、入力タイピングだけ読みやすく再タイミング

  - name: "edit in vim"
    run: vim README.md
    pty: true               # TTY依存コマンドの出力をリアルタイムでキャプチャ
    interactive: true       # 対話アプリケーション用
```

| GIF | SVG |
| --- | --- |
| ![](samples/interactive-pty.gif) | ![](samples/interactive-pty.svg) |

> [!WARNING] `pty: true` では stdout と stderr はバイトストリームに統合されるため、その step に `stderr-color` やパイプ向けの `highlight` は適用されません。[samples/interactive-pty.yaml](samples/interactive-pty.yaml) と [spec_pty.md](.github/docs/spec_pty.md) の Interactive PTY を参照してください。


## シナリオファイル形式

必須なのは `steps` のみで、他の top-level キーはすべて任意です。以下は実行可能なサンプルです。正本は [samples/scenario_format.yaml](samples/scenario_format.yaml)。フィールド定義の詳細は [.github/docs/spec_scenario.md](.github/docs/spec_scenario.md)。色・スタイル構文は [spec_highlight.md](.github/docs/spec_highlight.md)。pre/post の挙動は [spec_pre_post.md](.github/docs/spec_pre_post.md)。色の追加例は [samples/highlight.yaml](samples/highlight.yaml)。

![](samples/scenario_format.svg)

```yaml
# Scenario format reference — runnable demo (samples/scenario_format.yaml).
# Required: steps only. Everything else is optional.

title: "Scenario Format Demo"  # Optional cast title
width: 80                        # Default: 120
height: 24                       # Default: 24
# cwd: /your/project             # Optional working directory for all commands
shell: bash                      # bash | pwsh | powershell | path. Windows: Git Bash when bash

render:                          # Optional display metadata for cast header and SVG
  font-size: 16
  font-family: ui-monospace, monospace
  theme:
    preset: dark                 # dark | light; optional fg / bg / palette overrides
  window: macos                  # none | macos | windows

settings:                        # Defaults for all steps; map-form steps can override per key
  prompt: "$ "
  typing-speed: 0.04             # Average seconds per typed character
  typing-jitter: 0.01            # Random variance (+/- seconds) per character
  pre-delay: 0.5                 # Pause before typing each step
  post-delay: 1.0                # Pause after output before the next step
  execution-duration: 0.1        # Cast wait after command execution
  stderr-color: red              # When stderr has no ANSI SGR sequences
  # stream-output: false         # When true, replay captured chunk times in the cast
  # max-duration: 15             # Cap step output timeline; also enables stream emit
  # silence-threshold: 2.0       # Stream mode: collapse idle gaps longer than this (seconds)
  # max-idle: 0.2                # Stream mode: collapsed idle gap duration (seconds)

# Optional setup; runs before steps, not recorded in cast
pre:
  - echo "environment check (not recorded)"

steps:
  # String form - uses settings defaults
  - echo "Hello, World!"

  # Map form - comment line, per-step timing
  - name: "[cyan]Project status"
    run: printf 'name\tversion\nscenetake\t1.0\n'
    post-delay: 1.5

  - name: "Per-step timing overrides"
    run: printf 'typing slowly...\n'
    typing-speed: 0.12
    pre-delay: 0.8
    post-delay: 1.2

  - name: "execution-duration"
    run: sleep 1
    execution-duration: 0.5

  - name: "instant emit (ping, one blob)"
    run: bash -c 'case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) ping -n 4 127.0.0.1 ;; *) ping -c 4 127.0.0.1 ;; esac'
    execution-duration: 0.2

  - name: "stream-output (ping, staggered replay)"
    run: bash -c 'case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) ping -n 4 127.0.0.1 ;; *) ping -c 4 127.0.0.1 ;; esac'
    stream-output: true
    silence-threshold: 2.0
    max-idle: 0.2

  - name: "pty demo"
    run: echo "This command runs in a PTY, so output appears immediately and can include interactive features like colors and progress bars."
    pty: true

  - name: "run-highlight colors the typed command"
    run: printf 'done\n'
    run-highlight: bright-cyan

  - name: "stdout highlight"
    run: printf 'line1 alpha\nline2 beta gamma\nline3\n'
    highlight:
      - color: yellow
        at: "1"
      - color: red
        at: "2:7-10"
      - color: bright-cyan
        at: "2:12-"
      - color: underline fg:bright-white bg:blue
        at: "3"

  - name: "stderr (default red)"
    run: echo "plain stderr" 1>&2

  - name: "stderr-color override"
    run: echo "bright yellow stderr" 1>&2
    stderr-color: bright-yellow

  - echo "Done!"

# Optional teardown; runs after cast is written, not recorded
post:
  - echo "demo complete (not recorded)"
```

## Development

`dotnet` を使ってローカル実行・ビルドする場合は以下を利用します。

- .NET 10 SDK（C# file-based app 実行のため）

```bash
# ローカル実行
dotnet run scenetake.cs -- <scenario.yaml> [output.cast]
dotnet run scenetake.cs -- svg <scenario.cast> [output.svg]

# ビルド
dotnet publish scenetake.cs --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:DebugType=None
```

各シナリオの目的とプレビュー（GIF / SVG）は [samples/README-ja.md](samples/README-ja.md) を参照してください。

サンプルシナリオを `.cast` と `.gif` に変換するには、以下のようにします。

```bash
dotnet run samples/regenerate.cs
# GIF はコンテナ内に書き出してから取り出す（クイックスタートの Windows 向け agg の注記を参照）
foreach ($file in Get-ChildItem samples/*.cast) {
  $id = docker create -v "$($PWD.Path):/data:ro" ghcr.io/asciinema/agg /data/samples/$($file.BaseName).cast /tmp/out.gif --last-frame-duration 0
  docker start -a $id
  docker cp "${id}:/tmp/out.gif" samples/$($file.BaseName).gif
  docker rm $id
}
```

### Release

タグを打つ前に、`Directory.Build.props` と `npm/packages/` 配下の npm パッケージをまとめて更新します。スクリプトは最新の `v*` git タグを基準にします（タグがない場合はファイル上の現在値を使います）。

```bash
dotnet run scripts/set_version.cs -- patch   # 例: v1.3.0 -> 1.3.1
dotnet run scripts/set_version.cs -- minor   # 例: v1.3.0 -> 1.4.0
dotnet run scripts/set_version.cs -- major   # 例: v1.3.0 -> 2.0.0
```

バージョン更新をコミットしたあと、新しいバージョンに対応するタグ（例: `v1.4.0`）を作成して push します。タグの push で release workflow が走り、ネイティブバイナリのビルド、GitHub Release の作成、`scenetake` と `@scenetake/*` プラットフォームパッケージの stage が行われます。

workflow 完了後、ローカルで 2FA を使って staged パッケージを approve します。

```bash
npm stage list
npm stage approve <stage-id>
```

初回の npm リリースだけは手動の `npm publish` が必要です。staged publishing は registry 上にパッケージが既に存在していることが前提です。メインの `scenetake` より先に、プラットフォームパッケージ（`@scenetake/linux-x64` など）を approve してください。
