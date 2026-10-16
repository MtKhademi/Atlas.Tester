using Serilog;

namespace Atlas.Tester.Engine;

public static class TesterLogging
{
    public static ILogger Logger => Log.Logger;
}
