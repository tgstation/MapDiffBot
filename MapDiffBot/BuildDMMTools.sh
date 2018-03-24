#!/bin/bash

cd ../SpacemanDMM
cargo build -p cli --release
cd ../MapDiffBot
cp ../SpacemanDMM/target/release/dmm-tools.exe "${1//\\//}"
