ROWSENSE WINDOWS — .NET 8 / WPF VERSION

WHY THIS VERSION
----------------
This is a Windows-native rewrite that removes Qt/QML and therefore removes the qmlimportscanner/windeployqt failure completely.
It uses C# + .NET 8 + WPF + Windows BLE APIs.
For a Windows 10 BLE desktop app this is a much cleaner choice than Java.

WHAT IS IMPLEMENTED
-------------------
- Windows 10/11 x64 desktop app.
- Boat classes: 1x, 2x, 2-, 4x, 4-, 4+, 8+.
- External/internal lever settings.
- BLE scan for RowSense-Central.
- Fixed RowSense BLE UUIDs.
- LIVE_TX notification subscription.
- Exact 76-byte SerialStrokeData parser, little-endian.
- Validation athlete 1..8, two=0, count=15, reserved=0 and finite floats.
- Same lever corrections as current Qt logic.
- Athlete cards with current power, average power, tempo, Fmax and force curve.
- Athlete checkboxes; visible graphs re-layout automatically.
- START / FINISH commands over CONTROL_RX (0x01 / 0x02).
- Local JSON recording in Documents/RowSense/Trainings.
- Trainings button opens saved training folder.
- GitHub Actions Windows build without Qt.

GITHUB
------
1. Create a NEW empty repository.
2. Unzip this archive.
3. Upload ALL CONTENTS of the RowSense-Windows-DotNet folder to repository root.
4. IMPORTANT: .github/workflows/windows-build.yml must exist in the repo.
5. Commit.
6. Open Actions -> Build RowSense Windows .NET.
7. Wait for green check.
8. Download artifact RowSense-Windows-DotNet-x64.
9. Unzip artifact and launch RowSenseWindows.exe.

BLE CONTRACT
------------
Device name: RowSense-Central
Service:    8A4F1000-7C2B-4E91-A6D5-52A19F3C7001
CONTROL_RX: 8A4F1001-7C2B-4E91-A6D5-52A19F3C7001
CONTROL_TX: 8A4F1002-7C2B-4E91-A6D5-52A19F3C7001
LIVE_TX:    8A4F1003-7C2B-4E91-A6D5-52A19F3C7001
STATUS_TX:  8A4F1005-7C2B-4E91-A6D5-52A19F3C7001

START_RECORDING = 0x01
STOP_RECORDING  = 0x02
GET_STATUS      = 0x03

NOTE
----
This first .NET rebuild focuses on getting the Windows BLE/live/recording path working reliably.
The saved-training review UI can be extended next; the JSON files are already stored locally and compatible with that next step.
