using System.Runtime.CompilerServices;
using Images.Services;

namespace Images.Tests;

internal static class TestAssemblyInitializer
{
    [ModuleInitializer]
    internal static void InitializeCodecPolicy()
        => CodecRuntime.Configure();
}
