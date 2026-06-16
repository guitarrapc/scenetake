# scenario2cast

YAMLシナリオファイルから [asciinema v2 cast](https://docs.asciinema.org/manual/asciicast/v2/) ファイルを生成するツールです。

asciinema をインストール・起動する必要はありません。step を並べた YAML を書くだけで、実際にコマンドを実行してその出力を使った cast ファイルを生成します。

## 仕組み

1. YAMLシナリオに step を列挙する
2. ツールがコマンドを一文字ずつ入力しているようなイベントを cast ファイルに書き出す（タイピング速度はランダムジッターで自然に見える）
3. コマンドを実際に実行して出力を cast ファイルに書き出す

## 必要要件

- .NET 10 SDK（C# file-based app 実行のため）

## 使い方

```bash
dotnet run scenario2cast.cs <scenario.yaml> [output.cast]
```

`output.cast` を省略すると、シナリオファイルと同じディレクトリに `.cast` 拡張子で出力します。

```bash
# 基本
dotnet run scenario2cast.cs examples/basic.yaml

# 出力先を指定
dotnet run scenario2cast.cs examples/basic.yaml demo.cast

# asciinema で再生
asciinema play demo.cast

# agg で gif に変換
docker run --rm -v "$($PWD.Path):/data" kayvan/agg /data/examples/basic.cast /data/examples/basic.gif
```

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
  pre-command-delay: 0.8   # 次の step のタイピング開始前の停止時間
  post-command-delay: 1.5  # プロンプト表示後・次の step 入力までの停止時間
  command-execution-duration: 0.7 # 任意。各コマンド実行の cast 上待機時間の既定値 (既定: 0.1)

steps:
  # 文字列で書くだけ（シンプルな方法）
  - echo "Hello, World!"
  - ls -la

  # dict で書くと個別設定が可能
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
| `pre-delay` | タイピング前の停止時間 | `settings.pre-command-delay` |
| `post-delay` | プロンプト表示後の停止時間 | `settings.post-command-delay` |
| `execution-duration` | このコマンド実行の cast 上待機時間を上書き | `settings.command-execution-duration` |

## 注意事項

- トップレベルの `shell` で実行シェルを指定できます
- 後方互換として `settings.shell` も受け付けます
- cast 出力の改行は OS に関係なく `\r\n` に正規化されます（端末レンダラー互換のため）
- 同じシナリオファイルに対するタイピングシミュレーションは決定的です
- cast ヘッダーの `timestamp` も同じシナリオファイルで決定的です
- 未指定時のコマンド実行待機は cast 上で固定 `0.1s` です
- `execution-duration` は cast 上の待機時間だけを変更し、実コマンド自体は完了まで実行されます
- Linux/macOS のデフォルトは `$SHELL`、未設定時は `bash` です
- Windows のデフォルトは `pwsh`、未導入時は `powershell` です
- Windows で `settings.shell: bash` を指定した場合は Git Bash / MSYS の `bash` を使用し、WSL の `bash` は意図的に使用しません
- インタラクティブなコマンド（`vim`、`htop` など）は使用しないでください
- Windows の PowerShell では通常 `echo "text"` は追加のクォートなしで表示されます
- 実際に step が実行されるため、副作用のあるコマンドは注意して使用してください

## サンプル

```bash
dotnet run scenario2cast.cs examples/basic.yaml
dotnet run scenario2cast.cs examples/git-demo.yaml
```
