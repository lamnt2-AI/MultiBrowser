@echo off
title Khoi dong MultiBrowser

if exist "%~dp0MultiBrowser\bin\Release\net10.0-windows\publish\MultiBrowser.exe" (
    cd /d "%~dp0MultiBrowser\bin\Release\net10.0-windows\publish"
    start "" "MultiBrowser.exe"
    exit
)

if exist "%~dp0MultiBrowser\bin\Release\net10.0-windows\MultiBrowser.exe" (
    cd /d "%~dp0MultiBrowser\bin\Release\net10.0-windows"
    start "" "MultiBrowser.exe"
    exit
)

echo [LOI] Khong tim thay MultiBrowser.exe!
echo Dang tien hanh bien dich ung dung...
dotnet publish "%~dp0MultiBrowser\MultiBrowser.csproj" -c Release -o "%~dp0MultiBrowser\bin\Release\net10.0-windows\publish"
if exist "%~dp0MultiBrowser\bin\Release\net10.0-windows\publish\MultiBrowser.exe" (
    cd /d "%~dp0MultiBrowser\bin\Release\net10.0-windows\publish"
    start "" "MultiBrowser.exe"
    exit
)

pause
exit /b
