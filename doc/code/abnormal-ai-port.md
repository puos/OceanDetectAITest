# abnormal_detection → PCD.OceanDetectAI 이식 (LidarRecon.RMAI\AI 형태)

계획·검증: [doc/plan/abnormal-ai-port.md](../plan/abnormal-ai-port.md)

## 목차 (호출 트리)

- `OceanFacilityAbnormalPipeline` 결과·진행률 record — 항목 1
- `OceanFacilityAbnormalPipeline.Run` — 2
  - `PilotConfig.Load` (`AbnormalConfig.cs`) — 3, 4
  - `RunPreprocess` — 5
    - `ScanReader` → `PcdReader` · `XyzReader` · `Lzf` · `NumpyCompat` — 6 ~ 10
    - `Tiler` — 11
    - `TileStoreWriter` — 12
  - `RunInference` — 13
    - `AbnormalTileInference.ReadInput` — 14
    - `AbnormalModelFiles.Load` — 15
    - `AbnormalTileInference.Infer` (골격) — 16
    - `AbnormalTileInference.Write` — 17
  - `RunPostprocess` — 18
    - `PredictionStore.Read` — 19
    - `Postprocessor.RunByFrame` (`Predictions` · `PostprocessConfig`) — 20 ~ 22
    - `ResultWriter` — 23, 24
  - `ResetDirectory` — 25
- 앱 시작 `RunAI.EnsureInitialized` — 26
  - `CudaEnvironment.Initialize` → `CheckCuda` · `ConfigureNativePaths` · `Warmup` — 27 ~ 29
  - `CudaCheckResult` — 30
- 실행 리소스·빌드: `pilot.yaml` 31, `PCD.OceanDetectAI.csproj` 32
- 테스트: `PCD.OceanDetectAI.Tests.csproj` 33, `AbnormalPipelineTests` 34

## 적용 순서

1. 원본 복사 (PowerShell). 복사한 파일은 아래 항목에서 바뀌는 줄만 고친다.

```powershell
$src = 'D:\work\Ocean_Pilot\abnormal_detection\src\AbnormalDetection.Core'
$rm  = 'D:\work\LaneAITest\LidarRecon\LidarRecon.RMAI\AI'
$dst = 'D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI\AI'
$ab  = "$dst\Production\OceanFacility\Abnormal"
New-Item -ItemType Directory -Force "$dst\Onnx\ABNORMAL", "$ab\PreProcess", "$ab\Inference", "$ab\PostProcess" | Out-Null
Copy-Item "$rm\RunAI.cs", "$rm\CudaCheckResult.cs", "$rm\CudaEnvironment.cs", "$rm\CudaEnvironment.Configure.cs", "$rm\CudaEnvironment.Detect.cs" $dst
Copy-Item "$src\Pcd\Lzf.cs", "$src\Pcd\PcdReader.cs", "$src\Pcd\XyzReader.cs", "$src\Scans\NumpyCompat.cs", "$src\Scans\ScanReader.cs", "$src\Tiling\Tiler.cs", "$src\IO\TileStore.cs" "$ab\PreProcess"
Copy-Item "$src\IO\PredictionStore.cs" "$ab\Inference"
Copy-Item "$src\Postprocessing\Predictions.cs", "$src\Postprocessing\PostprocessConfig.cs", "$src\Postprocessing\Postprocessor.cs", "$src\IO\ResultWriter.cs" "$ab\PostProcess"
Copy-Item "$src\Config\PilotConfig.cs" "$ab\AbnormalConfig.cs"
```

2. 32 csproj (패키지·PCD.Common 참조가 있어야 빌드된다), 31 pilot.yaml
3. 복사 파일 수정: 3, 4, 6 ~ 12, 19 ~ 24, 26 ~ 30
4. 신규 파일: 1, 2, 5, 13, 18, 25 (이어 붙이면 `OceanFacilityAbnormalPipeline.cs` 전체), 15, 14, 16, 17 (이어 붙이면 `AbnormalTileInference.cs` 전체)
5. 테스트: 33, 34

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/OceanFacilityAbnormalPipeline.cs:1

