# NODUS Driver preview hardware test

The goal of the preview is to validate the same simple flow across different real PCs:

**install → open → automatic scan → review → update**

## Supported preview target

- Windows 10 x64
- Windows 11 x64

## Pass criteria

A build is testable when it can:

- start normally from the installed shortcut;
- enumerate installed PnP driver entries;
- complete a Windows Update Agent driver scan without freezing the UI;
- display zero or more applicable driver updates;
- select updates individually;
- download/install selected updates through Windows Update;
- show understandable failure text and retain a local log;
- report when Windows requires a restart.

## Feedback to capture

For a new PC, capture:

1. app immediately after launch;
2. scan completed;
3. update list, if any;
4. any error shown;
5. install result, only if an update is tested.

If scan or install fails, attach or quote the latest log from `%LOCALAPPDATA%\NODUS Driver\logs` after removing anything you do not want to share.
