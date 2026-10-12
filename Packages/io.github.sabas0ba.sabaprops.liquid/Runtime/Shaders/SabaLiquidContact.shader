Shader "Hidden/SabaProps/Liquid/Contact"
{
    Properties { _MainTex ("Source", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "SabaLiquidCanvas.cginc"
    sampler2D _MainTex, _DonorTex, _DonorPigment, _DonorFilm, _DonorDepth;
    float3 _DonorPoint, _DonorNormal, _DonorExtent, _Motion;
    float3 _ReceiverPoint, _ReceiverNormal, _ReceiverExtent;
    float3 _MapX, _MapY, _MapZ;
    float _DonorInset, _ReceiverInset, _Channel;
    float4 _Contact; // radius, fraction, wet smear strength, deterministic seed
    struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct varying { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
    varying vert(appdata v) { varying o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

    float component(float3 p, int axis) { return axis == 0 ? p.x : (axis == 1 ? p.y : p.z); }
    int faceFor(float3 n)
    {
        float3 a = abs(n);
        int axis = a.x >= a.y && a.x >= a.z ? 0 : (a.y >= a.z ? 1 : 2);
        return axis * 2 + (component(n, axis) < 0 ? 1 : 0);
    }
    float3 metric(float2 uv, int axis, float3 extent, float inset, float3 centre)
    {
        float2 q = SabaLiquidTileUvAtAtlasUv(uv, inset) * 2 - 1;
        return axis == 0 ? float3(centre.x, q.y * extent.y, q.x * extent.z)
            : (axis == 1 ? float3(q.x * extent.x, centre.y, q.y * extent.z)
                         : float3(q.x * extent.x, q.y * extent.y, centre.z));
    }
    float2 donorUv(float3 position)
    {
        int face = faceFor(_DonorNormal);
        return SabaLiquidAtlasUv(face, SabaLiquidTileUv(position / _DonorExtent, face / 2), _DonorInset);
    }
    float patch(float3 offset)
    {
        float edge = 1 - smoothstep(_Contact.x * 0.65, _Contact.x, length(offset));
        float grain = lerp(0.45, 1, SabaLiquidValueNoise(offset.xy * 190 + offset.z * 71 + _Contact.w));
        return edge * grain;
    }
    float transfer(float2 uv, float3 position)
    {
        float4 film = tex2D(_DonorFilm, uv);
        float2 depth = tex2D(_DonorDepth, uv).rg;
        float valid = SabaLiquidDepthMask(component(position, faceFor(_DonorNormal) / 2), depth.r, depth.g, 0.03);
        // Dry pigment transfers weakly. Viscosity reduces wet transfer.
        return _Contact.y * lerp(0.08, 1, saturate(film.r) * (1 - 0.7 * film.b)) * valid;
    }
    float4 receive(varying i) : SV_Target
    {
        float4 self = tex2D(_MainTex, i.uv);
        int face = SabaLiquidFaceAtAtlasUv(i.uv);
        if (face != faceFor(_ReceiverNormal)) return self;
        float3 p = metric(i.uv, face / 2, _ReceiverExtent, _ReceiverInset, _ReceiverPoint);
        float3 offset = p - _ReceiverPoint;
        float3 donor = _DonorPoint + _MapX * offset.x + _MapY * offset.y + _MapZ * offset.z;
        float2 uv = donorUv(donor);
        float inside = step(max(max(abs(donor.x / _DonorExtent.x), abs(donor.y / _DonorExtent.y)), abs(donor.z / _DonorExtent.z)), 1);
        float amount = patch(offset) * transfer(uv, donor) * inside;
        float4 sample = tex2D(_DonorTex, uv);
        float cover = saturate(tex2D(_DonorPigment, uv).a * amount);
        if (_Channel < 0.5) return saturate(self * (1 - cover) + sample * amount);
        if (_Channel < 1.5)
        {
            float added = sample.r * amount;
            return float4(min(self.r + added, 1), lerp(self.gba, sample.gba, added / max(self.r + added, 1e-5)));
        }
        if (_Channel < 2.5)
        {
            float contact = max(cover, tex2D(_DonorFilm, uv).r * amount);
            return contact > 0.00001 ? float4(component(_ReceiverPoint, face / 2), 1, 0, 0) : self;
        }
        return saturate(self * (1 - cover) + sample * amount);
    }
    float4 remove(varying i) : SV_Target
    {
        float4 self = tex2D(_MainTex, i.uv);
        int face = SabaLiquidFaceAtAtlasUv(i.uv);
        if (face != faceFor(_DonorNormal)) return self;
        float3 p = metric(i.uv, face / 2, _DonorExtent, _DonorInset, _DonorPoint);
        float weight = patch(p - _DonorPoint);
        float amount = weight * transfer(i.uv, p);
        float4 film = tex2D(_DonorFilm, i.uv);
        float smear = weight * _Contact.z * saturate(film.r) * (1 - film.b);
        float3 upstream = p - _Motion * smear;
        // Keep the warp within the contact patch: no remote pixels enter the smear.
        float2 uv = donorUv(lerp(p, upstream, patch(upstream - _DonorPoint)));
        float4 moved = tex2D(_MainTex, uv);
        if (_Channel < 0.5 || _Channel > 2.5) return moved * (1 - amount);
        if (_Channel < 1.5) return float4(moved.r * (1 - amount), moved.gba);
        return moved;
    }
    ENDCG
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment receive
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment remove
            ENDCG
        }
    }
}
