# 해저지형 탐지 SW 파일럿 — 요구분석 (v1, 전체 자료 검토판)

- 다이어그램: `doc/diagrams/해저지형SW-v1-dfd-S1, S2, S4 ~ S7-*.puml` (S3 은 S2 에 통합해 결번), `doc/diagrams/해저지형SW-v1-class.puml`
- 이전 분석: `doc/plan/해저지형SW-요구분석.md` (구 기능정의서 기준). 이 문서가 대체한다.

## 0. 검토 자료

| 약칭 | 파일 | 내용 | 날짜 |
|---|---|---|---|
| [정의서] | `해저지형_파일럿프로그램_기능정의서_v1.pptx` | F-01 ~ F-08 기능 표, 기능별 세부 조건, UI 예시 | 2026-10-30 |
| [결과물] | `결과물기능정의.jpg` (손그림) | AI ① 암반, AI ②③④ 어초·블록·침선, 클래스별 SHP, GeoJson, x,y,z+@, 3D Box | 2026-10-26 |
| [흐름] | `flowchart.jpg` (손그림) | ① 입력 → ② 깊이 색상 가시화 → ③ 추론 → ④ 결과 레이어 관리 → Export, 모델 입력 채널 | 2026-10-26 |
| [1단계] | `해저지형_1단계_기능구성및처리흐름.html` | 툴바 3버튼 흐름, Volt / CenterPoint 입력, 병합·복원, 산출물 스키마 | 2026-10-27 |
| [히스토리] | `해저지형 사업 히스토리 정리` (+ `image.png` 성능표) | 클래스 변경 경위, 50m 타일, 모델 선정, 국소 법선, 반사강도 없음, 음성데이터 | 2026-09-08 |
| [UI] | 정의서 4쪽 UI 예시 (`uiscreen.png`) | Import / Export / Run, 레이어 관리(암반·어초·블록·침선 색상·표시) | — |

### 자료 간 충돌과 결정

| 항목 | 자료별 내용 | 결정 | 근거 |
|---|---|---|---|
| 이상체 모델 수 | [1단계] CenterPoint 1개가 어초·블록·침선 / [결과물] AI ② 어초, ③ 블록, ④ 침선 따로 / [히스토리] 어초 탐지·블럭 탐지 학습데이터 비율을 따로 정함 | **클래스별 CenterPoint 모델 3개** (어초·블록·침선) | [결과물], [히스토리] 7 |
| 이상체 4번째 채널 | [정의서] x,y,z,**intensity** / [1단계]·[흐름] x,y,z,**0** / [히스토리] 학습데이터에 반사강도 없음 | **(N,4) = x,y,z,0** — intensity 자리를 0 으로 채운다 | [히스토리] 6, [흐름] |
| 지형 입력 | [정의서] 미기재 / [1단계]·[흐름]·[히스토리] x,y,z,nx,ny,nz (국소 법선 k=30, PCA) | **(N,6) = x,y,z,nx,ny,nz** | [히스토리] 6 |
| 타일링 대상 | [정의서] 이상체 항목 아래에만 기재 / [1단계] 공통 전처리 / [정의서] F-01 "AI 모델 내부 처리 단위 50m 타일" | **지형·이상체 공통**으로 50m 타일 | [정의서] F-01, [1단계] |
| 해저면 블록 | [정의서] 50m / [1단계] 200m | **50m** (최신 정의서) | [정의서] |
| 입력 형식 | [정의서] xyz 파일들이 든 폴더 / [1단계] pcd·xyz·laz / [흐름] xyz, Shift+PCD / [결과물] xyz 파일, las·npy (PCD 취소선) | **xyz 파일 폴더**. las / npy 는 내부 중간 형식 후보로만 둔다 | [정의서], [결과물] |
| 이상체 출력 | [정의서] SHP (+ 기능 표에 GeoJson) / [1단계] JSON·GeoJSON·SHP / [결과물] 어초·블록·침선 각각 shp, GeoJson | **클래스별 SHP 3개 + GeoJSON** | [결과물], [정의서] |
| 분류 점군 출력 | [정의서] xyz / [흐름] xyz,L (LAZ 취소선) / [결과물] x,y,z+@ / [1단계] x,y,z,(label) | **x,y,z,label 텍스트(.xyz)** | 네 자료 일치 |
| 검수용 라벨 JSON | [1단계]에만 있음 (NeoCloudLabel 호환) | **범위 밖** — 정의서·손그림에 없음 | [정의서] |
| 실행 순서·범위 | [1단계] 순차/별도, 전체/선택 미정 / [정의서] 순차 실행, 전체 데이터 | **순차, 전체 데이터** | [정의서] |
| 클래스별 색상 | [정의서] F-04 미정 / [UI] 암반 주황 채움, 어초 빨강·블록 하늘색·침선 보라 상자 | **[UI] 색상을 기본값으로** | [UI] |
| 좌표 | [1단계] EPSG:5186 대좌표 → 로컬 원점 Shift / [흐름] Shift / [정의서] 출력 EPSG:5186 | 로컬 Shift 로 처리, 내보낼 때 EPSG:5186 복원 | [1단계], [정의서] |

