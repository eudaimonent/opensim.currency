#!/bin/bash

CONFIGPATH=./config
OPNSIMPATH=../bin

echo "=============================================="
echo "Rebuild OpenSim for Secure DTL/NSL_CURRENCY"
echo "=============================================="


if [ ! -f ../bin/HttpServer_OpenSim.dll.orig ]; then
	mv ../bin/HttpServer_OpenSim.dll ../bin/HttpServer_OpenSim.dll.orig
	cp HttpServer_OpenSim/bin/HttpServer_OpenSim.dll ../bin
fi

cd ..
patch -p1 < ./opensim.currency.secure/patch/opensim.patch 
./build.sh || exit 1

