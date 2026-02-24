"""
Diagnostic: check which cities fall on the wrong color territory.
Sample the reference image color at each city position and flag mismatches.
"""
import re, cv2, numpy as np

ref = cv2.imread(r'C:\Users\loicr\.gemini\antigravity\brain\e3b2b779-3de8-42d1-aea7-995e32ff8ec9\media__1771883724314.jpg')
H, W = ref.shape[:2]

with open(r'e:\FPSLowPoly\Assets\Scripts\Campaign\CampaignManager.cs', 'r', encoding='utf-8') as f:
    text = f.read()

pattern = r'AddProvince\(\s*"(\w+)"\s*,\s*"([^"]+)"\s*,\s*FactionType\.(\w+)\s*,\s*new\s+Vector2\((\d+\.\d+)f\s*,\s*(\d+\.\d+)f\)'
matches = re.findall(pattern, text)

# Define reference color ranges for major factions (from the image, in BGR)
# France = bright red (B:0-80, G:0-80, R:160-255)
# Britain = orange (B:0-80, G:100-180, R:200-255) 
# Prussia = dark (low everything)
# Russia = brown/dark (B:0-80, G:20-80, R:80-150)
# Austria = yellow (B:0-80, G:180-255, R:200-255)
# Ottoman = magenta/pink (B:120-220, G:0-80, R:150-255)
# Poland = purple (B:120-200, G:0-100, R:120-200)
# Sweden = cyan (B:200-255, G:200-255, R:0-80)
# Water = dark blue (B:80-160, G:30-80, R:10-60)

def classify_pixel(bgr):
    b, g, r = int(bgr[0]), int(bgr[1]), int(bgr[2])
    # Water
    if b > 80 and r < 70 and g < 80:
        return "WATER"
    # France red
    if r > 160 and g < 80 and b < 80:
        return "France"
    # Britain orange  
    if r > 180 and g > 80 and g < 180 and b < 80:
        return "Britain"
    # Austria yellow
    if r > 180 and g > 180 and b < 100:
        return "Austria"
    # Sweden cyan
    if b > 180 and g > 180 and r < 100:
        return "Sweden"
    # Ottoman magenta/pink
    if r > 140 and b > 100 and g < 80:
        return "Ottoman"
    # Poland purple
    if r > 100 and b > 100 and g < 100 and r < 220:
        return "Poland"
    # Green (Portugal/Italy)
    if g > 130 and r < 130 and b < 130:
        return "Green_area"
    # Dark/brown (Russia/Prussia)
    if r < 120 and g < 80 and b < 80:
        return "Dark_area"
    return f"Unknown({r},{g},{b})"

# Check each city
print(f"{'Province':<20} {'City':<18} {'Faction':<12} {'Pos(x,y)':<14} {'Pixel(x,y)':<14} {'LandsOn':<20} {'OK?'}")
print("-" * 120)

wrong = []
for prov_id, city, faction, x, y in matches:
    nx, ny = float(x), float(y)
    px = int(nx * W)
    py = H - 1 - int(ny * H)
    
    if px < 0 or px >= W or py < 0 or py >= H:
        continue
    
    # Sample 5x5 area around the city
    y1, y2 = max(0, py-2), min(H, py+3)
    x1, x2 = max(0, px-2), min(W, px+3)
    avg_bgr = ref[y1:y2, x1:x2].mean(axis=(0,1))
    
    lands_on = classify_pixel(avg_bgr)
    
    # Check if it matches
    ok = "?"
    if faction == "France" and lands_on == "France":
        ok = "OK"
    elif faction == "Britain" and lands_on == "Britain":
        ok = "OK"
    elif faction == "France" and lands_on == "WATER":
        ok = "WATER!"
    elif faction == "France" and lands_on != "France":
        ok = "WRONG!"
    elif lands_on == "WATER":
        ok = "WATER!"
    else:
        ok = "~"
    
    if ok in ("WRONG!", "WATER!") or (faction == "France"):
        print(f"{prov_id:<20} {city:<18} {faction:<12} ({nx:.2f},{ny:.2f})   ({px:>4},{py:>4})     {lands_on:<20} {ok}")
        if ok in ("WRONG!", "WATER!"):
            wrong.append((prov_id, city, faction, nx, ny, px, py, lands_on))

print(f"\n--- {len(wrong)} misplaced cities ---")
for w in wrong:
    print(f"  {w[0]}: {w[1]} ({w[2]}) at ({w[3]:.2f},{w[4]:.2f}) -> lands on {w[7]}")