## 1. 요구사항

| ID | 요구사항 | 기능 ID | 출처 |
|---|---|---|---|
| REQ-1 | xyz 파일들이 든 폴더를 선택해 불러온다 (파일 하나 = 1:5,000 도엽) | F-01 | [정의서] [결과물] |
| REQ-2 | EPSG:5186 대좌표를 로컬 원점으로 Shift 하고 오프셋을 보관한다 | F-01 | [1단계] [흐름] |
| REQ-3 | 불러온 점군을 깊이(z) 기준 색상으로 3차원 표시한다 | F-01 · F-04 | [흐름] ② [1단계] |
| REQ-4 | 수천만 점 규모를 다운샘플링·타일 단위 로딩으로 표시한다 | F-04 | [정의서] [1단계] [히스토리] |
| REQ-5 | 파일 읽기·좌표변환·타일링 결과를 캐시로 저장해 재사용한다 | F-07 | [정의서] |
| REQ-6 | 캐시를 삭제할 수 있다 | F-07 | [정의서] |
| REQ-7 | 점군을 50m 타일, stride 40m(10m 겹침)로 나눈다 (지형·이상체 공통) | F-02 | [정의서] [1단계] [히스토리] |
| REQ-8 | 점이 50개 미만인 타일은 제외한다 | F-02 | [정의서] [1단계] |
| REQ-9 | 50m 블록 하위 2퍼센타일로 해저면을 계산해 z 를 정규화한다 | F-02 | [정의서] |
| REQ-10 | 지형 입력: 국소 표면 법선(k=30, PCA)을 계산해 (N,6) float32 x,y,z,nx,ny,nz 를 만든다 | F-02 | [1단계] [흐름] [히스토리] |
| REQ-11 | 이상체 입력: (N,4) float32 x,y,z,0 을 만든다 | F-02 | [1단계] [흐름] [히스토리] |
| REQ-12 | 암반 분류(Volt) → 어초 → 블록 → 침선 탐지(CenterPoint 3개)를 전체 데이터에 순차 실행한다 | F-03 | [정의서] [결과물] [히스토리] |
| REQ-13 | 지형 결과: 점별 암반 / 비암반 라벨 | F-03 | [정의서] [1단계] [결과물] |
| REQ-14 | 이상체 결과: 어초·블록·침선 객체별 3D Box (위치·크기·클래스·신뢰도) | F-03 | [정의서] [결과물] |
| REQ-15 | 실행 중에는 진행중 상태를 표시한다 (진행률은 표시하지 않음) | F-03 | [정의서] [1단계] |
| REQ-16 | 데이터가 없거나 실행이 실패하면 오류를 알리고 이전 화면을 유지한다 | F-03 | [정의서] |
| REQ-17 | 타일 결과를 도엽(입력 파일) 단위로 병합한다 — 겹침 구간 라벨 통합, 중복 Box 제거, 타일 로컬 → 도엽 로컬 좌표 | F-03 | [1단계] [정의서] F-01 출력 단위 |
| REQ-18 | 원본 점군 위에 암반은 점 색상, 이상체는 3D Box 로 클래스별 색상을 입혀 표시한다 | F-04 | [정의서] [UI] [1단계] |
| REQ-19 | 체크박스로 암반·어초·블록·침선 레이어 표시를 개별 설정하고 즉시 반영한다 | F-05 | [정의서] [흐름] ④ |
| REQ-20 | 마우스로 이동(왼쪽), 회전(오른쪽), 줌인·줌아웃(휠)한다 | F-08 | [정의서] |
| REQ-21 | 이상체 Box 를 클래스별 SHP 3개(어초·블록·침선)와 GeoJSON 으로 내보낸다 | F-06 | [결과물] [정의서] |
| REQ-22 | 분류 점군을 x,y,z,label 텍스트(.xyz)로 내보낸다 | F-06 | [정의서] [흐름] [결과물] |
| REQ-23 | 출력 좌표계는 EPSG:5186 이다 | F-06 | [정의서] [1단계] |

