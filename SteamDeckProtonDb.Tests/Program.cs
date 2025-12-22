using System;
using NUnitLite;

namespace SteamDeckProtonDb.Tests
{
    public class Program
    {
        // NUnitLite runner entry point
        public static int Main(string[] args)
        {
            // Run NUnitLite tests and forward arguments from dotnet run.
            return new AutoRun().Execute(args);
        }
    }
}
