Set fso = CreateObject("Scripting.FileSystemObject")
root = fso.GetParentFolderName(WScript.ScriptFullName)
If fso.FileExists(root & "\stop.flag") Then fso.DeleteFile root & "\stop.flag", True
CreateObject("WScript.Shell").Run Chr(34) & root & "\CardReaderGuard.exe" & Chr(34), 1, False
