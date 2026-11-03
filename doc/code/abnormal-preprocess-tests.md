# abnormal 단위테스트 — 단계별(PreProcess · Inference · PostProcess) 구성, 1차 전처리

계획·검증: [doc/plan/abnormal-preprocess-tests.md](../plan/abnormal-preprocess-tests.md)

## 목차 (호출 트리)

- `dotnet test` → `OceanDetect.OceanDetectAI.Tests`
  - `Abnormal/PreProcess`
    - `PreprocessSampleTests` (samples/test01.xyz 전체) — 7
      - `ScanReader.Discover` · `Read` — 4
        - `XyzReader.Read` — 3
        - `NumpyCompat.Percentile` — 2
      - `Tiler.Tiles` · `TileOrigins` (`NumpyCompat.Arange`) — 5, 2
      - `TileStoreWriter` · `TileStore.ReadMeta` — 6
  - `Abnormal/Inference`
    - `SampleRunner.Voxelize` — 8
  - `Abnormal/PostProcess` — 다음 작업
  - 공통 도우미 `TempDir` · `TestPaths` · `ScanFactory` — 1

## OceanDetect/OceanDetectAI.Tests/Abnormal/TestSupport.cs:1

신규. 임시 폴더 · 레포 루트(samples) 찾기 · 1 m 격자 Scan 생성 — 모든 테스트가 쓰는 도우미.

before
```csharp
```
after
```csharp
using AbnormalDetection.Pcd;
using AbnormalDetection.Scans;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal;

internal sealed class TempDir : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "OceanDetectAITests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Root);

    public string Write(string name, string content)
    {
        var path = Path.GetFullPath(Path.Combine(Root, name));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, true); }
        catch (IOException) { }
    }
}

internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();
    public static string SampleXyz => Path.Combine(RepoRoot, "samples", "test01.xyz");

    static string FindRepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "OceanDetectAITest.sln")))
                return d.FullName;
        throw new DirectoryNotFoundException("OceanDetectAITest.sln 을 찾지 못했습니다.");
    }
}

internal static class ScanFactory
{
    // 1 m 간격 width×height 격자. x, y 는 0 부터 정수.
    public static Scan Grid(string stem, int width, int height, double z = 0, double sfZ = 0)
    {
        var p = new PointXyz(width * height);
        int k = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++, k++)
            {
                p.X[k] = x;
                p.Y[k] = y;
                p.Z[k] = z;
            }
        return new Scan { Stem = stem, Path = stem + ".xyz", Xyz = p, SfZ = sfZ, Frame = "local:" + stem };
    }
}
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PreProcess/NumpyCompatTests.cs:1

신규. 해저면 기준(sf_z)과 타일 격자를 정하는 numpy 호환 계산(Percentile linear, Arange 길이)을 고정한다.

before
```csharp
```
after
```csharp
using AbnormalDetection;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PreProcess;

public class NumpyCompatTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(2, 1.06)]
    [InlineData(50, 2.5)]
    [InlineData(100, 4.0)]
    public void Percentile_MatchesNumpyLinear(double q, double expected)
    {
        var values = new double[] { 4, 1, 3, 2 };

        Assert.Equal(expected, NumpyCompat.Percentile(values, q), 12);
        Assert.Equal(new double[] { 4, 1, 3, 2 }, values);
    }

    [Fact]
    public void Percentile_Empty_Throws() =>
        Assert.Throws<ArgumentException>(() => NumpyCompat.Percentile(Array.Empty<double>(), 2));

    [Theory]
    [InlineData(0, 100, 40, new double[] { 0, 40, 80 })]
    [InlineData(0, 80, 40, new double[] { 0, 40 })]
    [InlineData(5, 5, 40, new double[0])]
    public void Arange_MatchesNumpy(double lo, double hi, double step, double[] expected) =>
        Assert.Equal(expected, NumpyCompat.Arange(lo, hi, step));
}
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PreProcess/XyzReaderTests.cs:1

신규. .xyz 의 머리글·주석·구분자 처리, 첫 점 원점 이동, 수심 부호 반전, float32 반올림을 확인한다.

