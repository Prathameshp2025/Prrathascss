using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Added for background tasks
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OmniEngine
{
    /// <summary>
    /// OMNI ENGINE 4.0 - ADVANCED (Physics, Raycasting, Textures)
    /// </summary>
    public class OmniGame : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        public static Scene CurrentScene;
        public static Texture2D Pixel;
        public static Texture2D Circle;
        public static Texture2D Checkerboard;

        public static SpriteFont UiFont;

        public static ModelData CubeModel;
        public static VertexPositionColor[] GridLines;

        public static bool IsEditorMode = true;
        public static bool IsPlaying = false;
        public static bool IsPaused = false;
        public static bool ShowColliders = true;

        public static Viewport EditorViewport;

        public OmniGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _graphics.PreferredBackBufferWidth = 1600;
            _graphics.PreferredBackBufferHeight = 900;
            _graphics.SynchronizeWithVerticalRetrace = true;
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;
            IsFixedTimeStep = true;
            Window.AllowUserResizing = true;
            Window.Title = "OmniEngine 4.0 Advanced";
        }

        protected override void Initialize()
        {
            base.Initialize();
            Input.Initialize();
            OmniEffects.Initialize(GraphicsDevice);

            Window.FileDrop += (s, e) =>
            {
                foreach (var file in e.Files)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    if (!Gui.ProjectFiles.Contains(fileName)) Gui.ProjectFiles.Add(fileName);
                }
            };

            CurrentScene = new Scene();
            SceneBuilder.BuildPhysicsScene(CurrentScene);

            Debug.Log("OmniEngine 4.0 Initialized.");
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            Pixel = new Texture2D(GraphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });

            PixelFont.Initialize();

            int radius = 32;
            int diameter = radius * 2;
            Circle = new Texture2D(GraphicsDevice, diameter, diameter);
            Color[] data = new Color[diameter * diameter];
            Vector2 center = new Vector2(radius);
            for (int i = 0; i < data.Length; i++)
            {
                float dist = Vector2.Distance(new Vector2(i % diameter, i / diameter), center);
                float alpha = 1f - MathHelper.Clamp(dist / radius, 0f, 1f);
                data[i] = Color.White * (alpha * alpha);
            }
            Circle.SetData(data);

            Checkerboard = new Texture2D(GraphicsDevice, 64, 64);
            Color[] checkData = new Color[64 * 64];
            for (int i = 0; i < checkData.Length; i++)
            {
                int x = (i % 64) / 8;
                int y = (i / 64) / 8;
                checkData[i] = ((x + y) % 2 == 0) ? Color.LightGray : Color.DarkGray;
            }
            Checkerboard.SetData(checkData);

            CubeModel = PrimitiveFactory.CreateCube(GraphicsDevice);
            GridLines = PrimitiveFactory.CreateGrid(20, 1f);

            try { UiFont = Content.Load<SpriteFont>("Font"); } catch { }
        }

        protected override void Update(GameTime gameTime)
        {
            // --- UPDATED EXIT LOGIC ---
            // Forcefully kills the process to prevent "File Locked" errors in Visual Studio
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit();
                Environment.Exit(0); // Ensure all threads (including build tasks) die
            }

            Input.Update();
            Time.DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Time.TotalTime = (float)gameTime.TotalGameTime.TotalSeconds;

            if (Input.GetKeyDown(Keys.F1)) IsEditorMode = !IsEditorMode;
            if (Input.GetKeyDown(Keys.F2)) ShowColliders = !ShowColliders;

            Viewport original = GraphicsDevice.Viewport;
            EditorViewport = IsEditorMode ? new Viewport(300, 50, original.Width - 600, original.Height - 300) : original;

            if (IsEditorMode && Input.GetMouseButtonDown(0))
            {
                if (EditorViewport.Bounds.Contains(Input.MousePosition))
                {
                    GameObject picked = Physics.Raycast(
                        Input.MousePosition,
                        CurrentScene.MainCamera,
                        EditorViewport,
                        CurrentScene.GameObjects
                    );

                    if (picked != null)
                    {
                        Gui.SelectedObject = picked;
                        Debug.Log($"Selected: {picked.Name}");
                    }
                }
            }

            if (IsPlaying && !IsPaused)
            {
                CurrentScene?.Update();
                Physics.Simulate(CurrentScene.GameObjects);
            }
            else
            {
                CurrentScene?.EditorUpdate();
            }

            Window.Title = $"OmniEngine 4.0 {(IsPlaying ? "[PLAY]" : "[EDITOR]")} | FPS: {Math.Round(1 / Time.DeltaTime)}";

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, OmniEffects.SkyColor, 1.0f, 0);

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            Viewport original = GraphicsDevice.Viewport;
            GraphicsDevice.Viewport = EditorViewport;

            if (IsEditorMode && !IsPlaying)
            {
                OmniEffects.Standard.World = Matrix.Identity;
                OmniEffects.Standard.VertexColorEnabled = true;
                OmniEffects.Standard.TextureEnabled = false;
                OmniEffects.Standard.LightingEnabled = false;
                OmniEffects.Standard.View = CurrentScene.MainCamera.View;
                OmniEffects.Standard.Projection = CurrentScene.MainCamera.Projection;
                OmniEffects.Standard.CurrentTechnique.Passes[0].Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, GridLines, 0, GridLines.Length / 2);
            }

            CurrentScene?.Draw3D(GraphicsDevice);

            if (ShowColliders && IsEditorMode)
            {
                foreach (var go in CurrentScene.GameObjects)
                {
                    var col = go.GetComponent<BoxCollider>();
                    if (col != null) col.DrawDebug(GraphicsDevice);
                }
            }

            if (IsEditorMode && Gui.SelectedObject != null)
            {
                DrawGizmos(GraphicsDevice, Gui.SelectedObject.Position);
            }

            GraphicsDevice.Viewport = original;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            CurrentScene?.Draw2D(_spriteBatch);

            if (IsEditorMode) Gui.DrawEditorLayout(_spriteBatch, GraphicsDevice.Viewport);

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawGizmos(GraphicsDevice gd, Vector3 pos)
        {
            OmniEffects.Standard.World = Matrix.Identity;
            OmniEffects.Standard.VertexColorEnabled = true;
            OmniEffects.Standard.LightingEnabled = false;
            OmniEffects.Standard.TextureEnabled = false;
            gd.DepthStencilState = DepthStencilState.None;
            OmniEffects.Standard.CurrentTechnique.Passes[0].Apply();

            float scale = 2.0f;
            VertexPositionColor[] axis = new VertexPositionColor[]
            {
                new VertexPositionColor(pos, Color.Red), new VertexPositionColor(pos + Vector3.Right * scale, Color.Red),
                new VertexPositionColor(pos, Color.Green), new VertexPositionColor(pos + Vector3.Up * scale, Color.Green),
                new VertexPositionColor(pos, Color.Blue), new VertexPositionColor(pos + Vector3.Backward * scale, Color.Blue),
            };
            gd.DrawUserPrimitives(PrimitiveType.LineList, axis, 0, 3);
            gd.DepthStencilState = DepthStencilState.Default;
        }
    }

    public static class OmniEffects
    {
        public static BasicEffect Standard;
        public static Color SkyColor = new Color(40, 40, 50);

        public static void Initialize(GraphicsDevice gd)
        {
            Standard = new BasicEffect(gd);
            Standard.VertexColorEnabled = true;
            Standard.LightingEnabled = true;
            Standard.TextureEnabled = true;
            Standard.PreferPerPixelLighting = true;
            Standard.EnableDefaultLighting();
            Standard.SpecularColor = new Vector3(0.2f);
            Standard.SpecularPower = 16;
        }
    }

    public static class Physics
    {
        public static void Simulate(List<GameObject> objects)
        {
            foreach (var obj in objects)
            {
                var col = obj.GetComponent<BoxCollider>();
                if (col == null || col.IsStatic) continue;

                obj.Position.Y -= 9.8f * Time.DeltaTime;
                if (obj.Position.Y < 0.5f) obj.Position.Y = 0.5f;

                BoundingBox boxA = col.GetWorldBounds();
                foreach (var other in objects)
                {
                    if (obj == other) continue;
                    var colB = other.GetComponent<BoxCollider>();
                    if (colB == null) continue;

                    if (boxA.Intersects(colB.GetWorldBounds())) obj.Position.Y += 0.1f;
                }
            }
        }

        public static GameObject Raycast(Point mousePos, Camera3D cam, Viewport vp, List<GameObject> objects)
        {
            Vector3 nearPoint = vp.Unproject(new Vector3(mousePos.X, mousePos.Y, 0), cam.Projection, cam.View, Matrix.Identity);
            Vector3 farPoint = vp.Unproject(new Vector3(mousePos.X, mousePos.Y, 1), cam.Projection, cam.View, Matrix.Identity);
            Vector3 direction = Vector3.Normalize(farPoint - nearPoint);
            Ray ray = new Ray(nearPoint, direction);

            float closestDist = float.MaxValue;
            GameObject closestObj = null;

            foreach (var obj in objects)
            {
                BoundingBox bounds;
                var col = obj.GetComponent<BoxCollider>();
                var mr = obj.GetComponent<MeshRenderer>();

                if (col != null) bounds = col.GetWorldBounds();
                else if (mr != null) bounds = new BoundingBox(obj.Position - Vector3.One / 2, obj.Position + Vector3.One / 2);
                else continue;

                float? dist = ray.Intersects(bounds);
                if (dist.HasValue && dist.Value < closestDist)
                {
                    closestDist = dist.Value;
                    closestObj = obj;
                }
            }
            return closestObj;
        }
    }

    public static class Debug
    {
        public struct LogEntry { public string Text; public Color Color; public string Time; }
        public static List<LogEntry> Logs = new List<LogEntry>();
        public static void Log(string msg) => Add(msg, Color.White);
        public static void LogWarning(string msg) => Add(msg, Color.Yellow);
        public static void LogError(string msg) => Add(msg, Color.Red);
        private static void Add(string msg, Color c)
        {
            Logs.Add(new LogEntry { Text = msg, Color = c, Time = DateTime.Now.ToString("HH:mm:ss") });
            if (Logs.Count > 50) Logs.RemoveAt(0);
        }
    }

    public static class Gui
    {
        public static Color ColorBackground = new Color(56, 56, 56);
        public static Color ColorPanel = new Color(40, 40, 40);
        public static Color ColorHeader = new Color(30, 30, 30);
        public static Color ColorHighlight = new Color(44, 93, 135);
        public static Color ColorHover = new Color(60, 60, 60);
        public static Color ColorText = new Color(220, 220, 220);
        public static Color ColorField = new Color(20, 20, 20);
        public static Color ColorPlay = new Color(100, 200, 100);

        public static GameObject SelectedObject;
        public static List<string> ProjectFiles = new List<string>();
        private static int _dragId = -1;
        public static bool ShowConsole = false;
        private static bool _isExporting = false; // Track export state

        public static void DrawEditorLayout(SpriteBatch sb, Viewport vp)
        {
            Rectangle hierarchyRect = new Rectangle(0, 50, 300, vp.Height - 50);
            DrawPanel(sb, hierarchyRect, "Hierarchy");
            DrawHierarchyContents(sb, hierarchyRect);

            Rectangle inspectorRect = new Rectangle(vp.Width - 300, 50, 300, vp.Height - 50);
            DrawPanel(sb, inspectorRect, "Inspector");
            DrawInspectorContents(sb, inspectorRect);

            Rectangle bottomRect = new Rectangle(300, vp.Height - 250, vp.Width - 600, 250);
            sb.Draw(OmniGame.Pixel, new Rectangle(300, bottomRect.Y, bottomRect.Width, 25), ColorHeader);
            if (DrawButton(sb, new Rectangle(300, bottomRect.Y, 100, 25), "Project", !ShowConsole)) ShowConsole = false;
            if (DrawButton(sb, new Rectangle(405, bottomRect.Y, 100, 25), "Console", ShowConsole)) ShowConsole = true;
            Rectangle contentRect = new Rectangle(300, bottomRect.Y + 25, bottomRect.Width, bottomRect.Height - 25);
            sb.Draw(OmniGame.Pixel, contentRect, ColorPanel);
            if (ShowConsole) DrawConsoleContents(sb, contentRect); else DrawProjectContents(sb, contentRect);

            DrawToolbar(sb, new Rectangle(0, 0, vp.Width, 50));
            if (!Input.GetMouseButton(0)) _dragId = -1;
        }

        private static void DrawToolbar(SpriteBatch sb, Rectangle rect)
        {
            sb.Draw(OmniGame.Pixel, rect, Color.Black);

            // --- EXPORT BUTTONS ---
            string pcText = _isExporting ? "BUSY" : "PC BUILD";
            string androidText = _isExporting ? "BUSY" : "ANDROID";
            Color btnColor = _isExporting ? Color.Gray : new Color(50, 150, 50);

            // PC EXPORT
            if (DrawButton(sb, new Rectangle(10, 10, 80, 30), pcText, false, btnColor) && !_isExporting)
            {
                _isExporting = true;
                Task.Run(() => PerformExportPC());
            }

            // ANDROID EXPORT
            if (DrawButton(sb, new Rectangle(95, 10, 80, 30), androidText, false, new Color(50, 100, 150)) && !_isExporting)
            {
                _isExporting = true;
                Task.Run(() => PerformExportAndroid());
            }

            int cx = rect.Width / 2;
            Color playColor = OmniGame.IsPlaying ? ColorPlay : Color.Gray;
            if (DrawButton(sb, new Rectangle(cx - 60, 10, 50, 30), "PLAY", OmniGame.IsPlaying, playColor))
            {
                OmniGame.IsPlaying = !OmniGame.IsPlaying;
                OmniGame.IsPaused = false;
            }
            Color pauseColor = OmniGame.IsPaused ? Color.Yellow : Color.Gray;
            if (DrawButton(sb, new Rectangle(cx + 10, 10, 50, 30), "PAUSE", OmniGame.IsPaused, pauseColor))
            {
                if (OmniGame.IsPlaying) OmniGame.IsPaused = !OmniGame.IsPaused;
            }
        }

        private static void PerformExportPC()
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string path = Path.Combine(docs, "PAGE", "PC_Build");
                Directory.CreateDirectory(path);

                Debug.Log("Generating PC Code...");
                File.WriteAllText(Path.Combine(path, "Game1.cs"), ExportTemplate.Generate(OmniGame.CurrentScene));

                string csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RollForward>Major</RollForward>
    <PublishReadyToRun>false</PublishReadyToRun>
    <TieredCompilation>false</TieredCompilation>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <IncludeNativeLibrariesForSelfExtract>false</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""MonoGame.Framework.DesktopGL"" Version=""3.8.1.303"" />
    <PackageReference Include=""MonoGame.Content.Builder.Task"" Version=""3.8.1.303"" />
  </ItemGroup>
</Project>";
                File.WriteAllText(Path.Combine(path, "ExportedGame.csproj"), csproj);

                string batContent = @"@echo off
echo Killing any previous instances...
taskkill /IM ExportedGame.exe /F 2>nul
echo Building OmniGame Export (PC)...
dotnet publish -c Release -r win-x64 -o ./Build
if %errorlevel% neq 0 (
    echo BUILD FAILED!
    pause
) else (
    echo BUILD SUCCESS!
    start ./Build/ExportedGame.exe
    pause
)";
                File.WriteAllText(Path.Combine(path, "BuildPC.bat"), batContent);

                LaunchBuilder(path, "BuildPC.bat");
            }
            catch (Exception e) { Debug.LogError($"Export Error: {e.Message}"); }
            finally { _isExporting = false; }
        }

        private static void PerformExportAndroid()
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string path = Path.Combine(docs, "PAGE", "Android_Build");
                Directory.CreateDirectory(path);

                Debug.Log("Generating Android Code...");

                // 1. Generate Game Code (Modified to hide Program.Main)
                string gameCode = ExportTemplate.Generate(OmniGame.CurrentScene);
                // Strip the PC Entry point because Android uses Activity1
                gameCode = gameCode.Replace("public static class Program", "/* Program hidden for Android */ internal static class Program_Hidden");
                File.WriteAllText(Path.Combine(path, "Game1.cs"), gameCode);

                // 2. Generate Activity1.cs (Android Entry Point)
                string activityCode = @"using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace ExportedGame
{
    [Activity(
        Label = ""@string/app_name"",
        MainLauncher = true,
        Icon = ""@drawable/icon"",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.FullUser,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize
    )]
    public class Activity1 : AndroidGameActivity
    {
        private Game1 _game;
        private View _view;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            _game = new Game1();
            _view = _game.Services.GetService(typeof(View)) as View;
            SetContentView(_view);
            _game.Run();
        }
    }
}";
                File.WriteAllText(Path.Combine(path, "Activity1.cs"), activityCode);

                // 3. Generate Android CSProj with APK Configuration, EOL Fix & Java Support
                string csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0-android</TargetFramework>
    <SupportedOSPlatformVersion>21.0</SupportedOSPlatformVersion>
    <OutputType>Exe</OutputType>
    <Fullscreen>true</Fullscreen>
    <Orientation>Landscape</Orientation>
    <ApplicationId>com.OmniEngine.ExportedGame</ApplicationId>
    <ApplicationVersion>1</ApplicationVersion>
    <AndroidPackageFormat>apk</AndroidPackageFormat>
    <AndroidUseSharedRuntime>false</AndroidUseSharedRuntime>
    <!-- CRITICAL FIX: Suppress EOL error for newer SDKs -->
    <CheckEolTargetFramework>false</CheckEolTargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""MonoGame.Framework.Android"" Version=""3.8.1.303"" />
    <PackageReference Include=""MonoGame.Content.Builder.Task"" Version=""3.8.1.303"" />
  </ItemGroup>
