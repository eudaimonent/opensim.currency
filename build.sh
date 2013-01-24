#!/bin/bash

CONFIGPATH=./config
AURORA_BIN=../bin

echo "=========================="
echo "DTL/NSL_CURRENCY"
echo "=========================="

rm -f  bin/*
rm -fr OpenSim.Data.MySQL.MySQLMoneyDataWrapper/bin
rm -fr OpenSim.Data.MySQL.MySQLMoneyDataWrapper/obj
rm -fr Aurora.Modules.Currency/bin
rm -fr Aurora.Modules.Currency/obj
rm -fr Aurora.Server.MoneyServer/bin
rm -fr Aurora.Server.MoneyServer/obj

yes | mono $AURORA_BIN/Prebuild.exe /clean
./runprebuild.sh
xbuild

cp bin/OpenSim.Data.MySQL.MySQLMoneyDataWrapper.dll $AURORA_BIN
cp bin/Aurora.Modules.Currency.dll $AURORA_BIN
cp bin/MoneyServer.exe $AURORA_BIN


if [ ! -f $AURORA_BIN/MoneyServer.ini ]; then
	cp $CONFIGPATH/MoneyServer.ini $AURORA_BIN
else
	cp $CONFIGPATH/MoneyServer.ini $AURORA_BIN/MoneyServer.ini.example
fi

if [ ! -f $AURORA_BIN/MoneyServer.exe.config ]; then
	cp $CONFIGPATH/MoneyServer.exe.config $AURORA_BIN
fi

if [ ! -f $AURORA_BIN/SineWaveCert.pfx ]; then
	p $CONFIGPATH/SineWaveCert.pfx $AURORA_BIN
fi


