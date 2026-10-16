using Serilog;

namespace Atlas.LoadTest.Engine;

public static class LoadTestLogging
{
    public static ILogger Logger => Log.Logger;
}
