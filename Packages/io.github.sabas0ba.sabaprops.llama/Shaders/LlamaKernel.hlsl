#ifndef SABAPROPS_LLAMA_KERNEL
#define SABAPROPS_LLAMA_KERNEL

Texture2D<float4> _Weights, _A, _B, _C;
int _Operation, _OffsetLo, _OffsetHi, _Count, _Inner, _Width, _AWidth;
int _HeadSize, _KvMultiple, _Position, _Token, _Rope;

float Weight(int index)
{
    return _Weights.Load(int3(index % 4096, index / 4096, 0)).r;
}

float A(int index) { return _A.Load(int3(index % _AWidth, index / _AWidth, 0)).r; }

float Rotated(int index)
{
    int pair = index - index % 2;
    float angle = _Position * pow(10000.0, -(float)(pair % _HeadSize) / _HeadSize);
    float s, c;
    sincos(angle, s, c);
    float a = _A.Load(int3(pair, 0, 0)).r;
    float b = _A.Load(int3(pair + 1, 0, 0)).r;
    return index % 2 == 0 ? a * c - b * s : a * s + b * c;
}

float4 LlamaKernel(int2 pixel)
{
    int offset = _OffsetHi * 65536 + _OffsetLo;
    int index = pixel.y * _Width + pixel.x;
    // Cache update: B is the old cache and A contains this token's projection.
    if (_Operation == 5)
    {
        if (pixel.y > _Position) return 0;
        if (pixel.y < _Position) return _B.Load(int3(pixel, 0));
        return float4(_Rope != 0 ? Rotated(pixel.x) : A(pixel.x), 0, 0, 0);
    }
    if (_Operation == 6)
    {
        if (pixel.x > _Position) return float4(-1e30, 0, 0, 0);
        int queryBase = pixel.y * _HeadSize;
        int keyBase = pixel.y / _KvMultiple * _HeadSize;
        float score = 0;
        [loop] for (int j = 0; j < _HeadSize; j++)
            score += _A.Load(int3(queryBase + j, 0, 0)).r * _B.Load(int3(keyBase + j, pixel.x, 0)).r;
        return float4(score * rsqrt((float)_HeadSize), 0, 0, 0);
    }
    if (_Operation == 7)
    {
        if (pixel.x > _Position) return 0;
        float maximum = -3.402823e38;
        [loop] for (int t = 0; t <= _Position; t++) maximum = max(maximum, _A.Load(int3(t, pixel.y, 0)).r);
        float sum = 0;
        [loop] for (int t = 0; t <= _Position; t++) sum += exp(_A.Load(int3(t, pixel.y, 0)).r - maximum);
        return float4(exp(_A.Load(int3(pixel, 0)).r - maximum) / sum, 0, 0, 0);
    }
    // Two-level argmax. G carries the original token ID, B validity.
    if (_Operation == 12 || _Operation == 13)
    {
        float4 best = float4(-3.402823e38, 0, 1, 0);
        bool valid = true;
        int begin = _Operation == 12 ? index * 256 : 0;
        int end = _Operation == 12 ? min(begin + 256, _Count) : _Count;
        [loop] for (int i = begin; i < end; i++)
        {
            float4 candidate = _A.Load(int3(i % _AWidth, i / _AWidth, 0));
            valid = valid && isfinite(candidate.r) && (_Operation == 12 || candidate.b > 0.5);
            if (_Operation == 12) candidate.g = i;
            if (i == begin || candidate.r > best.r) best = candidate;
        }
        best.b = valid ? 1 : 0;
        return best;
    }
    if (index >= _Count) return 0;
    float result = 0;
    if (_Operation == 0) result = Weight(offset + _Token * _Count + index);
    else if (_Operation == 1)
    {
        [loop] for (int j = 0; j < _Inner; j++) { float v = A(j); result += v * v; }
        result = rsqrt(result / _Inner + 1e-5);
    }
    else if (_Operation == 2) result = A(index) * _B.Load(int3(0, 0, 0)).r * Weight(offset + index);
    else if (_Operation == 3)
    {
        [loop] for (int j = 0; j < _Inner; j++) result += Weight(offset + index * _Inner + j) * A(j);
    }
    else if (_Operation == 4) result = Rotated(index);
    else if (_Operation == 8)
    {
        int head = index / _HeadSize;
        int valueIndex = head / _KvMultiple * _HeadSize + index % _HeadSize;
        [loop] for (int t = 0; t <= _Position; t++)
            result += _A.Load(int3(t, head, 0)).r * _B.Load(int3(valueIndex, t, 0)).r;
    }
    else if (_Operation == 9) result = A(index) + _B.Load(int3(pixel, 0)).r;
    else if (_Operation == 10) { float g = A(index); result = g / (1 + exp(-g)) * _B.Load(int3(pixel, 0)).r; }
    else if (_Operation == 11) return _A.Load(int3(pixel, 0));
    return float4(result, 0, 0, 0);
}
#endif