## 2. 가정

1. **구현 환경**: C# / WPF (MVVM). 제어 클래스 `SeabedMainViewModel` 에 시나리오마다 메서드 하나.
2. **[Run]**: 전처리 → 추론 → 후처리를 S2 하나(`RunDetection()`)로 실행한다.
3. **추론 실행 방식**: [결과물]에 "python / onnx" 가 함께 적혀 있다. ONNX Runtime 을 우선으로 하되, Volt·CenterPoint 의 sparse conv 변환이 막히면 Python 프로세스 호출로 바꾼다. `Predict(input)` 시그니처는 그대로 둔다. — **확인 필요(위험)**
4. **이상체 모델 3개**: `ObjectDetector` 하나를 클래스별 모델 파일로 세 번 만들어 쓴다. 침선은 시범 연구 수준이라 성능이 낮을 수 있다([히스토리]: 학습 타일 11,117장 중 650장).
5. **다중 파일**: 폴더 안 xyz 파일 하나를 도엽 하나로 보고, 결과·출력도 파일마다 따로 만든다(출력 단위 = 입력 단위).
6. **3D Box 를 SHP 로**: SHP 는 2D 폴리곤이므로 Box 바닥면을 폴리곤으로 쓰고 z 범위·높이·회전각·신뢰도를 속성 필드로 둔다. — 확인 필요
7. **좌표**: 화면·추론·병합은 로컬 좌표, EPSG:5186 복원은 내보낼 때만 한다.
8. **캐시**: 읽기·Shift 결과와 정규화까지 마친 타일을 캐시한다. 캐시가 있으면 해당 단계를 건너뛴다.
9. **범위 밖**: 검수용 NeoCloudLabel JSON, 선형객체(라벨링 진행 중), 침식/퇴적 지형(backscatter 없음), 학습·성능 평가, 원본 점군 레이어 on/off.
10. 모든 클래스는 신규(`<<new>>`)다. LidarRecon 점군 렌더러 재사용 여부는 구현 전에 확인한다.

## 3. 시나리오

| 시나리오 | 트리거 | 제어 메서드 | 요구사항 |
|---|---|---|---|
| S1 데이터 불러오기 | 툴바 [Import] | `SeabedMainViewModel.OpenFolder()` | REQ-1 ~ 5 |
| S2 AI 탐지 (전처리·추론·후처리) | 툴바 [Run] | `SeabedMainViewModel.RunDetection()` | REQ-5, 7 ~ 18 |
| S4 레이어 관리 | 레이어 체크박스 | `SeabedMainViewModel.ApplyLayers()` | REQ-19 |
| S5 결과 내보내기 | 툴바 [Export] | `SeabedMainViewModel.Export()` | REQ-21 ~ 23 |
| S6 캐시 삭제 | 메뉴 [캐시 삭제] | `SeabedMainViewModel.ClearCache()` | REQ-6 |
| S7 화면 조작 | 마우스 클릭·휠 | `SeabedMainViewModel.Navigate(e)` | REQ-20 |

### 시나리오별 작업 단계

| 시나리오 | 단계 | 처리 |
|---|---|---|
| S1 | ① 캐시 확인 | (1) 캐시 조회 |
| S1 | ② 읽기·변환 (캐시 없을 때) | (2) xyz 파일 읽기, (3) 로컬 좌표 변환, (4) 캐시 저장 |
| S1 | ③ 표시 | (5) 표시용 LOD 구성, (6) 깊이 색상 표시 |
| S2 | ① 공통 전처리 | (1) 50m 타일 분할·희소 제외, (2) 해저면 기준 z 정규화 |
| S2 | ② 모델별 입력 | (3) 국소 법선 계산, (4) 모델 입력 구성 |
| S2 | ③ 추론 (순차) | (5) 암반 분류 추론, (6) 이상체 탐지 추론 (어초·블록·침선 모델마다) |
| S2 | ④ 후처리 | (7) 도엽 결과 병합, (8) 클래스별 색상 표시 |
| S4 | ① 선택 | (1) 레이어 체크 변경 |
| S4 | ② 반영 | (2) 클래스별 표시 필터, (3) 화면 갱신 |
| S5 | ① 좌표 복원 | (1) EPSG:5186 좌표 복원 |
| S5 | ② 결과 저장 | (2) 클래스별 Box 저장, (3) 분류 점군 저장 |
| S6 | ① 조회 | (1) 캐시 목록 조회, (2) 사용 중 캐시 제외 |
| S6 | ② 삭제 | (3) 캐시 파일 삭제 |
| S7 | ① 입력 해석 | (1) 마우스 입력 해석 |
| S7 | ② 카메라 변경 | (2) 이동, (3) 회전, (4) 줌인·줌아웃 |
| S7 | ③ 표시 | (5) 화면 다시 그리기 |

