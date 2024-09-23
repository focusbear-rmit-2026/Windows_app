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

