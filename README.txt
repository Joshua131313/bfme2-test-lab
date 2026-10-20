BFME2 Test Lab 1.0.5

A small Windows application for testing BFME2 1.00 hybrid patches.

START
Extract the entire ZIP and run Install.cmd to install the app and create
desktop/Start-menu shortcuts. Alternatively, run BFME2-Test-Lab.exe directly. Keep the other
files beside the EXE. Windows asks for administrator approval because BFME2
is usually installed in Program Files. This app is unsigned. Requires
Windows 10/11 with .NET Framework 4.7.2 or later (normally already included).
No Python, Git, PowerShell modules or separate .NET desktop runtime required.

The application finds common BFME2 locations, including names with/without
'(tm)'. Browse lets you choose another installation. It remembers your choice.

BUTTONS
Test changes: install the selected test package, backing up original files.
Restore game: return the engine, launcher and INI to verified original 1.00.
Check files: identify original, selected test, recognized prior test or
unsupported files, and check the 1.00 base data without changing game files.
Get latest files: download the newest test package from GitHub Releases,
verify its checksum and select it. Click Test changes to apply it.
Load ZIP: select a test ZIP copied from another computer, without internet.
Update app: download and verify a newer application release, then close,
replace the app files and reopen automatically. Game files are untouched.
Release notes: open the latest GitHub release page.
Copy log: copy diagnostic output for reporting a failed install or a bug.

Test05 is bundled for offline use. Downloads update GAME TEST PACKAGES;
application updates use the separate Update app button.
GitHub API rate limits or network failures leave the previous package usable.

COMPATIBILITY
Start with original 1.00 selected in your patch switcher. Existing Test04 is
recognized with the included package; its original backups can be reused.
For an older installed test, load that test ZIP first so the app can recognize
its hashes, then load/download the newer ZIP. Restore before using a different
patch switcher. Close the game before Test changes or Restore game.

The app replaces only game.dat, INI.big and lotrbfme2.exe. It verifies original
core baseline data, payload hashes, backups and staged outputs. A different
readable Maps.big produces a warning and is left unchanged; missing or invalid
map archives are refused. Map-specific gameplay compatibility is not certified. Failed installs
attempt rollback. Unknown files are refused. Map packs and optional DLLs are
not changed. These can still affect gameplay. Backups stay in the game folder
under _BFME2_TestLab_Backup; previous test backup folders are retained.
Cached test packages and settings are under %LOCALAPPDATA%\BFME2TestLab.

The bundled build uses the 1.09v3 engine with 1.00 balance data, fear-command
blocking, accepted Bloodthirsty compatibility changes, and 1.09 swoop timing.
The timing affects Fellbeasts, mounted Witch-king, Drogoth and Dragon Strike.
Damage per hit stays 1.00; effective damage per second can change. Fellbeast
improvement has been user-tested; all abilities and modes are not certified.

SECURITY AND PUBLISHING
Only fixed manifest/payload files are imported. Scripts inside downloaded
ZIPs are never run. GitHub asset SHA-256 and individual payload hashes are
checked. GitHub account compromise could still compromise published binaries;
hashes are integrity checks, not an independent code signature.
The source repository contains the app/installer source, not game payloads.
Release assets hold the test packages. The app does not collect telemetry.

Source and releases: https://github.com/Joshua131313/bfme2-test-lab

UPDATING FROM 1.0.0 OR 1.0.1
Close the old app, extract this app ZIP, and run Install.cmd again (or run the
new EXE directly). Get latest files updates game packages, not the app itself.
There is no need to restore the game just to update this application.
Once version 1.0.2 is installed, future app updates use Update app.

ENGINE IDENTIFICATION (1.0.3)
Check files prints version, size and SHA-256 of the engine, launcher and INI.
A different game.dat can be replaced if its embedded version is exactly
1.0.2194.40862, its x86 PE layout passes structural checks, and original 1.00
INI and core data checks pass. This identifies a candidate variant, not an
authenticated original binary. The executable is not run during detection.
Its exact bytes are preserved in _BFME2_TestLab_Variant_Backup, with a hash
record used for reinstall and restore. Corrupt backups are refused. Unknown
versions still stop and print diagnostics. Launcher and INI checks remain
strict. This cannot guarantee compatibility with an unseen executable.

LEGACY TEST01 MIGRATION (1.0.4)
The exact released Test01 game.dat and INI.big hashes are recognized without
importing its older format-1 package. Its verified original backups under
_BFME2_100_Test01_Backup are reused for upgrade/restore. Missing or corrupt
original backups still stop installation rather than treating Test01 as stock.

HYBRID TEST05 (1.0.5)
Candidate fear/stance fix: 1.09v3 engine with a custom guard on player group stance orders. Orders are rejected while the AI command-lock flag is active. Data and launcher are byte-identical to Test04. This is not an in-game confirmed fix.
Existing users: Update app, then Get latest files, then Test changes with BFME2 closed. All multiplayer participants need the same build. Restore game restores backed-up originals.
Validation: 40 x86 guard cases passed; Windows image mapping and package import passed.