## 4. 입력 · 출력

### 프로그램 전체

| 구분 | 내용 | 형식 |
|---|---|---|
| 입력 | 해저 점군 (MBES), 1:5,000 도엽 단위, 좌표만 보유 (반사강도 없음) | xyz 파일들이 든 폴더, EPSG:5186 |
| 입력 | AI 모델 4개 | Volt(암반), CenterPoint ×3 (어초·블록·침선) |
| 출력 | 이상체 3D Box — 클래스·위치·크기·신뢰도 | 어초.shp, 블록.shp, 침선.shp + GeoJSON, EPSG:5186 |
| 출력 | 분류 점군 — 점별 암반 / 비암반 라벨 | x,y,z,label 텍스트(.xyz), EPSG:5186 |
| 출력 | 결과 화면 — 암반 점 색상 + 이상체 Box, 레이어 4개 | 화면 |

### S2 처리별 입력 · 출력

| 처리 | 입력 | 출력 |
|---|---|---|
| (1) 50m 타일 분할·희소 제외 | 로컬 점군 (캐시 있으면 타일 캐시) | 50점 이상 타일 (stride 40m) |
| (2) 해저면 기준 z 정규화 | 타일 | 정규화 타일 (캐시에 저장) |
| (3) 국소 법선 계산 | 정규화 타일 | 점별 법선 nx,ny,nz |
| (4) 모델 입력 구성 | 정규화 타일, 법선 | 지형 (N,6) x,y,z,nx,ny,nz / 이상체 (N,4) x,y,z,0 |
| (5) 암반 분류 추론 | (N,6), Volt 모델 | 타일별 점 라벨 (암반 / 비암반) |
| (6) 이상체 탐지 추론 | (N,4), CenterPoint 모델 (어초·블록·침선) | 타일별 3D Box (클래스·위치·크기·신뢰도) |
| (7) 도엽 결과 병합 | 타일별 점 라벨, 타일별 Box | 도엽 단위 점 라벨, 중복 제거된 Box (도엽 로컬 좌표) |
| (8) 클래스별 색상 표시 | 도엽 단위 결과 | 결과 화면 |

## 5. 추적표

| REQ | 시나리오 | DFD 처리 | 클래스.메서드 |
|---|---|---|---|
| REQ-1 | S1 | (2) xyz 파일 읽기 | `XyzReader.ReadFolder` |
| REQ-2 | S1 | (3) 로컬 좌표 변환 | `CoordinateShifter.ToLocal` |
| REQ-3 | S1 | (6) 깊이 색상 표시 | `PointCloudViewer.RenderDepthColor` |
| REQ-4 | S1 | (5) 표시용 LOD 구성 | `LodBuilder.Build` |
| REQ-5 | S1 | (1) 캐시 조회, (4) 캐시 저장 | `CacheManager.TryLoad`, `CacheManager.Save` |
| REQ-5 | S2 | (1) 타일 캐시 조회, (2) 타일 캐시 저장 | `CacheManager.TryLoad`, `CacheManager.Save` |
| REQ-6 | S6 | (1) 목록 조회, (2) 사용 중 제외, (3) 삭제 | `CacheManager.List`, `.ExcludeInUse`, `.Delete` |
| REQ-7 | S2 | (1) 50m 타일 분할 | `Tiler.Split` |
| REQ-8 | S2 | (1) 희소 타일 제외 | `Tiler.DropSparse` |
| REQ-9 | S2 | (2) 해저면 기준 z 정규화 | `SeafloorNormalizer.Normalize` |
| REQ-10 | S2 | (3) 국소 법선 계산, (4) 모델 입력 구성 | `NormalEstimator.Estimate`, `ModelInputBuilder.BuildTerrainInput` |
| REQ-11 | S2 | (4) 모델 입력 구성 | `ModelInputBuilder.BuildObjectInput` |
| REQ-12 | S2 | (5) → (6) 순차 실행 | `SeabedMainViewModel.RunDetection` |
| REQ-13 | S2 | (5) 암반 분류 추론 | `TerrainSegmenter.Predict` |
| REQ-14 | S2 | (6) 이상체 탐지 추론 | `ObjectDetector.Predict` |
| REQ-15 | S2 | 시나리오 전체 (실행 상태) | `SeabedMainViewModel.RunDetection` |
| REQ-16 | S2 | 시나리오 전체 (예외 처리) | `SeabedMainViewModel.RunDetection` |
| REQ-17 | S2 | (7) 도엽 결과 병합 | `ResultMerger.MergeLabels`, `ResultMerger.MergeBoxes` |
| REQ-18 | S2 | (8) 클래스별 색상 표시 | `PointCloudViewer.RenderResult` |
| REQ-19 | S4 | (1) 체크 변경, (2) 표시 필터, (3) 화면 갱신 | `LayerManager.SetVisible`, `LayerManager.Filter`, `PointCloudViewer.Refresh` |
| REQ-20 | S7 | (1) 입력 해석, (2) 이동, (3) 회전, (4) 줌, (5) 다시 그리기 | `CameraController.MapInput`, `.Pan`, `.Rotate`, `.Zoom`, `PointCloudViewer.Redraw` |
| REQ-21 | S5 | (2) 클래스별 Box 저장 | `BoxExporter.ExportShp`, `BoxExporter.ExportGeoJson` |
| REQ-22 | S5 | (3) 분류 점군 저장 | `LabeledPointWriter.Write` |
| REQ-23 | S5 | (1) EPSG:5186 좌표 복원 | `CoordinateShifter.ToWorld` |

