"""Genereaza poze demo in folderul PozeDemo (fallback procedural).

NOTA (11.10.2026): folderul PozeDemo contine acum FOTOGRAFII REALE
(Echip01..Echip10_*.jpg, vezi PozeDemo/README.md + SURSE_FOTO.md).
Acest script ramane doar ca fallback offline — ruleaza-l doar daca vrei
sa regenerezi desenele procedurale. Numele de iesire este EchipNN_*.
"""
import os
from PIL import Image, ImageDraw, ImageFont

W, H = 1024, 768
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'PozeDemo')
OUT = os.path.abspath(OUT)
os.makedirs(OUT, exist_ok=True)

PROPS = [
    (1, 'PC Gaming', 'Bucuresti', 'PC-01 — Str. Mihai Viteazu 10'),
    (2, 'Consola', 'Cluj-Napoca', 'CON-01 — Str. Lalelelor 22'),
    (3, 'Server', 'Timisoara', 'SRV-01 — Str. Campului 5'),
    (4, 'Simulator', 'Bucuresti', 'SIM-01 — Bd. Unirii 30'),
    (5, 'PC Gaming', 'Iasi', 'PC-02 — Str. Pacurari 88'),
    (6, 'VR', 'Brasov', 'VR-01 — Str. Republicii 14'),
    (7, 'PC Gaming', 'Oradea', 'PC-03 — Calea Bucuresti 120'),
    (8, 'Consola', 'Constanta', 'CON-02 — Str. Primaverii 3'),
    (9, 'PC Gaming', 'Brasov', 'PC-04 — Str. Lunga 45'),
    (10, 'Server', 'Sibiu', 'SRV-02 — Str. Plopilor 60'),
]

# Desenul reutilizeaza paletele vechiului domeniu (cladiri) ca fundal
# decorativ pentru statiile clubului.
TIP_DESEN = {
    'PC Gaming': 'Apartament',
    'Consola': 'Casa',
    'Server': 'Teren',
    'Simulator': 'Spatiu comercial',
    'VR': 'Birou',
}

PALETE = {
    'Apartament': dict(cer1=(66, 165, 245), cer2=(227, 242, 253),
                       sol=(158, 158, 158), clad=(250, 250, 250),
                       acop=(63, 81, 181), acc=(255, 143, 0)),
    'Casa': dict(cer1=(79, 195, 247), cer2=(220, 245, 255),
                 sol=(124, 179, 66), clad=(255, 243, 224),
                 acop=(191, 54, 12), acc=(46, 125, 50)),
    'Teren': dict(cer1=(41, 182, 246), cer2=(225, 245, 254),
                  sol=(102, 187, 106), clad=(165, 214, 167),
                  acop=(27, 94, 32), acc=(93, 64, 55)),
    'Spatiu comercial': dict(cer1=(92, 107, 192), cer2=(232, 234, 246),
                             sol=(144, 164, 174), clad=(255, 236, 179),
                             acop=(255, 111, 0), acc=(13, 71, 161)),
    'Birou': dict(cer1=(55, 71, 79), cer2=(207, 216, 220),
                  sol=(96, 125, 139), clad=(144, 202, 249),
                  acop=(13, 71, 161), acc=(255, 193, 7)),
}
INTERIOR = dict(cer1=(239, 230, 216), cer2=(224, 208, 184),
                sol=(141, 110, 99), clad=(255, 248, 231),
                acop=(161, 136, 127), acc=(46, 125, 50))


def font(size, bold=False):
    for name in (('segoeui' + ('b' if bold else '') + '.ttf'),):
        for d in (r'C:\Windows\Fonts', '/usr/share/fonts'):
            p = os.path.join(d, name)
            if os.path.exists(p):
                try:
                    return ImageFont.truetype(p, size)
                except Exception:
                    pass
    return ImageFont.load_default()


def gradient(img, top, bottom, y0=0, y1=H):
    d = ImageDraw.Draw(img)
    for y in range(y0, y1):
        f = (y - y0) / max(1, (y1 - 1 - y0))
        c = tuple(int(top[i] + (bottom[i] - top[i]) * f) for i in range(3))
        d.line([(0, y), (W, y)], fill=c)


