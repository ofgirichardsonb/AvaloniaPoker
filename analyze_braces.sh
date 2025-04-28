#!/bin/bash
# Simple script to scan a C# file for brace structure issues

FILE=$1
echo "Analyzing $FILE for brace structure issues..."

# Count opening and closing braces
OPEN_BRACES=$(grep -o "{" $FILE | wc -l)
CLOSE_BRACES=$(grep -o "}" $FILE | wc -l)

echo "Opening braces: $OPEN_BRACES"
echo "Closing braces: $CLOSE_BRACES"

if [ $OPEN_BRACES -ne $CLOSE_BRACES ]; then
    echo "ISSUE DETECTED: Mismatched braces. Difference: $(($OPEN_BRACES - $CLOSE_BRACES))"
else
    echo "Brace count matches. No issues detected."
fi