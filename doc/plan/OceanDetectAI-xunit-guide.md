# OceanDetectAI xUnit 프로젝트 구성

`PCD.OceanDetectAI` 라이브러리와 `PCD.OceanDetectAI.Tests` 테스트 프로젝트를 생성하고 솔루션에 추가합니다. 아래 예시는 Windows PowerShell과 `D:\work\OceanDetectAITest` 경로를 기준으로 합니다.

## 1. 프로젝트 생성

솔루션에 추가하기 전에 프로젝트 파일을 생성합니다.

```powershell
dotnet new classlib -n PCD.OceanDetectAI -o D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI --no-restore

dotnet new xunit -n PCD.OceanDetectAI.Tests -o D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI.Tests --no-restore
```

## 2. 라이브러리 프로젝트 설정

생성된 `D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI\PCD.OceanDetectAI.csproj`를 다음 내용으로 교체합니다.

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

## 3. 테스트 프로젝트 설정

생성된 `D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI.Tests\PCD.OceanDetectAI.Tests.csproj`를 다음 내용으로 교체합니다.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Platforms>AnyCPU;x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PCD.OceanDetectAI\PCD.OceanDetectAI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="input\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

## 4. 스모크 테스트 작성

`D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI.Tests\SmokeTests.cs`를 새로 만듭니다.

```csharp
using Xunit;
using Xunit.Abstractions;

namespace PCD.OceanDetectAI.Tests;

public class SmokeTests
{
    private readonly ITestOutputHelper _output;

    public SmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Runner_Works()
    {
        _output.WriteLine($"[OUTPUT] baseDirectory={AppContext.BaseDirectory}");
        Assert.True(Directory.Exists(AppContext.BaseDirectory));
    }
}
```

템플릿에서 생성한 예제 클래스와 테스트는 필요하지 않으면 삭제합니다. 특히 SDK 버전에 따라 생성된 테스트가 다른 xUnit 버전을 사용할 수 있으므로 예제 테스트 파일을 삭제하고 위 스모크 테스트를 사용합니다.

## 5. 솔루션 생성 및 프로젝트 추가

.NET 9 이하 SDK에서는 다음 명령으로 `.sln`을 생성합니다.

```powershell
dotnet new sln -n PCDAITest -o D:\work\OceanDetectAITest
```

.NET 10 SDK에서는 기본 솔루션 형식이 `.slnx`이므로 `.sln`을 사용하려면 다음 명령을 대신 실행합니다.

```powershell
dotnet new sln -n PCDAITest -o D:\work\OceanDetectAITest --format sln
```

솔루션이 이미 있다면 생성 명령을 건너뛰고, 두 프로젝트를 추가합니다.

```powershell
dotnet sln D:\work\OceanDetectAITest\PCDAITest.sln add --solution-folder PCD `
  D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI\PCD.OceanDetectAI.csproj `
  D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI.Tests\PCD.OceanDetectAI.Tests.csproj
```

## 6. 테스트 실행

```powershell
dotnet test D:\work\OceanDetectAITest\PCD\PCD.OceanDetectAI.Tests\PCD.OceanDetectAI.Tests.csproj -p:Platform=x64
```

`Runner_Works`가 통과하면 테스트 러너 실행과 프로젝트 참조 빌드를 확인할 수 있습니다. .NET 8 대상 프로젝트를 빌드하고 실행할 수 있는 SDK와 x64 .NET 8 런타임이 필요합니다.
