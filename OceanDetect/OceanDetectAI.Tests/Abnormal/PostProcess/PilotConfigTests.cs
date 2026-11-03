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
