// textures
	Texture2D gInputMap;
	Texture2D gOverlayMap;
	Texture2D gDepthMap;

	SamplerState samInputImage {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};
	
// input resources
	cbuffer cbPerObject : register(b0) {
		float4 gScreenSize;
		float gSizeMultipler;
		float gMultipler;
		float2 gBokenMultipler;
	}

// fn structs
	struct VS_IN {
		float3 PosL    : POSITION;
		float2 Tex     : TEXCOORD;
	};

	struct PS_IN {
		float4 PosH    : SV_POSITION;
		float2 Tex     : TEXCOORD;
	};

// one vertex shader for everything
	PS_IN vs_main(VS_IN vin) {
		PS_IN vout;
		vout.PosH = float4(vin.PosL, 1.0);
		vout.Tex = vin.Tex;
		return vout;
	}

	float4 ps_Copy(PS_IN pin) : SV_Target {
		return gInputMap.Sample(samInputImage, pin.Tex);
	}

	technique10 Copy {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Copy()));
		}
	}

	float4 ps_Cut(PS_IN pin) : SV_Target {
		return gInputMap.Sample(samInputImage, (pin.Tex - 0.5) * gSizeMultipler + 0.5);
	}

	technique10 Cut {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Cut()));
		}
	}

	float4 ps_CopyNoAlpha(PS_IN pin) : SV_Target {
		return float4(gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0).rgb, 1.0);
	}

	technique10 CopyNoAlpha {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_CopyNoAlpha()));
		}
	}

// accumulation
	float4 ps_Accumulate(PS_IN pin) : SV_Target {
		return saturate(gInputMap.Sample(samInputImage, pin.Tex));
	}

	technique10 Accumulate {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Accumulate()));
		}
	}

	float4 ps_AccumulateDivide(PS_IN pin) : SV_Target {
		return gInputMap.Sample(samInputImage, pin.Tex) * gMultipler;
	}

	technique10 AccumulateDivide {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_AccumulateDivide()));
		}
	}

	float4 ps_AccumulateBokehDivide(PS_IN pin) : SV_Target{
		float4 b = gInputMap.Sample(samInputImage, pin.Tex);
		float4 o = gOverlayMap.Sample(samInputImage, pin.Tex);
		float m = saturate(10.0 * dot(o.rgb, float3(0.299f, 0.587f, 0.114f)) - 0.9) * gBokenMultipler.x;
		return b * gMultipler * (1.0 - m) + o * m;
	}

	technique10 AccumulateBokehDivide {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_AccumulateBokehDivide()));
		}
	}

// overlay (gui) mode
	float4 ps_Overlay(PS_IN pin) : SV_Target {
		float4 b = gInputMap.Sample(samInputImage, pin.Tex);
		float4 o = gOverlayMap.Sample(samInputImage, pin.Tex);
		return float4(b.rgb * (1 - o.a) + o.rgb * o.a, 1.0);
	}

	technique10 Overlay {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Overlay()));
		}
	}

// depth to linear
	float4 ps_DepthToLinear(PS_IN pin) : SV_Target {
		float z = gInputMap.Sample(samInputImage, pin.Tex).r;
		float n = gScreenSize.x;
		float f = gScreenSize.y;
		// float e = zNear * zFar / (zFar + zNear - b * (zFar - zNear));
		float e = (-1 * f * n / (f - n)) / (z - (f + n) / (f - n)) / gScreenSize.z;
		return float4((float3)saturate(e), 1.0);
	}

	technique10 DepthToLinear {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_DepthToLinear()));
		}
	}

// shadow (gui) mode
	float4 ps_Shadow(PS_IN pin) : SV_Target{
		float4 b = gInputMap.Sample(samInputImage, pin.Tex);

		float4 tex = gOverlayMap.Sample(samInputImage, pin.Tex);
		tex.rgb *= tex.a;

		float x, y;
		for (x = -1; x <= 1; x++)
			for (y = -1; y <= 1; y++) {
				float4 v = gOverlayMap.Sample(samInputImage, pin.Tex + float2(x * gScreenSize.z, y * gScreenSize.w));
				tex.a = max(tex.a, v.a);
			}

		return float4(tex.rgb, max(b.a, tex.a));;
	}

	technique10 Shadow {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Shadow()));
		}
	}

// depth debug mode
	float4 ps_Depth(PS_IN pin) : SV_Target{
		float4 background = gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0f);

		float mx = gScreenSize.z * 20;
		float my = gScreenSize.w * 20;
		float x = max(1 - gSizeMultipler - mx, 0);
		float y = max(1 - gSizeMultipler - my, 0);

		float rx = max(1 - mx, gSizeMultipler);
		float ry = max(1 - my, gSizeMultipler);

		if (pin.Tex.x > x && pin.Tex.x < rx) {
			if (pin.Tex.y > y && pin.Tex.y < ry) {
				float2 depthUv = float2((pin.Tex.x - x) / gSizeMultipler, (pin.Tex.y - y) / gSizeMultipler);
				return gDepthMap.SampleLevel(samInputImage, depthUv, 0.0f).r;
			}
		}

		return background;
	}

	technique10 Depth { // PT
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Depth()));
		}
	}

// fxaa
	#define FXAA_PRESET 5
	#include "FXAA.fx"

	float4 ps_Fxaa(PS_IN pin) : SV_Target {
		FxaaTex tex = { samInputImage, gInputMap };
		float3 aaImage = FxaaPixelShader(pin.Tex, tex, gScreenSize.zw);
		return float4(aaImage, 1.0f);
	}

	technique10 Fxaa { // PT
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_Fxaa() ) );
		}
	}