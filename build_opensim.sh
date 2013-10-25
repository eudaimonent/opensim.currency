#!/bin/bash

CONFIGPATH=./config
OPNSIMPATH=../bin


echo "=============================================="
echo "Rebuild OpenSim for Secure DTL/NSL_CURRENCY"
echo "=============================================="


if [ "$1" != '-R' ]; then
##
if [ ! -f ../bin/HttpServer_OpenSim.dll.orig ]; then
	mv ../bin/HttpServer_OpenSim.dll ../bin/HttpServer_OpenSim.dll.orig
	cp HttpServer_OpenSim/bin/HttpServer_OpenSim.dll ../bin
fi

cd ..
patch -p1 < ./opensim.currency/patch/opensim.patch 
./build.sh || exit 1

else
##
if [ -f ../bin/HttpServer_OpenSim.dll.orig ]; then
	rm -f ../bin/HttpServer_OpenSim.dll
	mv ../bin/HttpServer_OpenSim.dll.orig ../bin/HttpServer_OpenSim.dll
fi

cd ..
patch -p1 -R < ./opensim.currency/patch/opensim.patch 
./build.sh || exit 1

fi

