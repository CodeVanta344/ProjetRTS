import re, cv2, numpy as np

ref = cv2.imread(r'C:\Users\loicr\.gemini\antigravity\brain\e3b2b779-3de8-42d1-aea7-995e32ff8ec9\media__1771883724314.jpg')
H, W = ref.shape[:2]

hsv = cv2.cvtColor(ref, cv2.COLOR_BGR2HSV)
water = ((hsv[:,:,0] > 85) & (hsv[:,:,0] < 140) & (hsv[:,:,1] > 30) & (hsv[:,:,2] < 160)).astype(np.uint8) * 255
k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5,5))
water = cv2.morphologyEx(water, cv2.MORPH_CLOSE, k, iterations=2)
water = cv2.morphologyEx(water, cv2.MORPH_OPEN, k, iterations=1)

with open(r'e:\FPSLowPoly\Assets\Scripts\Campaign\CampaignManager.cs', 'r', encoding='utf-8') as f:
    text = f.read()

pattern = r'AddProvince\(\s*"(\w+)"\s*,\s*"([^"]+)"\s*,\s*FactionType\.(\w+)\s*,\s*new\s+Vector2\((\d+\.\d+)f\s*,\s*(\d+\.\d+)f\)'
matches = re.findall(pattern, text)

Y_SCALE, Y_OFFSET = 1.163, -0.154

print(f"Total cities: {len(matches)}")
print(f"{'Province':<22} {'City':<18} {'Faction':<12} {'Pos':<14} {'Status'}")
print("-"*80)

water_cities = []
for prov_id, city, faction, x, y in matches:
    nx, ny = float(x), float(y)
    img_ny = ny * Y_SCALE + Y_OFFSET
    px = int(nx * W)
    py = H - 1 - int(img_ny * H)
    if px < 0 or px >= W or py < 0 or py >= H:
        water_cities.append((prov_id, city, nx, ny))
        print(f"{prov_id:<22} {city:<18} {faction:<12} ({nx:.2f},{ny:.2f})   OUT_OF_BOUNDS")
        continue
    y1, y2 = max(0, py-2), min(H, py+3)
    x1, x2 = max(0, px-2), min(W, px+3)
    water_pct = water[y1:y2, x1:x2].mean() / 255.0
    if water_pct > 0.3:
        water_cities.append((prov_id, city, nx, ny))
        print(f"{prov_id:<22} {city:<18} {faction:<12} ({nx:.2f},{ny:.2f})   WATER {water_pct:.0%}")

print(f"\n--- {len(water_cities)} cities in water ---")
