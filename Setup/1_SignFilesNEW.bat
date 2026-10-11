"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /a "files/focus bear.exe"
signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /a "files/FocusBear.Core.dll"
 