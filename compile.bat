@echo off
cd /d "D:\Dropbox\My Apps\DotNet\OpenSurge"

SET CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

IF NOT EXIST "%CSC%" (
    echo CSC not found at %CSC%
    pause & exit /b 1
)

IF NOT EXIST "Newtonsoft.Json.dll" (
    echo Copying Newtonsoft.Json.dll from Qonda Load Tester...
    copy /Y "D:\Dropbox\My Apps\DotNet\Qonda Load Tester\Newtonsoft.Json.dll" Newtonsoft.Json.dll
    IF ERRORLEVEL 1 (
        echo Failed to copy Newtonsoft.Json.dll
        pause & exit /b 1
    )
)

"%CSC%" ^
    /target:winexe ^
    /platform:x64 ^
    /win32icon:icon.ico ^
    /out:OpenSurge.exe ^
    /r:System.dll ^
    /r:System.Core.dll ^
    /r:System.Windows.Forms.dll ^
    /r:System.Drawing.dll ^
    /r:System.Net.Http.dll ^
    /r:Newtonsoft.Json.dll ^
    Program.cs ^
    Logger.cs ^
    Config.cs ^
    Session.cs ^
    HarImporter.cs ^
    HttpExecutor.cs ^
    ReportGenerator.cs ^
    AddRequestForm.Designer.cs ^
    AddRequestForm.cs ^
    MainForm.Designer.cs ^
    MainForm.cs

IF ERRORLEVEL 1 (
    echo.
    echo BUILD FAILED
    pause & exit /b 1
)

echo.
echo Build succeeded: OpenSurge.exe
pause
