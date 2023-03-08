"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

signtool sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /n "Focus Bear Pty Ltd" "files\*.exe" "output/*.e32"

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

 