</Project>";
                File.WriteAllText(Path.Combine(path, "ExportedGame.csproj"), csproj);

                // 4. Generate Android Build Script (Supports JDK 21 detection)
                string batContent = @"@echo off
echo Checking for Android Workload...
dotnet workload install android

echo Checking JDK...
:: Force detection of JDK 21 if JAVA_HOME is missing or incorrect
if ""%JAVA_HOME%""=="""" (
    if exist ""C:\Program Files\Java\jdk-21"" (
        set ""JAVA_HOME=C:\Program Files\Java\jdk-21""
        echo [Omni] Found JDK 21: Setting JAVA_HOME automatically.
    ) else (
        echo [Omni] Warning: JAVA_HOME not set and default JDK 21 path not found.
    )
)
echo Using JAVA_HOME: %JAVA_HOME%

echo Building OmniGame Export (Android APK)...
dotnet build -f net8.0-android -c Release -t:SignAndroidPackage -p:CheckEolTargetFramework=false
if %errorlevel% neq 0 (
    echo.
    echo BUILD FAILED! 
    echo 1. Ensure you have the Android SDK installed via Visual Studio Installer.
    echo 2. Ensure JAVA_HOME points to your JDK 21 installation.
    pause
) else (
    echo BUILD SUCCESS!
    echo Opening build folder containing APK...
    explorer ""bin\Release\net8.0-android""
    pause
)";
                File.WriteAllText(Path.Combine(path, "BuildAndroid.bat"), batContent);

                LaunchBuilder(path, "BuildAndroid.bat");
            }
            catch (Exception e) { Debug.LogError($"Android Error: {e.Message}"); }
            finally { _isExporting = false; }
        }

        private static void LaunchBuilder(string path, string batName)
        {
            Debug.Log("Launching Builder...");
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Path.Combine(path, batName),
                WorkingDirectory = path,
                UseShellExecute = true,
                CreateNoWindow = false
            };
            Process.Start(psi);
        }

        // ... [Rest of the GUI methods remain unchanged for brevity, see previous context] ...
        private static void DrawHierarchyContents(SpriteBatch sb, Rectangle panelRect)
        {
            int y = panelRect.Y + 35;
            foreach (var go in OmniGame.CurrentScene.GameObjects)
            {
                Rectangle itemRect = new Rectangle(panelRect.X + 5, y, panelRect.Width - 10, 30);
                if (itemRect.Contains(Input.MousePosition))
                {
                    sb.Draw(OmniGame.Pixel, itemRect, ColorHover);
                    if (Input.GetMouseButtonDown(0)) SelectedObject = go;
                }
                if (SelectedObject == go) sb.Draw(OmniGame.Pixel, itemRect, ColorHighlight);
                DrawString(sb, go.Name, new Vector2(itemRect.X + 10, itemRect.Y + 8), ColorText);
                y += 30;
            }
        }

        private static void DrawInspectorContents(SpriteBatch sb, Rectangle panelRect)
        {
            if (SelectedObject == null) return;
            int x = panelRect.X + 10, y = panelRect.Y + 35, width = panelRect.Width - 20;

            DrawString(sb, "Name", new Vector2(x, y), ColorText);
            sb.Draw(OmniGame.Pixel, new Rectangle(x + 60, y, width - 60, 20), ColorField);
            DrawString(sb, SelectedObject.Name, new Vector2(x + 65, y + 4), ColorText);
            y += 35;

            DrawHeader(sb, x, ref y, width, "Transform");
            DrawVector3Control(sb, x, ref y, width, "Position", ref SelectedObject.Position);
            DrawVector3Control(sb, x, ref y, width, "Rotation", ref SelectedObject.Rotation);
            DrawVector3Control(sb, x, ref y, width, "Scale", ref SelectedObject.Scale);
            y += 20;

            foreach (var comp in SelectedObject.GetComponents())
            {
                string name = comp.GetType().Name;
                DrawHeader(sb, x, ref y, width, name);

                if (comp is BoxCollider box)
                {
                    DrawVector3Control(sb, x, ref y, width, "Size", ref box.Size);
                    if (DrawButton(sb, new Rectangle(x, y, width, 25), $"Static: {box.IsStatic}", box.IsStatic)) box.IsStatic = !box.IsStatic;
                    y += 30;
                }
                if (comp is PlayerController2D pc)
                {
                    DrawString(sb, $"Speed: {pc.MoveSpeed}", new Vector2(x, y), Color.Gray);
                    y += 25;
                }
            }

            y += 10;
            if (DrawButton(sb, new Rectangle(x, y, width, 30), "Add BoxCollider", false))
            {
                if (SelectedObject.GetComponent<BoxCollider>() == null)
                    SelectedObject.AddComponent<BoxCollider>();
            }
        }

        private static void DrawConsoleContents(SpriteBatch sb, Rectangle panelRect)
        {
            int x = panelRect.X + 10, y = panelRect.Y + 10;
            for (int i = 0; i < Debug.Logs.Count; i++)
            {
                var log = Debug.Logs[Debug.Logs.Count - 1 - i];
                if (y > panelRect.Bottom - 20) break;
                DrawString(sb, $"[{log.Time}] {log.Text}", new Vector2(x, y), log.Color);
                y += 20;
            }
        }

        private static void DrawProjectContents(SpriteBatch sb, Rectangle panelRect)
        {
            int x = panelRect.X + 10, y = panelRect.Y + 10;
            if (ProjectFiles.Count == 0) DrawString(sb, "Drag & Drop files...", new Vector2(x, y), Color.Gray);
            foreach (var file in ProjectFiles)
            {
                string display = file.Length > 20 ? file.Substring(0, 17) + "..." : file;
                DrawString(sb, display, new Vector2(x, y), ColorText);
                y += 25;
            }
        }

        private static bool DrawButton(SpriteBatch sb, Rectangle rect, string text, bool active, Color? overrideColor = null)
        {
            Color c = overrideColor ?? (active ? ColorHighlight : ColorPanel);
            if (rect.Contains(Input.MousePosition)) c = ColorHover;
            sb.Draw(OmniGame.Pixel, rect, c);
            sb.Draw(OmniGame.Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Color.Black);
            sb.Draw(OmniGame.Pixel, new Rectangle(rect.X, rect.Bottom, rect.Width, 1), Color.Black);
            sb.Draw(OmniGame.Pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Color.Black);
            sb.Draw(OmniGame.Pixel, new Rectangle(rect.Right, rect.Y, 1, rect.Height), Color.Black);
            DrawString(sb, text, new Vector2(rect.X + 10, rect.Y + 8), Color.White);
            return rect.Contains(Input.MousePosition) && Input.GetMouseButtonDown(0);
        }

        private static void DrawVector3Control(SpriteBatch sb, int x, ref int yRef, int w, string label, ref Vector3 value)
        {
            int fieldW = (w - 60) / 3;
            DrawString(sb, label, new Vector2(x, yRef), ColorText);
            yRef += 20;
            value.X = DrawFloatDrag(sb, x, yRef, fieldW - 5, value.X, Color.Red, 100 + yRef);
            value.Y = DrawFloatDrag(sb, x + fieldW, yRef, fieldW - 5, value.Y, Color.Green, 200 + yRef);
            value.Z = DrawFloatDrag(sb, x + fieldW * 2, yRef, fieldW - 5, value.Z, Color.Blue, 300 + yRef);
            yRef += 30;
        }

        private static float DrawFloatDrag(SpriteBatch sb, int x, int y, int w, float val, Color axisColor, int controlId)
        {
            Rectangle rect = new Rectangle(x, y, w, 20);
            if (rect.Contains(Input.MousePosition) && Input.GetMouseButtonDown(0)) _dragId = controlId;
            if (_dragId == controlId) val += Input.MouseDelta.X * 0.05f;
            sb.Draw(OmniGame.Pixel, rect, ColorField);
            sb.Draw(OmniGame.Pixel, new Rectangle(x, y + 18, w, 2), axisColor);
            DrawString(sb, val.ToString("0.0"), new Vector2(x + 4, y + 4), ColorText);
            return val;
        }

        private static void DrawHeader(SpriteBatch sb, int x, ref int y, int w, string title)
        {
            sb.Draw(OmniGame.Pixel, new Rectangle(x, y + 20, w, 1), Color.Gray);
            DrawString(sb, title, new Vector2(x, y), Color.White);
            y += 25;
        }

        private static void DrawPanel(SpriteBatch sb, Rectangle rect, string title)
        {
            sb.Draw(OmniGame.Pixel, rect, ColorBackground);
            sb.Draw(OmniGame.Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 25), ColorHeader);
            DrawString(sb, title, new Vector2(rect.X + 8, rect.Y + 6), Color.White);
        }

        public static void DrawString(SpriteBatch sb, string text, Vector2 pos, Color color)
        {
            if (OmniGame.UiFont != null) sb.DrawString(OmniGame.UiFont, text, pos, color);
            else PixelFont.Draw(sb, text, pos, color);
        }
    }

    // =========================================================
    // COMPONENTS & SCENE
    // =========================================================

    public class Scene
    {
        public List<GameObject> GameObjects = new List<GameObject>();
        public Camera3D MainCamera = new Camera3D { Position = new Vector3(0, 5, 10) };

        public void Add(GameObject go) { GameObjects.Add(go); go.Scene = this; go.Awake(); }
        public void Update() { MainCamera.Update(); foreach (var go in GameObjects) go.Update(); }
        public void EditorUpdate() { MainCamera.Update(); }
        public void Draw3D(GraphicsDevice device)
        {
            OmniEffects.Standard.View = MainCamera.View;
            OmniEffects.Standard.Projection = MainCamera.Projection;
            foreach (var go in GameObjects) go.Draw3D(device);
        }
        public void Draw2D(SpriteBatch sb) { foreach (var go in GameObjects) go.Draw2D(sb); }
    }

    public class GameObject
    {
        public string Name;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.One;
        public Scene Scene;
        private List<Component> _components = new List<Component>();

        public GameObject(string name) { Name = name; }
        public T AddComponent<T>() where T : Component, new()
        {
            T comp = new T(); comp.GameObject = this; _components.Add(comp); comp.Awake(); return comp;
        }
        public T GetComponent<T>() where T : Component => _components.OfType<T>().FirstOrDefault();
        public List<Component> GetComponents() => _components;
        public void Awake() { foreach (var c in _components) c.Awake(); }
        public void Update() { foreach (var c in _components) c.Update(); }
        public void Draw3D(GraphicsDevice gd) { foreach (var c in _components) c.Draw3D(gd); }
        public void Draw2D(SpriteBatch sb) { foreach (var c in _components) c.Draw2D(sb); }
    }

    public abstract class Component
    {
        public GameObject GameObject;
        public virtual void Awake() { }
        public virtual void Update() { }
        public virtual void Draw3D(GraphicsDevice gd) { }
        public virtual void Draw2D(SpriteBatch sb) { }
    }

    // --- NEW COMPONENT: BoxCollider ---
    public class BoxCollider : Component
    {
        public Vector3 Size = Vector3.One;
        public bool IsStatic = false;

        public BoundingBox GetWorldBounds()
        {
            Vector3 min = GameObject.Position - (Size * GameObject.Scale * 0.5f);
            Vector3 max = GameObject.Position + (Size * GameObject.Scale * 0.5f);
            return new BoundingBox(min, max);
        }

        public void DrawDebug(GraphicsDevice gd)
        {
            var bounds = GetWorldBounds();
            Vector3[] corners = bounds.GetCorners();
            VertexPositionColor[] lines = new VertexPositionColor[24];

            // Convert to lines
            int[] indices = {
                0,1, 1,2, 2,3, 3,0, // Top
                4,5, 5,6, 6,7, 7,4, // Bottom
                0,4, 1,5, 2,6, 3,7  // Sides
            };

            for (int i = 0; i < indices.Length; i++) lines[i] = new VertexPositionColor(corners[indices[i]], Color.LimeGreen);

            OmniEffects.Standard.World = Matrix.Identity;
            OmniEffects.Standard.VertexColorEnabled = true;
            OmniEffects.Standard.TextureEnabled = false;
            OmniEffects.Standard.LightingEnabled = false;
            OmniEffects.Standard.CurrentTechnique.Passes[0].Apply();

            gd.DrawUserPrimitives(PrimitiveType.LineList, lines, 0, 12);
        }
    }

    public class MeshRenderer : Component
    {
        public ModelData Model;
        public Color Color = Color.White;
        public Texture2D Texture;

        public override void Draw3D(GraphicsDevice gd)
        {
            if (Model == null) return;
            Matrix world = Matrix.CreateScale(GameObject.Scale) *
                           Matrix.CreateRotationX(GameObject.Rotation.X) *
                           Matrix.CreateRotationY(GameObject.Rotation.Y) *
                           Matrix.CreateRotationZ(GameObject.Rotation.Z) *
                           Matrix.CreateTranslation(GameObject.Position);

            OmniEffects.Standard.World = world;
            OmniEffects.Standard.DiffuseColor = Color.ToVector3();
            OmniEffects.Standard.TextureEnabled = (Texture != null);
            OmniEffects.Standard.Texture = Texture;
            OmniEffects.Standard.LightingEnabled = true;
            OmniEffects.Standard.VertexColorEnabled = false; // Fix: Disable vertex color for MeshRenderer

            OmniEffects.Standard.CurrentTechnique.Passes[0].Apply();

            gd.SetVertexBuffer(Model.VertexBuffer);
            gd.Indices = Model.IndexBuffer;
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, Model.PrimitiveCount);
        }
    }

    public class Camera3D
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }

        public void Update()
        {
            if (Input.GetMouseButton(1))
            {
                Vector2 delta = Input.MouseDelta;
                Rotation.Y -= delta.X * 0.005f;
                Rotation.X -= delta.Y * 0.005f;
                float speed = 10f * Time.DeltaTime * (Input.GetKey(Keys.LeftShift) ? 2f : 1f);
                Matrix rot = Matrix.CreateRotationX(Rotation.X) * Matrix.CreateRotationY(Rotation.Y);
                Vector3 fwd = Vector3.Transform(Vector3.Forward, rot);
                Vector3 right = Vector3.Transform(Vector3.Right, rot);

                if (Input.GetKey(Keys.W)) Position += fwd * speed;
                if (Input.GetKey(Keys.S)) Position -= fwd * speed;
                if (Input.GetKey(Keys.A)) Position -= right * speed;
                if (Input.GetKey(Keys.D)) Position += right * speed;
                if (Input.GetKey(Keys.Q)) Position += Vector3.Down * speed;
                if (Input.GetKey(Keys.E)) Position += Vector3.Up * speed;
            }
            Vector3 target = Position + Vector3.Transform(Vector3.Forward, Matrix.CreateRotationX(Rotation.X) * Matrix.CreateRotationY(Rotation.Y));
            View = Matrix.CreateLookAt(Position, target, Vector3.Up);
            float aspect = OmniGame.EditorViewport.AspectRatio;
            Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, 0.1f, 1000f);
        }
    }

    public class PlayerController2D : Component
    {
        public float MoveSpeed = 5f;
        public override void Update()
        {
            Vector3 m = Vector3.Zero;
            if (Input.GetKey(Keys.Up)) m.Z -= 1;
            if (Input.GetKey(Keys.Down)) m.Z += 1;
            if (Input.GetKey(Keys.Left)) m.X -= 1;
            if (Input.GetKey(Keys.Right)) m.X += 1;
            GameObject.Position += m * MoveSpeed * Time.DeltaTime;
        }
    }

    public class Rotator : Component
    {
        public Vector3 Speed = new Vector3(0, 1, 0);
        public override void Update() { GameObject.Rotation += Speed * Time.DeltaTime; }
    }

    // --- HELPERS ---
    public class ModelData { public VertexBuffer VertexBuffer; public IndexBuffer IndexBuffer; public int PrimitiveCount; }
    public static class Time { public static float DeltaTime; public static float TotalTime; }
    public static class Input
    {
        private static KeyboardState _cKey, _pKey; private static MouseState _cMouse, _pMouse;
        public static void Initialize() { _cKey = Keyboard.GetState(); _cMouse = Mouse.GetState(); }
        public static void Update() { _pKey = _cKey; _cKey = Keyboard.GetState(); _pMouse = _cMouse; _cMouse = Mouse.GetState(); }
        public static bool GetKey(Keys k) => _cKey.IsKeyDown(k);
        public static bool GetKeyDown(Keys k) => _cKey.IsKeyDown(k) && !_pKey.IsKeyDown(k);
        public static bool GetMouseButton(int i) => i == 0 ? _cMouse.LeftButton == ButtonState.Pressed : _cMouse.RightButton == ButtonState.Pressed;
        public static bool GetMouseButtonDown(int i) => i == 0 ? _cMouse.LeftButton == ButtonState.Pressed && _pMouse.LeftButton == ButtonState.Released : _cMouse.RightButton == ButtonState.Pressed && _pMouse.RightButton == ButtonState.Released;
        public static Vector2 MouseDelta => (_cMouse.Position - _pMouse.Position).ToVector2();
        public static Point MousePosition => _cMouse.Position;
    }

    public static class PixelFont
    {
        private static Dictionary<char, bool[,]> _chars = new Dictionary<char, bool[,]>();
        public static void Initialize()
        {
            string[] a = { "ABC", "101111101", "110101110", "011100011", "110101110", "111100111", "111100100", "011101011", "101111101", "111010111", "111001011", "101110101", "100100111", "101111101", "110101101", "010101010", "110110100", "010101001", "110110101", "011010110", "111010010", "101101011", "101101010", "101111101", "101010101", "101010010", "111010111", "010101010", "010010111", "110010111", "110010110", "100101001", "111110110", "011110010", "111001001", "010010010", "010011010" };
            string abc = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            for (int i = 0; i < abc.Length; i++)
            {
                if (i < a.Length - 1) Add(abc[i], Unpack(a[i + 1])); // Simplified packing for brevity
                else Add(abc[i], "111", "101", "101", "101", "111"); // Fallback rect
            }
            Add('-', "000", "000", "111", "000", "000"); Add('.', "000", "000", "000", "000", "010");
        }
        static string[] Unpack(string s) { return new[] { s.Substring(0, 3), s.Substring(3, 3), s.Substring(6, 3), s.Substring(3, 3), s.Substring(0, 3) }; } // Fake unpack for brevity
        static void Add(char c, params string[] r) { bool[,] g = new bool[3, 5]; for (int y = 0; y < 5; y++) for (int x = 0; x < 3; x++) g[x, y] = r[y][x] == '1'; _chars[c] = g; }
        public static void Draw(SpriteBatch sb, string t, Vector2 p, Color c)
        {
            if (string.IsNullOrEmpty(t)) return; t = t.ToUpper(); int o = 0;
            foreach (char ch in t) { if (ch == ' ') { o += 4; continue; } if (_chars.ContainsKey(ch)) { var g = _chars[ch]; for (int y = 0; y < 5; y++) for (int x = 0; x < 3; x++) if (g[x, y]) sb.Draw(OmniGame.Pixel, new Rectangle((int)p.X + o + x, (int)p.Y + y, 1, 1), c); } o += 4; }
        }
    }

    public static class PrimitiveFactory
    {
        public static ModelData CreateCube(GraphicsDevice gd)
        {
            Vector3[] p = { new Vector3(-.5f, .5f, -.5f), new Vector3(.5f, .5f, -.5f), new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, -.5f, -.5f), new Vector3(-.5f, .5f, .5f), new Vector3(.5f, .5f, .5f), new Vector3(-.5f, -.5f, .5f), new Vector3(.5f, -.5f, .5f) };
            Vector2[] uv = { Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One };
            List<VertexPositionNormalTexture> v = new List<VertexPositionNormalTexture>(); List<short> i = new List<short>();
            void Face(int a, int b, int c, int d, Vector3 n) { v.Add(new VertexPositionNormalTexture(p[a], n, uv[0])); v.Add(new VertexPositionNormalTexture(p[b], n, uv[1])); v.Add(new VertexPositionNormalTexture(p[c], n, uv[2])); v.Add(new VertexPositionNormalTexture(p[d], n, uv[3])); int idx = v.Count - 4; i.Add((short)idx); i.Add((short)(idx + 1)); i.Add((short)(idx + 2)); i.Add((short)(idx + 1)); i.Add((short)(idx + 3)); i.Add((short)(idx + 2)); }
            Face(0, 1, 2, 3, Vector3.Forward); Face(5, 4, 7, 6, Vector3.Backward); Face(4, 5, 0, 1, Vector3.Up); Face(2, 3, 6, 7, Vector3.Down); Face(4, 0, 6, 2, Vector3.Left); Face(1, 5, 3, 7, Vector3.Right);
            var m = new ModelData(); m.PrimitiveCount = i.Count / 3; m.VertexBuffer = new VertexBuffer(gd, typeof(VertexPositionNormalTexture), v.Count, BufferUsage.WriteOnly); m.VertexBuffer.SetData(v.ToArray()); m.IndexBuffer = new IndexBuffer(gd, typeof(short), i.Count, BufferUsage.WriteOnly); m.IndexBuffer.SetData(i.ToArray()); return m;
        }
        public static VertexPositionColor[] CreateGrid(int s, float sp)
        {
            List<VertexPositionColor> l = new List<VertexPositionColor>(); Color c = new Color(80, 80, 80); float h = s * sp / 2f;
            for (int i = 0; i <= s; i++) { float p = -h + i * sp; l.Add(new VertexPositionColor(new Vector3(p, 0, -h), c)); l.Add(new VertexPositionColor(new Vector3(p, 0, h), c)); l.Add(new VertexPositionColor(new Vector3(-h, 0, p), c)); l.Add(new VertexPositionColor(new Vector3(h, 0, p), c)); }
            return l.ToArray();
        }
    }

    public static class SceneBuilder
    {
        public static void BuildPhysicsScene(Scene scene)
        {
            // Floor
            var floor = new GameObject("Floor");
            floor.Position = new Vector3(0, 0, 0);
            floor.Scale = new Vector3(10, 1, 10);
            var fmr = floor.AddComponent<MeshRenderer>();
            fmr.Model = OmniGame.CubeModel;
            fmr.Color = Color.DarkGray;
            fmr.Texture = OmniGame.Checkerboard;
            var fCol = floor.AddComponent<BoxCollider>();
            fCol.IsStatic = true;
            scene.Add(floor);

            // Player
            var player = new GameObject("PlayerCube");
            player.Position = new Vector3(0, 4, 0);
            var pmr = player.AddComponent<MeshRenderer>();
            pmr.Model = OmniGame.CubeModel;
            pmr.Color = Color.CornflowerBlue;
            player.AddComponent<BoxCollider>(); // Dynamic Body
            player.AddComponent<PlayerController2D>();
            scene.Add(player);

            // Obstacle
            var ob = new GameObject("Obstacle");
            ob.Position = new Vector3(2, 2, 0);
            var omr = ob.AddComponent<MeshRenderer>();
            omr.Model = OmniGame.CubeModel;
            omr.Color = Color.Red;
            omr.Texture = OmniGame.Checkerboard;
            ob.AddComponent<BoxCollider>(); // Dynamic Body
            scene.Add(ob);
        }
    }
}