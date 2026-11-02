Set fso = CreateObject("Scripting.FileSystemObject")
root = fso.GetParentFolderName(WScript.ScriptFullName)
fso.CreateTextFile(root & "\stop.flag", True).Close