def sun_clouds(d):
    d.ellipse([830, 30, 960, 160], fill=(255, 255, 255, 60))
    d.ellipse([855, 55, 935, 135], fill=(255, 242, 138))
    for x, y, w in ((120, 70, 130), (620, 120, 120)):
        d.ellipse([x, y, x + w, y + 44], fill=(255, 255, 255, 200))
        d.ellipse([x + 50, y - 15, x + 50 + 110, y + 29], fill=(255, 255, 255, 200))


def tree(d, x, y, s=1.0):
    d.rectangle([x, y + 70 * s, x + 22 * s, y + 140 * s], fill=(93, 64, 55))
    d.ellipse([x - 45 * s, y, x + 67 * s, y + 95 * s], fill=(46, 125, 50))


def exterior(d, tip, P):
    gy = H - 190
    if tip == 'Casa':
        x, y, w, h = 312, 250, 400, 330
        d.rectangle([x, y + 110, x + w, y + h], fill=P['clad'])
        d.polygon([(x - 30, y + 115), (x + w / 2, y), (x + w + 30, y + 115)],
                  fill=P['acop'])
        d.rectangle([x + w - 110, y + 18, x + w - 76, y + 88], fill=(109, 76, 65))
        d.rectangle([x + w / 2 - 45, y + h - 140, x + w / 2 + 45, y + h],
                    fill=(93, 64, 55))
        for fx in (x + 40, x + w - 130):
            d.rectangle([fx, y + 170, fx + 90, y + 250], fill=(187, 222, 251),
                        outline=(141, 110, 99), width=3)
        for gx in range(x - 90, x + w + 90, 34):
            d.rectangle([gx, y + h - 60, gx + 16, y + h], fill=(141, 110, 99))
        d.rectangle([x - 100, y + h - 62, x + w + 100, y + h - 50],
                    fill=(141, 110, 99))
    elif tip == 'Teren':
        for gx in range(60, W - 40, 90):
            d.rectangle([gx, H - 330, gx + 14, H - 180], fill=P['acc'])
        d.rectangle([50, H - 320, W - 50, H - 308], fill=P['acc'])
        d.rectangle([50, H - 260, W - 50, H - 248], fill=P['acc'])
        d.rectangle([362, 300, 662, 420], fill='white', outline=P['acc'], width=4)
        d.text((435, 335), 'TEREN', font=font(30, True), fill=P['acc'])
    elif tip == 'Spatiu comercial':
        x, y, w, h = 232, 260, 560, 320
        d.rectangle([x, y, x + w, y + h], fill=P['clad'],
                    outline=P['acc'], width=5)
        dw = w // 10
        for i in range(10):
            d.rectangle([x + i * dw, y, x + (i + 1) * dw, y + 54],
                        fill=P['acop'] if i % 2 == 0 else 'white')
        d.rectangle([x, y, x + w, y + 54], outline=P['acc'], width=3)
        d.rectangle([x + 30, y + 110, x + 220, y + 240], fill=(187, 222, 251))
        d.rectangle([x + w - 220, y + 110, x + w - 30, y + 240],
                    fill=(187, 222, 251))
        d.rectangle([x + w / 2 - 55, y + 110, x + w / 2 + 55, y + 320],
                    fill=P['acc'])
    elif tip == 'Birou':
        x, y, w, h = 352, 130, 320, 450
        d.rectangle([x, y, x + w, y + h], fill=P['clad'],
                    outline=P['acop'], width=5)
        rows, cols, mx, my = 7, 4, 22, 22
        gw = (w - mx * (cols + 1)) // cols
        gh = (h - 120 - my * (rows + 1)) // rows
        for r in range(rows):
            for c in range(cols):
                d.rectangle([x + mx + c * (gw + mx), y + 24 + r * (gh + my),
                             x + mx + c * (gw + mx) + gw, y + 24 + r * (gh + my) + gh],
                            fill=P['acop'])
        d.rectangle([x + w / 2 - 60, y + h - 90, x + w / 2 + 60, y + h],
                    fill=P['acc'])
    else:  # Apartament (bloc)
        x, y, w, h = 292, 150, 440, 430
        d.rectangle([x, y, x + w, y + h], fill=P['clad'],
                    outline=P['acop'], width=5)
        d.rectangle([x - 12, y - 26, x + w + 12, y], fill=P['acop'])
        cols, rows, mx, my, gh = 4, 3, 28, 30, 62
        gw = (w - mx * (cols + 1)) // cols
        for r in range(rows):
            for c in range(cols):
                fx, fy = x + mx + c * (gw + mx), y + 34 + r * (gh + my)
                d.rectangle([fx, fy, fx + gw, fy + gh], fill=(187, 222, 251),
                            outline=(144, 164, 174), width=2)
                d.line([(fx + gw / 2, fy), (fx + gw / 2, fy + gh)],
                       fill=(144, 164, 174), width=2)
        d.rectangle([x + w / 2 - 45, y + h - 110, x + w / 2 + 45, y + h],
                    fill=P['acc'])
    tree(d, 120, H - 300)
    tree(d, W - 180, H - 320)


