#!/bin/bash

# Bash script to make all .sh files in _scripts directory executable
# Run this script with: ./make-scripts-executable.sh
# Make sure this script is executable first with: chmod +x make-scripts-executable.sh

echo "Making all .sh files in _scripts directory executable..."

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "Script directory: $SCRIPT_DIR"

# Find all .sh files recursively in the _scripts directory
sh_files=$(find "$SCRIPT_DIR" -name "*.sh" -type f)
total_count=$(echo "$sh_files" | wc -l)

echo "Found $total_count .sh files"

# Make all .sh files executable
find "$SCRIPT_DIR" -name "*.sh" -type f -exec chmod +x {} \;

echo ""
echo "List of .sh files found:"
while IFS= read -r file; do
    relative_path="${file#$SCRIPT_DIR}"
    relative_path="${relative_path#/}"
    echo "  $relative_path"
done <<< "$sh_files"

echo ""
echo "Total .sh files processed: $total_count"
echo "All .sh files in _scripts directory are now executable!"