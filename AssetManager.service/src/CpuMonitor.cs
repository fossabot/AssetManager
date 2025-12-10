using System.Diagnostics;

public interface ICpuMonitor
{
    float GetCpuUsage();
}

public class CpuMonitor : ICpuMonitor
{
    private readonly PerformanceCounter _cpuCounter;

    public CpuMonitor()
    {
        if (OperatingSystem.IsWindows())
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        }
        else
        {
            throw new PlatformNotSupportedException("PerformanceCounter is only supported on Windows.");
        }
        _cpuCounter.NextValue();
    }

    public float GetCpuUsage()
    {
#pragma warning disable CA1416 // Validar a compatibilidade da plataforma
        return _cpuCounter.NextValue();
#pragma warning restore CA1416 // Validar a compatibilidade da plataforma
    }
}
