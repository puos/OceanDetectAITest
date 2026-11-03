# abnormal 단위테스트 — 2차 후처리 (PostProcess)

계획·검증: [doc/plan/abnormal-postprocess-tests.md](../plan/abnormal-postprocess-tests.md) · 1차 전처리: [abnormal-preprocess-tests.md](abnormal-preprocess-tests.md) (공통 도우미 `TempDir` · `ScanFactory` 는 그 문서 항목 1)

## 목차 (호출 트리)

- `dotnet test` → `OceanDetect.OceanDetectAI.Tests` → `Abnormal/PostProcess`
  - `PostprocessFlowTests` (파일 → 최종 결과) — 6
    - `TileStore.ReadMeta` (전처리 산출물)
    - `PredictionStore.Read` — 3
    - `PilotConfig.Load` → `PostprocessConfig` — 2
    - `Postprocessor.RunByFrame` → `Run` — 4
      - `FilterScore` → `ToGlobal` → `FilterSize` → `GlobalNms` (`BevIou`) → `CrossClassNms`
    - `ResultWriter.SaveFinal` · `WriteIndex` — 5
  - 공통 도우미 `Boxes` — 1

## OceanDetect/OceanDetectAI.Tests/Abnormal/PostProcess/Boxes.cs:1

신규. 원본 pilot.yaml 의 tiling·postprocess 절, 예측·타일 메타 생성, predictions.json 쓰기 — 후처리 테스트 공통 도우미.

before
```csharp
```
after
```csharp
using System.Text.Json;
using AbnormalDetection.Config;
using AbnormalDetection.IO;
using AbnormalDetection.Postprocessing;
using AbnormalDetection.Tiling;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PostProcess;

internal static class Boxes
{
    // 원본 config/pilot.yaml 의 tiling · postprocess 절
    public const string PilotYaml =
        "tiling:\n" +
        "  tile_size_m: 50\n" +
        "  overlap_m: 10\n" +
        "  min_points_per_tile: 50\n" +
        "  seafloor_percentile: 2\n" +
        "postprocess:\n" +
        "  score_threshold:\n" +
        "    Fish_Reef: 0.5\n" +
        "    Block: 0.5\n" +
        "    Wreck: 0.7\n" +
        "  size_filter:\n" +
        "    Wreck: {min_max_side: 5.0}\n" +
        "  global_nms:\n" +
        "    Fish_Reef:\n" +
        "      - {max_side_lt: 10, type: dist, thresh: 1.2}\n" +
        "      - {type: iou,  thresh: 0.3}\n" +
        "    Block:\n" +
        "      - {max_side_lt: 10, type: dist, thresh: 1.2}\n" +
        "      - {type: iou,  thresh: 0.3}\n" +
        "    Wreck:     {type: iou,  thresh: 0.3}\n" +
        "  cross_class_nms:\n" +
        "    - classes: [Fish_Reef, Block]\n" +
        "      dist: 1.2\n" +
        "      iou:  0.3\n";

    public static PostprocessConfig Pilot(TempDir dir) =>
        PilotConfig.Load(dir.Write("pilot.yaml", PilotYaml)).Postprocess;

    public static Prediction Raw(string cls, double score, double cx = 0, double cy = 0, string tile = "s_t0000",
                                 double dx = 3, double dy = 3, double dz = 1, double cz = 0, double heading = 0) =>
        new Prediction
        {
            ClassName = cls, Score = score, Cx = cx, Cy = cy, Cz = cz,
            Dx = dx, Dy = dy, Dz = dz, Heading = heading, TileId = tile,
        };

    public static GlobalPrediction At(string cls, double score, double x, double y,
                                      double dx = 3, double dy = 3, double heading = 0, string tile = "s_t0000") =>
        new GlobalPrediction
        {
            ClassName = cls, Score = score, X = x, Y = y, Dx = dx, Dy = dy, Dz = 1,
            Heading = heading, TileId = tile, Scan = "s", Frame = "local:s",
        };

    public static TileMeta Tile(string id, double ox, double oy, double sfZ = 0, string scan = "s", int points = 100) =>
        new TileMeta
        {
            TileId = id, OriginX = ox, OriginY = oy, SeafloorZ = sfZ,
            Scan = scan, Frame = "local:" + scan, PointCount = points,
        };

    public static Dictionary<string, TileMeta> Metas(params TileMeta[] tiles) => tiles.ToDictionary(t => t.TileId);

    public static void WritePredictions(string dir, IEnumerable<Prediction> preds)
    {
        var json = JsonSerializer.Serialize(new
        {
            format = PredictionStore.Format,
            predictions = preds.Select(p => new
            {
                tile_id = p.TileId, class_name = p.ClassName, score = p.Score,
                cx = p.Cx, cy = p.Cy, cz = p.Cz, dx = p.Dx, dy = p.Dy, dz = p.Dz, heading = p.Heading,
            }),
        });
        File.WriteAllText(Path.Combine(dir, PredictionStore.JsonName), json);
    }
}
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PostProcess/PilotConfigTests.cs:1

신규. pilot.yaml 의 tiling·score·size·global_nms(단일/리스트)·cross_class_nms 를 읽는지, 빠진 절은 기본값인지, type 없는 규칙은 예외인지 본다.

before
```csharp
```
after
```csharp
using AbnormalDetection.Config;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PostProcess;

