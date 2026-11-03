# abnormal 단위테스트 — 단계별 구성, 1차 전처리 (2026-11-03)

## 배경

- [sln-oceansw-link](sln-oceansw-link.md) 로 테스트 프로젝트가 OceanSW `NeoOceanCore` · `NeoOceanAICore` 를 참조하게 됐다.
- NeoOceanAICore 에는 예전 계획([abnormal-ai-port](abnormal-ai-port.md))의 `OceanFacilityAbnormalPipeline`(RunPreprocess · RunInference · RunPostprocess) 이 없다. 원본 Core 클래스(`ScanReader`, `Tiler`, `TileStoreWriter` …)와 네이티브 모델이 있어야 끝까지 도는 `SampleRunner.Run` 만 있다.
- 그래서 파이프라인 하나만 돌리는 테스트로는 어디서 깨졌는지 알 수 없고, 추론은 모델·GPU 가 없으면 아예 못 돈다 → 단계별로 나눠 테스트한다.

## 결정

| 항목 | 결정 | 근거 |
|---|---|---|
| 폴더 | `Abnormal/PreProcess`, `Abnormal/Inference`, `Abnormal/PostProcess` (namespace 도 같게) | 예전 RMAI 형태의 단계 구분을 테스트에서 유지 (사용자 결정) |
| 이번 범위 | PreProcess 전체 + Inference 의 입력 변환(`SampleRunner.Voxelize`) | 사용자 요청 "abnormal 전처리부터". Voxelize 는 모델 입력 변환이라 Inference 쪽에 둔다 |
| 기대값 | 손으로 계산 가능한 합성 입력(1 m 격자, 4점 배열 등) | 규칙이 바뀌면 어느 값이 틀렸는지 바로 보인다 |
| 회귀 | `samples/test01.xyz` 전체 전처리 → 타일 63, 행 2,846,483 | 원본 CLI 와 바이트 동일했던 값(abnormal-ai-port 검증) |
| 샘플 위치 | 실행 폴더에서 위로 올라가 `OceanDetectAITest.sln` 이 있는 곳의 `samples/` | 출력 폴더로 58 MB 를 복사하지 않는다 |
| 임시 파일 | `%TEMP%\OceanDetectAITests\<guid>` 에 쓰고 Dispose 에서 지움 | 테스트끼리 간섭 없음 |
| OceanSW | 수정하지 않음 | 모두 `public` 이라 InternalsVisibleTo 불필요 |

## 변경

Before/After(적용은 사용자): [doc/code/abnormal-preprocess-tests.md](../code/abnormal-preprocess-tests.md) — 8개 항목(모두 신규 파일).

- 1 공통 도우미 `TestSupport.cs`
- 2 ~ 7 `Abnormal/PreProcess/*Tests.cs` (NumpyCompat → XyzReader → ScanReader → Tiler → TileStore → 샘플 전체)
- 8 `Abnormal/Inference/VoxelizeTests.cs`

## 현황

| # | 항목 | 상태 |
|---|---|---|
| 1 | 테스트 설계 · Before/After | 완료 |
| 2 | 스크래치 적용본 실행 | 30개 통과 |
| 3 | 레포 적용 | 완료 (사용자 요청으로 Claude 적용, 레포에서 49개 통과) |
| 4 | PostProcess 테스트 (`PredictionStore` · `Postprocessor.RunByFrame` · `ResultWriter`) | [abnormal-postprocess-tests](abnormal-postprocess-tests.md) |
| 5 | Inference 모델 실행 (`NativeInferenceModel` / `SampleRunner.Run`) | 모델 경로·GPU 필요 — 범위 확인 필요 |

## 검증 (2026-11-03, 스크래치 적용본)

| 항목 | 방법 | 결과 |
|---|---|---|
| 전체 | `dotnet test ... -c Debug -p:Platform=x64` (OceanSW 절대경로 참조 사본) | 30개 통과, 테스트 프로젝트 경고 0 |
| 샘플 회귀 | `Test01Xyz_ProducesKnownTilesAndRows` | 점 1,906,727 · 타일 63 · 행 2,846,483, 약 1 초 |

- 적용 후 확인: `dotnet test OceanDetect/OceanDetectAI.Tests -c Debug -p:Platform=x64` 에서 30개 통과.

## 위험 · 확인 필요

- `samples/test01.xyz` 는 git LFS 다. LFS 를 받지 않은 PC 에서는 포인터 파일만 있어 샘플 테스트가 실패한다.
- 테스트는 NeoOceanAICore 원본 클래스를 직접 부른다. OceanSW 쪽에서 단계 클래스(Pipeline)를 새로 만들면 샘플 테스트를 그 진입점으로 바꿔야 한다.
