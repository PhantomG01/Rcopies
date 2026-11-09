@echo off
setlocal
cd /d "%~dp0"
dotnet publish src\RcopiesAdmin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:NuGetAudit=false -o portable
if errorlevel 1 exit /b 1
echo Administrador generado en portable\RcopiesAdmin.exe
