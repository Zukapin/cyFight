using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.CompilerServices;
using cySim;
using System.Threading.Tasks;
using System.IO;
using cyUtility;

namespace cyServer
{
    class Program
    {
        const double GoalTimestep = 1000.0 / 60.0; //in ms
        const float Timestep = (float)(1.0 / 60.0); //in s

        static Stopwatch watch = new Stopwatch();
        static double leftoverTime = 0;
        static double spinGoal = 0;

        static Network net;
        static void Main(string[] args)
        {
            net = new Network();

            var consoleInputThread = new Thread(new ThreadStart(ReadConsoleInput));
            consoleInputThread.Start();

            Logger.WriteLine(LogType.DEBUG, "Server is running");
            watch.Start();
            while (!KillThreads)
            {
                //spinlock here til Timestep has passed
                //we can actually remove the spinwait and it will still spin itself, but the timer doesn't do a great job when being restarted that frequently
                //this is stable over longer time periods (10s) but windows schedueling may cause lower-level jitter
                //shouldn't be Too Big of a deal as this is planned to run on a linux server (which has tighter scheduling than windows), 
                //and we have built-in jitter concerns from the network anyways
                spinGoal = GoalTimestep - leftoverTime;
                SpinWait.SpinUntil(HasElapsed);
                leftoverTime += watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                net.Update(Timestep);
                leftoverTime -= GoalTimestep;
            }

            net.Shutdown();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool HasElapsed()
        {
            return watch.Elapsed.TotalMilliseconds > spinGoal;
        }

        static void ReadConsoleInput()
        {
            var inputStream = Console.OpenStandardInput();
            Console.CancelKeyPress += OnConsoleCancel;

            while (!KillThreads)
            {
                if (Reader.ReadLine(out var line, 100))
                {
                    if (line != null)
                    {
                        line = line.Trim();
                        if (line.StartsWith("kill", true, null))
                        {
                            KillThreads = true;
                            break;
                        }
#if DEBUG
                        else if (line.StartsWith("lag start 1", true, null))
                        {
                            net.LagStart1();
                        }
                        else if (line.StartsWith("lag start 2", true, null))
                        {
                            net.LagStart2();
                        }
                        else if (line.StartsWith("lag start 3", true, null))
                        {
                            net.LagStart3();
                        }
                        else if (line.StartsWith("lag start 4", true, null))
                        {
                            net.LagStart4();
                        }
                        else if (line.StartsWith("lag start 5", true, null))
                        {
                            net.LagStart5();
                        }
                        else if (line.StartsWith("lag start 6", true, null))
                        {
                            net.LagStart6();
                        }
                        else if (line.StartsWith("lag stop", true, null))
                        {
                            net.LagStop();
                        }
#endif
                        else
                        {
                            Console.WriteLine("Unhandled command input: " + line);
                        }
                    }
                }
            }
        }

        static bool KillThreads = false;
        
        static void OnConsoleCancel(object sender, ConsoleCancelEventArgs e)
        {
            Logger.WriteLine(LogType.DEBUG, "Kill command recieved -- use the command 'kill' instead");
            e.Cancel = true;
        }

        class Reader
        {
            private static Thread inputThread;
            private static AutoResetEvent getInput, gotInput;
            private static string input;

            static Reader()
            {
                getInput = new AutoResetEvent(false);
                gotInput = new AutoResetEvent(false);
                inputThread = new Thread(reader);
                inputThread.IsBackground = true;
                inputThread.Start();
            }

            private static void reader()
            {
                while (true)
                {
                    getInput.WaitOne();
                    input = Console.ReadLine();
                    gotInput.Set();
                }
            }

            public static bool ReadLine(out string line, int timeOutMillisecs = Timeout.Infinite)
            {
                getInput.Set();
                bool success = gotInput.WaitOne(timeOutMillisecs);
                if (success)
                    line = input;
                else
                    line = null;
                return success;
            }
        }
    }
}
