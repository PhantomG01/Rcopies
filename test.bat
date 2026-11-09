@echo off
setlocal
cd /d "%~dp0"
dotnet run --project tests\EngineTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --project tests-mirror\MirrorTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --project tests-lite\LiteTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --project tests-security\SecurityTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --no-build --project tests-security\SecurityTests.csproj -c Release -- --probe-budget
if errorlevel 1 exit /b 1
dotnet run --project tests-features\FeatureTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --project tests-handoff\HandoffTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --project tests-input\InputTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --project tests-parallel\ParallelTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --project tests-comparison\ComparisonTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
dotnet run --project tests-automatic\AutomaticTests.csproj -c Release -p:NuGetAudit=false
if errorlevel 1 exit /b 1
