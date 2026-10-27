#!/usr/bin/env python3
"""Gera efeitos sonoros curtos com a API de Sound Effects do ElevenLabs (POST /v1/sound-generation) e grava o MP3 em Assets/Audio/.

Uso:  ELEVEN_API_KEY=... scripts/generate-sfx.py [--name star-lost] [--key-file caminho] [--dry-run]
Hoje: star-lost (feedback negativo quando a CPU faz ponto no modo treino e uma estrela se esvazia).
O arquivo atual em Assets/Audio/star-lost.mp3 é um provisório sintetizado; rodar este script o sobrescreve (o GameManager.starLost aponta para o clipe).
A chave nunca é gravada aqui.
"""
import argparse, json, os, subprocess, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "Assets", "Audio")
SFX = {
    "star-lost": {
        "text": "short cartoon negative feedback sound for a lost point in a mobile game, soft descending two-note 'wah wah' on a muted trombone, "
                "playful, not harsh, clean, no reverb, no music",
        "duration_seconds": 1.0,
        "prompt_influence": 0.6,
    },
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", nargs="*", default=list(SFX))
    ap.add_argument("--key-file")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    key = open(args.key_file).read().strip() if args.key_file else os.environ.get("ELEVEN_API_KEY", "")
    if not key and not args.dry_run:
        sys.exit("Defina ELEVEN_API_KEY (ou use --key-file).")
    for name in args.name:
        spec = SFX[name]
        path = os.path.join(OUT, name + ".mp3")
        print("%s: %s" % (name, spec["text"]))
        if args.dry_run:
            continue
        r = subprocess.run(["curl", "-s", "-m", "90", "-w", "\n%{http_code}", "-X", "POST",
                            "https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_22050_32",
                            "-H", "xi-api-key: " + key, "-H", "Content-Type: application/json", "-d", json.dumps(spec)],
                           stdout=subprocess.PIPE, check=True)
        data, _, code = r.stdout.rpartition(b"\n")
        if code != b"200":
            sys.exit("ElevenLabs HTTP %s: %s" % (code.decode(), data.decode(errors="replace")[:300]))
        open(path, "wb").write(data)
        print("  ->", path, len(data), "bytes")


main()
