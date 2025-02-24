# Project Name

## Description
This project is a WPF application developed in C# for Focus Bear Windows App.  

## Prerequisites
- Microsoft Visual Studio Professional 2019
- [.NET Framework 4.7.2 ]
- INNO Setup 6 UNICODE to build the setup
- 
## Getting Started

### Cloning the Repository
To get a local copy of this project, you can clone it from GitHub.

## Opening the Project
Open Microsoft Visual Studio Professional 2019.
Go to File > Open > Project/Solution.
Navigate to the folder where you cloned the repository and select the .sln file.

## Building the Project
To compile the project:

In Visual Studio, ensure that the solution is set to build in Release mode for production.
You can change this in the toolbar where it says Debug, select Release instead.
Go to Build > Build Solution or press Ctrl + Shift + B.

## Upgrading the App Version
To update the application version:
Open the Assembly.cs file located in the Properties folder.
Locate the following lines:
[assembly: AssemblyVersion("1.0.131.0")]
[assembly: AssemblyFileVersion("1.0.131.0")]

Change the version numbers as needed following semantic versioning guidelines (major.minor.patch).

## Dependencies 
All dependencies along with packages are already included in the project and the soultion

## Setup
Read the HowToCreateSetup.txt file in Setup Folder


### Naming Conventions

This project follows a consistent naming convention for variables, properties, and other identifiers to ensure clarity and maintainability. Below are the conventions used:

#### 1. **Prefixes for Variable Types**
   - **`b`**: Boolean variables (e.g., `bRestartAppNeeded`, `bHasUserEditedSettings`).
   - **`s`**: String variables (e.g., `sMacAddress`, `sGlobalUUID`).
   - **`n`**: Numeric variables (e.g., `nHabitIndex`, `nTrialDays`).
   - **`d`**: DateTime variables (e.g., `dLastActivityPerformedStartTime`, `dSettingsFetchedTime`).
   - **`arr`**: Arrays or lists (e.g., `arrGLOBALAllMorningHabits`, `arrAllbreakActivities`).
   - **`obj`**: Object references (e.g., `objChild`).
   - **`wnd`**: Window or form references (e.g., `wndGentleBlock`).

#### 2. **PascalCase for Public Properties**
   - Public properties use PascalCase (e.g., `IsLoggedIn`, `AppVersion`, `CurrentActivityType`).

#### 3. **camelCase for Private Fields**
   - Private fields use camelCase (e.g., `currentHabitIndex`, `isAppEnable`).

#### 4. **Descriptive Names**
   - Variable and property names are descriptive and reflect their purpose (e.g., `MorningHabitsShownTillDate`, `FocusSummaryShownTillDate`).

#### 5. **Constants**
   - Constants are written in uppercase with underscores (e.g., `MAX_RETRY_COUNT`, `DEFAULT_TIMEOUT`). (Note: No constants are shown in the provided code, but this is a general convention.)

#### 6. **Enums**
   - Enum types and values use PascalCase (e.g., `CurrentActivityType`, `FocusMode`).

#### 7. **Collections**
   - Collections (e.g., lists, arrays) are prefixed with `arr` or use plural names to indicate multiple items (e.g., `arrAllMorningHabits`, `InstalledApps`).

#### 8. **Boolean Variables**
   - Boolean variables often start with `is`, `has`, or `b` to indicate their true/false nature (e.g., `isPurchased`, `bBlockWhileOnbording`).

#### 9. **Temporary or Local Variables**
   - Temporary variables may include `Temp` in their name (e.g., `currentActivityChoiceTemp`).

#### 10. **API-Related Variables**
   - Variables storing API-related data often include `Raw` or `API` in their names (e.g., `sRawUserSettings`, `sRawHabitPacks`).

#### 11. **ObservableCollections**
   - ObservableCollections are prefixed with `_Obj` or use descriptive names (e.g., `_ObjHabitListSelectable`, `arrFocusModes`).

#### 12. **Special Cases**
   - Variables related to specific features or modules may include the feature name (e.g., `GentleBlock`, `SuperPomodoro`).

---

### Examples

| **Type**               | **Example**                                |
|-------------------------|--------------------------------------------|
| Boolean variable        | `bRestartAppNeeded`, `isExpired`           |
| String variable         | `sMacAddress`, `sGlobalUUID`              |
| Numeric variable        | `nHabitIndex`, `nTrialDays`               |
| DateTime variable       | `dLastActivityPerformedStartTime`         |
| List/Array variable     | `arrGLOBALAllMorningHabits`               |
| Public property         | `IsLoggedIn`, `AppVersion`                |
| Enum                    | `CurrentActivityType`, `FocusMode`        |
| ObservableCollection    | `_ObjHabitListSelectable`, `arrFocusModes`|
| API-related variable    | `sRawUserSettings`, `sRawHabitPacks`      |

---

### Notes
- These conventions are designed to improve code readability and maintainability.
- Consistency is key: ensure all team members adhere to these conventions when contributing to the project.


