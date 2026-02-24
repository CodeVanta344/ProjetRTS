"""
Full preview: reference image + corrected Voronoi political overlay + city labels.
Applies Y-transform (img_y = 1.163 * game_y - 0.154) and updated faction colors.
France = RED to match reference.
"""
import re, cv2, numpy as np

ref = cv2.imread(r'C:\Users\loicr\.gemini\antigravity\brain\e3b2b779-3de8-42d1-aea7-995e32ff8ec9\media__1771883724314.jpg')
H, W = ref.shape[:2]

with open(r'e:\FPSLowPoly\Assets\Scripts\Campaign\CampaignManager.cs', 'r', encoding='utf-8') as f:
    text = f.read()

pattern = r'AddProvince\(\s*"(\w+)"\s*,\s*"([^"]+)"\s*,\s*FactionType\.(\w+)\s*,\s*new\s+Vector2\((\d+\.\d+)f\s*,\s*(\d+\.\d+)f\)'
matches = re.findall(pattern, text)

# Transform: img_y = 1.163 * game_y - 0.154
Y_SCALE, Y_OFFSET = 1.163, -0.154

# Faction colors (RGB this time, matching reference image and CampaignMap3D.cs)
FC = {
    'France':      (220, 25,  25),    # Bright red
    'Britain':     (230, 140, 38),    # Orange  
    'Prussia':     (50,  50,  75),    # Dark charcoal
    'Russia':      (128, 75,  38),    # Brown
    'Austria':     (242, 217, 50),    # Bright yellow
    'Spain':       (217, 100, 25),    # Dark orange
    'Ottoman':     (217, 38,  165),   # Magenta
    'Portugal':    (38,  153, 38),    # Green
    'Sweden':      (25,  230, 230),   # Cyan
    'Denmark':     (217, 50,  128),   # Pink
    'Poland':      (153, 50,  178),   # Purple
    'Venice':      (75,  153, 204),   # Teal
    'Dutch':       (204, 153, 50),    # Olive gold
    'Bavaria':     (100, 128, 217),   # Medium blue
    'Saxony':      (128, 178, 75),    # Yellow-green
    'PapalStates': (230, 190, 65),    # Gold
    'Savoy':       (140, 75,  25),    # Dark brown
    'Switzerland': (178, 38,  38),    # Dark red
    'Genoa':       (50,  178, 100),   # Sea green
    'Tuscany':     (140, 204, 50),    # Lime green
    'Hanover':     (165, 140, 90),    # Khaki
    'Modena':      (204, 128, 75),    # Light brown
    'Parma':       (178, 100, 140),   # Mauve
    'Lorraine':    (140, 115, 90),    # Tan
}

# Convert RGB to BGR for OpenCV
FC_BGR = {k: (v[2], v[1], v[0]) for k, v in FC.items()}

# Gather data with transformed coords
positions_img = []  # (img_nx, img_ny) for Voronoi
factions_list = []
colors_list = []
city_names = []
prov_ids = []

for prov_id, city, faction, x, y in matches:
    nx, ny = float(x), float(y)
    img_ny = ny * Y_SCALE + Y_OFFSET
    positions_img.append((nx, img_ny))
    factions_list.append(faction)
    colors_list.append(FC_BGR.get(faction, (128, 128, 128)))
    city_names.append(city)
    prov_ids.append(prov_id)

pos_x = np.array([p[0] for p in positions_img], dtype=np.float32)
pos_y = np.array([p[1] for p in positions_img], dtype=np.float32)

# Compute Voronoi at reduced res
VOR = 512
yy, xx = np.mgrid[0:VOR, 0:VOR]
vnx = xx.astype(np.float32) / VOR
vny = yy.astype(np.float32) / VOR

nearest = np.zeros((VOR, VOR), dtype=np.int32)
min_dist = np.full((VOR, VOR), 1e9, dtype=np.float32)
for i in range(len(matches)):
    d = (vnx - pos_x[i])**2 + (vny - pos_y[i])**2
    mask = d < min_dist
    min_dist[mask] = d[mask]
    nearest[mask] = i

# Upscale to full res and flip Y
nearest_full = cv2.resize(nearest.astype(np.float32), (W, H), interpolation=cv2.INTER_NEAREST).astype(np.int32)
nearest_full = nearest_full[::-1]  # Flip Y

