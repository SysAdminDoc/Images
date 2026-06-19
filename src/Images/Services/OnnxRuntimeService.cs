using Microsoft.ML.OnnxRuntime;

namespace Images.Services;

public enum OnnxProvider
{
    Unavailable,
    Cpu,
    DirectML,
}

public static class OnnxRuntimeService
{
    private static readonly Lazy<OnnxProvider> _provider = new(Probe);

    public static OnnxProvider Provider => _provider.Value;

    public static string ProviderLabel => Provider switch
    {
        OnnxProvider.DirectML => "DirectML",
        OnnxProvider.Cpu => "CPU",
        _ => "Unavailable",
    };

    public static SessionOptions CreateSessionOptions()
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING,
        };

        if (Provider == OnnxProvider.DirectML)
        {
            try
            {
                options.AppendExecutionProvider_DML(0);
            }
            catch
            {
                // Probe said DirectML was available but session creation disagreed;
                // fall through to CPU gracefully.
            }
        }

        return options;
    }

    public static InferenceSession CreateSession(string modelPath)
        => new(modelPath, CreateSessionOptions());

    private static OnnxProvider Probe()
    {
        try
        {
            var probe = new SessionOptions();
            probe.AppendExecutionProvider_DML(0);
            probe.Dispose();
            return OnnxProvider.DirectML;
        }
        catch
        {
            // DirectML unavailable — CPU fallback
        }

        try
        {
            _ = typeof(InferenceSession).Assembly;
            return OnnxProvider.Cpu;
        }
        catch
        {
            return OnnxProvider.Unavailable;
        }
    }
}
