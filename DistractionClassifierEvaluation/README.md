# Windows native classifier diagnostics

## Scope and current status

Windows runs the shared v2.1 relevance model locally in C#/.NET with ONNX Runtime. There is no Flask server, Python runtime or local Python HTTP API in the Windows inference path. This integration is experimental and off by default. When explicitly enabled it runs in **shadow mode**: the backend controls blocking, while local predictions and disagreements provide diagnostic evidence.

The evaluation sources and historical datasets in this directory are excluded from production app compilation/packaging. The old startup evaluation hooks have been removed. Setting `FOCUSBEAR_DISAGREEMENT_EVAL=1` no longer runs five examples automatically in the production app.

## Shared model and installation

The shared model has **6,313 features**:

| Feature block | Dimensions |
| --- | ---: |
| Goal TF-IDF | 533 |
| Page context TF-IDF | 5,000 |
| Scalar features | 12 |
| Goal MiniLM embedding | 384 |
| Page MiniLM embedding | 384 |
| Total | 6,313 |

Logistic regression, XGBoost and tuned XGBoost use a 1:2:2 weighted soft vote. Browser suffix removal, homepage feed handling and the goal-aware email Allow rule are shared integration behavior; the email override is reported as `EmailGoalRule` in summaries.

On an opt-in launch, missing or invalid assets are downloaded in the background from:

```text
https://pub-a0f25f0d306d4d63bbfc52aa624ae389.r2.dev/v2.1/
```

The seven files are `minilm.onnx`, `vocab.txt`, `lr.onnx`, `xgb.onnx`, `xgb_tuned.onnx`, `tfidf_goal.json` and `tfidf_context.json`. Expected byte sizes and SHA-256 values are compiled into `Core/Relevance/RelevanceModelFiles.cs`; the remote manifest is not the trust anchor. Downloads use temporary files, are verified before installation, and repair existing corrupt files using atomic replacement. If verification, download or model loading fails, the classifier remains unavailable and the backend continues its normal work.

Cache location:

```text
%LOCALAPPDATA%\FocusBear\RelevanceModel\v2.1
```

Do not rename files from the previous uploaded-model cache or use `Install-UploadedModels.ps1` for shared v2.1. Model binaries do not need to be checked into Git or packaged in the installer.

## Build and opt in on a research machine

Use Visual Studio on Windows with the .NET Framework 4.7.2 targeting tools, the repository's WPF build prerequisites, and the .NET SDK needed by its test projects. Restore packages and build **Release / Any CPU**. The application retains `Prefer32Bit=true`; do not force x64 merely to load ONNX.

The current local experimental configuration pins both `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.OnnxRuntime.Managed` to **1.22.1** for x86 compatibility. This pin is a temporary compatibility choice, not production approval. A supported production runtime/security and architecture decision remains open with maintainers.

Fully quit Focus Bear before rebuilding or launching another copy. From the repository root in Developer PowerShell:

```powershell
$env:FOCUSBEAR_NATIVE_RELEVANCE = "1"
$env:FOCUSBEAR_FEATURE_DIAGNOSTICS = "0"
Start-Process -FilePath ".\bin\Release\Focus Bear.exe"
```

The app reads the native relevance flag once per process. A local, git-ignored `.env` may also opt in with `NATIVE_RELEVANCE=true`. An explicit environment value of `0` overrides that opt-in. Restart the app after changing a flag. First-run download, verification, model loading and warm-up run on a background task after `StartWorkFlow()` is invoked; inference remains unavailable until loading finishes.

To explicitly disable the feature on the next launch, fully quit the app and launch from a shell with:

```powershell
$env:FOCUSBEAR_NATIVE_RELEVANCE = "0"
$env:FOCUSBEAR_FEATURE_DIAGNOSTICS = "0"
Start-Process -FilePath ".\bin\Release\Focus Bear.exe"
```

The installer excludes the ONNX libraries and model assets. Native opt-in testing therefore uses the development build with matching managed/native runtime libraries present.

## Scheduling and privacy

Browser and GentleBlock callers capture input strings before asynchronous work and share a single non-waiting inference slot. Duplicate samples can reuse a recent result; busy optional samples are skipped instead of building an inference backlog. YouTube content predictions can be deferred to the browser's settled-title path. GentleBlock never awaits the shadow prediction before making its backend blocking decision. Disagreements are recorded only when both results are available.

