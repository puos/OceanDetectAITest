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
