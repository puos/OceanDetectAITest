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
