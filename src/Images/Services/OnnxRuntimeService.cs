using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Windows.AI.MachineLearning;

namespace Images.Services;

public enum OnnxProvider
{
    Unavailable,
    Cpu,
    Gpu,
    Npu,
}

public sealed record OnnxRuntimeInfo(
    OnnxProvider Provider,
    string HardwareLabel,
    string ExecutionProvider,
    string? Vendor,
    uint? DeviceId,
    bool IsWindowsMl)
{
    public string DetailLabel
    {
        get
        {
            var backend = string.IsNullOrWhiteSpace(Vendor)
                ? ExecutionProvider
                : $"{ExecutionProvider}, {Vendor}";
            return $"{HardwareLabel} ({backend})";
        }
    }
}

internal sealed record OnnxPathValidation(OnnxRuntimeInfo Runtime, bool Success, string Message);

public static class OnnxRuntimeService
{
    private enum CandidateKind { WindowsMlDevice, DirectMl, Cpu }

    private sealed record Candidate(
        OnnxRuntimeInfo Info,
        CandidateKind Kind,
        OrtEpDevice? Device = null);

    private sealed record ProbeResult(
        IReadOnlyList<Candidate> Candidates,
        bool WindowsMlCatalogAvailable,
        int ReadyCertifiedProviderCount);

    private static readonly Lazy<ProbeResult> _probe = new(Probe);
    private static OnnxRuntimeInfo? _activeRuntime;

    private static OnnxRuntimeInfo PreferredRuntime =>
        _probe.Value.Candidates.FirstOrDefault()?.Info ??
        new OnnxRuntimeInfo(OnnxProvider.Unavailable, "Unavailable", "Unavailable", null, null, false);

    public static OnnxProvider Provider => (Volatile.Read(ref _activeRuntime) ?? PreferredRuntime).Provider;

    public static string ProviderLabel => (Volatile.Read(ref _activeRuntime) ?? PreferredRuntime).HardwareLabel;

    public static string ProviderDetail => (Volatile.Read(ref _activeRuntime) ?? PreferredRuntime).DetailLabel;

    public static IReadOnlyList<OnnxRuntimeInfo> AvailablePaths =>
        _probe.Value.Candidates.Select(candidate => candidate.Info).ToArray();

    public static bool WindowsMlCatalogAvailable => _probe.Value.WindowsMlCatalogAvailable;

    public static int ReadyCertifiedWindowsMlProviderCount => _probe.Value.ReadyCertifiedProviderCount;

    public static SessionOptions CreateSessionOptions()
    {
        var candidate = _probe.Value.Candidates.FirstOrDefault();
        if (candidate is null)
            throw new InvalidOperationException("No ONNX Runtime inference path is available.");

        return CreateSessionOptions(candidate);
    }

    public static InferenceSession CreateSession(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        Exception? lastError = null;

        foreach (var candidate in _probe.Value.Candidates)
        {
            try
            {
                using var options = CreateSessionOptions(candidate);
                var session = new InferenceSession(modelPath, options);
                Volatile.Write(ref _activeRuntime, candidate.Info);
                return session;
            }
            catch (Exception ex) when (IsRecoverableRuntimeFailure(ex))
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException("No available inference path could load the ONNX model.", lastError);
    }

    internal static (InferenceSession First, InferenceSession Second, OnnxRuntimeInfo Runtime) CreateSessionPair(
        string firstModelPath,
        string secondModelPath)
    {
        Exception? lastError = null;

        foreach (var candidate in _probe.Value.Candidates)
        {
            InferenceSession? first = null;
            InferenceSession? second = null;
            try
            {
                using var options = CreateSessionOptions(candidate);
                first = new InferenceSession(firstModelPath, options);
                second = new InferenceSession(secondModelPath, options);
                Volatile.Write(ref _activeRuntime, candidate.Info);
                return (first, second, candidate.Info);
            }
            catch (Exception ex) when (IsRecoverableRuntimeFailure(ex))
            {
                second?.Dispose();
                first?.Dispose();
                lastError = ex;
            }
        }

        throw new InvalidOperationException("No available inference path could load both ONNX models.", lastError);
    }

    internal static IReadOnlyList<OnnxPathValidation> ValidatePinnedAddModelPaths(string modelPath)
    {
        var validations = new List<OnnxPathValidation>();
        foreach (var candidate in _probe.Value.Candidates)
        {
            try
            {
                using var options = CreateSessionOptions(candidate);
                using var session = new InferenceSession(modelPath, options);
                var inputNames = session.InputMetadata.Keys.ToArray();
                if (inputNames.Length != 2)
                    throw new InvalidDataException($"Expected two inputs, found {inputNames.Length}.");

                var shape = new[] { 3, 4, 5 };
                var left = new DenseTensor<float>(Enumerable.Repeat(1f, 60).ToArray(), shape);
                var right = new DenseTensor<float>(Enumerable.Repeat(2f, 60).ToArray(), shape);
                var inputs = new[]
                {
                    NamedOnnxValue.CreateFromTensor(inputNames[0], left),
                    NamedOnnxValue.CreateFromTensor(inputNames[1], right),
                };
                using var results = session.Run(inputs);
                var output = results.Single().AsTensor<float>().ToArray();
                var valid = output.Length == 60 && output.All(value => Math.Abs(value - 3f) < 0.00001f);
                validations.Add(new OnnxPathValidation(
                    candidate.Info,
                    valid,
                    valid ? "60 add outputs matched." : "Add output did not match the pinned fixture."));
            }
            catch (Exception ex)
            {
                validations.Add(new OnnxPathValidation(candidate.Info, false, ex.Message));
            }
        }

        return validations;
    }

    private static SessionOptions CreateSessionOptions(Candidate candidate)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING,
        };

