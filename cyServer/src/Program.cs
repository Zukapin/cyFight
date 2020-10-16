using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.CompilerServices;
using cySim;

namespace cyServer
{
    class Program
    {
        const double GoalTimestep = 1000.0 / 60.0; //in ms
        const float Timestep = (float)(1.0 / 60.0); //in s

        static Stopwatch watch = new Stopwatch();
        static double leftoverTime = 0;
        static double spinGoal = 0;
        static void Main(string[] args)
        {
            var net = new Network();

            watch.Start();
            while (true)
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool HasElapsed()
        {
            return watch.Elapsed.TotalMilliseconds > spinGoal;
        }
    }
}
