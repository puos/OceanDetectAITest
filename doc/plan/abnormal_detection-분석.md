# abnormal_detection 분석 — 해저이상체 탐지 모델과 처리 구성

## 배경

- 해저지형 탐지 SW 파일럿(`doc/plan/해저지형SW-v1-요구분석.md`)의 이상체 탐지 부분을 기존 코드로 실현할 수 있는지 확인한다.
- 대상: `D:\work\Ocean_Pilot\abnormal_detection` (2026-10-30 기준)
- 확인 범위: README, `docs/models.md` · `input_spec.md` · `interface.md`, `src/README.md`, `config/pilot.yaml`, `models/*.cfg.yaml`, `inference/engine.py`, `Tiling/Tiler.cs`, `_python_backup/tiler.py`

## 결론

- 이상체 탐지 모델은 **CenterPoint 3개**(어초·블럭·침선 클래스별 1개)다. 요구분석의 "클래스별 모델 3개" 결정과 맞는다.
- 모델 자체에는 전처리가 없다. 50m 타일 분할·해저면 z 정규화는 **모델 밖 전처리**이고, 이 레포에는 C# 으로 구현돼 있다.
- 처리 구성은 **C# 전처리 → Python 추론 → C# 후처리** 이고, 둘 사이는 작업 폴더의 파일로 주고받는다.
- 추론은 ONNX 가 아니라 **Python + PyTorch + CUDA**(OpenPCDet, spconv)다.

## 1. 모델

| 클래스 | 가중치 (`models/`) | 학습 출처 | 성능 |
|---|---|---|---|
| 어초 `Fish_Reef` | `reef_det_model.pth` | reef19_hn20 ep40 | CD-mAP 65.17 |
| 블럭 `Block` | `block_det_model.pth` | block20_full ep45 | CD-mAP 69.89 |
| 침선 `Wreck` | `shipwreck_det_model.pth` | wreck20_full ep75 | IoU-mAP 26.91 |

- 세 모델은 구조가 같고 가중치만 다르다. 각 약 35 MB, 파라미터 8,836,277개(fp32).
- 구조: `MeanVFE` → `VoxelResBackBone8x`(spconv) → `HeightCompression` → `BaseBEVBackbone`(NUM_FILTERS [128, 256]) → `CenterHead`(단일 클래스 헤드)
- 프레임워크: 패치된 OpenPCDet(`third_party/OpenPCDet`), torch 2.1.0, spconv 2.3.6
- 성능 지표: 어초·블럭은 중심거리 1·2·3 m AP 평균(CD-mAP), 침선은 IoU-mAP

### 입력

| 항목 | 값 |
|---|---|
| 점군 | `(N, 4)` float32 = x, y, z, intensity |
| intensity | **항상 0** (MBES 에 세기 값이 없다, `Tiler.cs:56`, `tiler.py:74`) |
| x · y | 타일 로컬 — 타일 중심을 뺀 값 (±25 m) |
| z | 해저면 기준 — 스캔 전체 z 의 하위 2퍼센타일(`sf_z`)을 뺀 값 |
| POINT_CLOUD_RANGE | `[-25, -25, -2, 25, 25, 30]` m |
| VOXEL_SIZE | 0.2 m (grid 250 × 250 × 160) |

### 출력

- Box `[cx, cy, cz, dx, dy, dz, heading]` (타일 로컬, m / rad, heading 은 x축 기준 반시계) + score
- cfg 의 POST_PROCESSING: score 0.3, 어초·블럭 dist_nms 1.2 m, 침선 nms_gpu IoU 0.1

## 2. 처리 구성

```
도엽 스캔 → [C#] 읽기 · sf_z · 50m/stride40 타일   → work/tiles.json + tiles.bin
          → [Py] inference/run_tiles.py  CenterPoint ×3 → work/predictions.json
          → [C#] 좌표 복원 · score · size · NMS          → final.json, detections.csv, index.csv, meta.json
```

| 단계 | 구현 | 하는 일 |
|---|---|---|
| 읽기 | `Pcd/PcdReader.cs`, `Pcd/XyzReader.cs`, `Scans/ScanReader.cs` | .pcd(ascii / binary / binary_compressed) · .xyz 읽기, 스캔별 sf_z |
| 전처리 | `Tiling/Tiler.cs` | 50 m / stride 40 m 타일, 격자 원점 = 스캔 min(x, y), 50점 미만 제외, (N,4) 구성 |
| 추론 | `inference/run_tiles.py`, `engine.py` | 모델 3개를 차례로 GPU 추론, raw 예측 저장 (score 임계 없음) |
| 후처리 | `Postprocessing/Postprocessor.cs` | 타일 로컬 → 스캔 로컬 복원 → score → size → 클래스별 전역 NMS → 클래스 간 NMS |
| 저장 | `IO/ResultWriter.cs` 등 | final.json · detections.csv · index.csv · meta.json |

- C# Core 는 `netstandard2.0`, CLI 는 `net8.0` (`preprocess` / `postprocess` / `run`).
- C# 전처리·후처리는 Python 원본(`_python_backup/`)과 결과가 같은지 검증됐다(2026-10-26, 139장·타일 2,224개, 최종 595건 동일).

### 입력 형식 (`docs/input_spec.md`)

