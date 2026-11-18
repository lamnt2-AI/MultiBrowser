@echo off
title Khoi dong MultiBrowser
cd /d "%~dp0MultiBrowser\bin\Release\net10.0-windows\publish"
if not exist "MultiBrowser.exe" (
    echo [LOI] Khong tim thay MultiBrowser.exe!
    pause
    exit /b
)
start "" "MultiBrowser.exe"
exit