신규. 파이프라인 결과·진행률 record와 단계 가중치(RMAI LaneFacilityLanePipeline 과 같은 모양).

before
```csharp

```
after
```csharp
using NeoCore.Logging;
using PCD.AI.Abnormal;
using System.Diagnostics;

namespace PCD.AI.Production.OceanFacility;

public sealed record OceanFacilityAbnormalResult(IReadOnlyList<GlobalPrediction> Detections, string FinalJsonPath);
public sealed record OceanFacilityAbnormalProgress(double Fraction, string Message);

internal static class OceanFacilityAbnormalPipeline
{
    private const double PreprocessWeight = 1;
    private const double InferenceWeight = 1;
    private const double PostprocessWeight = 1;
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/OceanFacilityAbnormalPipeline.cs:16

신규. 진입점 — pilot.yaml 을 읽고 prep → infer → post 를 순서대로 돌린다.

before
```csharp

```
after
```csharp
    internal static OceanFacilityAbnormalResult Run(
        string inputPath,
        string workDirectory,
        CancellationToken token,
        Action<OceanFacilityAbnormalProgress>? onProgress)
    {
        var stopwatch = Stopwatch.StartNew();
        var totalWeight = PreprocessWeight + InferenceWeight + PostprocessWeight;
        var modelDirectory = Path.Combine(AppContext.BaseDirectory, "AI", "Onnx", "ABNORMAL");
        var config = PilotConfig.Load(Path.Combine(modelDirectory, "pilot.yaml"));
        var prepDirectory = Path.Combine(workDirectory, "prep");
        var inferDirectory = Path.Combine(workDirectory, "infer");
        var postDirectory = Path.Combine(workDirectory, "post");

        onProgress?.Invoke(new OceanFacilityAbnormalProgress(0, "Stage 1/3 · Preprocess"));
        RunPreprocess(inputPath, config.Tiling, prepDirectory, token);
        token.ThrowIfCancellationRequested();

        RunInference(prepDirectory, inferDirectory, modelDirectory, token, (done, count) => onProgress?.Invoke(new OceanFacilityAbnormalProgress(
            (PreprocessWeight + InferenceWeight * done / count) / totalWeight,
            $"Stage 2/3 · CenterPoint (tile {done + 1}/{count})")));
        token.ThrowIfCancellationRequested();

        var postLabel = "Stage 3/3 · Postprocess";
        onProgress?.Invoke(new OceanFacilityAbnormalProgress((PreprocessWeight + InferenceWeight) / totalWeight, postLabel));
        var result = RunPostprocess(inputPath, config, prepDirectory, inferDirectory, postDirectory, stopwatch, token);
        onProgress?.Invoke(new OceanFacilityAbnormalProgress(1.0, postLabel));

        return result;
    }
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/AbnormalConfig.cs:1

복사본. nullable 끄고 namespace 를 PCD.AI.Abnormal 로, AbnormalDetection using 삭제.

before
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AbnormalDetection.Postprocessing;
using AbnormalDetection.Tiling;
using YamlDotNet.Serialization;

namespace AbnormalDetection.Tiling
```
after
```csharp
#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using YamlDotNet.Serialization;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/AbnormalConfig.cs:16

두 namespace 블록(Tiling·Config)을 하나로 합친다.

before
```csharp
    }
}

namespace AbnormalDetection.Config
{
    /// <summary>
```
after
```csharp
    }

    /// <summary>
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/OceanFacilityAbnormalPipeline.cs:47

신규. CLI Preprocess 조합 — 스캔 읽기 → 50m 타일 → tiles.json/bin.

before
```csharp

