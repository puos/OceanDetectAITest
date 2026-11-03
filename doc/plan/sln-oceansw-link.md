# sln 정상화 + OceanSW NeoOceanCore · NeoOceanAICore 연결 (2026-11-03)

## 배경

- `PCD/` → `OceanDetect/` 로 옮기면서 sln 이 실제 폴더와 어긋났다.
  - sln 의 테스트 경로 `OceanDetect\OceanDetect.OceanDetectAI.Tests\` ↔ 실제 `OceanDetect\OceanDetectAI.Tests\` → VS 에서 로드 실패.
  - 지워진 `PCD.OceanDetectAI` (`{90D75D18}`) 가 빌드 구성 · 폴더 배치에 남아 있음.
  - 테스트 csproj 가 없는 `..\PCD.OceanDetectAI\PCD.OceanDetectAI.csproj` 를 참조.
- [abnormal-ai-port](abnormal-ai-port.md) 에서 이 레포에 넣으려던 이식 코드는 `D:\work\OceanSW\NeoOceanAICore` 로 갔다(abnormal_detection · seafloor_seg · RunAI/CudaEnvironment, 대부분 `public`). 단위테스트는 이 레포에서 OceanSW 코어를 참조해 작성한다.

## 결정

| 항목 | 결정 | 근거 |
|---|---|---|
| 경로 기준 | 폴더 `OceanDetectAI.Tests` 를 그대로 두고 sln 경로를 맞춤 | 실제 폴더 · csproj 가 이미 있음 |
| 코어 참조 | 테스트 csproj → `..\..\..\OceanSW\NeoOceanCore`, `...\NeoOceanAICore` ProjectReference | NeoOceanAICore 가 NeoOceanCore 를 참조. 둘 다 명시해 테스트에서 로그 등 직접 사용 |
| sln 배치 | 솔루션 폴더 `OceanSW` 아래 두 코어, GUID 는 OceanSW.sln 과 동일 | 두 sln 에서 같은 프로젝트로 인식 |
| 플랫폼 | 코어는 모든 플랫폼 → Any CPU (OceanSW.sln 과 동일), 테스트는 기존 x64 유지 | OnnxRuntime.Gpu · OceanInference.Native.dll 이 x64 |
| TFM | 테스트 `net8.0-windows` 유지 | 코어의 `net8.0-windows7.0` 과 같은 대상 |
| `.vs/` | .gitignore 에 추가 | untracked 로 뜸 |
| OceanSW | 수정하지 않음 | 범위 밖 |

## 변경

Before/After(적용은 사용자): [doc/code/sln-oceansw-link.md](../code/sln-oceansw-link.md) — 5개 항목, 번호 순서대로 적용.

- 1 테스트 csproj 참조 교체
- 2 ~ 4 `OceanDetectAITest.sln` (프로젝트 선언 · 빌드 구성 · 폴더 배치)
- 5 `.gitignore`

## 현황

| # | 항목 | 상태 |
|---|---|---|
| 1 | Before/After 작성 | 완료 |
| 2 | 레포 적용 | 완료 (사용자 요청으로 Claude 적용) |
| 3 | 빌드 확인 | 완료 |
| 4 | 단위테스트 작성 | 다음 작업 |

## 검증 (2026-11-03)

| 항목 | 방법 | 결과 |
|---|---|---|
| sln 빌드 | `dotnet build OceanDetectAITest.sln -c Debug -p:Platform=x64` | 오류 0, 경고 0 |
| 테스트 실행 | `dotnet test OceanDetect/OceanDetectAI.Tests -c Debug -p:Platform=x64` | 빌드 성공, "테스트를 사용할 수 없습니다"(테스트 클래스가 아직 없음 — 정상) |
| 참조 복사 | 테스트 출력 `bin\x64\Debug\net8.0-windows\` | `NeoOceanCore.dll` · `NeoOceanAICore.dll` 복사됨 |

- 프로젝트 단위 `dotnet test` 는 x64 가 코어까지 전달돼 코어가 `bin\x64\Debug` 로, sln 빌드는 Any CPU 매핑이라 `bin\Debug` 로 나간다. 둘 다 동작하지만 출력 폴더가 다르다.
- `dotnet test` 경로에서는 NeoOceanAICore 의 기존 nullable 경고(PcdReader CS8600/CS8603 등)가 보인다. OceanSW 코드라 그대로 둔다.
- VS 에서 sln 로드 확인은 사용자.

## 위험 · 확인 필요

- OceanSW 를 `..\OceanSW` 상대경로로 참조한다. 이 레포와 OceanSW 를 같은 상위 폴더(`D:\work`)에 둬야 빌드된다.
- 이 sln 으로 빌드하면 OceanSW 코어의 `bin/obj` 가 OceanSW 폴더 안에 생긴다(OceanSW.sln 빌드와 공유).
- NeoOceanAICore 의 `ModelHandle` · `Native` 등 일부 타입은 `internal` 이고 `InternalsVisibleTo` 가 없다. 테스트에서 필요하면 OceanSW 쪽 수정이 따로 필요.
- 지워진 `PCD/` 파일은 git 에서 삭제 상태로 남아 있다. 적용 커밋에 함께 넣는다.
