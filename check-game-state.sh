#!/bin/bash

echo "Checking current poker game state..."
echo "==============================="
echo "Running processes:"
ps aux | grep -i "dotnet run" | grep -v grep
echo ""

echo "Checking for service discovery messages:"
grep -E "ServiceDiscovery|registered" /home/runner/workspace/poker_verbose.log | tail -10 || echo "No recent service discovery messages found"
echo ""

echo "Checking for game-related messages:"
grep -E "StartGame|StartHand|DeckShuffled|GameStarted" /home/runner/workspace/poker_verbose.log | tail -10 || echo "No recent game-related messages found"
echo ""

echo "Checking for broker activity:"
grep -E "\[CentralMessageBroker\]" /home/runner/workspace/poker_verbose.log | grep -v "Heartbeat" | tail -10 || echo "No recent broker activity found"
echo ""

echo "Checking for any errors or warnings:"
grep -E "ERROR|WARNING|exception|failed" /home/runner/workspace/poker_verbose.log | tail -10 || echo "No recent errors or warnings found"