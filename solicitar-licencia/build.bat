@echo off
setlocal
cd /d "%~dp0"
if not exist portable mkdir portable
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /win32icon:..\Rcopies\src\Assets\Rcopies.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:portable\Solicitar-Licencia-Rcopies.exe Solicitud.cs
exit /b %errorlevel%
