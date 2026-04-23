using System.Diagnostics;

namespace MultiAgentDemo.Tracing;

internal static class MultiAgentWorkflowTracing
{
    public const string ActivitySourceName = "MultiAgentDemo.Workflows";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
