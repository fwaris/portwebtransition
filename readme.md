# FsPlan & FsPlay
Frameworks for organizing a complex, multi-step workload into a plan of tasks and then executing the plan with the help of a LLM functioning as a computer-use-agent (CUA). 

## FsPlan

A framework for a) defining a 'plan' ([FsPlan](/src/FsPlan/FsPlan.fs#l31)) that is composed of 'tasks' ([FsTask](/src/FsPlan/FsPlan.fs#l10)); and b) running the plan to accomplish a desired overall goal. 

The framework itself is rather abstract. It provides a way of breaking down a complex workload into smaller tasks. The tasks are composed into a plan with a *flow* which can be either [linear or a graph](/src/FsPlan/FsPlan.fs#l27).

FsTask<'T> is a parameterized type where the actual task type 'T is defined at implementation time. How 'T needs to be executed (i.e. how to run a task) is also defined at implementation.

### Plan Flows

In the ***linear*** flow the tasks are executed sequentially. 

For the ***graph*** flow, there is [graph](/src/FsPlan/FsPlan.fs#l24) with a *root* task and a [transition](/src/FsPlan/FsPlan.fs#l17) map. When a task is done and there are outgoing tasks available, the execution passes to the one of the available outgoing tasks. The transition decision is made with the help of a LLM. The transition 'prompt' can be defined as part of the [transition](/src/FsPlan/FsPlan.fs#l17) structure.

> Note: The transition mechanism has not been fully tested yet.

FsPlan was designed to work with FsPlay, described next, but can also be used independently.

## FsPlay

A framework for driving mobile embedded web browsers via an API. Its designed to work with a LLM functioning as a computer-use-agent or CUA. 

A central abstraction is the [IUIDriver](/src/FsPlay.Abstractions/FsPlay.Abstractions.fs) interface. Its an interface designed to drive web browsers - either *mobile embedded* or *desktop*. FsPlay [provides an implementation for mobile embedded browsers](/src/FsPlay/MauiWebViewDriver.fs), for the [Maui](https://dotnet.microsoft.com/en-us/apps/maui) platform. For desktop browsers, the *IUIDriver* interface can be implemented to wrap libraries like Playwright.

#### Implementation Notes

The FsPlay mobile driver injects a [bootstrap](/src/FsPlay/Bootstrap.fs) JavaScript - prior to any website being loaded - i.e., when the mobile web control is instantiated. The IUIDriver implementation then uses the mobile WebView's `EvaluateJavaScriptAsync` method to invoke functions defined in the bootstrap script. For the most part, the functions allow for the following actions:
- Click - buttons, links or to focus into a input field
- Key press - e.g. Alt + LeftArrow
- Type text into a focused text field
- Scroll page up or down

#### Debugging JavaScript
Fortunately, mobile web browsers running in either the IOS simulator; a MacCatalyst app; the Android emulator or an attached device - can be debugged with the 'F12' Developer Tools approach. To attach developer tools, see the following steps for the respective platforms:
- IOS | MacCatalyst
    - Set the option in Safari so that *Developer* menu is available in the top menu bar when Safari is active. The exact steps on how to enable this option have changed over time so please see the latest documentation.
    - Start the MacCatalyst app; the IOS Simulator app; or the attached iPhone app - with the embedded browser loaded in the app
    - Then under the Developer menu you will see an option to connect to the embedded browser and open the developer tools on it

- Android
    - Start the app in the emulator or a attached physical device
    - Open either Chrome or Edge on the desktop
    - Enter `chrome://inspect` or `edge://inspect` in the URL
    - This will bring up a page from which you can connect developer tools to the embedded browser
    - Allow a few seconds for the page to find the embedded browser and show the link

Note: The injected script can be seen in Safari dev tools *Sources* tab under 'User Scripts' but they are not easily visible in Chrome/Edge. An option to locate the script in Chrome/Edge is to enter `window.__fsDriver` in the console. This will allow you to inspect the JavaScript object to which the bootstrap functions are attached. From there you can get to the source and add a breakpoint, etc..

# CuaSample
Included in the repo is a CUA sample app that shows how FsPlan, FsPlay and RTFlow can be combined to execute a plan. Several example plans are included in the sample. See the [readme](/src/samples/CuaSample/readme.md) of the sample for more details.

Note: [RTFlow](/src/RTFlow/readme.md) is a real-time, multi-agent library that can be used to implement task execution with multiple collaborating agents. RTFlow is not strictly required but the CuaSample app uses it nonetheless. 



