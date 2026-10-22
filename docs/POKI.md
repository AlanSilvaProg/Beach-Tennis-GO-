# Poki SDK — Beach Tennis GO!

Só os requisitos obrigatórios de SDK por enquanto (loading, gameplay start/stop, commercial break). Sem rewarded, contas, ranking nem save na nuvem.

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

## Checklist dos requisitos (QA da Poki)
- Desktop/mobile/tablet: mouse e toque; tablet usa o controle por toque (`Application.isMobilePlatform`).
- 16:9: o canvas cobre a janela toda e a câmera se adapta ao formato da tela.
- Incognito: o jogo não usa localStorage/PlayerPrefs; o cache de dados da Unity (IndexedDB) falha sem derrubar o jogo.
- Sem requisições externas: fontes, sons e vozes estão na build (`StreamingAssets`); só o script do SDK vem da CDN da Poki.
- Sem branding/anúncios externos, sem links externos (não precisa de política de privacidade in-game).
- Tela de carregamento com logo próprio e barra de progresso; download inicial pequeno (~8 MB zip; vozes só do idioma do jogador).
- Pendentes (fora do escopo desta etapa): thumbnails estática e animada, teste no ambiente real da Poki, save de progresso (não há progresso persistente; o jogo é partida a partida).