# Political overlay
colors_arr = np.array(colors_list, dtype=np.uint8)
pol_overlay = np.zeros_like(ref)
for c in range(3):
    pol_overlay[:,:,c] = colors_arr[nearest_full, c]

# Water detection from reference image
hsv = cv2.cvtColor(ref, cv2.COLOR_BGR2HSV)
water = ((hsv[:,:,0] > 85) & (hsv[:,:,0] < 140) & (hsv[:,:,1] > 30) & (hsv[:,:,2] < 160)).astype(np.uint8) * 255
k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5,5))
water = cv2.morphologyEx(water, cv2.MORPH_CLOSE, k, iterations=2)
water = cv2.morphologyEx(water, cv2.MORPH_OPEN, k, iterations=1)
land = (water == 0).astype(np.float32)

# Blend terrain + political
alpha = 0.50
result = ref.astype(np.float32)
gray = np.mean(result, axis=2, keepdims=True)
result = result * 0.7 + gray * 0.3  # slight desat
blended = result * (1 - alpha) + pol_overlay.astype(np.float32) * alpha
land3 = np.stack([land]*3, axis=-1)
result = ref.astype(np.float32) * (1 - land3) + blended * land3
result = np.clip(result, 0, 255).astype(np.uint8)

# Borders
faction_idx_map = np.array([list(FC.keys()).index(f) if f in FC else -1 for f in factions_list])
fmap = faction_idx_map[nearest_full]

# National borders
nh = fmap[:, 1:] != fmap[:, :-1]
nv = fmap[1:, :] != fmap[:-1, :]
nat = np.zeros((H,W), dtype=np.uint8)
nat[:, 1:] |= nh.astype(np.uint8)
nat[1:, :] |= nv.astype(np.uint8)
nat *= (land > 0.5).astype(np.uint8)
nat = cv2.dilate(nat, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3,3)), iterations=2)

# Province borders  
bh = nearest_full[:, 1:] != nearest_full[:, :-1]
bv = nearest_full[1:, :] != nearest_full[:-1, :]
prov = np.zeros((H,W), dtype=np.uint8)
prov[:, 1:] |= bh.astype(np.uint8)
prov[1:, :] |= bv.astype(np.uint8)
prov *= (land > 0.5).astype(np.uint8)
prov = cv2.dilate(prov, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2,2)), iterations=1)

# Apply borders
nat3 = np.stack([nat]*3, axis=-1).astype(np.float32)
result = (result.astype(np.float32) * (1 - nat3*0.7) + np.array([10,10,5], dtype=np.float32) * nat3 * 0.7)
result = np.clip(result, 0, 255).astype(np.uint8)

prov_only = prov * (nat == 0).astype(np.uint8)
prov3 = np.stack([prov_only]*3, axis=-1).astype(np.float32)
result = (result.astype(np.float32) * (1 - prov3*0.3))
result = np.clip(result, 0, 255).astype(np.uint8)

# City labels
font = cv2.FONT_HERSHEY_SIMPLEX
capitals = {"paris", "london", "lower_austria", "brandenburg", "moscow", "castile", "thrace"}

for i in range(len(matches)):
    nx = positions_img[i][0]
    img_ny = positions_img[i][1]
    px = int(nx * W)
    py = H - 1 - int(img_ny * H)
    if px < 0 or px >= W or py < 0 or py >= H:
        continue
    
    is_cap = prov_ids[i] in capitals
    col = colors_list[i]
    r = 5 if is_cap else 3
    cv2.circle(result, (px, py), r+1, (0,0,0), -1)
    cv2.circle(result, (px, py), r, (255,255,255), -1)
    
    fs = 0.38 if is_cap else 0.26
    label = city_names[i]
    (tw,th),_ = cv2.getTextSize(label, font, fs, 1)
    tx, ty = px - tw//2, py - 8
    cv2.putText(result, label, (tx,ty), font, fs, (0,0,0), 3, cv2.LINE_AA)
    cv2.putText(result, label, (tx,ty), font, fs, (255,255,255), 1, cv2.LINE_AA)

out = r'C:\Users\loicr\.gemini\antigravity\brain\e3b2b779-3de8-42d1-aea7-995e32ff8ec9\map_preview.png'
cv2.imwrite(out, result)
print(f"Done! {W}x{H}")
