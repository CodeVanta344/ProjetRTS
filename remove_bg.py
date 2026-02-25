import sys
from rembg import remove
from PIL import Image

input_path = "C:/Users/loicr/Desktop/PROJECT RTS NAPOLEONIC/ProjetRTS/Assets/Resources/UI/FormationMenuBg.png"
output_path = "C:/Users/loicr/Desktop/PROJECT RTS NAPOLEONIC/ProjetRTS/Assets/Resources/UI/FormationMenuBg.png"

try:
    print(f"Loading image from {input_path}...")
    with open(input_path, 'rb') as i:
        input_data = i.read()
    
    print("Removing background using rembg with alpha matting for PERFECT edges...")
    output_data = remove(
        input_data,
        alpha_matting=True,
        alpha_matting_foreground_threshold=240,
        alpha_matting_background_threshold=10,
        alpha_matting_erode_size=5
    )
    
    print(f"Saving output to {output_path}...")
    with open(output_path, 'wb') as o:
        o.write(output_data)
    
    print("Background removed successfully with perfect edges!")
except Exception as e:
    print(f"Error: {e}")
