"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "FocusBearSetup.iss"   

azuresigntool sign -kvu https://focusbear-win-signing.vault.azure.net/ -kvc FocusBear-win-code-signing-cert -kvt afb40d39-abcb-4f16-b465-cec417d304ec -kvi 2134f727-80cc-4f12-95ad-fe7d52e4801c -kvs 3nd8Q~Ruf5gnxtnqjzlSKwtlJ4~Gq25lhr4jWc45 -tr https://rfc3161.ai.moda/microsoft -td sha256 "files\focus bear.exe" 



 