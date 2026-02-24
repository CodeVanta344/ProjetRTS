"""
Comprehensive water fix: reads all province lines, checks against reference image,
and rewrites coordinates using province ID matching (not float string matching).
"""
import re, cv2, numpy as np

ref = cv2.imread(r'C:\Users\loicr\.gemini\antigravity\brain\e3b2b779-3de8-42d1-aea7-995e32ff8ec9\media__1771883724314.jpg')
H, W = ref.shape[:2]

hsv = cv2.cvtColor(ref, cv2.COLOR_BGR2HSV)
water = ((hsv[:,:,0] > 85) & (hsv[:,:,0] < 140) & (hsv[:,:,1] > 30) & (hsv[:,:,2] < 160)).astype(np.uint8) * 255
k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5,5))
water = cv2.morphologyEx(water, cv2.MORPH_CLOSE, k, iterations=2)
water = cv2.morphologyEx(water, cv2.MORPH_OPEN, k, iterations=1)

Y_SCALE, Y_OFFSET = 1.163, -0.154

def is_water_px(px, py):
    if px < 0 or px >= W or py < 0 or py >= H:
        return True
    y1, y2 = max(0, py-2), min(H, py+3)
    x1, x2 = max(0, px-2), min(W, px+3)
    return water[y1:y2, x1:x2].mean() / 255.0 > 0.3

def game_to_img(nx, ny):
    img_ny = ny * Y_SCALE + Y_OFFSET
    px = int(nx * W)
    py = H - 1 - int(img_ny * H)
    return px, py

def img_to_game(px, py):
    nx = px / W
    img_ny = (H - 1 - py) / H
    ny = (img_ny - Y_OFFSET) / Y_SCALE
    return round(nx, 2), round(ny, 2)

def snap_to_land(px, py):
    for r in range(1, 200):
        for ai in range(max(16, r*6)):
            angle = ai * 2 * np.pi / max(16, r*6)
            tx = int(px + np.cos(angle) * r)
            ty = int(py + np.sin(angle) * r)
            if 0 <= tx < W and 0 <= ty < H and not is_water_px(tx, ty):
                return tx, ty
    return px, py

# Read file
cs_path = r'e:\FPSLowPoly\Assets\Scripts\Campaign\CampaignManager.cs'
with open(cs_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Find all AddProvince lines with their EXACT text
pattern = r'(AddProvince\(\s*"(\w+)"\s*,\s*"[^"]+"\s*,\s*FactionType\.\w+\s*,\s*new\s+Vector2\()(\d+\.?\d*)f\s*,\s*(\d+\.?\d*)f(\))'
fixes = 0
def fix_match(m):
    global fixes
    prefix = m.group(1)
    prov_id = m.group(2)
    old_x = float(m.group(3))
    old_y = float(m.group(4))
    suffix = m.group(5)
    
    px, py = game_to_img(old_x, old_y)
    if is_water_px(px, py):
        new_px, new_py = snap_to_land(px, py)
        new_x, new_y = img_to_game(new_px, new_py)
        fixes += 1
        print(f"  FIX {prov_id:22s}: ({old_x:.2f},{old_y:.2f}) -> ({new_x:.2f},{new_y:.2f})")
        return f"{prefix}{new_x:.2f}f, {new_y:.2f}f{suffix}"
    return m.group(0)

new_content = re.sub(pattern, fix_match, content)

with open(cs_path, 'w', encoding='utf-8') as f:
    f.write(new_content)

print(f"\nFixed {fixes} cities")