```
after
```csharp
    internal static void RunPreprocess(string inputPath, TilingConfig tiling, string prepDirectory, CancellationToken token)
    {
        ResetDirectory(prepDirectory);

        var reader = new ScanReader(inputPath, tiling.SeafloorPercentile, TraceUtil.WriteLine);
        var tiler = new Tiler(tiling.TileSize, tiling.Overlap, tiling.MinPoints);
        using (var store = new TileStoreWriter(prepDirectory))
        {
            foreach (var scan in reader.Read())
            {
                token.ThrowIfCancellationRequested();
                var before = tiler.StatTiles;
                foreach (var (meta, points) in tiler.Tiles(scan))
                    store.Add(meta, points);
                store.Scans.Add(new ScanInfo
                {
                    Scan = scan.Stem, File = scan.Path, Points = scan.Points,
                    SeafloorZ = scan.SfZ, Frame = scan.Frame, Tiles = tiler.StatTiles - before,
                    OffsetX = scan.OriginX, OffsetY = scan.OriginY,
                });
            }
        }

        TraceUtil.WriteLine($"[Abnormal ①] scans={tiler.StatScans}, points={tiler.StatPoints}, tiles={tiler.StatTiles}, dropped={tiler.StatTilesDropped}, dir={prepDirectory}");
    }
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PreProcess/ScanReader.cs:1

복사본. nullable 끄고 namespace 통합, using 삭제.

before
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AbnormalDetection.Pcd;

namespace AbnormalDetection.Scans
```
after
```csharp
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PreProcess/PcdReader.cs:1

복사본. nullable 끄고 namespace 통합.

before
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AbnormalDetection.Pcd
```
after
```csharp
#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PreProcess/XyzReader.cs:1

복사본. nullable 끄고 namespace 통합.

before
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AbnormalDetection.Pcd
```
after
```csharp
#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PreProcess/Lzf.cs:1

복사본. nullable 끄고 namespace 통합.

before
```csharp
using System;

namespace AbnormalDetection.Pcd
```
after
```csharp
#nullable disable
using System;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PreProcess/NumpyCompat.cs:1

복사본. nullable 끄고 namespace 통합.

before
```csharp
using System;
using System.Globalization;

namespace AbnormalDetection
```
after
```csharp
#nullable disable
using System;
using System.Globalization;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PreProcess/Tiler.cs:1

복사본. nullable 끄고 namespace 통합, using 삭제.

before
```csharp
using System.Collections.Generic;
using AbnormalDetection.Scans;

namespace AbnormalDetection.Tiling
```
after
```csharp
#nullable disable
using System.Collections.Generic;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PreProcess/TileStore.cs:1

복사본. nullable 끄고 namespace 통합, using 삭제.

before
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using AbnormalDetection.Tiling;

namespace AbnormalDetection.IO
```
after
```csharp
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/OceanFacilityAbnormalPipeline.cs:73

신규. 모델(클래스) 바깥·타일 안쪽 순서로 추론해 predictions.json 을 쓴다(후처리가 이 순서에 의존).

before
```csharp

```
after
```csharp
    internal static void RunInference(string prepDirectory, string inferDirectory, string modelDirectory, CancellationToken token, Action<int, int> onTile)
    {
        var tiles = AbnormalTileInference.ReadInput(prepDirectory);
        ResetDirectory(inferDirectory);

        var models = AbnormalModelFiles.Load(modelDirectory).ByClass;
        var predictions = new List<Prediction>();
        var count = models.Count * tiles.Count;
        for (var model = 0; model < models.Count; model++)
            for (var tile = 0; tile < tiles.Count; tile++)
            {
                token.ThrowIfCancellationRequested();
                onTile(model * tiles.Count + tile, count);

                var (className, modelPath) = models[model];
                predictions.AddRange(AbnormalTileInference.Infer(className, modelPath, tiles[tile].Meta, tiles[tile].Points, token));
            }

        AbnormalTileInference.Write(inferDirectory, predictions);
        TraceUtil.WriteLine($"[Abnormal ②] tiles={tiles.Count}, models={models.Count}, raw={predictions.Count}, dir={inferDirectory}");
    }
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/Inference/AbnormalTileInference.cs:21

신규. tiles.json + tiles.bin 을 타일별 (TileMeta, N×4) 로 되읽는다.

