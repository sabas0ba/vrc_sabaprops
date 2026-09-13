#ifndef SABAPROPS_LLAMA_KERNEL
#define SABAPROPS_LLAMA_KERNEL

Texture2D<float4> _Weights, _A, _B, _C;
float _Operation, _OffsetLo, _OffsetHi, _Count, _Inner, _Width, _AWidth;
float _HeadSize, _KvMultiple, _Position, _Token, _Rope;

float Weight(int index)
{
    return _Weights.Load(int3(index % 4096, index / 4096, 0)).r;
}

float A(int index)
{
    int width = (int)_AWidth;
    return _A.Load(int3(index % width, index / width, 0)).r;
}

float Rotated(int index)
{
    int pair = index - index % 2;
    float angle = _Position * pow(10000.0, -(float)(pair % (int)_HeadSize) / _HeadSize);
    float s, c;
    sincos(angle, s, c);
    float a = _A.Load(int3(pair, 0, 0)).r;
    float b = _A.Load(int3(pair + 1, 0, 0)).r;
    return index % 2 == 0 ? a * c - b * s : a * s + b * c;
}

float4 LlamaKernel(int2 pixel)
{
    // Material.SetInt is float-backed in Unity 2022.3. All stored values fit
    // exactly in float; convert before address arithmetic and integer division.
    int operation = (int)_Operation;
    int offsetLo = (int)_OffsetLo;
    int offsetHi = (int)_OffsetHi;
    int count = (int)_Count;
    int inner = (int)_Inner;
    int width = (int)_Width;
    int aWidth = (int)_AWidth;
    int headSize = (int)_HeadSize;
    int kvMultiple = (int)_KvMultiple;
    int position = (int)_Position;
    int token = (int)_Token;
    int rope = (int)_Rope;
    int offset = offsetHi * 65536 + offsetLo;
    int index = pixel.y * width + pixel.x;
    // Cache update: B is the old cache and A contains this token's projection.
    if (operation == 5)
    {
        if (pixel.y > position) return 0;
        if (pixel.y < position) return _B.Load(int3(pixel, 0));
        return float4(rope != 0 ? Rotated(pixel.x) : A(pixel.x), 0, 0, 0);
    }
    if (operation == 6)
    {
        if (pixel.x > position) return float4(-1e30, 0, 0, 0);
        int queryBase = pixel.y * headSize;
        int keyBase = pixel.y / kvMultiple * headSize;
        float score = 0;
        [loop] for (int j = 0; j < headSize; j++)
            score += _A.Load(int3(queryBase + j, 0, 0)).r * _B.Load(int3(keyBase + j, pixel.x, 0)).r;
        return float4(score * rsqrt((float)headSize), 0, 0, 0);
    }
    if (operation == 7)
    {
        if (pixel.x > position) return 0;
        float maximum = -3.402823e38;
        [loop] for (int t = 0; t <= position; t++) maximum = max(maximum, _A.Load(int3(t, pixel.y, 0)).r);
        float sum = 0;
        [loop] for (int t = 0; t <= position; t++) sum += exp(_A.Load(int3(t, pixel.y, 0)).r - maximum);
        return float4(exp(_A.Load(int3(pixel, 0)).r - maximum) / sum, 0, 0, 0);
    }
    // Two-level argmax. G carries the original token ID, B validity.
    if (operation == 12 || operation == 13)
    {
        float4 best = float4(-3.402823e38, 0, 1, 0);
        bool valid = true;
        int begin = operation == 12 ? index * 256 : 0;
        int end = operation == 12 ? min(begin + 256, count) : count;
        [loop] for (int i = begin; i < end; i++)
        {
            float4 candidate = _A.Load(int3(i % aWidth, i / aWidth, 0));
            valid = valid && isfinite(candidate.r) && (operation == 12 || candidate.b > 0.5);
            if (operation == 12) candidate.g = i;
            if (i == begin || candidate.r > best.r) best = candidate;
        }
        best.b = valid ? 1 : 0;
        return best;
    }
    if (index >= count) return 0;
    float result = 0;
    if (operation == 0) result = Weight(offset + token * count + index);
    else if (operation == 1)
    {
        [loop] for (int j = 0; j < inner; j++) { float v = A(j); result += v * v; }
        result = rsqrt(result / inner + 1e-5);
    }
    else if (operation == 2) result = A(index) * _B.Load(int3(0, 0, 0)).r * Weight(offset + index);
    else if (operation == 3)
    {
        [loop] for (int j = 0; j < inner; j++) result += Weight(offset + index * inner + j) * A(j);
    }
    else if (operation == 4) result = Rotated(index);
    else if (operation == 8)
    {
        int head = index / headSize;
        int valueIndex = head / kvMultiple * headSize + index % headSize;
        [loop] for (int t = 0; t <= position; t++)
            result += _A.Load(int3(t, head, 0)).r * _B.Load(int3(valueIndex, t, 0)).r;
    }
    else if (operation == 9) result = A(index) + _B.Load(int3(pixel, 0)).r;
    else if (operation == 10) { float g = A(index); result = g / (1 + exp(-g)) * _B.Load(int3(pixel, 0)).r; }
    else if (operation == 11) return _A.Load(int3(pixel, 0));
    return float4(result, 0, 0, 0);
}
#endif
