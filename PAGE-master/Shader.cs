using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OmniEngine
{
    /// <summary>
    /// Handles Shader management, Environment settings, and Global Graphics State.
    /// </summary>
    public static class Shader
    {
        // The global standard shader instance (BasicEffect)
        public static BasicEffect Standard;

        // Global Environment Settings
        public static Color SkyColor;

        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            Standard = new BasicEffect(graphicsDevice);

            // 1. Configure Standard Lighting Defaults
            Standard.LightingEnabled = true;
            Standard.EnableDefaultLighting(); // Sets up a basic Key/Fill/Back setup
            Standard.PreferPerPixelLighting = true; // High Quality Lighting

            // 2. Initialize Environment Defaults (Fog & Sky)
            // We default to "Cornflower Blue" - the classic XNA sky color
            SetEnvironment(false, Color.CornflowerBlue, Color.CornflowerBlue, 10f, 100f);
        }

        /// <summary>
        /// Configures the scene's atmospheric fog and background sky color.
        /// </summary>
        public static void SetEnvironment(bool fogEnabled, Color skyColor, Color fogColor, float startDistance, float endDistance)
        {
            SkyColor = skyColor;

            Standard.FogEnabled = fogEnabled;
            Standard.FogColor = fogColor.ToVector3();
            Standard.FogStart = startDistance;
            Standard.FogEnd = endDistance;
        }

        /// <summary>
        /// Updates the View/Projection matrices for the shader.
        /// Should be called every frame by the active camera.
        /// </summary>
        public static void UpdateMatrices(Matrix view, Matrix projection)
        {
            Standard.View = view;
            Standard.Projection = projection;
        }

        /// <summary>
        /// Applies object-specific world transformations.
        /// </summary>
        public static void ApplyObject(Matrix world, Color color, Texture2D texture = null)
        {
            Standard.World = world;
            Standard.DiffuseColor = color.ToVector3();

            if (texture != null)
            {
                Standard.TextureEnabled = true;
                Standard.Texture = texture;
            }
            else
            {
                Standard.TextureEnabled = false;
            }

            // Apply the pass for drawing
            Standard.CurrentTechnique.Passes[0].Apply();
        }
    }
}