before
```csharp

```
after
```csharp
public static class AbnormalTileInference
{
    public static IReadOnlyList<(TileMeta Meta, float[] Points)> ReadInput(string directory)
    {
        var (tiles, _) = TileStore.ReadMeta(directory);
        var points = MemoryMarshal.Cast<byte, float>(File.ReadAllBytes(Path.Combine(directory, TileStoreWriter.BinName)));
        var total = tiles.Sum(meta => (long)meta.PointCount);
        if (points.Length != total * 4)
            throw new InvalidDataException($"tiles.bin/tiles.json mismatch : {points.Length / 4} != {total}");

        var result = new List<(TileMeta, float[])>(tiles.Count);
        var offset = 0;
        foreach (var meta in tiles)
        {
            result.Add((meta, points.Slice(offset * 4, meta.PointCount * 4).ToArray()));
            offset += meta.PointCount;
        }

        return result;
    }
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/Inference/AbnormalTileInference.cs:1

신규. 클래스별 CenterPoint ONNX 경로(LaneModelFiles 처럼 경로만 만든다).

before
```csharp

```
after
```csharp
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PCD.AI.Abnormal;

public sealed record AbnormalModelFiles(string ReefPath, string BlockPath, string WreckPath)
{
    public static AbnormalModelFiles Load(string directory) => new(
        Path.Combine(directory, "reef_det_model.onnx"),
        Path.Combine(directory, "block_det_model.onnx"),
        Path.Combine(directory, "shipwreck_det_model.onnx"));

    public IReadOnlyList<(string ClassName, string ModelPath)> ByClass => new[]
    {
        ("Fish_Reef", ReefPath),
        ("Block", BlockPath),
        ("Wreck", WreckPath)
    };
}
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/Inference/AbnormalTileInference.cs:42

신규 골격. CenterPoint ONNX 가 없어 NotSupportedException — 모델이 오면 이 메서드만 채운다.

before
```csharp

```
after
```csharp
    public static IReadOnlyList<Prediction> Infer(string className, string modelPath, TileMeta meta, float[] points, CancellationToken token) =>
        throw new NotSupportedException($"CenterPoint ONNX 모델이 아직 없습니다 : {className}, {modelPath}");
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/Inference/AbnormalTileInference.cs:45

신규. Python run_tiles.py 와 같은 predictions.json(abnormal_detection.predictions/1) 을 쓴다.

before
```csharp

