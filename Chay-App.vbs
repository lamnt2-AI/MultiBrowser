Set WshShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
currentDir = fso.GetParentFolderName(WScript.ScriptFullName)
publishDir = currentDir & "\MultiBrowser\bin\Release\net10.0-windows\publish"
WshShell.CurrentDirectory = publishDir
dotnetExe = WshShell.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\Microsoft\dotnet\dotnet.exe"

If fso.FileExists(dotnetExe) Then
    WshShell.Run """" & dotnetExe & """ MultiBrowser.dll", 0, False
Else
    WshShell.Run "dotnet MultiBrowser.dll", 0, False
End If
