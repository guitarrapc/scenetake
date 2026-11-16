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

SVG と mp4 の出力では、フォント、ライト/ダークテーマのほか、ターミナルを囲むウィンドウを次の設定で変えられます。どれもシナリオの `render` キーとコマンドラインのフラグの両方で指定できます。

| `render` キー | フラグ | 設定する内容 |
| --- | --- | --- |
| `window` | `--window` | macOS か Windows のタイトルバー（`macos` / `windows`） |
| `window-title` | `--window-title` | タイトルバーの文字。既定はシナリオの `title`。`""` で非表示 |
| `background` | `--background` | ウィンドウの周りを埋めるもの。1 色（`"#1e3c72"`）、左上から右下へのグラデーションになる 2〜4 色（`"#1e3c72 #2a5298"`）、または `.png`、`.jpg`、`.gif`、`.webp` の画像ファイル |
| `margin` | `--margin` | ウィンドウの周りの余白（ピクセル、0〜200）。背景があるときの既定は 32 |
| `padding` | `--padding` | ターミナルの端と文字の間の余白（ピクセル、0〜100） |
| `opacity` | `--opacity` | ターミナルの背景とタイトルバーが背景をどれだけ覆うか（0〜1）。既定は 1。文字は透けません |
| `blur` | `--blur` | `opacity` が 1 未満のウィンドウ越しに見える背景のぼかし（ピクセル、0〜50） |

`background` を指定しなければ、見えるのはウィンドウだけです。SVG の余白は透明で、mp4 はウィンドウだけの映像になります。背景は、作ったページの外へ持ち出すときに役立ちます。

- **X や Reddit に投稿する動画。** 背景付きの mp4 は、ターミナルを切り抜いた映像ではなく、デスクトップのスクリーンショットのように見えます（agg で作る GIF にはウィンドウ枠も背景も付きません）。
- **サイズを決めた動画。** `--size 1920x1080` では、余った部分をターミナルの背景色ではなく背景で埋め、ウィンドウを中央に置きます。
- **置き先の色がわからないページ。** GitHub の README は、読む人の設定でライトにもダークにもなります。背景を付ければ、どちらでも同じ見た目になります。

README の本文に並べるデモは、たいてい背景なしで十分です。ページの色になじみ、ファイルも小さく済みます。画像の背景は SVG ごとに埋め込まれるので、横幅 1000〜1600 ピクセル程度の WebP か JPEG を使ってください。

画像の背景では、`opacity` で背景が透けるターミナルのようにウィンドウ越しに画像を見せ、`blur` で透けた部分をぼかせます。半透明になるのはターミナルの背景とタイトルバーだけで、文字とプログラムが色を付けたセルは透けません。明るい画像なら 0.85 以上、暗い画像なら 0.7 程度が目安です。

デモの終わり方も同じように指定できます。

| `render` キー | フラグ | 設定する内容 |
| --- | --- | --- |
| `loop` | `--no-loop` / `--loop` | `false` で SVG を 1 回だけ再生し、最後の画面で止めます。既定は `true`。mp4 にループはなく、再生するプレイヤーが決めます |
| `end-hold` | `--end-hold` | 最後の画面を表示し続ける秒数（0〜60）。その後 SVG は最初に戻り、mp4 は終わります。既定は最後の出力の前の間と同じ長さ |
| `fade-out` | `--fade-out` | `end-hold` の後に文字を消していく秒数（0〜10）。ループが空のターミナルから始まり直します。ウィンドウと背景は残ります |

| --- | --- |
| ![](samples/theme-macos.svg) | ![](samples/theme-windows.svg) |

## ギャラリー

以下の録画は、どれも [`samples/showcase`](samples/showcase) のシナリオを Linux で実行したものです。[`record.sh`](samples/showcase/record.sh) が、ツールを入れたコンテナ（[`Dockerfile`](samples/showcase/Dockerfile)）の中で録画します。

| [fastfetch](samples/showcase/fetch.yaml) | [bat、git、jq](samples/showcase/devtools.yaml) |
| --- | --- |
| ![](samples/showcase/fetch.svg) | ![](samples/showcase/devtools.svg) |
| [**figlet、lolcat、toilet**](samples/showcase/logo.yaml) | [**sl**](samples/showcase/sl.yaml) |
| ![](samples/showcase/logo.svg) | ![](samples/showcase/sl.svg) |
| [**`keys` で操作する vim と git diff（背景は画像）**](samples/showcase/vim.yaml) | |
| ![](samples/showcase/vim.svg) | |

