#!/bin/sh
set -e
cd "$(dirname "$0")"
API=/usr/lib/mono/4.5-api
SRC="src/AssemblyInfo.cs src/Core.cs src/Logs.Locations.cs src/Logs.Etl.cs src/Logs.EventLog.cs src/Net.Remote.cs src/Net.Update.cs src/Logs.Follow.cs src/Ui.Follow.cs src/Ui.Base.cs src/Ui.Controls.cs src/Ui.Controls2.cs src/Ui.Grid.cs src/Ui.Browse.cs src/Ui.Highlights.cs src/Ui.Settings.cs src/Ui.MainForm.cs src/Ui.Lookup.cs src/Ui.Notes.cs src/Ui.Updater.cs src/Program.cs"
mcs -langversion:5 -target:winexe -platform:anycpu -optimize+ -warn:4 -warnaserror+ -noconfig -nostdlib \
  -r:$API/mscorlib.dll -r:$API/System.dll -r:$API/System.Core.dll -r:$API/System.Management.dll -r:$API/System.Drawing.dll -r:$API/System.Windows.Forms.dll \
  -win32icon:LogVice.ico -resource:LogVice.ico,LogVice.ico -resource:CHANGELOG.md,CHANGELOG.md -out:LogVice.exe $SRC
echo EXE_OK