Release classifier summaries omit goals, URLs, titles and exception messages. They record action, confidence, email-rule status and inference latency; load and disagreement summaries add operational metrics. This statement concerns classifier diagnostics, not an audit of every app log.

Summary log:

```text
%LOCALAPPDATA%\Focus Bear\Logs\Debug_Logs.txt
```

Detailed records require a **Debug build** and both native relevance and feature diagnostics opt-ins (`FOCUSBEAR_FEATURE_DIAGNOSTICS=1`). They may contain goals, URLs, titles and probabilities. Their file `native-relevance-shadow.log` is capped at 2 MB plus one rolled copy. Release builds do not produce these detailed records.

## Windows verification recorded on 24 October 2026

These observations came from the local Windows development build, not from a signed production installer or a new CI run.

| Check | Observed result |
| --- | --- |
| Release / Any CPU rebuild | 5 succeeded, 0 failed |
| Core.Tests, Release net472 | 1,739 passed, 0 failed |
| Core.Tests, Release net8.0 | 1,739 passed, 0 failed |
| App.Tests, Release | 279 passed, 0 failed |
| Shared model startup | `Mode=SHADOW`, `Version=v2.1`, `LoadMs=17553`, `WarmUpMs=37` |
| Explicitly disabled launch | Normal panel; no new classifier entries observed |
| Disabled launch from a copy without ONNX DLLs | Basic startup/panel smoke test passed |
| Temporary unsigned Inno Setup compile of corrected script | Exit code 0 |
| Earlier temporary compile compression-log scan | No matching runtime/model/evaluation payload entries |

`LoadMs` includes the first-run background installation and initialization work; it is not page inference latency. No-ONNX startup was tested with an explicit flag value of `0`, not every possible environment or blocking flow. The installer was compiled but not installed; signed release installation and upgrades remain untested.

Three Release shadow summaries after the v2.1 load were:

| Local timestamp | Action | Confidence | LatencyMs |
| --- | --- | ---: | ---: |
| 12:52:57.006 | Allow | 0.8920 | 15.07 |
| 12:53:55.270 | Block | 0.8931 | 31.57 |
| 12:55:23.031 | Block | 0.8931 | 16.46 |

These are smoke-test observations, not an accuracy result or a representative latency benchmark. The relevant/irrelevant browser checks used a Python-learning focus goal, but Release logs intentionally omit page identities, so page attribution relies on test timing.

Commands used for the test suites, from the repository root:

```powershell
dotnet test ".\Core.Tests\FocusBear.Core.Tests.csproj" --framework net472 --configuration Release
dotnet test ".\Core.Tests\FocusBear.Core.Tests.csproj" --framework net8.0 --configuration Release
# Build this WPF-dependent test project using Visual Studio MSBuild when necessary:
msbuild ".\App.Tests\FocusBear.App.Tests.csproj" /restore /t:Build /p:Configuration=Release
dotnet test ".\App.Tests\FocusBear.App.Tests.csproj" --configuration Release --no-build --no-restore
```

## Remaining evidence and rollout decisions

- Obtain maintainer agreement on a production ONNX runtime/architecture choice; the temporary x86 pin remains unresolved.
- Replay the canonical held-out and hard datasets through the current Windows C# implementation/runtime, retain row-level comparison evidence and summarize accuracy, macro-F1 and per-tier behavior. That replay has not been completed for this local configuration. Do not re-enable production startup evaluation hooks to perform it.
- Measure representative C# Release latency without a debugger, reporting sample count, hardware, warm/cold conditions and percentiles. Three live samples do not establish those statistics.
- Review the complete PR diff and CI results, including previously committed changes.
- Before wide rollout, move the model host to the bucket's production custom domain, as requested in Jeremy's review. The present r2.dev endpoint remains the development configuration.

Jeremy's PR #1696 describes separate reference-harness parity and model limitations. Its reported 88.06% held-out accuracy and 40% hard-set accuracy are not measurements from the local 1.22.1 Windows configuration documented here.

The two `windows_all` CSV files and `NATIVE_EVALUATION.md` are historical results for the earlier 5,545-feature export. Keep them clearly separated from shared v2.1 results; they do not validate the current model.