REQ-12, 15, 16 은 실행 흐름 전체의 순서·상태·예외 처리라 제어 메서드에 연결했다. 연결되지 않은 REQ 와 메서드는 없다.

## 6. 프로그램 목록

| NO | 프로그램ID | 기능명 | 소스코드 파일명 | 관련 요구사항ID |
|---|---|---|---|---|
| 1 | PG-001-01 | xyz 폴더 불러오기·캐시 재사용 | XyzReader.cs<br>CacheManager.cs | REQ-1<br>REQ-5 |
| 2 | PG-001-02 | 로컬 원점 좌표 Shift | CoordinateShifter.cs | REQ-2 |
| 3 | PG-001-03 | 깊이 색상 점군 표시 | LodBuilder.cs<br>PointCloudViewer.cs | REQ-3<br>REQ-4 |
| 4 | PG-002-01 | 타일 분할·해저면 정규화 | Tiler.cs<br>SeafloorNormalizer.cs<br>CacheManager.cs | REQ-5<br>REQ-7<br>REQ-8<br>REQ-9 |
| 5 | PG-002-02 | 모델별 입력 구성 | NormalEstimator.cs<br>ModelInputBuilder.cs | REQ-10<br>REQ-11 |
| 6 | PG-002-03 | 암반 분류 추론 | TerrainSegmenter.cs | REQ-13 |
| 7 | PG-002-04 | 어초·블록·침선 탐지 추론 | ObjectDetector.cs | REQ-14 |
| 8 | PG-002-05 | 도엽 결과 병합·표시 | ResultMerger.cs<br>PointCloudViewer.cs | REQ-17<br>REQ-18 |
| 9 | PG-002-06 | 실행 순서·상태 표시·예외 처리 | SeabedMainViewModel.cs | REQ-12<br>REQ-15<br>REQ-16 |
| 10 | PG-004-01 | 결과 레이어 표시 설정 | LayerManager.cs<br>PointCloudViewer.cs | REQ-19 |
| 11 | PG-005-01 | 클래스별 이상체 Box 내보내기 | CoordinateShifter.cs<br>BoxExporter.cs | REQ-21<br>REQ-23 |
| 12 | PG-005-02 | 분류 점군 내보내기 | CoordinateShifter.cs<br>LabeledPointWriter.cs | REQ-22<br>REQ-23 |
| 13 | PG-006-01 | 캐시 삭제 | CacheManager.cs | REQ-6 |
| 14 | PG-007-01 | 마우스 화면 조작 | CameraController.cs<br>PointCloudViewer.cs | REQ-20 |

## 7. 클래스 설명

