[![Build](https://github.com/guitarrapc/scenetake/actions/workflows/build.yaml/badge.svg)](https://github.com/guitarrapc/scenetake/actions/workflows/build.yaml)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
[![NuGet](https://img.shields.io/nuget/v/scenetake.svg?label=scenetake%20nuget)](https://www.nuget.org/packages/scenetake)
[![npm](https://img.shields.io/npm/v/scenetake.svg?label=scenetake%20npm)](https://www.npmjs.com/package/scenetake)

# scenetake

[English](README.md) | 日本語

scenetakeは、YAMLシナリオに書いたコマンドからターミナルのデモを録画するツールです。

![](samples/showcase/welcome.svg)

## なぜscenetake?

手で録ったターミナルのデモは、ツールが変わると古くなります。撮り直す時、コマンドを全部打ち直して出力を待ち、打ち間違えないよう気を付けて、何度も取り直したくなくなります。

scenetakeはデモの作り直しを簡単にするためのツールです。YAMLシナリオを用意すればコマンドを自動入力するので、撮り直しが簡単です。TUIや対話型アプリでは書いたとおりにキーを自動入力し、トークンやユーザー名は隠します。出力が変わらないステップは前回と同じに録れるのでデモを撮り直しても、差分は変更のあった部分だけです。

## ギャラリー

| [fastfetch](samples/showcase/fetch.yaml) | [bat、git、jq](samples/showcase/devtools.yaml) |
| --- | --- |
| ![](samples/showcase/fetch.svg) | ![](samples/showcase/devtools.svg) |
| [figlet、lolcat、toilet](samples/showcase/logo.yaml) | [sl](samples/showcase/sl.yaml) |
| ![](samples/showcase/logo.svg) | ![](samples/showcase/sl.svg) |
| [`keys`で操作するvimとgit diff（背景は画像）](samples/showcase/vim.yaml) | [トークンとパスワードを自動で隠す](samples/showcase/secrets.yaml) |
| ![](samples/showcase/vim.svg) | ![](samples/showcase/secrets.svg) |

![](samples/showcase/nyancat.svg)

## 仕組み

scenetakeはステップに書いたコマンドを実際に実行します。ステップを設定した速さで入力し、コマンドの実出力を記録します。`pty: true`ステップは疑似端末で実行され、TUIやプログレスバーもターミナルと同じように描かれます。

録画はasciinema v3形式のcastで、SVG/PNG/mp4/GIFで出力できます。castをgitコミットしておくと、コマンドを実行し直さずに別の形式やテーマで描き直せます。

## 例

[samples/basic.yaml](samples/basic.yaml)のシナリオは、次のように録画されます。

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/guitarrapc/scenetake/v1.5.0/schema/scenario.schema.json
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
    run: curl -sI https://google.com
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

## 使い始める

### インストール

[Releases](https://github.com/guitarrapc/scenetake/releases)からLinux、macOS、Windows向けのバイナリをダウンロードするか、次のいずれかでインストールします。asciinemaや.NETランタイムは不要です。

```sh
# dotnet tool
dotnet tool install -g scenetake

# npm
npm install -g scenetake

# scoop
scoop bucket add guitarrapc https://github.com/guitarrapc/scoop-bucket
scoop install scenetake
```

mp4とGIFには[ffmpeg](https://ffmpeg.org/)が必要です。`winget install Gyan.FFmpeg`、`brew install ffmpeg`、`sudo apt install ffmpeg`などでインストールできます。

### 録画する

![](samples/quickstart.svg)

ひな形のシナリオscenario.yamlを作ります。

```sh
scenetake init
```

シナリオを実行してscenario.svgとして録画します。scenario.castも作成します。

```sh
scenetake --format svg scenario.yaml
```

シナリオなしで1コマンドを録画します。シナリオとして残して、ステップを足すこともできます。

```sh
scenetake capture -- git log --oneline -5
scenetake capture --save-scenario help.yaml -- mycli --help
```

フォルダ内のシナリオをまとめて録画します。

```sh
scenetake --format svg demos/*.yaml
```

コマンドを実行し直さず、castからmp4を作ります。

```sh
scenetake mp4 scenario.cast
```

全オプションを確認します。

```sh
scenetake --help
```

### エディターとAIエージェント

シナリオファイルの1行目に、scenetakeのバージョンの[シナリオスキーマ](schema/scenario.schema.json)を指定します。VS Codeの[YAML拡張](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml)や、yaml-language-serverを使うほかのエディターで、キーと値が補完され、キーの説明、キーの綴り間違いや値の誤りが分かります。

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/guitarrapc/scenetake/v1.5.0/schema/scenario.schema.json
```

コーディングエージェント向けにscenetakeスキルを導入すると、シナリオの書き方、録画、結果の確認、調整の進め方をエージェントが判断できます。

```sh
npx skills add guitarrapc/scenetake
```

## CLIリファレンス

コマンドとオプションの用途の一覧です。値の範囲と既定値は`scenetake --help`や`scenetake <コマンド> --help`で確認できます。

### コマンド

| コマンド | 用途 |
| --- | --- |
| `scenetake <scenario.yaml> [output]` | シナリオを実行して録画する。`[output]`はファイル名で、`out`ならout.castとout.svgを書きます |
| `scenetake <scenario.yaml>...` | 複数のデモを1回で録画する。CIで`docs/*.yaml`を撮るときなど |
| `scenetake svg\|mp4\|png\|gif <input.cast> [output]` | コマンドを実行し直さず、castから別の形式を作る |
| `scenetake capture -- <command>` | シナリオを書かずに1コマンドを録画する |
| `scenetake init [scenario.yaml]` | コメント付きのひな形からシナリオを始める |
| `scenetake --version` | バージョンを確認する |

### オプション

`capture`は`--save-keys`、`--save-outputs`、`--replay`以外のシナリオのオプションを使えます。svg、mp4、png、gifコマンドは、見た目のオプションとその形式のオプションを使えます。

| オプション | 用途 |
| --- | --- |
| **録画** | |
| `--format cast\|svg\|mp4\|png\|gif` | castのほかに1つの形式を書く。指定しなければ、シナリオは`outputs`に並べたファイルを、`capture`はSVGを書きます |
| `--strict` | ステップのコマンドが失敗したらエラーで止める。壊れたデモをCIで残さないために |
| `--save-keys FILE` | `interactive: true`のステップで打ったキーを保存し、次からの録画で自動入力する |
| `--save-outputs FILE` | 各ステップの出力を`--replay`用に保存する |
| `--replay FILE` | コマンドを実行し直さず、見た目、タイミング、形式を変える |
| `--verbose` | 実行中のフェーズと、止まったときのエラーの詳細を見る |
| **見た目**（シナリオの`render`やcastの保存値を上書き） | |
| `--theme NAME` | カラーテーマを選ぶ。明るいページには明るいテーマなど |
| `--font-size N`、`--font-family FAMILIES` | 文字を読みやすくする、好きなフォントを使う |
| `--window none\|macos\|windows`、`--window-title TEXT` | タイトルバーを付けてターミナルのウィンドウらしく見せる |
| `--background VALUE` | ウィンドウの周りを色、グラデーション、画像にする |
| `--opacity N`、`--blur N` | 背景をウィンドウに透かす、ぼかす |
| `--margin N`、`--padding N` | ウィンドウの周りと文字の周りの余白を変える |
| `--fit none\|height\|width\|both` | 出力より下の空き行を描かない（既定）。`none`でターミナル全体を描く |
| `--crop-top N`、`--crop-bottom N`、`--crop-left N`、`--crop-right N` | 見せたくない行や列を端から切る |
| **アニメーションと静止画** | |
| `--no-loop`、`--loop` | SVGやGIFを1回だけ再生して最後の画面で止める。シナリオやcastがループしない設定でもループさせる |
| `--end-hold N` | 最初に戻る前や終わる前に、最後の画面を読む時間を取る |
| `--fade-out N` | 最後に文字をフェードアウトさせて終える |
| `--speed N` | 録り直さずに、SVG、mp4、GIFを短く（`1.5`）、またはゆっくり（`0.5`）にする |
| `--max-fps N` | 画面がよく変わるSVGを小さくする |
| `--at TIME` | ある時点の静止画をSVGかPNGで作る。`end`なら最後の画面 |
| **mp4、GIF、PNG** | |
| `--fps N` | mp4とGIFの滑らかさとファイルサイズを調整する。滑らかさを維持するため`--speed`を上げると既定値も上がる |
| `--crf N` | mp4の画質とファイルサイズを調整する |
| `--max-size WxH` | サイトのアップロード上限に収める。Xなら`1280x1024` |
| `--size WxH` | `1920x1080`など、決まったサイズの動画を作る |
| `--font-file PATH` | インストールしていないフォントファイルを使う。mp4、GIF、PNGは文字を自分で描き、SVGは見る人のフォントを使います |
| **capture** | |
| `--width N`、`--height N` | ターミナルのサイズを決める |
| `--shell NAME` | 既定以外のシェルでコマンドを実行する |
| `--pipe` | パイプで受けた出力を録画する。多くのツールは色なしで出力します |
| `--timeout N` | `tail -f`など、自分では終わらないコマンドを止める |
| `-o, --output NAME` | ファイル名を決める。`-o demo`ならdemo.castとdemo.svg |
| `--save-scenario FILE` | コマンドをシナリオとして残し、ステップを足して撮り直せるようにする |
| **init** | |
| `--shell bash\|pwsh\|powershell` | ひな形のステップをそのシェル向けに書く |

PATHにないffmpegを使うときは、`SCENETAKE_FFMPEG`にそのパスを設定します。

### 出力形式

| 形式 | 作り方 | 備考 |
| --- | --- | --- |
| `.cast` | `scenetake scenario.yaml` | asciinema v3形式。`asciinema play`や[agg](https://docs.asciinema.org/manual/agg/)でも使えます |
| `.svg` | `--format svg`、`scenetake svg scenario.cast` | アニメーション。`--at`で1画面の静止画 |
| `.png` | `--format png`、`scenetake png scenario.cast` | 1画面の静止画 |
| `.mp4` | `--format mp4`、`scenetake mp4 scenario.cast` | [ffmpeg](https://ffmpeg.org/)が必要 |
| `.gif` | `--format gif`、`scenetake gif scenario.cast` | [ffmpeg](https://ffmpeg.org/)が必要 |

1回で複数フォーマットを出力する時は`outputs`キーを使います。

```yaml
outputs:
  - format: svg                # demo.svg（README用）
  - format: svg
    theme: github-light
    file: demo-light.svg
  - format: gif                # Zenn用（3MB以下）
    font-size: 12
    fps: 10
  - format: mp4                # X用
    max-size: 1280x1024
```

### 終了コード

| コード | 意味 |
| --- | --- |
| `0` | 録画できた。`--strict`なしなら、ステップのコマンドが失敗しても0です |
| `1` | シナリオ、オプション、ファイルのエラー、ffmpegがない、`--strict`でステップが失敗した、複数のシナリオのうち録画できなかったものがある |
| その他 | `pre`か`post`のコマンドが失敗した。そのコマンドの終了コードを返します |

## シナリオファイル形式

`steps`キーのみ必須です。`: `を含むコマンドは`run: 'echo "answer: $a"'`のように引用符で囲んでください。キーの詳細は[spec_scenario.md](.github/docs/spec_scenario.md)にあります。

| キー | 既定値 | 内容 |
| --- | --- | --- |
| `title` | — | castのタイトル。`window-title`がなければタイトルバーの文字にもなります |
| `width`、`height` | `80`、`24` | ターミナルの列数と行数 |
| `cwd` | 実行したフォルダ | コマンドを実行するフォルダ。相対パスはシナリオのフォルダから |
| `shell` | `$SHELL`かbash、Windowsでは`pwsh` | `bash`、`pwsh`、`powershell`、またはパス |
| `render` | — | 見た目。[見た目の調整](#見た目の調整)を参照 |
| `outputs` | — | castのほかに書くファイル。[出力形式](#出力形式)を参照 |
| `settings` | — | 全ステップの既定値。プロンプト、入力速度、待ち時間、マスク |
| `pre`、`post` | — | ステップの前後に実行するコマンド。録画しません |
| `steps` | 必須 | 録画するコマンド |

### 出力モード

#### 通常出力

コマンドの入力に続けて出力を記録します。`highlight`で色を付けられます。

```yaml
steps:
  - name: comment color is cyan
    run: echo hello
  - name: run color override
    run: date
    run-highlight: "bold underline green"
```

| GIF | SVG |
| --- | --- |
| ![](samples/highlight.gif) | ![](samples/highlight.svg) |

#### ストリーム出力

`stream-output: true`で、出力を実際のタイミングで再生します。

```yaml
steps:
  - run: ping -n 4 127.0.0.1
    stream-output: true
```

| GIF | SVG |
| --- | --- |
| ![](samples/stream-output.gif) | ![](samples/stream-output.svg) |

#### TUI出力

`pty: true`でTUIやプログレスバーを記録します。

```yaml
steps:
  - run: matrix --duration 5
    pty: true
```

| GIF | SVG |
| --- | --- |
| ![](samples/pty.gif) | ![](samples/pty.svg) |

`keys`でキー操作をTUIに送り、`timeout`で止めるまでの時間を書けます。

```yaml
steps:
  - run: vim greet.py
    pty: true
    timeout: 30             # 30秒たったらCtrl+C
    keys:
      - wait: "greet.py"    # 出力にこの文字が出るまで待つ
        type: "/return\n"   # 1文字ずつ入力する。改行はEnter
      - key: escape         # enter、tab、up、f1、ctrl+c、alt+xなど
      - type: ":wq\n"
```

#### 対話型アプリケーション出力

`interactive: true`で、自分で操作しながら録画します。

```yaml
steps:
  - run: vim README.md
    pty: true
    interactive: true
```

| GIF | SVG |
| --- | --- |
| ![](samples/interactive-pty.gif) | ![](samples/interactive-pty.svg) |

`--save-keys`で操作を`keys`として保存しておけば、次からは同じ操作を自動で再生して録画できます。Windowsではパスワードも保存されます。

```bash
scenetake --save-keys keys.yaml demo.yaml
```

### ステップをまたぐ

bash、zsh、PowerShellでは、`cd`や`export`が後のステップにも引き継がれます。

```yaml
steps:
  - run: cd app && npm install
    show: none              # 録画せずに実行する
  - run: npm run dev
    background: true        # 動かしたまま次へ進む
    wait: "Local:"          # この文字が出たら次へ
  - curl -s localhost:5173/api/hello
  - clear: true             # 画面を消す
```

### タイトル画面とプロンプト

`card`ステップは、ウィンドウ中央寄せでオープニング表示します。`prompt`に`{dir}`を指定するとステップごとにそのフォルダ名を表示します。

```yaml
settings:
  prompt:
    - text: "{dir}"
      color: "fg:cyan"
    - " $ "
steps:
  - card:
      - "[bold fg:bright-cyan]my-app"
      - "Fast JSON diffs in your terminal"
    post-delay: 2.5
  - my-app diff a.json b.json
  - card: "[fg:yellow]npm install -g my-app"
```

### 見た目の調整

シナリオの`render`キーか、同じ名前のフラグ（`--window macos`など）で指定します。

| キー | 内容 |
| --- | --- |
| `window`、`window-title` | タイトルバー（`macos` / `windows`）とその文字 |
| `background` | ウィンドウの周り。色、グラデーション（`"#1e3c72 #2a5298"`）、画像 |
| `opacity`、`blur` | 画像の背景を透かす、ぼかす |
| `margin`、`padding` | 余白（px） |
| `loop`、`end-hold`、`fade-out` | 1回だけ再生（`--no-loop`）、最後の画面を表示する秒数、文字を消す秒数 |
| `speed` | SVG、mp4、GIFを録画の何倍の速さで再生するか。`0.25`〜`4` |

`speed`はタイピング、間、出力をまとめて速くするので、デモのリズムは変わりません。castには録画したときの時間が残るので、出力ごとに速さを変えられます。たとえばX用だけを短くできます。

```yaml
outputs:
  - format: svg              # README用。録画どおりの速さ
  - format: mp4              # X用。1.5倍速
    speed: 1.5
```

`end-hold`と`fade-out`はどの速さでも出力上の秒数のままです。`--at`は録画上の時点を指します。滑らかさを保つため、mp4とGIFの`--fps`の既定値も速さに合わせて上がります。

| 録画どおり | `speed: 2`（[speed.yaml](samples/speed.yaml)） |
| --- | --- |
| ![](samples/speed.svg) | ![](samples/speed-2x.svg) |

| macOS | Windows |
| --- | --- |
| ![](samples/theme-macos.svg) | ![](samples/theme-windows.svg) |

既定では出力より下の空き行を描きません。動画の解像度そのままにするには`--fit none`を指定します。`--fit none`のとき、デフォルト`80×24`は、mp4で`820×488`、SVGで`829.6×504`です。1920×1080動画を作るときは、`--font-size`を上げると読みやすく解像度もちょうどよくなります。

カラーテーマは`render.theme.preset`か`--theme`で指定します。

| 暗い背景 | |
| --- | --- |
| ![](samples/theme-presets/dark.svg) | ![](samples/theme-presets/macos-dark.svg) |
| ![](samples/theme-presets/dracula.svg) | ![](samples/theme-presets/nord.svg) |
| ![](samples/theme-presets/one-dark.svg) | ![](samples/theme-presets/gruvbox-dark.svg) |
| ![](samples/theme-presets/catppuccin-mocha.svg) | ![](samples/theme-presets/tokyo-night.svg) |
| ![](samples/theme-presets/solarized-dark.svg) | ![](samples/theme-presets/github-dark.svg) |
| 明るい背景 | |
| ![](samples/theme-presets/light.svg) | ![](samples/theme-presets/macos-light.svg) |
| ![](samples/theme-presets/solarized-light.svg) | ![](samples/theme-presets/github-light.svg) |

### 全体の例

各キーにコメントを付けた、実行できるシナリオ[samples/scenario_format.yaml](samples/scenario_format.yaml)です。

![](samples/scenario_format.svg)

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/guitarrapc/scenetake/v1.5.0/schema/scenario.schema.json
# Scenario format reference — runnable demo (samples/scenario_format.yaml).
# Required: steps only. Everything else is optional.

title: "Scenario Format Demo"  # Optional cast title
width: 80                        # Default: 80
height: 24                       # Default: 24
# cwd: /your/project             # Optional working directory for all commands
shell: bash                      # bash | pwsh | powershell | path. Windows: Git Bash when bash

render:                          # Optional display metadata for cast header and SVG
  font-size: 16
  font-family: ui-monospace, monospace
  theme:
    preset: dark                 # dark, light, nord, dracula, and more, with optional fg / bg / palette overrides
  window: macos                  # none | macos | windows
  window-title: "scenario_format.yaml"  # title bar text; default: the top-level title, "" for none
  background: "#0f2027 #2c5364"  # one color, 2-4 colors for a gradient, or a .png/.jpg/.gif/.webp file
  margin: 24                     # px around the window (default 8, 12 with a window, 32 with a background)
  padding: 12                    # px between the terminal edge and its text (default from font-size)

# outputs:                       # Files to write besides the cast, when no --format is given
#   - format: svg
#   - format: svg                # A light copy, for a README that follows the reader's light or dark mode
#     theme: light
#     file: scenario_format-light.svg
#   - format: gif                # Needs ffmpeg
#     fps: 10

settings:                        # Defaults for all steps; map-form steps can override per key
  prompt:                        # Text ("$ "), or parts with a color each. {dir} is the folder the step starts in
    - text: "{dir}"
      color: cyan
    - " $ "
  typing-speed: 0.04             # Average seconds per typed character
  typing-jitter: 0.01            # Random variance (+/- seconds) per character
  pre-delay: 0.5                 # Pause before typing each step
  post-delay: 1.0                # Pause after output before the next step
  execution-duration: 0.1        # Cast wait after command execution
  stderr-color: red              # When stderr has no ANSI SGR sequences
  # stream-output: false         # When true, replay captured chunk times in the cast
  # max-duration: 15             # Cap step output timeline, and also enables stream emit
  # silence-threshold: 2.0       # Stream mode: collapse idle gaps longer than this (seconds)
  # max-idle: 0.2                # Stream mode: collapsed idle gap duration (seconds)
  # mask:                        # Texts to hide wherever the recording shows them, as *
  #   - "internal.example.com"
  # mask-auto: true              # Also hide tokens, secret KEY=value values, URL passwords, secret env values

# Optional setup; runs before steps, not recorded in cast
pre:
  - echo "environment check (not recorded)"

steps:
  # A card shows centered lines on a cleared screen and runs nothing: a title, or an install line at the end
  - card:
      - "[bold fg:bright-cyan]Scenario Format Demo"
      - "every key, recorded"
    post-delay: 2

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

  - name: "keys and timeout"
    run: read -r -p "Your name? " name; echo "Hello, $name"
    pty: true                    # keys need a PTY to type into
    timeout: 10                  # seconds; then Ctrl+C, as a person would press it
    keys:                        # sleep (seconds), then type (text) or key (enter, up, esc, ctrl+c, f1...)
      - sleep: 0.5
        type: "scenetake"
      - key: enter

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

# Optional teardown; runs last, not recorded
post:
  - echo "demo complete (not recorded)"
```

## 秘密情報のマスク

トークン、`API_KEY=…`の値、URL中のパスワード、秘密鍵、`GITHUB_TOKEN`などの環境変数の値は、自動で`*`に置き換えます。ほかに隠したい文字列は`mask`で指定します。

```yaml
settings:
  mask:
    - "internal.example.com"
```

録画した人の情報をマスクします。対象は`/home/<名前>`や`ls -l`に出るユーザー名、コンピューター名、`git log`に出るgitの名前とメールです。表示したいときは`mask-personal: false`にします。webアドレス中の名前（例: `github.com/<名前>/tool`）はマスクしません。

```yaml
settings:
  mask-personal: false
```

## 撮り直し

ツールが変わったらシナリオを撮り直します。次の機能で、撮り直しを手早くし、差分を変わったところだけにできます。

### 実行し直さず見た目だけ変える

`--replay`と`--save-outputs`を組み合わせて、一度録画した出力ファイルを再利用してテーマや出力形式だけ変えられます。

```bash
scenetake --save-outputs demo.outputs.json demo.yaml
scenetake --replay demo.outputs.json --format mp4 --theme dracula demo.yaml
```

### 撮り直しても差分を出さない

撮り直したとき、出力が前回と同じステップは前回の録画をそのまま使います。`date`のように毎回変わる出力でも前回の出力を再利用するには`keep-output: true`を指定します。

```text
Compared with the earlier cast: 4 of 5 steps printed the same, 1 changed
  changed: step 3: npm test
```

### CI

製品が変わったら、GitHub Actionsでデモを撮り直します。出力が変わらないデモは同じファイルになるので、差分に出るのは変わったデモだけです。

```yaml
# .github/workflows/demos.yaml
name: Update demos
on:
  workflow_dispatch:
  release:
    types: [published]
permissions:
  contents: write
  pull-requests: write
jobs:
  record:
    runs-on: ubuntu-24.04
    steps:
      - uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0
        with:
          # releaseはタグをチェックアウトし、そこからはPRを作れない
          ref: ${{ github.event_name == 'release' && github.event.repository.default_branch || github.ref }}
      # デモで見せる製品をここでインストールする
      - run: sudo apt-get update && sudo apt-get install -y ffmpeg # gifとmp4を出力するときだけ
      - run: npx -y scenetake --strict docs/demos/*.yaml
      # リポジトリ設定の「Allow GitHub Actions to create and approve pull requests」が必要
      - uses: peter-evans/create-pull-request@3ffccacd46053766916f087ce41d627c0b8cf2d1 # v8.1.3
        with:
          title: Update demos
          branch: update-demos
```

デモが古いままのPRを落とすには、撮り直してからcastを比べます。`git add -N`で、まだコミットしていないcastも差分に出します。

```bash
npx -y scenetake --strict docs/demos/*.yaml
git add -N docs/demos
git diff --exit-code -- 'docs/demos/*.cast'
```

- 手元でもCIと同じシェル（Linuxのbashなど）で撮ります。Windowsのpwshで撮ったデモは、CIで最初に撮り直したときに全部変わります。
- 比べるのはcastです。GIFとmp4はffmpegのバージョンでバイトが変わります。
- `interactive: true`のステップはCIでは撮れません。`keys`のステップは撮れます。
- `--strict`を付けると、ステップのコマンドが0以外で終了したときにエラーにできます。失敗するのが正しいステップには`continue-on-error: true`を付けます。

## 開発

.NET 10 SDKが必要です。

```sh
# ビルド
dotnet build

# テスト。バイナリのテストはSCENETAKE_BINに発行したバイナリを指定したときだけ実行します
dotnet test

# このマシン向けのNative AOTバイナリを発行する
dotnet publish src/Scenetake/Scenetake.csproj -c Release -p:PublishAot=true
```

仕様は[.github/docs](.github/docs/spec_index.md)にあります。

## ライセンス

MIT。[LICENSE.md](LICENSE.md)を参照してください。
