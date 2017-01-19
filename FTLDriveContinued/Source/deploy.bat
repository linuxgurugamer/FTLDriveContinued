rem line must not be used

set H=R:\KSP_1.2.2_dev
echo %H%


set d=%H%
if exist %d% goto one
mkdir %d%
:one
set d=%H%\Gamedata
if exist %d% goto two
mkdir %d%
:two
set d=%H%\Gamedata\FTLDriveContinued
if exist %d% goto three
mkdir %d%
:three
set d=%H%\Gamedata\FTLDriveContinued\Plugins
if exist %d% goto four
mkdir %d%
:four
set d=%H%\Gamedata\FTLDriveContinued\Parts
if exist %d% goto five
mkdir %d%
:five
set d=%H%\Gamedata\FTLDriveContinued\Sounds
if exist %d% goto six
mkdir %d%
:six

copy bin\Debug\FTLDriveContinued.dll ..\GameData\FTLDriveContinued\Plugins
xcopy /Y /s ..\GameData\FTLDriveContinued %H%\GameData\FTLDriveContinued
