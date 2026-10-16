Shader "Hidden/SinkScrapt/RetroPost"
{
    // Full-screen PS1-horror look: ordered dither + 5-bit colour banding, film grain, vignette and a gloomy grade.
    // Gameplay feeds _SinkDamage / _SinkWet / _SinkFear through RetroLook (global shader floats), so feedback stays behind the HUD.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "RetroPost"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _SinkRetro;   // 0 = off, 1 = full filter
            float _SinkDamage;  // red hit flash
            float _SinkWet;     // underwater / drowning
            float _SinkFear;    // monster proximity pulse
            float _SinkTime;
            float _SinkBright;  // player brightness setting, 1 = default

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            static const float kBayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 centered = uv - 0.5;
                float edge = dot(centered, centered);

                // Fear adds a little chromatic split towards the screen edge.
                float split = (_SinkFear * 0.006 + _SinkWet * 0.002) * saturate(edge * 4.0);
                float3 col;
                col.r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(split, 0), 0).r;
                col.g = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).g;
                col.b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv - float2(split, 0), 0).b;

                float2 px = floor(uv * _ScreenParams.xy);
                float luma = dot(col, float3(0.299, 0.587, 0.114));

                // Gloomy grade: pull saturation down, tint shadows towards the sea.
                float retro = saturate(_SinkRetro);
                col = lerp(col, luma.xxx, 0.22 * retro);
                col *= lerp(float3(1, 1, 1), float3(0.93, 1.02, 1.01), retro);

                // Ordered dither and 5-bit colour banding.
                float dither = kBayer[(uint(px.y) & 3u) * 4u + (uint(px.x) & 3u)] / 16.0 - 0.5;
                float bright = _SinkBright > 0.01 ? _SinkBright : 1.0;
                col = pow(max(col, 0.0), lerp(1.0, 0.86, retro) / bright); // lift the murk a little so dark rooms stay readable
                float levels = lerp(255.0, 44.0, retro);
                col = floor(col * levels + 0.5 + dither * 0.42 * retro) / levels;

                // Film grain (mostly in the mid-tones) and a faint lamp flicker.
                float grain = Hash(px + floor(_SinkTime * 24.0) * 17.31) - 0.5;
                col += grain * 0.034 * retro * (0.35 + luma * 1.6);
                col *= 1.0 + (Hash(float2(floor(_SinkTime * 9.0), 3.7)) - 0.5) * 0.03 * retro;

                // Vignette.
                col *= 1.0 - edge * lerp(0.15, 1.15, retro);

                // Gameplay feedback.
                col = lerp(col, col * float3(0.42, 0.78, 0.82), _SinkWet * 0.65);
                col *= 1.0 - _SinkWet * edge * 1.6;
                col *= 1.0 - _SinkFear * edge * (1.5 + 0.8 * sin(_SinkTime * 7.0));
                float hit = _SinkDamage * saturate(edge * 3.0 + 0.25);
                col = lerp(col, float3(0.72, 0.04, 0.03) * (0.4 + luma), hit);

                return float4(saturate(col), 1);
            }
            ENDHLSL
        }
    }
}
