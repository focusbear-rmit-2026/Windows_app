signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /a "Output\uninst-6.4.3-91006bc295.e32"

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /a "output/FocusBearSetup4.1.252.exe"

