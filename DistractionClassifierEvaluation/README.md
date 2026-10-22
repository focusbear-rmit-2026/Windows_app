# Windows Native Classifier Diagnostics

The Windows classifier runs locally in C# with ONNX Runtime, in shadow mode. It runs the shared relevance model
(`v2.1`, the same files as the Mac and Android apps), which the app downloads on first run from
`https://pub-a0f25f0d306d4d63bbfc52aa624ae389.r2.dev/v2.1/` into `%LOCALAPPDATA%\FocusBear\RelevanceModel\v2.1`.
Every file is checked against the size and SHA-256 compiled into `Core/Relevance/RelevanceModelFiles.cs`; if any
file is missing or wrong, shadow mode stays off.

Set FOCUSBEAR_DISAGREEMENT_EVAL=1 to run five predefined native examples after the model loads. This optional
diagnostic does not call the backend or change blocking decisions.

Debug builds write each prediction (goal, URL, title, probabilities) to
`Logs\native-relevance-shadow.log` in the app data folder, capped at 2 MB plus one rolled copy. Release builds
write only the action, confidence and latency to the app log, never the goal, URL or title.

The two windows_all CSV files and NATIVE_EVALUATION.md are historical results for the earlier 5,545-feature export.
They do not describe the current model.

Live backend disagreement logging runs after the backend answers, off the blocking path
(`RelevanceShadowMode.LogDisagreementWhenDone`). YouTube watch predictions are deferred to the browser's
stable-title queue.