```
after
```csharp
    public static void Write(string directory, IReadOnlyList<Prediction> predictions)
    {
        Directory.CreateDirectory(directory);
        using var stream = new FileStream(Path.Combine(directory, PredictionStore.JsonName), FileMode.Create);
        using var writer = new Utf8JsonWriter(stream, Json.Options);
        writer.WriteStartObject();
        writer.WriteString("format", PredictionStore.Format);
        writer.WriteStartArray("predictions");
        foreach (var p in predictions)
        {
            writer.WriteStartObject();
            writer.WriteString("class_name", p.ClassName);
            writer.WriteNumber("score", p.Score);
            writer.WriteNumber("cx", p.Cx);
            writer.WriteNumber("cy", p.Cy);
            writer.WriteNumber("cz", p.Cz);
            writer.WriteNumber("dx", p.Dx);
            writer.WriteNumber("dy", p.Dy);
            writer.WriteNumber("dz", p.Dz);
            writer.WriteNumber("heading", p.Heading);
            writer.WriteString("tile_id", p.TileId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/OceanFacilityAbnormalPipeline.cs:95

신규. CLI Postprocess 조합 — 좌표 복원·게이트·NMS → final.json·detections.csv·index.csv·meta.json.

before
```csharp

```
after
```csharp
    internal static OceanFacilityAbnormalResult RunPostprocess(string inputPath, PilotConfig config, string prepDirectory, string inferDirectory, string postDirectory, Stopwatch stopwatch, CancellationToken token)
    {
        ResetDirectory(postDirectory);

        var (tiles, scans) = TileStore.ReadMeta(prepDirectory);
        var raw = PredictionStore.Read(inferDirectory);
        token.ThrowIfCancellationRequested();
        var (final, stats) = Postprocessor.RunByFrame(raw, tiles.ToDictionary(meta => meta.TileId), config.Postprocess);

        var writer = new ResultWriter(postDirectory);
        writer.SaveFinal(final, ScanReader.LocalCrs);
        writer.WriteIndex(tiles, final);
        writer.WriteMeta(inputPath, scans, config.Tiling, tiles.Count, raw.Count, stats, final, ScanReader.LocalCrs, stopwatch.Elapsed.TotalSeconds);

        TraceUtil.WriteLine($"[Abnormal ③] frames={stats.Frames}, score={stats.Score}, size={stats.Size}, nms={stats.Nms}, cross={stats.Cross}, final={final.Count}, dir={postDirectory}");

        return new OceanFacilityAbnormalResult(final, Path.Combine(postDirectory, "predictions", "final", "final.json"));
    }
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/Inference/PredictionStore.cs:1

복사본. nullable 끄고 namespace 통합, using 삭제.

before
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AbnormalDetection.Postprocessing;

namespace AbnormalDetection.IO
```
after
```csharp
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PostProcess/Predictions.cs:1

복사본. nullable 끄고 namespace 통합.

before
```csharp
namespace AbnormalDetection.Postprocessing
```
after
```csharp
#nullable disable
namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PostProcess/PostprocessConfig.cs:1

복사본. nullable 끄고 namespace 통합.

before
```csharp
using System.Collections.Generic;

namespace AbnormalDetection.Postprocessing
```
after
```csharp
#nullable disable
using System.Collections.Generic;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PostProcess/Postprocessor.cs:1

복사본. nullable 끄고 namespace 통합, using 삭제.

before
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AbnormalDetection.Tiling;

namespace AbnormalDetection.Postprocessing
```
after
```csharp
#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PostProcess/ResultWriter.cs:1

복사본. nullable 끄고 namespace 통합, using 삭제.

before
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AbnormalDetection.Postprocessing;
using AbnormalDetection.Tiling;

namespace AbnormalDetection.IO
```
after
```csharp
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PCD.AI.Abnormal
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/Abnormal/PostProcess/ResultWriter.cs:107

namespace 통합으로 Tiling. 접두어가 필요 없다.

before
```csharp
        public void WriteMeta(string input, List<ScanInfo> scans, Tiling.TilingConfig tiling,
```
after
```csharp
        public void WriteMeta(string input, List<ScanInfo> scans, TilingConfig tiling,
```

## PCD/PCD.OceanDetectAI/AI/Production/OceanFacility/OceanFacilityAbnormalPipeline.cs:114

신규. 단계 폴더를 비우고 다시 만든다(RMAI 파이프라인과 같음).

before
```csharp

```
after
```csharp
    private static void ResetDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
        Directory.CreateDirectory(directory);
    }
}
```

## PCD/PCD.OceanDetectAI/AI/RunAI.cs:1

RMAI 복사본. namespace PCD.AI, 이 프로젝트에 없는 OpenCvSharp·WPF using 삭제.

before
```csharp
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LidarRecon.AI;
```
after
```csharp
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PCD.AI;
```

## PCD/PCD.OceanDetectAI/AI/CudaEnvironment.cs:1

RMAI 복사본. namespace PCD.AI.

before
```csharp
using NeoCore.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LidarRecon.AI;
```
after
```csharp
using NeoCore.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCD.AI;
```

## PCD/PCD.OceanDetectAI/AI/CudaEnvironment.Detect.cs:1

RMAI 복사본. namespace PCD.AI.

before
```csharp
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace LidarRecon.AI;
```
after
```csharp
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace PCD.AI;
```

## PCD/PCD.OceanDetectAI/AI/CudaEnvironment.Configure.cs:1

RMAI 복사본. namespace PCD.AI.

before
```csharp
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LidarRecon.AI;
```
after
```csharp
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PCD.AI;
```

## PCD/PCD.OceanDetectAI/AI/CudaCheckResult.cs:1

RMAI 복사본. namespace PCD.AI.

before
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LidarRecon.AI;
```
after
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PCD.AI;
```

## PCD/PCD.OceanDetectAI/AI/Onnx/ABNORMAL/pilot.yaml:1

신규. 원본 config/pilot.yaml 의 tiling·postprocess 절만(모델 폴더에 설정을 둔다).

before
```yaml

```
after
```yaml
# 해저이상체 탐지 — 전처리·후처리 설정 (abnormal_detection config/pilot.yaml 의 tiling · postprocess 절)

tiling:
  tile_size_m: 50
  overlap_m: 10               # stride 40
  min_points_per_tile: 50
  seafloor_percentile: 2      # 해저면 기준 z

postprocess:
  # 적용 순서: score → size → 클래스별 전역 NMS → 클래스 간 NMS
  score_threshold:
    Fish_Reef: 0.5
    Block: 0.5
    Wreck: 0.7

  size_filter:            # 최대 수평변 max(dx, dy) 기준 [m]
    Wreck: {min_max_side: 5.0}

  global_nms:             # 리스트면 위에서부터 max_side_lt 로 선택. 대역 간에는 억제하지 않는다.
    Fish_Reef:
      - {max_side_lt: 10, type: dist, thresh: 1.2}
      - {type: iou,  thresh: 0.3}
    Block:
      - {max_side_lt: 10, type: dist, thresh: 1.2}
      - {type: iou,  thresh: 0.3}
    Wreck:     {type: iou,  thresh: 0.3}
  cross_class_nms:        # 서로 다른 클래스가 같은 물체를 잡으면 점수 높은 하나만
    - classes: [Fish_Reef, Block]
      dist: 1.2
      iou:  0.3
```

## PCD/PCD.OceanDetectAI/PCD.OceanDetectAI.csproj:1

ONNX GPU·WMI·YAML 패키지, PCD.Common 참조, 테스트 internal 공개, ABNORMAL 폴더 출력 복사.

before
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Platforms>AnyCPU;x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RootNamespace>PCD</RootNamespace>
  </PropertyGroup>
</Project>
```
after
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Platforms>AnyCPU;x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RootNamespace>PCD</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.ML.OnnxRuntime.Gpu" Version="1.23.2" />
    <PackageReference Include="System.Management" Version="8.0.0" />
    <PackageReference Include="YamlDotNet" Version="16.1.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\PCDAITest\PCD\PCD.Common\PCD.Common.csproj" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="PCD.OceanDetectAI.Tests" />
  </ItemGroup>

  <ItemGroup>
    <None Include="AI\Onnx\ABNORMAL\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

## PCD/PCD.OceanDetectAI.Tests/PCD.OceanDetectAI.Tests.csproj:22

테스트 입력으로 samples/test01.xyz 를 링크해 출력 폴더 input/ 에 복사.

before
```xml
  <ItemGroup>
    <None Update="input\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```
after
```xml
  <ItemGroup>
    <None Update="input\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\..\samples\test01.xyz" Link="input\test01.xyz">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
```

## PCD/PCD.OceanDetectAI.Tests/AbnormalPipelineTests.cs:1

신규. 전처리 불변식, 합성 예측 후처리, 추론 골격 예외를 확인한다.

before
```csharp

```
after
```csharp
using PCD.AI.Abnormal;
using PCD.AI.Production.OceanFacility;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace PCD.OceanDetectAI.Tests;

public class AbnormalPipelineTests
{
    private static readonly string InputPath = Path.Combine(AppContext.BaseDirectory, "input", "test01.xyz");
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "AI", "Onnx", "ABNORMAL", "pilot.yaml");

    private readonly ITestOutputHelper _output;

    public AbnormalPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Preprocess_WritesTileLocalPoints()
    {
        var prep = Path.Combine(WorkDirectory("preprocess"), "prep");
        OceanFacilityAbnormalPipeline.RunPreprocess(InputPath, PilotConfig.Load(ConfigPath).Tiling, prep, CancellationToken.None);

        var tiles = AbnormalTileInference.ReadInput(prep);
        var maxAbsXy = 0f;
        var maxAbsIntensity = 0f;
        foreach (var (meta, points) in tiles)
        {
            Assert.True(meta.PointCount >= Tiler.MinPoints);
            for (var i = 0; i < points.Length; i += 4)
            {
                maxAbsXy = Math.Max(maxAbsXy, Math.Max(Math.Abs(points[i]), Math.Abs(points[i + 1])));
                maxAbsIntensity = Math.Max(maxAbsIntensity, Math.Abs(points[i + 3]));
            }
        }

        _output.WriteLine($"[OUTPUT] tiles={tiles.Count}, points={tiles.Sum(tile => (long)tile.Meta.PointCount)}, max_abs_xy={maxAbsXy}, dir={prep}");
        Assert.NotEmpty(tiles);
        Assert.True(maxAbsXy <= 25f);
        Assert.Equal(0f, maxAbsIntensity);
    }

    [Fact]
    public void Postprocess_KeepsScoresAboveThreshold()
    {
        var work = WorkDirectory("postprocess");
        var prep = Path.Combine(work, "prep");
        var infer = Path.Combine(work, "infer");
        var config = PilotConfig.Load(ConfigPath);
        OceanFacilityAbnormalPipeline.RunPreprocess(InputPath, config.Tiling, prep, CancellationToken.None);
        AbnormalTileInference.Write(infer, SyntheticPredictions(TileStore.ReadMeta(prep).Tiles, seed: 7));

        var result = OceanFacilityAbnormalPipeline.RunPostprocess(InputPath, config, prep, infer, Path.Combine(work, "post"), Stopwatch.StartNew(), CancellationToken.None);

        _output.WriteLine($"[OUTPUT] final={result.Detections.Count}, file={result.FinalJsonPath}");
        Assert.True(File.Exists(result.FinalJsonPath));
        Assert.NotEmpty(result.Detections);
        Assert.All(result.Detections, detection => Assert.True(detection.Score >= config.Postprocess.ScoreThreshold[detection.ClassName]));
    }

    [Fact]
    public void Run_StopsAtInference_WithoutOnnx()
    {
        Assert.Throws<NotSupportedException>(() => OceanFacilityAbnormalPipeline.Run(InputPath, WorkDirectory("run"), CancellationToken.None, null));
    }

    private static string WorkDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "output", "abnormal", name);

    private static List<Prediction> SyntheticPredictions(IReadOnlyList<TileMeta> tiles, int seed)
    {
        var random = new Random(seed);
        var predictions = new List<Prediction>();
        foreach (var className in new[] { "Fish_Reef", "Block", "Wreck" })
            foreach (var tile in tiles)
                for (var count = random.Next(4); count > 0; count--)
                {
                    var box = new Prediction
                    {
                        ClassName = className,
                        TileId = tile.TileId,
                        Score = random.NextDouble(),
                        Cx = random.NextDouble() * 50 - 25,
                        Cy = random.NextDouble() * 50 - 25,
                        Cz = random.NextDouble() * 3,
                        Dx = 1 + random.NextDouble() * (className == "Wreck" ? 30 : 15),
                        Dy = 1 + random.NextDouble() * 10,
                        Dz = 0.5 + random.NextDouble() * 2,
                        Heading = random.NextDouble() * 2 * Math.PI - Math.PI
                    };
                    predictions.Add(box);
                    if (random.Next(2) == 0)
                        predictions.Add(new Prediction
                        {
                            ClassName = box.ClassName, TileId = box.TileId, Score = random.NextDouble(),
                            Cx = box.Cx + random.NextDouble() - 0.5, Cy = box.Cy + random.NextDouble() - 0.5, Cz = box.Cz,
                            Dx = box.Dx, Dy = box.Dy, Dz = box.Dz, Heading = box.Heading
                        });
                }

        return predictions;
    }
}
```

