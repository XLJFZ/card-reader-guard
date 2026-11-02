Set fso = CreateObject("Scripting.FileSystemObject")
root = fso.GetParentFolderName(WScript.ScriptFullName)
CreateObject("WScript.Shell").Run Chr(34) & root & "\CardReaderGuard.exe" & Chr(34) & " --setup", 1, False
