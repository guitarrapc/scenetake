# scenario2cast

YAMLシナリオファイルから [asciinema v2 cast](https://docs.asciinema.org/manual/asciicast/v2/) ファイルを生成するツールです。

asciinema をインストール・起動する必要はありません。コマンドを並べたYAMLを書くだけで、実際にコマンドを実行してその出力を使ったcastファイルを生成します。

## 仕組み

1. YAMLシナリオにコマンドを列挙する
2. ツールがコマンドを一文字ずつ入力しているようなイベントをcastファイルに書き出す（タイピング速度はランダムジッターで自然に見える）
3. コマンドを実際に実行して出力をcastファイルに書き出す

## インストール

```bash
pip install pyyaml
```

Python 3.8 以上が必要です。

## 使い方

```bash
python scenario2cast.py <scenario.yaml> [output.cast]
```

`output.cast` を省略すると、シナリオファイルと同じディレクトリに `.cast` 拡張子で出力します。

```bash
# 基本
python scenario2cast.py examples/basic.yaml

# 出力先を指定
python scenario2cast.py examples/basic.yaml demo.cast

# asciinema で再生
asciinema play demo.cast
```

## シナリオファイル形式

```yaml
title: "デモタイトル"   # cast のタイトル（任意）
width: 120              # ターミナル幅（デフォルト: 120）
height: 24              # ターミナル高さ（デフォルト: 24）
cwd: /path/to/dir       # コマンドを実行するディレクトリ（任意）

settings:
  prompt: "$ "          # プロンプト文字列（デフォルト: "$ "）
  typing_speed: 0.05    # 1文字あたりの平均秒数（デフォルト: 0.05）
  typing_jitter: 0.015  # ジッター幅 ±秒（デフォルト: 0.015）
  pre_command_delay: 0.8   # タイピング開始前の停止時間（デフォルト: 0.8）
  post_command_delay: 1.5  # 出力後・次プロンプトまでの停止時間（デフォルト: 1.5）

commands:
  # 文字列で書くだけ（シンプルな方法）
  - echo "Hello, World!"
  - ls -la

  # dict で書くと個別設定が可能
  - cmd: git log --oneline -10
    post_delay: 3.0         # この出力の後は長めに停止

  - cmd: git status
    typing_speed: 0.10      # ゆっくりタイプ
    pre_delay: 1.5
    post_delay: 2.0
```

### コマンド設定一覧

| キー | 説明 | デフォルト |
|------|------|-----------|
| `cmd` | 実行するコマンド | （必須） |
| `typing_speed` | 1文字あたりの平均秒数 | `settings.typing_speed` |
| `typing_jitter` | ジッター幅 | `settings.typing_jitter` |
| `pre_delay` | タイピング前の停止時間 | `settings.pre_command_delay` |
| `post_delay` | 出力後の停止時間 | `settings.post_command_delay` |

## 注意事項

- コマンドはシステムのデフォルトシェル（Linux/macOS: `$SHELL`、Windows: `COMSPEC`）で実行されます
- インタラクティブなコマンド（`vim`、`htop` など）は使用しないでください
- Windows では `echo "text"` が `"text"` とクォート付きで出力される場合があります（`echo text` を使うか Git Bash / WSL 経由で実行してください）
- 実際にコマンドが実行されるため、副作用のあるコマンドは注意して使用してください

## サンプル

```bash
# 基本サンプル
python scenario2cast.py examples/basic.yaml

# Git ワークフローサンプル（git リポジトリ内で実行）
python scenario2cast.py examples/git-demo.yaml
```