before
```csharp
```
after
```csharp
using AbnormalDetection.Pcd;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PreProcess;

public class XyzReaderTests
{
    [Fact]
    public void Read_SkipsNonDataLines_AndMakesLocalCoordinates()
    {
        using var dir = new TempDir();
        var path = dir.Write("scan.xyz",
            "X Y Z\n" +
            "# comment\n" +
            "\n" +
            "385403.472 3848908.310 3.381\n" +
            "385403.771,3848908.319,3.447\n" +
            "385403.861\t3848908.394\t3.304\textra\n");

        var (xyz, ox, oy) = XyzReader.Read(path);

        Assert.Equal(3, xyz.Count);
        Assert.Equal(3, XyzReader.CountPoints(path));
        Assert.Equal(385403.472, ox);
        Assert.Equal(3848908.310, oy);
        Assert.Equal(0.0, xyz.X[0]);
        Assert.Equal(0.0, xyz.Y[0]);
        Assert.Equal((double)(float)(385403.771 - 385403.472), xyz.X[1]);
        Assert.Equal((double)(float)(3848908.394 - 3848908.310), xyz.Y[2]);
        Assert.Equal((double)(float)-3.447, xyz.Z[1]);
    }

    [Fact]
    public void Read_NoDataLines_ReturnsEmpty()
    {
        using var dir = new TempDir();
        var path = dir.Write("empty.xyz", "X Y Z\n# only header\n");

        var (xyz, ox, oy) = XyzReader.Read(path);

        Assert.Equal(0, xyz.Count);
        Assert.Equal((0.0, 0.0), (ox, oy));
    }
}
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PreProcess/ScanReaderTests.cs:1

신규. 입력 탐색(pointclouds 우선·정렬·없는 경로), 빈 스캔 건너뛰기, 스캔 단위 2퍼센타일 sf_z 를 확인한다.

before
```csharp
```
after
```csharp
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
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PreProcess/TilerTests.cs:1

신규. 50 m 타일·40 m stride 격자, 타일 ID·점 수·중심, 타일 로컬 ±25 m·해저면 기준 z, 최소 점 수 필터를 확인한다.

before
```csharp
```
after
```csharp
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
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PreProcess/TileStoreTests.cs:1

신규. tiles.bin(float32 N×4) 내용·크기, tiles.json offset(누적 행), ReadMeta 왕복, 형식 불일치 예외를 확인한다.

before
```csharp
```
after
```csharp
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
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PreProcess/PreprocessSampleTests.cs:1

신규. samples/test01.xyz 전처리 전체(ScanReader→Tiler→TileStoreWriter)가 원본 CLI 결과(타일 63, 행 2,846,483)와 같은지 본다.

before
```csharp
```
after
```csharp
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
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/Inference/VoxelizeTests.cs:1

신규. 추론 입력 변환 SampleRunner.Voxelize 의 0.2 m 복셀 묶기·입력 순서·범위 밖 제거·복셀당 10점·잘못된 입력 예외를 확인한다.

before
```csharp
```
after
```csharp
using AbnormalDetection.SampleSupport;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.Inference;

public class VoxelizeTests
{
    [Fact]
    public void Voxelize_GroupsPointsByVoxel_InInputOrder()
    {
        float[] pts =
        {
            0.1f, 0.1f, 0.1f, 0f,
            0.5f, 0.1f, 0.1f, 0f,
            0.15f, 0.1f, 0.1f, 1f,
        };

        var v = SampleRunner.Voxelize(pts);

        Assert.Equal(new[] { 2, 1 }, v.Counts);
        Assert.Equal(new[] { 0, 10, 125, 125, 0, 10, 125, 127 }, v.Coordinates);
        Assert.Equal(80, v.Voxels.Length);
        Assert.Equal(pts[0..4], v.Voxels[0..4]);
        Assert.Equal(pts[8..12], v.Voxels[4..8]);
        Assert.Equal(pts[4..8], v.Voxels[40..44]);
        Assert.All(v.Voxels[8..40], f => Assert.Equal(0f, f));
    }

    [Theory]
    [InlineData(25f, 0f, 0f)]
    [InlineData(0f, -25.1f, 0f)]
    [InlineData(0f, 0f, -2.1f)]
    [InlineData(0f, 0f, 30f)]
    public void Voxelize_DropsPointsOutsideModelRange(float x, float y, float z)
    {
        var v = SampleRunner.Voxelize(new[] { x, y, z, 0f });

        Assert.Empty(v.Counts);
        Assert.Empty(v.Voxels);
    }

    [Fact]
    public void Voxelize_KeepsAtMostTenPointsPerVoxel()
    {
        var pts = Enumerable.Repeat(new[] { 0.1f, 0.1f, 0.1f, 0f }, 12).SelectMany(p => p).ToArray();

        Assert.Equal(new[] { 10 }, SampleRunner.Voxelize(pts).Counts);
    }

    [Fact]
    public void Voxelize_RejectsBadInput()
    {
        Assert.Throws<ArgumentException>(() => SampleRunner.Voxelize(new float[3]));
        Assert.Throws<ArgumentException>(() => SampleRunner.Voxelize(new[] { float.NaN, 0f, 0f, 0f }));
    }
}
```
