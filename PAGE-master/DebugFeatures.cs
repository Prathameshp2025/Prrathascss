using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

/// <summary>
/// A self-contained Debug Console for MonoGame.
/// Usage: 
/// 1. Add to Game.Components: Components.Add(new DebugFeatures(this));
/// 2. Assign a SpriteFont: debugFeatures.ConsoleFont = Content.Load<SpriteFont>("MyFont");
/// </summary>
public class DebugFeatures : DrawableGameComponent
{
    // Configuration
    public Keys ToggleKey = Keys.OemTilde; // The ` key
    public int MaxLogCount = 100;
    public Color BackgroundColor = new Color(0, 0, 0, 200); // Semi-transparent black
    public Color TextColor = Color.White;
    public Color WarningColor = Color.Yellow;
    public Color ErrorColor = Color.Red;
    public SpriteFont ConsoleFont; // MUST BE SET EXTERNALLY

    // State
    private bool _isVisible = false;
    private string _inputString = "";
    private KeyboardState _prevKeyState;

    // Debug Drawing State
    private bool _showDebugGrid = false;
    private BasicEffect _lineEffect;
    private VertexPositionColor[] _gridVertices;

    // Data
    private struct LogEntry
    {
        public string Message;
        public Color Color;
    }
    private readonly List<LogEntry> _logs = new List<LogEntry>();
    private readonly List<string> _commandHistory = new List<string>();
    private int _historyIndex = 0;

    // Command Registry
    private readonly Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();

    // Graphics Resources
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;

    // FPS Tracking
    private double _fps = 0;
    private int _frames = 0;
    private double _elapsed = 0;

    public DebugFeatures(Game game) : base(game)
    {
        // Register default commands
        RegisterCommand("help", HelpCommand);
        RegisterCommand("clear", (_) => _logs.Clear());
        RegisterCommand("quit", (_) => game.Exit());
        RegisterCommand("grid", (_) => {
            _showDebugGrid = !_showDebugGrid;
            Log($"Debug Grid: {(_showDebugGrid ? "ON" : "OFF")}");
        });
    }

    public override void Initialize()
    {
        base.Initialize();

        // Hook into window text input for typing
        Game.Window.TextInput += OnTextInput;

        // Setup Grid
        SetupGrid();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create 1x1 white texture for background
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Setup BasicEffect for lines
        _lineEffect = new BasicEffect(GraphicsDevice);
        _lineEffect.VertexColorEnabled = true;
    }

    /// <summary>
    /// Add a message to the console log manually.
    /// </summary>
    public void Log(string message, Color? color = null)
    {
        if (_logs.Count >= MaxLogCount) _logs.RemoveAt(0);
        _logs.Add(new LogEntry { Message = message, Color = color ?? TextColor });
    }

    public void LogWarning(string message) => Log("WARNING: " + message, WarningColor);
    public void LogError(string message) => Log("ERROR: " + message, ErrorColor);

    public override void Update(GameTime gameTime)
    {
        // Toggle Visibility
        var keyState = Keyboard.GetState();
        if (keyState.IsKeyDown(ToggleKey) && !_prevKeyState.IsKeyDown(ToggleKey))
        {
            _isVisible = !_isVisible;
        }
        _prevKeyState = keyState;

        // FPS Counter
        _elapsed += gameTime.ElapsedGameTime.TotalSeconds;
        _frames++;
        if (_elapsed >= 1)
        {
            _fps = _frames / _elapsed;
            _frames = 0;
            _elapsed = 0;
        }

        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        // 1. Draw 3D Debug Grid if enabled
        if (_showDebugGrid)
        {
            DrawGrid();
        }

        // 2. Draw Console UI if visible
        if (_isVisible && ConsoleFont != null)
        {
            DrawConsoleUI();
        }

        base.Draw(gameTime);
    }

    private void DrawGrid()
    {
        // Assuming Camera Setup - You might need to inject your camera's View/Projection matrices here
        // For now, using a default look at (0,0,0) from (0,5,10)
        var view = Matrix.CreateLookAt(new Vector3(0, 10, 10), Vector3.Zero, Vector3.Up);
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, GraphicsDevice.Viewport.AspectRatio, 0.1f, 100f);

