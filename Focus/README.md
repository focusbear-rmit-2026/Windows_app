<a id="readme-top"></a>

<br />
<div align="center">
  <p align="center">
    Focus Bear Windows application
    <br />
    <br />
    <br />
    <a href="https://github.com/Focus-Bear/windows-app-v2/issues/new?labels=bug&template=bug-report---.md">Report Bug</a>
    &middot;
    <a href="https://github.com/Focus-Bear/windows-app-v2/issues/new?labels=enhancement&template=feature-request---.md">Request Feature</a>
  </p>
</div>



<!-- TABLE OF CONTENTS -->
<summary>Table of Contents</summary>
<ol>
  <li><a href="#built-with">Built With</a></li>  
  <li><a href="#getting-started">Getting Started</a></li>
  <li><a href="#prerequisites">Prerequisites</a></li>
  <li><a href="#installation">Installation</a></li>
  <li><a href="#difference-of-debug-and-release">Difference of Debug and Release</a></li>
  <li><a href="#wpf-file-structure">WPF file structure</a></li>
  <li><a href="#git-work-flow">Git work flow</a></li>
</ol>





<p align="right">(<a href="#readme-top">back to top</a>)</p>



### Built With

This IDE used for this project is Visual Studio. 
* [Visual Studio][Vs-url]

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- GETTING STARTED -->
## Getting Started

This is an example of how you may give instructions on setting up your project locally.
### Prerequisites

Install and set Git on your PC

* [Git][Git-url]

### Installation

_Below is an example of how to clone repo to your local and start running the app._

To clone the repository in Visual Studio:

1. **Clone the repository:**
   - Open Visual Studio.
   - On the Start window, choose **Clone a repository**.
   - Alternatively, if Visual Studio is already open, choose **Git** > **Clone** from the menu bar.
   - In the **Repository URL** field, enter the following URL:
     ```
     https://github.com/Focus-Bear/windows-app-v2.git
     ```
   - Choose a **Path** where you want to clone the repository on your local machine.
   - Click the **Clone** button.  

