# abnormal 단위테스트 — 2차 후처리 (2026-11-03)

## 배경

- [abnormal-preprocess-tests](abnormal-preprocess-tests.md) 에서 테스트를 `Abnormal/{PreProcess, Inference, PostProcess}` 로 나누고 전처리를 채웠다. 이번에는 PostProcess 를 채운다.
- 대상은 NeoOceanAICore 의 원본 Core 클래스다: `PilotConfig`(postprocess 절), `PredictionStore`, `Postprocessor`, `ResultWriter`. 추론 없이 `predictions.json` 을 직접 만들어 넣으므로 모델·GPU 가 필요 없다.

## 결정

| 항목 | 결정 | 근거 |
|---|---|---|
| 규칙 값 | 원본 `D:\work\Ocean_Pilot\abnormal_detection\config\pilot.yaml` 의 tiling·postprocess 절을 테스트 상수로 넣음 | 실제 운용 규칙(Wreck 0.7 · min 5 m, 크기대별 NMS, 어초↔블럭 교차 NMS)으로 검증. 외부 경로에 기대지 않음 |
| 단계 검증 | `FilterScore` · `ToGlobal` · `FilterSize` · `BevIou` · `GlobalNms` · `CrossClassNms` 를 따로, `Run` 은 단계별 개수(`PostprocessStats`)로 | 어느 단계가 바뀌었는지 바로 보이게 |
| 경계값 | score `>=` 임계값 유지, size `<` min 만 제거, dist `<` thresh 만 억제 | 원본 Python 과 같은 부등호 |
| 흐름 | `PostprocessFlowTests` 하나로 tiles.json + predictions.json → 최종 파일 | 예전 파이프라인 `RunPostprocess` 자리. 겹침 타일 중복 제거와 index 합계 = 최종 개수 확인 |
| 범위 밖 | `ResultWriter.WriteMeta` (실행 시각·소요 시간이 매번 다름) | 예전 검증에서도 meta.json 은 비교에서 뺐다 |

## 변경

Before/After(적용은 사용자): [doc/code/abnormal-postprocess-tests.md](../code/abnormal-postprocess-tests.md) — 6개 항목(모두 신규 파일, `Abnormal/PostProcess/`).

- 1 공통 도우미 `Boxes.cs`
- 2 `PilotConfigTests` · 3 `PredictionStoreTests` · 4 `PostprocessorTests` · 5 `ResultWriterTests` · 6 `PostprocessFlowTests`
- 1차 문서의 `TestSupport.cs`(`TempDir` · `ScanFactory`) 를 쓰므로 1차를 먼저 적용한다.

## 현황

| # | 항목 | 상태 |
|---|---|---|
| 1 | 테스트 설계 · Before/After | 완료 |
| 2 | 스크래치 적용본 실행 (전처리 30 + 후처리 19) | 49개 통과 |
| 3 | 레포 적용 | 완료 (사용자 요청으로 Claude 적용, 레포에서 49개 통과 · sln 빌드 경고 0) |
| 4 | Inference 모델 실행 (`NativeInferenceModel` / `SampleRunner.Run`) | 모델 경로·GPU 필요 — 범위 확인 필요 |

## 검증 (2026-11-03, 스크래치 적용본)

| 항목 | 결과 |
|---|---|
| `dotnet test ... -c Debug -p:Platform=x64` (OceanSW 절대경로 참조 사본) | 49개 통과, 테스트 프로젝트 경고 0 |
| `Run_AppliesStagesInOrder_AndCountsEach` | 원시 7 → score 6 → size 5 → NMS 4 → 교차 3 |
| `Files_ToFinalResult_IndexMatchesFinal` | 겹침 타일의 같은 어초 2건 → 1건, Wreck 0.6 제거, 최종 2건 = index 합계 |

- 적용 후 확인: `dotnet test OceanDetect/OceanDetectAI.Tests -c Debug -p:Platform=x64` 에서 49개 통과.

## 위험 · 확인 필요

- pilot.yaml 규칙이 바뀌면 `Boxes.PilotYaml` 도 같이 바꿔야 한다(테스트는 상수 사본을 본다).
- `BevIou` 는 회전 박스의 외접 사각형 근사다. 회전 박스 실제 IoU 와 다르다는 점은 원본 동작이라 테스트도 근사값을 기대한다.
