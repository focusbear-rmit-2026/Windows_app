# Windows Distraction Classifier Evaluation

This folder contains the Windows-side disagreement evaluation for the Focus Bear distraction classifier.

## Purpose

The evaluator compares the local distraction classifier prediction with the existing Focus Bear backend result to identify agreement and disagreement cases.

## Evaluation Data

Three datasets were evaluated:

- `tier1_500_low_overlap_examples.csv` - 500 examples
- `tier1_200_low_overlap_indirect_examples.csv` - 200 examples
- `tier1_200_zero_overlap_semantic_examples_v2.csv` - 200 examples

Total examples evaluated: **900**

## Results

- Agreements: **455**
- Disagreements: **445**
- Total processed: **900**
- Failed / Not Compared: **0**

## Output Files

- `windows_all_900_results.csv` - Results for all 900 examples.
- `windows_all_disagreements.csv` - Contains only the 445 disagreement cases.
- `windows_results_500_low_overlap.csv` - Results for the 500 low-overlap examples.
- `windows_results_200_low_overlap_indirect.csv` - Results for the 200 low-overlap indirect examples.
- `windows_results_200_zero_overlap_semantic.csv` - Results for the 200 zero-overlap semantic examples.

## Implementation

`DisagreementEvaluator.cs` runs the evaluation on Windows.

The evaluator:

1. Reads the evaluation examples from CSV.
2. Gets the prediction from the local distraction classifier.
3. Gets the result from the existing Focus Bear backend.
4. Compares the local and backend decisions.
5. Saves the agreement and disagreement results to CSV.

The existing Focus Bear blocking behaviour is not changed by this evaluation.