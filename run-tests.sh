#!/bin/bash

# Set default test project if none specified
TEST_PROJECT=${1:-"all"}
COVERAGE_DIR="./coverage-results"
REPORT_DIR="./coverage-report"

# Create directories if they don't exist
mkdir -p $COVERAGE_DIR
mkdir -p $REPORT_DIR

# Helper function to run tests with coverage for a specific project
run_tests_with_coverage() {
    local project=$1
    echo "Running tests for $project with coverage..."
    dotnet test $project --no-build --verbosity normal --collect:"XPlat Code Coverage" --results-directory $COVERAGE_DIR
    
    # Check if tests ran successfully
    if [ $? -eq 0 ]; then
        echo "Tests for $project completed successfully."
    else
        echo "Tests for $project failed."
        return 1
    fi
}

# First build all projects
echo "Building projects..."
dotnet build

# Run tests based on input parameter
if [ "$TEST_PROJECT" == "all" ]; then
    # Run all test projects
    run_tests_with_coverage "MSA.Foundation.Tests"
    foundation_result=$?
    
    run_tests_with_coverage "PokerGame.Tests"
    poker_result=$?
    
    # Check overall test success
    if [ $foundation_result -eq 0 ] && [ $poker_result -eq 0 ]; then
        echo "All tests passed."
    else
        echo "Some tests failed."
        exit 1
    fi
else
    # Run specific test project
    run_tests_with_coverage $TEST_PROJECT
    
    if [ $? -ne 0 ]; then
        echo "Tests failed."
        exit 1
    fi
fi

echo "Test coverage files have been generated in $COVERAGE_DIR"
echo "To view detailed HTML reports, install the ReportGenerator tool:"
echo "  dotnet tool install -g dotnet-reportgenerator-globaltool"
echo "Then run:"
echo "  reportgenerator -reports:$COVERAGE_DIR/**/*.xml -targetdir:$REPORT_DIR -reporttypes:Html"