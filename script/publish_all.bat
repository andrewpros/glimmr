@echo off
IF [%1] == [] GOTO Error
set version=%~1

echo Building Glimmr Tray for Windows...
dotnet publish -r win-x64 -c release ..\src\GlimmrTray\GlimmrTray.csproj -o ..\src\Glimmr\bin\Windows --self-contained

echo Build packages for various architectures
for %%x in (
	Linux
	LinuxARM
	LinuxARM64
	Windows
	WindowsARM
	OSX
) do (
	echo Building %%x
	dotnet publish -c Release ..\src\Glimmr\Glimmr.csproj /p:PublishProfile=%%x -o ..\src\Glimmr\bin\%%x --self-contained=true
)


cd ..\src\Glimmr\

cd .\bin\
del /Q /S .\*.zip
del /Q /S .\*.tgz
del /Q /S .\*.zip
del /Q /S .\*.rpm
del /Q /S .\*.deb

cd ..

echo Build deb packages
dotnet deb -c Release -o .\bin -r linux-arm
dotnet deb -c Release -o .\bin -r linux-arm64
dotnet deb -c Release -o .\bin -r linux-x64
dotnet rpm -c Release -o .\bin -r linux-arm
dotnet rpm -c Release -o .\bin -r linux-arm64
dotnet rpm -c Release -o .\bin -r linux-x64


echo Build MSI package
SET "APP=C:\Progra~2\Inno Setup 6\iscc.exe"
"%APP%" "%~dp0..\src\Glimmr\build_app.iss"

cd .\bin\

echo Copying OSX Files...
del /S /Q ..\..\..\Glimmr-macos-installer-builder\macOS-x64\application\*
xcopy /Y /E .\OSX\* ..\..\..\Glimmr-macos-installer-builder\macOS-x64\application

echo Creating archives...

%~dp07z.exe a -ttar -so -an -r .\LinuxARM\* | %~dp07z a -si Glimmr-linux-arm-%version%.tgz
%~dp07z.exe a -ttar -so -an -r .\LinuxARM64\* | %~dp07z a -si Glimmr-linux-arm64-%version%.tgz
xcopy /-I /Y .\Glimmr-linux-arm64-%version%.tgz ..\..\..\Glimmr-image-gen-x64\stage2\05-glimmr\files\archive.tgz
xcopy /-I /Y .\Glimmr-linux-arm-%version%.tgz ..\..\..\Glimmr-image-gen\stage2\05-glimmr\files\archive.tgz
%~dp07z.exe a -ttar -so -an -r .\Linux\* | %~dp07z a -si Glimmr-linux-%version%.tgz
%~dp07z.exe a -ttar -so -an -r .\OSX\* | %~dp07z a -si Glimmr-osx-%version%.tgz
%~dp07z.exe a -tzip -r Glimmr-windows-%version%.zip .\Windows\*
%~dp07z.exe a -tzip -r Glimmr-windows-arm-%version%.zip .\WindowsARM\*
GOTO End

:Error
echo Please enter a version number.

:End