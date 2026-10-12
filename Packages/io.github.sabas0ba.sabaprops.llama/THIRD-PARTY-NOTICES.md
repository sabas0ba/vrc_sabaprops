# Third-Party Notices

本パッケージは Apache License 2.0 で提供します（[LICENSE.md](LICENSE.md)）。次の部分は第三者の成果物に基づくため、その著作権表示と許諾条件を併せて掲載します。

## llama2.c

- 配布元: <https://github.com/karpathy/llama2.c>
- 比較に用いた版: commit `350e04fe35433e6d2941dce5a1f53308f87058eb` の `run.c`
- ライセンス: MIT

次のファイルは、llama2.c の `run.c` と同じアルゴリズムと処理順序に従っています。ソースコードを複製したものではなく C# と HLSL で記述していますが、構成が対応するため表示を掲載します。

| ファイル | 対応する `run.c` の部分 |
| --- | --- |
| `Editor/Core/LlamaTokenizer.cs` | `build_tokenizer`、`encode`（dummy prefix、byte fallback、score による隣接ペアの結合） |
| `Editor/Core/LlamaReference.cs` | `rmsnorm`、`softmax`、`matmul`、`forward` |
| `Editor/Core/LlamaCheckpoint.cs` | `Config`、`memory_map_weights`、`read_checkpoint`（FP32 v0 形式の重みの並び） |
| `Samples~/VRChat/SabaLlamaRunner.cs` | `encode`、`decode`（BOS 直後の先頭空白の除去、byte token の復元） |
| `Shaders/LlamaKernel.hlsl`、`Editor/LlamaProgramBuilder.cs` | `forward` の演算順序 |

```text
MIT License

Copyright (c) 2023 Andrej

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## 形式の仕様

GGUF の読み込みは [GGUF 仕様](https://github.com/ggml-org/ggml/blob/master/docs/gguf.md) に基づいて記述しています。Q4_0 と Q8_0 の復号は、形式が定める block 構造（scale と量子化値）に従う計算です。llama.cpp / ggml のソースコードは取り込んでいません。

## 同梱していないもの

学習済みの重みと語彙は同梱していません。利用者が変換した重みには、その配布元のライセンスが適用されます。
