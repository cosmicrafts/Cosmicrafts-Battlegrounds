#!/bin/bash

# Create 'optimized' folder if it doesn't exist
mkdir -p optimized

# Loop through all PNG files in the current folder
for img in *.png; do
    if [ -f "$img" ]; then
        # Get filename without extension
        filename=$(basename "$img")
        
        # Get original width (assumes square images)
        width=$(identify -format "%w" "$img")

        # Calculate new width (half the size)
        new_width=$((width / 2))

        # Resize and compress
        convert "$img" -resize ${new_width}x${new_width} "optimized/$filename"
        pngquant --quality=65-80 --speed 1 --force --output "optimized/$filename" "optimized/$filename"

        echo "Optimized: $img -> optimized/$filename (from ${width}px to ${new_width}px)"
    fi
done

echo "✅ Optimization complete! Check the 'optimized' folder."
