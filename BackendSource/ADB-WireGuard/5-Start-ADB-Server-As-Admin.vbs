Set shell = CreateObject("Shell.Application")
batPath = Replace(WScript.ScriptFullName, WScript.ScriptName, "5-Start-ADB-Server-As-Admin.bat")
shell.ShellExecute batPath, "", "", "runas", 1
