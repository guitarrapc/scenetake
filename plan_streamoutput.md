# Stream Output Timing Plan

Status: **Phase 2 implemented**

## Motivation

Long-running commands (e.g. `docker build`) produce output that feels instant in SVG/GIF playback because pipe steps buffer all stdout/stderr and emit a single cast `o` event. The goal is natural replay timing by recording stream arrival times at cast generation time, then applying a shared timing pipeline for pipe and PTY steps.

## Decisions (grill-me summary)

| Topic | Decision |
|---|---|
| Goal | Cast timeline at recording time; playback (SVG/GIF/asciinema) should feel natural |
| Pipe capture | Parallel stdout/stderr stream read; merge chronologically by wall-clock time |
| `stderr-color` | Applied per stderr chunk before cast emit |
| `highlight` | Raw capture → timing pipeline → apply to full merged text → split back to timed segments |
| Long steps | Linear compression when `max-duration` is set: `scale = min(1, max / actual)` |
| Compression knob (v1) | `max-duration` only (no `playback-speed`) |
| Idle gaps | When stream mode: gap > `silence-threshold` collapses to `max-idle` |
| Burst output | In stream mode: merge gaps < 50ms to same timestamp; default mode emits one instant blob |
| Off-screen output | Record everything (v1) |
| PTY | Same idle + compression pipeline after existing startup offset |
| YAML placement | `settings` defaults + per-step override (`execution-duration` pattern) |
| Cast emit | Coalesce chunks that share the same adjusted timestamp |
| Compatibility | Lenient defaults (`silence-threshold: 2.0`, `max-idle: 0.2`) so short demos stay mostly unchanged |
| **Default pipe emit** | **Instant** — single `o` event at `execution-duration` (restores pre-stream behavior) |
| Stream emit opt-in | `stream-output: true` or `max-duration` set |

## Timing pipeline (stream mode only)

When `stream-output: true` or `max-duration` is set:

```
capture chunks (pipe stream or PTY bytes) + wall-clock time
  → startup offset (PtyCastTiming; pipe + PTY)
  → burst merge (gaps < 50ms → same timestamp)
  → idle collapse (silence-threshold / max-idle)
  → linear compression (max-duration when set)
  → stderr-color / highlight
  → coalesce same timestamp
  → cast o events
```

### Default (no stream mode)

Pipe steps still capture stdout/stderr in parallel, but instant emit merges **stdout then stderr** (not chronological interleaving). Emit **one** cast event at `commandStart + execution-duration` — same as pre-stream-output behavior.

### Defaults

| Key | Default | Description |
|---|---|---|
| `silence-threshold` | `2.0` | Gaps longer than this are treated as idle |
| `max-idle` | `0.2` | Collapsed idle gap duration |
| `max-duration` | unset | When set, linearly scale step output timeline to this ceiling |
| `stream-output` | `false` | When `true`, emit timed chunks instead of a single instant blob |

## Implementation plan

### Phase 1 — Core pipeline (this PR)

1. **`CastTimingPipeline.cs`**
   - `CollapseIdleGaps`
   - `LinearCompress`
   - `AdjustTimeline`
   - `CoalesceTimedText` / `CoalesceTimedUtf8`
   - `HighlightSplitter` (map highlighted full text back to timed segments)

2. **`PipeStreamCapture.cs`**
   - Async parallel `StandardOutput` / `StandardError` read
   - `PipeStreamChunk(TimeSpan Time, bool IsStderr, string Text)`
   - Chronological merge

3. **`scenetake.cs`**
   - `ScenarioSettings`: `SilenceThreshold`, `MaxIdle`, `MaxDuration`
   - `RunCommandCoreAsync`: pipe path uses stream capture
   - `GenerateAsync`: shared emit path for pipe + PTY through timing pipeline
   - Constants: `DefaultSilenceThreshold`, `DefaultMaxIdle`

4. **`tests/cast_timing_test.cs`**
   - Unit tests for pipeline, coalesce, startup + idle interaction

5. **CI** — register `tests/cast_timing_test.cs` in `build.yaml`

### Phase 2 — Docs and integration (done)

- [x] Update `.github/docs/spec_scenario.md` with new keys and lessons learned
- [x] Update `.github/docs/spec_pty.md` stream-mode cross-reference
- [x] Update `spec_index.md`, README / README-ja tips, `samples/scenario_format.yaml`, `CreateInitialScenarioYaml`
- [x] `tests/fixtures/stream-output-stagger.yaml` + `tests/stream_output_test.cs`
- [x] CI registers `stream_output_test.cs`

### Idle collapse detail

Gaps are measured from **original** chunk timestamps before adjustment. A long idle followed by a short active gap (e.g. 30s then 0.5s) collapses only the 30s gap; the 0.5s gap is preserved.

### Out of scope (v1)

- Viewport-based output thinning
- `playback-speed` multiplier
- External cast files (asciinema recordings) — timing pipeline is scenetake scenario recording only

## Test plan

- [x] `CollapseIdleGaps` short gap unchanged, long gap collapsed to `max-idle`
- [x] `LinearCompress` only when actual > `max-duration`
- [x] Coalesce merges same-timestamp chunks
- [x] Pipe stream: multi-chunk cast with distinct event times for staggered `echo` + `sleep` (`stream-output-stagger.yaml`)
- [x] PTY steps still pass existing `pty_timing_test` / `pty_test`
- [x] `samples/basic.yaml` smoke still passes
- [x] `samples/demo.yaml` matches pre-stream event shape (instant emit, agg progress in one event)
- [x] `IntegrationDockerBuildStream` — real `docker build` with `stream-output` + `max-duration`
- [x] `samples/scenario_format.svg` / `.cast` regenerated after `stream-output` step added

