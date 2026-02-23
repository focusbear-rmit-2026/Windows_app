signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /a "Output\uninst-6.4.3-b0d8f529bb.e32"

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /a "output/FocusBearSetup2.0.198.exe"

