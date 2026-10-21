#!/usr/bin/env python3
"""Gera as locuções (MP3 mono 22,05 kHz 32 kbps) com o ElevenLabs a partir de docs/VOICE_LINES.md.

Uso:  ELEVEN_API_KEY=... scripts/generate-voices.py [--lang en pt es] [--event Smash Ace ...] [--dry-run]
      (ou --key-file caminho, com a chave em um arquivo)
Saída: Assets/StreamingAssets/Voice/<lang>/Voice_<Evento>_<n>.mp3  (sobrescreve). Depois: Tools > Beach Tennis GO > Update Voice Manifests.
Requer o `lame` (brew install lame). A chave nunca é gravada aqui.
"""
import argparse, json, os, re, struct, subprocess, sys, tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DOC = os.path.join(ROOT, "docs", "VOICE_LINES.md")
OUT = os.path.join(ROOT, "Assets", "StreamingAssets", "Voice")
# eleven_v4 entende as audio tags ([excited], [crowd cheering]); os outros modelos leriam a tag em voz alta.
MODEL = "eleven_v4"
# Um voice id por idioma (troque aqui).
VOICES = {"en": "tnSpp4vdxKPjI9w0GnoV", "pt": "ORgG8rwdAiMYRug8RJwR", "es": "qWWAqFomnJ99VwQLREfT"}
# stability 0.5 = "Natural"; style 0 = "clone strength" zerado.
SETTINGS = {"stability": 0.5, "similarity_boost": 0.75, "style": 0.0, "use_speaker_boost": True}
TAG = "[excited]"
# Eventos que ganham [crowd cheering] depois da fala.
CHEER = set()  # ex.: {"Ace"}; ficou estranho em Smash/Ace/NoAd/MatchPoint, então está vazio
LANGS = ["en", "pt", "es"]
SR = 22050


def parse_table():
    lines = {}  # (event, lang) -> [phrase1, phrase2, phrase3]
    for row in open(DOC, encoding="utf-8"):
        cells = [c.strip() for c in row.strip().strip("|").split("|")]
        if len(cells) != 5 or not re.match(r"^[A-Za-z]+$", cells[0]) or cells[0] in ("Arquivo",):
            continue
        for lang, cell in zip(LANGS, cells[2:]):
            phrases = re.findall(r"(?:^|·\s*)\d\s+(.+?)(?=\s+·\s+\d\s|$)", cell)
            lines[(cells[0], lang)] = phrases
    return lines


def trim(pcm):
    """Corta o silêncio do começo/fim (a locução entra no instante do evento) e deixa 15 ms de folga."""
    n = len(pcm) // 2
    s = struct.unpack("<%dh" % n, pcm[: n * 2])
    thr = 600
    a = next((i for i, v in enumerate(s) if abs(v) > thr), 0)
    b = next((i for i in range(n - 1, -1, -1) if abs(s[i]) > thr), n - 1)
    pad = int(SR * 0.015)
    a, b = max(0, a - pad), min(n, b + pad)
    return struct.pack("<%dh" % (b - a), *s[a:b])


def tts(key, voice, text, lang):
    """Usa o curl (o Python do python.org no macOS costuma não ter certificados instalados)."""
    body = json.dumps({"text": text, "model_id": MODEL, "language_code": lang, "voice_settings": SETTINGS})
    r = subprocess.run(["curl", "-s", "-m", "60", "-w", "\n%{http_code}", "-X", "POST",
                        "https://api.elevenlabs.io/v1/text-to-speech/%s?output_format=pcm_22050" % voice,
                        "-H", "xi-api-key: " + key, "-H", "Content-Type: application/json", "-d", body],
                       stdout=subprocess.PIPE, check=True)
    data, _, code = r.stdout.rpartition(b"\n")
    if code != b"200":
        sys.exit("ElevenLabs HTTP %s: %s" % (code.decode(), data.decode(errors="replace")[:300]))
    return data


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--lang", nargs="*", default=LANGS)
    ap.add_argument("--event", nargs="*")
    ap.add_argument("--key-file")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    key = open(args.key_file).read().strip() if args.key_file else os.environ.get("ELEVEN_API_KEY", "")
    if not key and not args.dry_run:
        sys.exit("Defina ELEVEN_API_KEY (ou use --key-file).")
    lines = parse_table()
    done = 0
    for (event, lang), phrases in sorted(lines.items()):
        if lang not in args.lang or (args.event and event not in args.event):
            continue
        os.makedirs(os.path.join(OUT, lang), exist_ok=True)
        for n, text in enumerate(phrases, 1):
            path = os.path.join(OUT, lang, "Voice_%s_%d.mp3" % (event, n))
            spoken = "%s %s%s" % (TAG, text, " [crowd cheering]" if event in CHEER else "")
            print("%s: %r -> %s" % (lang, spoken, os.path.relpath(path, ROOT)))
            if args.dry_run:
                continue
            pcm = trim(tts(key, VOICES[lang], spoken, lang))
            with tempfile.NamedTemporaryFile(suffix=".pcm", delete=False) as f:
                f.write(pcm)
            subprocess.run(["lame", "-S", "-r", "-s", "22.05", "--bitwidth", "16", "--signed", "--little-endian",
                            "-m", "m", "-b", "32", f.name, path], check=True, stderr=subprocess.DEVNULL)
            os.unlink(f.name)
            done += 1
    print("Pronto: %d arquivo(s) gerado(s)." % done)


if __name__ == "__main__":
    main()
