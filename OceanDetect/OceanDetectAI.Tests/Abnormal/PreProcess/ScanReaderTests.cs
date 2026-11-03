using AbnormalDetection.Scans;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PreProcess;

public class ScanReaderTests
{
    [Fact]
    public void Discover_PrefersPointcloudsFolder_AndSorts()
    {
        using var dir = new TempDir();
        dir.Write("root.xyz", "0 0 0\n");
        var b = dir.Write("pointclouds/b.xyz", "0 0 0\n");
        var a = dir.Write("pointclouds/a.pcd", "");
        dir.Write("pointclouds/notes.txt", "");

        Assert.Equal(new[] { a, b }, ScanReader.Discover(dir.Root));
    }

    [Fact]
    public void Discover_MissingPath_Throws() =>
        Assert.Throws<FileNotFoundException>(() =>
            ScanReader.Discover(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    [Fact]
    public void Read_SkipsEmptyScan_AndUsesScanPercentileAsSeafloor()
    {
        using var dir = new TempDir();
        dir.Write("a_empty.xyz", "X Y Z\n");
        dir.Write("b_scan.xyz", string.Concat(Enumerable.Range(0, 100).Select(i => $"{1000 + i} {2000 + i} {i}\n")));
        var reader = new ScanReader(dir.Root);

        var scan = Assert.Single(reader.Read().ToList());

        Assert.Equal("b_scan", scan.Stem);
        Assert.Equal("local:b_scan", scan.Frame);
        Assert.Equal(100, scan.Points);
        Assert.Equal((1000.0, 2000.0), (scan.OriginX, scan.OriginY));
        Assert.Equal(-97.02, scan.SfZ, 9);
        Assert.Equal(1, reader.StatEmpty);
        Assert.Equal(1, reader.StatScans);
        Assert.Equal(100, reader.StatPoints);
    }
}