| 형식 | 좌표 | z | 읽을 때 변환 |
|---|---|---|---|
| .pcd (학습 형식) | 스캔 로컬 미터 | 위가 양수 | 없음 |
| .xyz | UTM 실좌표 (m) | 수심, 아래가 양수 | 첫 점을 원점으로 x·y 빼기, z 부호 반전, float32 |

- 실좌표 = 로컬 + `offset_x / offset_y` (`tiles.json` · `meta.json` 의 스캔별 값, .xyz 입력만 값이 있다).
- 결과 좌표는 스캔 로컬 미터(`local_meters`). 스캔끼리 좌표가 겹치므로 모든 레코드에 `scan` 열이 붙고 NMS 도 스캔 단위다.

### 후처리 규칙 (`config/pilot.yaml` `postprocess`)

| 순서 | 규칙 | 값 |
|---|---|---|
| 1 | score 게이트 | 어초 · 블럭 0.5, 침선 0.7 |
| 2 | size 게이트 | 침선: 최대 수평변 5 m 이상만 |
| 3 | 클래스별 전역 NMS | 어초 · 블럭: 최대변 10 m 미만은 거리 1.2 m, 10 m 이상은 IoU 0.3 / 침선: IoU 0.3 |
| 4 | 클래스 간 NMS | 어초 ↔ 블럭: 거리 1.2 m 또는 IoU 0.3, 점수 높은 쪽만 남김 |

### C# 으로 아직 옮기지 않은 것 (`src/README.md`)

- 이미지: 탐지 타일 BEV 이미지, 개관도 (`render.py`)
- 내보내기: json / label_json / **shp**, 공유 폴더 publish (`config/pilot.yaml` 의 `exports: []`)
- `coverage.npy`, 탐지 타일 점군 캐시

## 3. 요구분석(`해저지형SW-v1-요구분석.md`)과의 차이

| 항목 | 요구분석 | abnormal_detection | 처리 방향 |
|---|---|---|---|
| 해저면 기준 | 50m 블록 단위 하위 2퍼센타일 (기능정의서 v1) | **스캔 전체** z 의 2퍼센타일 하나 (학습 코드 `build_class_dataset_v2.py:259` 와 같음) | 학습과 맞춰야 하므로 스캔 전체로 바꾸고, 기능정의서 v1 F-02 수정 요청 — **확인 필요** |
| 입력 형식 | xyz 폴더 | .pcd · .xyz, 파일 또는 폴더 | 일치. .xyz 의 z 부호 반전·첫 점 원점 규칙을 요구분석에 추가 |
| 좌표 Shift 원점 | 로컬 원점 (방식 미정) | **첫 점**을 원점으로 | 같은 규칙을 쓴다 |
| 이상체 입력 | (N,4) x,y,z,0 | (N,4) x,y,z,intensity(=0) | 일치 |
| 후처리 | 겹침 중복 Box 제거 | score · size · 클래스별 NMS(크기대별) · 클래스 간 NMS | 요구분석 REQ-17 을 이 4단계로 구체화 |
| 출력 | 클래스별 SHP + GeoJSON, EPSG 좌표 | 로컬 미터 final.json · csv. shp 는 Python 에만 | SHP / GeoJSON 내보내기와 실좌표 복원은 새로 작성 |
| 추론 실행 | ONNX / Python 미정 | Python + PyTorch + CUDA | 파일럿은 Python 프로세스 호출로 간다. ONNX 는 spconv 변환이 난점 |
| 지형(암반) 모델 | Volt | 이 레포에 없음 | 별도 확인 |

## 4. OceanDetectAITest 에서 쓰는 방안 (제안)

| 단계 | 방안 |
|---|---|
| 읽기 · 전처리 | `AbnormalDetection.Core` 의 `Pcd/`, `Scans/`, `Tiling/` 을 그대로 참조하거나 `PCD.OceanDetectAI` 로 옮긴다. Core 가 netstandard2.0 이라 net8.0-windows 에서 참조 가능 |
| 추론 | `inference/run_tiles.py` 를 외부 프로세스로 호출하고 `work/tiles.*` → `work/predictions.json` 파일 계약을 그대로 쓴다 |
| 후처리 | `Postprocessing/Postprocessor.cs` + `config/pilot.yaml` 의 `postprocess` 절을 재사용 |
| 새로 작성 | 클래스별 SHP · GeoJSON 내보내기, 로컬 → 실좌표(+ 출력 CRS) 변환, 결과 시각화, 암반(Volt) 추론 연결 |
| 검증 | 같은 입력에 대해 `work/tiles.bin` · `predictions.json` · `final.json` 을 abnormal_detection CLI 결과와 비교한다 |

## 5. 미정 · 확인 필요

- 해저면 기준을 스캔 전체 2퍼센타일로 확정할지 (기능정의서 v1 은 50m 블록)
- 출력 좌표계: EPSG:32652 유지인지 EPSG:5186 재투영인지
- 3D Box 를 SHP 로 쓸 때의 형태와 속성 필드 (Python `result.py` 의 shp 내보내기 확인 필요)
- 실행 환경: 사용자 PC 에 CUDA · torch 2.1.0 · spconv 2.3.6 을 둘 수 있는지
- 암반(Volt) 모델 코드와 가중치 위치
