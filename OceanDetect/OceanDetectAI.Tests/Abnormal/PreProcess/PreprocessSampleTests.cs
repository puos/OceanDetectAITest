using AbnormalDetection.IO;
using AbnormalDetection.Scans;
using AbnormalDetection.Tiling;
using Xunit;
using Xunit.Abstractions;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PreProcess;

public class PreprocessSampleTests
{
    private readonly ITestOutputHelper _output;

    public PreprocessSampleTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // 기대값은 원본 AbnormalDetection.Cli preprocess 결과 (doc/plan/abnormal-ai-port.md 검증)
    [Fact]
    public void Test01Xyz_ProducesKnownTilesAndRows()
    {
        using var dir = new TempDir();
        var reader = new ScanReader(TestPaths.SampleXyz);
        var tiler = new Tiler();

        using (var w = new TileStoreWriter(dir.Root))
            foreach (var scan in reader.Read())
                foreach (var (meta, pts) in tiler.Tiles(scan))
                    w.Add(meta, pts);

        long rows = new FileInfo(Path.Combine(dir.Root, TileStoreWriter.BinName)).Length / 16;
        var (metas, _) = TileStore.ReadMeta(dir.Root);
        _output.WriteLine($"points={reader.StatPoints:N0} tiles={tiler.StatTiles} dropped={tiler.StatTilesDropped} rows={rows:N0}");

        Assert.Equal(1_906_727, reader.StatPoints);
        Assert.Equal(63, tiler.StatTiles);
        Assert.Equal(2_846_483, rows);
        Assert.Equal(63, metas.Count);
        Assert.All(metas, m => Assert.True(m.PointCount >= Tiler.MinPoints));
    }
}
