# abnormal_detection → PCD.OceanDetectAI 이식 (LidarRecon.RMAI\AI 형태, 2026-10-30)

## 배경

- [abnormal_detection-분석](abnormal_detection-분석.md) §4 의 방안대로, 이상체 탐지(어초·블럭·침선)의 전처리·후처리 C# 코드를 이 레포에 넣는다.
- 형태는 `D:\work\LaneAITest\LidarRecon\LidarRecon.RMAI\AI` 를 따른다: `AI/RunAI.cs` + `CudaEnvironment` + `AI/Onnx/<모델>/` + `AI/Production/<Facility>/<모듈>/{PreProcess, Inference, PostProcess}` + 세 단계를 묶는 `<Facility><모듈>Pipeline.Run(..., token, onProgress)`.
- 다음 작업은 `D:\work\Ocean_Pilot\seafloor_seg`(Volt 암반, ONNX 있음)이다. 아직 준비가 안 됐다고 해서 이번에는 자리만 둔다.

## 결정

| 항목 | 결정 | 근거 |
|---|---|---|
| 추론 | **C# 골격만**. `AbnormalTileInference.Infer` 는 `NotSupportedException`. 입력(`tiles.*`)과 출력(`predictions.json`) 파일 계약은 구현 | 사용자 결정. CenterPoint 는 ONNX 가 없고 spconv 라 LANE 방식(C# sparse 맵 + backbone ONNX) 변환이 먼저 필요 |
| 위치 | `PCD/PCD.OceanDetectAI` | 사용자 결정 |
| 로그 | `D:\work\PCDAITest\PCD\PCD.Common` ProjectReference (LidarRecon.Common 과 같은 코드, `NeoCore.Logging.TraceUtil`). sln 에는 넣지 않음 | 사용자 결정 |
| AI 골격 | RMAI 의 `RunAI` · `CudaEnvironment`(4개) 를 그대로 복사, `Microsoft.ML.OnnxRuntime.Gpu 1.23.2` · `System.Management` 포함 | 사용자 결정 (RMAI 형태를 가져온다) |
| 전·후처리 코드 | abnormal_detection Core 를 **복사**. 바꾼 것은 `#nullable disable`, namespace 를 `PCD.AI.Abnormal` 하나로, `using AbnormalDetection.*` 삭제, `Tiling.TilingConfig` → `TilingConfig`, PilotConfig 의 namespace 블록 2개 병합뿐 | Python 과 바이트 일치 검증된 코드라 로직은 건드리지 않는다 |
| 이름 | Facility `OceanFacility`, 모듈 `Abnormal`, 진입점 `OceanFacilityAbnormalPipeline` (namespace `PCD.AI.Production.OceanFacility`), 모듈 namespace `PCD.AI.Abnormal` | RMAI `LaneFacility` / `LidarRecon.AI.LaneLine` 대응. seafloor 는 `OceanFacilitySeafloorPipeline` · `PCD.AI.Seafloor` 로 붙인다 |
| 설정 | `AI/Onnx/ABNORMAL/pilot.yaml` — 원본의 tiling · postprocess 절만. 모델 `.onnx` 도 나중에 이 폴더 | SAM3 manifest 처럼 모델 폴더에 설정. input/output/model 절은 C# 파이프라인에서 쓰지 않음 |
| 작업 폴더 | `workDirectory/{prep, infer, post}`, 단계마다 비우고 쓴다 | RMAI 파이프라인과 같음 |
| 추론 순서 | 모델(클래스) 바깥 · 타일 안쪽 | 후처리 결과가 predictions 순서에 의존(`docs/interface.md`) |
| 접근성 | 파이프라인 `internal static`, 단계 메서드도 `internal`, csproj `InternalsVisibleTo PCD.OceanDetectAI.Tests` | RMAI 는 internal 이라 테스트로 부를 수 없었다. 단계별 검증을 위해 열어 둠 |
| 범위 밖 | SHP/GeoJSON 내보내기, 실좌표(EPSG) 복원, 이미지(`render.py`), CLI | 분석 문서 §4 "새로 작성" 항목. CLI 의 단계 조합은 파이프라인으로 옮김 |

## 변경

Before/After(적용은 사용자): [doc/code/abnormal-ai-port.md](../code/abnormal-ai-port.md) — 34개 항목, 문서 앞의 "적용 순서"대로.

