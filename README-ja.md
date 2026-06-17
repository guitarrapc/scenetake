[![Build](https://github.com/guitarrapc/scenario2cast/actions/workflows/build.yaml/badge.svg)](https://github.com/guitarrapc/scenario2cast/actions/workflows/build.yaml)

# scenario2cast

[English](README.md) | 日本語

YAMLシナリオファイルから [asciinema v2 cast](https://docs.asciinema.org/manual/asciicast/v2/) ファイルを生成するツールです。`asciinema`をインストール・起動する必要はありません。step を並べた YAML を書くだけで、実際にコマンドを実行してその出力を使った cast ファイルを生成します。

サンプルシナリオ`samples/basic.yaml`で生成したcastをgifに変換すると...!タイプを頑張る必要もないし、実際にコマンドも走るので、リアルなデモが簡単に作れます。

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
  - echo "Current directory:"
  - pwd
  - echo "Wait for 2 seconds..."
  - run: sleep 2
    execution-duration: 1.0
  - echo "Done!"
```

![](samples/basic.gif)

**動機**

作りたいのはasciinemaのcastファイルであって、コマンドのレコードを頑張りたくない、scenario2castはこんな動機で作ったツールです。asciinemaの周辺に様々なツールがありますが、シェルスクリプトに寄っていたり、asciinema 本体を前提にしていたり、実行パスがcastに出力されたり、実行せずにそれっぽい出力だけを作るものだったりして、どれも欲しい形とはずれています。欲しいのは、シナリオを素直に書けて、列挙したコマンドを実際に実行し、その結果から cast ファイルを直接生成できるものです。

最終的にcastファイルがあればいいのでasciinema介さず、シナリオから直接castを生成するクロスプラットフォームツールです。

1. 実行するコマンドをシナリオに書く
2. コマンドを一定の間隔で入力しているように cast イベントを生成する
3. コマンドを実際に実行し、その出力を cast に書き出す

## クイックスタート

GitHub の Releases ページから利用 OS 向けアセットをダウンロード、`scenario2cast`（Windows は `scenario2cast.exe`）を任意の場所に配置します。

```bash
# macOS/Linux は必要に応じて実行権限を付与
chmod +x ./scenario2cast

# 最小シナリオを作成
scenario2cast init scenario.yaml

# 省略すると、現在のディレクトリに`scenario.yaml`として初期ファイルを生成します。
# scenario2cast init

# 実行
scenario2cast scenario.yaml [output.cast]

# `output.cast`を省略すると、シナリオファイルと同じディレクトリに`.cast`拡張子で出力します。
scenario2cast samples/basic.yaml

# 出力先を指定
scenario2cast samples/basic.yaml basic.cast

# asciinemaで再生
asciinema play basic.cast

# gifに変換 (Linux/macOS) - GIF変換時のデフォルトフォントサイズは16です。小さすぎる・大きすぎると感じたらフォントサイズを調整してください。
docker run --rm -v "${PWD}:/data" kayvan/agg /data/samples/basic.cast /data/samples/basic.gif --font-size 16

# gifに変換 (Windows PowerShell)
docker run --rm -v "$($PWD.Path):/data" kayvan/agg /data/samples/basic.cast /data/samples/basic.gif --font-size 16
```

**注意事項**

- `shell` で実行シェルを指定できます
- `settings` で prompt と timing の既定値を設定できます
- `init` でコメント付きの初期シナリオを生成できます
- `vim` や `htop` のような対話的コマンドは避けてください
- ファイル変更や外部システムに影響するコマンドは慎重に使ってください
- 長いコマンドは `execution-duration` で見やすく調整できます
- Linux/macOS の既定シェルは `$SHELL`、なければ `bash` です
- Windows の既定シェルは `pwsh`、なければ `powershell` です
- Windows で `shell: bash` を指定した場合は Git Bash / MSYS の `bash` を使います
- 実際に`step`が実行されるため、副作用のあるコマンドは注意して使用してください

## シナリオファイル形式

```yaml
title: "Demo Title"     # cast のタイトル（任意）
width: 120              # ターミナル幅（デフォルト: 120）
height: 24              # ターミナル高さ（デフォルト: 24）
cwd: /path/to/dir       # step を実行するディレクトリ（任意）
shell: bash             # 実行シェルを指定 (任意)

settings:
  prompt: "$ "
  typing-speed: 0.05       # 1文字あたりの平均秒数
  typing-jitter: 0.015     # ジッター幅 ±秒
  pre-delay: 0.8           # 次の step のタイピング開始前の停止時間
  post-delay: 1.5          # プロンプト表示後・次の step 入力までの停止時間
  execution-duration: 0.1  # 任意。各コマンド実行の cast 上待機時間

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
```

### ハイライト使用例

```yaml
settings:
  stderr-color: red

steps:
  - run: git status
    highlight:
      - color: yellow
        at: "4"
      - color: red
        at: "6-10:3-"

  - run: git log --oneline -3
    run-highlight: bright-cyan

  - run: echo "plain stderr" 1>&2
    # stderr に ANSI SGR がない場合は settings.stderr-color を使用
```

- `highlight` は step のみ対応です（map-form の `run` step）。
- `run-highlight` は step のみ対応です（map-form の `run` step）。
- `stderr-color` は両対応です（`settings.stderr-color` の既定値 + step の `stderr-color` 上書き）。
- stderr 側に既存 ANSI がある場合はそれを保持し、`stderr-color` は ANSI がない stderr にのみ適用されます。

### カラー名とANSIコード

`highlight`、`run-highlight`、`stderr-color` は同じ16色の前景色パレットを使います。

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

### settings対応 / step対応メモ

- `highlight` はこのバージョンでは `settings` 既定値を持ちません。
- `run-highlight` はこのバージョンでは `settings` 既定値を持ちません。
- `stderr-color` は `settings` と step の両方で設定できます。

## Development

`dotnet` を使ってローカル実行・ビルドする場合は以下を利用します。

- .NET 10 SDK（C# file-based app 実行のため）

```bash
# ローカル実行
dotnet run scenario2cast.cs -- <scenario.yaml> [output.cast]

# ビルド
dotnet publish scenario2cast.cs --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:DebugType=None
```
