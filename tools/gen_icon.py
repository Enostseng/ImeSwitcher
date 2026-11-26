# 產生 ImeSwitcher 的程式圖示 assets/app.ico（以及預覽用 PNG）
# 設計：圓角方塊沿對角線一分為二，左上藍底白字「繁」、右下紅底白字「簡」，
#       顏色與系統匣圖示一致（藍 = 繁體、紅 = 簡體）。
# 小尺寸（16、24）字太小看不清，改成只有對角色塊和一道白色分隔線。
# 字型：Noto Sans CJK TC Bold（SIL Open Font License 1.1）
# 用法：python3 tools/gen_icon.py <字型檔.ttc> <輸出資料夾>
import sys, os
from PIL import Image, ImageDraw, ImageFont

FONT, OUT = sys.argv[1], sys.argv[2]
BLUE = (40, 110, 200, 255)
RED = (200, 60, 50, 255)
WHITE = (255, 255, 255, 255)
SS = 4  # 超取樣倍率

def tc_font(px):
    # .ttc 內有多個字型，找出繁體中文（TC）那一個
    for i in range(20):
        try:
            f = ImageFont.truetype(FONT, px, index=i)
        except OSError:
            break
        if 'TC' in ' '.join(f.getname()):
            return f
    raise SystemExit('找不到 Noto Sans CJK TC')

def render(size):
    S = size * SS
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    # 對角線分割的兩個色塊
    color = Image.new('RGBA', (S, S), RED)
    d = ImageDraw.Draw(color)
    d.polygon([(0, 0), (S, 0), (0, S)], fill=BLUE)
    # 白色對角分隔線（小尺寸也保留，讓輪廓清楚）
    gap = max(SS, int(S * (0.035 if size >= 48 else 0.06)))
    d.line([(S + gap, -gap), (-gap, S + gap)], fill=WHITE, width=gap)
    # 圓角遮罩
    mask = Image.new('L', (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)
    img.paste(color, (0, 0), mask)
    if size >= 32:
        d = ImageDraw.Draw(img)
        fpx = int(S * (0.40 if size >= 48 else 0.46))
        font = tc_font(fpx)
        for ch, (cx, cy) in (('繁', (0.31, 0.31)), ('簡', (0.69, 0.69))):
            d.text((S * cx, S * cy), ch, font=font, fill=WHITE, anchor='mm')
    return img.resize((size, size), Image.LANCZOS)

os.makedirs(OUT, exist_ok=True)
sizes = [256, 128, 64, 48, 32, 24, 16]
imgs = [render(s) for s in sizes]
imgs[0].save(os.path.join(OUT, 'app.ico'), format='ICO',
             sizes=[(s, s) for s in sizes], append_images=imgs[1:])
imgs[0].save(os.path.join(OUT, 'app_256.png'))
# 預覽圖：各尺寸並排（淺色、深色背景各一列）
W = sum(sizes) + 20 * (len(sizes) + 1)
prev = Image.new('RGBA', (W, 2 * 256 + 60), (255, 255, 255, 255))
ImageDraw.Draw(prev).rectangle([0, 256 + 30, W, 2 * 256 + 60], fill=(32, 32, 32, 255))
x = 20
for s, im in zip(sizes, imgs):
    prev.paste(im, (x, 10 + 256 - s), im)
    prev.paste(im, (x, 256 + 40 + 256 - s), im)
    x += s + 20
prev.save(os.path.join(OUT, 'icon_preview.png'))
print('ok', [im.size for im in imgs])
