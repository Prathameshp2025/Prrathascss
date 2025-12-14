namespace OmniEngine
{
    /// <summary>
    /// Collection of HLSL Vertex Shader source codes.
    /// </summary>
    public static class VertexShaders
    {
        // Standard Transform + Normal + Texture
        public const string StandardVS = @"
#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

matrix WorldViewProjection;
matrix World;

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
    float4 WorldPos : TEXCOORD1;
	float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

    // Calculate position
	output.Position = mul(input.Position, WorldViewProjection);
    output.WorldPos = mul(input.Position, World);
    
    // Pass through normal and uv
    output.Normal = mul(input.Normal, (float3x3)World);
    output.TexCoord = input.TexCoord;

	return output;
}

technique BasicColorDrawing
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
        // Pixel shader defined externally
	}
};";
    }
}