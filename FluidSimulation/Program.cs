using System;
using OpenTK;
using OpenTK.Windowing.Desktop;

namespace FluidSimulation
{
    class Program
    {
        static void Main(string[] args)
        {
            using (Simulation sim = new Simulation())
            {
                sim.Run();
            }
        }
    }
}