[nyancat](samples/showcase/nyancat.yaml)（96 × 26 セルのトゥルーカラーアニメーション、`timeout` で停止）:

![](samples/showcase/nyancat.svg)

## モチベーション

「コマンドをキャプチャしたいけど、コマンドのタイピングを頑張りたくない」、scenetakeはこんな動機で作ったツールです。asciinemaの周辺に様々なツールがありますが、シェルスクリプトに寄っていたり、asciinema 本体を前提にしていたり、実行パスがcastに出力されたり、実行せずにそれっぽい出力だけを作るものだったりして、どれも欲しい形とはずれています。欲しいのは、シナリオを素直に書けて、コマンドを実際に実行し、その結果から cast ファイルを直接生成できるものです。

scenetake は、asciinema を介さずシナリオから `.cast`（任意で `.svg` も）を直接生成するクロスプラットフォームツールです。

1. 実行するコマンドをシナリオに書く
2. 通常の step は一定の間隔で入力しているように cast イベントを生成する。`pty: true` の step は端末ストリームをリアルタイムで記録する
3. コマンドを実際に実行し、その出力を cast に書き出す

## クイックスタート

GitHub の Releases ページから利用 OS 向けアセットをダウンロードし、`scenetake`（Windows は `scenetake.exe`）を任意の場所に配置します。バイナリの実行に .NET ランタイムと ICU は不要です。

| アセット | 動作環境 |
|---|---|
| `scenetake-linux-amd64.tar.gz`, `scenetake-linux-arm64.tar.gz` | glibc 2.28 以降の Linux（RHEL 8、Debian 10、Ubuntu 20.04 以降） |
| `scenetake-linux-musl-amd64.tar.gz`, `scenetake-linux-musl-arm64.tar.gz` | musl 1.2.3 以降の Linux（Alpine 3.17 以降） |
| `scenetake-osx-amd64.tar.gz`, `scenetake-osx-arm64.tar.gz` | macOS 12.0 以降 |
| `scenetake-win-amd64.zip`, `scenetake-win-arm64.zip` | Windows 10 1809 以降 |

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

# OS 既定のシェル (Windows は pwsh、それ以外は $SHELL) ではなく bash 向けにスターターを作成
scenetake init --shell bash

# シナリオを実行して cast ファイルを生成
scenetake scenario.yaml

# cast とアニメーション SVG を一度に生成
scenetake --format svg scenario.yaml

# SVG のスタイル: フォント、テーマ、ウィンドウ枠、フレーム上限
scenetake --format svg scenario.yaml --font-size 20 --font-family "'Noto Sans Mono', ui-monospace" --theme light --window macos

# タイトル付きのウィンドウをグラデーションの上に置き、文字の周りの余白を広げる
scenetake --format svg scenario.yaml --window macos --window-title "my-cli demo" --background "#1e3c72 #2a5298" --padding 16

# 画像の上に半透明のウィンドウを置き、文字の後ろの画像をぼかす
scenetake --format svg scenario.yaml --window macos --background wallpaper.webp --opacity 0.85 --blur 8

# 最後の画面を 3 秒表示し、1 秒かけて文字を消してからループの最初に戻る
scenetake --format svg scenario.yaml --end-hold 3 --fade-out 1

# 既存の cast ファイルを SVG に変換（v2 / v3 対応）
scenetake svg scenario.cast

# svg サブコマンドでも同じスタイルフラグが使える
scenetake svg scenario.cast --theme light --window windows --max-fps 30

# cast ファイルを mp4（H.264、ffmpeg が必要）に変換。全画面 TUI や共有に向く
scenetake mp4 scenario.cast

# X (Twitter) の動画アップロード制限に収める
scenetake mp4 scenario.cast --max-size 1280x1024

# mp4 をちょうど 1920x1080 にする。動画編集やクリップの連結に向く
scenetake mp4 scenario.cast --size 1920x1080

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

`scenetake --help` は全コマンドとオプションの値の範囲・既定値を表示します。各コマンドのオプションは `scenetake svg --help`、`scenetake mp4 --help`、`scenetake init --help` で確認できます。

