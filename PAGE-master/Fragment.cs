namespace OmniEngine
{
    /// <summary>
    /// Collection of HLSL Pixel (Fragment) Shader source codes.
    /// </summary>
    public static class FragmentShaders
    {
        // Advanced "AAA-Lite" Shader: Blinn-Phong + Rim Lighting + High Quality Filtering
        public const string StandardPS = @"
#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- INPUTS ---
Texture2D Texture : register(t0);
sampler TextureSampler : register(s0)
{
    Texture = (Texture);
    MinFilter = Anisotropic; // High quality texture filtering
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
    MaxAnisotropy = 4;
};

// Material Properties
float3 DiffuseColor;
float3 SpecularColor;
float SpecularPower; // Controls glossiness (higher = sharper highlight)
float3 AmbientColor;

// Scene Lighting
float3 LightDirection; // Direction TO the light source (usually inverted in C#)
float3 LightColor;
float3 CameraPosition; // Required for specular/rim calculations

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
    float4 WorldPos : TEXCOORD1;
	float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

// --- PIXEL SHADER ---
float4 MainPS(VertexShaderOutput input) : COLOR
{
    // 1. Normalize Vectors
    float3 N = normalize(input.Normal);
    float3 L = normalize(-LightDirection);       // Light vector
    float3 V = normalize(CameraPosition - input.WorldPos.xyz); // View vector (Camera to Pixel)
    float3 H = normalize(L + V);                 // Half vector (for Blinn-Phong)

    // 2. Texture Sample
    float4 texColor = tex2D(TextureSampler, input.TexCoord);

    // 3. Ambient Light (Base lighting)
    // Default fallback if AmbientColor isn't set: float3(0.2, 0.2, 0.2)
    float3 ambient = (length(AmbientColor) > 0 ? AmbientColor : float3(0.2, 0.2, 0.2)) * texColor.rgb;

    // 4. Diffuse Light (Lambertian - Standard matte shading)
    float NdotL = max(dot(N, L), 0.0);
    float3 diffuse = NdotL * LightColor * DiffuseColor * texColor.rgb;

    // 5. Specular Light (Blinn-Phong - Realistic highlights)
    float3 specular = float3(0, 0, 0);
    if (NdotL > 0.0)
    {
        float NdotH = max(dot(N, H), 0.0);
        // Default SpecularPower to 16 if 0
        float power = SpecularPower > 0 ? SpecularPower : 32.0; 
        float specIntensity = pow(NdotH, power);
        
        // Use a default specular color (white) if not provided
        float3 specCol = length(SpecularColor) > 0 ? SpecularColor : float3(1, 1, 1);
        specular = specIntensity * specCol * LightColor;
    }

    // 6. Rim Lighting (Cinematic edge glow)
    // Calculates how perpendicular the surface is to the camera
    float rimFactor = 1.0 - max(dot(N, V), 0.0);
    rimFactor = pow(rimFactor, 3.0); // Sharpen the rim
    // Only show rim light on the illuminated side (optional stylistic choice)
    float3 rim = rimFactor * float3(0.8, 0.9, 1.0) * 0.5 * NdotL; 

    // 7. Combine All Components
    float3 finalColor = ambient + diffuse + specular + rim;

    // Optional: Simple Tone Mapping (reinhard) for HDR-like feel
    // finalColor = finalColor / (1.0 + finalColor);

	return float4(finalColor, texColor.a);
}

technique BasicColorDrawing
{
	pass P0
	{
        // Vertex shader is usually compiled separately or passed in
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};";
    }
}