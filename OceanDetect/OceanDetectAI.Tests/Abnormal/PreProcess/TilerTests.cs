using AbnormalDetection.Tiling;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PreProcess;

public class TilerTests
{
    [Fact]
    public void TileOrigins_StartsAtScanMin_WithStride()
    {
        Assert.Equal(new[] { 0.0, 40, 80 }, Tiler.TileOrigins(0, 99, 50, 40));
        Assert.Equal(new[] { 7.0 }, Tiler.TileOrigins(7, 7, 50, 40));
    }

    [Fact]
    public void Tiles_GridScan_MakesOverlappingTiles()
    {
        var tiler = new Tiler();

        var tiles = tiler.Tiles(ScanFactory.Grid("s", 100, 100)).ToList();

        Assert.Equal(
            new[] { "s_t0000", "s_t0001", "s_t0002", "s_t0100", "s_t0101", "s_t0102", "s_t0200", "s_t0201", "s_t0202" },
            tiles.Select(t => t.Meta.TileId));
        Assert.Equal(
            new[] { 2500, 2500, 1000, 2500, 2500, 1000, 1000, 1000, 400 },
            tiles.Select(t => t.Meta.PointCount));
        Assert.Equal((25.0, 25.0), (tiles[0].Meta.OriginX, tiles[0].Meta.OriginY));
        Assert.Equal((105.0, 105.0), (tiles[^1].Meta.OriginX, tiles[^1].Meta.OriginY));
        Assert.All(tiles, t => Assert.Equal(("s", "local:s"), (t.Meta.Scan, t.Meta.Frame)));
        Assert.Equal(9, tiler.StatTiles);
    }

    [Fact]
    public void Tiles_PointsAreTileLocal_AndSeafloorRelative()
    {
        var scan = ScanFactory.Grid("s", 100, 100, z: -10, sfZ: -12);

        foreach (var (meta, pts) in new Tiler().Tiles(scan))
        {
            Assert.Equal(-12, meta.SeafloorZ);
            Assert.Equal(meta.PointCount * 4, pts.Length);
            for (int k = 0; k < pts.Length; k += 4)
            {
                Assert.InRange(pts[k], -25f, 24f);
                Assert.InRange(pts[k + 1], -25f, 24f);
                Assert.Equal(2f, pts[k + 2]);
                Assert.Equal(0f, pts[k + 3]);
            }
        }
    }

    [Fact]
    public void Tiles_DropsTilesBelowMinPoints()
    {
        var tiler = new Tiler(minPoints: 401);

        var tiles = tiler.Tiles(ScanFactory.Grid("s", 100, 100)).ToList();

        Assert.Equal(8, tiles.Count);
        Assert.DoesNotContain(tiles, t => t.Meta.TileId == "s_t0202");
        Assert.Equal(1, tiler.StatTilesDropped);
    }

    [Fact]
    public void Tiles_ScanBelowMinPoints_YieldsNothing()
    {
        var tiler = new Tiler();

        Assert.Empty(tiler.Tiles(ScanFactory.Grid("s", 7, 7)));
        Assert.Equal(1, tiler.StatTilesDropped);
        Assert.Equal(0, tiler.StatScans);
    }
}