public class PilotConfigTests
{
    [Fact]
    public void Load_ReadsTilingAndPostprocessRules()
    {
        using var dir = new TempDir();

        var c = PilotConfig.Load(dir.Write("pilot.yaml", Boxes.PilotYaml));

        Assert.Equal((50.0, 10.0, 50, 2.0), (c.Tiling.TileSize, c.Tiling.Overlap, c.Tiling.MinPoints, c.Tiling.SeafloorPercentile));
        var pp = c.Postprocess;
        Assert.Equal(0.7, pp.ScoreThreshold["Wreck"]);
        Assert.Equal(((double?)5.0, (double?)null),(pp.SizeFilter["Wreck"].MinMaxSide, pp.SizeFilter["Wreck"].MaxMaxSide));

        var reef = pp.GlobalNms["Fish_Reef"];
        Assert.True(reef.IsList);
        Assert.Equal(((double?)10, "dist", 1.2), (reef.Rules[0].MaxSideLt, reef.Rules[0].Type, reef.Rules[0].Thresh));
        Assert.Equal(((double?)null, "iou", 0.3), (reef.Rules[1].MaxSideLt, reef.Rules[1].Type, reef.Rules[1].Thresh));
        var wreck = pp.GlobalNms["Wreck"];
        Assert.False(wreck.IsList);
        Assert.Equal(("iou", 0.3), (wreck.Rules[0].Type, wreck.Rules[0].Thresh));

        var cross = Assert.Single(pp.CrossClassNms);
        Assert.Equal(new[] { "Fish_Reef", "Block" }, cross.Classes);
        Assert.Equal(((double?)1.2, (double?)0.3), (cross.Dist, cross.Iou));
    }

    [Fact]
    public void Load_MissingSections_UsesDefaults()
    {
        using var dir = new TempDir();

        var c = PilotConfig.Load(dir.Write("pilot.yaml", "input: {path: a.xyz}\n"));

        Assert.Equal("a.xyz", c.InputPath);
        Assert.Null(c.OutputDir);
        Assert.Equal((50.0, 10.0, 50), (c.Tiling.TileSize, c.Tiling.Overlap, c.Tiling.MinPoints));
        Assert.Empty(c.Postprocess.ScoreThreshold);
        Assert.Empty(c.Postprocess.GlobalNms);
    }

    [Fact]
    public void Load_NmsRuleWithoutType_Throws()
    {
        using var dir = new TempDir();
        var path = dir.Write("pilot.yaml", "postprocess:\n  global_nms:\n    Block: {thresh: 0.3}\n");

        Assert.Throws<FormatException>(() => PilotConfig.Load(path));
    }
}
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PostProcess/PredictionStoreTests.cs:1

