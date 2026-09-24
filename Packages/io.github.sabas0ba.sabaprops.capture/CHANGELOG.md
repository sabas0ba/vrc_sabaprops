# Changelog

このパッケージの変更点をまとめています。フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [0.1.0] - 2026-09-24

最初のリリースです。ローカル専用の Recorder と再生パネルを収録します。

### Added

- `Capture Recorder`: RenderTexture または Camera から一定間隔で画像を撮り、実行時に確保する RenderTexture の配列へ有限枚数を保持する Udon の挙動
  - 撮影間隔、解像度、画素形式 (ARGB32 / RGB565)、最大枚数、VRAM の予算を指定できます
  - 満杯時の動作として、上書き・停止・間引きの 3 種類を選べます。間引きは 1 枚おきに捨てて撮影間隔を 2 倍にし、撮影開始から現在までを均等な間隔で覆います
  - 撮影時刻は予定時刻の格子から決めるため、フレームレートの揺れや処理落ちで間隔がずれません
- `Capture Player`: 保持した画像をスクラブ、コマ送り、再生、最新への追従で見返す Udon の挙動。RawImage と Renderer に表示でき、タイムラインのサムネイルを並べられます
- Recorder と再生パネルを配置する GameObject メニュー
- 撮影周期、枚数の計算、リングバッファ、間引き、再生位置の計算を Unity 無しで実行するオフライン検査
