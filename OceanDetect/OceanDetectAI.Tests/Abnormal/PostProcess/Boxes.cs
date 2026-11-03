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