def interior(d, tip, P):
    d.rectangle([60, 130, W - 60, 460], fill=P['clad'])
    d.rectangle([60, 460, W - 60, 660], fill=P['sol'])
    d.rectangle([60, 130, W - 60, 660], outline=P['acop'], width=4)
    d.rectangle([130, 180, 370, 370], fill=(187, 222, 251),
                outline=P['acop'], width=6)
    d.line([(250, 180), (250, 370)], fill=P['acop'], width=6)
    d.line([(130, 275), (370, 275)], fill=P['acop'], width=6)
    d.rectangle([470, 400, 800, 510], fill=P['acop'])
    d.rectangle([450, 330, 510, 510], fill=P['acop'])
    d.rectangle([760, 330, 820, 510], fill=P['acop'])
    d.rectangle([500, 420, 580, 480], fill=P['acc'])
    d.rectangle([600, 420, 680, 480], fill=P['acc'])
    d.rectangle([480, 530, 780, 552], fill=(109, 76, 65))
    d.rectangle([500, 552, 516, 612], fill=(109, 76, 65))
    d.rectangle([744, 552, 760, 612], fill=(109, 76, 65))
    d.rectangle([880, 380, 894, 530], fill=(109, 76, 65))
    d.polygon([(845, 380), (929, 380), (915, 320), (859, 320)],
              fill=(255, 242, 138))
    d.rectangle([640, 190, 890, 310], outline=P['acc'], width=4)
    d.text((655, 225), tip.upper(), font=font(20, True), fill=P['acc'])


def band(d, tip, oras, adresa, pid, vedere):
    d.rectangle([0, H - 120, W, H], fill=(20, 20, 20, 210))
    d.rectangle([30, H - 100, 280, H - 60], fill=(255, 143, 0))
    d.text((42, H - 96), 'ID %02d - %s' % (pid, vedere), font=font(18, True),
           fill='white')
    d.text((300, H - 108), ('%s - %s' % (tip, oras)).strip(' -'),
           font=font(30, True), fill='white')
    if adresa:
        d.text((302, H - 60), adresa + '  -  imagine demo', font=font(19),
               fill='white')


def make(pid, tip, oras, adresa, vedere):
    img = Image.new('RGB', (W, H), 'white')
    desen = TIP_DESEN.get(tip, 'Apartament')
    P = INTERIOR if vedere == 'Interior' else PALETE[desen]
    d = ImageDraw.Draw(img)
    if vedere == 'Interior':
        d.rectangle([0, 0, W, H], fill=P['cer2'])
        interior(d, tip, P)
    else:
        gradient(img, P['cer1'], P['cer2'])
        d = ImageDraw.Draw(img)
        d.rectangle([0, H - 190, W, H], fill=P['sol'])
        sun_clouds(d)
        exterior(d, TIP_DESEN.get(tip, 'Apartament'), P)
    band(d, tip, oras, adresa, pid, vedere)
    return img


def main():
    n = 0
    for pid, tip, oras, adresa in PROPS:
        for vedere in ('Exterior', 'Interior'):
            img = make(pid, tip, oras, adresa, vedere)
            nume = 'Echip%02d_%s.jpg' % (pid, vedere)
            cale = os.path.join(OUT, nume)
            img.save(cale, 'JPEG', quality=90)
            n += 1
            print('scris', cale)
    print('GATA: %d fisiere in %s' % (n, OUT))


if __name__ == '__main__':
    main()
