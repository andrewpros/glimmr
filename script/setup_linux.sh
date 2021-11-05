#!/bin/bash
PUBPROFILE="Linux";
PUBFILE="linux"
if [ -f "/usr/bin/raspi-config" ] 
  then
    PUBPROFILE="LinuxARM"
    PUBFILE="linux-arm"
fi

unameOut="$(uname -s)"
if [ "$unameOut" == "Darwin" ]
  then
    echo "Please use the setup_osx.sh script for OSX installation."
    exit 0
fi

if [ "$unameOut" == "FreeBSD" ]
  then
    PUBPROFILE="Portable"
fi

echo "Performing cleanup..."

serviceName="glimmr"
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Stopping and removing old glimmr service definitions..."
    service glimmr stop
    systemctl disable glimmr.service
    rm -rf /etc/systemd/system/glimmr.service
    echo "STOPPED!"
else
    echo "$serviceName is not installed."
fi


if [ -d "/opt/glimmr" ]
  then
    echo "Removing /opt/glimmr folder."
    rm -r "/opt/glimmr"
fi

if [ -d "/home/glimmrtv/glimmr" ]
  then
    echo "Removing /home/glimmrtv/glimmr folder."
    rm -r "/home/glimmrtv/glimmr"
fi

if [ -d "/home/glimmrtv/ws281x" ]
  then
    echo "Removing /home/glimmrtv/ws281x folder."
    rm -r "/home/glimmrtv/ws281x"
fi

if [ -d "/opt/dotnet" ]
  then
    echo "Removing /opt/dotnet folder."
    rm -r "/opt/dotnet"
fi

echo "Beginning Glimmr setup/update for $PUBPROFILE"

echo "Checking dependencies..."
if [ "$PUBPROFILE" == "LinuxARM" ] 
then
  echo "Installing Linux-ARM dependencies..."
  # Install dependencies
  sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libjasper-dev libqtgui4 libqt4-test libglu1-mesa libdc1394-22 libtesseract-dev icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev gfortran libopengl-dev git gcc xauth avahi-daemon x11-xserver-utils libtiff5-dev libgeotiff-dev libgtk-3-dev libgstreamer1.0-dev libavcodec-dev libswscale-dev libavformat-dev libopenexr-dev libjasper-dev libdc1394-22-dev libv4l-dev libeigen3-dev libopengl-dev cmake-curses-gui freeglut3-dev
  echo "gpio=19=op,a5" >> /boot/config.txt    
  echo "Raspi first-config is done!"
fi

if [ "$PUBPROFILE" == "Linux" ] 
then
    echo "Installing Linux (x64) dependencies..."
    sudo apt-get -y install libgtk-3-dev libhdf5-dev libatlas-base-dev libglu1-mesa libdc1394-22 libtesseract-dev icu-devtools libjpeg-dev libpng-dev libtiff-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev libxvidcore-dev libatlas-base-dev gfortran libopengl-dev git gcc xauth avahi-daemon x11-xserver-utils libopencv-dev python3-opencv unzip libtiff5-dev libgeotiff-dev libgtk-3-dev libgstreamer1.0-dev libavcodec-dev libswscale-dev libavformat-dev libopenexr-dev libdc1394-22-dev libv4l-dev libeigen3-dev libopengl-dev cmake-curses-gui freeglut3-dev lm-sensors
    sudo apt-get -y install libopencv-dev python3-opencv lm-sensors
    echo "DONE!"
fi

if [ ! -d "/usr/share/Glimmr" ]
  then
# Make dir
  mkdir /usr/share/Glimmr  
fi

# Check for service stop
serviceName="glimmr"
if systemctl --all --type service | grep -q "$serviceName";then
  echo "Stopping glimmr..."
    service glimmr stop
    echo "STOPPED!"
else
    echo "$serviceName is not installed."
fi

# Download and extract latest release
cd /tmp || exit
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo ver is $ver
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-$PUBFILE-$ver.tgz"
echo Grabbing archive from $url
wget -O archive.tgz $url
tar zxvf ./archive.tgz -C /usr/share/Glimmr/
chmod -R 777 /usr/share/Glimmr
rm ./archive.tgz
echo "DONE." >> $log

echo "Re-installing $serviceName."
echo "
[Unit]
Description=GlimmrTV

[Service]
Type=simple
RemainAfterExit=yes
StandardOutput=tty
Restart=always
User=root
WorkingDirectory=/usr/share/Glimmr
ExecStart=sudo Glimmr
KillMode=process

[Install]
WantedBy=multi-user.target

" > /etc/systemd/system/glimmr.service
systemctl daemon-reload
systemctl enable glimmr.service
systemctl start glimmr.service

read -n 1 -r -s -p $'Install complete, press enter to continue.\n'