| 클래스 | 파일 | 구분 | 역할 | 주요 메서드 | 관련 프로그램ID |
|---|---|---|---|---|---|
| SeabedMainViewModel | SeabedMainViewModel.cs | 신규 | 툴바·레이어·마우스 시나리오를 실행하고 실행 상태·오류를 알린다 | OpenFolder, RunDetection, ApplyLayers, Export, ClearCache, Navigate | PG-002-06 |
| XyzReader | XyzReader.cs | 신규 | 폴더 안의 xyz 파일들을 도엽별 점군으로 읽는다 | ReadFolder | PG-001-01 |
| CoordinateShifter | CoordinateShifter.cs | 신규 | EPSG:5186 대좌표와 로컬 좌표를 오프셋으로 오간다 | ToLocal, ToWorld | PG-001-02, PG-005-01, PG-005-02 |
| CacheManager | CacheManager.cs | 신규 | 읽기·좌표변환·타일링 캐시를 저장·조회·삭제한다 | TryLoad, Save, List, ExcludeInUse, Delete | PG-001-01, PG-002-01, PG-006-01 |
| LodBuilder | LodBuilder.cs | 신규 | 대용량 점군을 다운샘플링·타일 LOD 로 구성한다 | Build | PG-001-03 |
| PointCloudViewer | PointCloudViewer.cs | 신규 | 점군과 결과를 깊이 색상·클래스 색상·3D Box 로 그린다 | RenderDepthColor, RenderResult, Refresh, Redraw | PG-001-03, PG-002-05, PG-004-01, PG-007-01 |
| CameraController | CameraController.cs | 신규 | 마우스 입력을 이동·회전·줌으로 바꾼다 | MapInput, Pan, Rotate, Zoom | PG-007-01 |
| Tiler | Tiler.cs | 신규 | 50m 타일(stride 40m)로 나누고 50점 미만 타일을 뺀다 | Split, DropSparse | PG-002-01 |
| SeafloorNormalizer | SeafloorNormalizer.cs | 신규 | 50m 블록 하위 2퍼센타일 해저면으로 z 를 정규화한다 | Normalize | PG-002-01 |
| NormalEstimator | NormalEstimator.cs | 신규 | k=30 이웃 PCA 로 국소 표면 법선을 계산한다 | Estimate | PG-002-02 |
| ModelInputBuilder | ModelInputBuilder.cs | 신규 | 지형 (N,6) / 이상체 (N,4) 입력 텐서를 만든다 | BuildTerrainInput, BuildObjectInput | PG-002-02 |
| TerrainSegmenter | TerrainSegmenter.cs | 신규 | Volt 모델로 점별 암반 / 비암반을 추론한다 | Predict | PG-002-03 |
| ObjectDetector | ObjectDetector.cs | 신규 | CenterPoint 모델(클래스별 1개)로 3D Box 를 추론한다 | Predict | PG-002-04 |
| ResultMerger | ResultMerger.cs | 신규 | 타일 결과를 도엽 단위로 합친다(겹침 라벨 통합, 중복 Box 제거) | MergeLabels, MergeBoxes | PG-002-05 |
| LayerManager | LayerManager.cs | 신규 | 클래스별 레이어 표시 여부를 관리하고 표시 대상을 거른다 | SetVisible, Filter | PG-004-01 |
| BoxExporter | BoxExporter.cs | 신규 | 이상체 Box 를 클래스별 SHP 와 GeoJSON 으로 쓴다 | ExportShp, ExportGeoJson | PG-005-01 |
| LabeledPointWriter | LabeledPointWriter.cs | 신규 | 분류 점군을 x,y,z,label 텍스트로 쓴다 | Write | PG-005-02 |

엔티티 `PointCloud`(오프셋 보유, `Tile` 0..* 포함), `SeabedResult`(`DetectionBox` 0..* 포함)는 클래스 다이어그램에만 둔다.

## 8. 미정 · 확인 필요

- Volt / CenterPoint ONNX 변환 가능 여부 (가정 3) — 안 되면 추론 실행 구조가 바뀐다
- 이상체 모델이 정말 클래스별 3개인지 — [1단계]는 1개로 적었다 (가정 4)
- 정의서 F-02 의 "intensity" 를 "0" 으로 고칠지 (반사강도 데이터 없음)
- 해저면 블록 크기: 정의서 50m vs [1단계] 200m
- 3D Box 를 SHP 에 담는 방식과 속성 필드 (가정 6)
- 분류 점군 label 값: 정수 ID(0 비암반 / 1 암반)인지 클래스명인지, 구분자
- [결과물]의 "500m" 의 의미 (도엽 크기인지 처리 단위인지)
- 캐시 보관 위치·용량 정책
