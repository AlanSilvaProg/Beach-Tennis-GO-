#!/usr/bin/env python3
"""Gera o WebGL template (Assets/WebGLTemplates/BeachTennis/index.html) com a tela de carregamento: fundo (Thumb.jpg desfocado),
Logo.png e a barra de progresso abaixo dele. Os assets são otimizados e embutidos no próprio HTML, então a tela aparece antes de qualquer
outro arquivo ser baixado (sem requisições extras):
  - Logo.png: recorta as margens transparentes, reduz para 360 px de largura e converte para WebP com alfa (cwebp) -> ~25 KB
  - Thumb.jpg: reduz para 64 px (o fundo é desfocado e esticado em tela cheia, então não precisa de mais) -> ~3 KB
Uso: scripts/build-webgl-template.py   (precisa do macOS `sips` e do `cwebp`: brew install webp)
Rode de novo ao trocar Sprites/Logo.png ou Sprites/Thumb.jpg."""
import base64, os, struct, subprocess, sys, tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
LOGO = os.path.join(ROOT, "Assets", "Sprites", "Logo.png")
THUMB = os.path.join(ROOT, "Assets", "Sprites", "Thumb.jpg")
TEMPLATE = os.path.join(ROOT, "scripts", "webgl-template", "index.template.html")
OUT = os.path.join(ROOT, "Assets", "WebGLTemplates", "BeachTennis", "index.html")
LOGO_WIDTH = 360
THUMB_SIZE = 64


def alpha_bbox(png, tmp):
    """Caixa do conteúdo visível do PNG (via BMP de 32 bits gerado pelo sips)."""
    bmp = os.path.join(tmp, "logo.bmp")
    subprocess.run(["sips", "-s", "format", "bmp", png, "--out", bmp], check=True, stdout=subprocess.DEVNULL)
    d = open(bmp, "rb").read()
    off = struct.unpack("<I", d[10:14])[0]
    w, h = struct.unpack("<ii", d[18:26])
    bpp = struct.unpack("<H", d[28:30])[0]
    if bpp != 32:
        return 0, 0, w, abs(h)
    height = abs(h)
    row_size = ((w * bpp + 31) // 32) * 4
    x0, y0, x1, y1 = w, height, -1, -1
    for row in range(height):
        y = row if h < 0 else height - 1 - row
        base = off + row * row_size
        for x in range(w):
            if d[base + x * 4 + 3] > 12:
                x0, x1, y0, y1 = min(x0, x), max(x1, x), min(y0, y), max(y1, y)
    return x0, y0, x1 - x0 + 1, y1 - y0 + 1


def b64(path):
    return base64.b64encode(open(path, "rb").read()).decode()


def main():
    with tempfile.TemporaryDirectory() as tmp:
        x, y, w, h = alpha_bbox(LOGO, tmp)
        logo_h = round(LOGO_WIDTH * h / w)
        webp = os.path.join(tmp, "logo.webp")
        subprocess.run(["cwebp", "-quiet", "-q", "74", "-alpha_q", "85", "-m", "6", "-crop", str(x), str(y), str(w), str(h),
                        "-resize", str(LOGO_WIDTH), str(logo_h), LOGO, "-o", webp], check=True)
        thumb = os.path.join(tmp, "thumb.jpg")
        subprocess.run(["sips", "-Z", str(THUMB_SIZE), "-s", "format", "jpeg", "-s", "formatOptions", "55", THUMB, "--out", thumb],
                       check=True, stdout=subprocess.DEVNULL)
        html = open(TEMPLATE, encoding="utf-8").read()
        html = (html.replace("%%LOGO_DATA%%", b64(webp)).replace("%%THUMB_DATA%%", b64(thumb))
                .replace("%%LOGO_W%%", str(LOGO_WIDTH)).replace("%%LOGO_H%%", str(logo_h)))
        os.makedirs(os.path.dirname(OUT), exist_ok=True)
        open(OUT, "w", encoding="utf-8").write(html)
        print("logo %dx%d (crop %d,%d %dx%d) = %d bytes; thumb = %d bytes; index.html = %d bytes" %
              (LOGO_WIDTH, logo_h, x, y, w, h, os.path.getsize(webp), os.path.getsize(thumb), os.path.getsize(OUT)))


if __name__ == "__main__":
    main()
