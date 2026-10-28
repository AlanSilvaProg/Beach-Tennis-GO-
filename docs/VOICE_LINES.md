# Locuções do Beach Tennis GO!

Arquivos em `Assets/StreamingAssets/Voice/<en|pt|es>/Voice_<Evento>_<1-3>.mp3` (formato recomendado: **MP3 mono, 22,05 kHz, 32 kbps** — ~3,5 KB por locução; também aceita `.wav` e `.ogg`, mas WAV é ~10x maior e `.ogg` não toca no Safari).
Cada idioma tem a sua pasta; o jogo baixa só a pasta do idioma do usuário (primeiro idioma do navegador entre pt, es e en; `?lang=pt|es|en` na URL força um).
Evento sem arquivo no idioma cai no inglês. Depois de trocar arquivos, `Tools > Beach Tennis GO > Update Voice Manifests` (também roda sozinho na build).

| Arquivo (`Voice_<Evento>_<n>`) | Quando toca | English (en) | Português BR (pt) | Español (es) |
|---|---|---|---|---|
| Smash | Golpe alto, de cima para baixo, muito rápido | 1 Smash! · 2 What a smash! · 3 Boom, smash! | 1 Smash! · 2 Que smash! · 3 Pancada, smash! | 1 ¡Smash! · 2 ¡Qué smash! · 3 ¡Remate! |
| DropShot | Deixadinha: cai curta e lenta logo após a rede | 1 Drop shot! · 2 Soft touch! · 3 Sneaky drop! | 1 Deixadinha! · 2 Que toque! · 3 Deixadinha de mestre! | 1 ¡Dejada! · 2 ¡Qué toque! · 3 ¡Dejadita suave! |
| Lob | Lobby: alto e profundo, adversário colado na rede | 1 Lob! · 2 Over their heads! · 3 High lob! | 1 Lobby! · 2 Por cima! · 3 Lobby alto! | 1 ¡Globo! · 2 ¡Por encima! · 3 ¡Globo alto! |
| Drive | Rápido, firme e reto, na altura do peito/ombro | 1 Drive! · 2 Straight drive! · 3 Strong drive! | 1 Drive! · 2 Drive firme! · 3 Drive reto! | 1 ¡Drive! · 2 ¡Drive firme! · 3 ¡Drive directo! |
| Acceleration | Muito rápido, para o meio ou as laterais | 1 Acceleration! · 2 Speed up! · 3 Fast ball! | 1 Aceleração! · 2 Acelerou! · 3 Bola rápida! | 1 ¡Aceleración! · 2 ¡Aceleró! · 3 ¡Bola rápida! |
| Block | Defesa: bola forte devolvida devagar | 1 Block! · 2 Great defense! · 3 Blocked! | 1 Bloqueio! · 2 Boa defesa! · 3 Bloqueou! | 1 ¡Bloqueo! · 2 ¡Gran defensa! · 3 ¡Bloqueado! |
| Out | A bola cai fora da quadra | 1 Out! · 2 Too long! · 3 Out of bounds! | 1 Fora! · 2 Bola fora! · 3 Passou da linha! | 1 ¡Fuera! · 2 ¡Bola fuera! · 3 ¡Se pasó! |
| Ace | Saque que cai no lado adversário sem ninguém tocar | 1 Ace! · 2 Untouchable! · 3 Ace, no chance! | 1 Ace! · 2 Indefensável! · 3 Ace, sem chance! | 1 ¡Ace! · 2 ¡Imparable! · 3 ¡Ace, sin opción! |
| NoAd | Início do ponto em 15x15 (o próximo ponto vence) | 1 No-Ad point! · 2 Next point wins! · 3 Sudden death! | 1 Ponto decisivo! · 2 Próximo ponto vence! · 3 Morte súbita! | 1 ¡Punto de oro! · 2 ¡El próximo punto gana! · 3 ¡Muerte súbita! |
| MatchPoint | Início do ponto que pode decidir a partida | 1 Match point! · 2 Match point, here we go! · 3 Game on the line! | 1 Match point! · 2 Ponto da vitória! · 3 Valendo o jogo! | 1 ¡Punto de partido! · 2 ¡Punto de partido, atención! · 3 ¡Se define el partido! |
| VeryGood | Golpe do jogador muito bom: rápido e cruza a quadra (também mostra "QUE GOLPE!" na tela e dá bônus na premiação) | 1 What a shot! · 2 Beautiful! · 3 Great hit! | 1 Que golpe! · 2 Lindo! · 3 Que pancada! | 1 ¡Qué golpe! · 2 ¡Hermoso! · 3 ¡Qué pelotazo! |
| Perfect | Golpe do jogador perfeito: o mais forte, atravessa a quadra até o fundo (mostra "PERFEITO!" e dá mais bônus) | 1 Perfect! · 2 Absolutely perfect! · 3 Flawless! | 1 Perfeito! · 2 Perfeito demais! · 3 Sem defeito! | 1 ¡Perfecto! · 2 ¡Perfectísimo! · 3 ¡Impecable! |

Notas: em 15x15 tocam `NoAd` e logo depois `MatchPoint`. Out, Ace, NoAd e MatchPoint cortam a fala anterior; os golpes (Smash a Block) respeitam um intervalo de 1,5 s. VeryGood e Perfect (só golpes do jogador) tomam o lugar do nome do golpe e têm intervalo de 1 s.

Geração automática: `scripts/generate-voices.py` (ElevenLabs `eleven_v4`, stability 0,5, similarity 0,75, style 0, `language_code` por idioma). Toda fala leva `[excited]` no começo. `[crowd cheering]` foi testado e removido (ficou estranho); o conjunto `CHEER` do script está vazio.
