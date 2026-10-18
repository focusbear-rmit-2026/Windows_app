# Windows native relevance evaluation — 18 October 2026

## Scope and implementation
C#/.NET ONNX Runtime in SHADOW mode. Existing backend/app rules continue to control actual blocking. No Python/Flask inference service is required. These are offline saved-input replays, not end-to-end browser tests or backend comparisons.

Each run used 762 records, three sequential passes, shuffle seed 42, Release x64 without debugger, startup warmup completed and diagnostic logging enabled. Model and input hashes matched across runs. Goal cache was not reset between passes. Each run completed 2,286 calls without runtime errors. Thread settings were verified in the shadow logs; older manifests did not record them. Newly generated manifests record settings and executable hash.

## Latency evidence
Prediction timer excludes diagnostic serialization/file logging. Full-call measurements include it. Model loading and startup warmup are separate.

| Run | Threads | Cleaning | Pass | Median ms | P95 ms | Max ms | Calls >50 ms |
|---|---:|---|---:|---:|---:|---:|---:|
| 1 | 1 | on | 1 | 8.91 | 14.97 | 32.08 | 0 |
| 1 | 1 | on | 2 | 8.95 | 14.96 | 22.70 | 0 |
| 1 | 1 | on | 3 | 9.03 | 14.55 | 22.36 | 0 |
| 2 | 1 | off | 1 | 22.14 | 41.71 | 165.66 | 14 |
| 2 | 1 | off | 2 | 21.98 | 40.12 | 164.49 | 15 |
| 2 | 1 | off | 3 | 16.24 | 34.05 | 163.34 | 7 |
| 3 | 1 | on | 1 | 21.98 | 40.98 | 65.88 | 15 |
| 3 | 1 | on | 2 | 21.90 | 38.82 | 64.95 | 11 |
| 3 | 1 | on | 3 | 14.70 | 27.79 | 65.59 | 1 |
| 4 | 2 | on | 1 | 16.65 | 31.80 | 53.79 | 1 |
| 4 | 2 | on | 2 | 14.75 | 25.68 | 45.34 | 0 |
| 4 | 2 | on | 3 | 15.14 | 25.64 | 43.86 | 0 |

Run 4's full-call maximum was 77.20 ms. Therefore a strict every-call-under-50-ms requirement is not met. Machine conditions varied: do not attribute every timing difference to a setting change. Run 4 reduced >50 ms prediction calls from 27 to 1 compared with run 3, supporting two threads as a candidate on the tested 20-logical-processor laptop. This is not validation across hardware or a new default.

The encoded email tracking record (677) used 215 tokens without cleaning and 21 with cleaning. In adjacent runs 2 and 3 its prediction latency fell from 163–166 ms to 18–27 ms. Cleaning affected that one record per pass. Its class stayed Block but its confidence/probabilities changed. No predicted classes changed across these runs; two-thread probability differences were at most 2.4e-7 relative to run 3.

## Agreement with reference labels
All 762 rows: 662/762 correct (86.88%), macro F1 0.7977. Reference Caution recall: 45/94 (47.87%). Confusion matrix rows are reference, columns predicted; order Block, Caution, Allow:

| Reference | Block | Caution | Allow |
|---|---:|---:|---:|
| Block | 261 | 5 | 23 |
| Caution | 31 | 45 | 18 |
| Allow | 18 | 5 | 356 |

80 rows share exact goal/context/URL with reconstructed training inputs. Excluding them leaves 682 rows with 85.78% agreement, macro F1 0.7456 and Caution recall 25/72 (34.72%). The remaining rows are NOT proven independent. Labels derive from AIRelevanceScore and are not verified human ground truth. Saved split recovery does not establish exclusion from the exact exported models' training.

49 reference-Caution disagreements remain: 31 Block, 18 Allow. All three models agreed on the same non-Caution class in 34 cases. Ambiguous goals, incomplete context, encoding issues and label uncertainty need review. Do not relabel to match predictions or tune thresholds on these evaluation records. No model weights or thresholds were changed.

## Reproduction
Keep dataset and raw logs local. Supply an EvaluationInput JSON with Rows containing unique Id, Goal, Context, Url, Title, ExpectedTier (0–2), TrainingInputOverlap and a Limitations array. Use the deployed model assets in RelevanceClassifier next to the executable. Validation accepts URLs lacking a scheme, but the original URL is passed unchanged into prediction.

In a repository Command Prompt, after closing the existing Focus Bear process:

```bat
set "FOCUSBEAR_DISAGREEMENT_EVAL=1"
set "FOCUSBEAR_EVALUATION_INPUT=%CD%\DistractionClassifierEvaluation\recovered-evaluation-762.json"
set "FOCUSBEAR_TRACKING_CONTEXT_EXPERIMENT=1"
set "FOCUSBEAR_ONNX_THREADS=2"
start "" "bin\Release\Focus Bear.exe"
```

Use experiment=0 for original embedding preprocessing, threads=1 for the default thread configuration. Evaluation is off unless opted in. Tracking cleaning is off by default; two threads remain an environment override. Output: %LOCALAPPDATA%\FocusBear\Evaluation. Shadow details: %LOCALAPPDATA%\FocusBear\native-relevance-shadow.log.

Clear the four variables in the same terminal after evaluation by setting each to an empty value, and restart the app for normal operation. Also check local .env overrides if used.

## Remaining acceptance work
Build and affected automated tests after this documentation/manifest update; short live browser regression check; review final diff; push selected files and inspect CI. Do not claim complete Caution accuracy, independent holdout accuracy, guaranteed sub-50-ms latency, or backend agreement from these results. Reviewers must approve promotion of experimental preprocessing/default settings.
