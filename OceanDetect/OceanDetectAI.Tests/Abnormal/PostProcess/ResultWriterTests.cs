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
