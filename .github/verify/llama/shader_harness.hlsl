#include "LlamaKernel.hlsl"
float4 main(float4 position : SV_POSITION) : SV_Target
{
    return LlamaKernel(int2(position.xy));
}
