' Abre la prueba local (scripts/run-local.ps1) sin ninguna ventana de consola ni navegador: sólo aparece la caja.
' Uso: doble clic, o  wscript scripts\abrir-local.vbs [-Datos <carpeta>] [otras opciones de run-local.ps1]
' La salida del script queda en %TEMP%\casamiga-run-local.log; la nube local, en <Datos>\nube\nube.log.
Option Explicit
Dim shell, fso, script, args, i, comando
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
script = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "run-local.ps1")
args = ""
For i = 0 To WScript.Arguments.Count - 1
    args = args & " """ & WScript.Arguments(i) & """"
Next
' Node.js recién instalado puede no estar aún en el PATH heredado.
shell.Environment("Process")("PATH") = shell.ExpandEnvironmentStrings("%ProgramFiles%\nodejs") & ";" & shell.Environment("Process")("PATH")
comando = "cmd /c powershell -NoProfile -ExecutionPolicy Bypass -File """ & script & """ -SinNavegador" & args & _
    " > """ & shell.ExpandEnvironmentStrings("%TEMP%") & "\casamiga-run-local.log"" 2>&1"
shell.Run comando, 0, False
