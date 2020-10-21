using System;
using cylib;

using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace cyFight
{
    class Program
    {
        static Stopwatch timer;
        static void Main(string[] args)
        {
            //This is a workaround for VS and dotnet CLI consistency
            //By default, VS sets the working directory to be the bin directory, while CLI sets it to be the project directory.
            //This is apparently a contentious issue about web dev or something.
            string baseDir = Assembly.GetEntryAssembly().Location;
            if (baseDir != null && baseDir != "")
                Environment.CurrentDirectory = Path.GetDirectoryName(baseDir);
            else
                Environment.CurrentDirectory = AppContext.BaseDirectory;

            CyLib.Init();

            var window = new Window("faito", 1920, 1080, WindowFlags.NONE);
            var rend = new Renderer(window);
            rend.VSync = true;
            rend.Assets.AddAssetBlob("cyFight.blob");
            var stage = new GameStage(rend, "binds.cyb");
            var scene = new TestScene(stage);

            stage.switchToScene(scene);

            timer = Stopwatch.StartNew();
            var prev = TimeSpan.Zero;
            double estFPS = 0;
            while (true)
            {
                var now = timer.Elapsed;
                var dt = (now - prev).TotalSeconds;
                prev = now;
                if (!stage.Update(dt))
                    break;
                stage.Draw();

                //fps calculation
                //this is kinda an exponential decay to the 'current' fps, which is 1/dt, but also weighted by dt
                //so it converges in a number of 'seconds' rather than a number of 'frames', which could vary wildly if you have 60-2000 fps
                var fps = 1 / dt;
                var perc = Math.Min(dt, 1.0);
                estFPS = estFPS * (1 - perc) + perc * fps;
                window.Title = estFPS.ToString("F2");
            }
        }
    }
}
