#!/bin/bash

echo "Compiling basic_starthand_test.cs..."
cd /home/runner/workspace
dotnet build PokerGame.Core/PokerGame.Core.csproj -c Debug
dotnet csc /reference:/home/runner/workspace/MSA.Foundation/bin/Debug/net8.0/MSA.Foundation.dll \
            /reference:/home/runner/workspace/PokerGame.Core/bin/Debug/net8.0/PokerGame.Core.dll \
            /reference:System.dll \
            /reference:System.Core.dll \
            /reference:System.Net.Http.dll \
            /reference:System.Runtime.dll \
            /reference:System.Threading.Tasks.dll \
            /out:/home/runner/workspace/basic_starthand_test.exe \
            /home/runner/workspace/basic_starthand_test.cs

echo "Running test..."
cd /home/runner/workspace
dotnet /home/runner/workspace/basic_starthand_test.exe