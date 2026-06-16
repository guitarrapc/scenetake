[![Build](https://github.com/guitarrapc/scenario2cast/actions/workflows/build.yml/badge.svg)](https://github.com/guitarrapc/scenario2cast/actions/workflows/build.yml)

# scenario2cast

[English](README.md) | 日本語

YAMLシナリオファイルから [asciinema v2 cast](https://docs.asciinema.org/manual/asciicast/v2/) ファイルを生成するツールです。`asciinema`をインストール・起動する必要はありません。step を並べた YAML を書くだけで、実際にコマンドを実行してその出力を使った cast ファイルを生成します。

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

# 省略すると、現在のディレクトリに `scenario.yaml` として初期ファイルを生成します。
# scenario2cast init

# 実行
scenario2cast scenario.yaml [output.cast]

# `output.cast` を省略すると、シナリオファイルと同じディレクトリに `.cast` 拡張子で出力します。
scenario2cast examples/basic.yaml

# 出力先を指定
scenario2cast examples/basic.yaml demo.cast

# コメント付きの初期シナリオを生成
scenario2cast init [scenario.yaml]

# asciinema で再生
asciinema play demo.cast

# agg で gif に変換
docker run --rm -v "$($PWD.Path):/data" kayvan/agg /data/examples/basic.cast /data/examples/basic.gif
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
  execution-duration: 0.7  # 任意。各コマンド実行の cast 上待機時間

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

### コマンド設定一覧

| キー | 説明 | デフォルト |
|------|------|-----------|
| `run` | 実行するコマンド | 必須 |
| `typing-speed` | 1文字あたりの平均秒数 | `settings.typing-speed` |
| `typing-jitter` | ジッター幅 | `settings.typing-jitter` |
| `pre-delay` | タイピング前の停止時間 | `settings.pre-delay` |
| `post-delay` | プロンプト表示後の停止時間 | `settings.post-delay` |
| `execution-duration` | このコマンド実行の cast 上待機時間を上書き | `settings.execution-duration` |

## Development

`dotnet` を使ってローカル実行・ビルドする場合は以下を利用します。

- .NET 10 SDK（C# file-based app 実行のため）

```bash
# ローカル実行
dotnet run scenario2cast.cs <scenario.yaml> [output.cast]

# ビルド
dotnet publish scenario2cast.cs --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:DebugType=None
```
