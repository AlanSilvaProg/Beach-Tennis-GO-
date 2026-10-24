# Poki SDK — Beach Tennis GO!

- **Game ID:** `761a602c-88ba-435a-8df8-d5fdbe420364` (`PokiService.GameId`). O SDK HTML5 não pede o id em nenhuma chamada usada aqui (`PokiSDK.init()` não recebe parâmetros e a URL do script não tem id); ele serve para os serviços por jogo da Poki, como o AUDS (dados do jogador / ranking), se forem adicionados depois.
- **Teste oficial:** suba a build no Poki Inspector (https://inspector.poki.dev/) e depois peça a revisão.

Requisitos obrigatórios de SDK (loading, gameplay start/stop, commercial break) e um anúncio premiado (Retry na tela de derrota). Sem contas, ranking nem save na nuvem.

## Onde está
- **Script do SDK:** `Assets/WebGLTemplates/BeachTennis/index.html` (gerado por `scripts/build-webgl-template.py` a partir de `scripts/webgl-template/index.template.html`), tag `<script src="https://game-cdn.poki.com/scripts/v2/poki-sdk.js">` — vem da CDN oficial, não é empacotado.
- **Ponte na página:** `window.PokiBridge` (mesmo HTML). Decide quando falar com o SDK; tudo é seguro sem SDK (outro host, bloqueador de anúncios, rede lenta): o jogo segue e o anúncio termina na hora.
- **C# → página:** `Assets/Plugins/WebGL/PokiBridge.jslib`.
- **Lado do jogo:** `Assets/Scripts/PokiService.cs` (objeto `PokiService` na cena; o nome é usado pela página em `SendMessage`) e as chamadas em `GameManager`.
- **Menu:** Tools > Beach Tennis GO > Add Poki SDK (o Setup completo também faz).

## Fluxo
| Momento | Chamada |
|---|---|
| Página abre | `init` do SDK + `gameLoadingStart` |
| Unity terminou de carregar | `gameLoadingFinished` |
| Jogador clica em jogar (primeiro input) / começa a partida | `gameplayStart` |
| Partida acaba (vitória ou derrota) | `gameplayStop` |
| Clica para a próxima partida na tela de fim | `commercialBreak` → depois `StartMatch` → `gameplayStart` |
| Aba escondida / janela sem foco | `gameplayStop`; ao voltar, `gameplayStart` só se a partida ainda está em andamento |

Garantias (testadas com um SDK simulado):
- `gameplayStart`/`gameplayStop` nunca se repetem em sequência (estado derivado: "jogo quer jogar" E "página visível" E "sem anúncio"; só envia quando o resultado muda).
- Nenhum evento do SDK durante o anúncio. O jogo também ignora input e fica mudo (`AudioListener.volume = 0`) enquanto `PokiService.AdPlaying`.
- `commercialBreak` só ao voltar para o gameplay (da tela de fim para a próxima partida), nunca ao ir para menu.
- Falha de anúncio/SDK é silenciosa (sem mensagem de ad-block, sem timer interno de anúncios). Teto de 90 s para um anúncio que não termina.
- Anúncios só dentro da Poki (iframe ou host com "poki"); em hosts locais (localhost/IP privado) o SDK entra em modo debug com anúncios simulados, para testar o fluxo. Em outros hosts (ex.: GitHub Pages) o intervalo termina na hora.
- Página: espaço, setas ↑↓ e a roda do mouse não rolam a página.

## Tela de derrota: RESTART e RETRY (rewarded)
Ao perder a partida a tela mostra, juntos, dois botões (objeto `LoseButtons` no HUD, ajustável na cena):
- **RESTART** (padrão, azul, maior, em cima): recomeça tudo do primeiro nível (`level = startLevel`, placar zerado). Passa por um `commercialBreak` antes de voltar ao gameplay. Sempre disponível, sem anúncio premiado.
- **RETRY** (premiado, vermelho, menor, abaixo, com ícone de vídeo — nunca verde): `PokiSDK.rewardedBreak`; só se o jogador assistiu até o fim joga **o mesmo nível** de novo (placar zerado) e aparece o aviso "Retry!" no saque. Sem anúncio / bloqueador / fora da Poki / erro = nada acontece (sem recompensa e sem mensagem). Não dá para pagar duas vezes (`PokiService.AdPlaying` bloqueia um segundo clique).
- Os botões só ficam clicáveis depois de `matchOverInputDelay` e nunca durante um anúncio. Durante o anúncio o jogo fica mudo e sem input, sem eventos do SDK.
- No Editor a recompensa é concedida na hora, para testar. Em hosts locais (localhost/IP privado) o SDK roda em debug com anúncios simulados; fora da Poki em outros hosts (ex.: GitHub Pages) o Retry não dá recompensa.
- Menu: Tools > Beach Tennis GO > Add Lose Buttons (cria também o EventSystem e o GraphicRaycaster, necessários para os cliques).

## Checklist dos requisitos (QA da Poki)
- Desktop/mobile/tablet: mouse e toque; tablet usa o controle por toque (`Application.isMobilePlatform`).
- 16:9: o canvas cobre a janela toda e a câmera se adapta ao formato da tela.
- Incognito: o jogo não usa localStorage/PlayerPrefs; o cache de dados da Unity (IndexedDB) falha sem derrubar o jogo.
- Sem requisições externas: fontes, sons e vozes estão na build (`StreamingAssets`); só o script do SDK vem da CDN da Poki.
- Sem branding/anúncios externos, sem links externos (não precisa de política de privacidade in-game).
- Tela de carregamento com logo próprio e barra de progresso; download inicial pequeno (~8 MB zip; vozes só do idioma do jogador).
- Pendentes (fora do escopo desta etapa): thumbnails estática e animada, teste no ambiente real da Poki, save de progresso (não há progresso persistente; o jogo é partida a partida).

## Eventos customizados (comportamento do jogador)
`PokiEvents.Track(noun, verb, pares nome/valor...)` (`Assets/Scripts/PokiEvents.cs`, ponte `Assets/Plugins/WebGL/PokiEvents.jslib`) chama `PokiSDK.customEvent(noun, verb, dados)`. No editor só escreve no Console (`[PokiEvent] ...`); sem o SDK na página não faz nada. A lista completa fica no comentário do topo de `PokiEvents.cs`:
`game/loaded` · `ui/click` (todo botão) · `mode_menu/open|close` · `mode/select` · `settings/open|close|change` · `match/tap_to_play|start|end|continue|restart|retry|back` · `set/end` · `level/reached|complete|failed`.
Ao criar um evento novo, acrescente a linha no comentário de `PokiEvents.cs` e aqui.
