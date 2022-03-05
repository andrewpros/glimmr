#!/bin/bash
function vercomp () {
    if [[ "$1" == "$2" ]]
    then
        echo "0"
        return 0
    fi
    local IFS=.
    local i ver1=("$1") ver2=("$2")
    # fill empty fields in ver1 with zeros
    for ((i=${#ver1[@]}; i<${#ver2[@]}; i++))
    do
        ver1[i]=0
    done
    for ((i=0; i<${#ver1[@]}; i++))
    do
        if [[ -z ${ver2[i]} ]]
        then
            # fill empty fields in ver2 with zeros
            ver2[i]=0
        fi
        if ((10#${ver1[i]} > 10#${ver2[i]}))
        then
            echo "1"
            return 1
        fi
        if ((10#${ver1[i]} < 10#${ver2[i]}))
        then
            echo "2"
            return 2
        fi
    done
    echo "0"
    return 0
}

arch="$(arch)"
PUBPROFILE="Linux"
PUBPATH="linux"

if [ -f "/usr/bin/raspi-config" ] && [ "$arch" == "armv71" ] 
  then
    PUBPROFILE="LinuxARM"
    PUBPATH="linux-arm"
fi

if [ -f "/usr/bin/raspi-config" ] && [ "$arch" == "aarch64" ] 
  then
    PUBPROFILE="LinuxARM64"
    PUBPATH="linux-arm64"
fi

# shellcheck disable=SC2012
log=$(ls -t /var/log/glimmr/glimmr* | head -1)
if [ "$log" == "" ]
  then
    log=/var/log/glimmr/glimmr.log
fi

if [ ! -f $log ]
  then
    log=/var/log/glimmr/glimmr.log
    touch $log
    chmod 777 $log
fi

echo "Checking for Glimmr updates for $PUBPROFILE." >> $log

if [ ! -d "/usr/share/Glimmr" ]
  then
# Make dir
  mkdir /usr/share/Glimmr  
fi

# Download and extract latest release
ver=$(wget "https://api.github.com/repos/d8ahazard/glimmr/releases/latest" -q -O - | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')
echo "Repo version is $ver." >> $log
if [ -f "/etc/Glimmr/version" ]
  then
    curr=$(head -n 1 /etc/Glimmr/version)
    echo "Current version is $curr." >> $log
    diff=$(vercomp "$curr" "$ver")
    if [ "$diff" != "2" ]
      then
        echo "Nothing to update." >> $log
        exit 0
    fi
fi

cd /tmp || exit
echo "Updating glimmr to version $ver." >> $log
url="https://github.com/d8ahazard/glimmr/releases/download/$ver/Glimmr-$PUBPATH-$ver.tgz"
echo "Grabbing archive from $url" >> $log
wget -O archive.tgz "$url"
#Stop service
echo "Stopping glimmr services..." >> $log
service glimmr stop
echo "Services stopped." >> $log
echo "Extracting archive..." >> $log
tar zxvf ./archive.tgz -C /usr/share/Glimmr/
echo "Setting permissions..." >> $log
chmod -R 777 /usr/share/Glimmr/
echo "Cleanup..." >> $log
rm ./archive.tgz
echo "Update completed." >> $log
echo "$ver" > /etc/Glimmr/version
echo "Restarting glimmr service..." >> $log

# Restart Service
service glimmr start