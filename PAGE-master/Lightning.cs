using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OmniEngine
{
    /// <summary>
    /// Manages the Global Lighting setup (Ambient + 3 Directional Lights).
    /// Acts as a wrapper for BasicEffect's lighting capabilities.
    /// </summary>
    public static class Lighting
    {
        public static Color AmbientLight
        {
            get => new Color(Shader.Standard.AmbientLightColor);
            set => Shader.Standard.AmbientLightColor = value.ToVector3();
        }

        // Light 0 is typically the "Sun" or Main Key Light
        public static void SetSun(Vector3 direction, Color diffuseColor, Color specularColor)
        {
            Shader.Standard.DirectionalLight0.Enabled = true;
            Shader.Standard.DirectionalLight0.Direction = Vector3.Normalize(direction);
            Shader.Standard.DirectionalLight0.DiffuseColor = diffuseColor.ToVector3();
            Shader.Standard.DirectionalLight0.SpecularColor = specularColor.ToVector3();
        }

        // Light 1 is typically a Fill Light (so shadows aren't pitch black)
        public static void SetFillLight(Vector3 direction, Color color)
        {
            Shader.Standard.DirectionalLight1.Enabled = true;
            Shader.Standard.DirectionalLight1.Direction = Vector3.Normalize(direction);
            Shader.Standard.DirectionalLight1.DiffuseColor = color.ToVector3();
            Shader.Standard.DirectionalLight1.SpecularColor = Vector3.Zero; // Fill lights usually aren't shiny
        }

        // Light 2 is typically a Back Light / Rim Light (for depth)
        public static void SetRimLight(Vector3 direction, Color color)
        {
            Shader.Standard.DirectionalLight2.Enabled = true;
            Shader.Standard.DirectionalLight2.Direction = Vector3.Normalize(direction);
            Shader.Standard.DirectionalLight2.DiffuseColor = color.ToVector3();
            Shader.Standard.DirectionalLight2.SpecularColor = color.ToVector3();
        }

        public static void DisableSecondaryLights()
        {
            Shader.Standard.DirectionalLight1.Enabled = false;
            Shader.Standard.DirectionalLight2.Enabled = false;
        }
    }
}