2. **Add the Interop.TaskScheduler.dll reference.**
   - Obtain the `Interop.TaskScheduler.dll` file (please ask Mainish if you don't have it).
   - In Visual Studio's Solution Explorer (`Ctrl+Alt+L`), right-click on **References** under the **Focus Bear** project.
   - Select **Add Reference...**
   - In the Reference Manager dialog, choose **Browse** from the left-hand menu.
   - Click the **Browse...** button and locate the `Interop.TaskScheduler.dll` file.
   - Select the file and click **Add**.
   - Finally, click **OK** to add the reference.

3. **Choose Debug mode and Start.**
   - In Visual Studio, ensure the **Debug** configuration is selected in the dropdown menu (usually located in the toolbar).
   - Press the **Start** button (often a green triangle) to build and run the application in Debug mode.

4. **Exception Handle.**
If the application encounters exceptions during runtime, you have a couple of options to manage them. You can either click the **Continue** button (often represented by a green triangle in the Visual Studio toolbar) to instruct the program to attempt to proceed despite the exception. Alternatively, for more fine-grained control, you can configure exception handling behavior in Visual Studio by navigating to **Debug** > **Windows** > **Exception Settings**. Within the Exception Settings window, you can specify particular exceptions to be skipped, allowing the program to continue execution without breaking.

<p align="right">(<a href="#readme-top">back to top</a>)</p>


<!-- Difference -->
## Difference of Debug and Release

  The key differences between Debug and Release modes are geared towards their respective purposes. **Debug mode** prioritizes developer convenience, often including behaviors that aid in identifying and fixing issues. For instance, timers might complete more quickly to expedite testing cycles, and developers can Invoke calls directly within `#if DEBUG` in `App.xaml.cs` for targeted debugging. Furthermore, when an exception occurs in Debug mode, the program will typically pause execution, allowing developers to inspect the state and pinpoint the source of the error.

  Conversely, **Release mode** is optimized for the end-user experience. Timers will operate at their intended, normal speed. In the event of an exception, Release builds are generally configured to automatically skip over them and attempt to continue running, aiming for a smoother, uninterrupted user experience, albeit potentially masking underlying issues that would be caught in Debug mode.


<p align="right">(<a href="#readme-top">back to top</a>)</p>

## WPF file structure
  In a typical WPF (Windows Presentation Foundation) project, the file structure for UI elements follows a convention where **each XAML file (.xaml) generally has a corresponding C# code-behind file (.xaml.cs)**.

  The XAML file defines the user interface's layout and appearance using markup. Within the XAML, you can assign a **`Name` attribute** to specific elements. This `Name` acts as an identifier.

  Crucially, to interact with these named elements programmatically in your C# code-behind, **you need to save the XAML file first.** Once the XAML is saved, Visual Studio (or your development environment) will typically generate corresponding fields in the associated `.xaml.cs` file. These generated fields allow you to directly access and manipulate the properties and events of the named XAML elements from your C# code.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## How to debug a window
  To debug a window/page:

  1. **Ensure you are in Debug mode.** This allows you to access debugging tools and information.
  2. **Open the Live Visual Tree.** While your application is running in Debug mode, navigate to **Debug** > **Windows** > **Live Visual Tree** in Visual Studio. This tool provides a hierarchical view of the visual elements in your running application's windows.
  3. **Locate the target window element.** In the Live Visual Tree window, you'll see a tree-like structure representing your UI. Click the small triangle icons next to elements to expand their child elements and navigate down to the specific window or element you want to inspect.
  4. **Identify the XAML source.** The Live Visual Tree helps you pinpoint the XAML file that defines a selected element. Look for information displayed alongside the element in the Live Visual Tree window, which often indicates the corresponding XAML file path.
  5. **Inspect element highlighting.** As you move your mouse cursor over different elements within the Live Visual Tree, the corresponding element in your running application's window will highlight. This visual feedback allows you to easily identify which on-screen element corresponds to the selected node in the Live Visual Tree.



<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- CONTRIBUTING -->
## Git work flow

Here's a basic workflow for using Git within this project:

1.  **Open Git in Command Prompt:** Locate the **Git** option in the Visual Studio title bar (or your preferred Git integration in your IDE). Click on it and select **Open in Command Prompt** (or a similar option to access the Git command line).

2.  **Switch to the Main Branch:** To ensure you're working on the latest stable version, switch to the `main` branch using the following command:
    ```sh
    git checkout main
    ```

3.  **Get the Latest Changes:** Before starting any work, retrieve the most recent updates from the remote repository:
    ```sh
    git pull
    ```

4.  **Create a New Branch:** For your new feature or bug fix, create a dedicated branch. This keeps your changes isolated:
    ```sh
    git checkout -b "your branch name"
    ```
    Replace `"your branch name"` with a descriptive name for your branch.

5.  **Make Code Modifications:** Proceed with your code changes within your newly created branch.

6.  **Check Modified Files:** Before committing, inspect which files have been modified:
    ```sh
    git status
    ```
    - Files displayed in **red** are untracked changes (not yet staged for commit).
    - Files displayed in **green** are staged changes (will be included in the next commit).

7.  **Add Modified Files:** Stage the files you want to include in your commit:
    ```sh
    git add "filename"
    ```
    Replace `"filename"` with the specific file you want to add.  
    If you add new resource like image to the project, please remeber to add "Focus bear.csproj"  

    To add all modified files at once, use:
    ```sh
    git add --all
    ```

8.  **Commit Your Changes:** Once you've staged your changes, commit them locally with a descriptive message:
    ```sh
    git commit -m "Your commit message describing the changes"
    ```
    Replace `"Your commit message describing the changes"` with a concise summary of your work.

9.  **Push to the Remote Repository:** Upload your local branch and commits to the remote repository:
    ```sh
    git push
    ```
    **(Note: If this is the first time pushing this branch to the remote repository, you might see a message with a specific command to set the upstream. If so, please copy and execute that command as instructed.)**

10. **Request Peer Review and Create a Pull Request (PR):** After successfully pushing your branch, navigate to the project's repository on GitHub (or your chosen Git hosting platform). From there, initiate a **Pull Request** to merge your branch into the `main` branch. This process typically involves requesting a **peer review** from other team members to ensure the quality of your code before it's integrated.






<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->

[Vs-url]: https://visualstudio.microsoft.com/
[Git-url]: https://git-scm.com/downloads/win
