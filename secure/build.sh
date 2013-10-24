#!/bin/bash

CONFIGPATH=./config
OPNSIMPATH=../bin

echo "=========================="
echo "DTL/NSL_CURRENCY"
echo "=========================="

rm -f bin/*
yes | mono ../bin/Prebuild.exe /clean

./runprebuild.sh
xbuild || exit 1

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

# Sample Server Cert file 1
if [ ! -f $OPNSIMPATH/SineWaveCert.pfx ]; then
	cp $CONFIGPATH/SineWaveCert.pfx $OPNSIMPATH
fi

# Sample Server Cert file 2 (JOGRID.NET)
if [ ! -f $OPNSIMPATH/server_cert.p12 ]; then
	cp $CONFIGPATH/server_cert.p12 $OPNSIMPATH
fi

# CA Cert file of JOGRID,NET
if [ ! -f $OPNSIMPATH/cacert.crt ]; then
	cp $CONFIGPATH/cacert.crt $OPNSIMPATH
fi