- 신규: `OceanFacilityAbnormalPipeline.cs`(항목 1, 2, 5, 13, 18, 25), `AbnormalTileInference.cs`(14 ~ 17), `pilot.yaml`(31), `AbnormalPipelineTests.cs`(34)
- 복사 후 수정: 원본 Core 14개 파일(3, 4, 6 ~ 12, 19 ~ 24), RMAI 5개 파일(26 ~ 30)
- 프로젝트: `PCD.OceanDetectAI.csproj`(32), `PCD.OceanDetectAI.Tests.csproj`(33)

## 현황

| # | 항목 | 상태 |
|---|---|---|
| 1 | Before/After 작성 | 완료 |
| 2 | 스크래치 적용본 빌드 (`PCD.OceanDetectAI.Tests`, Release x64) | 오류 0. 경고 42개는 모두 PCD.Common 기존 코드(nullable), 이식 코드 경고 0 |
| 3 | xUnit 3개 (스크래치) | 통과 |
| 4 | 원본 CLI 와 동등성 (스크래치) | 전처리·후처리 모두 바이트 동일 (아래 검증) |
| 5 | 레포 적용 | 사용자 |
| 6 | CenterPoint ONNX 추론 | 미착수 — 모델 변환 필요 |

## 검증 (2026-10-30, 스크래치 적용본)

| 항목 | 방법 | 결과 |
|---|---|---|
| 전처리 | `samples/test01.xyz`(1,906,727 점) — 원본 `AbnormalDetection.Cli preprocess` vs `RunPreprocess` | 타일 63개, `tiles.bin`(2,846,483 행)·`tiles.json` 바이트 동일 |
| 후처리 | 위 tiles 에 합성 raw 396건(고정 시드 7, 근접 중복 포함) — 원본 CLI `postprocess` vs `RunPostprocess` | `final.json`·`detections.csv`·`index.csv` 바이트 동일. score 164 → size 161 → 클래스별 NMS 144 → 클래스 간 NMS 142 |
| `Preprocess_WritesTileLocalPoints` | 타일 ≥ 50점, \|x\|,\|y\| ≤ 25, intensity 0, bin 길이 = Σpoint_count×16 | 통과 (max \|xy\| = 25) |
| `Postprocess_KeepsScoresAboveThreshold` | 합성 예측 후처리, 남은 박스 score ≥ 클래스 임계값 | 통과 (최종 142건) |
| `Run_StopsAtInference_WithoutOnnx` | 전체 실행이 추론 단계에서 `NotSupportedException` | 통과 |

- `meta.json` 은 비교에서 뺐다(입력 경로 표기·소요 시간이 실행마다 다름).
- `RunAI.EnsureInitialized` 는 PC 의 CUDA 12.8 설치 여부에 따라 결과가 달라 테스트하지 않았다.
- 적용 후 확인: `dotnet test PCD/PCD.OceanDetectAI.Tests -c Release -p:Platform=x64` 에서 3개 통과.

## 위험 · 확인 필요

- PCD.Common 을 `..\..\..\PCDAITest\...` 상대경로로 참조한다. 이 레포만 받는 PC 에서는 빌드가 깨진다(PCDAITest 를 같은 상위 폴더에 두어야 함).
- CenterPoint ONNX 변환: VoxelResBackBone8x 가 spconv 라 LANE 처럼 C# sparse 맵 + backbone ONNX(+ BEV·head) 로 나눠야 한다. MeanVFE(voxel 평균) · HeightCompression 도 C# 또는 ONNX 로 옮겨야 하고, CenterHead 디코드·dist_nms 는 Python 과 같은 규칙이 필요. 변환 담당·일정 확인 필요.
- 분석 문서 §5 의 미정 사항(해저면 기준, 출력 좌표계, SHP 형태)은 그대로 남아 있다.

## 다음 작업

- seafloor_seg(Volt): `AI/Production/OceanFacility/Seafloor/{PreProcess, Inference, PostProcess}` + `AI/Onnx/SEAFLOOR/volt_seabed.onnx` + `OceanFacilitySeafloorPipeline`. 이미 ONNX 라 RMAI `LaneOnnxSessions` 같은 세션 코드로 바로 추론할 수 있다. 법선(k=30 PCA)·타일·병합은 Python(`inference/normals.py`, `tiler.py`, `result.py`)에서 옮긴다. 준비되면 착수.
- .pcd/.xyz 읽기(`ScanReader` 등)를 seafloor 와 같이 쓰게 되면 그때 `OceanFacility` 공통 폴더로 옮긴다.
