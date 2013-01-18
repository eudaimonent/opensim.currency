#!/bin/bash

CONFIGPATH=./config
OPNSIMPATH=../bin

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

yes | mono ../bin/Prebuild.exe /clean
./runprebuild_nant.sh
nant

cp bin/OpenSim.Data.MySQL.MySQLMoneyDataWrapper.dll ../$OPNSIMPATH
cp bin/Aurora.Modules.Currency.dll ../$OPNSIMPATH
cp bin/MoneyServer.exe ../$OPNSIMPATH


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