신규. predictions.json 을 엔진 순서·필드 그대로 읽는지와 형식 불일치 예외를 확인한다.

before
```csharp
```
after
```csharp
using AbnormalDetection.IO;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PostProcess;

public class PredictionStoreTests
{
    [Fact]
    public void Read_KeepsEngineOrderAndFields()
    {
        using var dir = new TempDir();
        Boxes.WritePredictions(dir.Root, new[]
        {
            Boxes.Raw("Wreck", 0.75, cx: -3.5, cy: 4.25, tile: "s_t0001", dx: 8, dy: 6, dz: 2, cz: 1.5, heading: 0.3),
            Boxes.Raw("Fish_Reef", 0.9),
        });

        var preds = PredictionStore.Read(dir.Root);

        Assert.Equal(new[] { "Wreck", "Fish_Reef" }, preds.Select(p => p.ClassName));
        var p = preds[0];
        Assert.Equal(("s_t0001", 0.75, -3.5, 4.25, 1.5), (p.TileId, p.Score, p.Cx, p.Cy, p.Cz));
        Assert.Equal((8.0, 6.0, 2.0, 0.3), (p.Dx, p.Dy, p.Dz, p.Heading));
    }

    [Fact]
    public void Read_OtherFormat_Throws()
    {
        using var dir = new TempDir();
        dir.Write(PredictionStore.JsonName, "{\"format\":\"other/1\",\"predictions\":[]}");

        Assert.Throws<InvalidOperationException>(() => PredictionStore.Read(dir.Root));
    }
}
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PostProcess/PostprocessorTests.cs:1

신규. score → 좌표 복원 → size → 클래스별 NMS(dist/iou, 크기대) → 클래스 간 NMS 각 단계와 Run 단계별 개수, 프레임 분리를 확인한다.

before
```csharp
```
after
```csharp
using AbnormalDetection.Postprocessing;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PostProcess;

public class PostprocessorTests
{
    [Fact]
    public void FilterScore_UsesClassThreshold_DefaultHalf()
    {
        var preds = new[]
        {
            Boxes.Raw("Wreck", 0.69), Boxes.Raw("Wreck", 0.7), Boxes.Raw("Block", 0.5), Boxes.Raw("Unknown", 0.49),
        };

        var kept = Postprocessor.FilterScore(preds, new Dictionary<string, double> { ["Wreck"] = 0.7, ["Block"] = 0.5 });

        Assert.Equal(new[] { preds[1], preds[2] }, kept);
    }

    [Fact]
    public void ToGlobal_AddsTileOriginAndSeafloor()
    {
        var metas = Boxes.Metas(Boxes.Tile("s_t0100", 65, 25, sfZ: -12));

        var g = Assert.Single(Postprocessor.ToGlobal(new[] { Boxes.Raw("Block", 0.8, cx: -3, cy: 4, cz: 1.5, tile: "s_t0100") }, metas));

        Assert.Equal((62.0, 29.0, -10.5), (g.X, g.Y, g.Z));
        Assert.Equal(("s", "local:s", "s_t0100"), (g.Scan, g.Frame, g.TileId));
        Assert.Throws<KeyNotFoundException>(() => Postprocessor.ToGlobal(new[] { Boxes.Raw("Block", 0.8, tile: "none") }, metas));
    }

    [Fact]
    public void FilterSize_UsesMaxHorizontalSide()
    {
        var preds = new[]
        {
            Boxes.At("Wreck", 0.9, 0, 0, dx: 4, dy: 4.9),
            Boxes.At("Wreck", 0.9, 0, 0, dx: 5, dy: 2),
            Boxes.At("Block", 0.9, 0, 0, dx: 1, dy: 1),
        };
        var rules = new Dictionary<string, SizeRule> { ["Wreck"] = new SizeRule { MinMaxSide = 5 } };

        Assert.Equal(new[] { preds[1], preds[2] }, Postprocessor.FilterSize(preds, rules));
    }

