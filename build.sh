#!/bin/bash

(cd OpenSim.Data.MySQL.MySQLMoneyDataWrapper/ && nant && cp *.dll ../../bin)
(cd OpenSim.Forge.Currency/ && nant && cp *.dll ../../bin)
(cd OpenSim.Grid.MoneyServer/ && nant && cp *.exe ../../bin)
cp -f config/MoneyServer.ini config/OpenSim.Grid.MoneyServer.exe.config config/SineWaveCert.pfx  ../bin
