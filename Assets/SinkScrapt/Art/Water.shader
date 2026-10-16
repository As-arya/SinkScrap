Shader "SinkScrapt/Water"
{
    // Layered sine swell with an analytic normal: fresnel blend towards the sky colour and a glittering sun path, as before.
    // The surface is now transparent: it reads the camera depth texture to find how much water lies in front of whatever is
    // behind it, so shallow water shows the floor through a tint, deep water fades to _DeepColor, and a thin foam line marks
    // where the surface meets hulls and walls. Seen from below it is a dim, mostly opaque ceiling.
    Properties
    {
        _BaseColor("Sea",Color)=(0.04,0.23,0.25,1)
        _SkyColor("Sky",Color)=(0.36,0.42,0.52,1)
        _DeepColor("Deep",Color)=(0.01,0.06,0.07,1)
        _Clarity("Clarity (metres)",Float)=4
        _Swell("Swell strength",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" "IgnoreProjector"="True" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float fog:TEXCOORD1;float4 screen:TEXCOORD2;float eye:TEXCOORD3;};
            CBUFFER_START(UnityPerMaterial) float4 _BaseColor; float4 _SkyColor; float4 _DeepColor; float _Clarity; float _Swell; CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings o;o.world=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);
                o.screen=ComputeScreenPos(o.positionCS);o.eye=-TransformWorldToView(o.world).z;return o;
            }
            // height gradient of three crossing swells
            float2 Slope(float2 p,float t)
            {
                float2 d1=normalize(float2(1,0.35)),d2=normalize(float2(-0.4,1)),d3=normalize(float2(0.7,-0.8));
                float a=dot(p,d1)*0.55+t*0.9,b=dot(p,d2)*0.9-t*0.7,c=dot(p,d3)*1.9+t*1.3;
                return d1*cos(a)*0.30+d2*cos(b)*0.18+d3*cos(c)*0.07;
            }
            half4 frag(Varyings input):SV_Target
            {
                float3 view=_WorldSpaceCameraPos-input.world;float dist=length(view);view/=max(dist,0.001);
                float detail=saturate(1.0-dist/160.0);
                float2 slope=Slope(input.world.xz,_Time.y)*(0.25+0.75*detail)*_Swell;
                float3 n=normalize(float3(-slope.x,1.0,-slope.y));
                bool below=_WorldSpaceCameraPos.y<input.world.y;
                // Water between this surface and the opaque scene behind it, measured along the view ray.
                float2 uv=input.screen.xy/input.screen.w;
                float sceneEye=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                float thickness=max(0.0,sceneEye-input.eye);
                float path=thickness*dist/max(input.eye,0.001);
                float murk=1.0-exp(-path/max(_Clarity,0.05));
                float foam=(1.0-saturate(thickness/0.18))*detail;
                if(below)
                {
                    float3 under=lerp(_DeepColor.rgb,_BaseColor.rgb,0.35);
                    return half4(MixFog(under,input.fog),0.82);
                }
                float fresnel=pow(1.0-saturate(dot(n,view)),4.0);
                float3 refl=reflect(-view,n);
                float3 sky=lerp(_SkyColor.rgb*0.75,_SkyColor.rgb*1.15,saturate(refl.y*2.0));
                float3 sunDir=normalize(_MainLightPosition.xyz);
                float glint=pow(saturate(dot(refl,sunDir)),90.0)*smoothstep(0.0,0.25,sunDir.y+0.2);
                float shade=0.9+0.2*saturate(dot(n,sunDir));
                float3 body=lerp(_BaseColor.rgb*shade,_DeepColor.rgb,murk);
                float reflectAmount=saturate(fresnel*0.9+0.08);
                float3 color=lerp(body,sky,reflectAmount);
                color+=_MainLightColor.rgb*glint*(0.6+0.4*detail);
                color=lerp(color,float3(0.62,0.68,0.66)*(_SkyColor.rgb*0.6+0.4),foam*0.55);
                float alpha=saturate(lerp(0.32,1.0,murk)+reflectAmount*0.5+foam*0.4+glint);
                return half4(MixFog(color,input.fog),alpha);
            }
            ENDHLSL
        }
    }
}