```bash
# 新しいシナリオファイルを初期化
scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]

# シナリオを実行して cast（既定）、cast + SVG、または cast + mp4 を生成
scenetake [--verbose] [--strict] [--format cast|svg|mp4]
  [--font-size N] [--font-family FAMILIES] [--theme dark|light]
  [--window none|macos|windows] [--max-fps N]
  [--fps N] [--crf N] [--max-size WxH | --size WxH] [--font-file PATH]
  scenario.yaml [output]

# 既存の cast ファイルを SVG に変換
scenetake svg [--font-size N] [--font-family FAMILIES] [--theme dark|light]
  [--window none|macos|windows] [--max-fps N]
  <input.cast> [output.svg]

# 既存の cast ファイルを mp4 に変換
scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme dark|light]
  [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH | --size WxH]
  <input.cast> [output.mp4]
```

`--max-fps` は SVG アニメーションのフレームレート上限です（`0` = オフ、cast のタイミングをそのまま使用）。`--format svg` 時と `svg` サブコマンドで有効です。

`--strict` は CI 向けです。`continue-on-error: true` を指定していない録画ステップが 0 以外で終了すると、録画を停止します。後続のステップは実行せず、出力も書きません。前回の出力ファイルはそのまま残し、`post` を実行します。終了コードは通常 `1` です。`post` のコマンドが失敗した場合は、そのコマンドの終了コードを返します。

エラーには、そのステップが最後に出力した数行を表示します。エラーのデモのように失敗するのが正しいステップには `continue-on-error: true` を付けます。

mp4 用のオプションは `--format mp4` 時と `mp4` サブコマンドで有効です。

- `--fps`: フレームレート（既定 `30`、最大 `60`）
- `--crf`: 画質（既定 `23`。大きいほど小さいファイル）
- `--max-size`: この範囲に収まるよう縮小（X 向けなら `1280x1024`）
- `--size`: 動画をちょうどこのサイズにする（たとえば `1920x1080`。後述の「ターミナルサイズと解像度」を参照）。`--max-size` とは同時に使えません
- `--font-file`: 使うフォントファイル

**SVG と mp4 の使い分け**

タイピング中心や短い出力なら、たいてい SVG が最も小さく（mp4 の半分程度になることも多い）、README に埋め込めて、拡大しても文字がぼけません。毎フレーム画面全体を描き直す全画面 TUI（`matrix`、`btop` など）は、SVG や GIF だと非常に大きくなります。こうした録画や、X・Slack での共有には mp4 を使ってください。たとえば `matrix --fps 60` を 200×60 で 7.5 秒録ると、SVG は 17.0MB、mp4 は 4.4MB（`--max-size 1280x1024` なら 2.8MB）です。SVG が 10MB を超えると、scenetake は警告を表示します。

