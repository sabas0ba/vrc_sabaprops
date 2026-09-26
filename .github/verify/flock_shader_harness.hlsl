// Standalone HLSL harness for SabaProps/Flock/Swarm.
// The shader is a plain vertex/fragment pair, so the harness runs the vertex
// function and feeds its output to the fragment function. Unity's includes
// are replaced by the stubs in unity_stubs/.

#include "flock_shader_body.hlsl"

float4 main(appdata v) : SV_POSITION
{
    v2f o = vert(v);
    float4 colour = frag(o);
    return o.pos + colour;
}
