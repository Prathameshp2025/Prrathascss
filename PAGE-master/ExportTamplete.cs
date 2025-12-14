using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OmniEngine;

namespace OmniEngine
{
    public static class ExportTemplate
    {
        public static string Generate(Scene scene)
        {
            StringBuilder sb = new StringBuilder();

            // 1. Header & Entry Point (Fixed CS5001 Error)
            sb.AppendLine(@"using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ExportedGame
{
    // --- ENTRY POINT ---
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new Game1())
                game.Run();
        }
    }

    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        public static Scene CurrentScene;
        public static Texture2D Pixel;
        public static Texture2D Checkerboard;
        public static ModelData CubeModel;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = ""Content"";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            Window.Title = ""Exported OmniGame"";
        }

        protected override void Initialize()
        {
            base.Initialize();
            Input.Initialize();
            OmniEffects.Initialize(GraphicsDevice);
            
            CurrentScene = new Scene();
            BuildExportedScene(CurrentScene);
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            // Assets
            Pixel = new Texture2D(GraphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });
            
            Checkerboard = new Texture2D(GraphicsDevice, 64, 64);
            Color[] d = new Color[64*64];
            for(int i=0; i<d.Length; i++) d[i] = ((i%64)/8 + (i/64)/8) % 2 == 0 ? Color.LightGray : Color.DarkGray;
            Checkerboard.SetData(d);

            CubeModel = PrimitiveFactory.CreateCube(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            Input.Update();
            Time.DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            CurrentScene.Update();
            Physics.Simulate(CurrentScene.GameObjects);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            
            CurrentScene.Draw3D(GraphicsDevice);
            
            base.Draw(gameTime);
        }

        private void BuildExportedScene(Scene scene)
        {");

            // 2. Scene Serialization
            int counter = 0;
            foreach (var go in scene.GameObjects)
            {
                if (go.Name == "EditorCamera") continue;

                string varName = $"go{counter++}";
                sb.AppendLine($"            var {varName} = new GameObject(\"{go.Name}\");");
                sb.AppendLine($"            {varName}.Position = new Vector3({go.Position.X}f, {go.Position.Y}f, {go.Position.Z}f);");
                sb.AppendLine($"            {varName}.Rotation = new Vector3({go.Rotation.X}f, {go.Rotation.Y}f, {go.Rotation.Z}f);");
                sb.AppendLine($"            {varName}.Scale = new Vector3({go.Scale.X}f, {go.Scale.Y}f, {go.Scale.Z}f);");

                foreach (var comp in go.GetComponents())
                {
                    if (comp is MeshRenderer mr)
                    {
                        sb.AppendLine($"            var mr{counter} = {varName}.AddComponent<MeshRenderer>();");
                        sb.AppendLine($"            mr{counter}.Model = Game1.CubeModel;");
                        sb.AppendLine($"            mr{counter}.Color = new Color({mr.Color.R}, {mr.Color.G}, {mr.Color.B}, {mr.Color.A});");
                        if (mr.Texture != null) sb.AppendLine($"            mr{counter}.Texture = Game1.Checkerboard;");
                    }
                    else if (comp is BoxCollider bc)
                    {
                        sb.AppendLine($"            var bc{counter} = {varName}.AddComponent<BoxCollider>();");
                        sb.AppendLine($"            bc{counter}.Size = new Vector3({bc.Size.X}f, {bc.Size.Y}f, {bc.Size.Z}f);");
                        sb.AppendLine($"            bc{counter}.IsStatic = {bc.IsStatic.ToString().ToLower()};");
                    }
                    else if (comp is PlayerController2D pc)
                    {
                        sb.AppendLine($"            var pc{counter} = {varName}.AddComponent<PlayerController2D>();");
                        sb.AppendLine($"            pc{counter}.MoveSpeed = {pc.MoveSpeed}f;");
                    }
                }
                sb.AppendLine($"            scene.Add({varName});");
                sb.AppendLine("");
            }

            // 3. Footer (Runtime Engine Code)
            sb.AppendLine(@"        }
    }

    // --- RUNTIME ENGINE CORE ---
    public static class Time { public static float DeltaTime; }
    public static class Input {
        static KeyboardState cK, pK;
        public static void Initialize() { cK = Keyboard.GetState(); }
        public static void Update() { pK = cK; cK = Keyboard.GetState(); }
        public static bool GetKey(Keys k) => cK.IsKeyDown(k);
    }

    public class Scene {
        public List<GameObject> GameObjects = new List<GameObject>();
        public Camera3D MainCamera = new Camera3D();
        public void Add(GameObject go) { GameObjects.Add(go); go.Scene = this; go.Awake(); }
        public void Update() { MainCamera.Update(); foreach(var g in GameObjects) g.Update(); }
        public void Draw3D(GraphicsDevice gd) { 
            OmniEffects.Standard.View = MainCamera.View; 
            OmniEffects.Standard.Projection = MainCamera.Projection;
            foreach(var g in GameObjects) g.Draw3D(gd); 
        }
    }

    public class GameObject {
        public string Name; public Vector3 Position, Rotation, Scale=Vector3.One; public Scene Scene;
        List<Component> c = new List<Component>();
        public GameObject(string n){Name=n;}
        public T AddComponent<T>() where T:Component,new(){ T t=new T(); t.GameObject=this; c.Add(t); t.Awake(); return t; }
        public T GetComponent<T>() => c.OfType<T>().FirstOrDefault();
        public List<Component> GetComponents() => c;
        public void Awake(){foreach(var i in c)i.Awake();} public void Update(){foreach(var i in c)i.Update();} public void Draw3D(GraphicsDevice gd){foreach(var i in c)i.Draw3D(gd);}
    }

    public abstract class Component { public GameObject GameObject; public virtual void Awake(){} public virtual void Update(){} public virtual void Draw3D(GraphicsDevice gd){} }

    public class MeshRenderer : Component {
        public ModelData Model; public Color Color = Color.White; public Texture2D Texture;
        public override void Draw3D(GraphicsDevice gd) {
            if(Model==null)return;
            Matrix w = Matrix.CreateScale(GameObject.Scale) * Matrix.CreateRotationY(GameObject.Rotation.Y) * Matrix.CreateTranslation(GameObject.Position);
            OmniEffects.Standard.World = w; OmniEffects.Standard.DiffuseColor = Color.ToVector3();
            OmniEffects.Standard.TextureEnabled = Texture!=null; OmniEffects.Standard.Texture = Texture;
            OmniEffects.Standard.VertexColorEnabled = false; // Fixed vertex declaration
            OmniEffects.Standard.CurrentTechnique.Passes[0].Apply();
            gd.SetVertexBuffer(Model.VertexBuffer); gd.Indices = Model.IndexBuffer; gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, Model.PrimitiveCount);
        }
    }

