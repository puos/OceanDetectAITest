using System.Text.Json;
using AbnormalDetection.IO;
using AbnormalDetection.Tiling;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PreProcess;

public class TileStoreTests
{
    static List<(TileMeta Meta, float[] Points)> WriteGridStore(string root)
    {
        var scan = ScanFactory.Grid("s", 100, 100, sfZ: -1);
        var tiles = new Tiler().Tiles(scan).ToList();
        using var w = new TileStoreWriter(root);
        foreach (var (meta, pts) in tiles)
            w.Add(meta, pts);
        w.Scans.Add(new ScanInfo
        {
            Scan = scan.Stem, File = scan.Path, Points = scan.Points, SeafloorZ = scan.SfZ,
            Frame = scan.Frame, Tiles = tiles.Count, OffsetX = 10, OffsetY = 20,
        });
        return tiles;
    }

    [Fact]
    public void Writer_BinHoldsAllTilePointsInOrder()
    {
        using var dir = new TempDir();
        var tiles = WriteGridStore(dir.Root);

        var bin = File.ReadAllBytes(Path.Combine(dir.Root, TileStoreWriter.BinName));
        var all = new float[bin.Length / 4];
        Buffer.BlockCopy(bin, 0, all, 0, bin.Length);

        Assert.Equal(tiles.Sum(t => t.Meta.PointCount) * 16L, bin.Length);
        Assert.Equal(tiles.SelectMany(t => t.Points), all);
    }

    [Fact]
    public void Writer_JsonOffsetIsCumulativeRow()
    {
        using var dir = new TempDir();
        var tiles = WriteGridStore(dir.Root);

        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir.Root, TileStoreWriter.JsonName)));
        var offsets = doc.RootElement.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("offset").GetInt64());

        long row = 0;
        var expected = tiles.Select(t => { var o = row; row += t.Meta.PointCount; return o; }).ToList();
        Assert.Equal(expected, offsets);
    }

    [Fact]
    public void ReadMeta_RestoresTilesAndScans()
    {
        using var dir = new TempDir();
        var tiles = WriteGridStore(dir.Root);

        var (metas, scans) = TileStore.ReadMeta(dir.Root);

        Assert.Equal(tiles.Select(t => t.Meta.TileId), metas.Select(m => m.TileId));
        Assert.Equal(tiles.Select(t => t.Meta.PointCount), metas.Select(m => m.PointCount));
        Assert.Equal((105.0, 105.0, -1.0), (metas[^1].OriginX, metas[^1].OriginY, metas[^1].SeafloorZ));
        var s = Assert.Single(scans);
        Assert.Equal(("s", 10000L, 9, 10.0, 20.0), (s.Scan, s.Points, s.Tiles, s.OffsetX, s.OffsetY));
    }

    [Fact]
    public void ReadMeta_OtherFormat_Throws()
    {
        using var dir = new TempDir();
        dir.Write(TileStoreWriter.JsonName, "{\"format\":\"other/1\",\"scans\":[],\"tiles\":[]}");

        Assert.Throws<InvalidOperationException>(() => TileStore.ReadMeta(dir.Root));
    }
}