        _lineEffect.View = view;
        _lineEffect.Projection = projection;
        _lineEffect.World = Matrix.Identity;

        // Apply Effect
        foreach (EffectPass pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _gridVertices, 0, _gridVertices.Length / 2);
        }
    }

    private void DrawConsoleUI()
    {
        int screenW = GraphicsDevice.Viewport.Width;
        int height = GraphicsDevice.Viewport.Height / 2;
        int lineHeight = ConsoleFont.LineSpacing;

        _spriteBatch.Begin();

        // Background
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenW, height), BackgroundColor);

        // Header (FPS + Mem)
        string header = $"FPS: {_fps:F1} | Cmds: {_commands.Count}";
        _spriteBatch.DrawString(ConsoleFont, header, new Vector2(5, 5), Color.Green);

        // Logs
        // Draw from bottom up
        int currentY = height - lineHeight * 2; // Leave room for input line
        for (int i = _logs.Count - 1; i >= 0; i--)
        {
            if (currentY < 20) break; // Don't draw over header
            _spriteBatch.DrawString(ConsoleFont, _logs[i].Message, new Vector2(10, currentY), _logs[i].Color);
            currentY -= lineHeight;
        }

        // Input Line
        int inputY = height - lineHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, inputY, screenW, 1), Color.Gray); // Separator
        _spriteBatch.DrawString(ConsoleFont, "> " + _inputString + "_", new Vector2(5, inputY), Color.White);

        _spriteBatch.End();
    }

    private void OnTextInput(object sender, TextInputEventArgs e)
    {
        if (!_isVisible) return;

        // Handle Backspace
        if (e.Key == Keys.Back)
        {
            if (_inputString.Length > 0)
                _inputString = _inputString.Substring(0, _inputString.Length - 1);
            return;
        }

        // Handle Enter
        if (e.Key == Keys.Enter)
        {
            ExecuteCommand(_inputString);
            _inputString = "";
            return;
        }

        // Ignore Tilde (Toggle Key) and other control chars
        if (e.Key == ToggleKey || char.IsControl(e.Character)) return;

        // Add character
        if (ConsoleFont != null && ConsoleFont.Characters.Contains(e.Character))
        {
            _inputString += e.Character;
        }
        else if (ConsoleFont == null)
        {
            // Fallback if font isn't checked/loaded yet, just accept it
            _inputString += e.Character;
        }
    }

    private void SetupGrid()
    {
        List<VertexPositionColor> verts = new List<VertexPositionColor>();
        Color gridColor = Color.Green;
        int size = 10;

        for (int i = -size; i <= size; i++)
        {
            // X lines
            verts.Add(new VertexPositionColor(new Vector3(-size, 0, i), gridColor));
            verts.Add(new VertexPositionColor(new Vector3(size, 0, i), gridColor));

            // Z lines
            verts.Add(new VertexPositionColor(new Vector3(i, 0, -size), gridColor));
            verts.Add(new VertexPositionColor(new Vector3(i, 0, size), gridColor));
        }
        _gridVertices = verts.ToArray();
    }

    // --- Command Logic ---

    public void RegisterCommand(string commandName, Action<string[]> action)
    {
        string key = commandName.ToLower();
        if (!_commands.ContainsKey(key)) _commands.Add(key, action);
        else _commands[key] = action;
    }

    private void ExecuteCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        Log("> " + input, Color.Cyan);
        _commandHistory.Add(input);

        string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string cmd = parts[0].ToLower();
        string[] args = parts.Skip(1).ToArray();

        if (_commands.ContainsKey(cmd))
        {
            try
            {
                _commands[cmd].Invoke(args);
            }
            catch (Exception ex)
            {
                LogError($"Exec Error: {ex.Message}");
            }
        }
        else
        {
            LogWarning($"Unknown command: '{cmd}'");
        }
    }

    private void HelpCommand(string[] args)
    {
        Log("Available Commands:", Color.Yellow);
        foreach (var key in _commands.Keys) Log($"- {key}");
    }
}