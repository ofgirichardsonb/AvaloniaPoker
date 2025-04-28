#!/bin/bash

# This script runs the poker console client with Release configuration
# to avoid the dependency issues in Debug configuration

cd /home/runner/workspace
./poker-services.sh start-client --port-offset 0 --verbose