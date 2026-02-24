"""
Generate zoomed-in regional previews to verify city placement.
Produces: British Isles, France/Iberia, Central Europe, Italy, Balkans/Ottoman, Scandinavia, Russia
Each saved as a separate image for close inspection.
"""
import re, cv2, numpy as np

ref = cv2.imread(r'C:\Users\loicr\.gemini\antigravity\brain\e3b2b779-3de8-42d1-aea7-995e32ff8ec9\media__1771883724314.jpg')
H, W = ref.shape[:2]

with open(r'e:\FPSLowPoly\Assets\Scripts\Campaign\CampaignManager.cs', 'r', encoding='utf-8') as f:
    text = f.read()

pattern = r'AddProvince\(\s*"(\w+)"\s*,\s*"([^"]+)"\s*,\s*FactionType\.(\w+)\s*,\s*new\s+Vector2\((\d+\.?\d*)f\s*,\s*(\d+\.?\d*)f\)'
matches = re.findall(pattern, text)

Y_SCALE, Y_OFFSET = 1.163, -0.154
capitals = {"paris", "london", "lower_austria", "brandenburg", "moscow", "castile", "thrace"}

FC = {
    'France': (0,0,220), 'Britain': (0,140,230), 'Prussia': (100,80,50),
    'Russia': (38,75,128), 'Austria': (0,217,242), 'Spain': (25,100,217),
    'Ottoman': (165,38,217), 'Portugal': (38,153,38), 'Sweden': (230,230,25),
    'Denmark': (128,50,217), 'Poland': (178,50,153), 'Venice': (204,153,75),
    'Dutch': (50,153,204), 'Bavaria': (217,128,100), 'Saxony': (75,178,128),
    'PapalStates': (65,190,230), 'Savoy': (25,75,140), 'Switzerland': (38,38,178),
    'Genoa': (100,178,50), 'Tuscany': (50,204,140), 'Hanover': (90,140,165),
    'Modena': (75,128,204), 'Parma': (140,100,178), 'Lorraine': (90,115,140),
}

# Pre-compute city pixel positions
cities = []
for prov_id, city, faction, x, y in matches:
    nx, ny = float(x), float(y)
    img_ny = ny * Y_SCALE + Y_OFFSET
    px = int(nx * W)
    py = H - 1 - int(img_ny * H)
    cities.append((prov_id, city, faction, px, py, prov_id in capitals))

def draw_region(name, x1, y1, x2, y2, scale=2.5):
    """Extract region and draw cities within it"""
    # Clamp
    x1, y1 = max(0,x1), max(0,y1)
    x2, y2 = min(W,x2), min(H,y2)
    
    crop = ref[y1:y2, x1:x2].copy()
    ch, cw = crop.shape[:2]
    
    # Upscale for readability
    crop = cv2.resize(crop, (int(cw*scale), int(ch*scale)), interpolation=cv2.INTER_LANCZOS4)
    sh, sw = crop.shape[:2]
    
    font = cv2.FONT_HERSHEY_SIMPLEX
    count = 0
    for prov_id, city_name, faction, px, py, is_cap in cities:
        if x1 <= px < x2 and y1 <= py < y2:
            # Map to crop coords
            cx = int((px - x1) * scale)
            cy = int((py - y1) * scale)
            if 0 <= cx < sw and 0 <= cy < sh:
                col = FC.get(faction, (128,128,128))
                r = 8 if is_cap else 5
                cv2.circle(crop, (cx, cy), r+2, (0,0,0), -1)
                cv2.circle(crop, (cx, cy), r, col, -1)
                
                fs = 0.5 if is_cap else 0.38
                (tw,th),_ = cv2.getTextSize(city_name, font, fs, 1)
                tx, ty = cx - tw//2, cy - 12
                cv2.putText(crop, city_name, (tx, ty), font, fs, (0,0,0), 3, cv2.LINE_AA)
                cv2.putText(crop, city_name, (tx, ty), font, fs, (255,255,255), 1, cv2.LINE_AA)
                
                # Also show faction in small text below
                ffs = 0.3
                (ftw,fth),_ = cv2.getTextSize(f"[{faction}]", font, ffs, 1)
                cv2.putText(crop, f"[{faction}]", (cx-ftw//2, cy+15), font, ffs, (0,0,0), 2, cv2.LINE_AA)
                cv2.putText(crop, f"[{faction}]", (cx-ftw//2, cy+15), font, ffs, (200,200,200), 1, cv2.LINE_AA)
                count += 1
    
    # Title
    cv2.putText(crop, name, (10, 30), font, 0.8, (0,0,0), 3, cv2.LINE_AA)
    cv2.putText(crop, name, (10, 30), font, 0.8, (255,255,255), 2, cv2.LINE_AA)
    
    out = f'C:\\Users\\loicr\\.gemini\\antigravity\\brain\\e3b2b779-3de8-42d1-aea7-995e32ff8ec9\\region_{name.lower().replace(" ","_").replace("/","_")}.png'
    cv2.imwrite(out, crop)
    print(f"{name}: {count} cities, saved")
    return out

# Define regions (in image pixel coords)
regions = [
    ("British Isles", 30, 180, 280, 440),
    ("France + Iberia", 50, 320, 370, 700),
    ("Low Countries + Germany", 280, 220, 530, 440),
    ("Italy + Alps", 320, 370, 530, 630),
    ("Balkans + Ottoman", 440, 370, 750, 650),
    ("Scandinavia", 320, 30, 620, 250),
    ("Russia", 600, 150, 1000, 500),
    ("North Africa + Middle East", 50, 600, 950, 860),
]

for name, x1, y1, x2, y2 in regions:
    draw_region(name, x1, y1, x2, y2)

print("\nAll regions generated!")
