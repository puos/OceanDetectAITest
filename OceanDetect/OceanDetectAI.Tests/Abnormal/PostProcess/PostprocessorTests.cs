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
