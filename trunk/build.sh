#!/bin/bash

CONFIGPATH=./config
OPNSIMPATH=./bin

(cd OpenSim.Data.MySQL.MySQLMoneyDataWrapper/ && nant && cp *.dll ../$OPNSIMPATH)
(cd OpenSim.Forge.Currency/ && nant && cp *.dll ../$OPNSIMPATH)
(cd OpenSim.Grid.MoneyServer/ && nant && cp *.exe ../$OPNSIMPATH)


if [ ! -f $CONFIGPATH/MoneyServer.ini ]; then
	cp $CONFIGPATH/MoneyServer.ini $OPNSIMPATH
fi

if [ ! -f $CONFIGPATH/OpenSim.Grid.MoneyServer.exe.config ]; then
	cp $CONFIGPATH/OpenSim.Grid.MoneyServer.exe.config $OPNSIMPATH
fi

if [ ! -f $CONFIGPATH/SineWaveCert.pfx ]; then
	cp $CONFIGPATH/SineWaveCert.pfx $OPNSIMPATH
fi
