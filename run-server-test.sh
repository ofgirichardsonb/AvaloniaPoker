#!/bin/bash

# This script corrects the server-test workflow command
# since the original workflow uses an invalid command (start-console)

cd /home/runner/workspace
./rebuild.sh
./poker-services.sh start-client --port-offset 0 --verbose