    [Fact]
    public void BevIou_UsesRotatedBoundingRectangle()
    {
        var a = Boxes.At("Block", 1, 0, 0, dx: 2, dy: 2);

        Assert.Equal(1.0, Postprocessor.BevIou(a, Boxes.At("Block", 1, 0, 0, dx: 2, dy: 2)), 9);
        Assert.Equal(1.0 / 3, Postprocessor.BevIou(a, Boxes.At("Block", 1, 1, 0, dx: 2, dy: 2)), 9);
        Assert.Equal(0.0, Postprocessor.BevIou(a, Boxes.At("Block", 1, 5, 0, dx: 2, dy: 2)));
        Assert.Equal(1.0, Postprocessor.BevIou(
            Boxes.At("Block", 1, 0, 0, dx: 4, dy: 2, heading: Math.PI / 2),
            Boxes.At("Block", 1, 0, 0, dx: 2, dy: 4)), 9);
    }

    [Fact]
    public void GlobalNms_Dist_KeepsHigherScore_Greedily()
    {
        var preds = new List<GlobalPrediction>
        {
            Boxes.At("Fish_Reef", 0.8, 1.0, 0), Boxes.At("Fish_Reef", 0.9, 0, 0), Boxes.At("Fish_Reef", 0.7, 1.3, 0),
        };
        var rules = new Dictionary<string, NmsRuleSet>
        {
            ["Fish_Reef"] = new NmsRuleSet { Rules = { new NmsRule { Type = "dist", Thresh = 1.2 } } },
        };

        Assert.Equal(new[] { 0.9, 0.7 }, Postprocessor.GlobalNms(preds, rules).Select(g => g.Score));
    }

    [Fact]
    public void GlobalNms_SizeBands_DoNotSuppressEachOther()
    {
        using var dir = new TempDir();
        var preds = new List<GlobalPrediction>
        {
            Boxes.At("Fish_Reef", 0.9, 0, 0),
            Boxes.At("Fish_Reef", 0.8, 0, 0, dx: 30, dy: 30),
            Boxes.At("Fish_Reef", 0.7, 3, 0, dx: 30, dy: 30),
        };

        var kept = Postprocessor.GlobalNms(preds, Boxes.Pilot(dir).GlobalNms);

        Assert.Equal(new[] { 0.9, 0.8 }, kept.Select(g => g.Score));
    }

    [Fact]
    public void GlobalNms_ComparesSameClassOnly_AndDefaultsToDist12()
    {
        var preds = new List<GlobalPrediction>
        {
            Boxes.At("Unknown", 0.9, 0, 0), Boxes.At("Unknown", 0.8, 1.0, 0), Boxes.At("Block", 0.6, 0, 0),
        };

        var kept = Postprocessor.GlobalNms(preds, new Dictionary<string, NmsRuleSet>());

        Assert.Equal(new[] { ("Unknown", 0.9), ("Block", 0.6) }, kept.Select(g => (g.ClassName, g.Score)));
    }

    [Fact]
    public void CrossClassNms_DropsLowerScoreOfOtherClass_ByDistOrIou()
    {
        using var dir = new TempDir();
        var preds = new List<GlobalPrediction>
        {
            Boxes.At("Fish_Reef", 0.9, 0, 0),
            Boxes.At("Block", 0.8, 0.5, 0),
            Boxes.At("Block", 0.85, 10, 0),
            Boxes.At("Wreck", 0.6, 0, 0),
            Boxes.At("Fish_Reef", 0.7, 50, 0, dx: 12, dy: 12),
            Boxes.At("Block", 0.95, 53, 0, dx: 12, dy: 12),
        };

        var kept = Postprocessor.CrossClassNms(preds, Boxes.Pilot(dir).CrossClassNms);

        Assert.Equal(new[] { preds[0], preds[2], preds[3], preds[5] }, kept);
    }