mp4 出力は、libx264 入りの [ffmpeg](https://ffmpeg.org/) を別プログラムとして呼び出します。インストール方法は次のとおりです。

- Windows: `winget install Gyan.FFmpeg` または `scoop install ffmpeg`
- macOS: `brew install ffmpeg`
- Debian/Ubuntu: `sudo apt install ffmpeg`

特定の ffmpeg を使うときは `SCENETAKE_FFMPEG` にそのパスを指定します。

mp4 は手元にインストールされたフォントで描画します。`font-family` の一覧を順に探し、見つからない文字は日本語などの CJK フォントや記号フォントで補います。フォントを固定したいときは `--font-file` を指定してください。`--window` を付けると、SVG と同じタイトルバーを描画します（外側の余白と影はありません）。

**ターミナルサイズと解像度**

`width` と `height` はターミナルの列数と行数です（cast ファイルでは `term.cols` と `term.rows`）。出力のピクセルサイズはこの値で決まります。既定の `font-size` 16 では次のとおりです。

| 出力 | 幅（px） | 高さ（px） |
| --- | --- | --- |
| mp4 | `width × 10 + 20` | `height × 20 + 8` |
| mp4（`--window` あり） | `width × 10 + 20` | `height × 20 + 42` |
| SVG | `width × 9.92 + 36` | `height × 20 + 24` |
| SVG（`--window` あり） | `width × 9.92 + 44` | `height × 20 + 66` |

たとえば既定の 120×24 は、mp4 で 1220×488、SVG で 1226.4×504 です。

動画の解像度いっぱいに使うときは、収まる最大の `width` × `height` を指定します。`font-size` 16 での値は次のとおりです。

| 解像度 | `width` × `height` → mp4 のサイズ | `--window` あり |
| --- | --- | --- |
| 1280×720（HD） | 126 × 35 → 1280×708 | 126 × 33 → 1280×702 |
| 1280×1024（X のアップロード上限） | 126 × 50 → 1280×1008 | 126 × 49 → 1280×1022 |
| 1920×1080（フル HD） | 190 × 53 → 1920×1068 | 190 × 51 → 1920×1062 |
| 2560×1440（WQHD） | 254 × 71 → 2560×1428 | 254 × 69 → 2560×1422 |
| 3840×2160（4K） | 382 × 107 → 3840×2148 | 382 × 105 → 3840×2142 |

- 1 行は 20 ピクセルなので、高さはたいてい解像度より少し小さくなります。残りは `--size` で埋められます（後述）。
- はじめから収まるサイズで録ると、大きく録って `--max-size` や `--size` で縮小するより文字がくっきりします。
- `font-size` を大きくするとセルも大きくなり、収まる列数と行数は減ります。セルは幅 `font-size × 0.62`、高さ `font-size × 1.25` で、mp4 では整数ピクセルに丸めます。余白も `font-size` に応じて変わります。詳しくは [spec_mp4.md](.github/docs/spec_mp4.md#layout) の Layout を参照してください。
- SVG は拡大縮小してもぼけないため、ピクセルサイズは最初に表示される大きさを決めるだけです。
- 録画中にサイズが変わる場合は、その中で最大のサイズを使います。

`--size WxH` を付けると、mp4 はちょうど `W×H` になります。幅と高さはどちらも偶数にしてください。録画したターミナルは変わらず、変わるのは周りの動画の枠だけです。

- 収まるターミナルは中央に置き、残りをテーマの背景色で埋めます。190 × 53 を `--size 1920x1080` にすると、上下に 6 ピクセルずつ足します。`--window` のタイトルバーは上端に残ります。
- 大きいターミナルは、その縦横比まで余白を足してから縮小します。200 × 60（2020×1208）は、文字が 89% に縮小され、左右に約 57 ピクセルずつ余白のある 1920×1080 になります。
- 小さいターミナルは拡大しません。80 × 24 は枠の中央に置かれます。文字を大きくしたいときは `--font-size` を上げてください。

**Notes**

- `shell`:
  - Linux/macOS の既定シェルは `$SHELL`、なければ `bash`
  - Windows の既定シェルは `pwsh`、なければ `powershell`、Windows で `shell: bash` を指定した場合は Git Bash / MSYS の `bash` を使います
  - Windows で `C:/tools/pwsh-preview/pwsh.exe` のようにパスを書くと、そのファイルを起動します。WSL の `bash` はパスで指定しても使いません
  - 名前で指定したシェルは、カレントディレクトリにある同名ファイルを起動しません。Windows ではそのファイルを無視します。Linux/macOS ではエラーで停止するので、別のディレクトリから実行するか、シェルをフルパスで指定してください
- `cwd` は `pre`、`steps`、`post` の作業フォルダです。相対パスは scenario ファイルのフォルダからの相対で、scenetake をどこから実行しても同じフォルダで動きます。`cwd` を書かなければ、scenetake を実行したフォルダで動きます。フォルダは実行前に存在している必要があります
- `settings` でpromptとtiming の既定値を設定できます
- `render` は cast ヘッダーの表示メタデータ（`st:font-size` タグ、`term.theme`）と SVG 出力を制御します。詳細は [.github/docs/spec_cast.md](.github/docs/spec_cast.md) と [.github/docs/spec_svg.md](.github/docs/spec_svg.md)
- `pre` / `post` は録画フロー外で setup / teardown コマンドを実行します。stdout/stderr は CLI に表示されますが、cast ファイルには一切書き込まれません。
- `steps`:
  - 実際に実行されるため、ファイル変更や外部システムを操作するような副作用のあるコマンドは慎重に使ってください
  - ターミナルを必要とするコマンドは `pty: true` で pseudo-terminal capture を有効化できます。詳細は [PTY 録画](#pty-録画)。
  - 実行時間が長いコマンドは `execution-duration` で見やすく調整できます
  - 長い出力を自然にリプレイしたい場合（`docker build` や `sleep` を挟むステップなど）は、その step に `stream-output: true` と必要なら `max-duration` を指定します。デフォルトの pipe emit はタイピング演出のため一括表示のままです
  - 0 以外で終了したステップは警告を出し、録画を続けます。想定した失敗には `continue-on-error: true` で警告を抑制できます。CI では前述の `--strict` を使います。

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

ANSI 色指定のない stderr は既定で赤く表示します。git のブランチ変更、curl の進捗、`docker build` など、通常のメッセージを stderr に出すツールもあります。通常の文字色にするには、`settings` またはステップに `stderr-color: none` を指定してください。

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

録画は DECDLD ソフトフォントも描画しますが、PTY の内側のプログラムからはそれを確かめられません。シナリオで指定してください。たとえば `matrix --mode movie --soft-font` です。

キーを押すまで終わらない TUI や、メニューを選ぶデモは `keys` で操作を書けるので、録画中にキーボードの前にいる必要はありません。`timeout` は終わらないコマンドを止めます。PTY ではまず Ctrl+C を押すので、プログラムは画面を元に戻せます。

```yaml
steps:
  - run: vim greet.py
    pty: true
    timeout: 30             # 保険。30 秒たったら Ctrl+C
    keys:                   # 上から順に PTY へ書き込む
      - sleep: 1.5          # 先に待つ秒数
        type: "/return\n"   # 文字列。タイピングの間隔で 1 文字ずつ送る。改行は Enter
      - type: "O# Say hello"
      - key: escape         # enter、tab、up、page-down、f1、ctrl+c、alt+x など
      - type: ":wq\n"
  - run: nyancat
    pty: true
    timeout: 5              # nyancat は止めるまで動き続ける
```

| 種類 | 使えるキー名（大文字小文字は区別しない） |
| --- | --- |
| 名前付き | `enter`、`tab`、`space`、`backspace`、`escape`（`esc`）、`up`、`down`、`left`、`right`、`home`、`end`、`page-up`、`page-down`、`insert`、`delete`、`f1`〜`f12` |
| 組み合わせ | `ctrl+` と英字 1 文字（例：`ctrl+c`）、`alt+` と ASCII 文字 1 文字（例：`alt+x`） |

[samples/showcase/vim.yaml](samples/showcase/vim.yaml) と [spec_pty.md](.github/docs/spec_pty.md#keys-and-timeout) の Keys and timeout を参照してください。

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

必須キーは `steps` だけです。他のトップレベルキーは任意です。不明なキー（`hightlight` など）には警告を出し、候補があれば正しいキー名を示します。

以下は実行可能なサンプルです。正本は [samples/scenario_format.yaml](samples/scenario_format.yaml) です。

- フィールド定義: [spec_scenario.md](.github/docs/spec_scenario.md)
- 色・スタイル構文: [spec_highlight.md](.github/docs/spec_highlight.md)
- `pre` / `post` の挙動: [spec_pre_post.md](.github/docs/spec_pre_post.md)
- 色の追加例: [samples/highlight.yaml](samples/highlight.yaml)

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
  window-title: "scenario_format.yaml"  # title bar text; default: the top-level title, "" for none
  background: "#0f2027 #2c5364"  # one color, 2-4 colors for a gradient, or a .png/.jpg/.gif/.webp file
  margin: 24                     # px around the window (default 8, 12 with a window, 32 with a background)
  padding: 12                    # px between the terminal edge and its text (default from font-size)

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

staged publishing は registry 上にパッケージが既に存在していることが前提なので、各 npm パッケージの初回リリースだけは手動の `npm publish` が必要です。後から追加したプラットフォームパッケージ（`@scenetake/linux-x64-musl`、`@scenetake/linux-arm64-musl`）も同様です。workflow はこのようなパッケージを stage せずに pack し、tarball を `npm-first-publish` artifact としてアップロードします。実行するコマンドは run の summary に出ます。

```bash
gh run download <run-id> --repo guitarrapc/scenetake --name npm-first-publish --dir npm-first-publish
npm publish --access public npm-first-publish/scenetake-linux-x64-musl-<version>.tgz
```

そのあと npmjs.com で新しいパッケージの trusted publishing を設定してください。次のリリースから stage できるようになります。

メインの `scenetake` より先に、プラットフォームパッケージ（`@scenetake/linux-x64` など）を approve または publish してください。
