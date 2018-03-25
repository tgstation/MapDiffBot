#!/bin/bash

cd ../SpacemanDMM
cargo build -p cli --release
cd ../MapDiffBot
if [ ! -f ../SpacemanDMM/target/release/dmm-tools.exe ]; then
    mv ../SpacemanDMM/target/release/dmm-tools ../SpacemanDMM/target/release/dmm-tools.exe
fi
cp ../SpacemanDMM/target/release/dmm-tools.exe "${1//\\//}"
