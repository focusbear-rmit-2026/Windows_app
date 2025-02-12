

azuresigntool sign -kvu https://focusbear-win-signing.vault.azure.net/ -kvc FocusBearWindowsApp2025 -kvt afb40d39-abcb-4f16-b465-cec417d304ec -kvi 2134f727-80cc-4f12-95ad-fe7d52e4801c -kvs 3nd8Q~Ruf5gnxtnqjzlSKwtlJ4~Gq25lhr4jWc45 -tr https://rfc3161.ai.moda/microsoft -td sha256 "Output\uninst-6.2.1-ce35a1f1d7.e32" 

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

azuresigntool sign -kvu https://focusbear-win-signing.vault.azure.net/ -kvc FocusBearWindowsApp2025 -kvt afb40d39-abcb-4f16-b465-cec417d304ec -kvi 2134f727-80cc-4f12-95ad-fe7d52e4801c -kvs 3nd8Q~Ruf5gnxtnqjzlSKwtlJ4~Gq25lhr4jWc45 -tr https://rfc3161.ai.moda/microsoft -td sha256   "output/FocusBearSetup1.0.143.exe"