        try
        {
            switch (candidate.Kind)
            {
                case CandidateKind.WindowsMlDevice:
                    options.EnableMemoryPattern = false;
                    options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                    options.AppendExecutionProvider(
                        OrtEnv.Instance(),
                        [candidate.Device ?? throw new InvalidOperationException("Windows ML device is unavailable.")],
                        new Dictionary<string, string>());
                    break;
                case CandidateKind.DirectMl:
                    options.EnableMemoryPattern = false;
                    options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                    options.AppendExecutionProvider_DML(0);
                    break;
                case CandidateKind.Cpu:
                    break;
            }

            return options;
        }
        catch
        {
            options.Dispose();
            throw;
        }
    }

    private static ProbeResult Probe()
    {
        var candidates = new List<Candidate>();
        var catalogAvailable = false;
        var readyCertified = 0;

        try
        {
            OrtEnv.Instance().DisableTelemetryEvents();
        }
        catch
        {
            // Runtime probing still falls back cleanly if telemetry control is unavailable.
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            try
            {
                var providers = ExecutionProviderCatalog.GetDefault().FindAllProviders();
                catalogAvailable = true;
                foreach (var provider in providers)
                {
                    if (provider.Certification != ExecutionProviderCertification.Certified ||
                        provider.ReadyState != ExecutionProviderReadyState.Ready)
                    {
                        continue;
                    }

                    readyCertified++;
                    try
                    {
                        _ = provider.TryRegister();
                    }
                    catch
                    {
                        // Another ready provider can still register, and bundled fallbacks remain.
                    }
                }
            }
            catch
            {
                catalogAvailable = false;
            }
        }

        try
        {
            foreach (var device in OrtEnv.Instance().GetEpDevices()
                         .Where(device => device.HardwareDevice.Type is OrtHardwareDeviceType.NPU or OrtHardwareDeviceType.GPU)
                         .Where(device => !device.EpName.Contains("DML", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(device => device.HardwareDevice.Type == OrtHardwareDeviceType.NPU ? 0 : 1)
                         .ThenBy(device => device.EpName, StringComparer.OrdinalIgnoreCase))
            {
                var provider = device.HardwareDevice.Type == OrtHardwareDeviceType.NPU
                    ? OnnxProvider.Npu
                    : OnnxProvider.Gpu;
                var hardwareLabel = provider == OnnxProvider.Npu ? "NPU" : "GPU";
                var vendor = string.IsNullOrWhiteSpace(device.HardwareDevice.Vendor)
                    ? device.EpVendor
                    : device.HardwareDevice.Vendor;
                var info = new OnnxRuntimeInfo(
                    provider,
                    hardwareLabel,
                    device.EpName,
                    vendor,
                    device.HardwareDevice.DeviceId,
                    IsWindowsMl: true);
                if (candidates.All(existing =>
                        existing.Info.Provider != info.Provider ||
                        !existing.Info.ExecutionProvider.Equals(info.ExecutionProvider, StringComparison.OrdinalIgnoreCase) ||
                        existing.Info.DeviceId != info.DeviceId))
                {
                    candidates.Add(new Candidate(info, CandidateKind.WindowsMlDevice, device));
                }
            }
        }
        catch
        {
            // The Windows ML catalog or ORT device enumeration is not available; use bundled fallbacks.
        }

        try
        {
            using var directMlProbe = new SessionOptions();
            directMlProbe.AppendExecutionProvider_DML(0);
            candidates.Add(new Candidate(
                new OnnxRuntimeInfo(OnnxProvider.Gpu, "GPU", "DirectML", null, 0, IsWindowsMl: false),
                CandidateKind.DirectMl));
        }
        catch
        {
            // DirectML unavailable; CPU remains the universal path.
        }

        try
        {
            _ = typeof(InferenceSession).Assembly;
            candidates.Add(new Candidate(
                new OnnxRuntimeInfo(OnnxProvider.Cpu, "CPU", "ONNX Runtime", null, null, IsWindowsMl: false),
                CandidateKind.Cpu));
        }
        catch
        {
            // No usable ONNX Runtime assembly.
        }

        return new ProbeResult(candidates, catalogAvailable, readyCertified);
    }

    private static bool IsRecoverableRuntimeFailure(Exception exception)
        => exception is OnnxRuntimeException or InvalidOperationException or NotSupportedException or
            ArgumentException or DllNotFoundException or EntryPointNotFoundException or TypeInitializationException;
}
