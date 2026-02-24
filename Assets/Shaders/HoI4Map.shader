// ============================================================================
// HoI4Map.shader — Hearts of Iron 4 style campaign map
//
// Water detection: uses _HeightMap texture. White (high values) = WATER.
// Political overlay: faction colors blended semi-transparently over terrain.
// Borders: thick dark national borders, thin subtle province borders.
// Coast: smooth anti-aliased edges at land/water transitions.
// ============================================================================
Shader "NapoleonicWars/HoI4Map"
{
    Properties
    {
        _BaseMap        ("Terrain Texture",   2D)          = "white" {}
        _HeightMap      ("Height Map",        2D)          = "black" {}
        _ProvinceMap    ("Province ID Map",   2D)          = "black" {}
        _FactionMap     ("Faction Color LUT", 2D)          = "black" {}
        _FlagAtlas      ("Flag Atlas",        2D)          = "white" {}
        _PoliticalAlpha ("Faction Overlay",   Range(0,1))  = 0.55
        _Desaturation   ("Terrain Desat",     Range(0,1))  = 0.30
        _WaterThreshold ("Water Threshold",   Range(0,1))  = 0.70
        _NatBorderWidth ("National Border",   Range(1,8))  = 3.5
        _ProvBorderWidth("Province Border",   Range(0.5,4))= 1.5
        _FlagAlpha      ("Flag Opacity",      Range(0,1))  = 0.35
        _FlagTile       ("Flag Tiling",       Range(0.5,12))= 6.0
        _ZoomFade       ("Zoom Fade",         Range(0,1))  = 0.0
        _NumFactions    ("Num Factions",      Float)       = 24.0
        _DebugMode      ("Debug Mode",        Float)       = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_HeightMap);   SAMPLER(sampler_HeightMap);
            TEXTURE2D(_ProvinceMap); SAMPLER(sampler_ProvinceMap);
            TEXTURE2D(_FactionMap);  SAMPLER(sampler_FactionMap);
            TEXTURE2D(_FlagAtlas);   SAMPLER(sampler_FlagAtlas);
            float4 _ProvinceMap_TexelSize;

            float _PoliticalAlpha, _Desaturation, _WaterThreshold, _DebugMode;
            float _NatBorderWidth, _ProvBorderWidth;
            float _FlagAlpha, _FlagTile, _ZoomFade, _NumFactions;

            // 8 directions for high-quality border detection
            static const float2 DIR8[8] = {
                float2(1,0), float2(-1,0), float2(0,1), float2(0,-1),
                float2(0.707,0.707), float2(-0.707,0.707),
                float2(0.707,-0.707), float2(-0.707,-0.707)
            };

            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 hcs : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.hcs = TransformObjectToHClip(i.pos.xyz);
                o.uv  = i.uv;
                return o;
            }

            // ── Sampling helpers ──
            float3 SampleTerrain(float2 uv) { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb; }
            float  SampleHeight(float2 uv)  { return SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, uv, 0).r; }
            bool   IsWater(float2 uv)       { return SampleHeight(uv) > _WaterThreshold; }
            float  GetProvID(float2 uv)     { return SAMPLE_TEXTURE2D_LOD(_ProvinceMap, sampler_ProvinceMap, uv, 0).r; }
            float4 GetFactionRGBA(float id) { return SAMPLE_TEXTURE2D_LOD(_FactionMap, sampler_FactionMap, float2((id*255.0+0.5)/256.0, 0.5), 0); }
            
            // ── Flag sampling ──
            float4 SampleFlag(float2 uv, float factionIdx)
            {
                float2 tiledUV = frac(uv * _FlagTile);
                float row = floor(factionIdx * _NumFactions);
                row = clamp(row, 0, _NumFactions - 1.0);
                float2 atlasUV = float2(tiledUV.x, (row + tiledUV.y) / _NumFactions);
                return SAMPLE_TEXTURE2D_LOD(_FlagAtlas, sampler_FlagAtlas, atlasUV, 0);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv    = i.uv;
                float2 texel = _ProvinceMap_TexelSize.xy;

                // ── DEBUG MODES ──
                if (_DebugMode > 0.5)
                {
                    float pid_d = GetProvID(uv);
                    if (_DebugMode < 1.5) { float4 fc = GetFactionRGBA(pid_d); return half4(fc.rgb, 1); }
                    if (_DebugMode < 2.5) { return half4(pid_d, pid_d, pid_d, 1); }
                    float h = SampleHeight(uv); return half4(h, h, h, 1);
                }

                // ── 1. Water check ──
                if (IsWater(uv)) return half4(0,0,0,0);

                // ── 2. Terrain base ──
                float3 terrain = SampleTerrain(uv);
                float  gray    = dot(terrain, float3(0.299, 0.587, 0.114));
                float3 desat   = lerp(terrain, float3(gray,gray,gray), _Desaturation);

                // ── 3. Political faction overlay ──
                float  pid     = GetProvID(uv);
                float4 fRGBA   = GetFactionRGBA(pid);
                float3 faction = fRGBA.rgb;
                float  fIdx    = fRGBA.a;
                
                // Blend: terrain shows through under the political tint
                // Multiply-then-lerp gives richer colors like the reference
                float3 tinted  = desat * lerp(float3(1,1,1), faction * 1.8, 0.6);
                float3 land    = lerp(tinted, faction, _PoliticalAlpha);
                
                // ── 3b. Flag overlay ──
                float flagBlend = _FlagAlpha * _ZoomFade;
                if (flagBlend > 0.01 && fIdx > 0.001)
                {
                    float4 flagSample = SampleFlag(uv, fIdx);
                    if (flagSample.a > 0.1)
                        land = lerp(land, flagSample.rgb, flagBlend * flagSample.a);
                }

                // ── 4. Border detection (8-direction for smooth edges) ──
                float natW = 0, provW = 0, coastW = 0;

                [unroll] for (int d = 0; d < 8; d++)
                {
                    float w = (d < 4) ? 0.15 : 0.10; // Cardinal dirs weighted more

                    // Province-width ring
                    float2 uvP = uv + DIR8[d] * texel * _ProvBorderWidth;
                    if (IsWater(uvP))
                    {
                        coastW += w;
                    }
                    else
                    {
                        float nid = GetProvID(uvP);
                        if (abs(pid - nid) > 0.002)
                        {
                            provW += w;
                            float3 nFaction = GetFactionRGBA(nid).rgb;
                            if (distance(faction, nFaction) > 0.05)
                                natW += w;
                        }
                    }

                    // National-width ring (wider offset for thick national borders)
                    float2 uvN = uv + DIR8[d] * texel * _NatBorderWidth;
                    if (!IsWater(uvN))
                    {
                        float nid2 = GetProvID(uvN);
                        if (abs(pid - nid2) > 0.002)
                        {
                            float3 nFaction2 = GetFactionRGBA(nid2).rgb;
                            if (distance(faction, nFaction2) > 0.05)
                                natW += w * 0.5;
                        }
                    }
                }

                // ── 5. Apply borders ──
                float3 result = land;
                
                // National borders: strong dark lines
                float natStrength = smoothstep(0.0, 0.5, natW);
                if (natStrength > 0.01)
                {
                    result = lerp(result, float3(0.03, 0.03, 0.02), natStrength * 0.85);
                }
                
                // Province borders: subtle darkening (only where there's no national border)
                float provStrength = smoothstep(0.0, 0.4, provW);
                if (provStrength > 0.01 && natStrength < 0.15)
                {
                    result = lerp(result, result * 0.65, provStrength * 0.45);
                }

                // ── 6. Coastline: smooth alpha fade ──
                float coastStrength = smoothstep(0.0, 0.6, coastW);
                float alpha = 1.0 - coastStrength * 0.6;
                
                // Darken coast edge slightly for definition
                if (coastStrength > 0.01)
                {
                    result = lerp(result, result * 0.80, coastStrength * 0.3);
                }

                return half4(result, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
