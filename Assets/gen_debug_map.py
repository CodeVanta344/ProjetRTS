import re, cv2, numpy as np

with open(r'e:\FPSLowPoly\Assets\Scripts\Campaign\CampaignManager.cs', 'r', encoding='utf-8') as f:
    text = f.read()

pattern = r'AddProvince\(\s*"(\w+)"\s*,\s*"([^"]+)"\s*,\s*FactionType\.(\w+)\s*,\s*new\s+Vector2\((\d+\.\d+)f\s*,\s*(\d+\.\d+)f\)'
matches = re.findall(pattern, text)
print(f'Found {len(matches)} provinces')

FC = {
    'France': (200,60,20),'Britain': (40,40,200),'Prussia': (80,60,40),
    'Russia': (50,130,50),'Austria': (50,200,200),'Spain': (30,140,230),
    'Ottoman': (30,30,140),'Portugal': (100,140,40),'Sweden': (180,140,60),
    'Denmark': (200,160,100),'Poland': (150,80,180),'Venice': (180,180,40),
    'Dutch': (200,100,40),'Bavaria': (80,160,200),'Saxony': (100,100,160),
    'PapalStates': (120,120,200),'Savoy': (150,120,100),'Switzerland': (180,180,180),
    'Genoa': (130,140,60),'Tuscany': (60,160,200),'Hanover': (130,130,60),
    'Modena': (150,100,170),'Parma': (170,120,140),'Lorraine': (120,140,150),
}

N = 512  # Small for speed
pos_x = np.array([float(m[3]) for m in matches], dtype=np.float32)
pos_y = np.array([float(m[4]) for m in matches], dtype=np.float32)
cols = np.array([FC.get(m[2], (128,128,128)) for m in matches], dtype=np.uint8)

# Vectorized Voronoi
yy, xx = np.mgrid[0:N, 0:N]
nx = xx.astype(np.float32) / N
nz = yy.astype(np.float32) / N

nearest = np.zeros((N, N), dtype=np.int32)
min_dist = np.full((N, N), 1e9, dtype=np.float32)

for i in range(len(matches)):
    d = (nx - pos_x[i])**2 + (nz - pos_y[i])**2
    mask = d < min_dist
    min_dist[mask] = d[mask]
    nearest[mask] = i

# Build color image (flip Y: row 0 = north)
img = cols[nearest][::-1].copy()

# Upscale for labels
img = cv2.resize(img, (2048, 2048), interpolation=cv2.INTER_NEAREST)
S = 2048

font = cv2.FONT_HERSHEY_SIMPLEX
for i, m in enumerate(matches):
    cx, cy = float(m[3]), float(m[4])
    px = int(cx * S)
    py = S - 1 - int(cy * S)
    cv2.circle(img, (px, py), 4, (255,255,255), -1)
    cv2.circle(img, (px, py), 4, (0,0,0), 1)
    label = m[1]
    (tw, th), _ = cv2.getTextSize(label, font, 0.35, 1)
    cv2.putText(img, label, (px - tw//2, py - 8), font, 0.35, (0,0,0), 2)
    cv2.putText(img, label, (px - tw//2, py - 8), font, 0.35, (255,255,255), 1)

# Legend
ly = 25
seen = set()
for m in matches:
    f = m[2]
    if f not in seen:
        seen.add(f)
        c = FC.get(f, (128,128,128))
        cv2.rectangle(img, (8, ly-10), (22, ly+2), c, -1)
        cv2.rectangle(img, (8, ly-10), (22, ly+2), (0,0,0), 1)
        cv2.putText(img, f, (26, ly), font, 0.4, (255,255,255), 1)
        ly += 18

out = r'e:\FPSLowPoly\Assets\debug_country_map.png'
cv2.imwrite(out, img)
print(f'Done! Saved {out} ({img.shape})')