    public class BoxCollider : Component { public Vector3 Size=Vector3.One; public bool IsStatic=false; 
        public BoundingBox GetBounds() { 
            Vector3 h = Size * GameObject.Scale * 0.5f; return new BoundingBox(GameObject.Position-h, GameObject.Position+h);
        }
    }
    
    public class PlayerController2D : Component {
        public float MoveSpeed = 5f;
        public override void Update() {
            Vector3 m = Vector3.Zero;
            if (Input.GetKey(Keys.Up)) m.Z -= 1; if (Input.GetKey(Keys.Down)) m.Z += 1;
            if (Input.GetKey(Keys.Left)) m.X -= 1; if (Input.GetKey(Keys.Right)) m.X += 1;
            GameObject.Position += m * MoveSpeed * Time.DeltaTime;
        }
    }

    public class Camera3D {
        public Vector3 Position = new Vector3(0, 10, 10);
        public Matrix View, Projection;
        public void Update() {
            View = Matrix.CreateLookAt(Position, Vector3.Zero, Vector3.Up);
            Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 16f/9f, 0.1f, 1000f);
        }
    }

    public static class Physics {
        public static void Simulate(List<GameObject> objs) {
            foreach(var o in objs) {
                var c = o.GetComponent<BoxCollider>();
                if(c == null || c.IsStatic) continue;
                o.Position.Y -= 9.8f * Time.DeltaTime;
                if(o.Position.Y < 0.5f) o.Position.Y = 0.5f; 
            }
        }
    }

    public static class OmniEffects {
        public static BasicEffect Standard;
        public static void Initialize(GraphicsDevice gd) {
            Standard = new BasicEffect(gd);
            Standard.VertexColorEnabled = false;
            Standard.LightingEnabled = true;
            Standard.EnableDefaultLighting();
        }
    }
    
    public class ModelData { public VertexBuffer VertexBuffer; public IndexBuffer IndexBuffer; public int PrimitiveCount; }
    
    public static class PrimitiveFactory {
        public static ModelData CreateCube(GraphicsDevice gd) {
           Vector3[] p = { new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(-.5f,.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f) };
            Vector2[] uv = { Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One };
            List<VertexPositionNormalTexture> v = new List<VertexPositionNormalTexture>(); List<short> i = new List<short>();
            void Face(int a,int b,int c,int d,Vector3 n) { v.Add(new VertexPositionNormalTexture(p[a],n,uv[0])); v.Add(new VertexPositionNormalTexture(p[b],n,uv[1])); v.Add(new VertexPositionNormalTexture(p[c],n,uv[2])); v.Add(new VertexPositionNormalTexture(p[d],n,uv[3])); int idx=v.Count-4; i.Add((short)idx); i.Add((short)(idx+1)); i.Add((short)(idx+2)); i.Add((short)(idx+1)); i.Add((short)(idx+3)); i.Add((short)(idx+2)); }
            Face(0,1,2,3,Vector3.Forward); Face(5,4,7,6,Vector3.Backward); Face(4,5,0,1,Vector3.Up); Face(2,3,6,7,Vector3.Down); Face(4,0,6,2,Vector3.Left); Face(1,5,3,7,Vector3.Right);
            var m=new ModelData(); m.PrimitiveCount=i.Count/3; m.VertexBuffer=new VertexBuffer(gd,typeof(VertexPositionNormalTexture),v.Count,BufferUsage.WriteOnly); m.VertexBuffer.SetData(v.ToArray()); m.IndexBuffer=new IndexBuffer(gd,typeof(short),i.Count,BufferUsage.WriteOnly); m.IndexBuffer.SetData(i.ToArray()); return m;
        }
    }
}");
            return sb.ToString();
        }
    }
}