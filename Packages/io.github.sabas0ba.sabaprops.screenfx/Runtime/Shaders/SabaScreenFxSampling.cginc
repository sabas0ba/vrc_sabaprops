#ifndef SABA_SCREEN_FX_SAMPLING_INCLUDED
#define SABA_SCREEN_FX_SAMPLING_INCLUDED

// uv is already packed by ComputeGrabScreenPos; delta is in per-eye UV units.
inline float2 SabaFxGrabUv(float2 uv, float2 delta, float4 scaleOffset, float2 texelSize)
{
    float2 inset = min(abs(texelSize) * 0.5, scaleOffset.xy * 0.5);
    // Keep bilinear filtering inside the eye as well as the sample centre.
    return clamp(uv + delta * scaleOffset.xy,
        scaleOffset.zw + inset, scaleOffset.zw + scaleOffset.xy - inset);
}

#endif
