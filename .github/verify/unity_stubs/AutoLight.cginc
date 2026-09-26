// Type-checking stand-in; actual attenuation and shadows are tested in Unity.
#ifndef SABA_STUB_AUTOLIGHT_INCLUDED
#define SABA_STUB_AUTOLIGHT_INCLUDED
#define UNITY_LIGHTING_COORDS(idx1, idx2)
#define UNITY_TRANSFER_LIGHTING(o, uv)
#define UNITY_LIGHT_ATTENUATION(name, i, position) float name = 1.0
#endif
