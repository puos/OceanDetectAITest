# sln 정상화 + OceanSW NeoOceanCore · NeoOceanAICore 연결

계획·검증: [doc/plan/sln-oceansw-link.md](../plan/sln-oceansw-link.md)

## 목차 (호출 트리)

- `OceanDetectAITest.sln` 로드
  - 프로젝트 선언 (테스트 경로 수정, OceanSW 폴더 · 코어 2개 추가) — 2
  - 빌드 구성 매핑 — 3
  - 솔루션 폴더 배치 — 4
  - `OceanDetect.OceanDetectAI.Tests` → `NeoOceanCore` · `NeoOceanAICore` 참조 — 1
- 저장소 정리: `.gitignore` — 5

## OceanDetect/OceanDetectAI.Tests/OceanDetect.OceanDetectAI.Tests.csproj:18

없는 PCD.OceanDetectAI 참조를 OceanSW 의 두 코어 프로젝트로 바꾼다.

before
```xml
  <ItemGroup>
    <ProjectReference Include="..\PCD.OceanDetectAI\PCD.OceanDetectAI.csproj" />
  </ItemGroup>
```
after
```xml
  <ItemGroup>
    <ProjectReference Include="..\..\..\OceanSW\NeoOceanCore\NeoOceanCore.csproj" />
    <ProjectReference Include="..\..\..\OceanSW\NeoOceanAICore\NeoOceanAICore.csproj" />
  </ItemGroup>
```

## OceanDetectAITest.sln:6

테스트 프로젝트 경로를 실제 폴더로 고치고, OceanSW 솔루션 폴더와 코어 2개를 OceanSW.sln 과 같은 GUID 로 추가한다.

before
```
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "OceanDetect", "OceanDetect", "{CC268035-F1D1-BBD8-B275-713FBC605CCD}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "OceanDetect.OceanDetectAI.Tests", "OceanDetect\OceanDetect.OceanDetectAI.Tests\OceanDetect.OceanDetectAI.Tests.csproj", "{ABA6326F-38F5-4468-9103-580BD536AF91}"
EndProject
```
after
```
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "OceanDetect", "OceanDetect", "{CC268035-F1D1-BBD8-B275-713FBC605CCD}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "OceanDetect.OceanDetectAI.Tests", "OceanDetect\OceanDetectAI.Tests\OceanDetect.OceanDetectAI.Tests.csproj", "{ABA6326F-38F5-4468-9103-580BD536AF91}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "OceanSW", "OceanSW", "{5B2E9A41-7C3D-4F8E-9A6B-2D1C0E3F4A57}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NeoOceanCore", "..\OceanSW\NeoOceanCore\NeoOceanCore.csproj", "{C8A3499A-171A-44C8-A139-24F4B1C72E17}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NeoOceanAICore", "..\OceanSW\NeoOceanAICore\NeoOceanAICore.csproj", "{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}"
EndProject
```

## OceanDetectAITest.sln:20

지워진 `{90D75D18}` 구성을 빼고, 코어 2개는 OceanSW.sln 처럼 모든 플랫폼을 Any CPU 로 매핑한다.

before
```
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Debug|x64.ActiveCfg = Debug|x64
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Debug|x64.Build.0 = Debug|x64
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Debug|x86.ActiveCfg = Debug|Any CPU
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Debug|x86.Build.0 = Debug|Any CPU
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Release|Any CPU.Build.0 = Release|Any CPU
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Release|x64.ActiveCfg = Release|x64
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Release|x64.Build.0 = Release|x64
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Release|x86.ActiveCfg = Release|Any CPU
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120}.Release|x86.Build.0 = Release|Any CPU
		{ABA6326F-38F5-4468-9103-580BD536AF91}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
```
after
```
		{ABA6326F-38F5-4468-9103-580BD536AF91}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
```
그리고 `{ABA6326F}...Release|x86.Build.0 = Release|Any CPU` 줄(43행) 뒤에 추가:
```
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Debug|x64.ActiveCfg = Debug|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Debug|x64.Build.0 = Debug|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Debug|x86.ActiveCfg = Debug|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Debug|x86.Build.0 = Debug|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Release|Any CPU.Build.0 = Release|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Release|x64.ActiveCfg = Release|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Release|x64.Build.0 = Release|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Release|x86.ActiveCfg = Release|Any CPU
		{C8A3499A-171A-44C8-A139-24F4B1C72E17}.Release|x86.Build.0 = Release|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Debug|x64.ActiveCfg = Debug|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Debug|x64.Build.0 = Debug|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Debug|x86.ActiveCfg = Debug|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Debug|x86.Build.0 = Debug|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Release|Any CPU.Build.0 = Release|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Release|x64.ActiveCfg = Release|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Release|x64.Build.0 = Release|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Release|x86.ActiveCfg = Release|Any CPU
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652}.Release|x86.Build.0 = Release|Any CPU
```

## OceanDetectAITest.sln:48

지워진 프로젝트의 폴더 배치를 빼고 코어 2개를 OceanSW 폴더 아래에 둔다.

before
```
	GlobalSection(NestedProjects) = preSolution
		{90D75D18-0C1F-482D-BC5A-E445CE7D9120} = {CC268035-F1D1-BBD8-B275-713FBC605CCD}
		{ABA6326F-38F5-4468-9103-580BD536AF91} = {CC268035-F1D1-BBD8-B275-713FBC605CCD}
	EndGlobalSection
```
after
```
	GlobalSection(NestedProjects) = preSolution
		{ABA6326F-38F5-4468-9103-580BD536AF91} = {CC268035-F1D1-BBD8-B275-713FBC605CCD}
		{C8A3499A-171A-44C8-A139-24F4B1C72E17} = {5B2E9A41-7C3D-4F8E-9A6B-2D1C0E3F4A57}
		{8EC036AB-C05E-4DCD-B20F-A6F1EF20A652} = {5B2E9A41-7C3D-4F8E-9A6B-2D1C0E3F4A57}
	EndGlobalSection
```

## .gitignore:57

VS 사용자 캐시 `.vs/` 가 untracked 로 잡히지 않게 한다.

before
```
nunit-*.xml
```
after
```
nunit-*.xml

# Visual Studio
.vs/
```
