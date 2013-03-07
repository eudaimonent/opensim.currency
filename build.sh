#!/bin/bash

CONFIGPATH=./config
OPNSIMPATH=../bin

echo "=========================="
echo "DTL/NSL_CURRENCY"
echo "=========================="

rm -f bin/*
yes | mono ../bin/Prebuild.exe /clean

./runprebuild.sh
xbuild

echo
cp bin/OpenSim.Data.MySQL.MySQLMoneyDataWrapper.dll $OPNSIMPATH
cp bin/OpenSim.Modules.Currency.dll $OPNSIMPATH
cp bin/MoneyServer.exe $OPNSIMPATH


rm -f $OPNSIMPATH/OpenSim.Forge.Currency.dll

if [ ! -f $OPNSIMPATH/MoneyServer.ini ]; then
	cp $CONFIGPATH/MoneyServer.ini $OPNSIMPATH
else
	cp $CONFIGPATH/MoneyServer.ini $OPNSIMPATH/MoneyServer.ini.example
fi

if [ ! -f $OPNSIMPATH/MoneyServer.exe.config ]; then
	cp $CONFIGPATH/MoneyServer.exe.config $OPNSIMPATH
fi

if [ ! -f $OPNSIMPATH/SineWaveCert.pfx ]; then
	cp $CONFIGPATH/SineWaveCert.pfx $OPNSIMPATH
fi
