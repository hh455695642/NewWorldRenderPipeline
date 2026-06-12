Shader "NewWorld/Sky/SkyBoxBase"
{
    Properties
    {
        [NoScaleOffset] _SunZenithGrad ("Sun-Zenith gradient", 2D) = "white" {}
        [NoScaleOffset] _ViewZenithGrad ("View-Zenith gradient", 2D) = "white" {}
        [NoScaleOffset] _SunViewGrad ("Sun-View gradient", 2D) = "white" {}

        _HorizonColor ("Horizon Color", Color) = (0.8, 0.6, 0.4, 1)
        _ZenithColor ("Zenith Color", Color) = (0.5, 0.7, 1.0, 1)
        [NoScaleOffset] _CloudCubeMap ("Cloud cube map", Cube) = "black" {}
        [NoScaleOffset] _CloudCubeMapMask ("Cloud cube map mask", Cube) = "black" {}
        _CloudIntensity ("Cloud intensity", Range(0, 5)) = 1.0
        _CloudRotation("Cloud Rotation (Degrees)", Range(0, 360)) = 0
        _CloudSpeed ("Cloud rotation speed", Float) = 0.001        

        [Header(Sun Settings)]
        _SunRadius ("Sun radius", Range(0,1)) = 0.05
        _SunFalloff ("Sun Falloff", Range(0.1, 10)) = 2.0
        _SunIntensity ("Sun Intensity", Range(0, 5)) = 1.0        
        [Header(Moon Settings)]
        [NoScaleOffset] _MoonCubeMap ("Moon cube map", Cube) = "black" {}
        _MoonRadius ("Moon radius", Range(0,0.2)) = 0.05
        _MoonExposure ("Moon exposure", Range(-16, 16)) = 0
        [Header(Star Settings)]
        [NoScaleOffset] _StarCubeMap ("Star cube map", Cube) = "black" {}
        _StarExposure ("Star exposure", Range(-16, 16)) = 0
        _StarPower ("Star power", Range(1,5)) = 1
        _StarLatitude ("Star latitude", Range(-90, 90)) = 0
        _StarSpeed ("Star speed", Float) = 0.001
    }
    
    SubShader
    {
        Tags
        {
            "RenderPipeline"="NewWorldRenderPipeline"
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
        }
        Cull Off ZWrite Off

        Pass
        {
            Name "NewWorldUnlit"
            Tags { "LightMode" = "NewWorldUnlit" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "../../../../NWRP/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 posOS    : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 posCS        : SV_POSITION;
                float3 viewDirWS    : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f Vertex(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                v2f OUT = (v2f)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.posOS.xyz);
                OUT.posCS = TransformWorldToHClip(positionWS);
                OUT.viewDirWS = positionWS;

                return OUT;
            }

            TEXTURE2D(_SunZenithGrad);      SAMPLER(sampler_SunZenithGrad);
            TEXTURE2D(_ViewZenithGrad);     SAMPLER(sampler_ViewZenithGrad);
            TEXTURE2D(_SunViewGrad);        SAMPLER(sampler_SunViewGrad);
            TEXTURECUBE(_MoonCubeMap);      SAMPLER(sampler_MoonCubeMap);
            TEXTURECUBE(_StarCubeMap);      SAMPLER(sampler_StarCubeMap);            
            TEXTURECUBE(_CloudCubeMap);     SAMPLER(sampler_CloudCubeMap);
            TEXTURECUBE(_CloudCubeMapMask); SAMPLER(sampler_CloudCubeMapMask);

            // DrawSkybox is not a normal DrawRenderers path; keep sky material
            // uniforms loose so Unity binds them consistently for skybox draws.
            float _CloudIntensity, _CloudSpeed, _CloudRotation;
            float3 _SunDir, _MoonDir;
            float4 _HorizonColor, _ZenithColor;
            float _SunRadius, _SunFalloff, _SunIntensity;
            float _MoonRadius, _MoonExposure;
            float4x4 _MoonSpaceMatrix;
            float _StarExposure, _StarPower;
            float _StarLatitude, _StarSpeed;
 

            float3 GetSkyColor(float3 viewDirection)
            {
                return lerp(_HorizonColor.rgb, _ZenithColor.rgb, 1.0 - pow(1.0 - abs(viewDirection.y), 3)); //2.5+ ZenithColor Darker
            }

            float GetSunMask(float sunViewDot, float sunRadius)
            {
                float stepRadius = 1 - sunRadius * sunRadius;
                return step(stepRadius, sunViewDot);
            }

            // 太阳光晕函数 - 围绕太阳的光晕效果
            float3 GetSunHalo(float3 viewDirection, float3 sunDirection, float3 sunColor, float falloff)
            {
                float sunViewDot = dot(sunDirection, viewDirection);
                float halo = pow(saturate(sunViewDot), falloff);
                return sunColor * halo * 0.3; // 光晕强度
            }
            // From Inigo Quilez, https://www.iquilezles.org/www/articles/intersectors/intersectors.html
            float sphIntersect(float3 rayDir, float3 spherePos, float radius)
            {
                float3 oc = -spherePos;
                float b = dot(oc, rayDir);
                float c = dot(oc, oc) - radius * radius;
                float h = b * b - c;
                if(h < 0.0) return -1.0;
                h = sqrt(h);
                return -b - h;
            }

            float3 GetMoonTexture(float3 normal)
            {
                // float3 uvw = normal; //still moon
                float3 uvw = mul(_MoonSpaceMatrix, float4(normal,0)).xyz;

                // Found through trial and error resulting in mul(AngleAxis3x3(0.5*PI, float3(0,1,0)), AngleAxis3x3(-0.08*PI, float3(1,0,0)));
                float3x3 correctionMatrix = float3x3( 0, -0.24869, 0.968583, 0, 0.968583, 0.24869, -1, 0, 0);
                uvw = mul(correctionMatrix, uvw);

                return SAMPLE_TEXTURECUBE(_MoonCubeMap, sampler_MoonCubeMap, uvw).rgb;
            }
            
            // Construct a rotation matrix that rotates around a particular axis by angle
            // From: https://gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
            float3x3 AngleAxis3x3(float angle, float3 axis)
            {
                float c, s;
                sincos(angle, s, c);

                float t = 1 - c;
                float x = axis.x;
                float y = axis.y;
                float z = axis.z;

                return float3x3(
                    t * x * x + c, t * x * y - s * z, t * x * z + s * y,
                    t * x * y + s * z, t * y * y + c, t * y * z - s * x,
                    t * x * z - s * y, t * y * z + s * x, t * z * z + c
                    );
            }
            
            // Rotate the view direction, tilt with latitude, spin with time
            float3 GetStarUVW(float3 viewDir, float latitude, float localSiderealTime)
            {
                // tilt = 0 at the north pole, where latitude = 90 degrees
                float tilt = PI * (latitude - 90) / 180;
                float3x3 tiltRotation = AngleAxis3x3(tilt, float3(1,0,0));

                // 0.75 is a texture offset for lST = 0 equals noon
                float spin = (0.75-localSiderealTime) * 2 * PI;
                float3x3 spinRotation = AngleAxis3x3(spin, float3(0, 1, 0));

                // The order of rotation is important
                float3x3 fullRotation = mul(spinRotation, tiltRotation);

                return mul(fullRotation,  viewDir);
            }
            
            float3 GetCloudUVW(float3 viewDir, float angleDegrees, float time, float speed)
            {
                float angleRad = radians(angleDegrees); // 转弧度
                float angle = fmod(time * speed, 1) * 2.0 * PI;
                float finalAngle = angle + angleRad; // 叠加用户自定义角度
                float3x3 rotMatrix = AngleAxis3x3(finalAngle, float3(0,1,0)); //绕Y轴旋转
                return mul(rotMatrix, viewDir);
            }

            float4 Fragment (v2f IN) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                float3 viewDir = normalize(IN.viewDirWS);

                // Main angles
                float sunViewDot = dot(_SunDir, viewDir);
                float sunZenithDot = _SunDir.y;
                float viewZenithDot = viewDir.y;
                float sunMoonDot = dot(_SunDir, _MoonDir);

                float sunViewDot01 = (sunViewDot + 1.0) * 0.5;
                float sunZenithDot01 = (sunZenithDot + 1.0) * 0.5;

                float3 sunZenithColor = SAMPLE_TEXTURE2D(_SunZenithGrad, sampler_SunZenithGrad, float2(sunZenithDot01, 0.5)).rgb;
                float3 viewZenithColor = SAMPLE_TEXTURE2D(_ViewZenithGrad, sampler_ViewZenithGrad, float2(sunZenithDot01, 0.5)).rgb;
                float vzMask = pow(saturate(1.0 - viewZenithDot), 8);
                float3 sunViewColor = SAMPLE_TEXTURE2D(_SunViewGrad, sampler_SunViewGrad, float2(sunZenithDot01, 0.5)).rgb;
                float svMask = pow(saturate(sunViewDot), 16);
                
                // 旋转采样方向
                float3 cloudUVW = GetCloudUVW(viewDir, _CloudRotation, _Time.y, _CloudSpeed);
                // 采样云朵纹理颜色（假设云朵图是遮罩黑白，按需要调整，也可以用rgb）
                float3 cloudColor = SAMPLE_TEXTURECUBE(_CloudCubeMap, sampler_CloudCubeMap, cloudUVW).rgb;
                float cloudMask = SAMPLE_TEXTURECUBE(_CloudCubeMapMask, sampler_CloudCubeMapMask, cloudUVW).r;  
                // 例：云朵图多亮，天空偏暗，叠加云影效果
                cloudMask = saturate(cloudMask); //如用红通道为云量遮罩             
                // float3 cloudOverlay = lerp(float3(0,0,0), _ZenithColor.rgb, cloudMask * cloudColor *  _CloudIntensity);
                // 将云朵叠加叠加在昼间天空色上（只加白天云）
                float sunHeight01 = saturate(_SunDir.y * 4); //对应太阳高度控制白天阶段

                // The sky
                float3 skyColor01 = sunZenithColor + vzMask * viewZenithColor + svMask * sunViewColor;
                float3 skyColor02 = GetSkyColor(viewDir) + vzMask * viewZenithColor;
                float3 skyColor = lerp(skyColor01, skyColor02, 0.8);
                float cloudBlend = saturate(cloudMask * _CloudIntensity) * sunHeight01;
                float3 finalSkyColor = lerp(skyColor, cloudColor, cloudBlend); //cloudOverlay + cloudColor
                skyColor = finalSkyColor;

                // The sun
                float sunMask = GetSunMask(sunViewDot, _SunRadius);
                float3 sunColor = _MainLightColor.rgb * sunMask;
                float3 sunHalo = GetSunHalo(viewDir, _SunDir, _MainLightColor.rgb, _SunFalloff) * _SunIntensity; 

                // The moon
                float moonIntersect = sphIntersect(viewDir, _MoonDir, _MoonRadius);
                float moonMask = moonIntersect > -1 ? 1 : 0;
                float3 moonNormal = normalize(viewDir * moonIntersect - _MoonDir);
                float moonNdotL = saturate(dot(moonNormal, _SunDir));
                float3 moonTexture = GetMoonTexture(moonNormal);
                float3 moonColor = moonMask * moonNdotL * exp2(_MoonExposure) * moonTexture;

                // The stars
                //float3 starUVW = viewDir; //still star
                float3 starUVW = GetStarUVW(viewDir, _StarLatitude, fmod(_Time.y * _StarSpeed, 1.0)); //rotate the star!
                float3 starColor = SAMPLE_TEXTURECUBE_LOD(_StarCubeMap, sampler_StarCubeMap, starUVW, 0.0).rgb;
                starColor = pow(abs(starColor), _StarPower);
                starColor *= exp2(_StarExposure);
                starColor *= (1 - sunMask) * (1 - moonMask); //paint star behind the sun and moon
                float starStrength = (1 - sunViewDot01) * (saturate(-sunZenithDot)); //avoid star shinning at sunny day
                starColor *= starStrength;               

                float3 col = skyColor + sunColor + sunHalo + moonColor + starColor;
                return float4(col, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
