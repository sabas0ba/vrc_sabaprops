using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SabaProps.Llama
{
    [Serializable]
    public sealed class LlamaTokenizer
    {
        public string[] pieces;
        public float[] scores;
        public int[] byteTokens;
        public string[] sortedPieces;
        public int[] sortedIds;

        public static LlamaTokenizer Read(string path, int vocabulary)
        {
            var tokenizer = new LlamaTokenizer { pieces = new string[vocabulary], scores = new float[vocabulary] };
            using (var reader = new BinaryReader(File.OpenRead(path), new UTF8Encoding(false, true)))
            {
                int maxLength = reader.ReadInt32();
                if (maxLength < 1 || maxLength > 4096) throw new InvalidDataException("tokenizerの最大長が不正です。");
                long total = 0;
                for (int i = 0; i < vocabulary; i++)
                {
                    tokenizer.scores[i] = reader.ReadSingle();
                    if (float.IsNaN(tokenizer.scores[i]) || float.IsInfinity(tokenizer.scores[i]))
                        throw new InvalidDataException("tokenizer scoreが有限値ではありません。");
                    int length = reader.ReadInt32();
                    total += length;
                    if (length < 1 || length > maxLength || total > 8 * 1024 * 1024)
                        throw new InvalidDataException("tokenizerの文字列長が不正です。");
                    byte[] bytes = reader.ReadBytes(length);
                    if (bytes.Length != length) throw new EndOfStreamException();
                    tokenizer.pieces[i] = new UTF8Encoding(false, true).GetString(bytes);
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                    throw new InvalidDataException("tokenizerの末尾に余分なデータがあります。");
            }
            tokenizer.Prepare();
            return tokenizer;
        }

        public void Prepare()
        {
            if (pieces.Length < 3 || pieces[1] != "<s>" || pieces[2] != "</s>")
                throw new InvalidDataException("BOS=1、EOS=2のtokenizerのみ対応しています。");
            sortedPieces = (string[])pieces.Clone();
            sortedIds = new int[pieces.Length];
            for (int i = 0; i < sortedIds.Length; i++) sortedIds[i] = i;
            Array.Sort(sortedPieces, sortedIds, StringComparer.Ordinal);
            for (int i = 1; i < sortedPieces.Length; i++)
                if (sortedPieces[i] == sortedPieces[i - 1]) throw new InvalidDataException("語彙が重複しています。");
            byteTokens = new int[256];
            for (int i = 0; i < byteTokens.Length; i++) byteTokens[i] = Find("<0x" + i.ToString("X2") + ">");
            if (Find(" ") < 0) throw new InvalidDataException("空白tokenが必要です。");
        }

        public int Find(string piece)
        {
            int index = Array.BinarySearch(sortedPieces, piece, StringComparer.Ordinal);
            return index < 0 ? -1 : sortedIds[index];
        }

        public int[] Encode(string text)
        {
            if (text == null || text.Length > 256) throw new ArgumentException("入力は256 UTF-16 code units以内です。");
            var tokens = new List<int> { 1 };
            if (text.Length > 0) tokens.Add(Find(" "));
            for (int i = 0; i < text.Length;)
            {
                int length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                string piece = text.Substring(i, length);
                i += length;
                int id = Find(piece);
                if (id >= 0) tokens.Add(id);
                else foreach (byte value in Encoding.UTF8.GetBytes(piece))
                {
                    if (byteTokens[value] < 0) throw new InvalidDataException("必要なbyte fallback tokenがありません。");
                    tokens.Add(byteTokens[value]);
                }
            }
            while (true)
            {
                int bestIndex = -1, bestId = -1;
                float bestScore = float.NegativeInfinity;
                for (int i = 1; i + 1 < tokens.Count; i++)
                {
                    int id = Find(pieces[tokens[i]] + pieces[tokens[i + 1]]);
                    if (id >= 0 && scores[id] > bestScore)
                    {
                        bestScore = scores[id]; bestIndex = i; bestId = id;
                    }
                }
                if (bestIndex < 0) break;
                tokens[bestIndex] = bestId;
                tokens.RemoveAt(bestIndex + 1);
            }
            return tokens.ToArray();
        }

        public static LlamaTokenizer Fixture()
        {
            var tokenizer = new LlamaTokenizer
            {
                pieces = new[] { "<unk>", "<s>", "</s>", " ", "a", "b", "ab", " ab" },
                scores = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 1f, 2f }
            };
            tokenizer.Prepare();
            return tokenizer;
        }
    }
}