    [Fact]
    public void Run_AppliesStagesInOrder_AndCountsEach()
    {
        using var dir = new TempDir();
        var metas = Boxes.Metas(Boxes.Tile("s_t0000", 25, 25));
        var raw = new[]
        {
            Boxes.Raw("Wreck", 0.65, dx: 8, dy: 6),
            Boxes.Raw("Wreck", 0.9, dx: 4, dy: 4),
            Boxes.Raw("Wreck", 0.8, dx: 8, dy: 6),
            Boxes.Raw("Fish_Reef", 0.9, cx: 5, cy: 5),
            Boxes.Raw("Fish_Reef", 0.8, cx: 5.5, cy: 5),
            Boxes.Raw("Block", 0.7, cx: 5, cy: 5.6),
            Boxes.Raw("Block", 0.6, cx: -10, cy: -10),
        };

        var (final, st) = Postprocessor.Run(raw, metas, Boxes.Pilot(dir));

        Assert.Equal((6, 5, 4, 3), (st.Score, st.Size, st.Nms, st.Cross));
        Assert.Equal(
            new[] { ("Wreck", 0.8, 25.0, 25.0), ("Fish_Reef", 0.9, 30.0, 30.0), ("Block", 0.6, 15.0, 15.0) },
            final.Select(g => (g.ClassName, g.Score, g.X, g.Y)));
    }

    [Fact]
    public void RunByFrame_DoesNotMixScans()
    {
        using var dir = new TempDir();
        var metas = Boxes.Metas(Boxes.Tile("b_t0000", 25, 25, scan: "b"), Boxes.Tile("a_t0000", 25, 25, scan: "a"));
        var raw = new[] { Boxes.Raw("Fish_Reef", 0.9, tile: "b_t0000"), Boxes.Raw("Fish_Reef", 0.8, tile: "a_t0000") };

        var (final, st) = Postprocessor.RunByFrame(raw, metas, Boxes.Pilot(dir));

        Assert.Equal(new[] { "a", "b" }, final.Select(g => g.Scan));
        Assert.Equal((2, 2, 2), (st.Frames, st.Nms, st.Cross));
    }
}
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PostProcess/ResultWriterTests.cs:1

신규. detections.csv 를 Python 과 같은 숫자 표기·\r\n·따옴표로, final.json 에 depth 를, index.csv 에 타일별 최종 개수를 쓰는지 본다.

before
```csharp
```
after
```csharp
using System.Text.Json;
using AbnormalDetection.IO;
using AbnormalDetection.Postprocessing;
using AbnormalDetection.Tiling;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PostProcess;

public class ResultWriterTests
{
    static GlobalPrediction Detection(string scan = "s") => new GlobalPrediction
    {
        Scan = scan, ClassName = "Fish_Reef", Score = 0.9, X = 1.5, Y = 2, Z = -10.25,
        Dx = 3, Dy = 2, Dz = 1, Heading = 0.1, TileId = "s_t0000",
    };

    [Fact]
    public void SaveFinal_WritesCsvLikePython_AndJsonWithDepth()
    {
        using var dir = new TempDir();

        new ResultWriter(dir.Root).SaveFinal(new List<GlobalPrediction> { Detection() }, "local_meters");

        var csv = File.ReadAllText(Path.Combine(dir.Root, "predictions", "final", "detections.csv"));
        Assert.Equal(
            "scan,class_name,score,depth,x,y,z,dx,dy,dz,heading,tile_id\r\n" +
            "s,Fish_Reef,0.9,10.25,1.5,2.0,-10.25,3.0,2.0,1.0,0.1,s_t0000\r\n", csv);
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir.Root, "predictions", "final", "final.json")));
        Assert.Equal("local_meters", json.RootElement.GetProperty("crs").GetString());
        var det = Assert.Single(json.RootElement.GetProperty("detections").EnumerateArray().ToList());
        Assert.Equal(10.25, det.GetProperty("depth").GetDouble());
    }

    [Fact]
    public void SaveFinal_QuotesCommaInCsv()
    {
        using var dir = new TempDir();

        new ResultWriter(dir.Root).SaveFinal(new List<GlobalPrediction> { Detection("a,b") }, "local_meters");

        var lines = File.ReadAllLines(Path.Combine(dir.Root, "predictions", "final", "detections.csv"));
        Assert.StartsWith("\"a,b\",Fish_Reef,", lines[1]);
    }

    [Fact]
    public void WriteIndex_CountsFinalDetectionsPerTile()
    {
        using var dir = new TempDir();
        var tiles = new List<TileMeta>
        {
            Boxes.Tile("s_t0000", 25, 25, points: 100), Boxes.Tile("s_t0001", 25, 65, points: 80),
        };
        var final = new List<GlobalPrediction>
        {
            Boxes.At("Fish_Reef", 0.9, 0, 0), Boxes.At("Fish_Reef", 0.8, 9, 0), Boxes.At("Wreck", 0.7123, 0, 9),
        };

        new ResultWriter(dir.Root).WriteIndex(tiles, final);

        Assert.Equal(new[]
        {
            "scan,tile_id,origin_x,origin_y,point_count,reef_count,block_count,wreck_count,max_score,image_path",
            "s,s_t0000,25.00,25.00,100,2,0,1,0.900,",
            "s,s_t0001,25.00,65.00,80,0,0,0,0.000,",
        }, File.ReadAllLines(Path.Combine(dir.Root, "index.csv")));
    }
}
```

