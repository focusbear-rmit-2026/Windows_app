

azuresigntool sign -kvu https://focusbear-win-signing.vault.azure.net/ -kvc FocusBearWindowsApp2025 -kvt afb40d39-abcb-4f16-b465-cec417d304ec -kvi 2134f727-80cc-4f12-95ad-fe7d52e4801c -kvs QVC8Q~J4gysDZ1R2s8sl1rBt2ewCbdtLP.Bc6chU -tr http://timestamp.digicert.com -td sha256 "Output\uninst-6.4.3-fcf1aec026.e32"

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

azuresigntool sign -kvu https://focusbear-win-signing.vault.azure.net/ -kvc FocusBearWindowsApp2025 -kvt afb40d39-abcb-4f16-b465-cec417d304ec -kvi 2134f727-80cc-4f12-95ad-fe7d52e4801c -kvs QVC8Q~J4gysDZ1R2s8sl1rBt2ewCbdtLP.Bc6chU -tr http://timestamp.digicert.com -td sha256   "output/FocusBearSetup1.0.181.exe"