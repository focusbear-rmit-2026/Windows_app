\# Focus Bear Windows – Distraction Classifier Integration



This repository contains the Windows-side work completed for the Focus Bear Distraction Classifier project.



\## Windows Integration



The Windows application was integrated with the local distraction classification model using `RelevanceModelClient.cs`.



The integration sends:

\- Focus intention

\- Current browser tab title

\- Current URL



to the local `/predict` endpoint.



The local classifier runs in shadow mode alongside the existing Focus Bear backend so that their predictions can be compared without changing the existing blocking behaviour.



\## Disagreement Evaluation



A Windows disagreement evaluator was implemented to compare:



\- Local distraction classifier decision

\- Existing Focus Bear backend decision



Three provided evaluation datasets were tested.



Total examples tested: \*\*900\*\*



Results:



\- Agreements: \*\*455\*\*

\- Disagreements: \*\*445\*\*

\- Failed / Not compared: \*\*0\*\*

\- Agreement rate: \*\*50.56%\*\*

\- Disagreement rate: \*\*49.44%\*\*



\## Repository Contents



\### API



`API/RelevanceModelClient.cs`



Client used by the Windows application to communicate with the local distraction classifier `/predict` endpoint.



\### DistractionClassifierEvaluation



`DisagreementEvaluator.cs`



Runs the Windows disagreement evaluation and compares the local model result with the existing backend result.



`windows\_all\_900\_results.csv`



Contains the complete results for all 900 evaluated examples.



`windows\_all\_disagreements.csv`



Contains only the 445 cases where the local classifier and existing backend disagreed.



Additional implementation details are available in:



`DistractionClassifierEvaluation/README.md`



\### Windows Integration Changes



`windows\_integration\_changes.patch`



Contains the Windows integration changes made to existing application files, including the evaluation trigger, developer flag, project configuration, and shadow-mode comparison logic.



\## Evaluation Behaviour



The evaluation uses the existing backend allow threshold of \*\*0.80\*\*.



A disagreement occurs when:



\- The local classifier returns \*\*Allow\*\* while the backend returns \*\*Block\*\*, or

\- The local classifier returns \*\*Block\*\* while the backend returns \*\*Allow\*\*.



The disagreement evaluation is developer-only and does not replace the existing Focus Bear blocking behaviour.

