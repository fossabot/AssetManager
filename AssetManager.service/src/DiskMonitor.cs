
public interface IDiskMonitor
{
    (float usedGb, float totalGb) GetDiskUsage();
}

public class DiskMonitor : IDiskMonitor
{
    public (float usedGb, float totalGb) GetDiskUsage()
    {
        var drive = DriveInfo.GetDrives()
            .FirstOrDefault(d => d.Name == "C:\\");

        if (drive == null || !drive.IsReady)
            return (0, 0);

        long total = drive.TotalSize;
        long free = drive.TotalFreeSpace;

        long used = total - free;

        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        float usedGb = (float)(used / bytesPerGigabyte);
        float totalGb = (float)(total / bytesPerGigabyte);

        return (usedGb, totalGb);
    }
}