## OceanDetect/OceanDetectAI.Tests/Abnormal/PostProcess/PostprocessFlowTests.cs:1

신규. tiles.json + predictions.json → RunByFrame → ResultWriter 파일 흐름 전체에서 겹침 타일 중복이 지워지고 index 합계가 최종 개수와 같은지 본다.

before
```csharp
```
after
```csharp
using AbnormalDetection.IO;
using AbnormalDetection.Postprocessing;
using AbnormalDetection.Tiling;
using Xunit;

namespace OceanDetect.OceanDetectAI.Tests.Abnormal.PostProcess;

public class PostprocessFlowTests
{
    // 전처리 산출물(tiles.json) + 추론 산출물(predictions.json) → 최종 결과 파일
    [Fact]
    public void Files_ToFinalResult_IndexMatchesFinal()
    {
        using var work = new TempDir();
        using var output = new TempDir();
        var tiles = new Tiler().Tiles(ScanFactory.Grid("s", 100, 100, sfZ: -1)).ToList();
        using (var w = new TileStoreWriter(work.Root))
            foreach (var (meta, pts) in tiles)
                w.Add(meta, pts);
        Boxes.WritePredictions(work.Root, new[]
        {
            Boxes.Raw("Fish_Reef", 0.9, cx: 20, cy: 0, tile: "s_t0000"),
            Boxes.Raw("Fish_Reef", 0.8, cx: -20, cy: 0, tile: "s_t0100"),
            Boxes.Raw("Block", 0.7, cx: 0, cy: 0, tile: "s_t0101"),
            Boxes.Raw("Wreck", 0.6, cx: 0, cy: 0, dx: 8, dy: 6, tile: "s_t0202"),
        });

        var (metas, _) = TileStore.ReadMeta(work.Root);
        var raw = PredictionStore.Read(work.Root);
        var (final, st) = Postprocessor.RunByFrame(raw, metas.ToDictionary(m => m.TileId), Boxes.Pilot(work));
        var writer = new ResultWriter(output.Root);
        writer.SaveFinal(final, "local_meters");
        writer.WriteIndex(metas, final);

        Assert.Equal((1, 3, 3, 2, 2), (st.Frames, st.Score, st.Size, st.Nms, st.Cross));
        Assert.Equal(new[] { ("Fish_Reef", 45.0, 25.0, -1.0), ("Block", 65.0, 65.0, -1.0) },
            final.Select(g => (g.ClassName, g.X, g.Y, g.Z)));
        var index = File.ReadAllLines(Path.Combine(output.Root, "index.csv")).Skip(1).Select(l => l.Split(',')).ToList();
        Assert.Equal(metas.Count, index.Count);
        Assert.Equal(final.Count, index.Sum(r => int.Parse(r[5]) + int.Parse(r[6]) + int.Parse(r[7])));
        Assert.Equal(final.Count + 1, File.ReadAllLines(Path.Combine(output.Root, "predictions", "final", "detections.csv")).Length);
    }
}
```
