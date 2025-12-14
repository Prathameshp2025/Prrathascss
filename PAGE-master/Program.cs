using System;

namespace OmniEngine
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new OmniGame())
                game.Run();
        }
    }
}