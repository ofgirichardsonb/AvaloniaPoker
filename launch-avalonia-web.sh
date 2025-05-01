#!/bin/bash

cd /home/runner/workspace

echo "Starting web server for placeholder UI..."
cd /home/runner/workspace/PokerGame.Avalonia/wwwroot
cp placeholder.html index.html
python3 -m http.server 5000