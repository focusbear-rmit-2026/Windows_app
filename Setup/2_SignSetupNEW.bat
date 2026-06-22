signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /a "Output\uninst-6.4.3-c91c81e1e4.e32"

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /a "output/FocusBearSetup3.0.219.exe"