## Lessons learned

実装と `demo.yaml` / SVG レビューで分かったこと。grill-me 時点の設計から変更した点も含む。

### 1. 実測ストリームをそのままデフォルトにするとデモが壊れる

最初の実装は「pipe read ごとに chunk + wall-clock 時刻 → cast `o`」を **常時** 適用していた。cast 上は忠実だが、playback（SVG/GIF）は明らかに悪化した。

| 症状 | 原因 |
|---|---|
| タイピング演出が消えたように見える | 出力が細切れイベントで画面が連続更新され、直前のタイピングが視覚的に埋もれる |
| 出力がカクつく | stderr や短いコマンドでも read 境界（4096B）ごとにイベントが分かれる |
| agg / プログレスバーが壊れる | 1 行ずつ `\r\n` 付きで到着 → 上書きではなく行追加になり、200 行スクロールになる |

**対策**: デフォルトは旧挙動（`commandStart + execution-duration` で **1 イベント**）。ストリームタイミングは `stream-output: true` または `max-duration` 指定時のみ。

### 2. 「capture はストリーム、emit は別」が正しい分離

stdout/stderr の **並行読み取り** は stdout/stderr 順序の再現に有用なので残す。一方 cast への書き込みは用途に応じて切り替える。

- **Instant emit（デフォルト）**: 捕捉済み全文をマージ → 1 イベント
- **Stream emit（opt-in）**: タイミングパイプライン → 複数イベント

「実行中に時刻を記録する」と「再生用のイベント粒度」は別レイヤー。

### 3. grill-me の「burst は実測どおり一瞬」は playback と両立しにくい

理論上は正しいが、pipe read の chunk 境界は「本当に一瞬だった出力」と「たまたま同じ read 間隔だった出力」を区別できない。SVG はデフォルトで `--max-fps` なしでも **イベントごとにフレーム** が増えるため、数 ms の間隔でもカクつきとして見える。

**対策**: stream モードでは **burst merge**（実測 gap < 50ms → 同一時刻）をパイプラインに追加。完全な「一瞬」は instant emit モードで担保。

### 4. `execution-duration` と startup offset の役割を混同しない

- `execution-duration`: Enter 後、**最初の出力が現れるまでの最低待ち**（タイピング完了後の間）
- startup offset（PTY 既存）: 遅い初回 chunk を `execution-duration` 以内に寄せる

instant emit では出力は常に `commandStart + execution-duration`。stream emit では startup offset + chunk 時刻を使う。どちらも「タイピング後に少し間を空ける」という scenetake の演出と整合させる必要がある。

### 5. idle 潰しは「元の gap」で測る

調整済み時刻の差分で潰すと、長い idle の直後の短い gap（0.5s）まで連鎖的に潰れてしまう。実装では **調整前の wall-clock gap** を見てから累積時刻を組み立てる。

### 6. `demo.yaml` の docker ステップは docker build ではない

`ghcr.io/asciinema/agg` の GIF 変換であり、プログレスバーが大量に stderr へ流れる。`execution-duration: 1.0` で **1 秒待ってから一括表示** するのが意図したデモ演出。stream 化するとこのステップが最初に壊れた。

本物の `docker build` を自然にリプレイしたい場合は、そのステップだけ `stream-output: true` + `max-duration` を付ける想定。

### 7. highlight は全文マージ後に適用が現実的

chunk 単位で `highlight` を当てると ANSI 境界で壊れやすい。stream emit 時は「全文 highlight → 可視文字オフセットで分割」にした。CSI シーケンス（`\e[31m` 等）のパースで `[` を誤って終端扱いすると分割が壊れる — `SkipAnsiSequence` で `[` 始まりを正しく処理する必要があった。

### 8. PTY burst merge は filtered chunk と raw chunk の対応に注意

`PtyLeadingInitFilter` で捨てた bytes があると、raw `PtyCaptureChunk` のインデックスと emit 対象 chunk のインデックスがずれる。PTY 側は **filter 後の chunk に original time を持たせる** 形で burst merge する。

### 9. 互換性のためのデフォルト値

`Silence-threshold: 2.0` / `max-idle: 0.2` は stream モード向けの緩い値。デフォルト instant emit なら既存シナリオへの影響は小さいが、stream を有効にしたステップだけ挙動が変わる点は spec に明記すべき。

### 10. Dogfooding（完了）

- `tests/fixtures/stream-output-docker.yaml` + `IntegrationDockerBuildStream` — 実際の `docker build`（レイヤーごと `sleep 2`）で stream emit + `max-duration: 12` を検証。Docker 未導入環境では skip。
- `samples/scenario_format.cast` / `.svg` を再生成済み。`stream-output` ステップは `stream-first` / `stream-second` が別イベント（0.25s / 0.1s gap）で記録されることを確認。

### 11. instant emit は stdout→stderr 連結（時系列インターリーブではない）

pipe capture は stdout/stderr を並行読み取りするが、**デフォルト（instant emit）の cast テキストは旧挙動どおり stdout 全文 + stderr 全文** とする。

時系列で chunk を連結すると、`curl -I` のように stderr プログレスと stdout ヘッダが途中で混ざり、`HTTP/1.1` がプログレス行に連結される（`basic.svg` で `TP/1.1` と分裂）など playback が壊れる。`highlight` の行番号も stdout 向け指定がずれる。

**stream emit**（`stream-output: true` / `max-duration`）のときだけ chunk 時系列マージを使う。

### 12. 今後の検討（任意）

- CI で Docker 統合テストを常時実行するか（runner に Docker が必要）
- 長時間ビルド向け `max-duration` の推奨値を README に追記するか
